param([string]$ServiceName = 'HiddenIconsService')
$ErrorActionPreference = 'Stop'
sc.exe stop $ServiceName *> $null
sc.exe delete $ServiceName
Write-Host "服务已卸载：$ServiceName"
