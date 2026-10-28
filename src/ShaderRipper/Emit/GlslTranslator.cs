using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace ShaderRipper.Emit
{
    /// <summary>
    /// GLSL -> HLSL, read with <c>Glsl.g4</c>.
    ///
    /// Unity stores the HLSLcc output for its GL platforms, so the input is already a
    /// register-style transliteration: one statement per line, <c>u_xlatN</c>
    /// temporaries, stage built-ins named <c>in_*</c>/<c>vs_*</c>/<c>gl_*</c>.  That
    /// makes a straight transliteration faithful -- the work is renaming and choosing
    /// the right sampling form, not recovering intent.
    ///
    /// The output keeps the same register-level, compilable style as the DXBC side.
    /// </summary>
    internal sealed class GlslTranslator
    {
        public sealed class Result
        {
            public string Hlsl = "";
            public int ParseErrors;
            public int Todo;
            public List<string> Missing = new();
        }

        private readonly string _src;
        private readonly bool _vertex;
        private readonly StringBuilder _sb = new();
        private readonly Result _r = new();
        private readonly List<(string type, string name)> _uniforms = new();
        private readonly HashSet<string> _samplers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _samplerKind = new(StringComparer.Ordinal);
        /// <summary>
        /// Is this a name Unity synthesises for a texture property?
        ///
        /// A <c>2D</c> property <c>&lt;name&gt;</c> implies <c>&lt;name&gt;_ST</c>
        /// (scale/offset), <c>&lt;name&gt;_TexelSize</c> and <c>&lt;name&gt;_HDR</c>,
        /// carried in the same material constant buffer.  A payload reads them
        /// directly and they are never listed in the Properties block, so without this
        /// they stay bare globals and read zero.
        /// </summary>
        private bool IsPropertyDerived(string name)
        {
            foreach (string suffix in new[] { "_ST", "_TexelSize", "_HDR" })
            {
                if (!name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                if (_props.Contains(name[..^suffix.Length])) return true;
            }
            return false;
        }

        private bool IsEngineName(string name)
        {
            // A shader that turns `UNITY_LIGHT_PROBE_PROXY_VOLUME` on makes the
            // include declare the probe-volume block, so the LOCAL copy must not be
            // emitted -- the two would collide ("redefinition of
            // 'unity_ProbeVolumeParams'").  This is checked before the Properties
            // test below, because a shader may list an engine global in its
            // Properties block without owning it.
            if (_engineRequestsLppv && name.StartsWith("unity_ProbeVolume", StringComparison.Ordinal))
                return true;
            // The same holds for the names the default include chain declares but a
            // shader happens to repeat in Properties:
            // `unity_ProbeVolumeParams` / `_LightColor0` are engine uploads either
            // way, and putting them in `UnityPerMaterial` collides with the
            // include's copy.  Ownership is decided by who FILLS the value.
            // The names in `NeedsLocalDeclaration` are engine uploads the include
            // chain does not declare.  They belong in the ENGINE branch -- not in
            // `UnityPerMaterial`, which is the buffer the MATERIAL fills -- and the
            // local declaration comes from `EngineBuiltins` below.  Putting them in
            // UnityPerMaterial made the merged file declare them twice
            // ("redefinition of 'unity_ProbeVolumeParams'").
            if (NeedsLocalDeclaration.Contains(name)) return true;
            if (_props.Contains(name)) return false;
            if (IsPropertyDerived(name)) return false;
            if (name.StartsWith("unity_", StringComparison.Ordinal)) return true;
            if (name.StartsWith("glstate_", StringComparison.Ordinal)) return true;
            return UnityEngineOnly.Contains(name);
        }

        /// <summary>
        /// Engine names Unity uploads but the default include chain does not
        /// declare.  Emitting a local copy is what the DXBC route does with an
        /// unnamed `$Globals` member, and it is the only way the name exists.
        ///
        /// `UnityCG.cginc` includes `UnityShaderVariables.cginc` (most `unity_*`
        /// matrices, `_Time`, `_ScreenParams`, `_ProjectionParams`) but NOT
        /// `UnityLightingCommon.cginc`, and it guards the probe-volume block behind
        /// `UNITY_LIGHT_PROBE_PROXY_VOLUME`, which defaults to 0.
        /// </summary>
        private static readonly HashSet<string> NeedsLocalDeclaration = new(StringComparer.Ordinal)
        {
            "_LightColor0", "_LightColor1", "_LightColor2", "_LightColor3",
            "_SpecColor", "_LightTexture0", "_LightTextureB0", "_LightMatrix0",
            "_ShadowMapTexture", "_ShadowOffsets0", "_ShadowOffsets1",
            "_ShadowOffsets2", "_ShadowOffsets3",
            "unity_ProbeVolumeParams", "unity_ProbeVolumeWorldToObject",
            "unity_ProbeVolumeSizeInv", "unity_ProbeVolumeMin",
        };

        /// <summary>
        /// Engine-filled scalars and vectors that do not carry the `unity_` prefix.
        /// Every one of these is declared by `UnityShaderVariables.cginc` and
        /// uploaded per camera/frame by the engine.
        /// </summary>
        private static readonly HashSet<string> UnityEngineOnly = new(StringComparer.Ordinal)
        {
            "_Time", "_SinTime", "_CosTime", "_DeltaTime", "_ProjectionParams",
            "_ScreenParams", "_ZBufferParams", "_OrthoParams", "_WorldSpaceCameraPos",
            "_WorldSpaceLightPos0", "_LightColor0", "_SpecColor",
            "_LightTexture0", "_LightTextureB0", "_LightMatrix0",
            "_ShadowMapTexture", "_ShadowOffsets0", "_ShadowOffsets1",
            "_ShadowOffsets2", "_ShadowOffsets3",
            "unity_ObjectToWorld", "unity_WorldToObject",
        };

        private readonly List<(string type, string name)> _ins = new();
        private readonly List<(string type, string name)> _outs = new();
        private readonly List<string> _locals = new();
        private readonly List<string> _buffers = new();

        /// <summary>
        /// Interpolators this stage writes (vertex) or reads (fragment).
        ///
        /// The vertex payload declares them <c>out</c> and the fragment payload
        /// declares the same names <c>in</c>; after the <c>vs_foo</c> -&gt; <c>foo</c>
        /// mapping both sides spell them identically.  Treating them as plain
        /// locals -- which is what the prefix-mapping alone used to do -- leaves the
        /// fragment reading a zero-initialised variable while the vertex's value is
        /// thrown away at `return`.  On a sprite shader that is an alpha of 0, i.e.
        /// completely invisible.
        /// </summary>
        private readonly List<(string type, string name, string semantic)> _varyingOut = new();
        private readonly List<(string type, string name, string semantic)> _varyingIn = new();

        /// <summary>
        /// Names the shader's Properties block declares.  A uniform in this set is a
        /// material property and has to live in `UnityPerMaterial`; anything else
        /// the engine owns is left to the include chain.
        /// </summary>
        private readonly HashSet<string> _props;

        /// <summary>
        /// Engine-owned uniforms this stage reads, so the merged file can pull them
        /// in through `UnityCG.cginc` instead of declaring dead copies.
        /// </summary>
        private readonly List<string> _engineNames = new();

        /// <summary>
        /// name -> HLSL type for every material property the payload declares, in
        /// first-seen order.
        ///
        /// The merged file emits this as ONE <c>UnityPerMaterial</c> block: a block per
        /// stage would define the shared names twice, and one built from the vertex
        /// stage alone leaves the fragment's properties undeclared.
        ///
        /// Per <see cref="Translate"/> call, not static: the two stages of one payload
        /// share it, two payloads must not -- a static made the content of a shader
        /// exported with <c>--jobs</c> depend on its neighbour.
        /// </summary>
        private readonly Dictionary<string, string> _materialUnion;

        /// <summary>Semantic to bind an interpolator to, in the order they appear.</summary>
        private static string VaryingSemantic(List<(string type, string name, string semantic)> list)
        {
            int n = list.Count;
            return n == 0 ? "TEXCOORD0" : "TEXCOORD" + n;
        }

        /// <summary>
        /// Every identifier the payload mentions, so engine-provided names can be
        /// declared exactly when they are used.
        /// </summary>
        private readonly HashSet<string> _usedNames = new(StringComparer.Ordinal);

        private GlslParser.TranslationUnitContext _unit;

        private GlslTranslator(string src, bool vertex, HashSet<string> props,
                               Dictionary<string, string> materialUnion)
        {
            _src = src;
            _vertex = vertex;
            _props = props ?? new HashSet<string>(StringComparer.Ordinal);
            _materialUnion = materialUnion;
            // GPU-instancing built-ins need real entry parameters (SV_InstanceID
            // / SV_VertexID); a static global cannot be seeded from a parameter,
            // so the entry grows arguments and the prologue aliases them.
            _usesInstance = src.Contains("gl_InstanceID", StringComparison.Ordinal);
            _usesVertex = src.Contains("gl_VertexID", StringComparison.Ordinal);
            // gl_FragCoord is rewritten to sv_position (see the identifier map),
            // and that rewritten reference needs the SV_Position input.
            _usesSvPosition = !_vertex && src.Contains("gl_FragCoord", StringComparison.Ordinal);

            // The payload's own text says whether it expects the engine to declare
            // `unity_ProbeVolumeParams`: the pragma that turns
            // `UNITY_LIGHT_PROBE_PROXY_VOLUME` on is the same one that makes the
            // include declare it.
            _engineRequestsLppv = src.Contains("UNITY_LIGHT_PROBE_PROXY_VOLUME", StringComparison.Ordinal);

            // Every identifier in the payload, so EngineBuiltins can declare the
            // engine's own names exactly when the body mentions them.  A GLSL
            // built-in is recorded under its HLSL name as well: the body says
            // `gl_ViewID_OVR`, the emitted code says `unity_StereoEyeIndex`, and
            // it is the emitted name that has to exist.
            //
            // HLSLcc's `hlslcc_mtx4x4` prefix is stripped by the identifier map, so
            // the STRIPPED spelling is what the emitted body spells -- and
            // therefore what has to be looked up.  Recording only the prefixed form
            // made `unity_ProbeVolumeWorldToObject` invisible to EngineBuiltins
            // even though the payload mentions it 88 times.
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(src, @"\b[A-Za-z_]\w*\b"))
            {
                string word = m.Value;
                _usedNames.Add(word);
                if (Renamed.TryGetValue(word, out var mappedName))
                    _usedNames.Add(mappedName);
                foreach (string prefix in new[] { "hlslcc_mtx4x4", "hlslcc_mtx3x3", "hlslcc_mtx2x2" })
                    if (word.StartsWith(prefix, StringComparison.Ordinal))
                        _usedNames.Add(word[prefix.Length..]);
            }
        }

        private readonly bool _usesInstance;
        private readonly bool _usesVertex;
        private readonly bool _usesSvPosition;

        /// <summary>
        /// The payload declares `UNITY_LIGHT_PROBE_PROXY_VOLUME`, so its own
        /// copy of `unity_ProbeVolumeParams` is behind the same guard the include
        /// uses.  A shader without the pragma gets the local declaration instead;
        /// declaring both would be the redefinition this whole branch exists to
        /// avoid.
        /// </summary>
        private readonly bool _engineRequestsLppv;

        /// <summary>
        /// Translate a whole payload: both stages into one HLSL block, which is
        /// what a ShaderLab Pass holds.
        ///
        /// The two stages share a scope in the input (they are separate
        /// translation units guarded by `#ifdef`), so the merge has to deal with
        /// two things: duplicated globals (`u_xlat0` is declared once per stage)
        /// and two entry points.  Globals are emitted once by first occurrence,
        /// and the entry points become `vert` / `frag` — the names the ShaderLab
        /// wrapper's pragmas refer to.
        /// </summary>
        public static Result Translate(string glsl, ICollection<string> properties = null)
        {
            var r = new Result();
            var globals = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var functions = new List<string>();
            var props = properties == null
                ? null
                : new HashSet<string>(properties, StringComparer.Ordinal);
            var engineNames = new List<string>();
            // One union per payload, shared by its stages and by nothing else.
            var materialUnion = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (stage, text) in Branches(glsl))
            {
                if (text == null) continue;
                bool vertex = stage == "VERTEX";
                var t = new GlslTranslator(text, vertex, props, materialUnion);
                t.Read();
                r.ParseErrors += t._r.ParseErrors;
                if (t._r.ParseErrors > 0) continue;
                r.Todo += t._r.Todo;
                r.Missing.AddRange(t._r.Missing);

                var (g, fn) = t.Split();
                // `_engineNames` is filled while the body is emitted, so it is read
                // after Split() -- reading it before would always see an empty list
                // and drop the include line.
                engineNames.AddRange(t._engineNames);
                // A struct's members are not free-standing globals: the vertex
                // stage's `vs_out` and the fragment stage's `vs_in` deliberately
                // carry the same field lines, so deduplicating them by text empties
                // whichever struct comes second.  The same applies to a struct
                // declaration itself -- `vs_out` and `vs_in` differ, but `appdata`
                // appears in both stages.
                int structDepth = 0;
                foreach (var line in g)
                {
                    string key = line.Trim();
                    bool structural = key.Length == 0 || key is "{" or "}" or "};"
                                      || key.StartsWith("#", StringComparison.Ordinal);

                    // A block body (struct OR cbuffer) must never be deduplicated:
                    // the vertex and fragment stages each declare their own
                    // `appdata` / `UnityPerMaterial`, and dropping the repeated
                    // header line leaves an orphan `{ ... };` that does not parse.
                    bool opensBlock = key.StartsWith("struct ", StringComparison.Ordinal)
                                      || key.StartsWith("cbuffer ", StringComparison.Ordinal);
                    if (structDepth > 0 || opensBlock
                        || (key == "{" && (IsStructName(globals) || IsCbufferName(globals))))
                    {
                        globals.Add(line);
                        if (key == "{" || opensBlock) structDepth++;
                        else if (key == "};" && structDepth > 0) structDepth--;
                        continue;
                    }

                    if (!structural && !seen.Add(key)) continue;
                    globals.Add(line);
                }
                functions.Add(fn);
            }

            var sb = new StringBuilder();
            sb.Append("// translated from GLSL by ShaderRipper\n");
            sb.Append("// grammar -> statements -> HLSL, register level\n\n");
            // The engine's own globals (`unity_ObjectToWorld`, `_ScreenParams`,
            // `_Time`) are declared by Unity's CGIncludes and filled by the engine
            // under those names.  A private copy would compile and read zero, so the
            // include goes first -- once, at file scope, before any struct or entry
            // point.
            if (engineNames.Count > 0)
                sb.Append("#include \"UnityCG.cginc\"\n\n");
            // One `UnityPerMaterial` for the whole file, built from both stages'
            // property sets.  This is the buffer Unity fills from the Material; a
            // plain global of the same name compiles and reads zero, which is what
            // made every GLSL-route sprite fully transparent.
            if (materialUnion.Count > 0)
            {
                sb.Append("cbuffer UnityPerMaterial\n{\n");
                foreach (var (name, type) in materialUnion)
                    sb.Append("    ").Append(type).Append(' ').Append(name).Append(";\n");
                sb.Append("};\n\n");
            }
            foreach (var line in globals) sb.Append(line).Append('\n');
            sb.Append('\n');
            foreach (var fn in functions) sb.Append(fn).Append('\n');
            r.Hlsl = sb.ToString();
            return r;
        }

        /// <summary>Separate the global declarations from the entry point.</summary>
        private (List<string> globals, string function) Split()
        {
            string text = Emit();
            var lines = text.Split('\n').ToList();

            // The entry point's return type varies (a bare `float4` when the stage
            // has no interpolators, a struct when it does), so the signature is
            // located by its name rather than by a hard-coded prefix.
            int at = lines.FindIndex(l => EntryRx.IsMatch(l));
            if (at < 0) return (lines, "");

            var globals = lines.Take(at).ToList();
            var fn = lines.Skip(at).ToList();
            fn[0] = EntryRx.Replace(fn[0], m => m.Groups[1].Value + (_vertex ? "vert(" : "frag("));
            return (globals, string.Join('\n', fn));
        }

        private static readonly System.Text.RegularExpressions.Regex EntryRx =
            new(@"^(\s*(?:float4|vs_out|frag_out|\w+)\s+)main\(",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Does the last global line open a struct body?</summary>
        private static bool IsStructName(List<string> globals) =>
            globals.Count > 0 && globals[^1].TrimStart().StartsWith("struct ", StringComparison.Ordinal);

        /// <summary>Does the last global line open a cbuffer body?</summary>
        private static bool IsCbufferName(List<string> globals) =>
            globals.Count > 0 && globals[^1].TrimStart().StartsWith("cbuffer ", StringComparison.Ordinal);

        /// <summary>
        /// Split `#ifdef VERTEX` / `#ifdef FRAGMENT`.  The preprocessor is
        /// deliberately not modelled by the grammar (a directive is one opaque
        /// line), so the branch boundaries are the caller's business — the same
        /// split the stored payload itself uses.
        /// </summary>
        private static IEnumerable<(string stage, string text)> Branches(string glsl)
        {
            foreach (var stage in new[] { "VERTEX", "FRAGMENT" })
                yield return (stage, Branch(glsl, stage));
        }

        private static string Branch(string glsl, string stage)
        {
            if (glsl == null) return null;
            string head = "#ifdef " + stage;
            int at = glsl.IndexOf(head, StringComparison.Ordinal);
            if (at < 0) return null;
            at += head.Length;
            int depth = 0;
            for (int i = at; i < glsl.Length;)
            {
                int eol = glsl.IndexOf('\n', i);
                if (eol < 0) eol = glsl.Length;
                string line = glsl[i..eol].Trim();
                if (line.StartsWith("#if")) depth++;
                else if (line.StartsWith("#endif"))
                {
                    if (depth == 0) return glsl[at..i];
                    depth--;
                }
                i = eol + 1;
            }
            return glsl[at..];
        }

        // ------------------------------------------------------------------ read

        private void Read()
        {
            var lexer = new GlslLexer(new AntlrInputStream(_src));
            lexer.RemoveErrorListeners();
            var parser = new GlslParser(new CommonTokenStream(lexer));
            var errors = new Counter();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errors);
            _unit = parser.translationUnit();
            _r.ParseErrors = errors.Count;

            foreach (var item in _unit.externalItem())
            {
                var decl = item.declaration();
                if (decl is GlslParser.VariableDeclContext v) { ReadVariables(v); continue; }
                if (decl is GlslParser.StructDeclContext sd) { ReadStructDecl(sd); continue; }
                if (decl is GlslParser.InterfaceBlockContext ib) { ReadBufferBlock(ib); continue; }
                var fn = item.functionDefinition();
                if (fn == null) continue;
                if (fn.IDENT()?.GetText() == "main") _main = fn;
                else _helpers.Add(fn);          // HLSLcc emits bitfieldExtract and friends
            }
        }

        private GlslParser.FunctionDefinitionContext _main;
        private readonly List<GlslParser.FunctionDefinitionContext> _helpers = new();

        /// <summary>
        /// An SSBO / UBO interface block (`readonly buffer name { members };`).
        /// SSBOs become HLSL StructuredBuffers over a synthesized struct -- the
        /// body references `name[i].member[...]`, which StructuredBuffer gives
        /// verbatim.  Uniform blocks become plain cbuffers; the binding slot is
        /// a compile-time non-issue because HLSL assigns registers itself.
        /// </summary>
        private void ReadBufferBlock(GlslParser.InterfaceBlockContext ib)
        {
            string name = ib.IDENT(0)?.GetText() ?? "";
            if (string.IsNullOrEmpty(name)) return;
            bool isSsbo = ib.BUFFER() != null;

            // `buffer name { T inst[]; }` -- the common HLSLcc shape: one
            // unsized array of a (usually previously declared) struct.  That
            // maps straight to `StructuredBuffer<T> name;`, the body's
            // `name[i].member` accesses carrying over untouched.
            var members = new List<string>();
            foreach (var sm in ib.structBody().structMember())
            {
                // GLSL also writes sized arrays type-first (`uint[14] value`),
                // so the array suffix can ride on the type; move it to the name.
                string type0 = TypeText(sm.fullySpecifiedType());
                string tarr = "";
                int tb = type0.IndexOf('[');
                if (tb >= 0) { tarr = type0[tb..]; type0 = type0[..tb]; }
                string type = MapType(type0);
                foreach (var sd in sm.structDeclaratorList().structDeclarator())
                {
                    string raw = sd.GetText();
                    string nm = sd.IDENT()?.GetText() ?? raw;

                    // The member name goes through the same mapping the body does,
                    // or the declaration and every use disagree.  HLSLcc spells a
                    // matrix `vec4 hlslcc_mtx4x4unity_ObjectToWorld[4]`, and the
                    // body refers to it as `unity_ObjectToWorld`; declaring the
                    // prefixed form leaves every reference undeclared.
                    string mapped = Identifier(nm) ?? nm;
                    if (mapped != nm && nm.Length > 0)
                        raw = raw.Replace(nm, mapped);

                    string arr = raw.Contains(mapped) ? raw[(raw.IndexOf(mapped, StringComparison.Ordinal) + mapped.Length)..] : "";
                    members.Add($"{type}|{mapped}{tarr}{arr}");
                }
            }
            if (members.Count == 0) return;

            // `buffer block { T inst[]; }` -- the common HLSLcc shape: one
            // unsized array whose element type is usually a declared struct.
            // The body indexes the MEMBER name, so the StructuredBuffer takes it.
            if (isSsbo && members.Count == 1)
            {
                int bar = members[0].IndexOf('|');
                string elem = members[0][..bar];
                string inst = members[0][(bar + 1)..];
                if (inst.EndsWith("[]", StringComparison.Ordinal))
                {
                    _buffers.Add($"StructuredBuffer<{elem}> {inst[..^2]};");
                    return;
                }
            }

            var body = new List<string>();
            foreach (var m in members.Distinct())
            {
                int bar = m.IndexOf('|');
                body.Add($"    {m[..bar]} {m[(bar + 1)..]};");
            }
            if (isSsbo)
            {
                _buffers.Add($"struct {name}_t\n{{\n{string.Join("\n", body)}\n}};");
                _buffers.Add($"StructuredBuffer<{name}_t> {name};");
            }
            else
            {
                _buffers.Add($"cbuffer {name} : register(b{_cbIndex++})\n{{\n{string.Join("\n", body)}\n}};");
            }
        }

        /// <summary>
        /// Names declared as members of a cbuffer, so a plain global declaration is
        /// not emitted for the same name.
        ///
        /// The GLSL payload declares a matrix twice in effect: a `uniform vec4
        /// hlslcc_mtx4x4X[4]` inside a block (which becomes the cbuffer member) and
        /// the body's use of `X`.  After the name mapping both are `X`, and HLSL
        /// treats a second declaration of the same name as a redefinition.
        /// </summary>
        private HashSet<string> BufferMemberNames()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string b in _buffers)
            {
                int at = b.IndexOf('{');
                if (at < 0) continue;
                foreach (string line in b[(at + 1)..].Split('\n'))
                {
                    string t = line.Trim().TrimEnd(';').Trim();
                    if (t.Length == 0 || t == "}") continue;
                    int sp = t.IndexOf(' ');
                    if (sp <= 0) continue;
                    string name = t[(sp + 1)..].Trim();
                    int br = name.IndexOf('[');
                    if (br > 0) name = name[..br];
                    if (name.Length > 0) set.Add(name);
                }
            }
            return set;
        }

        /// <summary>
        /// Engine-provided names the body references but the payload never declares.
        ///
        /// A GLSL payload is a complete translation unit for the shader, but Unity's
        /// globals arrive through the include chain when the editor compiles the file.
        /// Only the names the body actually mentions are declared here, since an unused
        /// local is an error at higher warning levels.
        /// </summary>
        private IEnumerable<(string type, string name)> EngineBuiltins()
        {
            // `gl_Layer` has no CGIncludes declaration -- it is a GLSL built-in that
            // this route has to spell out itself.
            if (_usedNames.Contains("gl_Layer"))
                yield return ("uint", "gl_Layer");

            // `unity_ProbeVolumeParams` is declared by the include ONLY when
            // `UNITY_LIGHT_PROBE_PROXY_VOLUME` is 1, and that macro defaults to 0
            // (`UnityShaderVariables.cginc`: no `UNITY_NO_LPPV` -> 0).  A game
            // shader that reads it without the matching pragma therefore has no
            // declaration at all, and handing it to the include leaves the name
            // undeclared ("undeclared identifier 'unity_ProbeVolumeParams'").
            //
            // The same holds for every other name in `NeedsLocalDeclaration`: the
            // default include chain (`UnityCG.cginc` and what it pulls in) does not
            // declare them.  Emitting the declaration here is what the DXBC route
            // does with an unnamed `$Globals` member, and it is the only way the
            // name exists at all.
            if (!_engineRequestsLppv)
            {
                foreach (var (type, name) in LocalEngineDecls)
                    if (_usedNames.Contains(name))
                        yield return (type, name);
            }
            else
            {
                // With the pragma on, the probe-volume block comes from the include;
                // only the light names remain local.
                foreach (var (type, name) in LocalEngineDecls)
                    if (!name.StartsWith("unity_ProbeVolume", StringComparison.Ordinal) &&
                        _usedNames.Contains(name))
                        yield return (type, name);
            }
        }

        /// <summary>
        /// Engine names the default include chain does not declare, with the type
        /// the engine uploads.  Order is declaration order in the emitted file.
        /// </summary>
        private static readonly (string type, string name)[] LocalEngineDecls =
        {
            ("float4", "_LightColor0"), ("float4", "_LightColor1"),
            ("float4", "_LightColor2"), ("float4", "_LightColor3"),
            ("float4", "_SpecColor"),
            ("float4", "unity_ProbeVolumeParams"),
            ("float4x4", "unity_ProbeVolumeWorldToObject"),
            ("float3", "unity_ProbeVolumeSizeInv"),
            ("float3", "unity_ProbeVolumeMin"),
        };

        /// <summary>
        /// Engine names the include chain genuinely provides -- only
        /// <c>unity_StereoEyeIndex</c>, which <c>UnityPerDraw</c> declares on every
        /// D3D11 compile, so a local copy would be a redefinition.
        /// </summary>
        private IEnumerable<string> IncludeProvided()
        {
            if (_usedNames.Contains("unity_StereoEyeIndex")) yield return "unity_StereoEyeIndex";
        }

        /// <summary>GLSL `struct X { members };` -> HLSL struct with mapped types.</summary>
        private void ReadStructDecl(GlslParser.StructDeclContext sd)
        {
            string name = sd.IDENT(0)?.GetText() ?? "";
            if (string.IsNullOrEmpty(name) || !_structs.Add(name)) return;
            var body = new List<string>();
            foreach (var sm in sd.structBody().structMember())
            {
                string type0 = TypeText(sm.fullySpecifiedType());
                string tarr = "";
                int tb = type0.IndexOf('[');
                if (tb >= 0) { tarr = type0[tb..]; type0 = type0[..tb]; }
                string type = MapType(type0);
                foreach (var sdel in sm.structDeclaratorList().structDeclarator())
                {
                    string raw = sdel.GetText();
                    string nm = sdel.IDENT()?.GetText() ?? raw;
                    string arr = raw.Contains(nm) ? raw[(raw.IndexOf(nm, StringComparison.Ordinal) + nm.Length)..] : "";
                    body.Add($"    {type} {nm}{tarr}{arr};");
                }
            }
            if (body.Count > 0)
                _buffers.Add($"struct {name}\n{{\n{string.Join("\n", body)}\n}};");
        }

        private readonly HashSet<string> _structs = new(StringComparer.Ordinal);
        private int _cbIndex;

        private void ReadVariables(GlslParser.VariableDeclContext v)
        {
            var type = v.fullySpecifiedType();
            string storage = StorageOf(type);
            string typeName = TypeText(type);

            foreach (var d in v.declaratorList().declarator())
            {
                string declared = d.IDENT().GetText();
                string name = Identifier(declared) ?? declared;

                // HLSLcc packs a matrix as `vec4 hlslcc_mtx4x4<name>[4]`; the
                // prefix is a naming convention, and the array is the matrix.
                bool matrix = declared.StartsWith("hlslcc_mtx4x4", StringComparison.Ordinal);
                string text = matrix ? "float4" : MapType(typeName);

                // The array suffix belongs to the declarator (`name[4]`).
                string arr = "";
                string raw = d.GetText();
                int br = raw.IndexOf('[');
                if (br >= 0)
                {
                    int close = raw.IndexOf(']', br);
                    if (close > br) arr = raw[br..(close + 1)];
                }

                if (storage == "uniform") _uniforms.Add((text, name + arr));
                else if (storage == "in" && _vertex) _ins.Add((text, name + arr));
                else if (storage == "out" && _vertex)
                    _varyingOut.Add((text, name + arr, VaryingSemantic(_varyingOut)));
                else if (storage == "in" && !_vertex)
                    _varyingIn.Add((text, name + arr, VaryingSemantic(_varyingIn)));
                else if (storage == "out" && !_vertex) _outs.Add((text, name + arr));
                else _locals.Add(text + " " + name + arr);
            }
        }

        private static string StorageOf(GlslParser.FullySpecifiedTypeContext t)
        {
            foreach (var q in t.typeQualifier())
            {
                var s = q.storageQualifier()?.GetText();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return "";
        }

        private static string TypeText(GlslParser.FullySpecifiedTypeContext t) =>
            t.typeSpecifier()?.GetText() ?? "float";

        private sealed class Counter : BaseErrorListener
        {
            public int Count;
            public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer,
                                             IToken offendingSymbol, int line, int charPositionInLine,
                                             string msg, RecognitionException e) => Count++;
        }

        // ------------------------------------------------------------------ emit

        private string Emit()
        {
            _sb.Append("// ---- ").Append(_vertex ? "VERTEX" : "FRAGMENT").Append(" ----\n");

            foreach (var b in _buffers)
                _sb.Append(b).Append('\n');
            if (_buffers.Count > 0) _sb.Append('\n');

            var samplers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (type, name) in _uniforms)
            {
                if (!type.StartsWith("sampler", StringComparison.Ordinal)) continue;
                samplers.Add(name);
                _samplers.Add(name);
                _samplerKind[name] = type;
                _samplerKind[name] = type;
                _samplers.Add(name);
                string tex = type switch
                {
                    "sampler3D" => "Texture3D",
                    "samplerCube" => "TextureCube",
                    "sampler2DArray" => "Texture2DArray",
                    _ => "Texture2D",
                };
                _sb.Append(tex).Append(' ').Append(name).Append(";\n");
                _sb.Append("SamplerState sampler_").Append(name).Append(";\n");
            }
            // Names already declared inside a cbuffer must not be declared again:
            // HLSL rejects the redefinition ("redefinition of '_SpecColor'"), and
            // the cbuffer member is the one the engine actually binds.
            var inBuffer = BufferMemberNames();

            // Engine built-ins the GLSL body reads but never declares itself, and
            // which no include provides: `gl_Layer` is a GLSL built-in with no
            // HLSL/CGIncludes equivalent.  Declared on demand only -- adding the
            // whole set unconditionally would trip "unused variable" elsewhere.
            //
            // Anything Unity's own CGIncludes declares is NOT declared here; it is
            // requested through the include instead (see `IncludeProvided`), because
            // a local copy of `unity_StereoEyeIndex` is a redefinition of the one in
            // `UnityPerDraw`.
            foreach (var (type, name) in EngineBuiltins())
            {
                if (inBuffer.Contains(name)) continue;
                _sb.Append(type).Append(' ').Append(name).Append(";\n");
            }
            foreach (string name in IncludeProvided())
            {
                if (inBuffer.Contains(name)) continue;
                if (!_engineNames.Contains(name)) _engineNames.Add(name);
            }

            // Uniforms split three ways, and the split is what makes the values
            // arrive:
            //
            //   * a Properties-block name is a MATERIAL property, so it has to be
            //     declared in `UnityPerMaterial` -- that is the buffer Unity uploads
            //     into.  A bare `float4 _Color;` compiles and reads zero.
            //   * a name the engine owns is declared by Unity's CGIncludes, so it is
            //     left out and pulled in through `#include "UnityCG.cginc"`.
            //   * anything else is a payload local and stays a plain global.
            var material = new List<(string type, string name)>();
            var engine = new List<(string type, string name)>();
            var plain = new List<(string type, string name)>();
            foreach (var u in _uniforms)
            {
                if (u.type.StartsWith("sampler", StringComparison.Ordinal)) continue;
                string bare = u.name.Split('[')[0];
                if (inBuffer.Contains(bare)) continue;

                // Three destinations, in this order:
                //
                //   1. a Properties entry, or a name Unity derives from one
                //      (`<tex>_ST`), is a material property -> UnityPerMaterial;
                //   2. a name Unity's own includes declare is left to the include
                //      chain, because the engine fills those buffers by name;
                //   3. anything else is a constant the material owns, so it also goes
                //      in UnityPerMaterial -- the same buffer the DXBC route calls
                //      `$Globals`.
                //
                // `IsEngineName` is consulted BEFORE the is-a-property check, because
                // a shader may list an engine global in Properties without owning it,
                // and declaring it locally then collides with the include's copy.
                // What decides ownership is who fills the value.
                if (IsEngineName(bare)) engine.Add(u);
                else material.Add(u);
            }

            if (engine.Count > 0)
            {
                // Declare them through the include chain rather than locally: the
                // engine fills `UnityPerFrame`/`UnityPerDraw` by name, so a private
                // copy of `unity_ObjectToWorld` is a variable nothing ever writes.
                foreach (var (_, name) in engine) _engineNames.Add(name);
            }
            if (material.Count > 0)
            {
                // Both stages declare properties, and they are not the same set: the
                // vertex reads `_Color`/`_FlashColor`, the fragment reads a dozen
                // more.  Emitting a block per stage would define the shared names
                // twice, so this stage's block is suppressed and the union is
                // collected for the merged file to emit once.
                foreach (var (type, name) in material)
                    _materialUnion.TryAdd(name, type);
            }
            foreach (var (type, name) in plain)
                _sb.Append(type).Append(' ').Append(name).Append(";\n");
            _sb.Append('\n');
            // Helper functions the GLSL payload defines (HLSLcc's
            // uint_bitfieldExtract and friends): same statement renderer, but
            // `return expr;` keeps its operand -- only main's bare `return`
            // is rewritten to the output variable.
            foreach (var h in _helpers)
            {
                var ps = new List<string>();
                if (h.parameterList() != null)
                    foreach (var p in h.parameterList().parameterDeclaration())
                    {
                        string pt = MapType(TypeText(p.fullySpecifiedType()));
                        string pn = p.IDENT()?.GetText() ?? "";
                        if (pn.Length > 0) ps.Add($"{pt} {pn}");
                        else ps.Add(pt);
                    }
                _sb.Append(MapType(TypeText(h.fullySpecifiedType())))
                   .Append(' ').Append(h.IDENT().GetText())
                   .Append('(').Append(string.Join(", ", ps)).Append(")\n{\n");
                if (h.compoundStatement() != null)
                    foreach (var st in h.compoundStatement().statement())
                    {
                        string text = Render(st)?.Trim();
                        if (string.IsNullOrEmpty(text)) continue;
                        foreach (var line in text.Split('\n'))
                        {
                            string l = line.Trim();
                            if (l.Length == 0) continue;
                            _sb.Append("    ").Append(l).Append('\n');
                        }
                    }
                _sb.Append("}\n\n");
            }

            // Vertex inputs are HLSL parameters, not file-scope globals.  The GLSL
            // body names them `in_XXX0`; declaring them as globals leaves them
            // unseeded (all zero), so the geometry collapses to a point.  Emitting
            // an appdata struct and seeding the names from it is what makes the
            // position and colour actually arrive.
            bool appdata = _vertex && _ins.Count > 0;
            if (appdata)
            {
                _sb.Append("struct appdata\n{\n");
                for (int i = 0; i < _ins.Count; i++)
                {
                    var (type, name) = _ins[i];
                    _sb.Append($"    {type} {name} : {InputSemantic(name, i)};\n");
                }
                _sb.Append("};\n\n");
            }

            // The entry point's shape depends on whether the payload has
            // interpolators.  A vertex program that writes them has to return a
            // struct carrying them -- a bare `: SV_Position` return throws them
            // away, which is how a sprite shader lost its vertex colour and
            // rendered fully transparent.  A fragment program reads its inputs from
            // a matching struct parameter.
            bool varying = _vertex ? _varyingOut.Count > 0 : _varyingIn.Count > 0;
            string structName = _vertex ? "vs_out" : "vs_in";
            if (varying)
            {
                var fields = new List<string>();
                if (_vertex)
                {
                    fields.Add("    float4 pos : SV_POSITION;");
                    for (int i = 0; i < _varyingOut.Count; i++)
                        fields.Add($"    {_varyingOut[i].type} {_varyingOut[i].name} : {_varyingOut[i].semantic};");
                }
                else
                {
                    // The fragment's struct mirrors the vertex's: the vertex writes
                    // fields in its own order and the fragment reads them in the
                    // same order, so the semantics only have to agree between the
                    // two stages.  `_varyingIn` is filled from this stage's own
                    // `in` declarations, which the payload lists in the same order
                    // the vertex declared its `out`s.
                    for (int i = 0; i < _varyingIn.Count; i++)
                        fields.Add($"    {_varyingIn[i].type} {_varyingIn[i].name} : TEXCOORD{i};");
                }
                if (fields.Count > 0)
                {
                    _sb.Append("struct ").Append(structName).Append("\n{\n");
                    foreach (string f in fields) _sb.Append(f).Append('\n');
                    _sb.Append("};\n\n");
                }
            }

            // A fragment program with more than one colour target returns a struct,
            // exactly like the DXBC route: `float4 frag(...) : SV_Target` can only
            // name one target, and the payload's `SV_Target1..3` stores then have
            // nowhere to go -- three of four targets of an MRT/merge shader were
            // silently dropped.
            bool mrt = !_vertex && _outs.Select(o => o.name).Distinct().Count() > 1;
            if (mrt)
            {
                _sb.Append("struct _frag_out\n{\n");
                foreach (var (_, name) in _outs.Distinct())
                {
                    string idx = name.Length > 1 && char.IsDigit(name[1]) ? name[1..] : "0";
                    _sb.Append($"    float4 {name} : SV_Target{idx};\n");
                }
                _sb.Append("};\n\n");
            }

            _sb.Append(varying
                ? (_vertex ? structName + " main(" : (mrt ? "_frag_out main(" : "float4 main(") + structName + " stage_in")
                : (_vertex ? "float4 main(" : (mrt ? "_frag_out main(" : "float4 main(")));
            if (_vertex)
            {
                var ps = new List<string>();
                if (appdata) ps.Add("appdata v");
                if (_usesInstance) ps.Add("uint sv_instance : SV_InstanceID");
                if (_usesVertex) ps.Add("uint sv_vertex : SV_VertexID");
                if (ps.Count > 0) _sb.Append(string.Join(", ", ps));
            }
            else if (_usesSvPosition)
            {
                _sb.Append("float4 sv_position : SV_Position");
            }
            _sb.Append(varying
                ? (_vertex ? ")\n{\n" : (mrt ? ")\n{\n" : ") : SV_Target\n{\n"))
                : (_vertex ? ") : SV_Position\n{\n" : (mrt ? ")\n{\n" : ") : SV_Target\n{\n")));

            // Vertex inputs are procedure parameters in HLSL -- the payload's
            // `in_XXX0` file-scope names become function locals seeded from the
            // appdata argument.  Leaving them as static globals compiles, but
            // nothing ever writes them, so the position and colour were zero and
            // the mesh collapsed.
            var vertexInputs = _vertex ? _ins : new List<(string type, string name)>();
            if (_usesInstance) _sb.Append("    uint gl_InstanceID = sv_instance;\n");
            if (_usesVertex) _sb.Append("    uint gl_VertexID = sv_vertex;\n");
            foreach (var (type, name) in vertexInputs)
                _sb.Append("    ").Append(type).Append(' ').Append(name).Append(";\n");
            if (vertexInputs.Count > 0)
                foreach (var (_, name) in vertexInputs)
                    _sb.Append("    ").Append(name).Append(" = v.").Append(name).Append(";\n");

            // Temporaries belong to the function, not the file: a global is
            // implicitly const in HLSL and every assignment to it is X3025.
            //
            // The zero literal has to match the declared TYPE: `bool2 b = 0.0;`
            // is not valid HLSL (a bool cannot be constructed from a float), and
            // the payload's boolean temporaries are exactly what a comparison
            // writes into.
            foreach (var l in _locals)
            {
                string zero = l.TrimStart().StartsWith("bool", StringComparison.Ordinal)
                    ? " = false;"
                    : " = 0.0;";
                _sb.Append("    ").Append(l).Append(l.Contains('[') ? ";\n" : zero + "\n");
            }

            if (varying)
            {
                if (_vertex)
                {
                    foreach (var (type, name, _) in _varyingOut)
                        _sb.Append("    ").Append(type).Append(' ').Append(name).Append(" = 0.0;\n");
                }
                else
                {
                    foreach (var (type, name, _) in _varyingIn)
                        _sb.Append("    ").Append(type).Append(' ').Append(name)
                           .Append(" = stage_in.").Append(name).Append(";\n");
                }
                // Output registers are declared in BOTH branches: a fragment pass
                // that has interpolators still writes `SV_Target0`, and skipping the
                // declaration here left `o0` undefined.
                foreach (var (type, name) in _outs.Distinct())
                {
                    if (_usesSvPosition && name == "sv_position") continue;
                    _sb.Append("    ").Append(type).Append(' ').Append(name).Append(" = 0.0;\n");
                }
            }
            else
            {
                foreach (var (type, name) in _outs.Distinct())
                {
                    // sv_position arrives as the entry parameter; a same-named
                    // local would shadow it and X3000s the compile.
                    if (_usesSvPosition && name == "sv_position") continue;
                    _sb.Append("    ").Append(type).Append(' ').Append(name).Append(" = 0.0;\n");
                }
            }
            if (_vertex && !varying && !_outs.Any(o => o.name == "o0"))
                _sb.Append("    float4 o0 = 0.0;\n");
            if (_vertex && varying)
                _sb.Append("    float4 o0 = 0.0;   // gl_Position\n");
            // A fragment program's colour output: the payload writes `SV_Target0`,
            // which maps onto `o0`, and the return statement names it.  Declaring it
            // is what the varying branch above must not skip.
            if (!_vertex && !_outs.Any(o => o.name == "o0"))
                _sb.Append("    float4 o0 = 0.0;\n");

            // Vertex inputs arrive as parameters of a matching struct in HLSL; the
            // GLSL body names them `in_XXX0` at file scope, so they are declared as
            // globals there and never assigned.  A static global that nothing seeds
            // is zero -- the vertex position was literally (0,0,0,0), which is why
            // the geometry collapsed.
            if (_vertex && _ins.Count > 0)
            {
                // Nothing to alias here: the signature is built below, and the
                // parameter struct is emitted by the same code that emits
                // `vs_out`.  See MainSignature.
            }

            if (_main?.compoundStatement() != null)
            {
                // The translated statement text carries the source's own layout,
                // which is GLSLcc output: braces are glued to the preceding
                // expression (`if(u_xlatb0){`) and the body is one flat column.
                // Re-indenting by brace depth is what makes the emitted function
                // readable; trimming alone left every nested statement at the
                // same level as the `if` that guards it.
                int depth = 1;   // 1 = inside the function's own body
                foreach (var st in _main.compoundStatement().statement())
                {
                    string text = Render(st)?.Trim();
                    if (string.IsNullOrEmpty(text)) continue;
                    foreach (var line in text.Split('\n'))
                    {
                        string l = line.Trim();
                        if (l.Length == 0) continue;
                        // The payload's `return;` is the stage's exit.  A vertex
                        // program with interpolators has to hand them over in the
                        // return struct -- returning `o0` alone discards the vertex
                        // colour, which is what made the sprite fully transparent.
                        if (l == "return;" || l == "return o0;")
                        {
                            if (_vertex && varying)
                            {
                                string pad = new string(' ', depth * 4);
                                _sb.Append(pad).Append("{\n");
                                _sb.Append(pad).Append("    vs_out o;\n");
                                _sb.Append(pad).Append("    o.pos = o0;\n");
                                foreach (var (_, name, _) in _varyingOut)
                                    _sb.Append(pad).Append("    o.").Append(name)
                                       .Append(" = ").Append(name).Append(";\n");
                                _sb.Append(pad).Append("    return o;\n");
                                _sb.Append(pad).Append("}\n");
                                continue;
                            }
                            if (!_vertex && _outs.Select(o => o.name).Distinct().Count() > 1)
                            {
                                string pad = new string(' ', depth * 4);
                                _sb.Append(pad).Append("{\n");
                                _sb.Append(pad).Append("    _frag_out _fo;\n");
                                foreach (var (_, name) in _outs.Distinct())
                                    _sb.Append(pad).Append("    _fo.").Append(name)
                                       .Append(" = ").Append(name).Append(";\n");
                                _sb.Append(pad).Append("    return _fo;\n");
                                _sb.Append(pad).Append("}\n");
                                continue;
                            }
                            l = _vertex ? "return o0;"
                                        : "return " + (_outs.Count > 0 ? _outs[0].name : "o0") + ";";
                        }

                        // A line that opens with `}` closes the block that was
                        // open, so it sits one level out from the body it ends.
                        // `} else {` is the exception in spirit but not in form:
                        // it closes one block and opens another, so the level it
                        // belongs to is still the outer one -- it is the *text*
                        // that reads at the inner level, not the line.
                        int opens = l.Count(c => c == '{');
                        int closes = l.Count(c => c == '}');

                        int lead = l.StartsWith("}", StringComparison.Ordinal) ? depth - 1 : depth;
                        if (lead < 0) lead = 0;
                        _sb.Append(new string(' ', lead * 4)).Append(l).Append('\n');

                        // The depth moves by the line's net brace change; the `}`
                        // that decided `lead` is a real close and returns the
                        // level to the enclosing block for the next statement.
                        depth += opens - closes;
                        if (depth < 1) depth = 1;
                    }
                }
            }

            _sb.Append("}\n\n");

            // The payload unrolled every matrix-vector product into one assignment
            // per row; put them back.  See MatrixChain.
            var folded = MatrixChain.Fold(_sb.ToString().Split('\n').ToList());
            _sb.Clear();
            foreach (string one in folded) _sb.Append(one).Append('\n');
            return _sb.ToString();
        }

        /// <summary>
        /// The vertex-input semantic for a payload name.
        ///
        /// HLSLcc names its stage inputs after the D3D semantics it was fed, so the
        /// name itself carries the answer: `in_POSITION0` is POSITION, `in_COLOR0`
        /// is COLOR0, `in_TEXCOORD1` is TEXCOORD1.  Anything unrecognised falls back
        /// to a positional TEXCOORD, which is what Unity binds last.
        /// </summary>
        private static string InputSemantic(string name, int index)
        {
            string n = name.StartsWith("in_", StringComparison.Ordinal) ? name[3..] : name;
            var m = System.Text.RegularExpressions.Regex.Match(n, @"^([A-Za-z]+)(\d*)$");
            if (!m.Success) return "TEXCOORD" + index;
            string stem = m.Groups[1].Value.ToUpperInvariant();
            string idx = m.Groups[2].Value;
            return stem switch
            {
                "POSITION" => "POSITION",
                "NORMAL" => "NORMAL",
                "TANGENT" => "TANGENT",
                "COLOR" => "COLOR" + (idx.Length > 0 ? idx : "0"),
                "TEXCOORD" => "TEXCOORD" + (idx.Length > 0 ? idx : "0"),
                "BLENDWEIGHTS" => "BLENDWEIGHTS",
                "BLENDINDICES" => "BLENDINDICES",
                _ => "TEXCOORD" + index,
            };
        }

        private static string MapType(string gl) => gl switch
        {
            "vec2" => "float2", "vec3" => "float3", "vec4" => "float4",
            "ivec2" => "int2", "ivec3" => "int3", "ivec4" => "int4",
            "uvec2" => "uint2", "uvec3" => "uint3", "uvec4" => "uint4",
            "bvec2" => "bool2", "bvec3" => "bool3", "bvec4" => "bool4",
            "mat3" => "float3x3", "mat4" => "float4x4",
            "mat2" => "float2x2",
            _ => gl,
        };

        // ------------------------------------------------------------- rewriting

        private struct Edit
        {
            public int Start, Stop;
            public string Text;
            public Edit(int s, int e, string t) { Start = s; Stop = e; Text = t; }
        }

        /// <summary>
        /// Print a node: the original bytes, with only the rewritten children
        /// substituted.  That is what keeps the transliteration honest — an
        /// expression the rewriter does not understand comes out exactly as the
        /// compiler wrote it.
        /// </summary>
        private string Render(ParserRuleContext ctx)
        {
            if (ctx == null) return "";
            string custom = Custom(ctx);
            if (custom != null) return custom;

            var edits = new List<Edit>();
            for (int i = 0; i < ctx.ChildCount; i++)
            {
                var child = ctx.GetChild(i);
                if (child is ParserRuleContext rc && rc.Start != null && rc.Stop != null)
                {
                    string t = Render(rc);
                    string orig = _src.Substring(rc.Start.StartIndex, rc.Stop.StopIndex - rc.Start.StartIndex + 1);
                    if (!string.Equals(t, orig, StringComparison.Ordinal))
                        edits.Add(new Edit(rc.Start.StartIndex, rc.Stop.StopIndex, t));
                }
                else if (child is ITerminalNode tn)
                {
                    string t = TokenText(tn.Symbol);
                    if (t != null) edits.Add(new Edit(tn.Symbol.StartIndex, tn.Symbol.StopIndex, t));
                }
            }
            return Apply(ctx.Start.StartIndex, ctx.Stop.StopIndex, edits);
        }

        private string Apply(int start, int stop, List<Edit> edits)
        {
            string text = _src.Substring(start, stop - start + 1);
            edits.Sort((a, b) => b.Start.CompareTo(a.Start));
            foreach (var e in edits)
            {
                int s = e.Start - start, len = e.Stop - e.Start + 1;
                text = text[..s] + e.Text + text[(s + len)..];
            }
            return text;
        }

        private string Custom(ParserRuleContext ctx)
        {
            switch (ctx)
            {
                case GlslParser.NamedCallContext nc: return Call(nc.IDENT().GetText(), Args(nc.functionArgumentList()));
                case GlslParser.ConstructorCallContext cc:
                    return Ctor(Render(cc.typeSpecifierNonArray()), Args(cc.functionArgumentList()));
                // GLSL `return;` hands back whatever was written to the outputs;
                // HLSL has to name it, or the compiler rejects the path (X3507).
                case GlslParser.ReturnStmtContext r when r.expression() == null:
                    return "return o0;";
                default: return null;
            }
        }

        private List<string> Args(GlslParser.FunctionArgumentListContext a)
        {
            var res = new List<string>();
            var list = a?.assignmentExpression();
            if (list == null) return res;
            foreach (var e in list) res.Add(Render(e));
            return res;
        }

        private string Ctor(string type, List<string> args)
        {
            string mapped = MapType(type);
            if (!BuiltinTypes.Contains(mapped)) return Call(type, args);
            if (args.Count == 1 && (mapped.StartsWith("float") || mapped.StartsWith("int")
                                    || mapped.StartsWith("uint") || mapped.StartsWith("bool")))
                return $"({mapped})({args[0]})";
            return null;
        }

        /// <summary>
        /// Calls: sampling and the GLSL spellings HLSL does not have.  Everything
        /// else keeps its name and arguments.
        /// </summary>
        private string Call(string name, List<string> args)
        {
            if (name is "texture" && args.Count == 2 && _samplers.Contains(args[0]))
            {
                string sampler = args[0];
                string coord = args[1];
                // GLSL passes a vec4 coordinate; .Sample takes a float2.
                // A 2D fetch narrows the vec4 coordinate to .xy; a cube or 3D
                // fetch takes three components and must keep them.
                string kind = _samplerKind.TryGetValue(sampler, out var k) ? k : "sampler2D";
                if (kind is "sampler2D" or "sampler2DArray")
                {
                    var m = Swizzle.Match(coord);
                    coord = m.Success ? $"({m.Groups[1].Value}).xy" : $"({coord}).xy";
                }
                return $"{sampler}.Sample(sampler_{sampler}, {coord})";
            }
            // GLSL's vector relations return a BOOLEAN VECTOR; HLSL's comparison
            // operators return one too, so the spelling carries over -- but the
            // result is consumed by `any()`/`all()`/`? :`, and a bare bool vector
            // cannot be multiplied or selected on.  Spelling the comparison as a
            // per-component 0/-1 mask is what the DXBC route does and what every
            // downstream use here expects (a `bvec` local is declared boolN, so the
            // cast is explicit and the mask is recovered where it is read).
            if (Relations.TryGetValue(name, out var op) && args.Count == 2)
                return $"(({args[0]}) {op} ({args[1]}))";

            // `any(v)` / `all(v)` exist in HLSL under the same names and take the
            // same argument, so they pass through unchanged.

            if (name == "textureLod" && args.Count == 3)
                return $"{args[0]}.SampleLevel(sampler_{args[0]}, {args[1]}, {args[2]})";

            // Integer-coordinate fetch.  GLSL's texelFetch takes (sampler, ivecN,
            // lod) and returns the unfiltered texel; HLSL's equivalent is
            // Texture.Load(int3(x, y, lod)) on a 2D texture.  The coordinate has to
            // be widened to an int3 with the mip in z, which is why this is not a
            // plain rename.
            if (name is "texelFetch" or "texelFetchOffset" && args.Count >= 2 && _samplers.Contains(args[0]))
            {
                string sampler = args[0];
                string coord = args[1];
                string lod = args.Count > 2 ? args[2] : "0";
                var m = Swizzle.Match(coord);
                string xy = m.Success ? m.Groups[1].Value : coord;
                return $"{sampler}.Load(int3({xy}, {lod}))";
            }

            // Texture dimensions.  GLSL returns ivecN; HLSL needs the texture
            // object's own accessors, so the call becomes a GetDimensions query --
            // expressed here as a helper the caller can inline.
            if (name is "textureSize" && args.Count >= 1 && _samplers.Contains(args[0]))
            {
                string sampler = args[0];
                return $"({sampler}.GetDimensions)";
            }

            return null;
        }

        private static readonly System.Text.RegularExpressions.Regex Swizzle =
            new(@"^(\w[\w.\[\]]*)\.([xyzw]+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private string TokenText(IToken t)
        {
            switch (t.Type)
            {
                case GlslLexer.VEC2: return "float2";
                case GlslLexer.VEC3: return "float3";
                case GlslLexer.VEC4: return "float4";
                case GlslLexer.IVEC2: return "int2";
                case GlslLexer.IVEC3: return "int3";
                case GlslLexer.IVEC4: return "int4";
                case GlslLexer.UVEC2: return "uint2";
                case GlslLexer.UVEC3: return "uint3";
                case GlslLexer.UVEC4: return "uint4";
                case GlslLexer.BVEC2: return "bool2";
                case GlslLexer.BVEC3: return "bool3";
                case GlslLexer.BVEC4: return "bool4";
                case GlslLexer.MAT2: return "float2x2";
                case GlslLexer.MAT3: return "float3x3";
                case GlslLexer.MAT4: return "float4x4";
                case GlslLexer.IDENT: return Identifier(t.Text);
                default: return null;
            }
        }

        private string Identifier(string s)
        {
            if (_vertex)
            {
                if (s == "gl_Position") return "o0";
                if (s.StartsWith("in_", StringComparison.Ordinal)) return s;
                if (s.StartsWith("vs_", StringComparison.Ordinal)) return s[3..].ToLowerInvariant();
            }
            else
            {
                if (s == "gl_FragCoord") return "sv_position";
                if (s.StartsWith("vs_", StringComparison.Ordinal)) return s[3..].ToLowerInvariant();
                // `SV_TargetN` -> `oN`.  Mapping every one of them onto `o0` (which
                // is what a blanket `StartsWith(SV_Target)` did) made a
                // multi-render-target shader write all its targets into the first
                // one: `SV_Target0..3` became four stores to the same register, and
                // three of them were then dead-code-eliminated.  A merge/MRT shader
                // rendered only its first target and still compiled.
                if (s.StartsWith("SV_Target", StringComparison.Ordinal))
                {
                    string idx = s["SV_Target".Length..];
                    return idx.Length == 0 ? "o0" : "o" + idx;
                }
            }
            if (s.StartsWith("hlslcc_mtx4x4", StringComparison.Ordinal)) return s["hlslcc_mtx4x4".Length..];
            return Renamed.TryGetValue(s, out var t) ? t : null;
        }

        private static readonly Dictionary<string, string> Relations = new(StringComparer.Ordinal)
        {
            ["lessThan"] = "<", ["lessThanEqual"] = "<=",
            ["greaterThan"] = ">", ["greaterThanEqual"] = ">=",
            ["equal"] = "==", ["notEqual"] = "!=",
        };

        private static readonly HashSet<string> BuiltinTypes = new(StringComparer.Ordinal)
        {
            "void", "float", "double", "half", "int", "uint", "bool",
            "float2", "float3", "float4", "int2", "int3", "int4",
            "uint2", "uint3", "uint4", "bool2", "bool3", "bool4",
            "float2x2", "float3x3", "float4x4",
            "sampler1D", "sampler2D", "sampler3D", "samplerCube", "sampler2DArray",
        };

        private static readonly Dictionary<string, string> Renamed = new(StringComparer.Ordinal)
        {
            ["mix"] = "lerp",
            ["fract"] = "frac",
            ["mod"] = "fmod",
            ["dFdx"] = "ddx",
            ["dFdy"] = "ddy",
            ["roundEven"] = "round",
            ["inversesqrt"] = "rsqrt",
            ["intBitsToFloat"] = "asfloat",
            ["floatBitsToInt"] = "asint",
            ["floatBitsToUint"] = "asuint",
            ["uintBitsToFloat"] = "asfloat",

            // OpenVR's single-pass-stereo built-in.  Unity's own shaders read
            // `unity_StereoEyeIndex` for the same value, and that is what the
            // CGIncludes declare, so the GLSL spelling is mapped onto it rather
            // than declared locally (the engine fills it per draw call).
            ["gl_ViewID_OVR"] = "unity_StereoEyeIndex",
        };
    }
}
