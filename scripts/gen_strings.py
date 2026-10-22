"""Regenerate StringsData.cs from Localization/strings.{en,zh-Hans}.txt.

The .txt files already carry C# escape syntax (a literal backslash-n means a
newline), so values are copied verbatim; only double quotes need escaping.
This replaces the hand-written generator used earlier -- keeping it in the repo
means the two files can never drift again.
"""

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOC = os.path.join(ROOT, "src", "ShaderRipper", "Localization")
OUT = os.path.join(ROOT, "src", "ShaderRipper", "StringsData.cs")


def load(name):
    path = os.path.join(LOC, name)
    rows = []
    with open(path, "r", encoding="utf-8-sig") as f:
        for line in f:
            line = line.rstrip("\r\n")
            if not line or line.lstrip().startswith("#"):
                continue
            if "\t" not in line:
                raise SystemExit(f"{name}: line without a tab separator: {line[:60]!r}")
            key, val = line.split("\t", 1)
            rows.append((key.strip(), val))
    return rows


def emit(rows, indent="            "):
    out = []
    for key, val in rows:
        out.append(f'{indent}["{key}"] = "{val.replace(chr(34), chr(92) + chr(34))}",')
    return "\n".join(out)


def main():
    en = load("strings.en.txt")
    zh = load("strings.zh-Hans.txt")

    en_keys = [k for k, _ in en]
    zh_keys = [k for k, _ in zh]
    missing_zh = [k for k in en_keys if k not in zh_keys]
    extra_zh = [k for k in zh_keys if k not in en_keys]
    if missing_zh:
        print(f"[warn] missing in zh-Hans: {', '.join(missing_zh)}")
    if extra_zh:
        print(f"[warn] not in en (falls back to itself): {', '.join(extra_zh)}")

    text = (
        "namespace ShaderRipper\n"
        "{\n"
        "    /// <summary>\u53cc\u8bed UI \u6587\u6848\uff0c\u7531 Localization/strings.*.txt \u751f\u6210\uff08literal \\nn = \u6362\u884c\uff09\u3002</summary>\n"
        "    internal static class StringsData\n"
        "    {\n"
        "        public static readonly System.Collections.Generic.Dictionary<string, string> En = new()\n"
        "        {\n"
        f"{emit(en)}\n"
        "        };\n"
        "\n"
        "        public static readonly System.Collections.Generic.Dictionary<string, string> Zh = new()\n"
        "        {\n"
        f"{emit(zh)}\n"
        "        };\n"
        "    }\n"
        "}\n"
    )

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"wrote {OUT}: {len(en)} en / {len(zh)} zh key(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())