using System;
using System.Collections.Generic;
using System.IO;
using AssetRipper.Primitives;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;

namespace ShaderRipper.Data
{
    /// <summary>
    /// Schema source backed by AssetRipper's TypeTreeDumps database
    /// (<c>data/sourcetpk.tpk</c>, extracted once from the embedded
    /// <c>AssetRipper.SourceGenerated.SourceTpk</c> with <c>tools/tpkdump</c>).
    /// Covers class trees for 403 classes across 738 Unity versions
    /// (2017.4 – 6000.7), which is what lets the parser handle game files from
    /// generations other than the hand-transcribed 6000.x JSON schema.
    /// </summary>
    internal static class TpkSchema
    {
        private static TpkTypeTreeBlob _blob;

        /// <summary>True when the tpk database file is available.</summary>
        public static bool Available
        {
            get
            {
                try { return Blob != null; }
                catch { return false; }
            }
        }

        private static TpkTypeTreeBlob Blob
        {
            get
            {
                if (_blob != null) return _blob;
                string path = Environment.GetEnvironmentVariable("SHADERRIP_TPK")
                              ?? Paths.Find("data/sourcetpk.tpk")
                              ?? Paths.Find("sourcetpk.tpk");
                if (path == null)
                    throw new FileNotFoundException("sourcetpk.tpk not found (set SHADERRIP_TPK or place it under data/)");
                using var f = File.OpenRead(path);
                _blob = (TpkTypeTreeBlob)TpkFile.FromStream(f).GetDataBlob();
                return _blob;
            }
        }

        /// <summary>
        /// Build a <see cref="TypeNode"/> tree for <paramref name="classId"/> at
        /// <paramref name="unityVersion"/>.  Uses the <b>release</b> tree (built
        /// player assets); returns null when the database has no entry.
        /// </summary>
        public static TypeNode BuildClass(int classId, string unityVersion)
        {
            var blob = Blob;
            TpkClassInformation info = null;
            foreach (var c in blob.ClassInformation)
                if (c.ID == classId) { info = c; break; }
            if (info == null) return null;

            UnityVersion want;
            try { want = UnityVersion.Parse(unityVersion); }
            catch { return null; }

            KeyValuePair<UnityVersion, TpkUnityClass> best = default;
            bool have = false;
            foreach (var kv in info.Classes)
            {
                // Only consider type trees not newer than the file. If the
                // database has no entry old enough, return null instead of
                // silently parsing with an incompatible (too new) layout.
                if (kv.Key > want) continue;
                if (!have || kv.Key > best.Key)
                {
                    best = kv;
                    have = true;
                }
            }
            if (!have || best.Value == null) return null;

            var node = blob.NodeBuffer[best.Value.ReleaseRootNode];
            return FromTpk(node, blob);
        }

        private static TypeNode FromTpk(TpkUnityNode node, TpkTypeTreeBlob blob)
        {
            var n = new TypeNode
            {
                Type = blob.StringBuffer[node.TypeName],
                Name = blob.StringBuffer[node.Name],
                Flags = (int)node.MetaFlag,
            };
            foreach (int sub in node.SubNodes)
                n.Children.Add(FromTpk(blob.NodeBuffer[sub], blob));
            return n;
        }
    }
}
