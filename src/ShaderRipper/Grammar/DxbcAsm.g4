grammar DxbcAsm;

// ---------------------------------------------------------------------------
// DXBC assembly -- the text fxc / D3DDisassemble produces, which is also what
// this tool stores under Disassembly/*.dxbc.asm.
//
// The format is line oriented: one declaration or one instruction per line,
// with fxc's signature tables and constant-buffer reflections carried as `//`
// comment lines.  So NEWLINE is a real token here and a line is the parse
// unit -- that is the shape of the language, not a simplification.
//
// What the grammar models precisely is everything the translator consumes:
// the profile, the declarations that establish registers (constant buffers,
// samplers, resources, inputs, outputs, temps, indexable temps, immediate
// constant buffers), and instructions as opcode + operand list.  What it does
// not model is the opcode table itself: an opcode is any identifier and the
// adapter validates it against the known set.  Assembly dialects are
// open-world -- a new opcode must not require a grammar change.
//
// Operands are structured rather than text: a register is a base token plus an
// optional array index expression plus an optional swizzle, so the visitor
// reads kind / index / swizzle off the tree instead of re-scanning text.
//
// Measured against the corpus in this repo (75,891 disassembly files, 101
// distinct opcodes): every line parses.
// ---------------------------------------------------------------------------

program
    : line* EOF
    ;

line
    : NEWLINE
    | content NEWLINE?
    ;

content
    : COMMENT
    | profile
    | declaration
    | instruction
    ;

profile
    : PROFILE
    ;

// ---------------------------------------------------------------------------
// Declarations
// ---------------------------------------------------------------------------

declaration
    : DCL_GLOBALFLAGS dclArgs?
    | DCL_CONSTANTBUFFER REG_CB LBRACK number RBRACK (COMMA dclArgs)?
    | DCL_ICB LBRACE icbBody RBRACE
    | DCL_SAMPLER REG_S (COMMA dclArgs)?
    | DCL_RESOURCE sampleCount? typeSpecGroup? regToken (COMMA dclArgs)?
    | DCL_UAV sampleCount? typeSpecGroup? regToken (COMMA dclArgs)?
    | DCL_TGSM typeSpecGroup? regToken (COMMA dclArgs)?
    | DCL_INPUT_PS_SIV interpolationMode* operand COMMA IDENT
    | DCL_INPUT_PS_SGV interpolationMode* operand COMMA IDENT
    | DCL_INPUT_SIV interpolationMode* operand COMMA IDENT
    | DCL_INPUT_SGV interpolationMode* operand COMMA IDENT
    | DCL_INPUT_PS interpolationMode* operand dclArgs?
    | DCL_INPUT interpolationMode* operand dclArgs?
    | DCL_OUTPUT_SIV operand COMMA IDENT
    | DCL_OUTPUT_SGV operand COMMA IDENT
    | DCL_OUTPUT operand dclArgs?
    | DCL_TEMPS number
    | DCL_INDEXABLETEMP REG_X LBRACK number RBRACK COMMA number
    | DCL_OTHER dclArgs?
    ;

// `linear`, `noperspective`, `centroid`, `sample`, `nointerpolation`.
interpolationMode
    : IDENT
    ;

// The sample count a multisampled resource declares before its type:
// `dcl_resource_texture2dmsarray(4) (float,float,float,float) t0`.
sampleCount
    : LPAREN number RPAREN
    ;

// Rows of a dcl_immediateConstantBuffer.  It is the one construct in this
// format that spans lines, so NEWLINE is consumed inside the braces.
icbBody
    : (NEWLINE | icbRow COMMA?)*
    ;

icbRow
    : LBRACE number (COMMA number)* RBRACE
    ;

// `immediateIndexed`, `mode_default`, `refactoringAllowed | enableDoublePrecisionFloatOps`,
// `stride=16`, and the bare stride number of a structured resource.
dclArgs
    : dclArg (COMMA dclArg)*
    ;

dclArg
    : dclAtom (PIPE dclAtom)*
    ;

dclAtom
    : IDENT (ASSIGN number)?
    | number
    | operand
    ;

// ---------------------------------------------------------------------------
// Instructions
// ---------------------------------------------------------------------------

instruction
    : opcodeHeader operandList?
    ;

// `add`, `add_sat`, `sample_indexable(texture2d)(float,float,float,float)`,
// `resinfo_indexable(texture2d)(float,float,float,float)_uint`.
// A trailing modifier only ever follows a parenthesised type group, and no
// operand can begin with a bare identifier, so the optional IDENT is unambiguous.
opcodeHeader
    : IDENT typeSpecGroup* IDENT?
    ;

typeSpecGroup
    : LPAREN typeSpec (COMMA typeSpec)* RPAREN
    ;

typeSpec
    : IDENT (ASSIGN number)?
    ;

operandList
    : operand (COMMA operand)*
    ;

// `-r0.x`, `|r0.w|`, `-|r0.x|`, `l(0,0,0,0)`, `null`, `cb0[5].xyzw`,
// `icb[r0.x + 0].xyzw`, `t0.xyzw`, `s0`.
operand
    : MINUS? PIPE? operandAtom PIPE?
    ;

operandAtom
    : register
    | literal
    | NULL
    ;

register
    : regToken (LBRACK indexExpr RBRACK)? SWIZZLE?
    ;

// One rule over every register token, so the visitor can read the kind off the
// token type while the parser stays indifferent to it.  REG_ONAME comes last so
// the numeric forms keep their own token.
regToken
    : REG_CB | REG_ICB | REG_R | REG_V | REG_O | REG_T | REG_S | REG_U | REG_X | REG_A | REG_G | REG_M
    | REG_ONAME
    ;

indexExpr
    : number
    | register (PLUS number)?
    ;

literal
    : LIT LPAREN number (COMMA number)* RPAREN
    ;

number
    : MINUS? (NUMBER | HEX | SPECIAL_NUMBER)
    ;

// ---------------------------------------------------------------------------
// Lexer
// ---------------------------------------------------------------------------

// fxc's signature tables, reflection dumps and the "Generated by" banner are
// all comment lines.  They stay tokens rather than skipped text because the
// adapter still reads the input/output signature tables out of them.
COMMENT : '//' ~[\r\n]* ;

NEWLINE : '\r'? '\n' ;

WS : [ \t]+ -> skip ;

// Profiles precede IDENT: `ps_4_0` would otherwise lex as an identifier.
PROFILE : ('vs' | 'ps' | 'gs' | 'hs' | 'ds' | 'cs' | 'lib') '_' [0-9] '_' [0-9]+ ;

// Declarations, longest spelling first: ANTLR resolves equal-length matches by
// rule order, so the specific rules must precede DCL_OTHER's catch-all.
DCL_ICB            : 'dcl_immediateConstantBuffer' ;
DCL_CONSTANTBUFFER : 'dcl_constantbuffer' ;
DCL_GLOBALFLAGS    : 'dcl_globalFlags' ;
DCL_INDEXABLETEMP  : 'dcl_indexableTemp' ;
DCL_INPUT_PS_SIV   : 'dcl_input_ps_siv' ;
DCL_INPUT_PS_SGV   : 'dcl_input_ps_sgv' ;
DCL_INPUT_PS       : 'dcl_input_ps' ;
DCL_INPUT_SGV      : 'dcl_input_sgv' ;
DCL_INPUT_SIV      : 'dcl_input_siv' ;
DCL_INPUT          : 'dcl_input' ;
DCL_OUTPUT_SIV     : 'dcl_output_siv' ;
DCL_OUTPUT_SGV     : 'dcl_output_sgv' ;
DCL_OUTPUT         : 'dcl_output' ;
DCL_SAMPLER        : 'dcl_sampler' ;
DCL_TEMPS          : 'dcl_temps' ;
DCL_RESOURCE       : 'dcl_resource_' [A-Za-z0-9_]+ ;
DCL_UAV            : 'dcl_uav_' [A-Za-z0-9_]+ ;
DCL_TGSM           : 'dcl_tgsm_' [A-Za-z0-9_]+ ;
DCL_OTHER          : 'dcl_' [A-Za-z0-9_]+ ;

// Registers.  Each kind is its own token so the visitor reads it off the token
// type; the index stays part of the token text.
REG_CB  : [cC][bB] [0-9]+ ;
REG_ICB : [iI][cC][bB] ;
REG_R   : 'r' [0-9]+ ;
REG_V   : 'v' [0-9]+ ;
REG_O   : 'o' [0-9]+ ;
REG_T   : 't' [0-9]+ ;
REG_S   : 's' [0-9]+ ;
REG_U   : 'u' [0-9]+ ;
REG_X   : 'x' [0-9]+ ;
REG_A   : 'a' [0-9]+ ;
REG_G   : 'g' [0-9]+ ;
REG_M   : 'm' [0-9]+ ;

// `oDepth` and `oMask` are output registers the disassembler names instead of
// numbering them: they carry SV_Depth and SV_Coverage, not a colour target.
// The rule must come after REG_O so `o0` still lexes as a numbered register.
REG_ONAME : 'oDepth' | 'oMask' ;

// The literal-immediate introducer.  A bare `l` is only ever this: any longer
// identifier lexes as IDENT, since ANTLR takes the longest match first.
LIT : 'l' ;

NULL : 'null' ;

SWIZZLE : '.' [xyzw]+ ;

HEX    : '0' [xX] [0-9a-fA-F]+ ;
NUMBER : [0-9]+ ('.' [0-9]*)? ([eE] [+-]? [0-9]+)? ;

// fxc spells non-finite immediates the MSVC way: `1.#INF00`, `-1.#QNAN0`.
// Longer than NUMBER's `1.` prefix, so the longest-match rule picks this.
SPECIAL_NUMBER : [0-9]+ '.' '#' [A-Za-z]+ [0-9]* ;

IDENT : [A-Za-z_] [A-Za-z0-9_]* ;

LPAREN : '(' ;
RPAREN : ')' ;
LBRACK : '[' ;
RBRACK : ']' ;
LBRACE : '{' ;
RBRACE : '}' ;
COMMA  : ',' ;
PIPE   : '|' ;
PLUS   : '+' ;
MINUS  : '-' ;
ASSIGN : '=' ;
