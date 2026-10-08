param(
    [string]$ProjectPath = $PSScriptRoot,
    [int]$Port = 15000,
    [switch]$CaptureScreenshot
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$playerPath = Join-Path $projectRoot 'Build\MultiplayerBase\ZZabmongus.exe'
if (-not (Test-Path -LiteralPath $playerPath)) { throw 'Build the Multiplayer Windows Test Player first.' }
$resultDirectory = Join-Path $projectRoot ('Logs\multiplayer-probe-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$processes = @()
try {
    foreach ($role in @('host', '1', '2', '3')) {
        $resultPath = Join-Path $resultDirectory ($role + '.json')
        $logPath = Join-Path $resultDirectory ($role + '.log')
        $arguments = @('-batchmode', '-nographics', '-zzNetProbe', $role, '-zzNetPort', $Port,
            '-zzNetResult', ('"' + $resultPath + '"'), '-logFile', ('"' + $logPath + '"'))
        if ($CaptureScreenshot -and $role -eq 'host') {
            $arguments = @('-batchmode', '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '1000',
                '-zzNetProbe', $role, '-zzNetPort', $Port, '-zzNetResult', ('"' + $resultPath + '"'), '-logFile', ('"' + $logPath + '"'),
                '-zzNetScreenshot', ('"' + (Join-Path $resultDirectory 'host.png') + '"'))
        }
        $processes += Start-Process -FilePath $playerPath -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
    }
    $deadline = (Get-Date).AddSeconds(85)
    while (@($processes | Where-Object { -not $_.HasExited }).Count -gt 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    if (@($processes | Where-Object { -not $_.HasExited }).Count -gt 0) { throw 'Network integration processes timed out.' }
    $passed = $true
    foreach ($role in @('host', '1', '2', '3')) {
        $resultPath = Join-Path $resultDirectory ($role + '.json')
        if (-not (Test-Path -LiteralPath $resultPath)) { $passed = $false; Write-Output "Missing result: $role"; continue }
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        $passed = $passed -and $result.passed
        Write-Output ($role + ': ' + $result.passed + ' | ' + $result.detail)
    }
    Write-Output ('Reports: ' + $resultDirectory)
    if (-not $passed) { throw 'Network integration check failed; inspect the reports.' }
} finally {
    # Only stop child processes started by this run, never an existing Unity Editor/player.
    foreach ($process in $processes) { if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force } }
}
