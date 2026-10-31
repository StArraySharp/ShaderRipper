using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderRipper.Ast;
using ShaderRipper.Data;
using ShaderRipper.Emit;

namespace ShaderRipper.Game
{
    /// <summary>
    /// ComputeShader -&gt; <c>.compute</c>.
    ///
    /// A compute shader is not a ShaderLab document: there is no Properties block,
    /// no render state and no pass, because the engine dispatches a kernel by name
    /// rather than drawing geometry with it.  The output form is therefore the
    /// <c>.compute</c> file Unity's compute pipeline expects -- the keyword pragmas,
    /// then one <c>[numthreads(x, y, z)]</c> entry point per kernel.
    ///
    /// The translation itself is the same DXBC route a pass takes; only the wrapper
    /// differs.  A kernel is <c>cs_5_0</c>, which the existing emitter already reads,
    /// so this class is bookkeeping: which kernel, which keywords, which platform.
    /// </summary>
    internal static class ComputeExport
    {
        /// <summary>
        /// Write one compute shader.  Returns the number of kernels emitted; 0 means
        /// nothing was written (no variant this build can read).
        /// </summary>
        /// <param name="origAsm">
        /// Also write each kernel's disassembly beside the <c>.compute</c>, the same
        /// way a pass does with <c>--orig-asm</c>.
        /// </param>
        public static int Export(ComputeShaderAsset asset, string name, string rel, string outDir,
                                 bool shaderLab, bool origAsm, GameExport.Stats s)
        {
            // A compute shader is written as `.compute`, and it is the only place the
            // extension is decided: the claiming pass derived a `.shader` path, which
            // is what keeps compute and shader names from colliding.
            rel = rel.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                ? rel[..^".shader".Length] + ".compute"
                : rel + ".compute";
            // One platform per file, chosen the way the Shader route chooses but
            // narrower: a compute kernel's program is DXBC or nothing, so a variant
            // whose backend produces a payload this build cannot translate is only a
            // fallback.  Which one actually works is answered by trying them: a
            // ComputeShader's `targetRenderer` does not use the Shader's platform
            // numbering, so picking by id alone exported the wrong slot.
            // A ComputeShader's `targetRenderer` does not use the Shader's platform
            // numbering, so an id cannot be trusted to say what the payload is.  The
            // variants are therefore tried in asset order and the first one whose
            // programs turn out to be compute DXBC is the one written.
            foreach (var candidate in asset.Variants)
            {
                var attempt = TryExport(asset, name, rel, outDir, shaderLab, origAsm, s, candidate);
                if (attempt != 0) return attempt;
            }

            s.Skipped++;
            s.Notes.Add($"{name}: no compute variant this build can translate");
            return 0;
        }

        /// <summary>
        /// Translate one platform's variants.  Returns the number of kernels written,
        /// or 0 when this backend's payloads did not turn out to be compute programs
        /// -- which is how the caller learns to try the next backend.
        /// </summary>
        private static int TryExport(ComputeShaderAsset asset, string name, string rel, string outDir,
                                     bool shaderLab, bool origAsm, GameExport.Stats s,
                                     ComputeVariant chosen)
        {
            // A kernel with keyword variants appears once per variant in the asset's
            // kernel array, so the same name can occur a dozen times.  A `.compute`
            // declares each kernel once, so the first program per name is the one
            // that becomes its entry point.
            var byName = new List<ComputeKernel>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in chosen.Kernels)
                if (seen.Add(k.Name)) byName.Add(k);

            var kernels = new List<ComputeKernelText>();
            foreach (var kernel in byName)
            {
                var text = TranslateKernel(name, kernel, s);
                if (text != null) kernels.Add(text);
            }
            if (kernels.Count == 0) return 0;

            string full = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? outDir);

            if (origAsm)
            {
                string dir = Path.Combine(Path.GetDirectoryName(full) ?? outDir,
                                          Path.GetFileNameWithoutExtension(full));
                Directory.CreateDirectory(dir);
                foreach (var k in kernels)
                    if (k.Asm != null)
                        File.WriteAllText(Path.Combine(dir, k.Name + ".orig.asm"), k.Asm);
            }

            if (shaderLab)      // the same switch the .shader route uses: write the deliverable
            {
                File.WriteAllText(full, Wrap(name, kernels, asset, chosen));
                s.Emitted++;
                Log.Info(name, rel, Plugins.PlatformCatalog.Get(chosen.Platform).Name);
            }
            return kernels.Count;
        }

        private sealed class ComputeKernelText
        {
            public string Name = "";
            public int[] ThreadGroup = Array.Empty<int>();
            public string Hlsl = "";
            public string Asm;
            /// <summary>How many keyword variants the build made of this kernel.</summary>
            public int VariantCount = 1;
        }

        private static ComputeKernelText TranslateKernel(string shaderName, ComputeKernel kernel, GameExport.Stats s)
        {
            // A kernel is a single entry point in one `.compute` file, so the base
            // program is what gets translated.  Further programs are the same kernel
            // recompiled under keywords, and reproducing those needs the guards the
            // asset no longer stores -- the base body plus the keyword pragmas is
            // what the build's data actually supports.
            //
            // The platform is not handed in: a ComputeShader's variant table is
            // keyed by a renderer id this build does not have a table for, so which
            // variant holds the DXBC is answered by the first kernel that decodes.
            // Every failure here is silent; the caller counts what came back.
            foreach (var program in kernel.Programs)
            {
                string asm = BlobTool.Disassemble(program);
                if (asm == null) continue;

                if (Environment.GetEnvironmentVariable("SHADERRIP_CDBG") == "1")
                {
                    string dbg = Path.Combine(Path.GetTempPath(),
                                              $"shaderripper_{SafeName(kernel.Name)}.orig.asm");
                    File.WriteAllText(dbg, asm);
                    Log.Note($"compute disassembly -> {dbg}");
                }

                try
                {
                    var module = Module.Read(asm);
                    // Not a compute container: a kernel's code is cs_*, and treating
                    // anything else as one would emit a fragment body under a numthreads
                    // attribute.
                    if (module.Code.Count == 0 || module.Stage is not "cs") continue;

                    var r = HlslEmitter.Emit(module, standalone: true);
                    s.Programs++;
                    s.Disassembled++;
                    s.Translated++;
                    return new ComputeKernelText
                    {
                        Name = kernel.Name,
                        // The thread-group size comes from `dcl_thread_group`, which is
                        // what the program actually declares.  The asset's own
                        // `threadGroupSize` field read as 1,1,1 on a kernel whose
                        // disassembly says 64,1,1 -- the compiled value is authoritative.
                        ThreadGroup = module.ThreadGroup.Length == 3 ? module.ThreadGroup
                                                                     : kernel.ThreadGroupSize,
                        Hlsl = r.Hlsl,
                        Asm = asm,
                        VariantCount = kernel.Programs.Count,
                    };
                }
                catch (Exception e) when (e is IndexOutOfRangeException ||
                                          e is ArgumentOutOfRangeException ||
                                          e is NullReferenceException ||
                                          e is InvalidOperationException)
                {
                    // One kernel the translator cannot read must not take the whole
                    // compute shader down: the other kernels of the same asset are
                    // independent programs, and the note says which one was lost.
                    s.Skipped++;
                    s.Notes.Add($"{shaderName}/{kernel.Name}: {e.Message}");
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// Assemble the <c>.compute</c> document: pragmas, then every kernel.
        ///
        /// A <c>.compute</c> file has no ShaderLab around it, so the per-kernel bodies
        /// are concatenated with their own <c>[numthreads]</c> attribute and the pragma
        /// block up top.
        ///
        /// The declarations are the UNION of every kernel's, not one kernel's: each
        /// compiled program carries only the resources IT reads, so a shader whose
        /// `main` uses t0-t2 and whose `batchMain` uses t4 and t9 would otherwise
        /// declare half its buffers and fail to compile on the other half.  Duplicates
        /// are dropped by name, so a resource every kernel shares appears once.
        /// </summary>
        private static string Wrap(string name, List<ComputeKernelText> kernels,
                                   ComputeShaderAsset asset, ComputeVariant variant)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("// Reconstructed compute shader by ShaderRipper\n");
            sb.Append("// backend: ").Append(Plugins.PlatformCatalog.Get(variant.Platform).Name)
              .Append(" / ").Append(variant.Platform).Append('\n');

            if (asset.LocalKeywords.Count > 0)
                sb.Append("#pragma multi_compile_local __ ")
                  .Append(string.Join(" ", asset.LocalKeywords)).Append('\n');
            if (asset.GlobalKeywords.Count > 0)
                sb.Append("#pragma multi_compile __ ")
                  .Append(string.Join(" ", asset.GlobalKeywords)).Append('\n');
            // Unity's compute pipeline dispatches by kernel name, and the name only
            // exists for it if a `#pragma kernel` line declares it.  One line per
            // kernel, before any body.
            foreach (var k in kernels)
                sb.Append("#pragma kernel ").Append(SafeName(k.Name)).Append('\n');
            sb.Append('\n');

            // Split every body first: the shared declarations are the union of the
            // non-function items, and the functions are what each kernel contributes.
            var perKernel = new List<(ComputeKernelText kernel, List<HlslTree.Item> decls,
                                      List<HlslTree.Item> fns)>();
            foreach (var k in kernels)
            {
                var items = HlslTree.Split(k.Hlsl, out _);
                if (items.Count == 0 || items.Any(i => i.Kind == "raw"))
                {
                    // A body the grammar could not split is emitted whole; it is
                    // still better than dropping the kernel.
                    perKernel.Add((k, new List<HlslTree.Item>(), new List<HlslTree.Item>
                    {
                        new() { Kind = "raw", Text = k.Hlsl },
                    }));
                    continue;
                }
                perKernel.Add((k,
                    items.Where(i => i.Kind != "func" && i.Kind != "semi" && !IsBanner(i.Text))
                         .Select(CloneItem).ToList(),
                    items.Where(i => i.Kind == "func").ToList()));            }

            // Declarations, deduplicated: a cbuffer, struct or resource two kernels
            // both read is declared once.  Two cases need more than "keep the first":
            //
            //   * a cbuffer's extent is declared per program, and one kernel's
            //     `CB0[1]` says nothing about another's `CB0[4]`, so the WIDEST wins
            //     (`cb0[3]` past the end of a one-row buffer is X3504);
            //   * the same register bound to a DIFFERENT type -- `t0` as a Texture2D
            //     in one kernel and a Texture2DArray in another.  One HLSL file cannot
            //     declare `t0` twice, so the later kernel's copy is renamed and its
            //     body rewritten to match.  Both are real declarations of the same
            //     register; only the name has to differ.
            var order = new List<HlslTree.Item>();
            var at = new Dictionary<string, int>(StringComparer.Ordinal);
            var renames = new List<(string from, string to)>[perKernel.Count];
            for (int i = 0; i < perKernel.Count; i++)
            {
                var (_, decls, _) = perKernel[i];
                foreach (var it in decls)
                {
                    // A plain declaration's identifier is not carried by the parse
                    // tree (only a cbuffer/struct/function name is), so it is read
                    // from the text: everything before the `:` or `=` or `;`.
                    if (it.Name.Length == 0 && it.Kind == "decl") it.Name = DeclName(it.Text);
                    string key = it.Name.Length > 0 ? it.Name : it.Text;
                    if (!at.TryGetValue(key, out int idx))
                    {
                        at[key] = order.Count;
                        order.Add(it);
                        continue;
                    }
                    var kept = order[idx];
                    if (it.Kind == "cbuffer" && CbRows(it.Text) > CbRows(kept.Text))
                    {
                        order[idx] = it;
                        continue;
                    }
                    if (it.Text == kept.Text) continue;
                    // A texture and its sampler are not the same declaration: a
                    // sampler for a texture the FIRST kernel declared still has to be
                    // emitted even though its name is already taken.  Only the
                    // texture types conflict.
                    if (it.Name.StartsWith("sampler_", StringComparison.Ordinal) &&
                        kept.Name == it.Name)
                    {
                        order.Add(it);
                        continue;
                    }

                    // Same name, different declaration: rename this kernel's copy.
                    // `register(t0)` names the binding, not the variable, so it stays
                    // as it was -- the copy still occupies the same register.
                    string renamed = key + "_" + (i + 1);
                    it.Text = ReplaceWord(it.Text, key, renamed)
                        .Replace("register(" + renamed + ")", "register(" + key + ")");
                    it.Name = renamed;
                    (renames[i] ??= new()).Add((key, renamed));
                    order.Add(it);
                    // The sampler that belongs to a renamed texture follows it, or the
                    // pair stops naming the same binding.
                    foreach (var sampler in decls.Where(d => d.Name == "sampler_" + key).ToList())
                    {
                        string newName = "sampler_" + renamed;
                        sampler.Text = ReplaceWord(sampler.Text, "sampler_" + key, newName);
                        sampler.Name = newName;
                        (renames[i] ??= new()).Add(("sampler_" + key, newName));
                        order.Add(sampler);
                    }
                }
            }
            foreach (var it in order) sb.Append(it.Text).Append('\n');
            if (order.Count > 0) sb.Append('\n');

            for (int i = 0; i < perKernel.Count; i++)
            {
                var (kernel, _, fns) = perKernel[i];
                foreach (var fn in fns)
                {
                    string body = fn.Text;
                    if (body.Contains("void CSMain(", StringComparison.Ordinal))
                        body = body.Replace("void CSMain(", "void " + SafeName(kernel.Name) + "(",
                                            StringComparison.Ordinal);
                    if (renames[i] != null)
                        foreach (var (from, to) in renames[i]) body = RenameBody(body, from, to);
                    sb.Append(Threads(kernel)).Append('\n').Append(body).Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Rename a resource in a kernel body, leaving a <c>register(...)</c> alone.
        ///
        /// The binding is the one thing that must NOT follow the identifier: the
        /// renamed copy still occupies the same register, which is the whole reason
        /// it could not keep the name.
        /// </summary>
        private static string RenameBody(string body, string from, string to)
        {
            var sb = new System.Text.StringBuilder(body.Length);
            int i = 0;
            while (i < body.Length)
            {
                int at = body.IndexOf("register(", i, StringComparison.Ordinal);
                int stop = at < 0 ? body.Length : at;
                sb.Append(ReplaceWord(body[i..stop], from, to));
                if (at < 0) break;

                int close = body.IndexOf(')', at);
                if (close < 0) close = body.Length - 1;
                sb.Append(body[at..(close + 1)]);
                i = close + 1;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Replace an identifier wherever it appears as a whole word, so `t0` is
        /// renamed without touching `t01`, `_t0` or a comment's prose.
        /// </summary>
        private static string ReplaceWord(string text, string from, string to) =>
            System.Text.RegularExpressions.Regex.Replace(
                text, @"(?<![A-Za-z0-9_])" + System.Text.RegularExpressions.Regex.Escape(from) + @"(?![A-Za-z0-9_])",
                to);

        /// <summary>
        /// The identifier a simple declaration introduces: the last word before the
        /// register binding, the initializer or the semicolon.
        ///
        /// <c>Texture2D t0 : register(t0);</c> -&gt; <c>t0</c>, <c>static float4 _x0[4];</c>
        /// -&gt; <c>_x0</c>, <c>RWByteAddressBuffer u0;</c> -&gt; <c>u0</c>.
        /// </summary>
        private static string DeclName(string text)
        {
            string s = text;
            int colon = s.IndexOf(':');
            if (colon >= 0) s = s[..colon];
            int semi = s.IndexOf(';');
            if (semi >= 0) s = s[..semi];
            int assign = s.IndexOf('=');
            if (assign >= 0) s = s[..assign];

            var m = System.Text.RegularExpressions.Regex.Matches(s.Trim(),
                @"[A-Za-z_][A-Za-z0-9_]*");
            if (m.Count == 0) return "";
            // `uint2 gridsize` and `static float4 _x0` both end with the name; a
            // declaration with no type at all (`t0;`) has it as the only word.
            return m[m.Count - 1].Value;
        }

        /// <summary>
        /// A mutable copy of a declaration item: the shared-declaration pass rewrites
        /// the text of a renamed resource, and the item still belongs to its kernel.
        /// </summary>
        private static HlslTree.Item CloneItem(HlslTree.Item it) => new()
        {
            Kind = it.Kind,
            Text = it.Text,
            Name = it.Name,
            Register = it.Register,
        };

        /// <summary>The row count a cbuffer declaration spells (<c>float4 cb0[N];</c>).</summary>
        private static int CbRows(string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, @"\[\s*(\d+)\s*\]");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        /// <summary>The translator's own banner, which the file already states once.</summary>
        private static bool IsBanner(string text) =>
            text.StartsWith("// translated from a DXBC", StringComparison.Ordinal) ||
            text.StartsWith("// grammar -> AST", StringComparison.Ordinal) ||
            text.StartsWith("// profile:", StringComparison.Ordinal);

        /// <summary>The <c>[numthreads(x, y, z)]</c> attribute a kernel declared.</summary>
        private static string Threads(ComputeKernelText k) =>
            "[numthreads(" +
            string.Join(", ", k.ThreadGroup.Length == 3 ? k.ThreadGroup : new[] { 1, 1, 1 }) +
            ")]";

        /// <summary>An HLSL identifier derived from a kernel name.</summary>
        private static string SafeName(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length + 1);
            for (int i = 0; i < (name ?? "").Length; i++)
            {
                char c = name[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }
            string s = sb.ToString();
            if (s.Length == 0) s = "Kernel";
            if (char.IsDigit(s[0])) s = "K" + s;
            return s;
        }
    }
}
