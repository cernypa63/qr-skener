# Vytvori klientsky RustDesk exe, ktery se sam pripoji k Vasemu serveru.
# Pouziti (PowerShell):
#   powershell -ExecutionPolicy Bypass -File vytvor_klienta.ps1 -Server moje.domena.cz -Klic "obsah id_ed25519.pub"
param(
    [Parameter(Mandatory = $true)][string]$Server,
    [Parameter(Mandatory = $true)][string]$Klic,
    [string]$Verze = "1.5.3",
    [string]$Vystup = $PSScriptRoot
)
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$json = '{"host":"' + $Server.Trim() + '","key":"' + $Klic.Trim() + '","relay":"","api":""}'
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
$b64 = $b64.TrimEnd('=').Replace('+', '-').Replace('/', '_')
$znaky = $b64.ToCharArray(); [Array]::Reverse($znaky)
$config = -join $znaky

$url = "https://github.com/rustdesk/rustdesk/releases/download/$Verze/rustdesk-$Verze-x86_64.exe"
$exe = Join-Path $Vystup ("rustdesk-licensed-" + $config + ".exe")
Write-Host "Stahuji $url"
Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing
Set-Content -Path (Join-Path $Vystup "config.txt") -Value $config -Encoding ASCII
Write-Host ""
Write-Host "Hotovo: $exe"
Write-Host "Tento soubor poslete klientum. Nazev souboru se nesmi menit (obsahuje adresu serveru a klic)."
Write-Host "Konfiguracni retezec (pro rucni nastaveni nebo rustdesk.exe --config) je v config.txt"
