param(
    [string]$InstallRoot = "$env:ProgramFiles\HiddenIcons"
)
$ErrorActionPreference = 'Stop'
$serviceExe = Join-Path $InstallRoot 'HiddenIcons.Service.exe'
if (-not (Test-Path -LiteralPath $serviceExe)) { throw "Missing $serviceExe. Publish the service first." }
$dataRoot = Join-Path $env:ProgramData 'HiddenIcons'
New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null
# The tray UI runs as the logged-in user while the service runs as LocalService.
icacls.exe $dataRoot /grant '*S-1-5-32-545:(OI)(CI)M' /inheritance:e | Out-Null
$serviceName = 'HiddenIconsService'
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    if ($existing.Status -ne 'Stopped') { Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue }
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 1
}
$quote = [char]34
$binaryPath = $quote + $serviceExe + $quote
New-Service -Name $serviceName -BinaryPathName $binaryPath -DisplayName 'Hidden Icons Service' -Description 'Starts user-approved background programs from Hidden Icons profiles.' -StartupType Automatic | Out-Null
# New-Service creates the service reliably; use sc.exe only to lower its account.
sc.exe config $serviceName 'obj=' 'NT AUTHORITY\LocalService' 'password=' '""' | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Could not set LocalService account (sc.exe exit $LASTEXITCODE)." }
sc.exe failure $serviceName 'reset= 86400' 'actions= restart/5000/restart/30000/none/0' | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Could not configure service recovery (sc.exe exit $LASTEXITCODE)." }
Start-Service -Name $serviceName
Write-Host "Service installed and started: $serviceName"
