using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// Curated <c>$Globals</c> row names for shaders whose row layout is not
    /// recoverable from the asset itself.
    ///
    /// <c>$Globals</c> is fxc's implicit constant buffer, so a compiled program
    /// records neither its member order nor a member table -- only the rows the
    /// code actually touches are visible in the disassembly.  A hand-written
    /// ShaderLab source for the same shader pins the rest: each uniform there
    /// appears in an expression, and the disassembly shows which operation reads
    /// which row.  When those two agree for every row a candidate name claims,
    /// the assignment is evidence rather than a guess.
    ///
    /// The tables live outside the tool so they can be reviewed and corrected
    /// without a rebuild; <c>tools/verify_globals_names.py</c> re-derives every
    /// claim from the disassembly and from <c>fxc</c>, so a wrong entry fails
    /// loudly instead of silently mislabelling the output.
    ///
    /// File layout -- <c>&lt;dir&gt;/&lt;shader file name&gt;.json</c> (JSONC:
    /// <c>//</c> comments allowed, and used for the per-row evidence):
    /// <code>
    /// {
    ///   "shader": "ADOFAI/Frames",
    ///   "$Globals": {
    ///     "size": 64,
    ///
    ///     // o.color = v.color * _Color * _RendererColor
    ///     "2":   "float4 _Color",       // whole row = one variable; lanes from the type
    ///     "3.x": "float _Frame",        // explicit lane when only part of a row is named
    ///     "3.y": "float _Frames"
    ///   }
    /// }
    /// </code>
    /// A sized buffer key ("$Globals@336") marks rows that REPLACE asset reflection
    /// for that byte size.  The older nested form ("rows": { "2": { "xyzw": ... } })
    /// is still accepted.
    /// </summary>
    internal sealed class GlobalsNames
    {
        /// <summary>cbuffer name -> row -> (lane chars -> "type name").</summary>
        public readonly Dictionary<string, Dictionary<int, Dictionary<string, string>>> Buffers
            = new(StringComparer.Ordinal);

        /// <summary>cbuffer name -> byte size the table was authored against.</summary>
        public readonly Dictionary<string, int> Sizes = new(StringComparer.Ordinal);

        /// <summary>
        /// When true, sized rows REPLACE whatever the asset reflection already
        /// named (reflection can describe a compile other than the one in the
        /// build -- ADOFAI's MergeSprite).  Default false: tables only fill gaps.
        /// </summary>
        public bool Override;

        public string Shader;

        /// <summary>Directory the tables are read from; null disables the feature.</summary>
        public static string TableDir;

        private static readonly Dictionary<string, GlobalsNames> Cache = new(StringComparer.Ordinal);

        /// <summary>Table for one shader, or null when none was authored.</summary>
        public static GlobalsNames For(string shaderKey)
        {
            if (TableDir == null || shaderKey == null) return null;
            if (Cache.TryGetValue(shaderKey, out var hit)) return hit;

            GlobalsNames res = null;
            string path = Path.Combine(TableDir, shaderKey + ".json");
            if (File.Exists(path))
            {
                try { res = Parse(File.ReadAllText(path)); }
                catch (Exception e) { Console.Error.WriteLine($"  [!] names table {path}: {e.Message}"); }
            }
            Cache[shaderKey] = res;
            return res;
        }

        /// <summary>Top-level keys that describe the table rather than name rows.</summary>
        private static readonly HashSet<string> Meta = new(StringComparer.Ordinal)
            { "shader", "note", "witnesses", "source" };

        /// <summary>
        /// Removes <c>//</c> and <c>/* */</c> comments outside string literals, so
        /// tables can carry their evidence next to the row it justifies.
        /// </summary>
        public static string StripComments(string json)
        {
            var sb = new System.Text.StringBuilder(json.Length);
            bool str = false, esc = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (str) { sb.Append(c); if (esc) esc = false; else if (c == '\\') esc = true; else if (c == '"') str = false; continue; }
                if (c == '"') { str = true; sb.Append(c); continue; }
                if (c == '/' && i + 1 < json.Length && json[i + 1] == '/') { while (i < json.Length && json[i] != '\n') i++; if (i < json.Length) sb.Append('\n'); continue; }
                if (c == '/' && i + 1 < json.Length && json[i + 1] == '*') { i += 2; while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/')) i++; i++; continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Lane chars a declared type fills, "xyzw" when unknown.</summary>
        private static string LanesOf(string type) => WidthOf(type) switch
        {
            1 => "x", 2 => "xy", 3 => "xyz", _ => "xyzw",
        };

        public static GlobalsNames Parse(string json)
        {
            var res = new GlobalsNames();
            using var doc = JsonDocument.Parse(StripComments(json));
            var root = doc.RootElement;
            if (root.TryGetProperty("shader", out var s)) res.Shader = s.GetString();
            if (root.TryGetProperty("override", out var ov) && ov.ValueKind == JsonValueKind.True)
                res.Override = true;

            void AddRow(Dictionary<int, Dictionary<string, string>> table, string key, string spec)
            {
                int dot = key.IndexOf('.');
                int row = int.Parse(dot < 0 ? key : key.Substring(0, dot));
                string lanes = dot < 0 ? LanesOf(TypeOf(spec)) : key.Substring(dot + 1);
                if (!table.TryGetValue(row, out var l)) table[row] = l = new Dictionary<string, string>(StringComparer.Ordinal);
                l[lanes] = spec;
            }

            foreach (var cb in root.EnumerateObject())
            {
                if (Meta.Contains(cb.Name)) continue;
                var body = cb.Value;
                if (body.ValueKind != JsonValueKind.Object) continue;
                if (body.TryGetProperty("size", out var sz)) res.Sizes[cb.Name] = sz.GetInt32();

                var table = new Dictionary<int, Dictionary<string, string>>();
                foreach (var e in body.EnumerateObject())
                {
                    if (e.Value.ValueKind == JsonValueKind.String)
                    {
                        AddRow(table, e.Name, e.Value.GetString());
                    }
                    else if (e.Name == "rows" && e.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var r in e.Value.EnumerateObject())
                            foreach (var l in r.Value.EnumerateObject())
                                if (l.Value.ValueKind == JsonValueKind.String)
                                    AddRow(table, r.Name + "." + l.Name, l.Value.GetString());
                    }
                }
                if (table.Count > 0) res.Buffers[cb.Name] = table;
            }
            return res;
        }

        public static string TypeOf(string spec)
        {
            int sp = spec.IndexOf(' ');
            return sp < 0 ? "float4" : spec.Substring(0, sp);
        }

        public static string NameOf(string spec)
        {
            int sp = spec.IndexOf(' ');
            return sp < 0 ? spec : spec.Substring(sp + 1).Trim();
        }

        /// <summary>Component count a declared type occupies, 4 when unknown.</summary>
        public static int WidthOf(string type) => type switch
        {
            "float" or "half" or "int" or "uint" or "bool" => 1,
            "float2" or "half2" or "int2" => 2,
            "float3" or "half3" or "int3" => 3,
            _ => 4,
        };

        /// <summary>Rows the table has an entry for, in a stable order.</summary>
        public IEnumerable<int> RowsOf(string cb) =>
            Buffers.TryGetValue(cb, out var t) ? t.Keys.OrderBy(x => x) : Enumerable.Empty<int>();

        /// <summary>
        /// <c>"$Globals@288"</c> style keys exist because one shader can compile its
        /// keyword variants into differently packed <c>$Globals</c> blocks (ADOFAI's
        /// MergeSprite: 272 B for the base variants, 288 B when the T5 group moves).
        /// Returns the size-specific table when present, else the unsized one.
        /// </summary>
        public bool TrySizedTable(string cb, int size, out string key)
        {
            key = null;
            if (size > 0 && Buffers.ContainsKey(cb + "@" + size)) { key = cb + "@" + size; return true; }
            if (Buffers.ContainsKey(cb)) { key = cb; return true; }
            return false;
        }

        public Dictionary<string, string> Row(string cb, int row) =>
            Buffers.TryGetValue(cb, out var t) && t.TryGetValue(row, out var l) ? l : null;

        /// <summary>
        /// The variable a row is made of, when exactly one declared variable spans
        /// it.  Null when the row is a pack of scalars.
        /// </summary>
        public (string type, string name)? WholeRow(string cb, int row)
        {
            var lanes = Row(cb, row);
            if (lanes == null || lanes.Count == 0) return null;
            var names = lanes.Values.Select(NameOf).Distinct().ToList();
            if (names.Count != 1) return null;
            string spec = lanes.Values.First();
            if (TypeOf(spec) == "float") return null;
            if (WidthOf(TypeOf(spec)) != 4) return null;
            return (TypeOf(spec), names[0]);
        }

        /// <summary>Declarations the table implies, one per distinct variable.</summary>
        public List<(string type, string name)> Declarations(string cb)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var res = new List<(string, string)>();
            foreach (int row in RowsOf(cb))
                foreach (var spec in Row(cb, row).Values)
                {
                    var (ty, nm) = (TypeOf(spec), NameOf(spec));
                    if (seen.Add(nm)) res.Add((ty, nm));
                }
            return res;
        }
    }
}
