using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.SPIRV.Cross;

namespace ShaderRipper.Plugin.SpirV
{
    /// <summary>
    /// The SPIRV-Cross call sequence, wrapped so a failure is a null instead of an
    /// exception.
    ///
    /// The C API is used rather than the C++ interface: it has a stable ABI, so the
    /// same sequence works against whatever build of the native library is on the
    /// machine, and the handles are opaque pointers rather than vtables.  The context
    /// owns everything created from it -- the API exposes no per-compiler destroy --
    /// so one context is created per module and destroyed at the end.
    /// </summary>
    internal static unsafe class SpirvCross
    {
        private static readonly object Gate = new();
        private static Cross _api;
        private static string _loadError;

        /// <summary>The loaded API, or null when the native library is unavailable.</summary>
        public static Cross Api
        {
            get
            {
                lock (Gate)
                {
                    if (_api != null || _loadError != null) return _api;
                    try
                    {
                        // The binding loads the native library by bare name, and it
                        // probes the *entry* assembly's directory -- which belongs to
                        // the host, not to this plugin, so a plugin that ships its own
                        // native binary finds nothing.  Loading the file here by full
                        // path puts the module in the process first, after which the
                        // bare-name lookup succeeds by name alone.
                        PreloadNative();
                        _api = Cross.GetApi();
                    }
                    catch (Exception e) { _loadError = e.GetType().Name + ": " + e.Message; }
                    return _api;
                }
            }
        }

        /// <summary>
        /// Load this plugin's own copy of SPIRV-Cross, if it shipped one.
        ///
        /// NuGet puts the binary under <c>runtimes/&lt;rid&gt;/native</c> beside the
        /// plugin assembly.  A failure here is not reported: the binding's own probing
        /// may still succeed, and its error is the more useful one.
        /// </summary>
        private static void PreloadNative()
        {
            string assembly = typeof(SpirvCross).Assembly.Location;
            if (string.IsNullOrEmpty(assembly)) return;
            string root = Path.GetDirectoryName(assembly);
            if (string.IsNullOrEmpty(root)) return;

            string rid = RuntimeInformation.RuntimeIdentifier;
            string name = OperatingSystem.IsWindows() ? "spirv-cross.dll"
                        : OperatingSystem.IsMacOS() ? "libspirv-cross.dylib"
                        : "libspirv-cross.so";

            foreach (string candidate in new[]
                     {
                         Path.Combine(root, "runtimes", rid, "native", name),
                         Path.Combine(root, name),
                     })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    if (NativeLibrary.TryLoad(candidate, out _)) return;
                }
                catch { }
            }
        }

        /// <summary>Why <see cref="Api"/> is null, or null when it loaded.</summary>
        public static string LoadError => _loadError;

        /// <summary>
        /// Translate one SPIR-V module to HLSL.
        ///
        /// <paramref name="names"/> supplies the resource names the module does not
        /// carry, and <paramref name="vertex"/> selects the attribute semantic table,
        /// because the two stages name their interfaces differently.
        /// Returns null when SPIRV-Cross rejects the module, with the reason in
        /// <paramref name="error"/>.
        /// </summary>
        public static string Compile(byte[] spirv, SpirvPayload names, bool vertex, out string error)
        {
            return Compile(spirv, names, vertex, null, out error);
        }

        /// <summary>
        /// Translate one SPIR-V module to HLSL, tagging a diagnostic dump with
        /// <paramref name="blobTag"/>.  See the other overload for the contract.
        /// </summary>
        public static string Compile(byte[] spirv, SpirvPayload names, bool vertex,
                                     string blobTag, out string error,
                                     IReadOnlyDictionary<int, string> vertexSemantics = null)
        {
            error = null;
            Cross api = Api;
            if (api == null)
            {
                error = "SPIRV-Cross is unavailable (" + (LoadError ?? "unknown") + ")";
                return null;
            }
            if (spirv == null || spirv.Length < 20 || spirv.Length % 4 != 0)
            {
                error = "module is not a whole number of SPIR-V words";
                return null;
            }

            uint[] words = new uint[spirv.Length / 4];
            Buffer.BlockCopy(spirv, 0, words, 0, spirv.Length);

            Context* ctx = null;
            try
            {
                if (api.ContextCreate(&ctx) != Result.Success) { error = "ContextCreate failed"; return null; }

                ParsedIr* ir;
                if (api.ContextParseSpirv(ctx, ref words[0], (nuint)words.Length, &ir) != Result.Success)
                {
                    error = LastError(api, ctx);
                    return null;
                }

                Compiler* compiler;
                if (api.ContextCreateCompiler(ctx, Backend.Hlsl, ir, CaptureMode.Copy, &compiler) != Result.Success)
                {
                    error = LastError(api, ctx);
                    return null;
                }

                ApplyOptions(api, compiler);
                if (vertex) ApplyVertexAttributes(api, compiler, spirv, vertexSemantics);

                byte* source;
                if (api.CompilerCompile(compiler, &source) != Result.Success)
                {
                    error = LastError(api, ctx);
                    return null;
                }

                string text = new string((sbyte*)source);
                text = Rename(text, names, vertex, spirv);
                text = Flatten(text, vertex);
                text = FoldMatrixMul(text);

                string debugDir = Environment.GetEnvironmentVariable("SHADERRIP_SPIRVDBG");
                if (!string.IsNullOrEmpty(debugDir))
                {
                    try
                    {
                        Directory.CreateDirectory(debugDir);
                        string tag = vertex ? "vs" : "ps";
                        File.WriteAllText(Path.Combine(debugDir,
                            $"{blobTag}_{tag}.hlsl"), text);
                    }
                    catch { }
                }
                return text;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return null;
            }
            finally
            {
                if (ctx != null) api.ContextDestroy(ctx);
            }
        }

        private static string LastError(Cross api, Context* ctx)
        {
            try
            {
                string message = api.ContextGetLastErrorStringS(ctx);
                return string.IsNullOrEmpty(message) ? "SPIRV-Cross reported no reason" : message;
            }
            catch { return "SPIRV-Cross reported no reason"; }
        }

        /// <summary>
        /// Ask for shader model 5.0.
        ///
        /// The default is model 3, which rejects <c>roundEven</c> and the other
        /// rounding opcodes Unity's GLSL produces -- "roundEven is not supported in
        /// HLSL shader model 2/3" -- so most fragment modules fail to translate at the
        /// default.  5.0 is what the register-level output needs anyway.
        /// </summary>
        private static void ApplyOptions(Cross api, Compiler* compiler)
        {
            CompilerOptions* options;
            if (api.CompilerCreateCompilerOptions(compiler, &options) != Result.Success) return;
            api.CompilerOptionsSetUint(options, CompilerOption.HlslShaderModel, 50);
            api.CompilerOptionsSetBool(options, CompilerOption.HlslPointSizeCompat, 1);
            api.CompilerInstallCompilerOptions(compiler, options);
        }

        /// <summary>
        /// Give the vertex inputs the semantics a mesh stream supplies.
        ///
        /// SPIR-V records only a location, and ShaderLab needs <c>POSITION</c>,
        /// <c>TEXCOORD0</c> and friends.  So each location is named explicitly, which
        /// also keeps them from shifting: SPIRV-Cross assigns its own generic
        /// <c>TEXCOORDn</c> to everything it was not told about, and naming only the
        /// first input offsets every later one by the number of names supplied.
        ///
        /// A sibling platform's table wins when there is one.  The GLSL build of the
        /// same shader spells every input's semantic in its own source
        /// (<c>in vec4 in_COLOR0;</c>), and both backends number locations in the same
        /// order, so that table is evidence and the positional guess below is not:
        /// binding location 1 to <c>TEXCOORD0</c> when the mesh streams a vertex
        /// color there is what turned every translation from a color-carrying slot
        /// into a blank render -- the fragment read a UV as its tint and multiplied
        /// by whatever lay in that stream.
        /// </summary>
        private static void ApplyVertexAttributes(Cross api, Compiler* compiler, byte[] spirv,
                                                  IReadOnlyDictionary<int, string> vertexSemantics)
        {
            SortedSet<int> locations = InputLocations(spirv);
            if (locations.Count == 0) return;

            var pinned = new List<GCHandle>(locations.Count);
            var entries = new HlslVertexAttributeRemap[locations.Count];
            try
            {
                int i = 0;
                foreach (int location in locations)
                {
                    // The sibling table first; the positional fallback only for a
                    // location it does not mention.
                    string semantic = vertexSemantics != null &&
                                      vertexSemantics.TryGetValue(location, out string known)
                        ? known
                        : location == 0 ? "POSITION" : "TEXCOORD" + (location - 1);
                    pinned.Add(GCHandle.Alloc(Encoding.ASCII.GetBytes(semantic + "\0"), GCHandleType.Pinned));
                    entries[i++] = new HlslVertexAttributeRemap
                    {
                        Location = (uint)location,
                        Semantic = (byte*)pinned[^1].AddrOfPinnedObject(),
                    };
                }

                fixed (HlslVertexAttributeRemap* p = entries)
                    api.CompilerHlslAddVertexAttributeRemap(compiler, p, (nuint)i);
            }
            finally
            {
                foreach (GCHandle handle in pinned) handle.Free();
            }
        }

        /// <summary>Every <c>Input</c> variable's location, ascending.</summary>
        private static SortedSet<int> InputLocations(byte[] spirv)        {
            var result = new SortedSet<int>();
            uint[] words = new uint[spirv.Length / 4];
            Buffer.BlockCopy(spirv, 0, words, 0, spirv.Length);

            var inputs = new HashSet<uint>();
            var locations = new Dictionary<uint, int>();
            int at = 5;   // past the five-word header
            while (at < words.Length)
            {
                int op = (int)(words[at] & 0xFFFF);
                int wordCount = (int)(words[at] >> 16);
                if (wordCount <= 0) break;

                // OpVariable, storage class 1 = Input.
                if (op == 59 && wordCount >= 4 && words[at + 3] == 1) inputs.Add(words[at + 2]);
                // OpDecorate, decoration 30 = Location.
                if (op == 71 && wordCount >= 4 && words[at + 2] == 30) locations[words[at + 1]] = (int)words[at + 3];

                at += wordCount;
            }

            foreach (var kv in locations)
                if (inputs.Contains(kv.Key)) result.Add(kv.Value);
            return result;
        }

        // ------------------------------------------------------------------
        // Naming
        // ------------------------------------------------------------------

        /// <summary>
        /// Replace the identifiers SPIRV-Cross invented with the names Unity recorded.
        ///
        /// SPIRV-Cross names a resource after its SPIR-V id (<c>_13</c>) and pairs it
        /// with a sampler named <c>__13_sampler</c>.  Both appear in the output exactly
        /// as the declaration, the sampler and every <c>.Sample</c> argument, so the
        /// rename is a substitution over whole identifiers.  The register each
        /// declaration was given is the one to look the name up by, because it is the
        /// same <c>t#</c> the parameter table is keyed by.
        ///
        /// Constant buffers are named through the module rather than by register.  The
        /// parameter slot records each buffer with the descriptor set and binding the
        /// runtime uses, and the SPIR-V module decorates its block variable with the
        /// same pair, so the two can be joined without guessing: a <c>b#</c> is a
        /// per-stage number and says nothing, but a <c>(set, binding)</c> is global.
        /// The members are then matched by byte offset, which the module records as
        /// <c>OpMemberDecorate MemberOffset</c> and the slot records per member.
        ///
        /// Every identifier SPIRV-Cross generated that was NOT named this way is tagged
        /// with its stage.  Those names are derived from SPIR-V ids, and an id is unique
        /// only WITHIN one module -- both stages of a pass come out of the same glslang
        /// run and number their blocks alike, so an unnamed vertex <c>_26_m0</c> and an
        /// unnamed fragment <c>_26_m0</c> would merge into one declaration even when
        /// they are unrelated.  A <c>vs_</c>/<c>ps_</c> tag keeps them apart without
        /// inventing any fact about them: the tag says which module the id came from,
        /// which is all the name ever meant.
        /// </summary>
        private static string Rename(string text, SpirvPayload names, bool vertex, byte[] spirv)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            string tag = vertex ? "vs_" : "ps_";
            // One property can be bound to two registers in a single program -- Unity
            // keeps a second binding for the same texture, for instance when one
            // sampler state reads it with a different filter.  Renaming both to the
            // property name declares it twice ("redefinition of '_MainTex'"), so the
            // first declaration of each final name wins and any further one gets a
            // numbered suffix: the name stays recognisable and unique, which is what
            // fxc needs from a declaration.
            var taken = new HashSet<string>(StringComparer.Ordinal);

            string Unique(string wanted)
            {
                if (taken.Add(wanted)) return wanted;
                for (int n = 2; ; n++)
                {
                    string candidate = wanted + "_" + n.ToString(CultureInfo.InvariantCulture);
                    if (taken.Add(candidate)) return candidate;
                }
            }

            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                int textureRegister = RegisterOf(trimmed, "register(t");
                if (textureRegister >= 0 && names.Textures.TryGetValue(textureRegister, out string texture))
                {
                    string declared = DeclaredName(trimmed);
                    if (declared != null) map[declared] = Unique(texture);
                    continue;
                }

                // A sampler SPIRV-Cross synthesised next to a texture is named
                // `__<id>_sampler`; the texture's own name decides the sampler's.  It
                // takes the same suffix its texture did, so the pair stays together.
                int samplerRegister = RegisterOf(trimmed, "register(s");
                if (samplerRegister >= 0)
                {
                    string declared = DeclaredName(trimmed);
                    if (declared == null) continue;
                    if (names.Textures.TryGetValue(samplerRegister, out string owned))
                    {
                        map[declared] = Unique("sampler" + owned);
                        continue;
                    }

                    // The payload does not name this one -- a texture type it does not
                    // record, or a sampler bound without a matching table entry.  fxc
                    // requires a sampler's name to identify its texture, so it follows
                    // the texture SPIRV-Cross paired it with: `__<id>_sampler` belongs
                    // to `_<id>`, which this class renames to `<tag>_<id>`.
                    int at = declared.StartsWith("__", StringComparison.Ordinal) ? 2 : 0;
                    int cut = declared.IndexOf('_', at);
                    if (cut <= at) continue;
                    string id = declared.Substring(at, cut - at);
                    if (id.Length == 0) continue;
                    foreach (char ch in id)
                        if (!char.IsDigit(ch)) { id = null; break; }
                    if (id == null) continue;
                    map[declared] = Unique("sampler_" + tag + id);
                }
            }

            foreach (var kv in CBufferNames(spirv, names)) map[kv.Key] = kv.Value;

            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != '_' && !char.IsLetter(c)) { sb.Append(c); i++; continue; }

                int start = i;
                while (i < text.Length && (text[i] == '_' || char.IsLetterOrDigit(text[i]))) i++;
                string word = text.Substring(start, i - start);

                if (map.TryGetValue(word, out string replacement)) sb.Append(replacement);
                else if (Generated(word))
                {
                    // `_12` loses its underscore before the tag so the result reads
                    // `vs_12` rather than `vs__12`; a name that already has a letter
                    // (`u_xlat2`) keeps all of it.
                    sb.Append(tag).Append(word[0] == '_' ? word.AsSpan(1) : word.AsSpan());
                }
                else sb.Append(word);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The constant-buffer names the payload recorded, keyed by the identifier
        /// SPIRV-Cross gave them.
        ///
        /// The module declares a block variable that points at a struct, and SPIRV-Cross
        /// spells the block <c>_&lt;structId&gt;_&lt;variableId&gt;</c> and each of its
        /// members <c>_&lt;variableId&gt;_m&lt;index&gt;</c>.  That is how the two tables
        /// are joined: the variable carries the descriptor the slot's register map names,
        /// and the member's <c>OpMemberDecorate</c> offset is the one the slot records
        /// for its name.
        ///
        /// A descriptor is not unique within a payload -- one shader has a parameter slot
        /// per keyword variant, and the variants do NOT agree: a shader that samples
        /// <c>_ScreenParams</c> in one branch and not another puts its first member at 0
        /// in one slot and at 16 in the next, so every name after it shifts.  Picking the
        /// first record with a matching descriptor therefore names the members from a
        /// different variant's layout, which is worse than leaving them unnamed: the
        /// names would point at the wrong bytes.  The record whose offsets actually line
        /// up with the module's is the one that belongs to it, so the candidates are
        /// scored and the best kept.
        /// </summary>
        private static Dictionary<string, string> CBufferNames(byte[] spirv, SpirvPayload names)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (names == null || names.Buffers.Count == 0) return map;

            foreach (var block in SpirvBlocks.Read(spirv).Values)
            {
                if (block.Set < 0 || block.Binding < 0) continue;

                CBufferRecord record = BestRecord(names.Buffers, block);
                if (record == null) continue;

                map[$"_{block.Struct}_{block.Variable}"] = record.Name;

                // Unity records some engine buffers with their size but no member
                // table -- `UnityPerDraw` is the common one -- so the members come
                // from the engine layout the host already carries, keyed by the row
                // each one starts at.  Only names the engine table actually has are
                // used; a row it does not describe keeps its positional name.
                foreach (var kv in block.MemberOffsets)
                {
                    CBufferMember member = record.Members.Find(m => m.Offset == kv.Value);
                    if (member != null)
                    {
                        map[$"_{block.Variable}_m{kv.Key}"] = member.Name;
                        continue;
                    }

                    var engine = Plugins.Platforms.EngineBuffers.At(record.Name, kv.Value / 16);
                    if (engine == null) continue;
                    map[$"_{block.Variable}_m{kv.Key}"] = engine.Value.Name;
                }
            }
            return map;
        }

        /// <summary>
        /// The recorded buffer that best explains this block.
        ///
        /// A descriptor alone is not enough to choose -- one payload carries a parameter
        /// slot per keyword variant and the variants disagree, so the record has to be
        /// the one whose layout the module actually has.  The test is a subset one: every
        /// member the record names must sit at an offset the module also has.  A record
        /// from a variant that shifted by a prefix fails it, because the offsets it names
        /// do not exist in this module at all, while the record that belongs to the
        /// module passes with every member it knows.
        ///
        /// The subset direction also lets a partly-recorded buffer contribute: Unity
        /// lists only <c>unity_MatrixVP</c> for <c>UnityPerFrame</c>, and naming that one
        /// member is right even though the other ten stay positional.  Requiring the
        /// module's offsets to all be covered instead threw the whole table away.
        ///
        /// Among the records that pass, the one naming the most members wins; ties go to
        /// the closer member count.  A record with NO members still names its block --
        /// Unity records <c>UnityPerDraw</c> as a size and nothing else -- so it ranks
        /// last rather than being skipped, and decides the name only when no record
        /// describes any member.  Null when nothing carries the descriptor.
        /// </summary>
        private static CBufferRecord BestRecord(List<CBufferRecord> records, SpirvBlock block)
        {
            if (block.MemberOffsets.Count == 0) return null;

            CBufferRecord best = null;
            int bestNamed = -1;
            int bestDistance = int.MaxValue;

            foreach (CBufferRecord record in records)
            {
                if (!record.HasBinding || record.Set != block.Set || record.Binding != block.Binding)
                    continue;

                bool fits = true;
                foreach (CBufferMember member in record.Members)
                {
                    if (block.MemberOffsets.ContainsValue(member.Offset)) continue;
                    fits = false;
                    break;
                }
                if (!fits) continue;

                int named = record.Members.Count;
                int distance = Math.Abs(record.Members.Count - block.MemberCount);
                if (best == null || named > bestNamed ||
                    (named == bestNamed && distance < bestDistance))
                {
                    best = record;
                    bestNamed = named;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>
        /// Is this identifier one SPIRV-Cross derived from a SPIR-V id?
        ///
        /// Those are <c>_&lt;id&gt;</c> with an optional suffix, as in <c>_13</c>,
        /// <c>_24_26</c> (a block) and <c>_26_m0</c> (a member).  A name Unity kept --
        /// <c>vs_TEXCOORD0</c> -- never has that shape, and neither does anything this
        /// class invents, so the test only has to recognise the leading number.
        ///
        /// <c>u_xlatN</c>, <c>in_*</c> and <c>SV_TargetN</c> come from the same place
        /// and need the same treatment: SPIRV-Cross names its temporaries after the
        /// HLSLcc convention, and those names are only unique within one module.  A
        /// pass whose vertex stage uses <c>u_xlat2</c> as a <c>float4</c> and whose
        /// fragment stage uses it as a <c>float</c> would otherwise declare it twice
        /// and be rejected.
        /// </summary>
        private static bool Generated(string word)
        {
            if (word.Length < 2) return false;

            // `u_xlatN`, `u_xlatbN`, `in_*`, `out_*` -- HLSLcc's names for the module's
            // own temporaries and interface copies.
            if (word.StartsWith("u_xlat", StringComparison.Ordinal) ||
                word.StartsWith("in_", StringComparison.Ordinal) ||
                word.StartsWith("out_", StringComparison.Ordinal))
                return true;

            if (word[0] != '_' || !char.IsDigit(word[1])) return false;
            for (int i = 1; i < word.Length; i++)
            {
                char c = word[i];
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }
            return true;
        }

        /// <summary>
        /// Fold SPIRV-Cross's two-function shape into the one entry point ShaderLab
        /// takes, and give the interface structs a per-stage name.
        ///
        /// The backend always emits an inner <c>&lt;entry&gt;_main()</c> that works on
        /// module-level statics, plus a <c>main(SPIRV_Cross_Input)</c> wrapper that
        /// copies the inputs in, calls it and builds the output struct.  Two stages
        /// merged into one program block then contain two functions both called
        /// <c>main</c>, which is a redefinition, and both define
        /// <c>SPIRV_Cross_Input</c>/<c>SPIRV_Cross_Output</c> with DIFFERENT members
        /// (the fragment stage's output is its <c>SV_Target</c>), so keeping one copy
        /// leaves the other stage returning the wrong interface.
        ///
        /// Inlining the wrapper into the body function fixes the first and needs no
        /// reordering -- the inlined entry takes the body function's place, so anything
        /// declared before it stays declared before it.  Tagging the two struct names
        /// fixes the second.  The result is one function per stage, named
        /// <c>vert</c>/<c>frag</c>, which is also the name the merge keeps.
        /// </summary>
        private static string Flatten(string text, bool vertex)
        {
            string tag = vertex ? "vs" : "ps";
            string entry = vertex ? "vert" : "frag";
            var lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));

            for (int i = 0; i < lines.Count; i++)
            {
                lines[i] = Replace(lines[i], "SPIRV_Cross_Input", "SPIRV_Cross_Input_" + tag);
                lines[i] = Replace(lines[i], "SPIRV_Cross_Output", "SPIRV_Cross_Output_" + tag);
            }

            // A fragment program with no inputs gets no SPIRV_Cross_Input from
            // SPIRV-Cross, but the entry point it writes still takes one.  Declaring an
            // empty struct is what the module actually says -- nothing is passed in --
            // and without it the parameter type is simply undefined.
            string inputStruct = "struct SPIRV_Cross_Input_" + tag;
            if (!lines.Any(l => l.TrimStart().StartsWith(inputStruct, StringComparison.Ordinal)))
            {
                // One line per statement, so the brace walk below can find the body of
                // a struct this pass had to synthesise as readily as one the backend
                // wrote.
                lines.Insert(0, "};");
                lines.Insert(0, "{");
                lines.Insert(0, inputStruct);
            }

            EnsurePositionInput(lines, tag, vertex);

            int wrapper = lines.FindIndex(l => l.TrimStart().StartsWith("SPIRV_Cross_Output_" + tag + " main(",
                                                                StringComparison.Ordinal));
            if (wrapper < 0) return string.Join("\n", lines);

            int open = lines.FindIndex(wrapper, l => l.Trim() == "{");
            int close = open < 0 ? -1 : MatchBrace(lines, open);
            if (close < 0) return string.Join("\n", lines);

            // The call the wrapper makes is the body function; its definition is what
            // the wrapper is folded into.
            int call = -1;
            for (int i = open + 1; i < close && call < 0; i++)
                if (CalledName(lines[i]) != null) call = i;
            if (call < 0) return string.Join("\n", lines);

            string callee = CalledName(lines[call]);
            // The body function is always emitted before the wrapper.  Any other
            // shape is not one this pass understands, and leaving it alone is
            // better than splicing in the wrong function.
            int head = lines.FindIndex(l => l.Trim() == "void " + callee + "()");
            if (head < 0 || head > wrapper) return string.Join("\n", lines);
            int bodyOpen = lines.FindIndex(head, l => l.Trim() == "{");
            int bodyClose = bodyOpen < 0 ? -1 : MatchBrace(lines, bodyOpen);
            if (bodyClose < 0) return string.Join("\n", lines);

            var result = new List<string>(lines.Count + 8);
            result.AddRange(lines.GetRange(0, head));
            result.Add($"SPIRV_Cross_Output_{tag} {entry}(SPIRV_Cross_Input_{tag} stage_input)");
            result.Add("{");
            result.AddRange(lines.GetRange(open + 1, call - open - 1));
            result.AddRange(lines.GetRange(bodyOpen + 1, bodyClose - bodyOpen - 1));
            // Everything from here on is what the wrapper did after the call; a bare
            // `return` in the body has to do the same thing.
            var epilogue = lines.GetRange(call + 1, close - call - 1);
            result.AddRange(epilogue);
            result.Add("}");
            result.AddRange(lines.GetRange(close + 1, lines.Count - close - 1));
            return RedirectEarlyReturns(string.Join("\n", result), tag, entry, epilogue);
        }

        /// <summary>
        /// Give the fragment stage's input struct an <c>SV_Position</c> member.
        ///
        /// D3D requires the pixel stage to receive the position even when the fragment
        /// program never reads it: a pixel input struct without <c>SV_Position</c> does
        /// not link against a vertex output struct that has one, and Unity reacts to
        /// the failed link by dropping every draw of the material -- no compile error,
        /// no warning, just a sprite that renders fully transparent.
        ///
        /// SPIRV-Cross only emits the member when the module reads <c>gl_FragCoord</c>,
        /// so the overwhelmingly common case arrives without it and has to be supplied
        /// here.  A struct that already carries the semantic is left alone; rewriting
        /// it would declare the member twice.
        /// </summary>
        private static void EnsurePositionInput(List<string> lines, string tag, bool vertex)
        {
            if (vertex) return;

            string decl = "struct SPIRV_Cross_Input_" + tag;
            int head = lines.FindIndex(l => l.TrimStart().StartsWith(decl, StringComparison.Ordinal));
            if (head < 0) return;

            int open = lines.FindIndex(head, l => l.Trim() == "{");
            int close = open < 0 ? -1 : MatchBrace(lines, open);
            if (close < 0) return;

            bool taken = false;
            for (int i = open + 1; i < close; i++)
                if (lines[i].Contains("SV_Position", StringComparison.Ordinal)) return;
                else if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"\bpos\b")) taken = true;

            // `pos` is what the GLSL route names the member, so the two routes read the
            // same; an interpolator that already claimed the identifier gets a suffixed
            // spelling rather than a duplicate declaration.
            string indent = new string(' ', lines[open].Length - lines[open].TrimStart().Length + 4);
            lines.Insert(open + 1, indent + "float4 " + (taken ? "pos0" : "pos") + " : SV_Position;");
        }

        /// <summary>
        /// Replace the body function's bare <c>return;</c> with the entry point's
        /// epilogue.
        ///
        /// The body function SPIRV-Cross emits is <c>void</c>, so a branch that leaves
        /// early says <c>return;</c>.  Inlining the wrapper moves those statements into
        /// a function that has to return the output struct, and fxc rejects the result
        /// outright -- "function must return a value".
        ///
        /// The epilogue is the handful of lines the wrapper writes after the call:
        /// declare the output struct, copy the statics into it, return it.  Repeating
        /// them where the body returned says exactly what the original meant, and each
        /// copy declares its own local so nothing has to be in scope across branches.
        ///
        /// A jump would be shorter, but neither spelling is usable here: <c>goto</c> is
        /// not in the HLSL grammar this project parses with -- the whole program would
        /// stop being readable as a parse tree, and the merge that follows depends on
        /// that tree for every cbuffer it names -- while a <c>do { } while (false)</c>
        /// makes fxc warn "loop only executes for 1 iteration(s)" on every program that
        /// has an early return.
        /// </summary>
        private static string RedirectEarlyReturns(string text, string tag, string entry,
                                                   List<string> epilogue)
        {
            var lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            string head = $"SPIRV_Cross_Output_{tag} {entry}(";
            int start = lines.FindIndex(l => l.TrimStart().StartsWith(head, StringComparison.Ordinal));
            if (start < 0) return text;

            int open = lines.FindIndex(start, l => l.Trim() == "{");
            int close = open < 0 ? -1 : MatchBrace(lines, open);
            if (close < 0) return text;

            var result = new List<string>(lines.Count + 8);
            for (int i = 0; i <= close; i++)
            {
                if (i > open && i < close && lines[i].Trim() == "return;")
                {
                    result.AddRange(epilogue);
                    continue;
                }
                result.Add(lines[i]);
            }
            result.AddRange(lines.GetRange(close + 1, lines.Count - close - 1));
            return string.Join("\n", result);
        }

        /// <summary>
        /// Collapse the four instructions of a matrix-vector product into one
        /// <c>mul(matrix, vector)</c> call.
        ///
        /// SPIR-V has no matrix type for Unity's row-major engine matrices -- the
        /// module stores each row as a plain float4 vector -- so SPIRV-Cross spells
        /// the product out instruction by instruction:
        ///
        /// <code>
        /// t = v.yyyy * M[1];
        /// t = (M[0] * v.xxxx) + t;
        /// t = (M[2] * v.zzzz) + t;    // or the accumulator lives in v itself
        /// t += M[3];
        /// </code>
        ///
        /// That is mathematically the same as <c>mul(M, v)</c> -- and spelling it as
        /// the product is what the original source did, because Unity's own include
        /// files define the helper in those terms.  Folding the four lines back also
        /// removes the extra accumulator every such product carried, which is most of
        /// the statics a vertex shader declares.
        ///
        /// The fold only fires when all four lines are consecutive and read exactly
        /// this shape; anything else is left for the reader to see as SPIRV-Cross
        /// wrote it.
        /// </summary>
        private static string FoldMatrixMul(string text)
        {
            // A matrix declared as `float4 M[4]` cannot feed mul() -- the intrinsic
            // wants a float4x4 -- so a product over such a matrix keeps its spelled-out
            // rows.  SPIRV-Cross spells engine matrices both ways depending on how the
            // module typed them, and the declaration is in this same text, so the
            // check is a scan away.
            //
            // The engine's own matrices (`unity_ObjectToWorld` and friends) are the
            // exception: the module stores them as vector arrays, but the merge hands
            // their declarations to Unity's include files, which spell them
            // `float4x4` -- exactly what mul() wants.  A module-level array declaration
            // for one of those is an artifact of the SPIR-V typing, not of the final
            // program, so the fold applies.
            var arrays = new HashSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex
                         .Matches(text, @"float4\s+(\w+)\s*\[\s*4\s*\]"))
            {
                string name = m.Groups[1].Value;
                if (!name.StartsWith("unity_", StringComparison.Ordinal))
                    arrays.Add(name);
            }

            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            for (int i = 0; i + 4 <= lines.Count; i++)
            {
                var m0 = AccLine(lines[i]);
                if (m0 == null) continue;

                // The accumulator the first line writes is the one the next three
                // read; its name is what the whole product collapses into.
                string acc = m0.Groups[1].Value;
                string vec = m0.Groups[2].Value;
                string mat = m0.Groups[3].Value;
                if (arrays.Contains(mat)) continue;

                var m1 = AddLine(lines[i + 1], acc, vec, mat, 0, "xxxx");
                if (m1 == null) continue;
                var m2 = AddLine(lines[i + 2], m1.Groups[1].Value, vec, mat, 2, "zzzz");
                if (m2 == null) continue;
                // The last row is either `acc += M[3]` (when the accumulator keeps
                // living) or `result = (M[3] * vec.wwww) + acc` (when this product's
                // value is written straight into its destination -- the gl_Position
                // store a vertex shader ends on).
                var m3 = PlusLine(lines[i + 3], m2.Groups[1].Value, mat)
                      ?? LastLine(lines[i + 3], m2.Groups[1].Value, vec, mat);
                if (m3 == null) continue;

                string result = m3.Groups[1].Value;
                // Keep the line's own indentation: the folded statement replaces the
                // first of four lines that already sit inside the function, and
                // dropping the leading whitespace left it flush against the body while
                // everything around it stayed indented.
                string indent = lines[i][..(lines[i].Length - lines[i].TrimStart().Length)];
                lines[i] = $"{indent}{result} = mul({mat}, {vec});";
                lines[i + 1] = null;
                lines[i + 2] = null;
                lines[i + 3] = null;
                i += 3;
            }

            // The object-to-clip pair: `mul(unity_ObjectToWorld, v)` whose result is
            // immediately consumed by `mul(unity_MatrixVP, ...)` assigned to
            // gl_Position is what Unity's own source spells as one call --
            // `UnityObjectToClipPos(v)` -- because that helper also carries the
            // stereo-eye selection a stereo build needs.  Folding the two products
            // back restores the call the original source wrote instead of the two
            // halves its compiler happened to emit.
            var kept = lines.Where(l => l != null).ToList();
            for (int i = 0; i + 1 < kept.Count; i++)
            {
                var first = System.Text.RegularExpressions.Regex.Match(kept[i],
                    @"^(\s*)([\w]+)\s*=\s*mul\(unity_ObjectToWorld,\s*([\w]+)\)\s*;\s*$");
                if (!first.Success) continue;
                var second = System.Text.RegularExpressions.Regex.Match(kept[i + 1],
                    @"^(\s*)gl_Position\s*=\s*mul\(unity_MatrixVP,\s*" + first.Groups[2].Value + @"\)\s*;\s*$");
                if (!second.Success) continue;

                kept[i] = $"{first.Groups[1].Value}gl_Position = UnityObjectToClipPos({first.Groups[3].Value});";
                kept[i + 1] = null;
                i++;
            }

            // SPIRV-Cross flips clip-space Y when it emits HLSL, because a Vulkan
            // module addresses clip space with Y pointing down while D3D points it
            // up -- `gl_Position.y = -gl_Position.y;` right before the store.  Unity
            // is not that consumer: it renders ShaderLab through its own clip-space
            // convention, which the GLSL build of the same shader reaches without any
            // flip.  Leaving the line in turned every vertex inside out, so the whole
            // mesh fell outside the viewport and the material drew nothing.
            //
            // Only the store into gl_Position is affected; a shader that negates Y
            // for its own reasons writes some other target and is untouched.
            kept.RemoveAll(l => l != null && System.Text.RegularExpressions.Regex.IsMatch(
                l, @"^\s*gl_Position\s*\.\s*y\s*=\s*-\s*gl_Position\s*\.\s*y\s*;\s*$"));

            return string.Join("\n", kept.Where(l => l != null));
        }

        /// <summary><c>acc = vec.yyyy * matrix[1];</code> -- the seed of the product.</summary>
        private static System.Text.RegularExpressions.Match AccLine(string line)
        {
            return System.Text.RegularExpressions.Regex.Match(line.Trim(),
                @"^([\w]+)\s*=\s*([\w]+)\.yyyy\s*\*\s*([\w]+)\[1\]\s*;\s*$");
        }

        /// <summary>
        /// <c>acc2 = (matrix[row] * vec.swizzle) + acc;</c> -- a middle term, where the
        /// swizzle is the lane that row of the product reads (<c>xxxx</c> for row 0,
        /// <c>zzzz</c> for row 2).
        /// </summary>
        private static System.Text.RegularExpressions.Match AddLine(string line, string acc, string vec,
                                                                    string matrix, int row, string swizzle)
        {
            var m = System.Text.RegularExpressions.Regex.Match(line.Trim(),
                @"^([\w]+)\s*=\s*\(" + matrix + @"\[" + row + @"\]\s*\*\s*" + vec +
                @"\." + swizzle + @"\)\s*\+\s*" + acc + @"\s*;\s*$");
            return m.Success ? m : null;
        }

        /// <summary><c>acc += matrix[3];</c> -- the final row, accumulator kept.</summary>
        private static System.Text.RegularExpressions.Match PlusLine(string line, string acc, string matrix)
        {
            var m = System.Text.RegularExpressions.Regex.Match(line.Trim(),
                @"^([\w]+)\s*\+=\s*" + matrix + @"\[3\]\s*;\s*$");
            return m.Success ? m : null;
        }

        /// <summary>
        /// <c>result = (matrix[3] * vec.wwww) + acc;</c> -- the final row written
        /// straight into its destination, which is how the gl_Position store ends.
        /// </summary>
        private static System.Text.RegularExpressions.Match LastLine(string line, string acc, string vec, string matrix)
        {
            var m = System.Text.RegularExpressions.Regex.Match(line.Trim(),
                @"^([\w]+)\s*=\s*\(" + matrix + @"\[3\]\s*\*\s*" + vec +
                @"\.wwww\)\s*\+\s*" + acc + @"\s*;\s*$");
            return m.Success ? m : null;
        }

        /// <summary>The identifier in a bare <c>name();</c> statement, or null.</summary>
        private static string CalledName(string line)
        {
            string t = line.Trim();
            if (!t.EndsWith("();", StringComparison.Ordinal)) return null;
            string name = t[..^3];
            if (name.Length == 0) return null;
            foreach (char c in name)
                if (c != '_' && !char.IsLetterOrDigit(c)) return null;
            return name;
        }

        /// <summary>Index of the <c>}</c> that closes the <c>{</c> at <paramref name="open"/>.</summary>
        private static int MatchBrace(List<string> lines, int open)
        {
            int depth = 0;
            for (int i = open; i < lines.Count; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c == '{') depth++;
                    else if (c == '}' && --depth == 0) return i;
                }
            }
            return -1;
        }

        private static string Replace(string line, string from, string to)
        {
            if (line.IndexOf(from, StringComparison.Ordinal) < 0) return line;
            var sb = new StringBuilder(line.Length + 8);
            int at = 0;
            while (true)
            {
                int hit = line.IndexOf(from, at, StringComparison.Ordinal);
                if (hit < 0) { sb.Append(line, at, line.Length - at); return sb.ToString(); }
                // Only a whole word: `SPIRV_Cross_Input` must not match inside
                // `SPIRV_Cross_Input_vs` when both stages run through here.
                bool left = hit == 0 || !(char.IsLetterOrDigit(line[hit - 1]) || line[hit - 1] == '_');
                int end = hit + from.Length;
                bool right = end >= line.Length || !(char.IsLetterOrDigit(line[end]) || line[end] == '_');
                sb.Append(line, at, hit - at);
                sb.Append(left && right ? to : from);
                at = end;
            }
        }

        /// <summary>
        /// The register a declaration line binds, or -1.  Matches
        /// <c>register(t0)</c>, <c>: register(t12)</c> and the same for samplers.
        /// </summary>
        private static int RegisterOf(string line, string prefix)
        {
            int at = line.IndexOf(prefix, StringComparison.Ordinal);
            if (at < 0) return -1;
            at += prefix.Length;
            int end = line.IndexOf(')', at);
            if (end < 0) return -1;
            return int.TryParse(line.AsSpan(at, end - at), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int value) ? value : -1;
        }

        /// <summary>
        /// The identifier a declaration introduces: the token before the colon for
        /// <c>Texture2D&lt;float4&gt; _13 : register(t0);</c>, the last token otherwise.
        /// </summary>
        private static string DeclaredName(string line)
        {
            int colon = line.IndexOf(':');
            string head = (colon >= 0 ? line[..colon] : line).TrimEnd(';', ' ', '\t');

            int generic = head.LastIndexOf('>');
            if (generic >= 0) head = head[(generic + 1)..].Trim();
            else
            {
                int space = head.LastIndexOf(' ');
                if (space >= 0) head = head[(space + 1)..].Trim();
            }

            if (head.Length == 0) return null;
            foreach (char c in head)
                if (c != '_' && !char.IsLetterOrDigit(c)) return null;
            return head;
        }
    }
}

