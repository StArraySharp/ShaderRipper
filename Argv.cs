using System;
using System.Collections.Generic;

namespace ShaderRipper
{
    /// <summary>
    /// 极简命令行解析：<c>--name value</c> / <c>--flag</c>，其余非选项参数按位置收集。
    ///
    /// 之所以不用库，是因为需要一条特殊规则：<c>--bundles</c> 的值可以省（省掉就等于
    /// "扫默认目录"），但不能把紧跟其后的位置参数（游戏数据目录）吞掉。所以只有
    /// 列在 <see cref="Valued"/> 里的选项才会尝试取值，且 <c>--bundles</c> 还要求
    /// 取到的值确实是个目录。
    /// </summary>
    internal sealed class Argv
    {
        private static readonly HashSet<string> Valued = new(StringComparer.OrdinalIgnoreCase)
        {
            "--data", "--out", "--root", "--bundles", "--names", "--builtin-index",
            "--matrix-names", "--schema", "--tmp-layouts", "--only", "--dump",
            "--list-shaders", "--platform", "--glsl2cg",
        };

        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _bare = new();

        public IReadOnlyList<string> Bare => _bare;

        public bool Has(string name) =>
            _flags.Contains(name) || _values.ContainsKey(name);

        public string Value(string name) =>
            _values.TryGetValue(name, out var v) ? v : null;

        public static Argv Parse(string[] args)
        {
            var a = new Argv();
            for (int i = 0; i < args.Length; i++)
            {
                string t = args[i];
                if (!t.StartsWith("-", StringComparison.Ordinal) || t == "-")
                {
                    a._bare.Add(t);
                    continue;
                }

                string next = i + 1 < args.Length ? args[i + 1] : null;
                bool takesValue = Valued.Contains(t) &&
                                  next != null &&
                                  !next.StartsWith("-", StringComparison.Ordinal);

                // --bundles 的值必须是已存在的目录，否则当作"没有值"
                if (takesValue && t.Equals("--bundles", StringComparison.OrdinalIgnoreCase) &&
                    !System.IO.Directory.Exists(next))
                    takesValue = false;

                if (takesValue)
                {
                    a._values[t] = next;
                    i++;
                }
                else
                {
                    a._flags.Add(t);
                }
            }
            return a;
        }
    }
}
