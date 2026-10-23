grammar CgExpr;

// ---------------------------------------------------------------------------
// Expression grammar for one line of Cg / HLSL as this tool emits it.
//
// Scope: the *expression* layer only.  There is deliberately no statement or
// declaration grammar here -- the rewriter does not re-parse whole lines.  It
// walks the token stream, and wherever a sampling intrinsic appears
// (tex2D/tex3D/texCUBE/tex2Dlod/tex3Dlod/texCUBElod) it asks this grammar for
// the argument structure of *that* call and nothing else.  Anything the
// grammar does not recognise is simply left as tokens and never rewritten,
// which is what keeps this additive: the regex path stays authoritative until
// the two are shown to agree.
//
// The lexer, by contrast, must cover the whole line without error, because it
// is the thing that knows where strings, `//` comments and `/* */` comments
// are.  That is the actual win over the current regex pass: it cannot be
// fooled by a `tex2D(` that lives inside a comment or a string literal, and it
// cannot be confused by a call whose arguments span several lines.
// ---------------------------------------------------------------------------

@parser::namespace { ShaderRipper.Antlr }
@lexer::namespace { ShaderRipper.Antlr }

// ---------------------------------------------------------------------------
// Parser
// ---------------------------------------------------------------------------

expression
    : expression DOT IDENT                                  # memberAccess
    | expression LBRACK expression RBRACK                   # indexAccess
    | expression LPAREN argumentList? RPAREN                # callSuffix
    | IDENT                                                 # identifier
    | literal                                               # literalExpr
    | LPAREN expression RPAREN                              # parenExpr
    | op = (PLUS | MINUS | BANG | TILDE) expression          # unaryExpr
    | expression op = (STAR | SLASH | PERCENT) expression    # multiplicative
    | expression op = (PLUS | MINUS) expression              # additive
    | expression op = (LSHIFT | RSHIFT) expression           # shift
    | expression op = (LT | LE | GT | GE) expression         # relational
    | expression op = (EQEQ | BANGEQ) expression             # equality
    | expression op = AMP expression                         # bitAnd
    | expression op = CARET expression                       # bitXor
    | expression op = PIPE expression                        # bitOr
    | expression op = ANDAND expression                      # logicalAnd
    | expression op = OROR expression                        # logicalOr
    | expression QUESTION expression COLON expression        # ternary
    ;

argumentList
    : expression (COMMA expression)*
    ;

literal
    : NUMBER
    | FLOAT
    | HEX
    | STRING
    ;

// ---------------------------------------------------------------------------
// Lexer -- must tokenise an arbitrary emitted line without error.
// ---------------------------------------------------------------------------

// A preprocessor line wins over everything else: `#` never appears in HLSL
// expressions, and swallowing the whole line keeps `#pragma vertex vert` and
// `#include "UnityCG.cginc"` from being picked apart into expression tokens.
PP : '#' ~[\r\n]* ;

// Comments are tokens, not skipped text: keeping them lets the rewriter copy
// the untouched original through verbatim, and keeps call scanning out of them.
LINE_COMMENT  : '//' ~[\r\n]* ;
BLOCK_COMMENT : '/*' .*? '*/' ;

// Numbers.  FLOAT is listed before NUMBER so `1.5` does not lex as `1` `.` `5`.
HEX   : '0' [xX] [0-9a-fA-F]+ [uUlL]? ;
FLOAT : ([0-9]+ '.' [0-9]* | '.' [0-9]+) ([eE] [+-]? [0-9]+)? [fFhH]?
      | [0-9]+ [eE] [+-]? [0-9]+ [fFhH]?
      ;
NUMBER : [0-9]+ [uUlL]? ;

STRING : '"' ( '\\' . | ~["\\\r\n] )* '"' ;

IDENT : [a-zA-Z_] [a-zA-Z_0-9]* ;

// Two-character operators must precede their single-character prefixes.
LSHIFT : '<<' ;
RSHIFT : '>>' ;
LE     : '<=' ;
GE     : '>=' ;
EQEQ   : '==' ;
BANGEQ : '!=' ;
ANDAND : '&&' ;
OROR   : '||' ;

LPAREN  : '(' ;
RPAREN  : ')' ;
LBRACK  : '[' ;
RBRACK  : ']' ;
DOT     : '.' ;
COMMA   : ',' ;
QUESTION: '?' ;
COLON   : ':' ;
PLUS    : '+' ;
MINUS   : '-' ;
STAR    : '*' ;
SLASH   : '/' ;
PERCENT : '%' ;
BANG    : '!' ;
TILDE   : '~' ;
LT      : '<' ;
GT      : '>' ;
AMP     : '&' ;
CARET   : '^' ;
PIPE    : '|' ;
SEMI    : ';' ;
LBRACE  : '{' ;
RBRACE  : '}' ;
ASSIGN  : '=' ;
AT      : '@' ;

// Whitespace goes to the hidden channel rather than being skipped, so that a
// rewrite of one call leaves every byte around it -- spacing, indentation,
// line breaks -- exactly as the producer wrote it.
WS : [ \t\r\n]+ -> channel(HIDDEN) ;

// Last resort: any character the rules above do not claim still becomes a
// token, so lexing an emitted line can never fail.
OTHER : . ;
