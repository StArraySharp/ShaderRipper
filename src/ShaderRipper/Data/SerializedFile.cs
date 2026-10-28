using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Unity SerializedFile — object table, plus the Shader and Material bodies
    /// the exporter needs.
    ///
    /// Type trees come from <see cref="TpkSchema"/> only.  The hand-transcribed
    /// JSON schemas this used to fall back on covered one Unity generation each
    /// and had to be kept in step by hand; the tpk database covers 403 classes
    /// across 738 versions (2017.4 – 6000.7), which is every file seen so far and
    /// every one the parser is expected to handle.
    /// </summary>
    internal sealed class SerializedFile
    {
        public string UnityVersion;
        public int TargetPlatform;
        public bool EnableTypeTree;
        public uint DataOffset;
        public byte[] Raw;
        public TypeNode Schema;
        public TypeNode MaterialSchema;
        public readonly List<Shader> Shaders = new List<Shader>();
        public readonly List<MaterialAsset> Materials = new List<MaterialAsset>();

        /// <summary>m_Externals entries: index+1 == the PPtr m_FileID that names it.</summary>
        public readonly List<string> Externals = new List<string>();

        /// <summary>Class IDs in type-table order (object.typeId indexes this).</summary>
        public readonly List<int> ClassIds = new List<int>();

        /// <summary>Raw object table: pathID, typeId(index), absolute offset, size.</summary>
        public readonly List<(long pathId, int typeId, long off, uint size)> Objects =
            new List<(long pathId, int typeId, long off, uint size)>();

        /// <summary>
        /// <c>AssetBundle.m_Container</c>: asset path -&gt; the object it names.
        ///
        /// A bundle that was built from an AssetBundle asset carries the full
        /// project-relative path of every object it holds
        /// ("Assets/Resources/camerafilterpack_rain.shader").  That is the only
        /// place the ORIGINAL location of a bundle-packed shader exists -- the
        /// Shader object itself knows nothing but its own name, and a project that
        /// shipped the same name under several paths then has no way back.  Keyed
        /// by pathID because that is what the container's PPtr resolves to.
        /// </summary>
        public readonly Dictionary<long, string> ContainerPaths = new Dictionary<long, string>();

        public static SerializedFile Load(string path,
                                          Func<string, int, TypeNode> tpkClass) =>
            Load(File.ReadAllBytes(path), tpkClass);

        /// <summary>Parse a serialized file that was extracted from a bundle.</summary>
        public static SerializedFile Load(byte[] fileBytes,
                                          Func<string, int, TypeNode> tpkClass)
        {
            var sf = new SerializedFile();
            var b = sf.Raw = fileBytes;

            uint metadataSize = BE(b, 0);
            uint fileSize = BE(b, 4);
            uint version = BE(b, 8);
            uint dataOffset = version >= 22 ? (uint)BE64(b, 0x20) : BE(b, 12);
            if (version >= 22) metadataSize = BE(b, 0x14);

            int i = 16;
            i += 1;
            i += 3;
            if (version >= 22) i += 4 + 8 + 8 + 8;

            var r = new EndianReader(b, i, dataOffset > 0 ? (int)dataOffset : b.Length);
            r.Version = (int)version;
            sf.DataOffset = dataOffset;

            if (version >= 7) sf.UnityVersion = r.ReadCString();
            if (version >= 8) sf.TargetPlatform = r.ReadI32();
            if (version >= 13) sf.EnableTypeTree = r.ReadByte() != 0;

            sf.Schema = tpkClass?.Invoke(sf.UnityVersion, 48);
            sf.MaterialSchema = tpkClass?.Invoke(sf.UnityVersion, 21);

            int typeCount = r.ReadI32();
            var classIds = new int[typeCount];
            for (int t = 0; t < typeCount; t++) classIds[t] = SkipSerializedType(r, sf.EnableTypeTree);
            sf.ClassIds.AddRange(classIds);

            if (version >= 7 && version < 14) r.ReadI32();

            int objectCount = r.ReadI32();
            // Offsets are kept as long and validated: v22 stores 64-bit
            // byteStart, and a corrupt entry must not wrap into a bogus offset.
            var entries = new List<(long pathId, long off, uint size, int typeId)>(objectCount);
            for (int o = 0; o < objectCount; o++)
            {
                r.Align4();
                long pathId = version < 14 ? r.ReadI32() : r.ReadI64();
                long byteStart = version >= 22 ? r.ReadI64() : r.ReadU32();
                uint byteSize = r.ReadU32();
                int typeId = r.ReadI32();
                if (version < 16) r.ReadU16();
                if (version >= 11 && version < 17) r.ReadI16();
                if (version < 11) r.ReadU16();
                long off = byteStart + dataOffset;
                if (off < 0 || byteSize > 0 && (ulong)off + byteSize > (ulong)b.Length) continue;
                entries.Add((pathId, off, byteSize, typeId));
                sf.Objects.Add((pathId, typeId, off, byteSize));
            }

            foreach (var e in entries)
            {
                if (classIds[e.typeId] == 48)
                {
                    try { sf.Shaders.Add(ParseShader(sf, e.pathId, e.off, e.size)); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  [!] shader pathId={e.pathId}: {ex.Message}");
                    }
                }
                else if (classIds[e.typeId] == 21 && sf.MaterialSchema != null)
                {
                    try { sf.Materials.Add(ParseMaterial(sf, e.pathId, e.off, e.size)); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  [!] material pathId={e.pathId}: {ex.Message}");
                    }
                }
                else if (classIds[e.typeId] == 142)
                {
                    try { ReadAssetBundleContainer(sf, e.off, e.size, version); }
                    catch (Exception ex)
                    {
                        if (Environment.GetEnvironmentVariable("SHADERRIP_TYPEDBG") == "1")
                            Console.Error.WriteLine($"  [!] AssetBundle pathId={e.pathId}: {ex.Message}");
                    }
                }
            }
            // The trailing sections describe cross-file references: script types,
            // externals and ref types.  None is needed to read a Shader or Material
            // body, so a file whose tail cannot be parsed is still usable and
            // failing the whole load over it would throw away a complete object
            // table.  Switch builds (platform 19) do put a tail here that the
            // PC-oriented reader does not match.
            try
            {
                ReadTailSections(r, sf, version);
            }
            catch (Exception e)
            {
                // Record it, keep the objects.  Only reachable for a tail layout
                // this reader does not model; the header and object table were
                // already validated.
                if (Environment.GetEnvironmentVariable("SHADERRIP_TYPEDBG") == "1")
                    Console.Error.WriteLine($"  [tail] {e.Message}");
            }

            return sf;
        }

        /// <summary>
        /// The sections that follow the object table: script types, externals and
        /// (from v20) ref types.
        ///
        /// Separated from <see cref="Load"/> so a layout mismatch here can be
        /// contained.  Every read is bounded: the reader's window ends at the
        /// metadata section, and a count that would run past it means the tail is
        /// not the shape this reader knows.
        /// </summary>
        private static void ReadTailSections(EndianReader r, SerializedFile sf, uint version)
        {
            int Remaining() => r.Remaining;

            if (version >= 11 && Remaining() >= 4)
            {
                int st = r.ReadI32();
                // ScriptType = classID (i32) + PPtr(m_FileID i32, m_PathID).
                // m_PathID is 64-bit from v14, so the stride grows 12 -> 16.
                int stride = version >= 14 ? 16 : 12;
                if (st > 0 && st < 1_000_000 && st * stride <= Remaining()) r.Skip(st * stride);
                else if (st != 0) return;   // the tail does not continue in a shape we know
            }

            if (Remaining() < 4) return;
            int extCount = r.ReadI32();
            for (int x = 0; x < extCount && x < 4096; x++)
            {
                // {temp empty string, guid(16) + type(4), path}
                if (Remaining() < 4) return;
                if (version >= 6) r.ReadCString();
                if (Remaining() < 20) return;
                if (version >= 5) r.Skip(20);
                if (Remaining() < 4) return;
                sf.Externals.Add(r.ReadCString());
            }

            if (version >= 20)
            {
                if (Remaining() < 4) return;
                int rc = r.ReadI32();
                // A ref-type entry is at least 4 bytes of class id plus a type
                // tree; if the count cannot possibly fit, the tail is misaligned
                // and continuing would only walk further off the end.
                if (rc < 0 || rc > 65536 || rc * 4 > Remaining()) return;
                for (int t = 0; t < rc; t++) SkipSerializedType(r, sf.EnableTypeTree);
            }
        }

        /// <summary>
        /// Parse an <c>AssetBundle</c> (ClassID 142) far enough to read
        /// <c>m_Container</c>, which maps each object's pathID to the project path it
        /// came from.
        ///
        /// Read by hand rather than through the type tree because a release build
        /// usually strips the bundle's own tree, while the shape is short and fixed:
        /// m_Name, m_PreloadTable, m_Container.
        /// </summary>
        private static void ReadAssetBundleContainer(SerializedFile sf, long off, uint size, uint version)
        {
            var r = new EndianReader(sf.Raw, (int)off, (int)(off + size));

            // m_Name (aligned string).
            int nameLen = r.ReadI32();
            if (nameLen < 0 || nameLen > r.Remaining) return;
            r.Skip(nameLen);
            r.Align4();

            // m_PreloadTable: vector of PPtr<Object>.  Their contents are recomputed
            // from m_Container, so they are skipped -- but the STRIDE has to be right,
            // or the container count below reads garbage.
            int pptrStride = version >= 14 ? 12 : 8;
            int preload = (int)r.ReadU32();
            if (preload < 0 || preload > 1_000_000 || preload * pptrStride > r.Remaining) return;
            r.Skip(preload * pptrStride);

            int count = (int)r.ReadU32();
            if (count < 0 || count > 1_000_000) return;

            for (int i = 0; i < count; i++)
            {
                if (r.Remaining < 4) return;
                int len = r.ReadI32();
                if (len < 0 || len > r.Remaining) return;
                string path = System.Text.Encoding.UTF8.GetString(sf.Raw, r.Position, len);
                r.Skip(len);
                r.Align4();
                if (r.Remaining < 20) return;

                // m_Container holds `AssetBundle.AssetInfo`:
                //
                //     PPtr<Object> preloadIndex   -- first object of the preload run
                //     int          preloadSize    -- how many that run holds
                //     PPtr<Object> asset          -- the object the path names
                //
                // With these bundles both PPtrs are 8 bytes (int fileID + int
                // pathID), so one entry's payload is 8 + 4 + 8 = 20 bytes.  Measured
                // from the bytes, not assumed: the path string is followed by exactly
                // 20 bytes and then the next entry's length prefix.
                r.ReadI32();                    // preloadIndex.m_FileID
                int preloadPathId = r.ReadI32(); // preloadIndex.m_PathID
                r.ReadI32();                    // preloadSize
                r.ReadI32();                    // asset.m_FileID
                int assetPathId = r.ReadI32();   // asset.m_PathID

                if (string.IsNullOrEmpty(path)) continue;
                // The asset PPtr is what the path names.  A SCENE bundle leaves it
                // empty and points at the preload run instead (Addressables refers to
                // scenes by name, not by PPtr), so the preload entry is the fallback
                // rather than a discard.
                long pathId = assetPathId != 0 ? assetPathId : preloadPathId;
                if (pathId == 0) continue;
                // First path wins: an object listed twice keeps the first name the
                // build wrote for it.
                if (!sf.ContainerPaths.ContainsKey(pathId)) sf.ContainerPaths[pathId] = path;
            }
        }

        /// <summary>
        /// Parse a <c>Material</c> (ClassID 21): identity, shader PPtr and the
        /// saved property sheet.  The tree always comes from
        /// <see cref="TpkSchema"/> (the JSON schema never carried Material), so
        /// this works for every Unity generation the tpk covers.
        /// </summary>
        private static MaterialAsset ParseMaterial(SerializedFile sf, long pathId, long off, uint size)
        {
            var mat = new MaterialAsset { PathId = pathId };
            var sr = new SchemaReader(sf.Raw, (int)off, (int)(off + size), sf.MaterialSchema);

            foreach (var c in sf.MaterialSchema.Children)
            {
                switch (c.Name)
                {
                    case "m_Name":
                        mat.Name = (string)sr.ReadValue(c);
                        break;

                    case "m_Shader":
                        if (sr.ReadValue(c) is Dictionary<string, object> p)
                        {
                            mat.ShaderFileId = Int(p, "m_FileID");
                            mat.ShaderPathId = p.TryGetValue("m_PathID", out var pid)
                                ? Convert.ToInt64(pid) : 0;
                        }
                        break;

                    case "m_SavedProperties":
                        if (sr.ReadValue(c) is Dictionary<string, object> sp)
                        {
                            if (sp.TryGetValue("m_TexEnvs", out var te))
                                foreach (var kv in AsList(te))
                                {
                                    if (kv is not Dictionary<string, object> e) continue;
                                    if (e["second"] is not Dictionary<string, object> env) continue;
                                    if (env["m_Texture"] is not Dictionary<string, object> ptr) continue;
                                    mat.TexEnvs.Add(((string)e["first"],
                                                     Int(ptr, "m_FileID"),
                                                     ptr.TryGetValue("m_PathID", out var tp)
                                                         ? Convert.ToInt64(tp) : 0));
                                }
                            if (sp.TryGetValue("m_Floats", out var fl))
                                foreach (var kv in AsList(fl))
                                {
                                    if (kv is not Dictionary<string, object> e) continue;
                                    mat.Floats.Add(((string)e["first"],
                                                    Convert.ToSingle(e["second"])));
                                }
                            if (sp.TryGetValue("m_Colors", out var co))
                                foreach (var kv in AsList(co))
                                {
                                    if (kv is not Dictionary<string, object> e) continue;
                                    if (e["second"] is not Dictionary<string, object> col) continue;
                                    mat.Colors.Add(((string)e["first"],
                                                    Convert.ToSingle(col.TryGetValue("r", out var r) ? r : 0f),
                                                    Convert.ToSingle(col.TryGetValue("g", out var g) ? g : 0f),
                                                    Convert.ToSingle(col.TryGetValue("b", out var b) ? b : 0f),
                                                    Convert.ToSingle(col.TryGetValue("a", out var a) ? a : 0f)));
                                }
                        }
                        break;

                    default:
                        sr.Skip(c);
                        break;
                }
            }
            return mat;
        }

        private static int SkipSerializedType(EndianReader r, bool typeTree)
        {
            int classId = r.ReadI32();
            short scriptIdx = -1;
            bool stripped = false;
            if (r.Version >= 16) stripped = r.ReadByte() != 0;
            if (r.Version >= 17) scriptIdx = r.ReadI16();
            if (r.Version >= 13)
            {
                if (r.Version >= 16 && classId == 114) r.Skip(16);
                r.Skip(16);
            }
            if (typeTree && !stripped && r.Version >= 12)
            {
                int nodeCount = r.ReadI32();
                int strSize = r.ReadI32();
                int stride = r.Version >= 19 ? 32 : 24;
                r.Skip(nodeCount * stride);
                r.Skip(strSize);
            }
            if (typeTree && r.Version >= 21)
            {
                // A bundle node can be truncated (Unity writes some built-in asset
                // bundles that way); report it as bad data instead of letting the
                // reader run off the end of the array.
                if (r.Remaining < 4)
                    throw new InvalidDataException(
                        $"truncated type table: version-21 dependency count missing ({r.Remaining} byte(s) left)");
                int deps = r.ReadI32();
                if (deps > 0 && deps < 4096) r.Skip(deps * 4);
            }
            return classId;
        }

        private static Shader ParseShader(SerializedFile sf, long pathId, long off, uint size)
        {
            var sh = new Shader { PathId = pathId, Offset = off, Size = size };
            var sr = new SchemaReader(sf.Raw, (int)off, (int)(off + size), sf.Schema);

            foreach (var c in sf.Schema.Children)
            {
                switch (c.Name)
                {
                    case "m_Name":
                        sh.Name = (string)sr.ReadValue(c);
                        break;

                    case "m_ParsedForm":
                        sh.ParsedForm = ParseParsedForm(sr, c);
                        break;

                    case "platforms":
                        sh.Platforms = ToU32Array(sr.ReadValue(c));
                        break;

                    case "offsets":
                        sh.Offsets = ToU32Array2D(sr.ReadValue(c));
                        break;

                    case "compressedLengths":
                        sh.CompressedLengths = ToU32Array2D(sr.ReadValue(c));
                        break;

                    case "decompressedLengths":
                        sh.DecompressedLengths = ToU32Array2D(sr.ReadValue(c));
                        break;

                    case "compressedBlob":
                        sh.CompressedBlob = ToBytes(sr.ReadValue(c));
                        break;

                    default:
                        sr.Skip(c);
                        break;
                }
            }
            return sh;
        }

        private static ParsedForm ParseParsedForm(SchemaReader sr, TypeNode pf)
        {
            var result = new ParsedForm();
            foreach (var c in pf.Children)
            {
                switch (c.Name)
                {
                    case "m_PropInfo":
                        foreach (var pi in c.Children)
                        {
                            if (pi.Name != "m_Props") { sr.Skip(pi); continue; }
                            var props = (List<object>)sr.ReadValue(pi);
                            foreach (var pv in props)
                                if (pv is Dictionary<string, object> pd) result.Props.Add(MapProp(pd));
                        }
                        break;

                    case "m_SubShaders":
                        var sss = (List<object>)sr.ReadValue(c);
                        foreach (var sv in sss)
                            if (sv is Dictionary<string, object> sd) result.SubShaders.Add(MapSubShader(sd));
                        break;

                    case "m_Name":
                        result.Name = (string)sr.ReadValue(c);
                        break;

                    case "m_FallbackName":
                        result.FallbackName = (string)sr.ReadValue(c);
                        break;

                    case "m_KeywordNames":
                        foreach (var kv in AsList(sr.ReadValue(c)))
                            if (kv is string ks) result.KeywordNames.Add(ks);
                        break;

                    case "m_CustomEditorName":
                        result.CustomEditorName = (string)sr.ReadValue(c);
                        break;

                    case "m_Dependencies":
                        foreach (var kv in AsList(sr.ReadValue(c)))
                            if (kv is string ks) result.Dependencies.Add(ks);
                        break;

                    default:
                        sr.Skip(c);
                        break;
                }
            }
            return result;
        }

        private static Property MapProp(Dictionary<string, object> p)
        {
            var o = new Property
            {
                Name = Str(p, "m_Name"),
                Description = Str(p, "m_Description"),
                Flags = Int(p, "m_Flags"),
                Type = Int(p, "m_Type"),
            };
            for (int i = 0; i < 4; i++)
                o.DefValue[i] = FVal(p, "m_DefValue[" + i + "]");
            var dt = p.TryGetValue("m_DefTexture", out var d) ? d as Dictionary<string, object> : null;
            if (dt != null)
            {
                o.DefTextureName = Str(dt, "m_DefaultName");
                o.DefTextureDim = Int(dt, "m_TexDim");
            }
            return o;
        }

        private static float FVal(Dictionary<string, object> d, string k)
        {
            if (d == null || !d.TryGetValue(k, out var v) || v == null) return 0f;
            return v switch { float f => f, double db => (float)db, int i => i, uint u => u, _ => 0f };
        }

        /// <summary>Decode a SerializedTagMap dictionary into key/value pairs.</summary>
        private static void ReadTagMap(Dictionary<string, object> node, List<(string k, string v)> into)
        {
            if (node == null) return;
            var tags = node.TryGetValue("tags", out var tl) ? tl as List<object> : null;
            if (tags == null) return;
            foreach (Dictionary<string, object> t in tags)
                into.Add((Str(t, "first"), Str(t, "second")));
        }

        private static SubShader MapSubShader(Dictionary<string, object> ss)
        {
            var o = new SubShader { LOD = Int(ss, "m_LOD") };
            if (ss.TryGetValue("m_Tags", out var tg) && tg is Dictionary<string, object> tgd)
                ReadTagMap(tgd, o.Tags);
            if (ss.TryGetValue("m_Passes", out var pl) && pl is List<object> passes)
                foreach (Dictionary<string, object> p in passes) o.Passes.Add(MapPass(p));
            return o;
        }

        private static Pass MapPass(Dictionary<string, object> p)
        {
            var o = new Pass();
            if (p.TryGetValue("m_State", out var st) && st is Dictionary<string, object> stt)
            {
                o.StateName = Str(stt, "m_Name");
                o.ZClip = FV(stt, "zClip", 1f);
                o.ZClipName = FVName(stt, "zClip");
                o.ZTest = FV(stt, "zTest", 4f);
                o.ZTestName = FVName(stt, "zTest");
                o.ZWrite = FV(stt, "zWrite", 1f);
                o.ZWriteName = FVName(stt, "zWrite");
                o.Culling = FV(stt, "culling", 2f);
                o.CullingName = FVName(stt, "culling");
                o.OffsetFactor = FV(stt, "offsetFactor", 0f);
                o.OffsetFactorName = FVName(stt, "offsetFactor");
                o.OffsetUnits = FV(stt, "offsetUnits", 0f);
                o.OffsetUnitsName = FVName(stt, "offsetUnits");
                o.AlphaToMask = FV(stt, "alphaToMask", 0f);
                o.AlphaToMaskName = FVName(stt, "alphaToMask");
                o.StencilRef = FV(stt, "stencilRef", 0f);
                o.StencilReadMask = FV(stt, "stencilReadMask", 255f);
                o.StencilWriteMask = FV(stt, "stencilWriteMask", 255f);
                o.StencilRefName = FVName(stt, "stencilRef");
                o.StencilReadMaskName = FVName(stt, "stencilReadMask");
                o.StencilWriteMaskName = FVName(stt, "stencilWriteMask");
                o.FogMode = Int(stt, "fogMode");
                o.Lighting = Int(stt, "lighting") != 0;
                o.FogDensity = FV(stt, "fogDensity", 0f);
                o.FogStart = FV(stt, "fogStart", 0f);
                o.FogEnd = FV(stt, "fogEnd", 0f);
                o.RtSeparateBlend = Int(stt, "rtSeparateBlend") != 0;

                for (int b = 0; b < 8; b++)
                {
                    var key = "rtBlend" + b;
                    if (stt.TryGetValue(key, out var rb) && rb is Dictionary<string, object> rbd)
                    {
                        var bs = new BlendState
                        {
                            SrcBlend = FV(rbd, "srcBlend", 1f),
                            DestBlend = FV(rbd, "destBlend", 0f),
                            SrcBlendAlpha = FV(rbd, "srcBlendAlpha", 1f),
                            DestBlendAlpha = FV(rbd, "destBlendAlpha", 0f),
                            BlendOp = FV(rbd, "blendOp", 0f),
                            BlendOpAlpha = FV(rbd, "blendOpAlpha", 0f),
                            ColMask = FV(rbd, "colMask", 15f),
                            ColMaskName = FVName(rbd, "colMask"),
                            SrcBlendName = FVName(rbd, "srcBlend"),
                            DestBlendName = FVName(rbd, "destBlend"),
                            SrcBlendAlphaName = FVName(rbd, "srcBlendAlpha"),
                            DestBlendAlphaName = FVName(rbd, "destBlendAlpha"),
                            BlendOpName = FVName(rbd, "blendOp"),
                            BlendOpAlphaName = FVName(rbd, "blendOpAlpha"),
                        };
                        o.Blends.Add(bs);
                    }
                }
                foreach (var sn in new[] { "stencilOp", "stencilOpFront", "stencilOpBack" })
                {
                    if (stt.TryGetValue(sn, out var so) && so is Dictionary<string, object> sod)
                        o.StencilOps.Add(new StencilOp
                        {
                            Comp = FV(sod, "comp", 0f),
                            PassOp = FV(sod, "pass", 0f),
                            Fail = FV(sod, "fail", 0f),
                            ZFail = FV(sod, "zFail", 0f),
                            CompName = FVName(sod, "comp"),
                            PassName = FVName(sod, "pass"),
                            FailName = FVName(sod, "fail"),
                            ZFailName = FVName(sod, "zFail"),
                        });
                }
            }

            if (p.TryGetValue("m_Tags", out var ptg) && ptg is Dictionary<string, object> ptgd)
            {
                // Pass-level tags override same-named state-level tags.  In
                // Unity's serialized form the Tags{} block inside a Pass lives
                // in the state's m_Tags; the pass-level map is usually empty.
                var passTags = ptgd.TryGetValue("tags", out var tl) ? tl as List<object> : null;
                var stateDict = p.TryGetValue("m_State", out var st2) && st2 is Dictionary<string, object> sd
                    ? sd : null;
                var stateTagDict = stateDict != null && stateDict.TryGetValue("m_Tags", out var stg) && stg is Dictionary<string, object> sgd
                    ? sgd : null;
                if (stateTagDict != null)
                    ReadTagMap(stateTagDict, o.Tags);
                if (passTags != null)
                    foreach (Dictionary<string, object> t in passTags)
                    {
                        var k = Str(t, "first");
                        var v = Str(t, "second");
                        int idx = o.Tags.FindIndex(x => x.k.Equals(k, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) o.Tags[idx] = (k, v);
                        else o.Tags.Add((k, v));
                    }
            }
            o.ProgramMask = (uint)Int(p, "m_ProgramMask");

            if (p.TryGetValue("m_NameIndices", out var niv))
                foreach (var item in AsList(niv))
                    if (item is Dictionary<string, object> pd)
                    {
                        string nm = Str(pd, "first");
                        if (!string.IsNullOrEmpty(nm)) o.NameIndices[nm] = Int(pd, "second");
                    }

            var idxToName = new Dictionary<int, string>(o.NameIndices.Count);
            foreach (var kv in o.NameIndices) idxToName[kv.Value] = kv.Key;

            foreach (var stage in new[] { ("progVertex", "Vertex"), ("progFragment", "Fragment"),
                                          ("progGeometry", "Geometry"), ("progHull", "Hull"),
                                          ("progDomain", "Domain") })
            {
                if (!p.TryGetValue(stage.Item1, out var pv) || pv is not Dictionary<string, object> pvd) continue;

                var subs = new List<object>();
                var paramIdx = new List<object>();
                var paramIdxMap = new Dictionary<int, int>();
                if (pvd.TryGetValue("m_ParameterBlobIndices", out var pbv))
                    paramIdx = AsList(pbv);
                if (pvd.TryGetValue("m_PlayerSubPrograms", out var plv))
                {
                    var groups = AsList(plv);
                    for (int g = 0; g < groups.Count; g++)
                    {
                        var inner = AsList(groups[g]);
                        var pidx = g < paramIdx.Count ? AsList(paramIdx[g]) : new List<object>();
                        for (int i = 0; i < inner.Count; i++)
                        {
                            subs.Add(inner[i]);
                            paramIdxMap[subs.Count - 1] =
                                i < pidx.Count ? Int(new Dictionary<string, object> { ["v"] = pidx[i] }, "v") : -1;
                        }
                    }
                }

                if (subs.Count == 0 && pvd.TryGetValue("m_SubPrograms", out var spl))
                    subs = AsList(spl);

                for (int si = 0; si < subs.Count; si++)
                {
                    if (subs[si] is not Dictionary<string, object> sp) continue;
                    var prog = new SubProgram
                    {
                        Stage = stage.Item2,
                        BlobIndex = Int(sp, "m_BlobIndex"),
                        GpuProgramType = Int(sp, "m_GpuProgramType"),
                        ParameterBlobIndex = paramIdxMap.TryGetValue(si, out var pbi) ? pbi : -1,
                    };
                    if (sp.TryGetValue("m_KeywordIndices", out var kiv))
                        foreach (var kv in AsList(kiv))
                            try { prog.KeywordIndices.Add(Convert.ToInt32(kv)); } catch { }
                    ReadConstantBuffers(sp, idxToName, prog.ConstantBuffers);
                    ReadConstantBuffers(pvd, idxToName, prog.ConstantBuffers);
                    ReadTextureParams(sp, idxToName, prog.TextureParams);
                    ReadTextureParams(pvd, idxToName, prog.TextureParams);
                    o.SubPrograms.Add(prog);
                }
            }
            return o;
        }

        /// <summary>
        /// Read <c>m_Parameters.m_ConstantBuffers</c> (or <c>m_CommonParameters</c>)
        /// off a program node into <paramref name="into"/>.  Each member carries a
        /// name index into the pass's name table and its byte offset
        /// (<c>m_Index</c>), which is exactly what the emitter needs to name a
        /// <c>$Globals</c> row instead of guessing it from the code.
        /// </summary>
        private static void ReadConstantBuffers(Dictionary<string, object> node,
                                                Dictionary<int, string> idxToName,
                                                List<AssetConstantBuffer> into)
        {
            object pars = null;
            if (!node.TryGetValue("m_Parameters", out pars) &&
                !node.TryGetValue("m_CommonParameters", out pars)) return;
            if (pars is not Dictionary<string, object> pd) return;
            if (!pd.TryGetValue("m_ConstantBuffers", out var cbs)) return;

            foreach (var item in AsList(cbs))
            {
                if (item is not Dictionary<string, object> cb) continue;
                var buf = new AssetConstantBuffer
                {
                    Size = Int(cb, "m_Size"),
                    Partial = Bool(cb, "m_IsPartialCB"),
                };
                int ni = Int(cb, "m_NameIndex");
                buf.Name = idxToName.TryGetValue(ni, out var bn) ? bn : ("cb#" + ni);

                if (cb.TryGetValue("m_MatrixParams", out var mp))
                    foreach (var e in AsList(mp))
                        if (e is Dictionary<string, object> m)
                            buf.Members.Add(new AssetCbMember
                            {
                                Name = NameAt(m, idxToName),
                                Offset = Int(m, "m_Index"),
                                Type = Int(m, "m_Type"),
                                RowCount = Int(m, "m_RowCount"),
                                ArraySize = Math.Max(1, Int(m, "m_ArraySize")),
                                IsMatrix = true,
                            });
                if (cb.TryGetValue("m_VectorParams", out var vp))
                    foreach (var e in AsList(vp))
                        if (e is Dictionary<string, object> m)
                            buf.Members.Add(new AssetCbMember
                            {
                                Name = NameAt(m, idxToName),
                                Offset = Int(m, "m_Index"),
                                Type = Int(m, "m_Type"),
                                Dim = Int(m, "m_Dim"),
                                ArraySize = Math.Max(1, Int(m, "m_ArraySize")),
                            });
                if (!string.IsNullOrEmpty(buf.Name) && buf.Name != "cb#0")
                {
                    var have = into.Find(x => x.Name == buf.Name);
                    if (have == null) { into.Add(buf); continue; }
                    have.Size = Math.Max(have.Size, buf.Size);
                    have.Partial |= buf.Partial;
                    foreach (var m in buf.Members)
                        if (!have.Members.Exists(x => x.Name == m.Name && x.Offset == m.Offset))
                            have.Members.Add(m);
                }
            }
        }

        private static string NameAt(Dictionary<string, object> m, Dictionary<int, string> idxToName)
        {
            int ni = Int(m, "m_NameIndex");
            return idxToName.TryGetValue(ni, out var n) ? n : "";
        }

        /// <summary>
        /// Read <c>m_Parameters.m_TextureParams</c> (or <c>m_CommonParameters</c>)
        /// into <paramref name="into"/>.  Each entry names one texture and records
        /// <c>m_Index</c> = the t# sampler register the compiled program reads it
        /// through, plus <c>m_SamplerIndex</c> = the s# binding.  Union on register,
        /// first writer wins (per-stage entries are the more specific ones).
        /// </summary>
        private static void ReadTextureParams(Dictionary<string, object> node,
                                              Dictionary<int, string> idxToName,
                                              List<(string Name, int Index, int Sampler)> into)
        {
            if (node == null) return;
            if (!node.TryGetValue("m_Parameters", out var pars) &&
                !node.TryGetValue("m_CommonParameters", out pars)) return;
            if (pars is not Dictionary<string, object> pd) return;
            if (!pd.TryGetValue("m_TextureParams", out var tps)) return;

            foreach (var item in AsList(tps))
            {
                if (item is not Dictionary<string, object> t) continue;
                string nm = NameAt(t, idxToName);
                if (string.IsNullOrEmpty(nm)) continue;
                int idx = Int(t, "m_Index");
                int samp = Int(t, "m_SamplerIndex");
                if (into.Exists(x => x.Index == idx)) continue;
                into.Add((nm, idx, samp));
            }
        }

        private static bool Bool(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) && v is bool b && b;

        private static string Str(Dictionary<string, object> d, string k) =>            d != null && d.TryGetValue(k, out var v) && v is string s ? s : "";

        private static int Int(Dictionary<string, object> d, string k)
        {
            if (d == null || !d.TryGetValue(k, out var v) || v == null) return 0;
            return v switch
            {
                int i => i, uint u => (int)u, short s => s,
                sbyte b => b, byte by => by, long l => (int)l,
                _ => 0
            };
        }

        private static float FV(Dictionary<string, object> d, string k, float def)
        {
            if (d == null || !d.TryGetValue(k, out var v) || v is not Dictionary<string, object> fv) return def;
            return fv.TryGetValue("val", out var val) && val is float f ? f : def;
        }

        /// <summary>
        /// The property name that accompanies a state value.  An empty result
        /// means the number is a constant; anything else means the number is only
        /// the serialized default and ShaderLab must reference the property.
        /// </summary>
        private static string FVName(Dictionary<string, object> d, string k)
        {
            if (d == null || !d.TryGetValue(k, out var v) || v is not Dictionary<string, object> fv) return "";
            if (!fv.TryGetValue("name", out var n) || n is not string s) return "";
            if (s.Length == 0 || s == "<noninit>") return "";
            return s;
        }

        /// <summary>Accepts either a plain list or the "map" wrapper ({ data: [...] }).</summary>
        private static List<object> AsList(object v)
        {
            if (v is List<object> l) return l;
            if (v is Dictionary<string, object> d)
                foreach (var key in new[] { "data", "Array", "array" })
                    if (d.TryGetValue(key, out var inner))
                    {
                        var r = AsList(inner);
                        if (r.Count > 0) return r;
                    }
            return new List<object>();
        }

        private static uint[] ToU32Array(object v) =>            v is List<object> l ? l.ConvertAll(x => Convert.ToUInt32(x)).ToArray() : Array.Empty<uint>();

        private static uint[][] ToU32Array2D(object v)
        {
            if (v is not List<object> outer) return Array.Empty<uint[]>();
            var res = new uint[outer.Count][];
            for (int i = 0; i < outer.Count; i++) res[i] = ToU32Array(outer[i]);
            return res;
        }

        private static byte[] ToBytes(object v)
        {
            if (v is not List<object> l) return Array.Empty<byte>();
            var a = new byte[l.Count];
            for (int i = 0; i < l.Count; i++) a[i] = Convert.ToByte(l[i]);
            return a;
        }

        private static uint BE(byte[] b, int o) =>
            (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);

        private static long BE64(byte[] b, int o) =>
            ((long)BE(b, o) << 32) | BE(b, o + 4);
    }
}
