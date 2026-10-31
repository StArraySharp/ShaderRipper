# ShaderRipper.Gui

Desktop front end over `src/ShaderRipper` — a Photino window drawing a Material You
(Material 3) interface, with the same export engine the command line uses.

**Not part of `ShaderRipper.slnx`, and `src/ShaderRipper.Gui/` is git-ignored.**
It is a local convenience build: `dotnet build ShaderRipper.slnx` does not touch it,
and nothing in the shipped tool depends on it.

## Build and run

```bash
dotnet build src/ShaderRipper.Gui/ShaderRipper.Gui.csproj -c Release
src/ShaderRipper.Gui/bin/Release/net10.0/ShaderRipper.Gui.exe
```

Single file, self-contained:

```bash
dotnet publish src/ShaderRipper.Gui/ShaderRipper.Gui.csproj -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=true -o publish/gui-win-x64
mv publish/gui-win-x64/ShaderRipper.Gui.exe publish/gui-win-x64/ShaderRipperGui.exe
```

`IncludeNativeLibrariesForSelfExtract` matters: Photino's own `Photino.Native.dll` and
`WebView2Loader.dll` are native, and without it they stay next to the exe.

**Do not trim this one.** A trimmed publish looks fine — 72 MB instead of 131 MB, zero
`IL` warnings, and it can even complete a full 425-shader export — but it dies on
launch with `0xC0000374` (STATUS_HEAP_CORRUPTION) about two runs in three, because
Photino's native side holds callbacks the trimmer could not see. The untrimmed build
was stable over repeated launches; the trimmed one was not. If the size ever has to
come down, publish framework-dependent instead:

```bash
dotnet publish src/ShaderRipper.Gui/ShaderRipper.Gui.csproj -c Release -r win-x64   --self-contained false -p:PublishSingleFile=true -o publish/gui-fx
```

That trades the runtime for one shared DLL set (so it needs the .NET 10 desktop
runtime installed) and leaves the CLR untrimmed.

Requires the WebView2 runtime, which ships with Windows 11 and with Edge on Windows 10.

## Command line

```
ShaderRipperGui.exe [--game <dir>] [--out <dir>] [--start]
```

A bare folder is taken as `--game`, so a directory dropped on the exe works. Without
`--start` the window opens on those values and waits for the button. An unquoted path
with spaces is accepted: the pieces are glued back together while the result is an
existing directory.

## What it drives

`GameExport.Run(...)` — the exact call the CLI makes, so the two front ends cannot
drift. The bridge is one JSON object per message: the page sends `{cmd:...}`, the host
answers `{ev:...}`.

```
page → host   hello | pick | start | cancel | reveal | saveLog
host → page   hello | autostart | picked | started | log | done | failed
```

Live progress comes from the library's log sink (`Log.Sink`, exposed to this assembly
through `InternalsVisibleTo`), which carries the same `done/total` counter the verbose
console line prints. `cancel` reaches the export through the `CancellationToken` on
`Run`, so a stop takes effect between shaders — every file on disk is a whole shader,
and a cancelled run writes no export marker.

## Interface

- Palette: one fixed blue hue (264) is turned into a full Material 3 tonal scheme in
  OKLCH and written onto `:root` as custom properties. There is no colour picker — the
  only choice is light / dark / follow system, remembered in `localStorage` together
  with the last used folders and options.
- Layout: two panes — settings on the left, run state and the live log on the right.
  The grid needs 770px of content (340 + 380 + gap + padding); below that the panes
  stack as a flex column with natural heights, and the window's own minimum width is
  820. The breakpoint is that measured number on purpose: a round 1000 fired while
  both columns still fit, and stacking them into one grid row let the run panel paint
  over the settings.
- The log pane caps itself at 4000 lines and keeps the tail when "save" is used.

## Files

| File | Role |
|---|---|
| `Program.cs` | window, message dispatch, export driver, folder dialogs |
| `Ui.cs` | stitches the three `ui/` files into the document the web view loads |
| `ui/index.html` | structure |
| `ui/app.css` | Material 3 tokens and components |
| `ui/app.js` | palette generation, state, bridge, log rendering |
| `app.manifest` | per-monitor DPI awareness (the layout is measured in CSS pixels) |
