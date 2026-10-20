using System.Globalization;
using System.Resources;

namespace ShaderRipper
{
    /// <summary>
    /// 本地化字符串访问点。
    ///
    /// 默认语言 = 系统 UI 语言（Strings.resx 为英文兜底，Strings.zh-Hans.resx 为中文），
    /// `--lang en|zh` 可强制覆盖。资源缺失时返回键名本身，调用处不至于崩。
    /// </summary>
    internal static class Strings
    {
        private static readonly ResourceManager RM = new("ShaderRipper.Strings", typeof(Strings).Assembly);

        /// <summary>当前 UI 语言；Main 里可被 --lang 覆盖。</summary>
        public static CultureInfo Culture = CultureInfo.CurrentUICulture;

        public static string Get(string name) => RM.GetString(name, Culture) ?? name;

        public static string F(string name, params object[] args) =>
            string.Format(Culture, Get(name), args);
    }
}
