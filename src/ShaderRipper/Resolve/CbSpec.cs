namespace ShaderRipper.Resolve
{
    /// <summary>
    /// Small helpers for the <c>"type name"</c> member specs the constant-buffer
    /// analysis passes around, and for the component width of an HLSL type.
    ///
    /// These used to hang off <c>GlobalsNames</c>, the curated per-shader
    /// <c>$Globals</c> row tables.  Those tables turned out to be redundant -- the
    /// asset's own parameter blob carries the same member table with byte offsets,
    /// so <c>CbResolver</c> now reads the names from there and the curated
    /// overrides only ever re-stated what the blob already said.  The helpers are
    /// what remained useful.
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
