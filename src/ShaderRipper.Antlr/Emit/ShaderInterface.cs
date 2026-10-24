using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ShaderRipper.Antlr.Ast;

namespace ShaderRipper.Antlr.Emit
{
    /// <summary>
    /// Semantic-aware struct builder.  Turns the DXBC signatures into the
    /// appdata / v2f pair that standard ShaderLab expects, and gives every
    /// register a field name so the translated body can be wired to it.
    /// </summary>
    internal sealed class ShaderInterface
    {
        public sealed class Field
        {
            public string Name;
            public string Type;
            public string Semantic;
            public int Index;

            /// <summary>Index the compiled signature used, kept for the comment.</summary>
            public int OrigIndex = -1;

            /// <summary>Base semantic name without its numeric suffix.</summary>
            public string Base;

            /// <summary>
            /// Component lanes this field occupies inside its register (`xy`, `zw`,
            /// ...).  Several semantics can share one register, so the mask is what
            /// says which lanes belong to this field.
            /// </summary>
            public string Mask;

            /// <summary>Component count implied by the declared HLSL type.</summary>
            public int Comps => Type switch
            {
                "float" => 1, "float2" => 2, "float3" => 3, _ => 4,
            };
        }

        public readonly List<Field> AppdataFields = new();
        public readonly List<Field> V2fFields = new();

        public readonly Dictionary<int, List<Field>> VsInReg = new();
        public readonly Dictionary<int, List<Field>> VsOutReg = new();
        public readonly Dictionary<int, List<Field>> PsInReg = new();

        private readonly HashSet<string> _appUsed = new(StringComparer.Ordinal);
        private readonly HashSet<string> _v2fUsed = new(StringComparer.Ordinal);

        public void BuildAppdata(List<SigEntry> ins)
        {
            AppdataFields.Clear();
            VsInReg.Clear();
            if (ins == null) return;
            foreach (var e in ins)
            {
                string nm = FieldName(e, _appUsed, false);
                var f = new Field
                {
                    Name = nm,
                    Type = e.HlslType,
                    Semantic = SemanticFor(e),
                    Index = e.Index,
                    Mask = e.Mask,
                };
                AppdataFields.Add(f);
                AddTo(VsInReg, e.Register, f);
            }
        }

        /// <summary>
        /// Build the interpolator struct.  The vertex output signature defines what
        /// gets written; the fragment input signature defines what gets read.  They
        /// are matched on (semantic, index), and any fragment-only input is appended
        /// so the struct stays a superset of both.
        /// </summary>
        public void BuildV2f(List<SigEntry> vsOut, List<SigEntry> psIn)
        {
            V2fFields.Clear();
            VsOutReg.Clear();
            PsInReg.Clear();
            _v2fUsed.Clear();

            var byKey = new Dictionary<string, Field>(StringComparer.Ordinal);
            var order = new List<Field>();

            if (vsOut != null)
                foreach (var e in vsOut)
                {
                    string key = e.Name + e.Index;
                    string nm = FieldName(e, _v2fUsed, true);
                    var f = new Field
                    {
                        Name = nm,
                        Type = e.IsSystem || IsSystemName(e.Name) ? "float4" : e.HlslType,
                        Semantic = SemanticFor(e),
                        Index = e.Index,
                        Mask = e.Mask,
                    };
                    byKey[key] = f;
                    order.Add(f);
                    AddTo(VsOutReg, e.Register, f);
                }

            if (psIn != null)
                foreach (var e in psIn)
                {
                    string key = e.Name + e.Index;
                    if (!byKey.TryGetValue(key, out var f))
                    {
                        string nm = FieldName(e, _v2fUsed, true);
                        f = new Field
                        {
                            Name = nm,
                            Type = e.HlslType,
                            Semantic = SemanticFor(e),
                            Index = e.Index,
                            Mask = e.Mask,
                        };
                        byKey[key] = f;
                        order.Add(f);
                    }
                    else if (!string.IsNullOrEmpty(e.Mask) && !string.IsNullOrEmpty(f.Mask))
                    {
                        f.Mask = string.Concat("xyzw".Where(c => f.Mask.Contains(c) || e.Mask.Contains(c)));
                    }
                    AddTo(PsInReg, e.Register, f);
                }

            V2fFields.AddRange(order.OrderBy(f => f.Semantic == "SV_POSITION" ? 0 : 1).ThenBy(f => f.Index));
        }

        private static bool IsSystemName(string n) =>
            n != null && n.StartsWith("SV_", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Renumber the interpolator semantics contiguously from zero.
        ///
        /// DXBC can carry sparse indices (a vertex shader here outputs TEXCOORD8),
        /// and some compilers cap the index.  The index is only a binding label --
        /// the vertex output and the fragment input are the same struct here, so
        /// fields are matched by name and the numbering is free to be
        /// conventional.  The compiled index stays visible in the field comment.
        /// </summary>
        private static void Renumber(List<Field> fields)
        {
            var next = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in fields)
            {
                string b = BaseSemantic(f.Semantic);
                if (!NumberedSemantics.Contains(b)) continue;
                f.Base = b;
                f.OrigIndex = f.Index;
                if (!next.TryGetValue(b, out int n)) n = 0;
                next[b] = n + 1;
                f.Index = n;
                f.Semantic = b + n;
            }
        }

        /// <summary>Semantic name without a trailing numeric suffix ("TEXCOORD1" -> "TEXCOORD").</summary>
        private static string BaseSemantic(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            int i = n.Length;
            while (i > 0 && char.IsDigit(n[i - 1])) i--;
            return i > 0 ? n.Substring(0, i) : n;
        }

        private static readonly HashSet<string> NumberedSemantics = new(StringComparer.OrdinalIgnoreCase)
        { "TEXCOORD", "COLOR", "BLENDWEIGHT", "BLENDINDICES" };

        /// <summary>
        /// 必须以整型声明的系统值语义（比较前去掉下划线、转大写，兼容
        /// SV_VERTEXID / SV_VertexID 两种写法）。
        /// </summary>
        private static readonly HashSet<string> IntegralSemantics = new(StringComparer.Ordinal)
        { "SVVERTEXID", "SVINSTANCEID", "SVPRIMITIVEID", "SVVIEWID" };

        private static string NormSemantic(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("_", "").ToUpperInvariant();

        /// <summary>
        /// The complete semantic annotation, index included.  The disassembler can
        /// print the index either in the name column ("TEXCOORD1") or separately,
        /// so the base name is normalised before the index is appended --
        /// otherwise "TEXCOORD1" + index 1 produced "TEXCOORD11".
        /// </summary>
        private static string SemanticFor(SigEntry e)
        {
            if (e.IsSystem)
                return e.Name.Equals("SV_Position", StringComparison.OrdinalIgnoreCase)
                    ? "SV_POSITION" : e.Name.ToUpperInvariant();
            string b = BaseSemantic(e.Name);
            return NumberedSemantics.Contains(b) ? b + e.Index : b;
        }

        /// <summary>Pick a conventional ShaderLab field name for a signature entry.</summary>
        private static string FieldName(SigEntry e, HashSet<string> used, bool v2f)
        {
            string baseName = BaseSemantic(e.Name).ToUpperInvariant() switch
            {
                "POSITION" => v2f ? "pos" : "vertex",
                "SV_POSITION" => "pos",
                "NORMAL" => "normal",
                "TANGENT" => "tangent",
                "COLOR" => v2f ? $"color{e.Index}" : "color",
                "TEXCOORD" => $"uv{e.Index}",
                "BLENDWEIGHT" => "blendWeight",
                "BLENDINDICES" => "blendIndices",
                "PSIZE" => "psize",
                "SV_TARGET" => "target",
                "SV_ISFRONTFACE" => "isFrontFace",
                "SV_DEPTH" => "depth",
                "VPOS" => "screenPos",
                _ => Sanitize(e.Name) + (e.Index > 0 ? e.Index.ToString() : ""),
            };
            string n = baseName;
            int k = 1;
            while (!used.Add(n)) n = baseName + k++;
            return n;
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? char.ToLowerInvariant(c) : '_');
            string r = sb.ToString();
            if (r.Length == 0 || char.IsDigit(r[0])) r = "f_" + r;
            return r;
        }

        /// <param name="renumber">
        /// Only the interpolator struct may be renumbered.  An appdata semantic index
        /// selects the mesh's UV/color channel, so it has to be preserved verbatim.
        /// </param>
        public static string StructText(string structName, List<Field> fields, bool renumber = false)
        {
            if (renumber) Renumber(fields);
            var sb = new StringBuilder();
            sb.AppendLine($"struct {structName}");
            sb.AppendLine("{");
            foreach (var f in fields)
            {
                string note = f.OrigIndex >= 0 && f.OrigIndex != f.Index
                    ? $"  // compiled as {f.Base}{f.OrigIndex}" : "";
                string type = IntegralSemantics.Contains(NormSemantic(f.Semantic))
                    ? "uint" : f.Type;
                sb.AppendLine($"\t{type,-7} {f.Name,-12} : {f.Semantic};{note}");
            }
            sb.AppendLine("};");
            return sb.ToString();
        }

        private static bool NeedsIndex(Field f) => false;

        /// <summary>
        /// Declares vN locals initialised from the input struct.  The local keeps
        /// the field's own type: Cg rejects initialising a float4 from a float2,
        /// and the declaration mask is exactly what the semantic supplies.  When
        /// the translated body nevertheless reads beyond that mask the local is
        /// widened and the missing lanes are zero-filled (the DXBC lanes past the
        /// mask are undefined, and zero is what the hardware supplies).
        /// </summary>
        /// <summary>First lane a field occupies inside its register.</summary>
        private static int LaneStart(Field f)
        {
            if (string.IsNullOrEmpty(f.Mask)) return 0;
            int i = "xyzw".IndexOf(f.Mask[0]);
            return i < 0 ? 0 : i;
        }

        /// <summary>Lanes a field occupies inside its register.</summary>
        private static int LaneCount(Field f) =>
            string.IsNullOrEmpty(f.Mask) ? Math.Max(1, f.Comps) : f.Mask.Length;

        private static void AddTo(Dictionary<int, List<Field>> map, int reg, Field f)
        {
            if (!map.TryGetValue(reg, out var list)) map[reg] = list = new List<Field>();
            if (!list.Contains(f)) list.Add(f);
        }

        /// <summary>
        /// Declares the vN locals the translated body expects.  A register can hold
        /// several semantics in disjoint lanes, so the local is assembled lane by
        /// lane from whichever field covers that lane -- zero past the last one, which
        /// is what the hardware supplies for lanes outside the declaration mask.
        /// </summary>
        public IEnumerable<string> BindInputs(Dictionary<int, List<Field>> map, string structVar,
                                              Func<int, int> neededComps = null)
        {
            foreach (var kv in new SortedDictionary<int, List<Field>>(map))
            {
                var fields = kv.Value;
                int need = 0;
                foreach (var f in fields) need = Math.Max(need, LaneStart(f) + LaneCount(f));
                need = Math.Max(need, neededComps?.Invoke(kv.Key) ?? 0);
                if (need <= 0) need = 1;

                if (fields.Count == 1 && LaneStart(fields[0]) == 0 && LaneCount(fields[0]) >= need)
                {
                    yield return $"\t{fields[0].Type,-6} v{kv.Key} = {structVar}.{fields[0].Name};";
                    continue;
                }

                var lanes = new List<string>(need);
                for (int i = 0; i < need; i++)
                {
                    string src = "0.0";
                    foreach (var f in fields)
                    {
                        int start = LaneStart(f);
                        int len = LaneCount(f);
                        if (i < start || i >= start + len) continue;
                        src = f.Comps == 1 && len == 1
                            ? $"{structVar}.{f.Name}"
                            : $"{structVar}.{f.Name}.{"xyzw"[i - start]}";
                        break;
                    }
                    lanes.Add(src);
                }
                string ty = need <= 1 ? "float" : "float" + need;
                yield return $"\t{ty,-6} v{kv.Key} = {ty}({string.Join(", ", lanes)});";
            }
        }

        /// <summary>Copies the oN locals back into the output struct, per lane group.</summary>
        public IEnumerable<string> BindOutputs(Dictionary<int, List<Field>> map, string structVar)
        {
            foreach (var kv in new SortedDictionary<int, List<Field>>(map))
                foreach (var f in kv.Value)
                {
                    if (string.IsNullOrEmpty(f.Mask) || (LaneStart(f) == 0 && f.Mask.Length >= 4))
                        yield return $"\t{structVar}.{f.Name} = o{kv.Key};";
                    else
                        yield return $"\t{structVar}.{f.Name} = o{kv.Key}.{f.Mask};";
                }
        }
    }
}
