<#
.SYNOPSIS
    Package ShaderRipper for release.

.DESCRIPTION
    Publishes a self-contained, multi-file build and zips it.  Nothing is trimmed and
    nothing is single-file, because both of those break plugin loading: a trimmed
    self-contained publish folds the runtime assemblies into native code, and a
    single-file publish hides them inside the exe, so a plugin loaded at run time has
    nothing to resolve its references against.

    Output, per platform:

        publish/<rid>/ShaderRipper.zip     the release artifact
        publish/<rid>/ShaderRipper.exe     (or `ShaderRipper`) beside it, plus the DLLs

    The zip is the deliverable; the unpacked tree is kept so the two can be compared
    and so a failed plugin run can be debugged without unzipping first.

.PARAMETER Rid
    One or more runtime identifiers.  Defaults to win-x64 and linux-x64.

.PARAMETER Target
    Which front end: 'cli' (default) or 'gui'.  The GUI is Windows-only.

.PARAMETER Configuration
    Build configuration, Release by default.

.EXAMPLE
    pwsh -File tools/make-release.ps1

.EXAMPLE
    pwsh -File tools/make-release.ps1 -Rid win-x64 -Target gui
#>
[CmdletBinding()]
param(
    [string[]] $Rid           = @('win-x64', 'linux-x64'),
    [ValidateSet('cli', 'gui')]
    [string]   $Target        = 'cli',
    [string]   $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $appProject = if ($Target -eq 'gui') {
        'src/ShaderRipper.Gui/ShaderRipper.Gui.csproj'
    } else {
        'src/ShaderRipper.Application/ShaderRipper.Application.csproj'
    }

    # The version comes from the project, not from an argument: a release that is
    # named 1.2.2 while its binaries say something else is worse than no name at all.
    [xml] $projectXml = Get-Content $appProject
    $version = ($projectXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
    if (-not $version) { throw "no <Version> in $appProject" }

    foreach ($oneRid in $Rid) {
        $onWindows = $oneRid -like 'win-*'
        if ($Target -eq 'gui' -and -not $onWindows) {
            Write-Host "skipping ${oneRid}: the GUI front end is Windows-only" -ForegroundColor Yellow
            continue
        }

        $out = Join-Path 'publish' $oneRid
        Write-Host "== $Target / $oneRid / $version ==" -ForegroundColor Cyan

        Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
        dotnet publish $appProject `
            -c $Configuration -r $oneRid --self-contained `
            -p:PublishSingleFile=false -p:PublishTrimmed=false `
            -o $out
        if ($LASTEXITCODE -ne 0) { throw "publish failed for $oneRid (exit $LASTEXITCODE)" }

        # Debugging aids, not part of the release: they are a third of the file count
        # and none of the runtime benefit.
        Get-ChildItem $out -Recurse -File -Include '*.pdb' | Remove-Item -Force

        $exeName = if ($Target -eq 'gui') { 'ShaderRipper.Gui' } else { 'ShaderRipper.Application' }
        if ($onWindows) { $exeName += '.exe' }
        if (-not (Test-Path (Join-Path $out $exeName))) {
            throw "publish did not produce $exeName for $oneRid"
        }

        # ShaderRipper-<target>-<version>-<rid>.zip: every release asset of every
        # platform and front end can sit in one download folder without colliding.
        $zipName = "ShaderRipper-$Target-$version-$oneRid.zip"
        $zip = Join-Path $out $zipName
        Remove-Item $zip -Force -ErrorAction SilentlyContinue

        # Zip from a sibling path: writing the archive inside the directory being
        # archived makes it try to include itself, and the file stays locked.
        $staging = Join-Path 'publish' (".$oneRid-zip")
        Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        Get-ChildItem $out -Force | Copy-Item -Destination $staging -Recurse
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            (Resolve-Path $staging).Path, (Join-Path (Resolve-Path $out).Path $zipName),
            [System.IO.Compression.CompressionLevel]::Optimal, $false)
        Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
        if ($LASTEXITCODE -ne 0) { throw "zip failed for $oneRid" }

        $zipMb  = [math]::Round((Get-Item $zip).Length / 1MB, 2)
        $treeMb = [math]::Round((Get-ChildItem $out -Recurse -File |
                                 Where-Object { $_.Name -ne $zipName } |
                                 Measure-Object Length -Sum).Sum / 1MB, 2)
        "    {0,-40} {1,8:N2} MB  ({2:N2} MB unpacked)" -f $zipName, $zipMb, $treeMb
        "    entry: $exeName"
    }
}
finally {
    Pop-Location
}
