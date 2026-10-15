param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '../Builds/LocalizationWalls/Driftline-LocalizationWalls.exe'),
    [string]$ReportDirectory = (Join-Path $PSScriptRoot '../Docs/Validation/LocalizationWalls')
)
$ErrorActionPreference='Stop'
$ExecutablePath=(Resolve-Path -LiteralPath $ExecutablePath).Path
$ReportDirectory=[IO.Path]::GetFullPath($ReportDirectory)
[IO.Directory]::CreateDirectory($ReportDirectory)|Out-Null
$backup=Join-Path $ReportDirectory 'native-preferences.json'
function Invoke-Probe([string]$phase) {
    $report=Join-Path $ReportDirectory ("native-$phase.jsonl")
    $log=Join-Path $ReportDirectory ("native-$phase.log")
    $arguments=@('-screen-fullscreen','0','-screen-width','1366','-screen-height','768','--driftline-presentation-smoke','--phase',$phase,'--report',('"'+$report+'"'),'--backup',('"'+$backup+'"'),'-logFile',('"'+$log+'"'))
    $process=Start-Process -FilePath $ExecutablePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    try {
        if(!$process.WaitForExit(55000)){throw "Native $phase probe timed out."}
        $process.WaitForExit()
        if($process.ExitCode -ne 0){throw "Native $phase probe failed with exit code $($process.ExitCode). See $log"}
        $entries=@(Get-Content -LiteralPath $report|ForEach-Object{$_|ConvertFrom-Json})
        if($entries.Count -eq 0 -or @($entries|Where-Object{!$_.passed}).Count -gt 0 -or $entries[-1].check -ne 'complete'){throw "Native $phase checks failed."}
        [pscustomobject]@{Phase=$phase;Checks=$entries.Count;Passed=$true}
    } finally {
        if(!$process.HasExited){Stop-Process -Id $process.Id}
        $process.Dispose()
    }
}
try {Invoke-Probe 'write'} finally {
    # The second launch verifies startup and restores the original preferences, even if writing failed.
    if(Test-Path -LiteralPath $backup){Invoke-Probe 'read'}
}
