grammar Hlsl;

// ---------------------------------------------------------------------------
// HLSL -- the dialect this tool emits inside HLSLPROGRAM, plus the CGPROGRAM
// form it still supports.
//
// Same relationship to Glsl.g4 as the languages have to each other: the
// expression grammar is nearly identical, and the differences are exactly what
// the emitter writes:
//
//   * semantic annotations  `float4 pos : SV_POSITION;`, `float4 frag(...) : SV_Target`
//   * resource declarations `Texture2D _MainTex; SamplerState sampler_MainTex;`
//   * register / packoffset `sampler2D t0 : register(s0);`, `float4 v : packoffset(c0);`
//   * constant buffers      `cbuffer UnityPerCamera { float4 _Time; }`
//   * method calls          `_MainTex.Sample(sampler_MainTex, uv)`
//   * swizzles on any lvalue, which the emitter writes constantly (`r0.xy = ...`)
//
// Measured over the corpus (22,997 program blocks, 11,549,561 lines) the
// constructs actually present are: struct, cbuffer-free resource declarations,
// if/for/while/return/break/continue/discard, and the preprocessor lines
// `#pragma`, `#if`, `#elif`, `#else`, `#endif`, `#include`, `#ifdef`.  `switch`
// and `do` do not occur but are legal HLSL, so the statement grammar keeps them.
//
// The preprocessor is one opaque line token, as in Glsl.g4: expanding macros is
// a different stage, and modelling #define bodies inside the parser would be a
// second preprocessor rather than a grammar.
// ---------------------------------------------------------------------------

translationUnit
    : externalItem* EOF
    ;

externalItem
    : ppLine
    | declaration
    | functionDefinition
    | SEMI
    ;

ppLine
    : PP
    ;

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

// `float4`, `Texture2D`, `SamplerState`, `appdata`, `float4x4`, `matrix`...
// A single token class, because HLSL types are not a closed set: the emitter
// writes struct names (`v2f vert(...)`) in exactly the same position.
typeName
    : IDENT
    ;

// `Texture2D<float4>`, `StructuredBuffer<T>`, `vector<float, 4>`
typeSpecifier
    : typeName (LESS typeArgument (COMMA typeArgument)* GREATER)?
    ;

typeArgument
    : typeSpecifier
    | INTCONSTANT
    ;

// ---------------------------------------------------------------------------
// Declarations
// ---------------------------------------------------------------------------

declaration
    : cbufferDecl
    | structDecl
    | typedefDecl
    | variableDecl
    ;

// `cbuffer UnityPerCamera : register(b0) { float4 _Time; }`
cbufferDecl
    : CBUFFER IDENT (COLON registerSpec)? LBRACE cbufferBody RBRACE SEMI?
    ;

cbufferBody
    : (cbufferMember | ppLine | SEMI)*
    ;

cbufferMember
    : fullySpecifiedType variableDeclarator (COLON packoffsetSpec)? SEMI
    ;

// `float4 pos : SV_POSITION;` -- a struct member always carries a semantic in
// emitted HLSL, but a plain struct (helper types) may not, so it is optional.
structDecl
    : STRUCT IDENT LBRACE structBody RBRACE SEMI
    ;

structBody
    : (structMember | ppLine | SEMI)*
    ;

structMember
    : fullySpecifiedType variableDeclarator (COLON semantic)? SEMI
    ;

typedefDecl
    : TYPEDEF typeSpecifier IDENT SEMI
    ;

variableDecl
    : fullySpecifiedType variableDeclaratorList SEMI
    ;

fullySpecifiedType
    : typeModifier* typeSpecifier
    ;

// `static const`, `uniform`, `row_major`, `precise`, `groupshared`, `volatile`.
typeModifier
    : STATIC
    | CONST
    | UNIFORM
    | VOLATILE
    | ROW_MAJOR
    | COLUMN_MAJOR
    | PRECISE
    | GROUPSHARED
    | EXTERN
    | SHARED
    | INLINE
    | NOINTERPOLATION
    | LINEAR
    | CENTROID
    | NOPERSPECTIVE
    | SAMPLE
    ;

variableDeclaratorList
    : variableDeclarator (COMMA variableDeclarator)*
    ;

// `_MainTex`, `t0 : register(s0)`, `uv[4]`, `x = 1.0`
variableDeclarator
    : IDENT (LBRACK expression? RBRACK)* (COLON registerSpec)? (ASSIGN initializer)?
    ;

initializer
    : assignmentExpression
    | LBRACE initializerList (COMMA)? RBRACE
    ;

initializerList
    : initializer (COMMA initializer)*
    ;

// `register(t0)`, `register(s0)`, `register(b0, space1)`
registerSpec
    : REGISTER LPAREN registerSlot (COMMA registerSlot)* RPAREN
    ;

registerSlot
    : IDENT (ASSIGN expression)?
    | expression
    ;

// `packoffset(c0)`, `packoffset(c0.x)`
packoffsetSpec
    : PACKOFFSET LPAREN expression RPAREN
    ;

// `SV_POSITION`, `TEXCOORD0`, `SV_Target`, `COLOR`, `POSITION`
semantic
    : IDENT
    ;

// ---------------------------------------------------------------------------
// Functions
// ---------------------------------------------------------------------------

functionDefinition
    : fullySpecifiedType IDENT LPAREN parameterList? RPAREN (COLON semantic)? (SEMI | compoundStatement)
    ;

parameterList
    : parameter (COMMA parameter)*
    ;

// `in appdata v`, `out v2f o`, `float4 uv : TEXCOORD0`, `uniform float x`
parameter
    : parameterModifier* typeSpecifier IDENT? (COLON semantic)? (ASSIGN initializer)?
    ;

parameterModifier
    : IN
    | OUT
    | INOUT
    | UNIFORM
    | CONST
    | PRECISE
    | ROW_MAJOR
    | COLUMN_MAJOR
    ;

// ---------------------------------------------------------------------------
// Statements
// ---------------------------------------------------------------------------

statement
    : compoundStatement                                   # compoundStmt
    | simpleStatement                                     # simpleStmt
    | IF LPAREN expression RPAREN selectionRest            # ifStmt
    | SWITCH LPAREN expression RPAREN LBRACE switchBody RBRACE # switchStmt
    | WHILE LPAREN condition RPAREN statement              # whileStmt
    | DO statement WHILE LPAREN expression RPAREN SEMI     # doStmt
    | FOR LPAREN forInit condition? SEMI expression? RPAREN statement # forStmt
    | RETURN expression? SEMI                              # returnStmt
    ;

compoundStatement
    : LBRACE (statement | ppLine)* RBRACE
    ;

simpleStatement
    : declaration
    | expression SEMI
    | DISCARD SEMI
    | BREAK SEMI
    | CONTINUE SEMI
    | SEMI
    ;

selectionRest
    : statement (ELSE statement)?
    ;

switchBody
    : (caseLabel | statement | ppLine)*
    ;

caseLabel
    : CASE expression COLON
    | DEFAULT COLON
    ;

forInit
    : declaration
    | expression? SEMI
    ;

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
    : conditionalExpression                                  # condExpr
    | unaryExpression ASSIGN assignmentExpression            # assignExpr
    | unaryExpression ADD_ASSIGN assignmentExpression        # addAssignExpr
    | unaryExpression SUB_ASSIGN assignmentExpression        # subAssignExpr
    | unaryExpression MUL_ASSIGN assignmentExpression        # mulAssignExpr
    | unaryExpression DIV_ASSIGN assignmentExpression        # divAssignExpr
    | unaryExpression MOD_ASSIGN assignmentExpression        # modAssignExpr
    | unaryExpression LEFT_ASSIGN assignmentExpression       # leftAssignExpr
    | unaryExpression RIGHT_ASSIGN assignmentExpression      # rightAssignExpr
    | unaryExpression AND_ASSIGN assignmentExpression        # andAssignExpr
    | unaryExpression XOR_ASSIGN assignmentExpression        # xorAssignExpr
    | unaryExpression OR_ASSIGN assignmentExpression         # orAssignExpr
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
    : exclusiveOrExpression (PIPE exclusiveOrExpression)*
    ;

exclusiveOrExpression
    : andExpression (CARET andExpression)*
    ;

andExpression
    : equalityExpression (AMPERSAND equalityExpression)*
    ;

equalityExpression
    : relationalExpression ((EQEQ | NEQ) relationalExpression)*
    ;

relationalExpression
    : shiftExpression ((LESS | GREATER | LE | GE) shiftExpression)*
    ;

shiftExpression
    : additiveExpression ((LSHIFT | RSHIFT) additiveExpression)*
    ;

additiveExpression
    : multiplicativeExpression ((PLUS | MINUS) multiplicativeExpression)*
    ;

multiplicativeExpression
    : unaryExpression ((STAR | SLASH | PERCENT) unaryExpression)*
    ;

unaryExpression
    : LPAREN typeSpecifier RPAREN unaryExpression    # castExpr
    | postfixExpression                     # postfixExpr
    | (INC | DEC) unaryExpression           # prefixIncExpr
    | (PLUS | MINUS | BANG | TILDE) unaryExpression  # unaryOpExpr
    ;

// Method calls are the reason this differs from Glsl.g4: `_MainTex.Sample(...)`
// is a call on a member, and the emitter writes it for every texture read.
postfixExpression
    : primaryExpression                              # primaryExpr
    | postfixExpression LBRACK expression RBRACK     # indexExpr
    | postfixExpression DOT IDENT                    # fieldExpr
    | postfixExpression (INC | DEC)                  # postfixIncExpr
    | postfixExpression LPAREN argumentList? RPAREN  # callExpr
    ;

argumentList
    : assignmentExpression (COMMA assignmentExpression)*
    ;

primaryExpression
    : IDENT                        # identifierExpr
    | literal                      # literalPrimary
    | LPAREN expression RPAREN     # parenPrimary
    ;

literal
    : INTCONSTANT
    | HEXCONSTANT
    | FLOATCONSTANT
    | BOOLCONSTANT
    ;

// ---------------------------------------------------------------------------
// Lexer
// ---------------------------------------------------------------------------

PP : '#' ~[\r\n]* ;

// Comments are not part of the grammar -- they go to the hidden channel, so the
// emitter's annotation lines (`// cb0 = $Globals ...`) can sit anywhere without
// the grammar needing a rule for "comment may appear here".
LINE_COMMENT  : '//' ~[\r\n]* -> channel(HIDDEN) ;
BLOCK_COMMENT : '/*' .*? '*/' -> channel(HIDDEN) ;

WS : [ \t\r\n]+ -> skip ;

// Keywords.  Longest spellings first where one is a prefix of another; ANTLR
// breaks equal-length ties by rule order, so each keyword must precede IDENT.
STRUCT       : 'struct' ;
CBUFFER      : 'cbuffer' ;
TYPEDEF      : 'typedef' ;
REGISTER     : 'register' ;
PACKOFFSET   : 'packoffset' ;
STATIC       : 'static' ;
CONST        : 'const' ;
UNIFORM      : 'uniform' ;
VOLATILE     : 'volatile' ;
ROW_MAJOR    : 'row_major' ;
COLUMN_MAJOR : 'column_major' ;
PRECISE      : 'precise' ;
GROUPSHARED  : 'groupshared' ;
EXTERN       : 'extern' ;
SHARED       : 'shared' ;
INLINE       : 'inline' ;
NOINTERPOLATION : 'nointerpolation' ;
LINEAR       : 'linear' ;
CENTROID     : 'centroid' ;
NOPERSPECTIVE : 'noperspective' ;
SAMPLE       : 'sample' ;
INOUT        : 'inout' ;
IN           : 'in' ;
OUT          : 'out' ;
IF           : 'if' ;
ELSE         : 'else' ;
SWITCH       : 'switch' ;
CASE         : 'case' ;
DEFAULT      : 'default' ;
WHILE        : 'while' ;
DO           : 'do' ;
FOR          : 'for' ;
RETURN       : 'return' ;
BREAK        : 'break' ;
CONTINUE     : 'continue' ;
DISCARD      : 'discard' ;

BOOLCONSTANT : 'true' | 'false' ;

// A float needs a '.' or an exponent; `1` must stay an integer literal.
FLOATCONSTANT : ( [0-9]+ '.' [0-9]* ( [eE] [+-]? [0-9]+ )?
                | '.' [0-9]+ ( [eE] [+-]? [0-9]+ )?
                | [0-9]+ [eE] [+-]? [0-9]+
                ) [fFhH]?
              ;

HEXCONSTANT : '0' [xX] [0-9a-fA-F]+ [uUlL]? ;
INTCONSTANT : [0-9]+ [uUlL]? ;

IDENT : [A-Za-z_] [A-Za-z0-9_]* ;

OR_OP   : '||' ;
AND_OP  : '&&' ;
EQEQ    : '==' ;
NEQ     : '!=' ;
LE      : '<=' ;
GE      : '>=' ;
LSHIFT  : '<<' ;
RSHIFT  : '>>' ;
INC     : '++' ;
DEC     : '--' ;
ADD_ASSIGN   : '+=' ;
SUB_ASSIGN   : '-=' ;
MUL_ASSIGN   : '*=' ;
DIV_ASSIGN   : '/=' ;
MOD_ASSIGN   : '%=' ;
LEFT_ASSIGN  : '<<=' ;
RIGHT_ASSIGN : '>>=' ;
AND_ASSIGN   : '&=' ;
XOR_ASSIGN   : '^=' ;
OR_ASSIGN    : '|=' ;

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
MINUS  : '-' ;
STAR   : '*' ;
SLASH  : '/' ;
PERCENT : '%' ;
BANG   : '!' ;
TILDE  : '~' ;
AMPERSAND : '&' ;
CARET  : '^' ;
PIPE   : '|' ;
LESS   : '<' ;
GREATER : '>' ;
