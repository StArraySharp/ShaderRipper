using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// 逐 shader 的 <c>$Globals</c> 布局表（<c>data/tmp_layouts/*.txt</c>）。
    ///
    /// TMP 系着色器的 <c>$Globals</c> 成员表在资产里被剥成 0 项：参数块不记名字，
    /// 资产反射也是空的，行号因此全裸。但布局本身是确定的——它等于 fxc 对
    /// "声明顺序 + HLSL 打包规则"的结果，而声明顺序就在 Unity 自己的 CGIncludes 里。
    ///
    /// 这些表由 <c>tools/gen_tmp_layout.py</c> 生成：拿 Unity 的 CGIncludes 真编一遍
    /// 源码，读 RDEF 抄下来。踩过的坑是 `CBUFFER_START` 只在
    /// <c>SHADER_API_D3D11</c> 下才展开成 <c>cbuffer</c>，不加这个宏编译出来的
    /// <c>$Globals</c> 会把 PerCamera/PerDraw 那些全局一起吞进去（3072 字节），
    /// 完全对不上；加上之后尺寸落到 528 字节 / 33 行，与游戏里实际编译出来的
    /// <c>dcl_constantbuffer</c> 一致，逐成员偏移也与资产反射逐个吻合。
    ///
    /// 运行时不做任何推导：按 <c>dcl</c> 的行数选表，再要求表的成员名与本程序
    /// name table 有足够交集，最后要求本程序读到的每一行都被表覆盖。任何一条不满足
    /// 就整块放弃，行号保持数字。
    /// </summary>
    internal static class TmpLayouts
    {
        public sealed class Member
        {
            public string Name;
            public int Offset;
            public string Type;
        }

        public sealed class Layout
        {
            public string Source;
            public int Bytes;
            public int Rows;
            public List<Member> Members = new();
        }

        private static readonly List<Layout> _all = new();

        public static int Count => _all.Count;
        public static string ResolvedDir { get; private set; }

        /// <summary>默认开启：表里的每个布局都用 <c>dcl</c> 行数 + name table 双重校验过。</summary>
        public static bool Enabled = true;

        public static int Load(string dir = null)
        {
            _all.Clear();
            string root = dir ?? Resolve();
            if (root == null || !Directory.Exists(root)) return -1;
            ResolvedDir = root;

            foreach (string f in Directory.GetFiles(root, "*.txt").OrderBy(x => x, StringComparer.Ordinal))
            {
                Layout cur = null;
                foreach (string raw in File.ReadAllLines(f))
                {
                    string s = raw.Trim();
                    if (s.Length == 0) continue;
                    if (s.StartsWith("layout ", StringComparison.Ordinal))
                    {
                        var parts = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 3) { cur = null; continue; }
                        if (!int.TryParse(parts[1], out int bytes) ||
                            !int.TryParse(parts[2], out int rows)) { cur = null; continue; }
                        cur = new Layout
                        {
                            Bytes = bytes,
                            Rows = rows,
                            Source = Path.GetFileNameWithoutExtension(f)
                                     + (raw.Contains('#') ? " " + raw.Substring(raw.IndexOf('#')).Trim() : ""),
                        };
                        _all.Add(cur);
                        continue;
                    }
                    if (s.StartsWith("#", StringComparison.Ordinal) || cur == null) continue;

                    int at = s.LastIndexOf('@');
                    if (at <= 0) continue;
                    if (!int.TryParse(s.Substring(at + 1).Trim(), out int off)) continue;
                    string head = s.Substring(0, at).Trim();
                    int sp = head.IndexOf(' ');
                    if (sp <= 0) continue;
                    cur.Members.Add(new Member
                    {
                        Type = head.Substring(0, sp),
                        Name = head.Substring(sp + 1).Trim(),
                        Offset = off,
                    });
                }
            }
            return _all.Count;
        }

        private static int Comps(string type) => type switch
        {
            "float" => 1, "float2" => 2, "float3" => 3, "float4" => 4, _ => 0,
        };

        private static int Rows(string type) => type switch
        {
            "float2x2" => 2, "float3x3" => 3, "float4x4" => 4, _ => 0,
        };

        /// <summary>
        /// 为一个 <c>$Globals</c> 程序挑布局并展开成 (row, col) → (表达式, 分量数)。
        /// 选不出可信布局时返回 null。
        /// </summary>
        public static Dictionary<(int row, int col), (string expr, int comps)> Match(
            int declaredRows, ICollection<string> nameTable, SortedSet<int> readRows, out Layout chosen,
            IReadOnlyDictionary<string, int> anchors = null)
        {
            chosen = null;
            if (!Enabled || _all.Count == 0) return null;
            if (declaredRows <= 0 || readRows == null || readRows.Count == 0) return null;

            var known = new HashSet<string>(nameTable ?? Array.Empty<string>(), StringComparer.Ordinal);

            Layout best = null;
            int bestScore = 0;
            foreach (var lay in _all)
            {
                if (lay.Rows < declaredRows || lay.Members.Count == 0) continue;

                var cov = Coverage(lay);
                if (readRows.Any(r => !cov.Contains(r))) continue;

                int overlap = lay.Members.Count(m => known.Contains(m.Name));
                int agree = anchors == null ? 0
                    : anchors.Count(a => lay.Members.Any(m => m.Name == a.Key && m.Offset == a.Value));
                bool accepted = agree >= 3 || overlap >= 4;
                if (!accepted) continue;

                int score = agree * 10 + overlap + (lay.Rows == declaredRows ? 1 : 0);
                if (score > bestScore ||
                    (score == bestScore && best != null && lay.Bytes < best.Bytes))
                {
                    best = lay;
                    bestScore = score;
                }
            }
            if (best == null) return null;
            chosen = best;
            return BuildMap(best);
        }

        /// <summary>
        /// 用**同一个 shader 里已经确认过的**布局展开（跨子程序复用）。
        ///
        /// Surface 着色器生成的分片程序常常连参数块都没有，name table 也只剩
        /// <c>_LightColor0</c>/<c>_SpecColor</c>，单看它自己凑不出证据；但同一个
        /// shader 的顶点程序已经把布局确认过了，而 <c>$Globals</c> 的布局由声明顺序
        /// 决定、同尺寸必然相同。
        /// </summary>
        public static Dictionary<(int row, int col), (string expr, int comps)> MapFrom(
            Layout lay, int declaredRows, SortedSet<int> readRows)
        {
            if (lay == null || readRows == null || readRows.Count == 0) return null;
            if (lay.Rows < declaredRows) return null;
            var cov = Coverage(lay);
            if (readRows.Any(r => !cov.Contains(r))) return null;
            return BuildMap(lay);
        }

        private static Dictionary<(int, int), (string, int)> BuildMap(Layout best)
        {
            var map = new Dictionary<(int, int), (string, int)>();
            foreach (var m in best.Members)
            {
                int mrows = Rows(m.Type);
                if (mrows > 0)
                {
                    int r0 = m.Offset / 16;
                    if (m.Offset % 16 != 0) continue;
                    for (int k = 0; k < mrows; k++)
                    {
                        map[(r0 + k, 0)] = ($"{m.Name}[{k}]", 4);
                    }
                    continue;
                }

                int comps = Comps(m.Type);
                if (comps == 0) continue;
                if (m.Offset % 4 != 0) continue;
                int row = m.Offset / 16, col = (m.Offset % 16) / 4;
                if (col + comps > 4) continue;

                if (comps == 4 && col == 0) map[(row, 0)] = (m.Name, 4);
                else
                    for (int k = 0; k < comps; k++)
                        map[(row, col + k)] = (m.Name, comps);
            }

            return map;
        }

        /// <summary>这张表覆盖了哪些 16 字节行。</summary>
        private static HashSet<int> Coverage(Layout lay)
        {
            var cov = new HashSet<int>();
            foreach (var m in lay.Members)
            {
                int n = Rows(m.Type);
                if (n > 0)
                    for (int k = 0; k < n; k++) cov.Add(m.Offset / 16 + k);
                else if (Comps(m.Type) > 0)
                    cov.Add(m.Offset / 16);
            }
            return cov;
        }

        /// <summary>Match 返回 null 时的原因（仅调试用）。</summary>
        public static string DebugWhy(int rows, ICollection<string> nameTable, SortedSet<int> readRows)
        {
            var same = _all.Where(l => l.Rows >= rows).ToList();
            if (same.Count == 0) return $"no layout with >= {rows} rows";
            string s = string.Join(" | ", same.Select(l =>
            {
                int sc = l.Members.Count(m => nameTable.Contains(m.Name));
                var cov = Coverage(l);
                var miss = readRows.Where(r => !cov.Contains(r)).ToList();
                return $"{l.Source}:{l.Rows}r,overlap={sc},uncovered=[{string.Join(",", miss)}]";
            }));
            return s;
        }

        private static string Resolve() => Paths.FindDir(Path.Combine("data", "tmp_layouts"));
    }
}
