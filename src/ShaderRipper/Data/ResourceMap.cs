using System;
using System.Collections.Generic;
using System.IO;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Restores Resources.Load paths from a player build's globalgamemanagers.
    ///
    /// The mapping lives in a ResourceManager object ("m_Container": vector of
    /// {string name; PPtr&lt;Object&gt; asset}). Names are the build-time project
    /// paths, e.g. "assets/resources/camerafilterpack_rainfx.shader", and the
    /// PPtr names the .assets file plus the object's pathID. Loose resources.assets
    /// files carry no such information — this table is the only way back.
    /// </summary>
    internal static class ResourceMap
    {
        /// <summary>Last failure reason, for diagnostics (null when clean).</summary>
        public static string LastError;

        public static Dictionary<(string file, long pathId), string> Load(
            byte[] ggBytes, Func<string, int, TypeNode> tpkClass)
        {
            LastError = null;
            var map = new Dictionary<(string, long), string>();
            SerializedFile gg;
            try { gg = SerializedFile.Load(ggBytes, tpkClass); }
            catch (Exception e) { LastError = "load: " + e.Message; return map; }

            foreach (var (pathId, typeId, off, size) in gg.Objects)
            {
                int cls = typeId >= 0 && typeId < gg.ClassIds.Count ? gg.ClassIds[typeId] : -1;
                TypeNode tree = null;
                try { tree = cls > 0 ? tpkClass?.Invoke(gg.UnityVersion, cls) : null; }
                catch (Exception e) { LastError = $"tpk({cls}): {e.Message}"; }
                if (tree == null) continue;
                var container = tree.Find("m_Container");
                if (container == null) continue;

                object value;
                try
                {
                    var sr = new SchemaReader(gg.Raw, (int)off, (int)(off + size), tree);
                    value = sr.ReadValue(tree);
                }
                catch (Exception e) { LastError = $"read({cls}): {e.Message}"; continue; }

                if (value is not Dictionary<string, object> obj) continue;
                if (!obj.TryGetValue("m_Container", out var rawEntries)) continue;
                if (rawEntries is not List<object> entries) continue;

                foreach (var e in entries)
                {
                    if (e is not Dictionary<string, object> pair) continue;
                    if (!pair.TryGetValue("first", out var nameObj)) continue;
                    if (!pair.TryGetValue("second", out var pptrObj)) continue;
                    if (pptrObj is not Dictionary<string, object> pptr) continue;
                    if (nameObj is not string name || string.IsNullOrEmpty(name)) continue;

                    int fileId = pptr.TryGetValue("m_FileID", out var fid) && fid is int f ? f : 0;
                    long id = pptr.TryGetValue("m_PathID", out var pid) && pid is long p ? p : 0;
                    if (id == 0) continue;

                    string file = (fileId > 0 && fileId <= gg.Externals.Count)
                        ? Path.GetFileName(gg.Externals[fileId - 1])
                        : "";
                    map[(file, id)] = name;
                }
            }
            return map;
        }
    }
}
