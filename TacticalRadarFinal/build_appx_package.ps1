$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppxDir = Join-Path $ProjectRoot "bin\x64\Debug\AppX"
$OutputAppx = Join-Path $ProjectRoot "TacticalRadarFinal.appx"
$OutputPfx = Join-Path $ProjectRoot "TacticalRadarFinal_TestCert.pfx"
$CertPasswordPlain = "123456"
$CertPassword = ConvertTo-SecureString -String $CertPasswordPlain -Force -AsPlainText

function Find-Tool {
    param(
        [string]$ToolName
    )

    $roots = @(
        "C:\Program Files (x86)\Windows Kits\10\bin",
        "C:\Program Files\Windows Kits\10\bin"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) {
            continue
        }

        $tool = Get-ChildItem -Path $root -Recurse -Filter $ToolName -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($tool) {
            return $tool.FullName
        }
    }

    throw "Tool not found: $ToolName. Please install Windows SDK first."
}

if (-not (Test-Path $AppxDir)) {
    throw "AppX folder not found: $AppxDir`nBuild Debug|x64 in Visual Studio first."
}

$MakeAppx = Find-Tool -ToolName "makeappx.exe"
$SignTool = Find-Tool -ToolName "signtool.exe"

Write-Host "MakeAppx: $MakeAppx"
Write-Host "SignTool: $SignTool"
Write-Host "AppX Dir : $AppxDir"
Write-Host "Output   : $OutputAppx"

if (Test-Path $OutputAppx) {
    Remove-Item -LiteralPath $OutputAppx -Force
}

& $MakeAppx pack /d $AppxDir /p $OutputAppx /o

if (-not (Test-Path $OutputPfx)) {
    Write-Host "Creating test signing certificate..."
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=TacticalRadarFinalTest" `
        -CertStoreLocation "Cert:\CurrentUser\My"

    Export-PfxCertificate `
        -Cert $cert `
        -FilePath $OutputPfx `
        -Password $CertPassword | Out-Null
}

& $SignTool sign /fd SHA256 /f $OutputPfx /p $CertPasswordPlain $OutputAppx

Write-Host ""
Write-Host "Done. Generated files:"
Write-Host "  $OutputAppx"
Write-Host "  $OutputPfx"
Write-Host ""
Write-Host "Install command:"
Write-Host ('  Add-AppxPackage "{0}"' -f $OutputAppx)
