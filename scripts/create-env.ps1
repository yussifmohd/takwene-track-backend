<#
Creates .env from .env.example with fresh random local secrets, so the project starts with
`docker compose up -d --build` and nothing to fill in by hand.

Usage, from the repository root (Windows PowerShell 5.1 or PowerShell 7):
    powershell -ExecutionPolicy Bypass -File scripts/create-env.ps1

- Never overwrites an existing .env.
- Generates the JWT signing key, the database password and the admin password.
- Picks the first free local port for the database (5432, then 5433-5435).
- Prints the admin email and password to sign in with (also stored in .env, which git ignores).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$example = Join-Path $root '.env.example'
$target = Join-Path $root '.env'

if (Test-Path $target) {
    Write-Host ".env already exists - nothing changed. Delete it first if you want a new one."
    exit 1
}

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
function New-RandomText([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    $rng.GetBytes($buffer)
    [Convert]::ToBase64String($buffer)
}

$signingKey = New-RandomText 48
$dbPassword = (New-RandomText 24) -replace '[+/=]', ''
# Prefix and suffix guarantee the password rules: upper case, lower case, a symbol and a digit.
$adminPassword = 'Tk!' + ((New-RandomText 18) -replace '[+/=]', '') + '9a'

$port = $null
foreach ($candidate in 5432, 5433, 5434, 5435) {
    $listener = New-Object System.Net.Sockets.TcpListener ([System.Net.IPAddress]::Loopback, $candidate)
    try { $listener.Start(); $listener.Stop(); $port = $candidate; break } catch { }
}
if (-not $port) {
    Write-Host "Ports 5432-5435 are all in use; set POSTGRES_HOST_PORT in .env to a free port before starting."
    $port = 5432
}

$lines = Get-Content -Path $example -Encoding UTF8 | ForEach-Object {
    switch -Regex ($_) {
        '^JWT__SIGNINGKEY=' { "JWT__SIGNINGKEY=$signingKey"; break }
        '^POSTGRES_PASSWORD=' { "POSTGRES_PASSWORD=$dbPassword"; break }
        '^ADMIN__PASSWORD=' { "ADMIN__PASSWORD=$adminPassword"; break }
        '^POSTGRES_HOST_PORT=' { "POSTGRES_HOST_PORT=$port"; break }
        default { $_ }
    }
}
# UTF-8 without BOM and LF line endings, which docker compose reads on every platform.
[System.IO.File]::WriteAllText($target, (($lines -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding $false))

$adminEmail = (($lines | Where-Object { $_ -match '^ADMIN__EMAIL=' }) -replace '^ADMIN__EMAIL=', '')
Write-Host ""
Write-Host "Created .env with new random secrets (database on localhost:$port)."
Write-Host "Sign in to the web app with:"
Write-Host "  email:    $adminEmail"
Write-Host "  password: $adminPassword"
Write-Host ""
Write-Host "Next: docker compose up -d --build"
