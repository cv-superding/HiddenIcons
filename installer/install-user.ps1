$ErrorActionPreference = 'Stop'
$sourceRoot = Join-Path $PSScriptRoot '..\release\HiddenIcons\app'
$installRoot = Join-Path $env:LOCALAPPDATA 'HiddenIcons\app'
if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'HiddenIcons.App.exe'))) {
    throw "Release package not found: $sourceRoot"
}
New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Copy-Item (Join-Path $sourceRoot '*') $installRoot -Recurse -Force
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -Path $runKey -Name 'HiddenIcons.Manager' -Value ('"' + (Join-Path $installRoot 'HiddenIcons.App.exe') + '" --tray')
$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Hidden Icons.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installRoot 'HiddenIcons.App.exe'
$shortcut.WorkingDirectory = $installRoot
$shortcut.Description = 'Hidden Icons manager'
$shortcut.Save()
Write-Host "User installation complete: $installRoot"
