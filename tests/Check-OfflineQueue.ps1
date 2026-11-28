param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$bin = (Resolve-Path $BuildDirectory).Path
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'HtmlAgilityPack.dll'))
$app = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'IdleMasterExtended.exe'))
$badgeType = $app.GetType('IdleMasterExtended.Badge')
$parse = $badgeType.GetMethod('ReadRemainingCards', [Reflection.BindingFlags]'NonPublic,Static')
$cases = @(
    @('<div><span class="progress_info_bold">4 card drops remaining</span></div>', '4'),
    @('<span class="progress_info_bold extra">1 card drop remaining</span>', '1'),
    @('<span class="progress_info_bold">No card drops remaining</span>', '0'),
    @('<div class="badge_title_stats_playtime">120 hrs</div>', ''),
    @('<form action="/login"><input type="password"></form>', ''),
    @('<span class="progress_info_bold">HTTP 503</span>', ''),
    @('<span class="progress_info_bold">4 card drops', '')
)
foreach ($case in $cases) {
    $document = New-Object HtmlAgilityPack.HtmlDocument
    $document.LoadHtml($case[0])
    if ([string]$parse.Invoke($null,@($document.DocumentNode)) -cne $case[1]) { throw "Unexpected count for $($case[0])" }
}
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$type = $app.GetType('IdleMasterExtended.Statistics')
$stats = [Activator]::CreateInstance($type)
$observe = $type.GetMethod('Observe',$flags)
foreach ($reading in @(@(1,4),@(2,3),@(1,3),@(1,3),@(2,0),@(2,0))) {
    [void]$observe.Invoke($stats,$reading)
}
if ($stats.getSessionCardIdled() -ne 4 -or $type.GetProperty('Completed',$flags).GetValue($stats) -ne 1) { throw 'Incorrect per-game accounting' }
# Deterministic latency model: 291 eligible games, 30 per page, 200 ms per sequential response.
$choose = $app.GetType('IdleMasterExtended.frmMain').GetMethod('EarlyCandidate',[Reflection.BindingFlags]'Static,NonPublic')
$games = [Activator]::CreateInstance([System.Collections.Generic.List`1].MakeGenericType($badgeType))
$first = 0
for ($i=1; $i -le 291; $i++) {
    $games.Add([Activator]::CreateInstance($badgeType,@([string]$i,"Game $i",'4','3')))
    if ($i % 30 -eq 0 -and !$first -and $choose.Invoke($null,@($games,'default',$true,$false))) { $first = $i / 30 }
}
if ($first -ne 1 -or $choose.Invoke($null,@($games,'mostcards',$true,$false))) { throw 'Unsafe or late startup choice' }
$total = [int][Math]::Ceiling(291/30)
"PASS: $($cases.Count) page fixtures and per-game accounting. Simulated first selection: $($total*200) ms before, $($first*200) ms after. Full scan: $total requests in both cases."
