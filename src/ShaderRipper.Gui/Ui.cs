namespace ShaderRipper.Gui;

/// <summary>
/// The one HTML document the window loads.
///
/// The three files are embedded in this assembly and stitched together here rather
/// than served from disk: a single-file publish has no asset directory to point a
/// <c>&lt;link&gt;</c> at, and a custom scheme handler would be one more thing that
/// can differ between platforms.  The markers are CSS/JS comments, so the document
/// is also valid if it is ever opened straight from <c>ui/index.html</c>.
/// </summary>
internal static class Ui
{
    private const string StyleMarker = "/*__STYLE__*/";
    private const string ScriptMarker = "/*__SCRIPT__*/";

    public static string Document()
    {
        string html = Read("index.html");
        html = html.Replace(StyleMarker, Read("app.css"), StringComparison.Ordinal);
        html = html.Replace(ScriptMarker, Read("app.js"), StringComparison.Ordinal);
        return html;
    }

    private static string Read(string name)
    {
        var asm = typeof(Ui).Assembly;
        using var stream = asm.GetManifestResourceStream("ShaderRipper.Gui.ui." + name);
        if (stream == null) return $"/* missing resource: {name} */";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
