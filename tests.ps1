$ErrorActionPreference = 'Stop'

$exePath = Join-Path $PSScriptRoot 'FileRenameTool-v2.6.exe'
if (-not (Test-Path -LiteralPath $exePath)) {
    throw 'FileRenameTool-v2.6.exe is missing. Run build.ps1 first.'
}

$assembly = [Reflection.Assembly]::LoadFrom($exePath)
$formType = $assembly.GetType('FileRenameTool.MainForm', $true)
$previewType = $assembly.GetType('FileRenameTool.RenamePreview', $true)
$binding = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$staticBinding = [Reflection.BindingFlags]'Static,NonPublic,Public'
$form = [Activator]::CreateInstance($formType, $true)
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('FileRenameTool-tests-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tempRoot) | Out-Null
$today = [DateTime]::Now.ToString('yyyyMMdd')
$passed = 0

function Get-PrivateField([string]$name) {
    $field = $formType.GetField($name, $binding)
    if (-not $field) { throw "Field not found: $name" }
    return $field.GetValue($form)
}

function Assert-Equal([string]$label, $actual, $expected) {
    if ($actual -ne $expected) {
        throw "$label failed.`nExpected: $expected`nActual:   $actual"
    }
    $script:passed++
    Write-Host "PASS $label"
}

function Set-Options([bool]$date, [bool]$version, [bool]$versionType, [bool]$cleanup = $false) {
    (Get-PrivateField 'onlyKeepFileName').Checked = $cleanup
    if (-not $cleanup) {
        (Get-PrivateField 'includeDate').Checked = $date
        (Get-PrivateField 'includeVersion').Checked = $version
        (Get-PrivateField 'includeVersionType').Checked = $versionType
    }
    (Get-PrivateField 'companyPrefix').Text = 'XX'
    (Get-PrivateField 'versionType').Text = '清洁版'
}

function Get-PreviewName([string]$sourceName) {
    $sourcePath = Join-Path $tempRoot $sourceName
    [IO.File]::WriteAllBytes($sourcePath, [byte[]]@(1, 2, 3))
    $sourceFiles = $formType.GetField('sourceFiles', $binding).GetValue($form)
    $sourceFiles.Clear()
    $sourceFiles.Add($sourcePath)
    $method = $formType.GetMethod('BuildPreviews', $binding)
    $previews = $method.Invoke($form, $null)
    if ($previews.Count -ne 1) { throw "Expected one preview for $sourceName" }
    return [IO.Path]::GetFileName($previews[0].TargetPath)
}

try {
    Set-Options $true $true $true
    foreach ($extension in @('.docx', '.doc', '.pdf', '.PDF', '.xlsx', '.pptx', '.txt', '.rtf', '')) {
        $actual = Get-PreviewName ("合同-20260730-修订版$extension")
        Assert-Equal "preserve extension [$extension]" $actual ("合同-$today-v1.0-XX清洁版$extension")
    }

    $actual = Get-PreviewName '合同-20260730-v1.2-修订版.pdf'
    Assert-Equal 'increment full standard name' $actual "合同-$today-v1.3-XX清洁版.pdf"

    foreach ($sourceName in @(
        '合同-20260730修订.docx',
        '合同_20260730_修订版.docx',
        '合同（20260730）修订版.docx',
        '合同-2026-07-30-修订版.docx'
    )) {
        $actual = Get-PreviewName $sourceName
        Assert-Equal "update embedded date [$sourceName]" $actual "合同-$today-v1.0-XX清洁版.docx"
    }

    Set-Options $true $true $false
    $actual = Get-PreviewName '合同-20260730-v1.2-修订版.pdf'
    Assert-Equal 'omit version type and company prefix' $actual "合同-$today-v1.3.pdf"
    $actual = Get-PreviewName '合同-20260730-修订版-3.docx'
    Assert-Equal 'omit sequential version type' $actual "合同-$today-v1.0.docx"

    Set-Options $false $true $true
    $actual = Get-PreviewName '合同-20260730-修订版.pdf'
    Assert-Equal 'version and type without date' $actual '合同-v1.0-XX清洁版.pdf'

    Set-Options $true $false $true
    $actual = Get-PreviewName '合同-20260730-v1.2-修订版.pdf'
    Assert-Equal 'date and type without version' $actual "合同-$today-XX清洁版.pdf"

    Set-Options $false $false $false
    $actual = Get-PreviewName '合同-20260730-v1.2-修订版.pdf'
    Assert-Equal 'base name only through optional elements' $actual '合同.pdf'

    Set-Options $true $true $true $true
    $actual = Get-PreviewName '合同-20260730-v1.2-修订版（1）.pdf'
    Assert-Equal 'only keep file name has highest priority' $actual '合同.pdf'
    Assert-Equal 'cleanup disables date' (Get-PrivateField 'includeDate').Enabled $false
    Assert-Equal 'cleanup disables version' (Get-PrivateField 'includeVersion').Enabled $false
    Assert-Equal 'cleanup disables version type' (Get-PrivateField 'includeVersionType').Enabled $false

    Set-Options $true $true $true
    $builtIn = $formType.GetField('BuiltInVersionTypes', $staticBinding).GetValue($null)
    $reviewedIndex = [Array]::IndexOf($builtIn, 'Reviewed Version')
    Assert-Equal 'Revised Version follows Reviewed Version' $builtIn[$reviewedIndex + 1] 'Revised Version'
    $actual = Get-PreviewName '合同-20260730-v1.2-Revised Version.docx'
    Assert-Equal 'recognize Revised Version' $actual "合同-$today-v1.3-XX清洁版.docx"

    $actual = Get-PreviewName '合同-20261340-修订版.pdf'
    Assert-Equal 'preserve invalid date digits' $actual "合同-20261340-$today-v1.0-XX清洁版.pdf"
    $actual = Get-PreviewName '合同-20261340-v1.2-修订版.pdf'
    Assert-Equal 'preserve invalid date and increment version' $actual "合同-20261340-$today-v1.3-XX清洁版.pdf"

    $actual = Get-PreviewName '合同-20260730-修订版-3.docx'
    Assert-Equal 'preserve sequential format when type enabled' $actual '合同-20260730-修订版-4.docx'

    Set-Options $true $true $false
    $newDocumentMethod = $formType.GetMethod('BuildNewDocumentTargetPath', $binding)
    $documentArguments = [object[]]@([string]$tempRoot, [string]'采购合同')
    $targetPath = $newDocumentMethod.Invoke($form, $documentArguments)
    Assert-Equal 'blank DOCX omits disabled type without trailing dash' `
        ([IO.Path]::GetFileName($targetPath)) "采购合同-$today-v1.0.docx"

    Write-Host "All $passed assertions passed."
}
finally {
    $form.Dispose()
    if ([IO.Directory]::Exists($tempRoot)) {
        [IO.Directory]::Delete($tempRoot, $true)
    }
}
