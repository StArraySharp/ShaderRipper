namespace ShaderRipper.Resolve
{
    /// <summary>
    /// Helpers for the <c>"type name"</c> member specs the constant-buffer analysis
    /// passes around, and for the component width of an HLSL type.
    /// </summary>
    internal static class CbSpec
    {
        /// <summary>The type half of a <c>"float4 _Color"</c> spec.</summary>
        public static string TypeOf(string spec)
        {
            int sp = spec.IndexOf(' ');
            return sp < 0 ? "float4" : spec.Substring(0, sp);
        }

        /// <summary>The name half of a <c>"float4 _Color"</c> spec.</summary>
        public static string NameOf(string spec)
        {
            int sp = spec.IndexOf(' ');
            return sp < 0 ? spec : spec.Substring(sp + 1).Trim();
        }

        /// <summary>Component count a declared type occupies, 4 when unknown.</summary>
        public static int WidthOf(string type) => type switch
        {
            "float" or "half" or "int" or "uint" or "bool" => 1,
            "float2" or "half2" or "int2" => 2,
            "float3" or "half3" or "int3" => 3,
            _ => 4,
        };
    }
}
