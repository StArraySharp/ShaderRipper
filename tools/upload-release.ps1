# Upload the release zips to the GitHub release for a tag, and mark it latest.
#
# The release itself is normally created by the CI workflow, but the assets the
# workflow uploads are its own artifacts; this uploads the zips built locally so a
# tag points at a downloadable file immediately.  A release that already exists is
# reused, an asset that already exists is replaced.
#
# The token comes from git's credential helper, so it never has to be pasted into a
# shell or written to a file:
#
#     pwsh -File tools/upload-release.ps1 -Tag v1.2.2
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Tag,
    [string] $Owner = 'StArraySharp',
    [string] $Repo  = 'ShaderRipper',
    [string] $Notes = ''
)

$ErrorActionPreference = 'Stop'

function Get-GitHubToken {
    # `git credential fill` prints protocol/host/username/password; only the last is
    # needed, and it stays in memory.
    $input = "protocol=https`nhost=github.com`n`n"
    $lines = $input | git credential fill 2>$null
    foreach ($line in $lines) {
        if ($line -match '^password=(.+)$') { return $Matches[1] }
    }
    throw 'no stored GitHub credential: run `git push` once so the helper can cache one'
}

$headers = @{
    Authorization          = "Bearer $(Get-GitHubToken)"
    Accept                 = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent'           = 'ShaderRipper-release'
}
$api = "https://api.github.com/repos/$Owner/$Repo"

# Reuse the release the workflow created, or make one.
$release = $null
try {
    $release = Invoke-RestMethod -Uri "$api/releases/tags/$Tag" -Headers $headers -Method Get
    Write-Host "release $Tag exists (id $($release.id))"
} catch {
    Write-Host "release $Tag not found; creating it"
    $body = @{
        tag_name         = $Tag
        name             = $Tag
        body             = $Notes
        draft            = $false
        prerelease       = $false
        make_latest      = 'true'
    } | ConvertTo-Json
    $release = Invoke-RestMethod -Uri "$api/releases" -Headers $headers -Method Post `
                                 -Body $body -ContentType 'application/json'
    Write-Host "created release $Tag (id $($release.id))"
}

# Replace an asset of the same name rather than failing: a re-run is then harmless.
foreach ($zip in Get-ChildItem -Path (Join-Path $PSScriptRoot '..\publish') -Recurse -Filter '*.zip') {
    $name = $zip.Name
    $existing = $release.assets | Where-Object { $_.name -eq $name }
    if ($existing) {
        Write-Host "replacing existing asset $name"
        Invoke-RestMethod -Uri "$api/releases/assets/$($existing.id)" -Headers $headers -Method Delete | Out-Null
    }

    $size = [math]::Round($zip.Length / 1MB, 1)
    Write-Host "uploading $name ($size MB)"
    $uploadHeaders = $headers.Clone()
    $uploadHeaders['Content-Type'] = 'application/zip'
    Invoke-RestMethod -Uri "https://uploads.github.com/repos/$Owner/$Repo/releases/$($release.id)/assets?name=$name" `
                      -Headers $uploadHeaders -Method Post -InFile $zip.FullName | Out-Null
}

# Mark it the latest even when the release already existed.
$patch = @{ make_latest = 'true' } | ConvertTo-Json
Invoke-RestMethod -Uri "$api/releases/$($release.id)" -Headers $headers -Method Patch `
                  -Body $patch -ContentType 'application/json' | Out-Null
Write-Host "release $Tag is latest"