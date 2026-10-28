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
        /// <summary>
        /// Options that consume the token after them.
        ///
        /// Commands are NOT in this set: <c>--list-shaders</c> and <c>--dump</c> name
        /// their input as the next positional (like <c>--game</c> and <c>--asm</c>),
        /// so listing them here would eat that path and leave the command with no
        /// argument at all.
        ///
        /// <c>--game</c> IS in this set: it always takes a path, and leaving it out
        /// put that path in the positional list where its position decided whether it
        /// was read as the input or as the output directory.  `--shaderlab out --game
        /// &lt;dir&gt;` then exported the wrong tree, because `out` came first.
        /// </summary>
        private static readonly HashSet<string> Valued = new(StringComparer.OrdinalIgnoreCase)
        {
            "--game", "--data", "--out", "--root", "--bundles", "--names", "--builtin-index",
            "--matrix-names", "--schema", "--tmp-layouts", "--only", "--dump-filter",
            "--platform", "--glsl2cg", "--disasm", "--lang",
            "--syntax", "--mat-check", "--file", "--file-bare", "--dump-objects", "--asm-parser",
            "--glsl-parser", "--asm-translate", "--jobs",
        };

        /// <summary>Options that accept several values rm/cp-style (space separated).</summary>
        private static readonly HashSet<string> Variadic = new(StringComparer.OrdinalIgnoreCase)
        {
            "--file", "--file-bare",
        };

        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _multi = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _bare = new();

        public IReadOnlyList<string> Bare => _bare;

        /// <summary>All values collected for a repeatable option (e.g. --file).</summary>
        public IReadOnlyList<string> Values(string name) =>
            _multi.TryGetValue(name, out var l) ? (IReadOnlyList<string>)l : Array.Empty<string>();

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

                if (takesValue && t.Equals("--bundles", StringComparison.OrdinalIgnoreCase) &&
                    !System.IO.Directory.Exists(next))
                    takesValue = false;

                if (takesValue)
                {
                    if (!a._multi.TryGetValue(t, out var l)) a._multi[t] = l = new List<string>();
                    l.Add(next);
                    a._values[t] = next;   // Value() keeps the last one for compat
                    i++;

                    // rm/cp-style variadic input: keep swallowing following bare
                    // tokens while they look like files (or wildcards), so
                    // "--file a.assets b.bundle c.apk" works. A token that is
                    // neither an existing file nor a wildcard stays positional
                    // (that is how the legacy "<data> <out>" form keeps working).
                    if (Variadic.Contains(t))
                        while (i + 1 < args.Length)
                        {
                            string nx = args[i + 1];
                            if (nx.StartsWith("-", StringComparison.Ordinal)) break;
                            bool looksLikeInput = System.IO.File.Exists(nx) ||
                                                  nx.Contains('*') || nx.Contains('?');
                            if (!looksLikeInput) break;
                            l.Add(nx);
                            a._values[t] = nx;
                            i++;
                        }
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
