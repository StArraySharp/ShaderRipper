// ShaderRipper -- read a texture transform back as the macro it came from.
//
// GLSL has no preprocessor worth using for this: Unity's front end expands
// TRANSFORM_TEX(uv, tex) before the shader ever reaches the driver, and HLSLcc
// keeps the expansion, so the payload carries
//
//     u_xlat0.xy = texcoord0.xy * _MainTex_ST.xy + _MainTex_ST.zw;
//
// where the shader's author wrote
//
//     o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
//
// The macro is `tex.xy * name##_ST.xy + name##_ST.zw`, so the statement IS the
// macro's expansion, character for character.  Reading it back is a re-spelling,
// not a rewrite -- and the two `_ST` references being the same name is what proves
// the operands are the two halves of one scale/offset row rather than two values
// that merely happen to line up.
//
// The multiplier keeps its `.xy` implicit: the macro appends it, so `texcoord0.xy`
// goes in as `texcoord0`.  A multiplier read through any other swizzle (`v0.yw`)
// would expand to `v0.yw.xy`, which is not HLSL, so those stay as they are.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ShaderRipper.Emit;

internal static class TransformTexChain
{
    /// <summary>`u_xlat0.xy = texcoord0.xy * _MainTex_ST.xy + _MainTex_ST.zw;`</summary>
    private static readonly Regex StTransform = new(
        @"^(?<lhs>[A-Za-z_]\w*(?:\.[xyzw]{1,4})?) = " +
        @"(?<uv>[A-Za-z_]\w*)(?:\.xy)? \* " +
        @"(?<tex>[A-Za-z_]\w*)_ST\.xy \+ (?<tex2>[A-Za-z_]\w*)_ST\.zw;$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The folded statements, or null when nothing in the list matched.</summary>
    public static List<string> Fold(List<string> lines)
    {
        List<string> outp = null;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ') indent++;

            var m = StTransform.Match(line.Trim());
            // Both halves have to name the same member: `_A_ST.xy + _B_ST.zw` is not
            // a texture transform, it is two unrelated uniforms.
            if (!m.Success || m.Groups["tex"].Value != m.Groups["tex2"].Value)
            {
                outp?.Add(line);
                continue;
            }

            outp ??= Copy(lines, i);
            outp.Add($"{line[..indent]}{m.Groups["lhs"].Value} = TRANSFORM_TEX(" +
                     $"{m.Groups["uv"].Value}, {m.Groups["tex"].Value});");
        }
        return outp;
    }

    private static List<string> Copy(List<string> lines, int count)
    {
        var copy = new List<string>(lines.Count);
        for (int i = 0; i < count; i++) copy.Add(lines[i]);
        return copy;
    }
}
