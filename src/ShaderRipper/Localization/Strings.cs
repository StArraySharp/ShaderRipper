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
    /// Two properties the tool depends on:
    ///   * a missing key returns the key itself, never an exception — a translation
    ///     gap must not take down an export that is otherwise fine;
    ///   * <c>--lang zh</c> works on a machine whose satellite assembly was stripped,
    ///     because the fallback is the neutral (English) resource.
    /// </summary>
    internal static class Strings
    {
        private static readonly ResourceManager Rm = new(
            "ShaderRipper.Localization.Strings",
            typeof(Strings).Assembly);

        /// <summary>The culture in effect.  Defaults to the system UI culture.</summary>
        public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

        /// <summary>
        /// Set the interface language from a command-line value.
        ///
        /// Accepts the tags people actually type (<c>en</c>, <c>zh</c>, <c>zh-CN</c>,
        /// <c>zh-Hans</c>) and maps them onto the cultures this build ships.  An
        /// unknown tag leaves the current culture alone rather than throwing: a
        /// typo in <c>--lang</c> should not stop an export.
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
