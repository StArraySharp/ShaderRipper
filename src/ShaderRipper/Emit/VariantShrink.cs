using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShaderRipper.Emit
{
    /// <summary>
    /// Rewrites a guard chain so the declarations and functions every variant shares
    /// are written once instead of once per branch.
    ///
    /// A chain is emitted branch by branch by default: each <c>#if</c> repeats the
    /// whole program text of its variant.  That reads fine for two or three
    /// variants, but a shader with a hundred keyword combinations spends most of its
    /// bytes repeating the same struct definitions, the same constant buffers and the
    /// same vertex function.  Only the fragment body actually differs, and often only
    /// a handful of lines of it.
    ///
    /// So the bodies are cut into top-level items (one struct, one cbuffer, one
    /// declaration, one function), the items are matched across branches by shape,
    /// and the result is emitted class by class:
    ///
    /// <code>
    /// // written once, because every variant has it
    /// struct SPIRV_Cross_Input_vs { ... };
    ///
    /// // written once per distinct text, behind a guard naming the variants using it
    /// #if defined(A) || defined(B)
    ///     static float4 ps_74;
    /// #elif defined(C)
    ///     static float4 ps_85;
    /// #endif
    /// </code>
    ///
    /// Matching is by shape, not by position: an item's shape is its kind plus its
    /// text with the generated identifiers (<c>vs_N</c>, <c>ps_N</c>, <c>u_xlatN</c>
    /// -- the ones SPIR-V id numbers name, which differ between variants by
    /// construction) replaced by numbered placeholders.  Two branches' fragment
    /// statics then line up even though one says <c>ps_74</c> where the other says
    /// <c>ps_85</c>, and the texts that differ only in those ids stay distinct --
    /// each variant's body must keep reading the variable its own variant declared.
    ///
    /// Every branch fills the groups in the same first-match order, so a branch with
    /// two same-shaped items feeds the first group and then the second, and no group
    /// ever receives two items from one branch.  That is what makes the per-group
    /// guards exact: a group's item set is one item per contributing branch, so a
    /// keyword state either names one of those branches or falls to the fallback --
    /// which is branch 0's item, emitted under <c>#else</c>.
    ///
    /// Groups are emitted by class -- preprocessor lines, types, constant buffers,
    /// declarations, functions -- because only that order is forced: declarations are
    /// independent of each other, and a function has to come after the declarations
    /// it reads.  Within a class, branch 0's order wins, so the output still reads
    /// like the input.
    ///
    /// A chain is left alone when the rewrite cannot be proven equivalent -- a body
    /// the splitter cannot cut, or a variant count too small for the cascade to pay
    /// for its own guards.
    /// </summary>
    internal static class VariantShrink
    {
        /// <summary>
        /// Below this many branches the cascade costs more guard lines than it saves
        /// body lines, so the chain is emitted as-is.
        /// </summary>
        private const int MinimumBranches = 4;

        /// <summary>
        /// Rewrite one chain in place, collapsing it to a single body.  Returns false
        /// when the chain was left untouched.
        /// </summary>
        public static bool Apply(ShaderLab.VariantChain chain)
        {
            if (chain == null || chain.Bodies.Count < MinimumBranches) return false;
            if (chain.Keywords.Count != chain.Bodies.Count) return false;

            int n = chain.Bodies.Count;
            var branches = new List<List<Item>>(n);
            foreach (string body in chain.Bodies)
            {
                var items = Split(body);
                if (items == null) return false;
                branches.Add(items);
            }

            // First-match group filling, branch 0 first so the group order is the
            // order a reader of the unshrunk chain has been looking at.
            //
            // A branch can declare the same shape twice -- the vertex and the
            // fragment stage both copy an interface variable into a static of the
            // same type and name -- and those are different slots, not one.  So a
            // branch's Nth item of one shape goes to the Nth group of that shape,
            // never two into one: the per-branch slot count below, not "any group
            // this branch has not filled yet", is what keeps the two copies apart.
            var slots = new Dictionary<string, List<Group>>(StringComparer.Ordinal);
            var groups = new List<Group>();
            for (int i = 0; i < n; i++)
            {
                var taken = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in branches[i])
                {
                    if (!slots.TryGetValue(item.Shape, out var list))
                        slots[item.Shape] = list = new List<Group>();
                    int nth = taken.Count(s => s == item.Shape);
                    taken.Add(item.Shape);
                    while (list.Count <= nth) list.Add(null);
                    var hit = list[nth];
                    if (hit == null)
                    {
                        hit = new Group { Shape = item.Shape };
                        list[nth] = hit;
                        groups.Add(hit);
                    }
                    hit.Items[i] = item.Text;
                }
            }

            // Groups a branch did not fill cannot be hoisted: the keyword states the
            // chain does not list compile as branch 0, and a state between the covered
            // ones would fall to an empty fallback and read an undeclared identifier.
            // Those items stay with their branches -- a per-branch tail keeps the
            // original internal order, so every body is still preceded by the items it
            // reads.
            //
            // A partially-filled TYPE or DECLARATION is the exception that forbids the
            // rewrite: hoisting moves the full groups to the top of the cascade, while
            // the partial ones move to the tail behind it -- so an interface struct
            // that exists only under some variants ends up declared AFTER the function
            // that takes it, and the function references a type that is not in scope
            // ("unrecognized identifier 'v2f'").  A partial preprocessor line is
            // harmless: an `#include` differs per branch only in its trailing comment,
            // and nothing hoisted depends on which spelling a branch got.  Functions
            // are fine too, since a partial one cannot be referenced by anything that
            // was hoisted.
            var full = new List<Group>();
            var consumed = new HashSet<(int branch, Item item)>();
            foreach (var g in groups)
            {
                if (g.Items.Count != n)
                {
                    string kind = g.Shape.Split('|')[0];
                    if (kind != "pp" && kind != "func" && kind != "semi") return false;
                    continue;
                }
                full.Add(g);
                foreach (var kv in g.Items) consumed.Add((kv.Key, branches[kv.Key].First(it => it.Text == kv.Value)));
            }

            var guard = new GuardBuilder(chain.Keywords);
            var lines = new List<string>();

            foreach (var g in full.OrderBy(g => Class(g.Shape.Split('|')[0])).ThenBy(g => g.First))
            {
                var distinct = g.Items.Values.Distinct().ToList();

                // Every branch contributes this item and they all spell it the same
                // way, so it needs no guard at all.
                if (distinct.Count == 1)
                {
                    lines.Add(distinct[0]);
                    continue;
                }

                bool first = true;
                foreach (string text in distinct)
                {
                    var cover = g.Items.Where(kv => kv.Value == text).Select(kv => kv.Key).ToList();
                    string condition = guard.For(cover);
                    if (condition == null) return false;
                    lines.Add((first ? "#if " : "#elif ") + condition);
                    lines.Add(text);
                    first = false;
                }

                // A keyword state the chain does not list -- one Unity stripped from
                // the build, or a driver's default state -- compiles as branch 0.
                // The guarded chain spelled that out by repeating branch 0 under
                // '#else', and the cascade keeps the promise: the guards above are
                // exact over the states the build shipped, but a state outside them
                // can match none of the guards and fall through the whole group, so
                // branch 0's spelling always stands behind them.
                if (g.Items.TryGetValue(0, out string fallback))
                {
                    lines.Add("#else");
                    lines.Add(fallback);
                }
                lines.Add("#endif");
            }

            // The tails: whatever a branch kept, in the order its own body listed it.
            // Mostly the functions -- a vertex/fragment pair is unique per variant by
            // construction -- plus the items no other branch shared.  Branch 0's tail
            // is repeated under '#else': a state outside the build compiles the base
            // variant, the same promise the guarded chain made.
            for (int i = 0; i < n; i++)
            {
                string condition = guard.For(new List<int> { i });
                if (condition == null) return false;
                lines.Add(i == 0 ? "#if " + condition : "#elif " + condition);
                foreach (var item in branches[i])
                    if (!consumed.Contains((i, item))) lines.Add(item.Text);
            }
            lines.Add("#else");
            foreach (var item in branches[0])
                if (!consumed.Contains((0, item))) lines.Add(item.Text);
            lines.Add("#endif");

            chain.Bodies.Clear();
            chain.Bodies.Add(string.Join("\n", lines));
            chain.Guards.Clear();
            chain.Guards.Add("");
            chain.Keywords.Clear();
            return true;
        }

        private sealed class Item
        {
            public string Text;
            /// <summary>Kind plus the text with generated ids placeholdered.</summary>
            public string Shape;
        }

        private sealed class Group
        {
            public string Shape;
            /// <summary>Branch index -> the text that branch contributes.</summary>
            public Dictionary<int, string> Items = new();
            /// <summary>Branch that created this group; the emit order inside a class follows it.</summary>
            public int First => Items.Keys.Min();
        }

        /// <summary>
        /// Cut a program body into its top-level items.
        ///
        /// The cut has to agree with the grammar, or two branches would be lined up by
        /// different boundaries and the cascade would interleave unrelated text.
        /// <see cref="HlslTree"/> already answers this from the parse tree and degrades
        /// to one <c>raw</c> item when the body does not parse, which the caller reads
        /// as "cannot compress".  A body that carries conditionals of its own cannot be
        /// cut either: the cascade would take the conditional apart.
        /// </summary>
        private static List<Item> Split(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return new List<Item>();

            foreach (string line in body.Replace("\r\n", "\n").Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("#if", StringComparison.Ordinal) ||
                    t.StartsWith("#else", StringComparison.Ordinal) ||
                    t.StartsWith("#elif", StringComparison.Ordinal) ||
                    t.StartsWith("#endif", StringComparison.Ordinal))
                    return null;
            }

            var raw = HlslTree.Split(body, out int errors);
            if (errors > 0) return null;
            if (raw.Count == 1 && raw[0].Kind == "raw") return null;

            var items = new List<Item>(raw.Count);
            foreach (var it in raw)
            {
                string text = it.Text.TrimEnd();
                if (text.Length == 0) continue;
                items.Add(new Item { Text = text, Shape = Shape(Kind(it, text), text) });
            }
            return items;
        }

        private static string Kind(HlslTree.Item it, string text) => it.Kind switch
        {
            "pp" => "pp",
            "struct" => "struct",
            "cbuffer" => "cbuffer",
            "func" => "func",
            "semi" => "semi",
            _ => "decl",
        };

        /// <summary>
        /// The emit class of a kind: preprocessor lines first, then types, then
        /// constant buffers, then declarations, then functions.  Declarations are
        /// independent of each other, so their internal order is free; a function has
        /// to come after the declarations it reads, which the class order guarantees.
        /// </summary>
        private static int Class(string kind) => kind switch
        {
            "pp" => 0,
            "struct" => 1,
            "cbuffer" => 2,
            "func" => 4,
            _ => 3,
        };

        /// <summary>Generated identifiers, spelled from SPIR-V id numbers.</summary>
        private static readonly Regex Generated =
            new(@"\b(?:vs|ps|u_xlatb?)_\d+\b|\bu_xlatb?\d+\b", RegexOptions.CultureInvariant);

        /// <summary>
        /// The text two branches' equivalent items are compared by.
        ///
        /// Functions and structures compare by their exact text.  Their generated
        /// identifiers are not noise a placeholder can absorb: a structure's members
        /// are the interface its branch's functions agree on, and a function reads
        /// the statics its own branch declared, so two branches' spellings are
        /// interchangeable only when they are identical.  Letting a near-match stand
        /// in pairs one branch's function with another's declarations, and the
        /// program stops compiling on an identifier the paired copy never declared.
        ///
        /// Declarations compare by kind, type and name -- with the name taken as the
        /// identifier the declaration introduces, generated or not.  Two branches'
        /// <c>static float4 vs_11</c> are the same slot (the vertex position copy),
        /// and a branch whose module numbered it <c>vs_12</c> lands in the same group
        /// with its own text; a branch that declares TWO float4 statics keeps them in
        /// different groups because their names differ.  Position alone cannot do
        /// this: the number of statics a branch declares varies with its keyword
        /// state, so a slot index in one branch is a different variable in another.
        /// </summary>
        private static string Shape(string kind, string text)
        {
            string head = text.Split('\n')[0].Trim();

            // An include's comment lists the engine names that file supplied for this
            // program; it differs per variant while the directive does not, so only
            // the directive takes part in the comparison.
            if (head.StartsWith("#", StringComparison.Ordinal))
                return kind + "|" + Regex.Match(head, @"^#\s*\w+[^""]*""[^""]*""").Value;

            // Structures and functions: exact text, nothing normalized.
            if (kind is "struct" or "cbuffer" or "func")
                return kind + "|" + text;

            // Declarations: type and name.  `static` is dropped so a plain global and
            // the same global spelled with the keyword still line up.
            string flat = Regex.Replace(text, @"\s+", " ").Trim();
            if (flat.StartsWith("static ", StringComparison.Ordinal)) flat = flat[7..];
            return kind + "|" + flat;
        }

        /// <summary>
        /// Turns a set of branch indices into the shortest <c>#if</c> condition that
        /// selects exactly those branches.
        ///
        /// The exact condition is an OR of the variants' full keyword assignments,
        /// which is unreadable at a hundred branches.  Most items only care about one
        /// or two keyword groups, though, so the condition is minimised: a group is
        /// dropped while the remaining ones still select the same set.  What survives
        /// is the group the item actually depends on.
        /// </summary>
        private sealed class GuardBuilder
        {
            private readonly List<HashSet<string>> _keywords;
            private readonly List<string> _all = new();

            public GuardBuilder(List<HashSet<string>> keywords)
            {
                _keywords = keywords;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var set in keywords)
                    foreach (string k in set)
                        if (seen.Add(k)) _all.Add(k);
                _all.Sort(StringComparer.Ordinal);
            }

            /// <summary>
            /// The condition selecting exactly <paramref name="branches"/>, or null when
            /// no such condition exists.  That is the whole set, which the caller has
            /// already hoisted out of the cascade.
            /// </summary>
            public string For(List<int> branches)
            {
                if (branches.Count == _keywords.Count) return null;

                var want = new HashSet<int>(branches);
                var keep = new List<string>(_all);
                foreach (string candidate in _all)
                {
                    var trial = keep.Where(k => k != candidate).ToList();
                    if (Selects(trial, want)) keep = trial;
                }
                if (keep.Count == 0 || !Selects(keep, want)) return null;

                var terms = new List<string>();
                var covered = new HashSet<int>();
                foreach (int i in branches)
                {
                    if (!covered.Add(i)) continue;
                    var parts = new List<string>();
                    foreach (string k in keep)
                        parts.Add((_keywords[i].Contains(k) ? "" : "!") + "defined(" + k + ")");
                    for (int j = 0; j < _keywords.Count; j++)
                        if (SameState(i, j, keep)) covered.Add(j);
                    terms.Add(string.Join(" && ", parts));
                }
                return terms.Count == 1 ? terms[0]
                     : string.Join(" || ", terms.Select(t => "(" + t + ")"));
            }

            /// <summary>Does this group selection pick out exactly <paramref name="want"/>?</summary>
            private bool Selects(List<string> groups, HashSet<int> want)
            {
                for (int i = 0; i < _keywords.Count; i++)
                {
                    if (want.Contains(i)) continue;
                    foreach (int j in want)
                        if (SameState(i, j, groups)) return false;
                }
                return true;
            }

            private bool SameState(int a, int b, List<string> groups)
            {
                foreach (string k in groups)
                    if (_keywords[a].Contains(k) != _keywords[b].Contains(k)) return false;
                return true;
            }
        }
    }
}
