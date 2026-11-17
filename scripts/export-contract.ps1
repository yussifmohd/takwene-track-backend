<#
.SYNOPSIS
  Writes the OpenAPI document served by the API (Development, in-memory test host) into a
  feature's contracts folder, so the frontend consumes exactly what the API serves.
  Without this script, the OpenApiContractTests test fails whenever the contract drifts.
.EXAMPLE
  ./scripts/export-contract.ps1 -Feature 001-project-foundation-and-health-check
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Feature
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$contracts = Join-Path $root "specs/$Feature/contracts"
if (-not (Test-Path (Join-Path $root "specs/$Feature"))) { throw "Unknown feature folder specs/$Feature" }
New-Item -ItemType Directory -Force -Path $contracts | Out-Null

$env:UPDATE_CONTRACT = '1'
$env:CONTRACT_FEATURE = $Feature
try {
    Push-Location $root
    dotnet test --project tests/TrackManagement.UnitTests --filter-method "*ServedDocument_MatchesCommittedContract"
    if ($LASTEXITCODE -ne 0) { throw "Contract export failed" }
}
finally {
    Pop-Location
    Remove-Item Env:UPDATE_CONTRACT, Env:CONTRACT_FEATURE -ErrorAction SilentlyContinue
}
Write-Host "Contract exported to specs/$Feature/contracts/openapi.json"
