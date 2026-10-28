using System;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace ShaderRipper.Localization
{
    /// <summary>
    /// Interface language, resolved from <c>--lang</c> or the system UI culture.
    ///
    /// The strings live in <c>Localization/Strings.resx</c> (English, the default)
    /// and <c>Strings.zh-Hans.resx</c>; the build turns them into satellite
    /// assemblies, so a new language is a new .resx and no code change.
    ///
    /// A missing key returns the key itself rather than throwing, so a translation
    /// gap cannot take down an export that is otherwise fine.
    /// </summary>
    internal static class Strings
    {
        private static readonly ResourceManager Rm = new(
            "ShaderRipper.Localization.Strings",
            typeof(Strings).Assembly);

        /// <summary>The culture in effect.  Defaults to the system UI culture.</summary>
        public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

        /// <summary>
        /// Set the interface language from a command-line value, accepting the tags
        /// people actually type (<c>en</c>, <c>zh</c>, <c>zh-CN</c>,
        /// <c>zh-Hans</c>).  An unknown tag leaves the current culture alone rather
        /// than throwing, so a typo in <c>--lang</c> does not stop an export.
        /// </summary>
        public static void SetLanguage(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            string t = tag.Trim().Replace('_', '-');

            // "zh" and "zh-CN" both mean Simplified here.  The resources are keyed
            // zh-Hans, and while the resource manager does walk up the parent
            // chain, a two-letter tag resolves to the neutral "zh" culture whose
            // parent is Invariant -- so the chain never reaches zh-Hans.  Mapping
            // the aliases explicitly is what makes --lang zh work.
            if (t.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("zh-cn", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("zh-sg", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("zh-hans", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("cn", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("chs", StringComparison.OrdinalIgnoreCase))
                t = "zh-Hans";

            CultureInfo culture;
            try { culture = CultureInfo.GetCultureInfo(t); }
            catch (CultureNotFoundException) { return; }

            Culture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        /// <summary>
        /// Language names this build can display, for <c>--help</c> and diagnostics.
        /// </summary>
        public static string[] Available => new[] { "en", "zh-Hans" };

        /// <summary>Look up one string by key; the key itself when it is missing.</summary>
        public static string Get(string key) => Get(key, null);

        /// <summary>Look up one string and format it with <paramref name="args"/>.</summary>
        public static string Get(string key, params object[] args)
        {
            string text;
            try { text = Rm.GetString(key, Culture); }
            catch (MissingManifestResourceException) { text = null; }
            catch (InvalidOperationException) { text = null; }

            if (string.IsNullOrEmpty(text)) return key;
            if (args == null || args.Length == 0) return text;
            try { return string.Format(text, args); }
            catch (FormatException)
            {
                // A translation with a wrong placeholder count must not crash the
                // tool; the unformatted text still names the problem.
                return text;
            }
        }

        /// <summary>Shorthand for <see cref="Get(string, object[])"/>.</summary>
        public static string F(string key, params object[] args) => Get(key, args);
    }
}
