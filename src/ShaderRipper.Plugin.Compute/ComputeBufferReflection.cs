using System.Text.RegularExpressions;
using ShaderRipper.Data;
using ShaderRipper.Emit;

namespace ShaderRipper.Plugin.Compute;

internal static class ComputeBufferReflection
{
    private const string Components = "xyzw";
    private const int MaxBufferSize = 65536;
    private static readonly Regex Identifier = new(@"\A[A-Za-z_][A-Za-z0-9_]*\z");

    private readonly record struct Lane(string Expression, string ScalarType, int Width, int Component);

    public static (string Hlsl, bool Restored) Apply(string hlsl,
                                                     IReadOnlyDictionary<int, AssetConstantBuffer> buffers)
    {
        bool restored = false;
        foreach (var (register, buffer) in buffers)
        {
            string raw = $"cb{register}";
            // Match the positional declaration emitted by HlslEmitter. Rewrite its
            // reads first and retain the original buffer if any read is unresolved.
            var declaration = Regex.Match(hlsl,
                $@"(?m)^cbuffer CB{register} : register\(b{register}\)\s*\{{\s*float4 {raw}\[(\d+)\];\s*\}};");
            if (!declaration.Success || !int.TryParse(declaration.Groups[1].Value, out int rows))
                continue;

            if (!TryBuildLayout(buffer, rows, hlsl, out var lanes, out var members)) continue;

            int end = declaration.Index + declaration.Length;
            string original = hlsl[end..];
            string indexed = $@"\b{raw}\s*\[";
            string rewritten = Regex.Replace(original, indexed +
                @"\s*(\d+)\s*\](?:\.([xyzw]{1,4})\b)?(?![\w.\[])", match =>
                {
                    if (!int.TryParse(match.Groups[1].Value, out int row) || (uint)row >= (uint)rows)
                        return match.Value;
                    string swizzle = match.Groups[2].Success ? match.Groups[2].Value : Components;
                    return Read(lanes, row, swizzle) ?? match.Value;
                });
            if (rewritten == original || Regex.IsMatch(rewritten, indexed)) continue;

            hlsl = hlsl[..declaration.Index] + $"cbuffer CB{register} : register(b{register})\n{{\n" +
                   string.Join("\n", members) + "\n};" + rewritten;
            restored = true;
        }
        return (hlsl, restored);
    }

    private static bool TryBuildLayout(AssetConstantBuffer buffer, int rows, string hlsl,
                                       out Dictionary<int, Lane> lanes, out List<string> declarations)
    {
        lanes = new();
        declarations = new();
        if (buffer.Size is <= 0 or > MaxBufferSize || rows <= 0 || rows > (buffer.Size + 15) / 16)
            return false;

        var names = new HashSet<string>(StringComparer.Ordinal);
        int end = 0;
        foreach (var member in buffer.Members.OrderBy(m => m.Offset))
        {
            // Unity ShaderParamType for 32-bit float, signed int, and unsigned int.
            string scalar = member.Type switch { 0 => "float", 2 => "int", 5 => "uint", _ => null };
            int offset = member.Offset;
            int col = offset % 16 / 4;
            int width = member.Dim;
            bool array = member.ArraySize > 0;
            if (scalar == null || member.RowCount != 1 || width is < 1 or > 4 || member.ArraySize < 0 ||
                offset < end || offset % 4 != 0 || col + width > 4 || (array && col != 0) ||
                !Identifier.IsMatch(member.Name) || !names.Add(member.Name) ||
                Regex.IsMatch(hlsl, $@"\b{member.Name}\b"))
                return false;

            int count = array ? member.ArraySize : 1;
            long limit = (long)offset + (array ? (long)count * 16 : width * 4);
            if (limit > buffer.Size) return false;
            end = (int)limit;
            declarations.Add(CbLayout.Line(new CbLayout.Member
            {
                Name = member.Name, Type = width == 1 ? scalar : scalar + width,
                Row = offset / 16, Col = col, IsArray = array, Rows = count,
            }));

            for (int i = 0; i < count; i++)
            {
                string expression = array ? $"{member.Name}[{i}]" : member.Name;
                int firstLane = offset / 4 + i * 4;
                for (int c = 0; c < width; c++)
                    lanes.Add(firstLane + c, new Lane(expression, scalar, width, c));
            }
        }
        return declarations.Count > 0;
    }

    private static string Read(Dictionary<int, Lane> lanes, int row, string swizzle)
    {
        var selected = new Lane[swizzle.Length];
        for (int i = 0; i < selected.Length; i++)
            if (!lanes.TryGetValue(row * 4 + Components.IndexOf(swizzle[i]), out selected[i])) return null;

        // The DXBC emitter treats raw cbuffer lanes as float bits. Integer
        // declarations must preserve those bits for existing asint/asuint reads.
        static string AsFloat(Lane lane, string suffix) => lane.ScalarType == "float"
            ? lane.Expression + suffix : $"asfloat({lane.Expression}{suffix})";

        Lane first = selected[0];
        if (selected.All(lane => lane.Expression == first.Expression) && (first.Width > 1 || selected.Length == 1))
        {
            string mask = new(selected.Select(lane => Components[lane.Component]).ToArray());
            string suffix = first.Width == 1 || mask == Components[..first.Width] ? "" : "." + mask;
            return AsFloat(first, suffix);
        }

        var parts = selected.Select(lane => AsFloat(lane, lane.Width == 1 ? "" : "." + Components[lane.Component]));
        return $"float{selected.Length}({string.Join(", ", parts)})";
    }
}
