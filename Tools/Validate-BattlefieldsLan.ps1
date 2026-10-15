param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '../Builds/BattlefieldsV2/Driftline-Battlefields.exe'),
    [string]$ReportDirectory = (Join-Path $PSScriptRoot '../Docs/Validation/BattlefieldsV2'),
    [int]$Port = 7795
)
$ErrorActionPreference='Stop'
$ExecutablePath=(Resolve-Path -LiteralPath $ExecutablePath).Path
$ReportDirectory=[IO.Path]::GetFullPath($ReportDirectory)
[IO.Directory]::CreateDirectory($ReportDirectory) | Out-Null
$hostReport=Join-Path $ReportDirectory 'host.jsonl'
$clientReport=Join-Path $ReportDirectory 'client.jsonl'
$hostLog=Join-Path $ReportDirectory 'host.log'
$clientLog=Join-Path $ReportDirectory 'client.log'
$hostArguments=@('-batchmode','-nographics','--driftline-lan-smoke','--role','host','--theme','hangar','--variant','1','--environment-seed','74019','--port',"$Port",'--report',('"'+$hostReport+'"'),'-logFile',('"'+$hostLog+'"'))
$clientArguments=@('-batchmode','-nographics','--driftline-lan-smoke','--role','client','--theme','forest','--variant','0','--environment-seed','222','--port',"$Port",'--report',('"'+$clientReport+'"'),'-logFile',('"'+$clientLog+'"'))
$hostProcess=$null;$clientProcess=$null
try {
    $hostProcess=Start-Process -FilePath $ExecutablePath -ArgumentList $hostArguments -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 1500
    $clientProcess=Start-Process -FilePath $ExecutablePath -ArgumentList $clientArguments -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddSeconds(140)
    while((!$hostProcess.HasExited -or !$clientProcess.HasExited) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Seconds 2}
    if(!$hostProcess.HasExited -or !$clientProcess.HasExited){throw 'Integration probe timed out.'}
    $hostProcess.WaitForExit();$clientProcess.WaitForExit()
    $hostEntries=@(Get-Content -LiteralPath $hostReport | ForEach-Object {$_|ConvertFrom-Json})
    $clientEntries=@(Get-Content -LiteralPath $clientReport | ForEach-Object {$_|ConvertFrom-Json})
    if($hostProcess.ExitCode -ne 0 -or $clientProcess.ExitCode -ne 0){throw 'A player returned a non-zero exit code.'}
    if(!($hostEntries|Where-Object complete) -or !($clientEntries|Where-Object complete)){throw 'Missing completion record.'}
    if(!($clientEntries|Where-Object reconnected)){throw 'Client reconnect was not observed.'}
    $hostStates=@{};$clientStates=@{}
    foreach($entry in $hostEntries){if($entry.phase){$hostStates[$entry.revision]=$entry}}
    foreach($entry in $clientEntries){if($entry.phase){$clientStates[$entry.revision]=$entry}}
    $common=@($hostStates.Keys | Where-Object {$clientStates.ContainsKey($_)} | Sort-Object)
    if($common.Count -lt 20){throw 'Too few shared revisions.'}
    foreach($revision in $common){if($hostStates[$revision].digest -ne $clientStates[$revision].digest){throw "Snapshot mismatch at revision $revision"}}
    foreach($entry in ($hostEntries+$clientEntries)){
        if($entry.error){throw $entry.error}
        if($entry.theme -ne 'hangar' -or $entry.variant -ne 1 -or $entry.environmentSeed -ne 74019){throw 'Client did not retain host scene identity/state/seed.'}
    }
    $exceptions=@(Select-String -LiteralPath $hostLog,$clientLog -Pattern '(NullReference|MissingReference|InvalidOperation|ArgumentOutOfRange|MissingComponent)Exception')
    if($exceptions.Count -gt 0){throw 'Runtime exception found in a player log.'}
    $result=[ordered]@{result='Passed';scope='Windows loopback / two standalone development players';theme='hangar';variant=1;seed=74019;clientPreference='forest';sharedRevisions=$common.Count;firstRevision=$common[0];lastRevision=$common[-1];reconnected=$true;hostExit=$hostProcess.ExitCode;clientExit=$clientProcess.ExitCode;utc=[DateTime]::UtcNow.ToString('o')}
    $result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $ReportDirectory 'lan-summary.json') -Encoding utf8
    $result|ConvertTo-Json -Compress
} finally {
    foreach($process in @($hostProcess,$clientProcess)){if($null -ne $process -and !$process.HasExited){Stop-Process -Id $process.Id}}
}
