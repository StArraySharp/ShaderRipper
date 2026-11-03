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
                                     string blobTag, out string error)
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
                if (vertex) ApplyVertexAttributes(api, compiler, spirv);

                byte* source;
                if (api.CompilerCompile(compiler, &source) != Result.Success)
                {
                    error = LastError(api, ctx);
                    return null;
                }

                string text = new string((sbyte*)source);
                text = Rename(text, names, vertex, spirv);
                text = Flatten(text, vertex);

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
        /// Location 0 is <c>POSITION</c> because every vertex program Unity compiles
        /// has one there.  The rest are <c>TEXCOORD(n-1)</c> rather than
        /// <c>NORMAL</c>/<c>TANGENT</c>: the engine's <c>VertexChannel</c> order
        /// suggests that mapping, but the GLSL build of the same shaders shows the
        /// declaration order is per-shader, so a positional table silently binds the
        /// wrong mesh stream.  A numbered channel is what the payload actually says.
        /// </summary>
        private static void ApplyVertexAttributes(Cross api, Compiler* compiler, byte[] spirv)
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
                    string semantic = location == 0 ? "POSITION" : "TEXCOORD" + (location - 1);
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

            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                int textureRegister = RegisterOf(trimmed, "register(t");
                if (textureRegister >= 0 && names.Textures.TryGetValue(textureRegister, out string texture))
                {
                    string declared = DeclaredName(trimmed);
                    if (declared != null) map[declared] = texture;
                    continue;
                }

                // A sampler SPIRV-Cross synthesised next to a texture is named
                // `__<id>_sampler`; the texture's own name decides the sampler's.
                int samplerRegister = RegisterOf(trimmed, "register(s");
                if (samplerRegister >= 0)
                {
                    string declared = DeclaredName(trimmed);
                    if (declared != null && names.Textures.TryGetValue(samplerRegister, out string owned))
                        map[declared] = "sampler" + owned;
                }
            }

            foreach (var kv in CBufferNames(spirv, names)) map[kv.Key] = kv.Value;

            var sb = new StringBuilder(text.Length);
            string tag = vertex ? "vs_" : "ps_";
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != '_' && !char.IsLetter(c)) { sb.Append(c); i++; continue; }

                int start = i;
                while (i < text.Length && (text[i] == '_' || char.IsLetterOrDigit(text[i]))) i++;
                string word = text.Substring(start, i - start);

                if (map.TryGetValue(word, out string replacement)) sb.Append(replacement);
                else if (Generated(word)) sb.Append(tag).Append(word, 1, word.Length - 1);
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
        /// </summary>
        private static bool Generated(string word)
        {
            if (word.Length < 2 || word[0] != '_' || !char.IsDigit(word[1])) return false;
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

            var result = new List<string>(lines.Count + 4);
            result.AddRange(lines.GetRange(0, head));
            result.Add($"SPIRV_Cross_Output_{tag} {entry}(SPIRV_Cross_Input_{tag} stage_input)");
            result.Add("{");
            result.AddRange(lines.GetRange(open + 1, call - open - 1));
            result.AddRange(lines.GetRange(bodyOpen + 1, bodyClose - bodyOpen - 1));
            result.AddRange(lines.GetRange(call + 1, close - call - 1));
            result.Add("}");
            result.AddRange(lines.GetRange(close + 1, lines.Count - close - 1));
            return string.Join("\n", result);
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

