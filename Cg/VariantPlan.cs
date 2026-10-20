using System;
using System.Collections.Generic;
using System.Linq;

namespace ShaderRipper.Cg
{
    /// <summary>
    /// Turns the per-stage sub-program list into ShaderLab variant blocks.
    ///
    /// Unity compiles one sub-program per shader-keyword combination and records
    /// the combination in <c>m_KeywordIndices</c>, which indexes the pass's name
    /// table.  Keywords that always appear together are a single toggle, so they
    /// are grouped; each group gets a <c>#pragma multi_compile</c> line and every
    /// variant is emitted behind the exact <c>#if defined(...)</c> guard that
    /// describes it.
    /// </summary>
    internal static class VariantPlan
    {
        public sealed class Group
        {
            public string Name;                                 // the toggle used in the pragma / guard
            public List<string> Aliases = new List<string>();   // co-occurring keywords it stands for
        }

        /// <summary>Keyword names of one sub-program, resolved through the name table.</summary>
        public static List<string> KeywordsOf(SubProgram sp, Dictionary<int, string> indexToName)
        {
            var res = new List<string>();
            foreach (int i in sp.KeywordIndices)
                if (indexToName != null && indexToName.TryGetValue(i, out var n) && !string.IsNullOrEmpty(n))
                    res.Add(n);
            res.Sort(StringComparer.Ordinal);
            return res;
        }

        public static string Key(IEnumerable<string> keywords) =>
            string.Join(",", keywords.OrderBy(x => x, StringComparer.Ordinal));

        /// <summary>
        /// Group keywords that have an identical presence pattern across all the
        /// variants -- they can never be toggled independently, so one pragma line
        /// stands for the whole set.
        /// </summary>
        public static List<Group> Groups(IEnumerable<IEnumerable<string>> keySets)
        {
            var sets = keySets.Select(s => new HashSet<string>(s, StringComparer.Ordinal)).ToList();
            var all = sets.SelectMany(s => s).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var byPattern = new Dictionary<string, Group>(StringComparer.Ordinal);
            var order = new List<Group>();
            foreach (string k in all)
            {
                string pat = string.Join(",", sets.Select(s => s.Contains(k) ? "1" : "0"));
                if (!byPattern.TryGetValue(pat, out var g))
                {
                    g = new Group { Name = k };
                    byPattern[pat] = g;
                    order.Add(g);
                }
                else g.Aliases.Add(k);
            }
            return order;
        }

        public static string Pragma(Group g) => $"#pragma multi_compile __ {g.Name}";

        /// <summary>Exact guard for one variant: every group either defined or not.</summary>
        public static string Guard(List<Group> groups, ICollection<string> keywords)
        {
            var terms = new List<string>();
            foreach (var g in groups)
            {
                bool on = keywords.Contains(g.Name) || g.Aliases.Any(keywords.Contains);
                terms.Add(on ? $"defined({g.Name})" : $"!defined({g.Name})");
            }
            return terms.Count == 0 ? "" : string.Join(" && ", terms);
        }

        /// <summary>Alias defines a variant needs so its body sees every keyword it was built with.</summary>
        public static IEnumerable<string> AliasDefines(List<Group> groups, ICollection<string> keywords)
        {
            foreach (var g in groups)
                if (keywords.Contains(g.Name) || g.Aliases.Any(keywords.Contains))
                    foreach (var a in g.Aliases)
                        yield return a;
        }

        // ------------------------------------------------------------------
        // stripped-variant analysis (no pragma text survives serialization, so
        // multi_compile vs shader_feature is inferred from coverage alone)
        // ------------------------------------------------------------------

        public sealed class Analysis
        {
            public int Groups;
            public int Present, Expected, Missing;
            public bool FullCartesian => Missing == 0;
            /// <summary>Groups that are on in every compiled variant (their __
            /// pragma branch is dead) -- a static keyword Unity kept in the table.</summary>
            public List<string> AlwaysOn = new List<string>();
            public List<string> Notes = new List<string>();
        }

        private static string Signature(List<Group> groups, HashSet<string> kw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var g in groups)
                sb.Append(kw.Contains(g.Name) || g.Aliases.Any(kw.Contains) ? '1' : '0');
            return sb.ToString();
        }

        /// <summary>
        /// Compare the compiled variant set against the full cartesian product of
        /// the toggle groups.  <c>multi_compile</c> enumerates every combination,
        /// so a full match means plain axes; missing combinations mean the build
        /// stripped variants (<c>shader_feature</c> / unused) -- and since the
        /// serialized blob only contains what the game can actually select, the
        /// emitted guard chain is already the complete runtime truth.
        /// </summary>
        public static Analysis Analyze(List<Group> groups, List<HashSet<string>> sets)
        {
            var a = new Analysis { Groups = groups.Count };
            var present = new HashSet<string>(sets.Select(s => Signature(groups, s)));
            a.Present = present.Count;
            a.Expected = 1 << groups.Count;
            a.Missing = a.Expected - a.Present;

            foreach (var g in groups)
            {
                bool anyOff = sets.Any(s => !(s.Contains(g.Name) || g.Aliases.Any(s.Contains)));
                if (!anyOff) a.AlwaysOn.Add(g.Name);
            }

            if (groups.Count == 0)
            {
                a.Notes.Add("no keywords: single variant");
            }
            else if (a.FullCartesian)
            {
                a.Notes.Add($"full cartesian coverage ({a.Present}/{a.Expected}): "
                            + "every toggle combination was compiled -- multi_compile semantics");
            }
            else
            {
                a.Notes.Add($"partial coverage ({a.Present}/{a.Expected}): {a.Missing} combination(s) "
                            + "absent from the build = stripped variants (shader_feature or unused); "
                            + "runtime can never select them, guard chain is complete as-is");
            }
            if (a.AlwaysOn.Count > 0)
                a.Notes.Add("always-on keyword(s): " + string.Join(", ", a.AlwaysOn)
                            + " (their '#pragma multi_compile __ KEY' off-branch is dead but harmless)");
            return a;
        }
    }
}
