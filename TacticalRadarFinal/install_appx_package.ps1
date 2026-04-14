$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputAppx = Join-Path $ProjectRoot "TacticalRadarFinal.appx"
$OutputPfx = Join-Path $ProjectRoot "TacticalRadarFinal_TestCert.pfx"
$CertPassword = ConvertTo-SecureString -String "123456" -Force -AsPlainText

if (-not (Test-Path $OutputAppx)) {
    throw "找不到安装包: $OutputAppx`n请先运行 build_appx_package.ps1"
}

if (Test-Path $OutputPfx) {
    Import-PfxCertificate `
        -FilePath $OutputPfx `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -Password $CertPassword | Out-Null
}

Add-AppxPackage $OutputAppx

Write-Host "安装完成。"
