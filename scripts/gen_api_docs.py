"""Generate the plugin API reference from the compiler's XML documentation.

The library is built with `GenerateDocumentationFile`, so every public member carries
a doc comment and the compiler writes them all to `ShaderRipper.xml`.  That file is
the single source of truth for this page: nothing here re-describes an API, it only
rearranges what the source already says, so the reference cannot drift from the code.

Only the plugin surface is published.  `ShaderDecompiler` and friends are the embedded
API for host applications; they are documented in the README and are not part of what a
plugin author needs.

Usage:
    py -3 scripts/gen_api_docs.py [--xml <ShaderRipper.xml>] [--out docs/api.md]
"""

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET

# Namespaces that make up the plugin surface.  A member outside them is skipped.
API_NAMESPACES = (
    "ShaderRipper.Plugins",
    "ShaderRipper.Plugins.Containers",
    "ShaderRipper.Plugins.Emit",
    "ShaderRipper.Plugins.Platforms",
)

# Types the compiler only documents because they carry a doc comment, but which are
# `internal`: the launcher, the registry internals and the built-in adapters.  Keeping
# them out of the published reference is the difference between "what a plugin author
# can use" and "everything the assembly happens to contain".
INTERNAL_TYPES = {
    "ShaderRipper.Plugins.BuiltinPlugins",
    "ShaderRipper.Plugins.HostBoundLoadContext",
    "ShaderRipper.Plugins.MatrixChainPass",
    "ShaderRipper.Plugins.PlatformCatalog",
    "ShaderRipper.Plugins.PluginContext",
    "ShaderRipper.Plugins.PluginHost",
}

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DEFAULT_XML = os.path.join(ROOT, "src", "ShaderRipper", "bin", "Release", "net10.0",
                           "ShaderRipper.xml")
DEFAULT_OUT = os.path.join(ROOT, "docs", "api.md")


# --------------------------------------------------------------------- doc text

def normalise(text):
    """Collapse the compiler's line wrapping; keep paragraph breaks."""
    if not text:
        return ""
    text = text.replace("\r\n", "\n")
    lines = [re.sub(r"[ \t]+", " ", ln).strip() for ln in text.split("\n")]
    out, blank = [], False
    for ln in lines:
        if not ln:
            if out and not blank:
                out.append("")
            blank = True
            continue
        out.append(ln)
        blank = False
    return "\n".join(out).strip()


def render(elem, indent=""):
    """Render a doc element tree as Markdown, resolving the common inline tags."""
    if elem is None:
        return ""

    parts = []
    if elem.text:
        parts.append(elem.text)

    for child in elem:
        tag = child.tag
        inner = render(child)

        if tag == "see":
            # <see cref="X"/> or <see langword="null"/>
            langword = child.get("langword")
            cref = child.get("cref")
            if langword:
                parts.append(f"`{langword}`")
            elif cref:
                parts.append(f"`{short_name(cref)}`")
            else:
                parts.append(inner or "")
        elif tag == "paramref":
            parts.append(f"`{child.get('name')}`")
        elif tag == "typeparamref":
            parts.append(f"`{child.get('name')}`")
        elif tag == "c":
            parts.append(f"`{inner}`")
        elif tag == "code":
            parts.append("\n\n```csharp\n" + (child.text or "").strip() + "\n```\n\n")
        elif tag == "para":
            parts.append("\n\n" + inner.strip() + "\n\n")
        elif tag == "list":
            parts.append("\n" + render_list(child, indent) + "\n")
        elif tag in ("b", "strong"):
            parts.append(f"**{inner}**")
        elif tag in ("i", "em"):
            parts.append(f"*{inner}*")
        elif tag == "remarks" or tag == "summary" or tag == "returns":
            parts.append(inner)
        else:
            parts.append(inner)

        if child.tail:
            parts.append(child.tail)

    return "".join(parts)


def render_list(elem, indent):
    lines = []
    for item in elem.findall("item"):
        term = item.find("term")
        desc = item.find("description")
        body = normalise(render(desc))
        if term is not None:
            lines.append(f"{indent}- **{normalise(render(term))}** — {body}")
        else:
            lines.append(f"{indent}- {body}")
    for child in elem.findall("listheader"):
        pass
    return "\n".join(lines)


def short_name(cref):
    """<see cref="M:Ns.Type.Method(...)"/> -> readable short name."""
    if not cref:
        return ""
    name = cref
    if ":" in name[:2]:
        name = name[2:]
    # Strip a parameter list, then qualify down to the last segment.
    name = re.sub(r"\(.*\)$", "", name)
    name = name.replace("{", "<").replace("}", ">")
    return name.rsplit(".", 1)[-1]


# ------------------------------------------------------------------- model

class Member:
    def __init__(self, name, kind, xml, doc_name=None):
        self.name = name
        self.kind = kind
        self.xml = xml
        # `name` is the cleaned form used for display; the raw doc name still carries
        # the parameter list, which is the only place a signature can come from.
        self.signature = member_signature(doc_name or name)
        self.summary = normalise(render(xml.find("summary"))) if xml is not None else ""
        self.remarks = normalise(render(xml.find("remarks"))) if xml is not None else ""
        self.returns = normalise(render(xml.find("returns"))) if xml is not None else ""
        self.parameters = []
        self.type_parameters = []
        self.exceptions = []
        if xml is not None:
            for p in xml.findall("param"):
                self.parameters.append((p.get("name"), normalise(render(p))))
            for p in xml.findall("typeparam"):
                self.type_parameters.append((p.get("name"), normalise(render(p))))
            for e in xml.findall("exception"):
                self.exceptions.append((short_name(e.get("cref")), normalise(render(e))))

    @property
    def anchor(self):
        return anchor_of(self.name)


class TypeEntry:
    def __init__(self, full_name, kind, xml):
        self.full_name = full_name
        self.kind = kind
        self.xml = xml
        self.members = []

    @property
    def name(self):
        return self.full_name.rsplit(".", 1)[-1]

    @property
    def namespace(self):
        return self.full_name.rsplit(".", 1)[0] if "." in self.full_name else ""

    @property
    def summary(self):
        return normalise(render(self.xml.find("summary"))) if self.xml is not None else ""

    @property
    def remarks(self):
        return normalise(render(self.xml.find("remarks"))) if self.xml is not None else ""

    @property
    def anchor(self):
        # The heading is the bare type name, so the anchor has to match that rather
        # than the namespace-qualified name.
        return anchor_of(self.name)


def anchor_of(name):
    """GitHub-style heading anchor."""
    slug = name.replace("`", "").replace("<", "").replace(">", "")
    slug = re.sub(r"\(.*\)", "", slug)
    slug = re.sub(r"[^A-Za-z0-9_. ]", "", slug)
    return slug.strip().lower().replace(" ", "-")


def member_kind(name):
    """'M:' -> method, 'P:' -> property, 'F:' -> field, 'T:' -> type."""
    if name.startswith("T:"):
        return "type"
    if name.startswith("M:"):
        return "method"
    if name.startswith("P:"):
        return "property"
    if name.startswith("F:"):
        return "field"
    return "other"


def clean_doc_name(name):
    """Strip the leading kind marker, generics and a parameter list."""
    if ":" in name[:2]:
        name = name[2:]
    name = name.replace("{", "<").replace("}", ">")
    # `Ns.Type.Method(System.String,System.Int32)` -> `Ns.Type.Method`
    name = re.sub(r"\(.*\)$", "", name)
    return name


def member_signature(doc_name):
    """The parameter list of a method doc name, readable; empty for anything else."""
    if not doc_name:
        return ""
    name = doc_name
    if ":" in name[:2]:
        name = name[2:]
    if "(" not in name:
        return ""
    inner = name[name.index("(") + 1:].rstrip(")")
    if not inner:
        return ""

    aliases = {"System.String": "string", "System.Int32": "int",
               "System.Boolean": "bool", "System.Byte": "byte",
               "System.Object": "object", "System.Single": "float",
               "System.Double": "double", "System.Void": "void"}

    parts = []
    for p in inner.split(","):
        p = p.strip().replace("{", "<").replace("}", ">")
        if p in aliases:
            parts.append(aliases[p])
            continue
        # Drop the namespace: `ShaderRipper.Plugins.PluginRegistry` -> `PluginRegistry`.
        if "." in p:
            p = p.rsplit(".", 1)[-1]
        parts.append(p)
    return ", ".join(parts)


# ------------------------------------------------------------------- parsing

def load(xml_path):
    tree = ET.parse(xml_path)
    root = tree.getroot()

    by_name = {}
    for m in root.findall("members/member"):
        by_name[m.get("name")] = m

    types = {}
    for doc_name, xml in by_name.items():
        kind = member_kind(doc_name)
        if kind != "type":
            continue
        full = clean_doc_name(doc_name)
        if not any(full == ns or full.startswith(ns + ".") for ns in API_NAMESPACES):
            continue
        if full in INTERNAL_TYPES:
            continue
        types[full] = TypeEntry(full, "type", xml)

    # Attach each member to its declaring type: the name up to the last dot that
    # still names a known type.
    for doc_name, xml in by_name.items():
        kind = member_kind(doc_name)
        if kind == "type":
            continue
        full = clean_doc_name(doc_name)
        owner = None
        for t in sorted(types, key=len, reverse=True):
            if full.startswith(t + "."):
                owner = t
                break
        if owner is None:
            continue
        types[owner].members.append(Member(full, kind, xml, doc_name))

    order = {"property": 0, "field": 1, "method": 2, "other": 3}
    for entry in types.values():
        entry.members.sort(key=lambda m: (order.get(m.kind, 9), m.name))
    return types


# ------------------------------------------------------------------- writing

def write_markdown(types, out_path, xml_name):
    lines = []
    a = lines.append

    a("# Plugin API reference")
    a("")
    a("The extensible surface of ShaderRipper: containers, backends and emitter passes.")
    a("A plugin is a class library that implements "
      "[`IShaderRipperPlugin`](#ishaderripperplugin) and registers whichever of those it")
    a("needs. For the worked example, see "
      "[`examples/ShaderRipper.Plugin.Example`](../examples/ShaderRipper.Plugin.Example/ExamplePlugin.cs).")
    a("")
    a("> Generated from the compiler's XML documentation — do not edit by hand. "
      "Regenerate with `py -3 scripts/gen_api_docs.py`.")
    a("")

    # Contents
    a("## Contents")
    a("")
    for ns in API_NAMESPACES:
        entries = sorted((t for t in types.values() if t.namespace == ns),
                         key=lambda t: t.name)
        if not entries:
            continue
        a(f"- **`{ns}`**")
        for t in entries:
            a(f"  - [`{t.name}`](#{t.anchor})")
    a("")
    for ns in API_NAMESPACES:
        entries = sorted((t for t in types.values() if t.namespace == ns),
                         key=lambda t: t.name)
        if not entries:
            continue

        a(f"## Namespace `{ns}`")
        a("")

        for t in entries:
            decl = declaration_of(t)
            a(f"### {t.name}")
            a("")
            a("```csharp")
            a(decl)
            a("```")
            a("")

            if t.summary:
                a(t.summary)
                a("")
            if t.remarks:
                a(t.remarks)
                a("")

            if not t.members:
                continue

            for m in t.members:
                short = m.name.rsplit(".", 1)[-1]
                # The compiler spells a constructor `#ctor`; name it after its type.
                if short in ("#ctor", "#cctor"):
                    short = t.name
                display = f"{short}({m.signature})" if m.signature else short
                a(f"#### `{display}`")
                a("")
                if m.summary:
                    a(m.summary)
                    a("")
                if m.parameters:
                    a("**Parameters**")
                    a("")
                    for name, doc in m.parameters:
                        a(f"- `{name}` — {doc}" if doc else f"- `{name}`")
                    a("")
                if m.type_parameters:
                    a("**Type parameters**")
                    a("")
                    for name, doc in m.type_parameters:
                        a(f"- `{name}` — {doc}" if doc else f"- `{name}`")
                    a("")
                if m.returns:
                    a(f"**Returns** — {m.returns}")
                    a("")
                if m.exceptions:
                    a("**Exceptions**")
                    a("")
                    for kind, doc in m.exceptions:
                        a(f"- `{kind}` — {doc}" if doc else f"- `{kind}`")
                    a("")
                if m.remarks:
                    a(m.remarks)
                    a("")

    a("---")
    a("")
    a(f"*Source of truth: `{xml_name}`.*")
    a("")

    # Collapse runs of blank lines, which the renderer leaves behind.
    text = "\n".join(lines)
    text = re.sub(r"\n{3,}", "\n\n", text)

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    return text


def declaration_of(entry):
    """A one-line C# declaration, inferred from what the type's docs describe."""
    name = entry.name
    generic = ""
    if "<" in name:
        generic = name[name.index("<"):]
        name = name[:name.index("<")]

    kinds = {m.kind for m in entry.members}
    is_iface = name.startswith("I") and len(name) > 1 and name[1].isupper() and "field" not in kinds
    # A public struct is recognisable from its docs: `readonly struct` never appears
    # there, so the two value types are listed by name.  Everything else in this
    # namespace is a class.
    value_types = {"PluginApiVersion"}

    if is_iface:
        kind = "interface"
    elif name in value_types:
        kind = "readonly struct"
    else:
        kind = "class"

    return f"public {kind} {name}{generic}"


def main():
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--xml", default=DEFAULT_XML)
    ap.add_argument("--out", default=DEFAULT_OUT)
    a = ap.parse_args()

    if not os.path.isfile(a.xml):
        sys.exit(f"no XML documentation at {a.xml}\n"
                 f"build the library first: dotnet build src/ShaderRipper -c Release")

    types = load(a.xml)
    if not types:
        sys.exit(f"no plugin types found in {a.xml}")

    text = write_markdown(types, a.out, os.path.basename(a.xml))
    count = sum(1 for _ in re.finditer(r"^### ", text, re.M))
    members = sum(1 for _ in re.finditer(r"^#### ", text, re.M))
    print(f"wrote {a.out}: {count} types, {members} members")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
