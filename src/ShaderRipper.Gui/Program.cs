using Photino.NET;
using ShaderRipper.Game;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShaderRipper.Gui;

/// <summary>
/// Window and message pump.
///
/// The interface is HTML/CSS/JS drawn by the platform web view: the export work
/// itself is the same <see cref="GameExport.Run"/> the command line calls, so the
/// two front ends cannot drift.  Communication is one JSON object per message in
/// either direction -- the browser asks with <c>cmd</c>, this side answers with
/// <c>ev</c>.
/// </summary>
internal static class Program
{
    private static PhotinoWindow _window;

    /// <summary>Set while a run is in flight; the window closing cancels it.</summary>
    private static CancellationTokenSource _cancel;

    private static volatile bool _running;

    /// <summary>Everything the log pane has shown, for "save log".</summary>
    private static readonly StringBuilder Transcript = new();

    /// <summary>
    /// Folders handed in on the command line, and whether to start immediately.
    ///
    /// <c>ShaderRipper.Gui.exe --game &lt;dir&gt; [--out &lt;dir&gt;] [--start]</c>: the same
    /// thing the two pickers do, minus the dialogs, so a folder can be dropped on
    /// the exe from Explorer.  The values are held until the page says hello, which
    /// is the only moment the document is guaranteed to be listening.
    /// </summary>
    private static string _startIn, _startOut;
    private static bool _startNow;

    [STAThread]
    private static void Main(string[] args)
    {
        // The console sink is silent here (no console is attached), but the level
        // still filters what reaches the sink, so keep it at Normal.
        Log.Level = LogLevel.Normal;
        Log.Sink = OnLog;

        _window = new PhotinoWindow()
            .SetTitle("ShaderRipper")
            .SetUseOsDefaultSize(false)
            .SetSize(1200, 830)
            // Narrow enough for the stacked layout to be reachable: the two-column
            // grid needs 772px of content, and anything under that is single column.
            .SetMinSize(820, 620)
            .SetDevToolsEnabled(false)
            .Center()
            .RegisterWebMessageReceivedHandler(OnMessage)
            .RegisterWindowClosingHandler((_, _) =>
            {
                // Stopping the workers is the difference between closing the window
                // and waiting 20 seconds for an export nobody is watching.
                try { _cancel?.Cancel(); } catch { }
                return false;
            });

        ParseArgs(args);
        _window.LoadRawString(Ui.Document());
        _window.WaitForClose();
    }

    // ---------------------------------------------------------------- inbound

    private static void OnMessage(object sender, string message)
    {
        JsonObject node;
        try { node = JsonNode.Parse(message) as JsonObject; }
        catch { return; }
        if (node == null) return;

        try
        {
            switch (Text(node, "cmd"))
            {
                case "hello":
                    Send(new JsonObject
                    {
                        ["ev"] = "hello",
                        ["jobs"] = Environment.ProcessorCount,
                        ["version"] = typeof(GameExport).Assembly.GetName().Version?.ToString(),
                    });
                    if (_startIn != null || _startOut != null)
                    {
                        Send(new JsonObject
                        {
                            ["ev"] = "autostart",
                            ["dataDir"] = _startIn ?? "",
                            ["outDir"] = _startOut ?? "",
                            ["start"] = _startNow,
                        });
                        _startIn = _startOut = null;
                    }
                    break;

                case "pick":
                    Pick(node);
                    break;

                case "start":
                    Begin(node);
                    break;

                case "cancel":
                    _cancel?.Cancel();
                    break;

                case "reveal":
                    Reveal(Text(node, "path"));
                    break;

                case "saveLog":
                    SaveLog();
                    break;
            }
        }
        catch (Exception e)
        {
            Send(new JsonObject { ["ev"] = "failed", ["text"] = e.Message });
        }
    }

    private static void Pick(JsonObject node)
    {
        bool input = Text(node, "kind") != "out";
        // The dialog starts where the field already points, which is the one thing
        // that makes picking a second directory pleasant.
        string start = Text(node, "path");
        string[] picked = _window.ShowOpenFolder(
            input ? "选择游戏数据目录" : "选择输出目录",
            string.IsNullOrWhiteSpace(start) ? null : start, false);
        if (picked == null || picked.Length == 0) return;
        Send(new JsonObject { ["ev"] = "picked", ["kind"] = input ? "in" : "out", ["path"] = picked[0] });
    }

    private static void Begin(JsonObject node)
    {
        if (_running)
        {
            Send(new JsonObject { ["ev"] = "failed", ["text"] = "上一次导出还没结束。" });
            return;
        }

        string dataDir = Text(node, "dataDir");
        string outDir = Text(node, "outDir");
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            Send(new JsonObject { ["ev"] = "failed", ["text"] = "请先选择游戏数据目录。" });
            return;
        }
        if (string.IsNullOrWhiteSpace(outDir))
        {
            Send(new JsonObject { ["ev"] = "failed", ["text"] = "请先选择输出目录。" });
            return;
        }

        var o = new Job
        {
            DataDir = dataDir,
            OutDir = outDir,
            Only = string.IsNullOrWhiteSpace(Text(node, "only")) ? null : Text(node, "only").Trim(),
            ShaderLab = Bool(node, "shaderlab"),
            Programs = Bool(node, "programs"),
            OrigAsm = Bool(node, "origAsm"),
            Overwrite = Bool(node, "overwrite"),
            Dedup = Bool(node, "dedup", true),
            Builtin = Bool(node, "builtin"),
            Jobs = Math.Clamp(Int(node, "jobs", Environment.ProcessorCount), 1, 64),
        };

        // None of the three switches means nothing is written: refuse rather than
        // spend a minute translating into nothing (the CLI refuses the same way).
        if (!o.ShaderLab && !o.Programs && !o.OrigAsm)
        {
            Send(new JsonObject { ["ev"] = "failed", ["text"] = "至少选择一种产物（.shader / 程序 HLSL / 反汇编）。" });
            return;
        }

        _running = true;
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        lock (Transcript) Transcript.Clear();

        Send(new JsonObject { ["ev"] = "started", ["out"] = o.OutDir });

        Task.Run(() =>
        {
            try
            {
                var s = GameExport.Run(o.DataDir, o.OutDir, o.Only, o.ShaderLab, o.OrigAsm,
                                       o.Dedup, o.Builtin, o.Programs, null, o.Overwrite,
                                       o.Jobs, token);
                Send(new JsonObject
                {
                    ["ev"] = "done",
                    ["files"] = s.Files,
                    ["shaders"] = s.Shaders,
                    ["emitted"] = s.Emitted,
                    ["programs"] = s.Programs,
                    ["disassembled"] = s.Disassembled,
                    ["translated"] = s.Translated,
                    ["skipped"] = s.Skipped,
                    ["failed"] = s.Failed,
                    ["builtinSkipped"] = s.BuiltinSkipped,
                    ["cancelled"] = token.IsCancellationRequested,
                    ["notes"] = new JsonArray(s.Notes.Select(n => (JsonNode)JsonValue.Create(n)).ToArray()),
                    ["out"] = o.OutDir,
                });
            }
            catch (Exception e)
            {
                // `--overwrite` refusing a foreign directory arrives here: it is a
                // message for the user, not a crash.
                Send(new JsonObject { ["ev"] = "failed", ["text"] = e.Message, ["out"] = o.OutDir });
            }
            finally
            {
                _running = false;
            }
        });
    }

    /// <summary>
    /// Two flags and one positional folder, plus the unquoted-path repair below.
    ///
    /// A folder name with a space arrives as several arguments
    /// (<c>--game C:\Games\A Dance</c> is three), which is what a shell does when
    /// the path was not quoted.  Rather than reject it, the pieces are glued back
    /// together while the result is not an existing directory -- the repair cannot
    /// invent a path, it only stops a valid one from being cut in half.
    /// </summary>
    private static void ParseArgs(string[] args)
    {
        var loose = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game":
                case "--in":
                    if (i + 1 < args.Length) { _startIn = args[++i]; loose.Clear(); }
                    break;
                case "--out":
                    if (i + 1 < args.Length) _startOut = args[++i];
                    break;
                case "--start":
                    _startNow = true;
                    break;
                default:
                    // A bare path is the game folder, which is what dropping a folder
                    // on the exe produces.
                    if (!args[i].StartsWith("-", StringComparison.Ordinal))
                    {
                        if (_startIn == null) { _startIn = args[i]; loose.Clear(); }
                        else loose.Add(args[i]);
                    }
                    break;
            }
        }

        if (_startIn != null && loose.Count > 0 && !Directory.Exists(_startIn))
        {
            string joined = _startIn;
            foreach (string part in loose)
            {
                joined += " " + part;
                if (Directory.Exists(joined)) { _startIn = joined; break; }
            }
        }
    }

    private static void Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Directory.Exists(path) && !File.Exists(path)) return;
        try
        {
            // The folder itself, not a shell: this is what Explorer's address bar does.
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private static void SaveLog()
    {
        string text;
        lock (Transcript) text = Transcript.ToString();
        string path = _window.ShowSaveFile("保存日志", "shaderripper.log", new[]
        {
            ("日志", new[] { "*.log" }),
            ("文本", new[] { "*.txt" }),
            ("所有文件", new[] { "*.*" }),
        });
        if (string.IsNullOrEmpty(path)) return;
        try { File.WriteAllText(path, text, new UTF8Encoding(false)); }
        catch (Exception e)
        {
            Send(new JsonObject { ["ev"] = "failed", ["text"] = "写日志失败：" + e.Message });
        }
    }

    // --------------------------------------------------------------- outbound

    private static void OnLog(Log.Line line)
    {
        string text = line.Kind is "summary" or "note"
            ? line.Subject
            : line.Detail == null ? line.Subject : line.Subject + "  " + line.Detail;

        lock (Transcript)
        {
            Transcript.AppendLine(text);
            // A 400-shader run is ~4k lines; keep the tail so "save log" stays small
            // and the string builder does not grow without bound over a long session.
            if (Transcript.Length > 400_000) Transcript.Remove(0, 100_000);
        }

        Send(new JsonObject
        {
            ["ev"] = "log",
            ["kind"] = line.Kind,
            ["text"] = text,
            ["done"] = line.Done,
            ["total"] = line.Total,
        });
    }

    private static void Send(JsonObject payload)
    {
        if (_window == null) return;
        try { _window.SendWebMessage(payload.ToJsonString()); }
        catch
        {
            // The window can be gone between the check and the call; nothing to do.
        }
    }

    // ------------------------------------------------------------------ helpers

    private static string Text(JsonObject node, string key) =>
        node != null && node.TryGetPropertyValue(key, out var v) && v is JsonValue jv &&
        jv.TryGetValue<string>(out var s) ? s : "";

    private static bool Bool(JsonObject node, string key, bool fallback = false) =>
        node != null && node.TryGetPropertyValue(key, out var v) && v is JsonValue jv &&
        jv.TryGetValue<bool>(out var b) ? b : fallback;

    private static int Int(JsonObject node, string key, int fallback) =>
        node != null && node.TryGetPropertyValue(key, out var v) && v is JsonValue jv &&
        jv.TryGetValue<int>(out var i) ? i : fallback;

    private sealed class Job
    {
        public string DataDir, OutDir, Only;
        public bool ShaderLab, Programs, OrigAsm, Overwrite, Dedup, Builtin;
        public int Jobs;
    }
}
