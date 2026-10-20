using System.Globalization;

namespace ShaderRipper
{
    /// <summary>
    /// 本地化字符串访问点。
    ///
    /// 文案编译进 StringsData.cs（由 Localization/strings.*.txt 生成：
    /// 英文兜底 + 中文）。不用 resx 卫星程序集 —— NativeAOT 不支持卫星
    /// 程序集，编译期字典在 AOT/单文件/Linux 下行为完全一致。
    /// `--lang en|zh` 可强制覆盖，默认跟随系统 UI 语言；
    /// 缺失的键回退英文，再缺失返回键名本身。
    /// </summary>
    internal static class Strings
    {
        private static bool _zhWanted = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";

        /// <summary>当前 UI 语言；Main 里可被 --lang 覆盖。</summary>
        public static CultureInfo Culture = CultureInfo.CurrentUICulture;

        public static string Get(string name)
        {
            if (_zhWanted && StringsData.Zh.TryGetValue(name, out var zh)) return zh;
            if (StringsData.En.TryGetValue(name, out var en)) return en;
            return StringsData.Zh.TryGetValue(name, out var z) ? z : name;
        }

        public static string F(string name, params object[] args) =>
            string.Format(Culture, Get(name), args);

        /// <summary>--lang 值 → 是否中文；无法识别时保持现状。</summary>
        public static void SetLang(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return;
            string l = lang.Trim().ToLowerInvariant();
            if (l is "zh" or "zh-hans" or "zh-cn" or "chinese") _zhWanted = true;
            else if (l is "en" or "english") _zhWanted = false;
        }
    }
}
