<#
.SYNOPSIS
  Check a published subsretimer.exe with MSYS2 removed from PATH: the bundled GTK layout
  is complete, --version answers, GTK loads from the bundle, and --auto re-times a pair
  of fixture files.

.DESCRIPTION
  Checks:
    1. the files the GTK bundle must contain are all there (DLLs, compiled schemas,
       pixbuf loaders.cache, the Adwaita theme and this application's own icon),
    2. "subsretimer.exe --version" exits 0 and prints a version,
    3. "subsretimer.exe --check-editor" exits 0: GTK loads from the bundle (every DLL
       libgtk-4-1.dll needs has to resolve), a display opens, and gtk_init() runs, which
       reads the bundled GSettings schemas and aborts without them,
    4. "subsretimer.exe --auto --print-output REF TARGET" on two fixture files this
       script writes itself exits 0, prints the saved path on stdout and nothing else,
       and the saved file carries the reference's timings with the target's text.

  The editor window itself is deliberately not opened. subsretimer is a console
  executable (its stdout is part of the contract with subs2srs), so a started process's
  MainWindowHandle is its console window, not the GTK window, and a window check built
  on that would pass with no GTK at all. --check-editor goes as far as a window would
  without one; the window, the icon and the editor's keys and mouse clicks stay a manual
  check on a Windows machine.

  Must run on Windows PowerShell 5.1 as well as 7: no &&, ||, ?: or ?? here, and the
  executable is started through Start-Process with its output redirected, because in 5.1
  a native command's redirected stderr becomes a terminating error under
  ErrorActionPreference=Stop and subsretimer writes its progress to stderr.

  Adapted from subs2srs's dist/windows/smoke.ps1, whose app is a windowed one and is
  therefore checked by waiting for its window instead.

.PARAMETER PublishDir
  Directory containing subsretimer.exe with the GTK runtime bundled.

.PARAMETER KeepWorkDir
  Keep the temporary directory holding the fixtures and the re-timed output.

.EXAMPLE
  pwsh dist/windows/smoke.ps1 -PublishDir out\win-x64
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [switch]$KeepWorkDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$PublishDir = (Resolve-Path $PublishDir).Path
$exe = Join-Path $PublishDir 'subsretimer.exe'
if (-not (Test-Path $exe)) { throw "not found: $exe" }

# Make sure we are NOT picking up a developer's MSYS2 from PATH.
$env:PATH = ($env:PATH -split ';' | Where-Object { $_ -notmatch 'msys64' }) -join ';'
$env:GSK_RENDERER = 'cairo'

# Run the executable and return its exit code with stdout and stderr kept apart: the
# stdout-carries-only-saved-paths contract is one of the things under test. Arguments
# that are paths must arrive quoted, %TEMP% can contain spaces.
function Invoke-Exe([string[]]$arguments) {
    $outFile = [IO.Path]::GetTempFileName()
    $errFile = [IO.Path]::GetTempFileName()
    try {
        $p = Start-Process -FilePath $exe -ArgumentList $arguments -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $outFile -RedirectStandardError $errFile
        return [pscustomobject]@{
            ExitCode = $p.ExitCode
            StdOut   = @(Get-Content $outFile)
            StdErr   = @(Get-Content $errFile)
        }
    }
    finally { Remove-Item $outFile, $errFile -Force -ErrorAction SilentlyContinue }
}

function Quote([string]$path) { return '"' + $path + '"' }

# ── 1. The bundle is complete ─────────────────────────────────────────────────
$required = @(
    'libgtk-4-1.dll', 'libglib-2.0-0.dll', 'libgobject-2.0-0.dll', 'libgdk_pixbuf-2.0-0.dll',
    'libcairo-2.dll', 'libpango-1.0-0.dll',
    'share\glib-2.0\schemas\gschemas.compiled',
    'share\icons\Adwaita\index.theme',
    'share\icons\hicolor\48x48\apps\subsretimer.png',
    'lib\gdk-pixbuf-2.0\2.10.0\loaders.cache'
)
$absent = @($required | Where-Object { -not (Test-Path (Join-Path $PublishDir $_)) })
if ($absent.Count -gt 0) { throw "the GTK bundle is incomplete, missing: $($absent -join ', ')" }
Write-Host ("bundle layout   : ok, all {0} required entries present" -f $required.Count)

# ── 2. --version ──────────────────────────────────────────────────────────────
$r = Invoke-Exe @('--version')
if ($r.ExitCode -ne 0) { throw "--version exited $($r.ExitCode): $($r.StdErr -join ' ')" }
$version = @($r.StdOut | Where-Object { $_ -match '\S' })
if ($version.Count -eq 0) { throw '--version printed nothing on stdout' }
Write-Host ("version         : {0}" -f $version[0])

# ── 3. GTK loads from the bundle ──────────────────────────────────────────────
# A missing or mismatched DLL fails the load; missing schemas abort gtk_init().
$r = Invoke-Exe @('--check-editor')
foreach ($line in $r.StdErr) { Write-Host "    $line" }
if ($r.ExitCode -ne 0) { throw "--check-editor exited $($r.ExitCode): GTK does not start from the bundle" }
if (@($r.StdOut | Where-Object { $_ -match '\S' }).Count -ne 0) { throw '--check-editor printed on stdout' }
Write-Host 'gtk             : ok, loads and initialises from the bundle'

# ── 4. --auto on two fixture files ────────────────────────────────────────────
# Six lines of dialogue; the target is the same dialogue exactly 5 s late, so auto-align
# must find one -5 s segment and the saved file must carry the reference's timings.
$work = Join-Path ([IO.Path]::GetTempPath()) ('subsretimer-smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $work | Out-Null

$referenceSrt = @'
1
00:00:05,000 --> 00:00:07,000
Reference one

2
00:00:12,500 --> 00:00:14,000
Reference two

3
00:00:20,000 --> 00:00:22,400
Reference three

4
00:00:31,250 --> 00:00:33,000
Reference four

5
00:00:44,000 --> 00:00:46,750
Reference five

6
00:00:58,500 --> 00:01:00,000
Reference six
'@

$targetSrt = @'
1
00:00:10,000 --> 00:00:12,000
Target one

2
00:00:17,500 --> 00:00:19,000
Target two

3
00:00:25,000 --> 00:00:27,400
Target three

4
00:00:36,250 --> 00:00:38,000
Target four

5
00:00:49,000 --> 00:00:51,750
Target five

6
00:01:03,500 --> 00:01:05,000
Target six
'@

$referencePath = Join-Path $work 'reference.srt'
$targetPath = Join-Path $work 'target.srt'
$expected = Join-Path $work 'target_retimed.srt'
Set-Content -Path $referencePath -Value $referenceSrt -Encoding ascii
Set-Content -Path $targetPath -Value $targetSrt -Encoding ascii

try {
    $r = Invoke-Exe @('--auto', '--print-output', (Quote $referencePath), (Quote $targetPath))
    foreach ($line in $r.StdErr) { Write-Host "    $line" }
    if ($r.ExitCode -ne 0) { throw "--auto exited $($r.ExitCode), expected 0" }
    if (-not (Test-Path $expected)) { throw "--auto wrote no $expected" }

    $printed = @($r.StdOut | Where-Object { $_ -match '\S' })
    if ($printed.Count -ne 1) { throw "stdout carried $($printed.Count) lines, expected only the saved path" }
    if ($printed[0] -ne $expected) { throw "stdout printed '$($printed[0])', expected '$expected'" }

    # The first cue must now sit where the reference's first cue sits ...
    $saved = Get-Content $expected -Raw
    if ($saved -notmatch '00:00:05,000 --> 00:00:07,000') {
        throw "the re-timed file was not shifted onto the reference:`n$saved"
    }
    # ... and the text must still be the target's.
    if ($saved -notmatch 'Target one') { throw 'the re-timed file lost the target text' }
    Write-Host ("auto-align      : ok, wrote {0}" -f $expected)
}
finally {
    if ($KeepWorkDir) { Write-Host ("work dir        : {0}" -f $work) }
    else { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host 'Smoke test passed.' -ForegroundColor Green
