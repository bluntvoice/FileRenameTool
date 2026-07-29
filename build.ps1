$ErrorActionPreference = 'Stop'

$version = 'v2.3'
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

$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.GenerateInMemory = $false
$parameters.OutputAssembly = $outputPath
$parameters.CompilerOptions = '/target:winexe /optimize+ /win32icon:"{0}"' -f $iconPath
[void]$parameters.ReferencedAssemblies.Add('System.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Core.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Drawing.dll')
[void]$parameters.ReferencedAssemblies.Add('System.IO.Compression.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Windows.Forms.dll')

$result = $provider.CompileAssemblyFromSource($parameters, $source)
$provider.Dispose()
if ($result.Errors.HasErrors) {
    $messages = $result.Errors | ForEach-Object {
        '{0}({1},{2}): {3}' -f $_.FileName, $_.Line, $_.Column, $_.ErrorText
    }
    throw ($messages -join [Environment]::NewLine)
}

Write-Host "Build completed: $outputPath"
