grammar ShaderLab;

// ---------------------------------------------------------------------------
// ShaderLab -- the container this tool emits, and the one Unity reads.
//
//   Shader "Name"
//   {
//       Properties { _Color ("Color", Color) = (1,1,1,1) }
//       SubShader
//       {
//           Tags { "Queue"="Transparent" "RenderType"="Transparent" }
//           LOD 300
//           Pass
//           {
//               Name "FORWARD"
//               Tags { "LightMode"="Always" }
//               ZWrite Off
//               Cull [_CullMode]
//               Blend SrcAlpha OneMinusSrcAlpha
//               Stencil { Ref 0 ReadMask 255 Comp Always }
//               HLSLPROGRAM
//               ...
//               ENDHLSL
//           }
//       }
//       CustomEditor "TMPro.EditorUtilities.TMP_SDFShaderGUI"
//       Fallback "Diffuse"
//   }
//
// What makes this a grammar rather than a text format: the program block.  Its
// body is HLSL, delimited by HLSLPROGRAM/ENDHLSL (or CGPROGRAM/ENDCG), and it
// must be captured as one opaque run -- the HLSL is parsed by Hlsl.g4, not by
// this grammar, and no ShaderLab rule may match inside it.  That is why the
// program body is a single token rather than a repetition of statements: a
// `}` inside a shader function would otherwise close the Pass.
//
// Everything else is small and closed: properties, tags, render state, and the
// handful of directives Unity defines.  Values are kept as tokens rather than
// typed, because ShaderLab accepts bare identifiers (`Off`, `Always`,
// `[_CullMode]`), quoted strings, numbers and bracketed property references
// interchangeably in most value positions.
// ---------------------------------------------------------------------------

shader
    : SHADER stringLiteral LBRACE shaderBody RBRACE EOF
    ;

// ShaderLab writes names, tags and editor class names as quoted strings
// everywhere; one rule keeps the call sites readable.
stringLiteral
    : STRING
    ;

shaderBody
    : shaderItem*
    ;

shaderItem
    : propertiesBlock
    | subShaderBlock
    | categoryBlock
    | fallbackDecl
    | customEditorDecl
    | dependencyDecl
    | SEMI
    ;

// ---------------------------------------------------------------------------
// Properties
// ---------------------------------------------------------------------------

propertiesBlock
    : PROPERTIES LBRACE propertyDecl* RBRACE
    ;

// `_Color ("Color", Color) = (1,1,1,1)`
// `_MainTex ("Font Atlas", 2D) = "white" {}`
// `_FaceDilate ("Face Dilate", Range(-1, 1)) = 0`
// `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}`
// -- the bracketed prefix is an attribute Unity's sprite/UI shaders use.
propertyDecl
    : propertyAttribute* propertyName LPAREN stringLiteral COMMA propertyType RPAREN ASSIGN propertyValue
    ;

propertyAttribute
    : LBRACK IDENT RBRACK
    ;

propertyName
    : IDENT
    ;

// `2D`, `3D`, `CUBE`, `2DArray`, `Color`, `Vector`, `Float`, `Range(-1, 1)`, `Int`
propertyType
    : TEXTYPE
    | IDENT (LPAREN valueList RPAREN)?
    ;

// A number, a quoted string, a parenthesised vector, or a `{}`-suffixed default.
propertyValue
    : LPAREN valueList RPAREN LBRACE RBRACE
    | LPAREN valueList RPAREN
    | stringLiteral LBRACE RBRACE
    | stringLiteral
    | valueToken
    ;

valueList
    : valueToken (COMMA valueToken)*
    ;

// One ShaderLab value: identifier, number, or a bracketed property reference.
valueToken
    : IDENT
    | NUMBER
    | LBRACK IDENT RBRACK
    ;

// ---------------------------------------------------------------------------
// SubShader / Category
// ---------------------------------------------------------------------------

subShaderBlock
    : SUBSHADER LBRACE subShaderBody RBRACE
    ;

subShaderBody
    : subShaderItem*
    ;

subShaderItem
    : tagsBlock
    | lodDecl
    | passBlock
    | subShaderBlock
    | SEMI
    ;

categoryBlock
    : CATEGORY LBRACE subShaderBody RBRACE
    ;

tagsBlock
    : TAGS LBRACE tagPair* RBRACE
    ;

// `"Queue"="Transparent"`, `"LightMode"="Always"`
tagPair
    : stringLiteral ASSIGN stringLiteral
    ;

lodDecl
    : LOD NUMBER
    ;

// ---------------------------------------------------------------------------
// Pass
// ---------------------------------------------------------------------------

passBlock
    : PASS LBRACE passBody RBRACE
    ;

passBody
    : passItem*
    ;

passItem
    : nameDecl
    | tagsBlock
    | renderState
    | programBlock
    | SEMI
    ;

nameDecl
    : NAME stringLiteral
    ;

// `ZWrite Off`, `Cull [_CullMode]`, `Blend SrcAlpha OneMinusSrcAlpha`,
// `ColorMask GBA`, `Offset 0, -1`, `Lighting On`, and the block form
// `Stencil { Ref 0 ReadMask 255 Comp Always Pass [_StencilOp] }`.
//
// Inside a block the key may be the `Pass` keyword itself (`Pass [_StencilOp]`,
// the stencil op), which is why the block keys are a separate rule from the
// flat ones: at pass level `Pass {` opens a subshader pass, inside a block it
// names a stencil operation.
renderState
    : renderStateKey renderStateValue*                        # flatState
    | blockStateKey LBRACE blockStateEntry* RBRACE            # blockState
    ;

blockStateEntry
    : blockStateKey renderStateValue*
    ;

blockStateKey
    : PASS
    | IDENT
    ;

renderStateKey
    : IDENT
    ;

renderStateValue
    : valueToken
    | COMMA
    ;

// The HLSL body, as one token.  See the header note: this must not be parsed
// by ShaderLab rules, or a `}` in a shader function would close the Pass.
programBlock
    : PROGRAM_BODY
    ;

// ---------------------------------------------------------------------------
// Top-level directives
// ---------------------------------------------------------------------------

// `Fallback "Diffuse"`, or `Fallback Off` to declare no fallback at all.
fallbackDecl
    : FALLBACK (stringLiteral | IDENT)
    ;

customEditorDecl
    : CUSTOMEDITOR stringLiteral
    ;

dependencyDecl
    : DEPENDENCY stringLiteral
    ;

// ---------------------------------------------------------------------------
// Lexer
// ---------------------------------------------------------------------------

// The program block is matched before anything else, greedily up to its
// matching end keyword.  `HLSLPROGRAM`/`CGPROGRAM` may be followed by a name.
// Non-greedy, so it stops at the FIRST end keyword -- the emitter writes
// exactly one block per Pass.
PROGRAM_BODY
    : ('HLSLPROGRAM' | 'CGPROGRAM') ~[\r\n]*
      ( . )*?
      ('ENDHLSL' | 'ENDCG')
    ;

SHADER       : 'Shader' ;
PROPERTIES   : 'Properties' ;
SUBSHADER    : 'SubShader' ;
CATEGORY     : 'Category' ;
PASS         : 'Pass' ;
TAGS         : 'Tags' ;
LOD          : 'LOD' ;
NAME         : 'Name' ;
FALLBACK     : 'Fallback' ;
CUSTOMEDITOR : 'CustomEditor' ;
DEPENDENCY   : 'Dependency' ;

// `//` and `/* */` comments are skipped: ShaderLab has no comment-carrying
// construct, and the emitted files use them only for annotation.
LINE_COMMENT  : '//' ~[\r\n]* -> skip ;
BLOCK_COMMENT : '/*' .*? '*/' -> skip ;

WS : [ \t\r\n]+ -> skip ;

STRING : '"' ( '\\' . | ~["\\\r\n] )* '"' ;

// `2D`, `3D`, `2DArray`, `3DArray` -- a texture property type starts with a
// digit, which no identifier can, so it needs its own token.  It must precede
// NUMBER, or `2D` would lex as the number 2 followed by the identifier D.
TEXTYPE : [0-9] 'D' ('Array')? ;

// A ShaderLab number is a plain decimal, possibly negative or fractional.
NUMBER : '-'? [0-9]+ ('.' [0-9]*)? ;

IDENT : [A-Za-z_] [A-Za-z0-9_]* ;

LBRACE : '{' ;
RBRACE : '}' ;
LPAREN : '(' ;
RPAREN : ')' ;
LBRACK : '[' ;
RBRACK : ']' ;
COMMA  : ',' ;
ASSIGN : '=' ;
SEMI   : ';' ;
