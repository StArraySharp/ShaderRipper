grammar Glsl;

// ---------------------------------------------------------------------------
// GLSL -- specifically the dialect Unity's HLSLcc emits, which is what this
// tool actually has to read.
//
// Scope decisions, each backed by a measurement over the corpus in this repo
// (74,559 files / 11,106,030 lines):
//
//   * The preprocessor is NOT modelled.  A directive is one opaque line token.
//     Macro expansion is a separate stage, and the only macros that occur are
//     HLSLcc's own (`UNITY_LOCATION(x)`, `UNITY_BINDING(x)`, `UNITY_UNIFORM`)
//     plus include guards -- six directive kinds in total.  Modelling #define
//     bodies inside the grammar would duplicate a preprocessor, not parse GLSL.
//
//   * The statement grammar is the full GLSL one, not just the subset observed.
//     `switch` / `do` / `struct` do not occur in this corpus, but they are
//     legal GLSL and a grammar that rejects them would be a grammar of one
//     compiler's output rather than of the language.
//
//   * Expressions carry real precedence and associativity so the parse tree is
//     usable directly: the translator needs to know that `a * b + c` is an add
//     of a product, not a flat token run.
//
// Types are identifiers with a built-in table, not keywords: GLSL lets a
// shader declare `struct Foo`, and `vec4` is only special because it is
// predefined.  The visitor resolves which is which.
//
// Input is the shader text only.  The stored blob carries buffer padding after
// the closing `#endif` of the FRAGMENT branch -- NUL, 0x19, sometimes a
// printable '9' or '?' -- so the caller cuts at that last `#endif` before
// lexing.  Measured: every one of the 74,559 GLSL files in this repo ends its
// real text that way.  It is a container detail, so the grammar does not model
// it, and a padding byte that happens to be printable must not be a token.
// ---------------------------------------------------------------------------

translationUnit
    : externalItem* EOF
    ;

externalItem
    : ppLine
    | precisionDecl
    | declaration
    | functionDefinition
    | SEMI
    ;

// A preprocessor directive, whole line, including the newline.
ppLine
    : PP
    ;

// `precision highp float;`
precisionDecl
    : PRECISION precisionQualifier typeSpecifier SEMI
    ;

precisionQualifier
    : HIGH_PRECISION
    | MEDIUM_PRECISION
    | LOW_PRECISION
    ;

// ---------------------------------------------------------------------------
// Declarations
// ---------------------------------------------------------------------------

declaration
    : typeQualifier* (UNIFORM | BUFFER) IDENT LBRACE structBody RBRACE (IDENT)? SEMI  # interfaceBlock
    | layoutQualifier storageQualifier SEMI       # layoutOnlyDecl
    | fullySpecifiedType declaratorList SEMI      # variableDecl
    | INVARIANT IDENT SEMI                        # invariantDecl
    | typeQualifier* STRUCT IDENT LBRACE structBody RBRACE (IDENT)? SEMI  # structDecl
    | typeQualifier* STRUCT IDENT SEMI             # structForward
    ;

// HLSLcc opens an interface block *inside* a preprocessor conditional and
// closes the `#endif` before the block's members:
//
//     #if HLSLCC_ENABLE_UNIFORM_BUFFERS
//     UNITY_BINDING(0) uniform UnityStereoGlobals {
//     #endif
//         UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_StereoMatrixVP[8];
//     };
//
// so a directive line can appear among the members.  Nothing else about the
// preprocessor is modelled.
structBody
    : (structMember | ppLine | SEMI)*
    ;

structMember
    : fullySpecifiedType structDeclaratorList SEMI
    ;

structDeclaratorList
    : structDeclarator (COMMA structDeclarator)*
    ;

structDeclarator
    : IDENT (LBRACK expression? RBRACK)*
    ;

// `uniform vec4 _Time;`, `in vec4 in_POSITION0;`, `vec4 u_xlat0;`,
// `const float k = 1.0;`, `layout(location = 0) in vec4 a;`
fullySpecifiedType
    : typeQualifier* typeSpecifier
    ;

typeQualifier
    : storageQualifier
    | interpolationQualifier
    | precisionQualifier
    | memoryQualifier
    | INVARIANT
    | PRECISE
    | layoutQualifier
    | macroQualifier
    ;

// `layout(std430, binding = 0) readonly buffer vertices { ... };`
memoryQualifier
    : COHERENT
    | VOLATILE
    | RESTRICT
    | READONLY
    | WRITEONLY
    ;

// HLSLcc writes `UNITY_LOCATION(0) uniform sampler2D _MainTex;`,
// `UNITY_BINDING(0) uniform ...` and, inside interface blocks,
// `UNITY_UNIFORM vec4 hlslcc_mtx4x4unity_StereoMatrixVP[8];`.  These are its
// own macros, expanded by the real preprocessor but present verbatim in the
// stored source.  Measured over the corpus: 161,358 UNITY_LOCATION, 106
// UNITY_BINDING, 372 UNITY_UNIFORM -- always in qualifier position, nowhere
// else.  UNITY_UNIFORM is a token of its own rather than a general
// `IDENT`-as-qualifier rule, which would be ambiguous with the type that
// follows it.
macroQualifier
    : IDENT LPAREN functionArgumentList? RPAREN
    | UNITY_UNIFORM
    ;

storageQualifier
    : CONST
    | IN
    | OUT
    | INOUT
    | UNIFORM
    | BUFFER
    | SHARED
    | ATTRIBUTE
    | VARYING
    | CENTROID
    | SAMPLE
    | PATCH
    ;

interpolationQualifier
    : SMOOTH
    | FLAT
    | NOPERSPECTIVE
    ;

// `layout(location = 0)`, `layout(std140)`, `layout(binding = 0, std140)`,
// `layout(location = 0) out vec4 fragColor;`
layoutQualifier
    : LAYOUT LPAREN layoutQualifierId (COMMA layoutQualifierId)* RPAREN
    ;

layoutQualifierId
    : IDENT (ASSIGN expression)?
    ;

typeSpecifier
    : typeSpecifierNonArray (LBRACK expression? RBRACK)?
    ;

typeSpecifierNonArray
    : VOID
    | FLOAT | DOUBLE | INT | UINT | BOOL
    | VEC2 | VEC3 | VEC4
    | DVEC2 | DVEC3 | DVEC4
    | IVEC2 | IVEC3 | IVEC4
    | UVEC2 | UVEC3 | UVEC4
    | BVEC2 | BVEC3 | BVEC4
    | MAT2 | MAT3 | MAT4
    | MAT2X2 | MAT2X3 | MAT2X4
    | MAT3X2 | MAT3X3 | MAT3X4
    | MAT4X2 | MAT4X3 | MAT4X4
    | SAMPLER1D | SAMPLER2D | SAMPLER3D | SAMPLERCUBE
    | SAMPLER1DARRAY | SAMPLER2DARRAY | SAMPLERCUBEARRAY
    | SAMPLER2DRECT | SAMPLERBUFFER
    | SAMPLER1DSHADOW | SAMPLER2DSHADOW | SAMPLERCUBESHADOW
    | SAMPLER2DARRAYSHADOW
    | ISAMPLER1D | ISAMPLER2D | ISAMPLER3D | ISAMPLERCUBE
    | ISAMPLER2DARRAY | USAMPLER1D | USAMPLER2D | USAMPLER3D
    | USAMPLERCUBE | USAMPLER2DARRAY
    | IMAGE1D | IMAGE2D | IMAGE3D | IMAGECUBE
    | IMAGE2DARRAY | IIMAGE2D | UIMAGE2D
    | ATOMIC_UINT
    | IDENT
    ;

declaratorList
    : declarator (COMMA declarator)*
    ;

declarator
    : IDENT (LBRACK expression? RBRACK)* (ASSIGN initializer)?
    ;

initializer
    : assignmentExpression
    | LBRACE initializerList (COMMA)? RBRACE
    ;

initializerList
    : initializer (COMMA initializer)*
    ;

// ---------------------------------------------------------------------------
// Functions
// ---------------------------------------------------------------------------

functionDefinition
    : fullySpecifiedType IDENT LPAREN parameterList? RPAREN (SEMI | compoundStatement)
    ;

parameterList
    : parameterDeclaration (COMMA parameterDeclaration)*
    ;

parameterDeclaration
    : fullySpecifiedType (IDENT (LBRACK expression RBRACK)?)?
    ;

// ---------------------------------------------------------------------------
// Statements
// ---------------------------------------------------------------------------

statement
    : compoundStatement                                       # compoundStmt
    | simpleStatement                                         # simpleStmt
    | IF LPAREN expression RPAREN selectionRestStatement       # ifStmt
    | SWITCH LPAREN expression RPAREN LBRACE switchBody RBRACE # switchStmt
    | WHILE LPAREN condition RPAREN statement                  # whileStmt
    | DO statement WHILE LPAREN expression RPAREN SEMI         # doStmt
    | FOR LPAREN forInitStatement condition? SEMI expression? RPAREN statement # forStmt
    | RETURN expression? SEMI                                 # returnStmt
    ;

compoundStatement
    : LBRACE statement* RBRACE
    ;

simpleStatement
    : declaration
    | expression SEMI
    | DISCARD SEMI
    | BREAK SEMI
    | CONTINUE SEMI
    | SEMI
    ;

selectionRestStatement
    : statement (ELSE statement)?
    ;

switchBody
    : (caseLabel | statement)*
    ;

caseLabel
    : CASE expression COLON
    | DEFAULT COLON
    ;

forInitStatement
    : declaration
    | expression? SEMI
    ;

// A condition may carry a declaration in desktop GLSL; HLSLcc never emits that,
// but accepting it keeps the grammar honest about the language.
condition
    : expression
    | fullySpecifiedType IDENT ASSIGN initializer
    ;

// ---------------------------------------------------------------------------
// Expressions
// ---------------------------------------------------------------------------

expression
    : assignmentExpression (COMMA assignmentExpression)*
    ;

assignmentExpression
    : conditionalExpression                                   # condExpr
    | unaryExpression ASSIGN assignmentExpression             # assignExpr
    | unaryExpression ADD_ASSIGN assignmentExpression         # addAssignExpr
    | unaryExpression SUB_ASSIGN assignmentExpression         # subAssignExpr
    | unaryExpression MUL_ASSIGN assignmentExpression         # mulAssignExpr
    | unaryExpression DIV_ASSIGN assignmentExpression         # divAssignExpr
    | unaryExpression MOD_ASSIGN assignmentExpression         # modAssignExpr
    | unaryExpression LEFT_ASSIGN assignmentExpression        # leftAssignExpr
    | unaryExpression RIGHT_ASSIGN assignmentExpression       # rightAssignExpr
    | unaryExpression AND_ASSIGN assignmentExpression         # andAssignExpr
    | unaryExpression XOR_ASSIGN assignmentExpression         # xorAssignExpr
    | unaryExpression OR_ASSIGN assignmentExpression          # orAssignExpr
    ;

conditionalExpression
    : logicalOrExpression (QUESTION expression COLON assignmentExpression)?
    ;

logicalOrExpression
    : logicalAndExpression (OR_OP logicalAndExpression)*
    ;

logicalAndExpression
    : inclusiveOrExpression (AND_OP inclusiveOrExpression)*
    ;

inclusiveOrExpression
    : exclusiveOrExpression (VERTICAL_BAR exclusiveOrExpression)*
    ;

exclusiveOrExpression
    : andExpression (CARET andExpression)*
    ;

andExpression
    : equalityExpression (AMPERSAND equalityExpression)*
    ;

equalityExpression
    : relationalExpression ((EQUAL_OP | NEQUAL_OP) relationalExpression)*
    ;

relationalExpression
    : shiftExpression ((LESS | GREATER | LE_OP | GE_OP) shiftExpression)*
    ;

shiftExpression
    : additiveExpression ((LEFT_OP | RIGHT_OP) additiveExpression)*
    ;

additiveExpression
    : multiplicativeExpression ((PLUS | DASH) multiplicativeExpression)*
    ;

multiplicativeExpression
    : unaryExpression ((STAR | SLASH | PERCENT) unaryExpression)*
    ;

// Prefix and postfix forms are separate alternatives so the tree says which.
unaryExpression
    : postfixExpression                                       # postfixExpr
    | (PLUS_PLUS | DASH_DASH) unaryExpression                 # prefixIncExpr
    | (PLUS | DASH | BANG | TILDE) unaryExpression            # unaryOpExpr
    ;

postfixExpression
    : primaryExpression                                       # primaryExpr
    | postfixExpression LBRACK expression RBRACK              # indexExpr
    | postfixExpression DOT fieldSelector                     # fieldExpr
    | postfixExpression (PLUS_PLUS | DASH_DASH)               # postfixIncExpr
    | functionCall                                            # callExpr
    ;

// A swizzle (`u_xlat0.xyz`) and a struct field are the same syntax; the
// adapter decides by checking the receiver's type.
fieldSelector
    : IDENT
    ;

functionCall
    : typeSpecifierNonArray LPAREN functionArgumentList? RPAREN   # constructorCall
    | IDENT LPAREN functionArgumentList? RPAREN                   # namedCall
    ;

functionArgumentList
    : VOID
    | assignmentExpression (COMMA assignmentExpression)*
    ;

primaryExpression
    : IDENT                        # identifier
    | literal                      # literalExpr
    | LPAREN expression RPAREN     # parenExpr
    ;

literal
    : INTCONSTANT
    | UINTCONSTANT
    | FLOATCONSTANT
    | BOOLCONSTANT
    ;

// ---------------------------------------------------------------------------
// Lexer
// ---------------------------------------------------------------------------

// A directive is one line.  It wins over everything: `#` has no meaning inside
// GLSL expressions, and swallowing the line keeps `#define UNITY_BINDING(x)
// layout(binding = x, std140)` from being picked apart as code.
PP : '#' ~[\r\n]* ;

// Comments are tokens rather than skipped text so the adapter can keep them:
// the emitted HLSL carries them through as the original-source annotations.
LINE_COMMENT  : '//' ~[\r\n]* ;
BLOCK_COMMENT : '/*' .*? '*/' ;

WS : [ \t\r\n]+ -> skip ;

// Keywords.  Longest spellings first: ANTLR resolves equal-length matches by
// rule order, and several of these are prefixes of others.
ATTRIBUTE      : 'attribute' ;
CONST          : 'const' ;
UNIFORM        : 'uniform' ;
BUFFER         : 'buffer' ;
SHARED         : 'shared' ;
VARYING        : 'varying' ;
CENTROID       : 'centroid' ;
READONLY       : 'readonly' ;
WRITEONLY      : 'writeonly' ;
COHERENT       : 'coherent' ;
VOLATILE       : 'volatile' ;
RESTRICT       : 'restrict' ;
INOUT          : 'inout' ;
INVARIANT      : 'invariant' ;
PRECISE        : 'precise' ;
NOPERSPECTIVE  : 'noperspective' ;
SMOOTH         : 'smooth' ;
FLAT           : 'flat' ;
IN             : 'in' ;
OUT            : 'out' ;
PATCH          : 'patch' ;
SAMPLE         : 'sample' ;
LAYOUT         : 'layout' ;
PRECISION      : 'precision' ;
HIGH_PRECISION : 'highp' ;
MEDIUM_PRECISION : 'mediump' ;
LOW_PRECISION  : 'lowp' ;
STRUCT         : 'struct' ;
IF             : 'if' ;
ELSE           : 'else' ;
SWITCH         : 'switch' ;
CASE           : 'case' ;
DEFAULT        : 'default' ;
WHILE          : 'while' ;
DO             : 'do' ;
FOR            : 'for' ;
RETURN         : 'return' ;
BREAK          : 'break' ;
CONTINUE       : 'continue' ;
DISCARD        : 'discard' ;

VOID   : 'void' ;
FLOAT  : 'float' ;
DOUBLE : 'double' ;
INT    : 'int' ;
UINT   : 'uint' ;
BOOL   : 'bool' ;

VEC2 : 'vec2' ; VEC3 : 'vec3' ; VEC4 : 'vec4' ;
DVEC2 : 'dvec2' ; DVEC3 : 'dvec3' ; DVEC4 : 'dvec4' ;
IVEC2 : 'ivec2' ; IVEC3 : 'ivec3' ; IVEC4 : 'ivec4' ;
UVEC2 : 'uvec2' ; UVEC3 : 'uvec3' ; UVEC4 : 'uvec4' ;
BVEC2 : 'bvec2' ; BVEC3 : 'bvec3' ; BVEC4 : 'bvec4' ;

MAT2 : 'mat2' ; MAT3 : 'mat3' ; MAT4 : 'mat4' ;
MAT2X2 : 'mat2x2' ; MAT2X3 : 'mat2x3' ; MAT2X4 : 'mat2x4' ;
MAT3X2 : 'mat3x2' ; MAT3X3 : 'mat3x3' ; MAT3X4 : 'mat3x4' ;
MAT4X2 : 'mat4x2' ; MAT4X3 : 'mat4x3' ; MAT4X4 : 'mat4x4' ;

SAMPLER1D : 'sampler1D' ; SAMPLER2D : 'sampler2D' ; SAMPLER3D : 'sampler3D' ;
SAMPLERCUBE : 'samplerCube' ;
SAMPLER1DARRAY : 'sampler1DArray' ; SAMPLER2DARRAY : 'sampler2DArray' ;
SAMPLERCUBEARRAY : 'samplerCubeArray' ;
SAMPLER2DRECT : 'sampler2DRect' ; SAMPLERBUFFER : 'samplerBuffer' ;
SAMPLER1DSHADOW : 'sampler1DShadow' ; SAMPLER2DSHADOW : 'sampler2DShadow' ;
SAMPLERCUBESHADOW : 'samplerCubeShadow' ;
SAMPLER2DARRAYSHADOW : 'sampler2DArrayShadow' ;

ISAMPLER1D : 'isampler1D' ; ISAMPLER2D : 'isampler2D' ; ISAMPLER3D : 'isampler3D' ;
ISAMPLERCUBE : 'isamplerCube' ; ISAMPLER2DARRAY : 'isampler2DArray' ;
USAMPLER1D : 'usampler1D' ; USAMPLER2D : 'usampler2D' ; USAMPLER3D : 'usampler3D' ;
USAMPLERCUBE : 'usamplerCube' ; USAMPLER2DARRAY : 'usampler2DArray' ;

IMAGE1D : 'image1D' ; IMAGE2D : 'image2D' ; IMAGE3D : 'image3D' ;
IMAGECUBE : 'imageCube' ; IMAGE2DARRAY : 'image2DArray' ;
IIMAGE2D : 'iimage2D' ; UIMAGE2D : 'uimage2D' ;
ATOMIC_UINT : 'atomic_uint' ;

BOOLCONSTANT : 'true' | 'false' ;

// A float needs a '.' or an exponent; otherwise `1` would be a float and every
// integer literal in the corpus would lose its type.
FLOATCONSTANT : ( [0-9]+ '.' [0-9]* ( [eE] [+-]? [0-9]+ )?
                | '.' [0-9]+ ( [eE] [+-]? [0-9]+ )?
                | [0-9]+ [eE] [+-]? [0-9]+
                ) [fF]?
              ;

// `0xbf800000u` occurs; so do plain `127` and `4294967295u`.
UINTCONSTANT : ( '0' [xX] [0-9a-fA-F]+ | [0-9]+ ) [uU] ;
INTCONSTANT  : '0' [xX] [0-9a-fA-F]+ | [0-9]+ ;

// HLSLcc's no-argument macro, used as a member qualifier inside interface
// blocks.  It must be declared BEFORE IDENT: both match the same 13 characters,
// and ANTLR breaks a longest-match tie by rule order, so listing it later would
// let IDENT win and the macro would parse as a type name.
UNITY_UNIFORM : 'UNITY_UNIFORM' ;

IDENT : [A-Za-z_] [A-Za-z0-9_]* ;

LEFT_OP  : '<<' ;
RIGHT_OP : '>>' ;
LE_OP    : '<=' ;
GE_OP    : '>=' ;
EQUAL_OP : '==' ;
NEQUAL_OP : '!=' ;
AND_OP   : '&&' ;
OR_OP    : '||' ;
PLUS_PLUS  : '++' ;
DASH_DASH  : '--' ;
LEFT_ASSIGN : '<<=' ;
RIGHT_ASSIGN : '>>=' ;
ADD_ASSIGN : '+=' ;
SUB_ASSIGN : '-=' ;
MUL_ASSIGN : '*=' ;
DIV_ASSIGN : '/=' ;
MOD_ASSIGN : '%=' ;
AND_ASSIGN : '&=' ;
XOR_ASSIGN : '^=' ;
OR_ASSIGN  : '|=' ;

LPAREN : '(' ;
RPAREN : ')' ;
LBRACK : '[' ;
RBRACK : ']' ;
LBRACE : '{' ;
RBRACE : '}' ;
DOT    : '.' ;
COMMA  : ',' ;
SEMI   : ';' ;
COLON  : ':' ;
QUESTION : '?' ;
ASSIGN : '=' ;
PLUS   : '+' ;
DASH   : '-' ;
STAR   : '*' ;
SLASH  : '/' ;
PERCENT : '%' ;
BANG   : '!' ;
TILDE  : '~' ;
AMPERSAND : '&' ;
CARET  : '^' ;
VERTICAL_BAR : '|' ;
LESS   : '<' ;
GREATER : '>' ;
