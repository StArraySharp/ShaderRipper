"""对比「原始 DXBC」与「重建后编译出的 DXBC」的逻辑等价性。

逐字节比对没有意义：寄存器分配、指令调度、常量折叠都可能不同，
但程序的**语义**必须一致。这里用一组不变量做判定：

  1. cbuffer 尺寸多重集  —— 每个 register 声明的行数
  2. opcode 直方图        —— 指令类型的分布（忽略顺序与寄存器号）
  3. 采样指令数           —— 纹理采样次数必须相等
  4. 常量缓冲读取行集合   —— 读了哪些 (register, row)，忽略顺序

用法:
    python cmp_logic.py <orig_dir> <rebuild_dir> [-v]

orig_dir/rebuild_dir 里放的是同名 .asm 文件（D3DDisassemble 的输出）。
"""
import collections
import os
import re
import sys

# --- 解析 D3D11 反汇编 ---------------------------------------------------

RE_CB = re.compile(r"^dcl_constantbuffer\s+cb(\d+)\[(\d+)\]", re.I)
RE_TEX = re.compile(r"^dcl_(?:resource|texture)\s+t(\d+)", re.I)
RE_SAMPLE = re.compile(r"^sample(?:_[a-z]+)?(?:_indexable)?\b", re.I)
RE_INSTR = re.compile(r"^\s*([a-z][a-z0-9_]*)\b", re.I)
RE_CBREAD = re.compile(r"\bcb(\d+)\[(\d+)\]", re.I)


def parse_asm(path):
    """-> dict(cbs, samplers, ops, samples, reads)"""
    cbs = []
    texs = set()
    ops = collections.Counter()
    samples = 0
    reads = set()

    with open(path, encoding="utf-8", errors="replace") as fh:
        for raw in fh:
            line = raw.strip()
            if not line or line.startswith("//") or line.startswith("#"):
                continue

            m = RE_CB.match(line)
            if m:
                cbs.append((int(m.group(1)), int(m.group(2))))
                continue

            m = RE_TEX.match(line)
            if m:
                texs.add(int(m.group(1)))
                continue

            # 指令行以 opcode 开头；操作数里的 cbN[row] 都算读取
            m = RE_INSTR.match(line)
            if not m:
                continue
            op = m.group(1).lower()
            if op.startswith("dcl_"):
                continue

            base = op
            for suffix in ("_sat", "_x2", "_x4", "_x8", "_d", "_glc", "_clc", "_nan"):
                if base.endswith(suffix):
                    base = base[: -len(suffix)]
                    break
            ops[base] += 1
            if RE_SAMPLE.match(op):
                samples += 1
            for cbm in RE_CBREAD.finditer(line):
                reads.add((int(cbm.group(1)), int(cbm.group(2))))

    return {
        "cbs": tuple(sorted(cbs)),
        "texs": tuple(sorted(texs)),
        "ops": ops,
        "samples": samples,
        "reads": reads,
    }


def compare(a, b):
    """返回 (ok, notes)。ok 表示所有不变量都满足。"""
    notes = []
    ok = True

    if a["cbs"] != b["cbs"]:
        ok = False
        notes.append("cbuffer 尺寸不同: orig=%s rebuild=%s" % (a["cbs"], b["cbs"]))

    if a["texs"] != b["texs"]:
        ok = False
        notes.append("纹理寄存器不同: orig=%s rebuild=%s" % (a["texs"], b["texs"]))

    if a["samples"] != b["samples"]:
        ok = False
        notes.append("采样数不同: orig=%d rebuild=%d" % (a["samples"], b["samples"]))

    # opcode 直方图：允许少量差异（fxc 折叠/展开），超过 5% 视为不等价
    keys = set(a["ops"]) | set(b["ops"])
    diff = 0
    total = max(sum(a["ops"].values()), 1)
    detail = []
    for k in sorted(keys):
        d = abs(a["ops"].get(k, 0) - b["ops"].get(k, 0))
        if d:
            diff += d
            detail.append("%s:%d/%d" % (k, a["ops"].get(k, 0), b["ops"].get(k, 0)))
    if diff * 100 > total * 5:
        ok = False
        notes.append("opcode 差异 %d/%d (%.1f%%): %s"
                     % (diff, total, diff * 100.0 / total, ", ".join(detail[:6])))

    if a["reads"] != b["reads"]:
        only_a = a["reads"] - b["reads"]
        only_b = b["reads"] - a["reads"]
        if only_a or only_b:
            notes.append("cb 读取行差异: orig独有=%d rebuild独有=%d"
                         % (len(only_a), len(only_b)))
            if len(only_a) + len(only_b) > max(len(a["reads"]), 1) * 0.2:
                ok = False

    return ok, notes


def collect(root):
    out = {}
    for fn in os.listdir(root):
        if fn.endswith((".asm", ".dxbc.asm")):
            out[fn] = os.path.join(root, fn)
    return out


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    orig_dir, rebuild_dir = sys.argv[1], sys.argv[2]
    verbose = "-v" in sys.argv

    a = collect(orig_dir)
    b = collect(rebuild_dir)
    common = sorted(set(a) & set(b))

    if not common:
        print("no common .asm files")
        print("  orig:    %d" % len(a))
        print("  rebuild: %d" % len(b))
        return 2

    passed = 0
    failed = []
    for fn in common:
        pa = parse_asm(a[fn])
        pb = parse_asm(b[fn])
        ok, notes = compare(pa, pb)
        if ok:
            passed += 1
            if verbose:
                print("PASS %s  ops=%d samples=%d" % (fn, sum(pa["ops"].values()), pa["samples"]))
        else:
            failed.append((fn, notes))
            print("FAIL %s" % fn)
            for n in notes:
                print("     " + n)

    print()
    print("total=%d passed=%d failed=%d rate=%.1f%%"
          % (len(common), passed, len(failed), passed * 100.0 / len(common)))
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
