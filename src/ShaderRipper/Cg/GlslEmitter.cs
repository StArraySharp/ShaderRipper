using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// Turns a read GLSL translation unit into Cg / HLSLPROGRAM text.
    ///
    /// Everything here is reader-independent: which attributes exist, which
    /// varyings exist, which globals the program declared, and the already
    /// translated body statements.  <see cref="GlslToCg"/> (line scanner) and
    /// <c>GlslToCgTree</c> (ANTLR) both hand it the same model, so the two
    /// readers cannot drift apart in what they emit — only in what they can
    /// read.
    /// </summary>
    internal static class GlslEmitter
    {
        public static GlslToCg.Result Emit(GlslBranch pv, GlslBranch pf, bool hlsl)
        {
            var notes = new List<string>();
            if (pv == null && pf == null)
            {
                notes.Add("no VERTEX/FRAGMENT branches found");
                return new GlslToCg.Result { Notes = notes };
            }

            var uniforms = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (decl, name) in (pv?.Uniforms ?? new List<(string, string)>())
                     .Concat(pf?.Uniforms ?? new List<(string, string)>()))
            {
                if (!seen.Add(name)) continue;
                if (GlslToCg.BuiltinUniforms.Contains(name)) continue;
                string d = decl;
                if (name.StartsWith("_") && d.StartsWith("int "))
                    d = "float " + name + ";";
                uniforms.Add(d);
            }

            var globals = new List<string>();
            var gseen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in (pv?.Globals ?? new List<string>())
                     .Concat(pf?.Globals ?? new List<string>()))
                if (gseen.Add(g)) globals.Add(g);
            foreach (var g in globals)
            {
                if (pv != null && pv.Globals.Contains(g)) pv.Locals.Insert(0, g);
                if (pf != null && pf.Globals.Contains(g)) pf.Locals.Insert(0, g);
            }

            var v2f = new Dictionary<string, GlslIo>(StringComparer.Ordinal);
            foreach (var v in pv?.Varyings ?? Enumerable.Empty<GlslIo>())
                v2f[v.Name] = v;
            foreach (var v in pf?.Varyings ?? Enumerable.Empty<GlslIo>())
                if (!v2f.ContainsKey(v.Name)) v2f[v.Name] = v;
            if (pf != null && pf.UsesFragCoord && !v2f.ContainsKey("fragcoord"))
                v2f["fragcoord"] = new GlslIo { Name = "fragcoord", CgType = "float4", Semantic = "SV_Position" };
            if (pv != null && pv.BodyText.Contains("gl_Position") && !v2f.ContainsKey("pos"))
                v2f["pos"] = new GlslIo { Name = "pos", CgType = "float4", Semantic = "SV_Position" };

            // GLSL vertex built-ins have no declared attribute, but HLSL needs them
            // in the input signature.  Without this the body keeps referencing
            // gl_VertexID, which no include defines (X3004 in Unity).
            var builtinIns = new List<GlslIo>();
            if (pv != null && pv.BodyText.Contains("gl_VertexID"))
                builtinIns.Add(new GlslIo { Name = "sv_vertexid", CgType = "uint", Semantic = "SV_VERTEXID" });
            if (pv != null && pv.BodyText.Contains("gl_InstanceID"))
                builtinIns.Add(new GlslIo { Name = "sv_instanceid", CgType = "uint", Semantic = "SV_INSTANCEID" });

            var sb = new StringBuilder();

            sb.AppendLine("struct appdata");
            sb.AppendLine("{");
            foreach (var a in pv?.Ins ?? new List<GlslIo>())
                sb.AppendLine($"\t{a.CgType} {a.Name} : {a.Semantic};");
            foreach (var a in builtinIns)
                sb.AppendLine($"\t{a.CgType} {a.Name} : {a.Semantic};");
            sb.AppendLine("};");
            sb.AppendLine();

            sb.AppendLine("struct v2f");
            sb.AppendLine("{");
            foreach (var v in v2f.Values)
                sb.AppendLine($"\t{v.CgType} {v.Name} : {v.Semantic};");
            sb.AppendLine("};");
            sb.AppendLine();

            foreach (var u in uniforms)
            {
                if (hlsl)
                {
                    var mu = Regex.Match(u, @"^(sampler1D|sampler2D|sampler3D|samplerCUBE|sampler2DArray)\s+(\w+)\s*;");
                    if (mu.Success)
                    {
                        foreach (var line in CgSyntax.DeclareTexture(mu.Groups[1].Value, mu.Groups[2].Value, true, "\t"))
                            sb.AppendLine(line);
                        continue;
                    }
                }
                sb.AppendLine(u);
            }

            if (pv != null && pv.Body.Count > 0)
            {
                sb.AppendLine("v2f vert (appdata v)");
                sb.AppendLine("{");
                foreach (var g in pv.Locals) sb.AppendLine("\t" + g + ";");
                sb.AppendLine("\tv2f o;");
                foreach (var l in GlslToCg.CollapseMvp(string.Join("\n", pv.Body)).Split('\n'))
                {
                    if (l.Length == 0) continue;
                    sb.AppendLine("\t" + GlslToCg.CgBuiltins(l));
                }
                if (!pv.Body[^1].Trim().StartsWith("return", StringComparison.Ordinal))
                    sb.AppendLine("\treturn o;");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            if (pf != null && pf.Body.Count > 0)
            {
                string ret = pf.OutputType ?? "float4";
                string sem = pf.OutputSemantic ?? "SV_Target";
                sb.AppendLine($"{ret} frag (v2f i) : {sem}");
                sb.AppendLine("{");
                foreach (var g in pf.Locals) sb.AppendLine("\t" + g + ";");
                sb.AppendLine($"\t{ret} fragOut = ({ret})0;");
                foreach (var l in pf.Body) sb.AppendLine("\t" + l);
                if (!pf.Body[^1].Trim().StartsWith("return", StringComparison.Ordinal))
                    sb.AppendLine("\treturn fragOut;");
                sb.AppendLine("}");
            }

            return new GlslToCg.Result { Cg = sb.ToString(), Notes = notes };
        }
    }
}
