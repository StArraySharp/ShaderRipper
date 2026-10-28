using System;
using System.Collections.Generic;
using ShaderRipper.Data;
using System.Linq;

namespace ShaderRipper.Emit
{
    /// <summary>
    /// Turns the per-stage sub-program list into ShaderLab variant blocks.
    ///
    /// Unity compiles one sub-program per shader-keyword combination and records
    /// the combination in <c>m_KeywordIndices</c>, which indexes the pass's name
    /// table.  The build therefore tells us, exactly, which keyword states exist
    /// -- but not how they were split across <c>#pragma multi_compile</c> lines.
    /// That split is recovered from which keywords can be on at the same time:
    /// two keywords that never co-occur in any variant are the alternatives of
    /// one line (Unity only ever sets one of them), while keywords that do
    /// co-occur are separate lines.  Each group becomes one <c>multi_compile</c>
    /// and every variant is emitted behind the exact <c>#if</c> guard for it.
    /// </summary>
    internal static class VariantPlan
    {
        public sealed class Group
        {
            /// <summary>Keyword the group is named after (the first one seen).</summary>
            public string Name;
            /// <summary>
            /// Mutually exclusive alternatives of this multi_compile line; the
            /// build never turned two of them on at once.
            /// </summary>
            public List<string> Alternatives = new List<string>();
        }

        /// <summary>
        /// Keyword names of one sub-program, resolved through the name table.
        /// Declaration order is kept: it is the order the original
        /// <c>multi_compile</c> line listed, which makes the rebuilt pragma read
        /// the same as the source it came from.
        /// </summary>
        public static List<string> KeywordsOf(SubProgram sp, Dictionary<int, string> indexToName)
        {
            var res = new List<string>();
            foreach (int i in sp.KeywordIndices)
                if (indexToName != null && indexToName.TryGetValue(i, out var n) && !string.IsNullOrEmpty(n)
                    && !res.Contains(n))
                    res.Add(n);
            return res;
        }

        public static string Key(IEnumerable<string> keywords) =>
            string.Join(",", keywords.OrderBy(x => x, StringComparer.Ordinal));

        /// <summary>
        /// Group keywords that can never be on at the same time -- they are the
        /// alternatives of a single <c>multi_compile</c> line, so one pragma
        /// stands for the whole set.  Keywords that do co-occur stay in separate
        /// groups, each of which becomes its own pragma.
        /// </summary>
        public static List<Group> Groups(IEnumerable<IEnumerable<string>> keySets)
        {
            var sets = keySets.Select(s => new HashSet<string>(s, StringComparer.Ordinal)).ToList();
            var all = sets.SelectMany(s => s).Distinct().ToList();

            var groups = new List<Group>();
            foreach (string k in all)
            {
                Group target = null;
                foreach (var g in groups)
                {
                    bool excl = true;
                    foreach (var m in Members(g))
                        if (sets.Any(s => s.Contains(k) && s.Contains(m))) { excl = false; break; }
                    if (excl) { target = g; break; }
                }
                if (target == null) groups.Add(new Group { Name = k });
                else target.Alternatives.Add(k);
            }
            return groups;
        }

        private static IEnumerable<string> Members(Group g)
        {
            yield return g.Name;
            foreach (var a in g.Alternatives) yield return a;
        }

        public static string Pragma(Group g) =>
            "#pragma multi_compile __ " + string.Join(" ", Members(g));

        /// <summary>
        /// Which pragma a group should get, given the variants the build shipped.
        ///
        /// <c>multi_compile</c> and <c>shader_feature</c> are indistinguishable in the
        /// serialized data but differ in what a build keeps: the former keeps every
        /// variant it enumerates, the latter only those some material selected.  A group
        /// whose <em>off</em> state never appears is the fingerprint of
        /// <c>shader_feature</c>, and emitting <c>multi_compile</c> there explodes a
        /// rebuilt shader into variants the original never had.  The reverse proves
        /// nothing, so it stays <c>multi_compile</c>.
        /// </summary>
        public static string Pragma(Group g, List<HashSet<string>> sets)
        {
            if (sets == null || sets.Count == 0) return Pragma(g);
            // `shader_feature` only when the group is on in *every* compiled variant
            // and has no alternative to switch to: a static keyword. Its off state
            // was stripped, which is exactly what a material-driven feature leaves
            // behind.  Removing it from the pragma would change behaviour, so it is
            // kept as a single-value line instead.
            bool alwaysOn = sets.All(s => Members(g).Any(s.Contains));
            if (alwaysOn && g.Alternatives.Count == 0)
                return "#pragma shader_feature " + g.Name;
            if (alwaysOn)
                return "#pragma shader_feature __ " + string.Join(" ", Members(g));
            return Pragma(g);
        }

        /// <summary>
        /// Exact guard for one variant: every group either off (none of its
        /// alternatives defined) or on with the one alternative it was built
        /// with.  A <c>multi_compile</c> line only ever defines a single value,
        /// so the on-branch names that value instead of OR-ing the whole group.
        /// </summary>
        public static string Guard(List<Group> groups, ICollection<string> keywords)
        {
            var terms = new List<string>();
            foreach (var g in groups)
            {
                var on = Members(g).Where(keywords.Contains).Select(k => $"defined({k})").ToList();
                if (on.Count == 0)
                    terms.Add(string.Join(" && ", Members(g).Select(k => $"!defined({k})")));
                else if (on.Count == 1) terms.Add(on[0]);
                else terms.Add("(" + string.Join(" || ", on) + ")");
            }
            return terms.Count == 0 ? "" : string.Join(" && ", terms);
        }

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

        /// <summary>
        /// One character per group: which alternative of it is defined, or
        /// <c>-</c> for none.  Keeping the alternative (instead of a plain on/off
        /// bit) is what makes the signature unambiguous -- MODE_ADD and MODE_MUL
        /// live in the same group but are different shaders.
        /// </summary>
        private static string Signature(List<Group> groups, HashSet<string> kw)
        {
            var sb = new System.Text.StringBuilder(groups.Count);
            foreach (var g in groups)
            {
                int idx = 0, n = 0;
                foreach (var m in Members(g))
                {
                    n++;
                    if (kw.Contains(m)) idx = n;
                }
                sb.Append(idx == 0 ? '-' : idx <= 9 ? (char)('0' + idx) : (char)('a' + idx - 10));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Compare the compiled variant set against the full cartesian product of
        /// the groups.  A <c>multi_compile</c> line enumerates every one of its
        /// alternatives, so a full match means the recovered line split is right;
        /// missing combinations mean the build stripped variants
        /// (<c>shader_feature</c> / unused) -- and since the serialized blob only
        /// contains what the game can actually select, the emitted guard chain is
        /// already the complete runtime truth.
        /// </summary>
        public static Analysis Analyze(List<Group> groups, List<HashSet<string>> sets)
        {
            var a = new Analysis { Groups = groups.Count };
            var present = new HashSet<string>(sets.Select(s => Signature(groups, s)));
            a.Present = present.Count;
            a.Expected = 1;
            foreach (var g in groups) a.Expected *= Members(g).Count() + 1;
            a.Missing = a.Expected - a.Present;

            foreach (var g in groups)
            {
                bool anyOff = sets.Any(s => !Members(g).Any(s.Contains));
                if (!anyOff) a.AlwaysOn.Add(g.Alternatives.Count == 0 ? g.Name
                    : g.Name + " (+" + g.Alternatives.Count + ")");
            }

            if (groups.Count == 0)
            {
                a.Notes.Add("no keywords: single variant");
            }
            else if (a.FullCartesian)
            {
                a.Notes.Add($"full cartesian coverage ({a.Present}/{a.Expected}): "
                            + "every combination of the recovered multi_compile lines was compiled");
            }
            else
            {
                a.Notes.Add($"partial coverage ({a.Present}/{a.Expected}): {a.Missing} combination(s) "
                            + "absent from the build = stripped variants (shader_feature or unused); "
                            + "runtime can never select them, guard chain is complete as-is");
            }
            if (a.AlwaysOn.Count > 0)
                a.Notes.Add("line(s) never switched off: " + string.Join(", ", a.AlwaysOn)
                            + " (no variant has them all off, so the empty state of that "
                            + "pragma is unreachable but harmless)");
            return a;
        }
    }
}
