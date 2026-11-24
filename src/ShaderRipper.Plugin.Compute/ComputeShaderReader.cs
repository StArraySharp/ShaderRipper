using System;
using System.Collections.Generic;
using System.Linq;
using ShaderRipper.Data;

namespace ShaderRipper.Plugin.Compute
{
    /// <summary>
    /// Reads a <c>ComputeShader</c> (ClassID 72) out of a serialized object.
    ///
    /// The shape is nothing like a Shader's.  A Shader holds passes, each with a
    /// slot table pointing into one shared compressed blob; a ComputeShader holds
    /// platform variants, each holding kernels, each holding the compiled DXBC for
    /// every keyword combination of that kernel.  There is no pass, no ShaderLab
    /// render state and no properties block, which is why this is a separate reader
    /// rather than another branch of <c>ParseShader</c>.
    /// </summary>
    internal static class ComputeShaderReader
    {
        /// <summary>
        /// Parse one object body.  Returns null (rather than throwing) when the node
        /// tree does not have the shape the class always has, so a caller can skip a
        /// single object instead of losing the file.
        /// </summary>
        public static ComputeShaderAsset Parse(byte[] data, int offset, int size, TypeNode schema)
        {
            var asset = new ComputeShaderAsset();
            var sr = new SchemaReader(data, offset, checked(offset + size), schema);

            foreach (var c in schema.Children)
            {
                switch (c.Name)
                {
                    case "m_Name":
                        asset.Name = sr.ReadValue(c) as string ?? "";
                        break;

                    case "variants":
                        foreach (var v in Values(sr.ReadValue(c)))
                        {
                            var variant = ReadVariant(v);
                            if (variant != null) asset.Variants.Add(variant);
                        }
                        break;

                    default:
                        sr.Skip(c);
                        break;
                }
            }

            // Kernel order and the keyword table live inside the variants: the
            // asset stores the same kernel list once per platform, so the first
            // variant that names any kernel defines the document order.
            foreach (var v in asset.Variants)
            {
                foreach (var k in v.Kernels)
                    if (!asset.KernelNames.Contains(k.Name)) asset.KernelNames.Add(k.Name);
                foreach (string kw in v.GlobalKeywords)
                    if (!asset.GlobalKeywords.Contains(kw)) asset.GlobalKeywords.Add(kw);
                foreach (string kw in v.LocalKeywords)
                    if (!asset.LocalKeywords.Contains(kw)) asset.LocalKeywords.Add(kw);
            }

            return asset.Variants.Count == 0 ? null : asset;
        }

        private static ComputeVariant ReadVariant(object raw)
        {
            if (raw is not Dictionary<string, object> d) return null;
            var variant = new ComputeVariant
            {
                Platform = Int(d, "targetRenderer"),
                Level = Int(d, "targetLevel"),
            };
            d.TryGetValue("constantBuffers", out var buffers);
            var constantBuffers = Values(buffers).Select(ReadConstantBuffer).ToArray();
            if (d.TryGetValue("kernels", out var kernels))
                foreach (var k in Values(kernels))
                {
                    var kernel = ReadKernel(k, constantBuffers);
                    if (kernel == null) continue;
                    variant.Kernels.Add(kernel);
                    CollectKeywords(k, variant);
                }
            return variant;
        }

        private static AssetConstantBuffer ReadConstantBuffer(object raw)
        {
            if (raw is not Dictionary<string, object> d) return null;
            var buffer = new AssetConstantBuffer { Name = Str(d, "name"), Size = Int(d, "byteSize") };
            if (d.TryGetValue("params", out var parameters))
                foreach (var rawMember in Values(parameters))
                {
                    if (rawMember is not Dictionary<string, object> member) return null;
                    buffer.Members.Add(new AssetCbMember
                    {
                        Name = Str(member, "name"), Offset = Int(member, "offset", -1),
                        Type = Int(member, "type", -1), Dim = Int(member, "colCount"),
                        RowCount = Int(member, "rowCount"), ArraySize = Int(member, "arraySize", -1),
                    });
                }
            return buffer;
        }

        private static ComputeKernel ReadKernel(object raw, AssetConstantBuffer[] constantBuffers)
        {
            if (raw is not Dictionary<string, object> d) return null;
            var kernel = new ComputeKernel { Name = Str(d, "name") };

            if (d.TryGetValue("threadGroupSize", out var tgs))
            {
                var sizes = Values(tgs).Select(x => Convert.ToInt32(x)).ToArray();
                if (sizes.Length > 0) kernel.ThreadGroupSize = sizes;
            }

            // A kernel either carries one program (`code`) or, when it was
            // compiled under keywords, one per unique variant.  Both shapes exist
            // in shipped builds, so both are read.
            AddProgram(d, constantBuffers, kernel.Programs);
            if (d.TryGetValue("uniqueVariants", out var uvs))
                foreach (var uv in Values(uvs))
                    if (uv is Dictionary<string, object> program)
                        AddProgram(program, constantBuffers, kernel.Programs);
            if (d.TryGetValue("variantMap", out var variantMap))
                foreach (var entry in Values(variantMap))
                    if (entry is Dictionary<string, object> pair &&
                        pair.TryGetValue("second", out var second) && second is Dictionary<string, object> program)
                        AddProgram(program, constantBuffers, kernel.Programs);

            if (kernel.Programs.Count == 0) return null;
            return kernel;
        }

        private static void AddProgram(Dictionary<string, object> variant, AssetConstantBuffer[] buffers,
                                       List<ComputeProgram> programs)
        {
            if (!variant.TryGetValue("code", out var rawCode)) return;
            var code = Bytes(rawCode);
            if (code.Length == 0) return;

            var bound = new Dictionary<int, AssetConstantBuffer>();
            programs.Add(new ComputeProgram(code, bound));
            if (!variant.TryGetValue("cbs", out var rawBindings)) return;

            var bindings = Values(rawBindings);
            variant.TryGetValue("cbVariantIndices", out var rawIndices);
            var indices = Values(rawIndices);
            if (indices.Count != 0 && indices.Count != bindings.Count) return;

            var registers = new HashSet<int>();
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i] is not Dictionary<string, object> binding) continue;
                int reg = Int(binding, "bindPoint", -1);
                if (reg < 0) continue;
                if (!registers.Add(reg))
                {
                    bound.Remove(reg);
                    continue;
                }
                string name = Str(binding, "name");
                if (name.Length == 0) continue;
                AssetConstantBuffer buffer;
                if (indices.Count > 0)
                {
                    int index = Int(indices[i], -1);
                    buffer = (uint)index < (uint)buffers.Length ? buffers[index] : null;
                }
                else
                {
                    // Older Unity versions stored the binding name without a layout index.
                    var matches = buffers.Where(b => b?.Name == name).Take(2).ToArray();
                    buffer = matches.Length == 1 ? matches[0] : null;
                }
                if (buffer?.Name == name) bound.Add(reg, buffer);
            }
        }

        /// <summary>Keywords the variant declares, gathered onto the variant.</summary>
        private static void CollectKeywords(object kernelRaw, ComputeVariant variant)
        {
            if (kernelRaw is not Dictionary<string, object> k) return;
            foreach (string field in new[] { "globalKeywords", "localKeywords" })
            {
                if (!k.TryGetValue(field, out var raw)) continue;
                var into = field == "localKeywords" ? variant.LocalKeywords : variant.GlobalKeywords;
                foreach (var one in Values(raw))
                    if (one is string kw && kw.Length > 0 && !into.Contains(kw))
                        into.Add(kw);
            }

            if (!k.TryGetValue("uniqueVariants", out var uvs)) return;
            foreach (var uv in Values(uvs))
            {
                if (uv is not Dictionary<string, object> uvd) continue;
                if (!uvd.TryGetValue("variants", out var vs)) continue;
                foreach (var pair in Values(vs))
                {
                    if (pair is not Dictionary<string, object> pd) continue;
                    string name = Str(pd, "first");
                    if (name.Length > 0 && !variant.GlobalKeywords.Contains(name))
                        variant.GlobalKeywords.Add(name);
                }
            }
        }

        /// <summary>A byte vector is a list of unsigned bytes in the type tree.</summary>
        private static byte[] Bytes(object raw)
        {
            var list = Values(raw);
            if (list.Count == 0) return Array.Empty<byte>();
            var bytes = new byte[list.Count];
            for (int i = 0; i < list.Count; i++) bytes[i] = Convert.ToByte(list[i]);
            return bytes;
        }

        private static List<object> Values(object raw) => raw switch
        {
            List<object> l => l,
            Dictionary<string, object> d when d.TryGetValue("data", out var inner) => Values(inner),
            _ => new List<object>(),
        };

        private static string Str(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) && v is string s ? s : "";

        private static int Int(Dictionary<string, object> d, string k, int fallback = 0) =>
            d.TryGetValue(k, out var value) ? Int(value, fallback) : fallback;

        private static int Int(object value, int fallback) => value switch
        {
            int i => i, uint u when u <= int.MaxValue => (int)u, short s => s,
            byte b => b, long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            _ => fallback,
        };
    }
}
