"""Regenerate every ANTLR parser under src/ShaderRipper/Grammar/.

The grammars are generated ahead of time and checked in, so building the
project needs no JVM; this script is only for when a .g4 changes.

Two things it does that `java -jar antlr.jar` alone does not:

  * ANTLR's C# target emits no namespace, while this project keeps every
    generated type under ``ShaderRipper``.  The wrapper is applied here so a
    regeneration needs no hand editing -- the same shape the checked-in files
    have.
  * Every grammar in the directory is regenerated, not just one, so a grammar
    and its four outputs cannot drift apart.

Usage:  py -3 gen_grammar.py [--antlr <path-to-antlr-complete.jar>] [grammar...]

With no grammar names, all of them are rebuilt.
"""

import os
import shutil
import subprocess
import sys
import tempfile

GRAMMAR_DIR = os.path.join(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))), "src", "ShaderRipper", "Grammar")
NAMESPACE = "ShaderRipper"
DEFAULT_JAR = os.path.join(tempfile.gettempdir(), "antlr.jar")

# ANTLR's output names, per grammar, for the options used below.
SUFFIXES = ("Lexer.cs", "Parser.cs", "BaseVisitor.cs", "Visitor.cs")


def grammars(explicit):
    if explicit:
        return [g if g.endswith(".g4") else g + ".g4" for g in explicit]
    return sorted(f for f in os.listdir(GRAMMAR_DIR) if f.endswith(".g4"))


def wrap(name, text):
    """Put the generated types inside `namespace ShaderRipper`."""
    if f"namespace {NAMESPACE}" in text:
        return text
    head, sep, body = text.partition("\nusing ")
    if not sep:
        raise SystemExit(f"{name}: no using block to wrap")
    return (head + f"\nnamespace {NAMESPACE} {{\nusing "
            + body.rstrip("\n") + f"\n}} // namespace {NAMESPACE}\n")


def main():
    jar = DEFAULT_JAR
    args = sys.argv[1:]
    if "--antlr" in args:
        i = args.index("--antlr")
        jar = args[i + 1]
        del args[i:i + 2]
    if not os.path.isfile(jar):
        raise SystemExit(
            f"antlr jar not found: {jar}\n"
            "download antlr-4.13.1-complete.jar from antlr.org and pass it with "
            "--antlr <path>, or drop it at the default location above")

    for grammar in grammars(args):
        stem = grammar[:-len(".g4")]
        out = tempfile.mkdtemp(prefix="antlr_")
        rc = subprocess.run(
            ["java", "-jar", jar, "-Dlanguage=CSharp", "-visitor",
             "-no-listener", "-o", out, grammar],
            cwd=GRAMMAR_DIR).returncode
        if rc != 0:
            raise SystemExit(f"{grammar}: antlr failed ({rc})")

        for suffix in SUFFIXES:
            name = stem + suffix
            src = os.path.join(out, name)
            if not os.path.isfile(src):
                # `-no-listener` still emits the visitor pair; a grammar with no
                # visitor rules simply has no Visitor.cs, which is not an error.
                continue
            with open(os.path.join(GRAMMAR_DIR, name), "w",
                      encoding="utf-8", newline="\n") as f:
                f.write(wrap(name, open(src, encoding="utf-8").read()))
            print("wrote", name)

        shutil.rmtree(out, ignore_errors=True)

    print("done")


if __name__ == "__main__":
    main()
