"""Working UnityShaderCompiler client: ShaderLab -> DXBC bytecode.

This is the finished article. Everything below is verified against
UnityShaderCompiler.exe 6000.3.10f1 by live capture, not inferred.

===========================================================================
TRANSPORT
===========================================================================
USC is the CLIENT. It dials 127.0.0.1:<port> (port = argv[3]):

    UnityShaderCompiler.exe <baseFolder> <logPath> <port> [...]

The host must be listening before USC starts. USC speaks first.

===========================================================================
MESSAGE FRAMING  (the part that took all the work)
===========================================================================
All three shapes begin with the same 4-byte magic 0x0C0BD1E4:

    payload      magic | u64 len | len bytes
    header-only  magic | u64 flag                (next bytes are another magic)
    bare int     magic | i32                     (8 bytes total)

Disambiguation, total and order-sensitive:

    read magic(4), read 4 bytes lo
    peek 4 (non-blocking)
      == magic  -> bare int, value = (int32)lo          [8-byte message]
      != magic  -> read 4 bytes hi, v = lo | hi<<32
                   peek 4 (non-blocking)
                     == magic -> header-only, value = v
                     != magic -> payload of v bytes

The peek MUST be non-blocking. At end-of-turn no further byte arrives, and a
blocking peek deadlocks the reader (USC then also aborts).

===========================================================================
PAYLOAD ENCODING
===========================================================================
Text commands are ASCII with a "c:" prefix, e.g. "c:preprocess".
Numbers are raw i32, NOT text. Sending ASCII "0" where an i32 is expected
consumes 4 bytes and eats the next message's magic
("failed to match magic number").

===========================================================================
ARGUMENT 2 IS THE INCLUDE ROOT, NOT THE SHADER PATH
===========================================================================
Both c:preprocess and c:compileSnippet take an include-search root, and USC
resolves includes as "<root>/<name>". Passing the shader *file* path makes
every #include unresolvable:

    filepaths: 1 -> .../Assets/Shaders/Probe.shader/HLSLSupport.cginc
    err 2: Couldn't open include file 'HLSLSupport.cginc'.
    -> compileSnippet then drops the snippet entirely with
       "CGPROGRAM block does not contain #pragma vertex ..."

Pass CGIncludes (no trailing separator needed) and everything resolves.

===========================================================================
validAPIs IS A PLATFORM BITMASK
===========================================================================
Passing 0 declares no platform valid. compileSnippet then answers ok=1 with
outsize=0 -- a silent "success" that emits nothing. Pass -1 (all bits) to get
real code.

===========================================================================
BYTECODE BODY
===========================================================================
After the "shader: <ok>" line the reply uses two messages:

    magic | i32 15            (body kind; 15 = DXBC)
    magic | u64 outsize | blob

The blob is NOT raw DXBC: it carries a Unity header first. The DXBC container
starts at the "DXBC" tag, and its own length field at +0x18 gives the exact
byte count, so locating the tag and reading that field is sufficient.

Usage: usc_compile.py [shaderfile] [platform]
       platform: 0=OpenGL 4=D3D11 14=Vulkan ...
"""

import os
import socket
import struct
import subprocess
import sys
import time

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

MAGIC = b"\xe4\xd1\x0b\x0c"
MAGIC_U32 = 0x0C0BD1E4
MAXLEN = 1 << 26
PEEK_TIMEOUT = 0.30


def log(m):
    print(m, flush=True)


def pack_str(payload):
    if isinstance(payload, str):
        payload = payload.encode("ascii", "replace")
    return MAGIC + struct.pack("<Q", len(payload)) + payload


def pack_int(v):
    return MAGIC + struct.pack("<i", int(v))


def pack_cmd(name):
    return pack_str("c:" + name)


class Reader:
    """Framing-aware reader. See module docstring."""

    def __init__(self, conn, verbose=False):
        self.conn = conn
        self.buf = b""
        self.verbose = verbose

    def _fill(self, k, timeout):
        self.conn.settimeout(timeout)
        while len(self.buf) < k:
            chunk = self.conn.recv(65536)
            if not chunk:
                raise EOFError("IPC stream closed")
            self.buf += chunk

    def _take(self, k, timeout):
        self._fill(k, timeout)
        out, self.buf = self.buf[:k], self.buf[k:]
        return out

    def _peek(self, k, timeout=PEEK_TIMEOUT):
        if len(self.buf) >= k:
            return self.buf[:k]
        try:
            self.conn.settimeout(timeout)
            chunk = self.conn.recv(65536)
        except (socket.timeout, OSError):
            return b""
        if not chunk:
            return b""
        self.buf += chunk
        return self.buf[:k]

    def read(self, label="", timeout=20.0):
        magic = self._take(4, timeout)
        if magic != MAGIC:
            raise ValueError(f"{label}: bad magic {magic.hex()}")

        lo, = struct.unpack("<I", self._take(4, timeout))

        if self._peek(4) == MAGIC:
            v = struct.unpack("<i", struct.pack("<I", lo))[0]
            if self.verbose:
                log(f"  <- {label} int {v}")
            return "int", v

        hi, = struct.unpack("<I", self._take(4, timeout))
        v = lo | (hi << 32)

        if self._peek(4) == MAGIC:
            if self.verbose:
                log(f"  <- {label} header {v}")
            return "header", v

        if v > MAXLEN:
            raise ValueError(f"{label}: implausible length {v}")
        payload = self._take(v, timeout) if v else b""
        if self.verbose:
            log(f"  <- {label} payload {len(payload)}B {payload[:80]!r}")
        return "payload", payload


def as_text(kind, val):
    return val.decode("ascii", "replace") if kind == "payload" else ""


class Client:
    def __init__(self, conn, verbose=False):
        self.conn = conn
        self.r = Reader(conn, verbose=verbose)

    def send(self, blob):
        self.conn.sendall(blob)

    # ---------------- preprocess ----------------
    def preprocess(self, source, include_root, platform, valid_apis=-1):
        self.send(pack_cmd("preprocess"))
        self.send(pack_str(source))
        self.send(pack_str(include_root))   # include search root
        self.send(pack_str(include_root))
        self.send(pack_int(0))              # surfaceOnly
        self.send(pack_int(platform))       # buildPlatform
        self.send(pack_int(valid_apis))     # platform bitmask, -1 = all
        self.send(pack_int(0))              # pKW count
        self.send(pack_int(0))              # dKW count

        snips, errors = [], []
        while True:
            kind, val = self.r.read("pp")
            if kind != "payload":
                continue
            line = val.decode("ascii", "replace")
            head = line.split(" ")[0]

            if head in ("includes:", "filepaths:"):
                n = int(line.split(" ")[1])
                for _ in range(n):
                    self.r.read(head[:-1])
                continue

            if head == "snip:":
                sid = int(line.split(" ")[1])
                k, text = self.r.read("snip.text")
                body = as_text(k, text)
                # consume the keyword block up to keywordsEnd
                while True:
                    k2, v2 = self.r.read("kw")
                    if k2 == "payload" and as_text(k2, v2).startswith("keywordsEnd:"):
                        break
                snips.append({"id": sid, "text": body})
                log(f"  snip {sid}: {len(body)}B")
                continue

            if head == "err:":
                f = line.split(" ")[1:4]
                k1, v1 = self.r.read("err.file")
                k2, v2 = self.r.read("err.msg")
                msg = as_text(k2, v2)
                errors.append({"lvl": int(f[0]), "plat": int(f[1]),
                               "line": int(f[2]), "msg": msg})
                if int(f[0]) >= 2:
                    log(f"  !! err {f[0]}/{f[1]}@{f[2]}: {msg[:170]}")
                continue

            if head == "shader:":
                ok = int(line.split(" ")[1]) if len(line.split(" ")) > 1 else 0
                k, v = self.r.read("shader.text")
                return snips, errors, ok != 0, as_text(k, v)

    # ---------------- compileSnippet ----------------
    def compile_snippet(self, source, include_root, function, keywords, platform):
        self.send(pack_cmd("compileSnippet"))
        self.send(pack_str(source))
        self.send(pack_str(include_root))
        self.send(pack_str(include_root))
        self.send(pack_str(""))            # name
        self.send(pack_str("0"))           # pass
        self.send(pack_int(0))             # ppOnly
        self.send(pack_int(0))             # stripLineD
        self.send(pack_int(0))             # buildPlatform
        self.send(pack_int(0))             # rsLen
        self.send(pack_int(len(keywords)))  # pKW
        for k in keywords:
            self.send(pack_str(k))
        self.send(pack_int(0))             # uKW
        self.send(pack_int(0))             # dKW
        self.send(pack_int(0))             # unlabelled
        self.send(pack_str(""))            # flags
        self.send(pack_int(0))             # lang
        self.send(pack_int(function))      # 0=vertex 1=fragment
        self.send(pack_int(platform))
        self.send(pack_str(""))            # reqs
        self.send(pack_int(0))             # mask
        self.send(pack_int(0))             # start

        errors, bindings = [], []
        while True:
            kind, val = self.r.read("cs")
            if kind != "payload":
                continue
            line = val.decode("ascii", "replace")
            head = line.split(" ")[0]

            if head == "err:":
                f = line.split(" ")[1:4]
                self.r.read("err.file")
                k2, v2 = self.r.read("err.msg")
                msg = as_text(k2, v2)
                errors.append(msg)
                log(f"  !! cs err {f[0]}/{f[1]}@{f[2]}: {msg[:170]}")
                continue

            # binding/stat lines are single self-contained payloads
            if head in ("input:", "cb:", "const:", "cbbind:", "texbind:",
                        "sampler:", "uavbind:", "bufferbind:", "stats:"):
                bindings.append(line)
                continue

            if head == "shader:":
                parts = line.split(" ")
                ok = int(parts[1]) if len(parts) > 1 else 0
                if not ok:
                    return False, b"", errors, bindings

                # body: magic|i32(kind) then magic|u64(outsize)|blob
                self.r.read("body.kind")
                k2, v2 = self.r.read("body")
                blob = v2 if k2 == "payload" else b""
                return True, blob, errors, bindings


def _load_d3d_disassembler():
    """ctypes binding for D3DDisassemble (d3dcompiler_47). Returns fn or None.

    The returned callable takes DXBC bytes and yields asm text (or None on
    failure).  Windows ships d3dcompiler_47.dll in System32.
    """
    import ctypes
    try:
        dll = ctypes.WinDLL("d3dcompiler_47")
        fn = dll.D3DDisassemble
        fn.restype = ctypes.HRESULT
        fn.argtypes = [ctypes.c_char_p, ctypes.c_size_t,
                       ctypes.c_uint, ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p)]
    except OSError as e:
        log(f"  [!] d3dcompiler_47 不可用，汇编输出关闭: {e}")
        return None

    def run(dxbc):
        out = ctypes.c_void_p()
        if fn(dxbc, len(dxbc), 0, None, ctypes.byref(out)) != 0 or not out.value:
            return None
        # COM object: first 8 bytes = vtable pointer; then
        # [0]QI [1]AddRef [2]Release [3]GetBufferPointer [4]GetBufferSize
        vptr, = struct.unpack_from("<Q", ctypes.string_at(out.value, 8))
        vtbl = struct.unpack_from("<5Q", ctypes.string_at(vptr, 40))
        release = ctypes.WINFUNCTYPE(ctypes.c_uint, ctypes.c_void_p)(vtbl[2])
        get_ptr = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(vtbl[3])
        get_size = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(vtbl[4])
        try:
            ptr, size = get_ptr(out), get_size(out)
            return ctypes.string_at(ptr, size).decode("utf-8", errors="replace")
        finally:
            release(out)

    return run


def extract_dxbc(blob):
    """Pull the DXBC container out of Unity's bytecode blob.

    The blob is not raw DXBC -- Unity prefixes it. The DXBC container carries
    its own total length at +0x18, so locating the tag and reading that field
    yields the exact extent.
    """
    i = blob.find(b"DXBC")
    if i < 0:
        return None
    if len(blob) < i + 0x1C:
        return None
    total, = struct.unpack_from("<I", blob, i + 0x18)
    end = i + total
    if total <= 0 or end > len(blob):
        return blob[i:]
    return blob[i:end]


USAGE = """usc_compile.py — ShaderLab -> DXBC bytecode via UnityShaderCompiler

用法:
  python usc_compile.py [选项] <shader 文件...>

参数:
  -u, --unity <dir>     Unity 编辑器目录（必填，如 D:\\Softwares\\Unity\\6000.3.10f1；
                        也可以用环境变量 UNITY_EDITOR 指定）
  <shader 文件...>      一个或多个 ShaderLab 文件（.shader / .cginc 片段均可）

选项:
  -p, --platform <名|id> 目标平台：d3d11(默认，DXBC+asm) | gles20 | gles3 |
                        openglcore/glcore（桌面 OpenGL，GLSL 源码）
  -o, --out <dir>       输出目录；默认为第一个输入文件所在目录
  -h, --help            显示本帮助（不带参数也只显示帮助，不会拉起 USC）

说明:
  每个输入文件产出一组 DXBC 字节码（.dxbc）及对应的汇编文本（.asm，经
  D3DDisassemble 反汇编，d3dcompiler_47 不可用时只有二进制）；命名
  <文件名>_<snip>_<vert|frag>_p<平台>.<ext>，usc.log 与产物同目录；
  多个文件共用一次 USC 进程。
"""


def main():
    import argparse

    ap = argparse.ArgumentParser(add_help=False, description=USAGE)
    ap.add_argument("-u", "--unity", default=os.environ.get("UNITY_EDITOR"),
                    help="Unity 编辑器目录（必填）")
    ap.add_argument("shaders", nargs="*")
    ap.add_argument("-p", "--platform", default=4,
                    help="目标平台：d3d11(默认)/gles20/gles3/openglcore(glcore) 或数字 ID")
    ap.add_argument("-o", "--out", default=None)
    ap.add_argument("-h", "--help", action="store_true")
    args = ap.parse_args()

    if args.help or not args.shaders:
        print(USAGE, end="")
        return 0

    if not args.unity:
        print("必须指定 Unity 编辑器目录：-u/--unity <dir> 或环境变量 UNITY_EDITOR")
        return 2

    data_dir = args.unity
    if os.path.isdir(os.path.join(data_dir, "Editor", "Data")):
        data_dir = os.path.join(data_dir, "Editor", "Data")
    usc_exe = os.path.join(data_dir, "Tools", "UnityShaderCompiler.exe")
    cg_includes = os.path.join(data_dir, "Resources", "CGIncludes")
    if not os.path.isfile(usc_exe):
        print(f"UnityShaderCompiler.exe 不存在: {usc_exe}")
        return 2

    platform = args.platform
    # Unity 着色器平台 ID（与 ShaderRipper/Platforms.cs 一致）
    PLATFORM_IDS = {"d3d11": 4, "gles20": 5, "gles3": 9,
                    "openglcore": 15, "glcore": 15}
    if isinstance(platform, str):
        key = platform.strip().lower()
        if key not in PLATFORM_IDS:
            print(f"未知平台: {platform}（可用: {', '.join(PLATFORM_IDS)} 或数字 ID）")
            return 2
        platform = PLATFORM_IDS[key]
    pname = next((n for n, i in PLATFORM_IDS.items() if i == platform), str(platform))
    text_platform = platform in (5, 9, 15)   # GL 系：产物是 GLSL 源码文本

    files = []
    for f in args.shaders:
        if os.path.isfile(f):
            files.append(os.path.abspath(f))
        else:
            print(f"shader 文件不存在，跳过: {f}")

    if not files:
        return 2

    out_dir = args.out or os.path.dirname(files[0])
    os.makedirs(out_dir, exist_ok=True)

    shaders = []
    for path in files:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            shaders.append((os.path.splitext(os.path.basename(path))[0], f.read()))

    logfile = os.path.join(out_dir, "usc.log")

    disasm = _load_d3d_disassembler()

    port = 0
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", 0))
    port = srv.getsockname()[1]
    srv.listen(4)
    srv.settimeout(30)

    t0 = time.time()
    proc = subprocess.Popen(
        [usc_exe, data_dir, logfile, str(port)], cwd=SCRIPT_DIR,
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))

    conn, _ = srv.accept()
    log(f"connected in {time.time() - t0:.2f}s  pid={proc.pid}  port={port}")

    c = Client(conn, verbose=os.environ.get("USC_VERBOSE") == "1")
    rc = 0
    try:
        c.r.read("hello")

        for label, shaderlab in shaders:
            snips, errs, ok, _ = c.preprocess(shaderlab, cg_includes, platform)
            log(f"[{label}] preprocess: ok={ok} snips={len(snips)} errors={len(errs)}")

            if not snips:
                log(f"[{label}] no snippets -> nothing to compile")
                rc = 1
                continue

            for s in snips:
                # GL 系平台（gles20/gles3/openglcore）返回的是 HLSLcc 生成的
                # 完整 GLSL 程序（#ifdef VERTEX + #ifdef FRAGMENT 两个分支），
                # 只在 vertex 调用里下发；fragment 调用会合法地返回空。
                stages = ((0, "vert"),) if text_platform else ((0, "vert"), (1, "frag"))
                for fn, fname in stages:
                    log(f"[{label}] compile snip {s['id']} {fname} platform={platform}")
                    cok, blob, cerrs, binds = c.compile_snippet(
                        s["text"], cg_includes, fn, [], platform)
                    if not cok:
                        log(f"  FAILED ({len(cerrs)} error(s))")
                        rc = 1
                        continue

                    if text_platform:
                        out = os.path.join(out_dir, f"{label}_{s['id']}_{pname}.glsl")
                        with open(out, "w", encoding="utf-8", newline="\n") as f:
                            f.write(blob.decode("utf-8", errors="replace"))
                        log(f"  OK  GLSL {len(blob)}B -> {out}  "
                            f"({len(binds)} binding line(s))")
                        continue

                    dxbc = extract_dxbc(blob)
                    if dxbc:
                        out = os.path.join(out_dir, f"{label}_{s['id']}_{fname}_{pname}.dxbc")
                        with open(out, "wb") as f:
                            f.write(dxbc)
                        log(f"  OK  DXBC {len(dxbc)}B -> {out}  "
                            f"({len(binds)} binding line(s))")
                        if disasm:
                            asm = disasm(dxbc)
                            if asm:
                                aout = out[:-5] + ".asm"
                                with open(aout, "w", encoding="utf-8") as f:
                                    f.write(asm)
                                log(f"  OK  asm {len(asm)}ch -> {aout}")
                    else:
                        out = os.path.join(out_dir, f"{label}_{s['id']}_{fname}_{pname}.bin")
                        with open(out, "wb") as f:
                            f.write(blob)
                        log(f"  OK  raw {len(blob)}B (no DXBC tag) -> {out}")

        c.send(pack_cmd("shutdown"))
        time.sleep(0.3)
    except Exception as e:
        log(f"  ERROR {type(e).__name__}: {e}")
        rc = 1
    finally:
        try:
            conn.close()
        except OSError:
            pass
        srv.close()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()

    return rc


FALLBACK_SHADER = """\
Shader "Probe/Simple" {
    Properties {
        _MainTex ("Base (RGB)", 2D) = "white" {}
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
"""

if __name__ == "__main__":
    raise SystemExit(main())