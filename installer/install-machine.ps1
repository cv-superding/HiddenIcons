$ErrorActionPreference = 'Stop'
$sourceRoot = Join-Path $PSScriptRoot '..\release\HiddenIcons'
$installRoot = Join-Path $env:ProgramFiles 'HiddenIcons'
if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'service\HiddenIcons.Service.exe'))) {
    throw "Release package not found: $sourceRoot"
}
New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Copy-Item (Join-Path $sourceRoot 'app') (Join-Path $installRoot 'app') -Recurse -Force
Copy-Item (Join-Path $sourceRoot 'service') (Join-Path $installRoot 'service') -Recurse -Force
& (Join-Path $PSScriptRoot 'install-service.ps1') -InstallRoot (Join-Path $installRoot 'service')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Hidden Icons.lnk'))
$shortcut.TargetPath = Join-Path $installRoot 'app\HiddenIcons.App.exe'
$shortcut.WorkingDirectory = Join-Path $installRoot 'app'
$shortcut.Description = 'Hidden Icons manager'
$shortcut.Save()
Write-Host "Installed to $installRoot"
