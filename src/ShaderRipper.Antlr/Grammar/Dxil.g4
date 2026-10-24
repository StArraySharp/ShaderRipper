grammar Dxil;

// ---------------------------------------------------------------------------
// DXIL -- DirectX Intermediate Language, the LLVM-IR subset Microsoft defines
// for Shader Model 6 (docs/DXIL.rst in DirectXShaderCompiler).
//
// This is the text form dxc / dxbc2dxil prints, which is what the tool has to
// read: the game data in this repo ships no D3D12 blobs at all, so the corpus
// behind this grammar is 300 modules produced by converting the real DXBC to
// DXIL with dxbc2dxil.exe.
//
// DXIL is "a subset of LLVM IR constructs that makes sense for HLSL": no new
// instructions, no new fundamental types, everything shader-specific expressed
// through intrinsics (`@dx.op.*`) and metadata (`!dx.*`).  So the grammar is
// the LLVM IR subset plus the naming conventions:
//
//   * local ids    `%0`, `%dx.v32.r12.0`, `%if0.then`
//   * global ids   `@main`, `@dx.op.sample.f32`
//   * metadata     `!0`, `!dx.version`, `!{...}`
//   * attributes   `attributes #0 = { nounwind readonly }`
//
// Types are parsed as structure (`{ i8* }`, `[160 x i8]`, `%dx.types.Handle`)
// rather than as a keyword table, because a DXIL module may define any number
// of named types and the emitter uses them freely.
//
// Instructions are modelled by shape, not by opcode table: LLVM's instruction
// set is large and grows, and every instruction here is "result? = name
// operands+ (metadata)*".  The opcode is an identifier and the adapter checks
// it, exactly as for DXBC asm -- an unknown opcode must not be a parse error.
// ---------------------------------------------------------------------------

module
    : topLevel* EOF
    ;

topLevel
    : typeDef
    | comdatDef
    | globalDef
    | attributesDef
    | declareDef
    | functionDef
    | metadataDef
    | moduleAsm
    | SEMI
    ;

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

// `%dx.types.Handle = type { i8* }`, `%dx.types.Sampler = type opaque`
typeDef
    : LOCAL_ID ASSIGN TYPE typeValue
    ;

typeValue
    : OPAQUE
    | type
    ;

// LLVM builds types by postfixing: `float*` is a pointer to float, `void ()` is
// a function returning void, `void (i32)*` is a pointer to that.  Written as
// base + suffixes so the rule is not left-recursive.
type
    : baseType typeSuffix*
    ;

// A pointer suffix puts the address space *before* the star:
// `%dx.types.Sampler addrspace(1)*`, `i8*`.
typeSuffix
    : (ADDRSPACE LPAREN NUMBER RPAREN)? STAR
    | LPAREN (type (COMMA type)*)? RPAREN
    ;

baseType
    : VOID
    | primitiveType
    | namedType
    | structType
    | arrayType
    | vectorType
    ;

primitiveType
    : I1 | I8 | I16 | I32 | I64 | I128
    | HALF | BFLOAT | FLOAT | DOUBLE
    | X86_FP80 | FP128 | PPC_FP128
    | LABEL | METADATA | TOKEN | X86_MMX
    ;

namedType
    : LOCAL_ID
    ;

// `{ i8* }`, `{ float, float, float, float, i32 }`, `{ }`
structType
    : LBRACE (type (COMMA type)*)? RBRACE
    ;

// `[160 x i8]`, `[4 x float]`
arrayType
    : LBRACK NUMBER X type RBRACK
    ;

// `<4 x float>`
vectorType
    : LESS NUMBER X type GREATER
    ;

// ---------------------------------------------------------------------------
// Globals, declarations, definitions
// ---------------------------------------------------------------------------

// `@x = internal constant [4 x i32] [i32 1, i32 2]`
globalDef
    : GLOBAL_ID ASSIGN linkage* globalKind type initializer? (COMMA alignSpec)*
    ;

globalKind
    : GLOBAL
    | CONSTANT
    | IDENT
    ;

initializer
    : type value
    | structInitializer
    | arrayInitializer
    ;

structInitializer
    : LBRACE (type value (COMMA type value)*)? RBRACE
    ;

arrayInitializer
    : LBRACK (type value (COMMA type value)*)? RBRACK
    ;

linkage
    : PRIVATE
    | INTERNAL
    | AVAILABLE_EXTERNALLY
    | LINKONCE
    | WEAK
    | COMMON
    | APPENDING
    | EXTERN_WEAK
    | EXTERNAL
    | UNNAMED_ADDR
    | LOCAL_UNNAMED_ADDR
    | DLLIMPORT
    | DLLEXPORT
    | THREAD_LOCAL
    | LOCALDYNAMIC
    | LOCALEXEC
    | CONSTANT
    | GLOBAL
    ;

// `attributes #0 = { nounwind readonly }`
attributesDef
    : ATTRIBUTES ATTR_ID ASSIGN LBRACE attribute* RBRACE
    ;

attribute
    : IDENT (LPAREN attributeArg (COMMA attributeArg)* RPAREN)?
    | STRING
    | NUMBER
    ;

attributeArg
    : IDENT (ASSIGN attributeArg)?
    | STRING
    | NUMBER
    ;

// `declare %dx.types.Handle @dx.op.createHandle(i32, i8, i32, i32, i1) #0`
declareDef
    : DECLARE type GLOBAL_ID LPAREN (type (COMMA type)*)? RPAREN attributeGroupRef*
    ;

attributeGroupRef
    : ATTR_ID
    ;

// `define void @main() { entry: ... }`
functionDef
    : DEFINE linkage* type GLOBAL_ID LPAREN (parameter (COMMA parameter)*)? RPAREN attributeGroupRef* LBRACE basicBlock* RBRACE
    ;

parameter
    : type value attributeGroupRef*
    ;

// A basic block is a label plus instructions, ending in a terminator.
basicBlock
    : LABEL_ID instruction* terminator
    ;

// An LLVM instruction is "result? = opcode args", where the arguments are
// comma-separated and each one is usually a *typed* value: `add i32 %a, %b`,
// `fcmp olt float 0.0, %320`, `phi float [ %302, %entry ], [ %374, %if0.end ]`.
// `call` adds a parenthesised argument list, which is why callArgs exists.
instruction
    : resultAssign? opcode typedArgs? (COMMA metadataAttachment)*
    ;

resultAssign
    : LOCAL_ID ASSIGN
    ;

typedArgs
    : typedArg (COMMA typedArg)*
    ;

// One argument.  A typed argument is `type value`, and `call`/`getelementptr`
// append a parenthesised list to it *without a comma*:
//   call %dx.types.Handle @dx.op.createHandle(i32 57, i8 2, ...)
//   getelementptr inbounds ([4 x float], [4 x float]* %0, i64 0, i64 2)
// so the list belongs inside the same alternative rather than beside it.
// `icmp`/`fcmp` put a predicate *before* the type with no comma either:
//   icmp sge i32 %dx.v32.r11.0, %312
// hence the predicate alternative carries its own type.
typedArg
    : predicate type (value callArgs? | phiIncoming)?
    | type (value callArgs? | phiIncoming | callArgs)?
    | callArgs
    | phiIncoming
    | attributeGroupRef                // `#0`
    | value                            // `%5`, `1`, `null`
    | IDENT                            // instruction flags: `fast`, `inbounds`, `nuw`
    ;

// The parenthesised argument list of `call`, `invoke`, `getelementptr` and
// friends.  Its arguments are typed the same way as the flat ones.
callArgs
    : LPAREN (typedArg (COMMA typedArg)*)? RPAREN
    ;

// The opcode is any identifier; the adapter validates it.  `call` and a few
// others carry a type in front, which is modelled as an operand.
opcode
    : IDENT
    | CALL
    | INVOKE
    | LOAD
    | STORE
    | ALLOCA
    | GETELEMENTPTR
    | PHI
    | SELECT
    | ICMP
    | FCMP
    | EXTRACTVALUE
    | INSERTVALUE
    | EXTRACTELEMENT
    | INSERTELEMENT
    | SHUFFLEVECTOR
    | BR
    | SWITCH
    | RET
    | UNREACHABLE
    | RESUME
    | LANDINGPAD
    ;

// An operand is a type, a value, or one of the instruction-specific shapes
// (a phi pair, an fcmp predicate, a metadata attachment).
// `[ %302, %entry ]`
phiIncoming
    : LBRACK value COMMA value RBRACK
    ;

// `!dbg !7`
metadataAttachment
    : BANG_NAME value
    ;

// `olt`, `oge`, `eq`, `sge`, `ult`...
predicate
    : PREDICATE
    ;

terminator
    : BR typedArgs? (COMMA metadataAttachment)*
    | RET typedArgs? (COMMA metadataAttachment)*
    | SWITCH typedArgs? (COMMA metadataAttachment)*
    | UNREACHABLE (COMMA metadataAttachment)*
    | RESUME typedArgs? (COMMA metadataAttachment)*
    ;

// ---------------------------------------------------------------------------
// Values
// ---------------------------------------------------------------------------

value
    : LOCAL_ID
    | GLOBAL_ID
    | NUMBER
    | FLOATCONSTANT
    | HEXCONSTANT
    | TRUE
    | FALSE
    | NULL
    | UNDEF
    | ZEROINITIALIZER
    | NONE
    | POISON
    | STRING
    | LABEL_ID
    | BANG_NAME
    | structInitializer
    | arrayInitializer
    ;
// ---------------------------------------------------------------------------
// Metadata
// ---------------------------------------------------------------------------

// Metadata.  In the corpus every tuple is flat (`!0 = !{i32 1, !"vs", i32 6,
// i32 0}`), but the grammar recurses anyway: nested tuples are legal LLVM and
// the emitter is not the only producer these modules may come from.
metadataDef
    : BANG_NAME ASSIGN metadataTuple
    ;

metadataTuple
    : BANG_LBRACE (metadataItem (COMMA metadataItem)*)? RBRACE
    ;

metadataItem
    : BANG_STRING
    | BANG_NAME
    | metadataTuple
    | type value
    | NULL
    | UNDEF
    ;

// ---------------------------------------------------------------------------
// Misc
// ---------------------------------------------------------------------------

comdatDef
    : COMDAT_ID ASSIGN COMDAT_KIND (LPAREN STRING RPAREN)?
    ;
moduleAsm
    : MODULE_ASM STRING
    ;

alignSpec
    : ALIGN NUMBER
    ;

// ---------------------------------------------------------------------------
// Lexer
// ---------------------------------------------------------------------------

// Comments run to end of line and carry the block predecessors (`; preds = %x`),
// which is debug text rather than IR.
COMMENT : ';' ~[\r\n]* -> channel(HIDDEN) ;

WS : [ \t\r\n]+ -> skip ;

// Metadata tokens.  A tuple opener is its own token so the parser can recurse
// into nested tuples; the closer is the ordinary RBRACE.
BANG_LBRACE : '!{' ;
BANG_LBRACE_LT : '!<{' ;
BANG_STRING : '!"' ( '\\' . | ~["\\] )* '"' ;
BANG_NAME : '!' [A-Za-z0-9_$.]+ ;

// `%0`, `%dx.v32.r12.0`, `%if0.then`, `%"\01?main@@..."` (quoted names)
LOCAL_ID : '%' ( [0-9]+ | [A-Za-z_$][A-Za-z0-9_$.]* | '"' ( '\\' . | ~["\\] )* '"' ) ;

// `@main`, `@dx.op.sample.f32`, `@"quoted name"`
GLOBAL_ID : '@' ( [0-9]+ | [A-Za-z_$][A-Za-z0-9_$.]* | '"' ( '\\' . | ~["\\] )* '"' ) ;

ATTR_ID  : '#' [0-9]+ ;
COMDAT_ID : '$' [A-Za-z_$][A-Za-z0-9_$.]* ;
COMDAT_KIND : 'comdat' ;

// Float constants are printed with an explicit exponent (`1.000000e+00`,
// `-0.000000e+00`) or as a hex bit pattern (`0x3F800000`).
FLOATCONSTANT : '-'? ( [0-9]+ '.' [0-9]* | '.' [0-9]+ ) ( [eE] [+-]? [0-9]+ )? ;
HEXCONSTANT   : '-'? '0x' [0-9a-fA-F]+ ;
NUMBER        : '-'? [0-9]+ ;

STRING : '"' ( '\\' . | ~["\\] )* '"' ;

// Types.
VOID : 'void' ;
I1 : 'i1' ; I8 : 'i8' ; I16 : 'i16' ; I32 : 'i32' ; I64 : 'i64' ; I128 : 'i128' ;
HALF : 'half' ; BFLOAT : 'bfloat' ; FLOAT : 'float' ; DOUBLE : 'double' ;
X86_FP80 : 'x86_fp80' ; FP128 : 'fp128' ; PPC_FP128 : 'ppc_fp128' ;
LABEL : 'label' ; METADATA : 'metadata' ; TOKEN : 'token' ; X86_MMX : 'x86_mmx' ;
OPAQUE : 'opaque' ;

// Values and keywords.
TRUE : 'true' ; FALSE : 'false' ;
NULL : 'null' ; UNDEF : 'undef' ; POISON : 'poison' ;
ZEROINITIALIZER : 'zeroinitializer' ; NONE : 'none' ;
ALIGN : 'align' ; ADDRSPACE : 'addrspace' ; X : 'x' ;
TYPE : 'type' ; DECLARE : 'declare' ; DEFINE : 'define' ; ATTRIBUTES : 'attributes' ;
GLOBAL : 'global' ; CONSTANT : 'constant' ; MODULE_ASM : 'module asm' ;

// Linkage.
PRIVATE : 'private' ; INTERNAL : 'internal' ;
AVAILABLE_EXTERNALLY : 'available_externally' ; LINKONCE : 'linkonce' ;
WEAK : 'weak' ; COMMON : 'common' ; APPENDING : 'appending' ;
EXTERN_WEAK : 'extern_weak' ; EXTERNAL : 'external' ;
UNNAMED_ADDR : 'unnamed_addr' ; LOCAL_UNNAMED_ADDR : 'local_unnamed_addr' ;
DLLIMPORT : 'dllimport' ; DLLEXPORT : 'dllexport' ;
THREAD_LOCAL : 'thread_local' ; LOCALDYNAMIC : 'localdynamic' ;
LOCALEXEC : 'localexec' ;

// Opcodes that are also useful as keywords, because they start a terminator.
CALL : 'call' ; INVOKE : 'invoke' ; LOAD : 'load' ; STORE : 'store' ;
ALLOCA : 'alloca' ; GETELEMENTPTR : 'getelementptr' ; PHI : 'phi' ;
SELECT : 'select' ; ICMP : 'icmp' ; FCMP : 'fcmp' ;
EXTRACTVALUE : 'extractvalue' ; INSERTVALUE : 'insertvalue' ;
EXTRACTELEMENT : 'extractelement' ; INSERTELEMENT : 'insertelement' ;
SHUFFLEVECTOR : 'shufflevector' ;
BR : 'br' ; SWITCH : 'switch' ; RET : 'ret' ;
UNREACHABLE : 'unreachable' ; RESUME : 'resume' ; LANDINGPAD : 'landingpad' ;

// Comparison predicates.  `true`/`false` are deliberately absent: they are
// value keywords (TRUE/FALSE) and listing them here would make two tokens match
// the same text, which ANTLR rejects.
PREDICATE : 'eq' | 'ne' | 'ugt' | 'uge' | 'ult' | 'ule' | 'sgt' | 'sge' | 'slt' | 'sle'
          | 'oeq' | 'one' | 'ogt' | 'oge' | 'olt' | 'ole' | 'ord' | 'uno' | 'ueq' | 'une'
          ;

// Fast-math and other instruction flags.  Kept as a keyword so an identifier
// that happens to be one of these words cannot be confused with it.
IDENT : [A-Za-z_$] [A-Za-z0-9_$.]* ;

// A block label is an identifier followed immediately by a colon, so it is one
// token: that keeps `type` (a keyword) and a label called `type` from colliding,
// because only this rule can consume the colon.  Declared last so every keyword
// wins the tie against it.
LABEL_ID : ( [A-Za-z_$][A-Za-z0-9_$.]* | [0-9]+ ) ':' ;

LBRACE : '{' ;
RBRACE : '}' ;
LBRACK : '[' ;
RBRACK : ']' ;
LPAREN : '(' ;
RPAREN : ')' ;
LESS   : '<' ;
GREATER : '>' ;
COMMA  : ',' ;
STAR   : '*' ;
SEMI   : ';' ;
ASSIGN : '=' ;
COLON  : ':' ;
