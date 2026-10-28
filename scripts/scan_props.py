"""How many material Properties does the emitter hand to an #include?

A property that ends up in the include set stops being a member of
`UnityPerMaterial`, so Unity never uploads the material's value for it -- the
body then reads whatever the include's declaration initialises to (zero), which
is a silent rendering bug rather than a compile error.

Usage:  py -3 scan_props.py <exported Assets dir>
"""

import collections
import os
import re
import sys

PROPS = re.compile(r"Properties\s*\{(.*?)\n    \}", re.S)
PROP = re.compile(r"^\s*(?:\[[^\]]+\]\s+)*(\w+)\s*\(", re.M)
INCLUDE = re.compile(r'#include\s+"[^"]+"\s*//(.*)')


def main(root):
    total = 0
    leaked = collections.Counter()
    files = 0
    examples = {}
    for dp, _, fs in os.walk(root):
        for f in fs:
            if not f.endswith(".shader"):
                continue
            files += 1
            text = open(os.path.join(dp, f), encoding="utf-8-sig", errors="replace").read()
            m = PROPS.search(text)
            if not m:
                continue
            props = set(PROP.findall(m.group(1)))
            total += len(props)
            for inc in INCLUDE.finditer(text):
                for name in re.split(r"[,\s]+", inc.group(1)):
                    name = name.strip()
                    if name in props:
                        leaked[name] += 1
                        examples.setdefault(name, f)
    print(f"files={files}  properties={total}  distinct leaked={len(leaked)}")
    for name, n in leaked.most_common(40):
        print(f"  {n:4d}  {name:28s} e.g. {examples[name]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1] if len(sys.argv) > 1 else "Assets"))
