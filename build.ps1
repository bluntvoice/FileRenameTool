$ErrorActionPreference = 'Stop'

$version = 'v2.6'
$sourcePath = Join-Path $PSScriptRoot 'FileRenameTool.cs'
$iconPath = Join-Path $PSScriptRoot 'assets\brush.ico'
$readmePath = Join-Path $PSScriptRoot 'README.md'
$outputPath = Join-Path $PSScriptRoot ("FileRenameTool-{0}.exe" -f $version)

if (-not $readmePath) {
    throw 'Readme file is missing.'
}
if (-not (Test-Path -LiteralPath $iconPath)) {
    throw 'Application icon is missing. Run assets\generate_icon.ps1 first.'
}

$readme = Get-Content -LiteralPath $readmePath -Raw -Encoding UTF8
if ($readme -notmatch [Regex]::Escape("### $version")) {
    throw "Version history for $version is missing from the readme."
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Force
}

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$cscPath = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $cscPath) {
    throw 'Microsoft .NET Framework C# compiler was not found.'
}

$compilerArguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    "/win32icon:$iconPath",
    "/out:$outputPath",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.IO.Compression.dll',
    '/reference:System.Windows.Forms.dll',
    $sourcePath
)
& $cscPath @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Build completed: $outputPath"
