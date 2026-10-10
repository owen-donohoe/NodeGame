# Live check of the ranked queue in a UGS environment: two throwaway
# anonymous players each create a ticket, then both are polled until the
# Matchmaker assigns them (or the attempt times out). Prints every status
# response verbatim, since the assignment's shape is what this proves.
#
# Needs no credentials: anonymous sign-in only uses the public project ID.
# Creates two anonymous players and one match record per successful run.
#
#   powershell -File scripts/matchmaker-spike.ps1 [-Environment development] [-Seconds 60]
param(
    [string]$Environment = "development",
    [int]$Seconds = 60,
    # PR D release identity: banks, district health, minions and Pier gates (sim 4).
    [int]$Protocol = 5,
    [int]$Sim = 4,
    [int]$Content = -1242191275,
    [switch]$EnsureRecords,
    # After a match forms: publish a code as slot 0, read it as slot 1, then
    # leave before connecting, which must void the match and free both players.
    [switch]$Rendezvous,
    # After a match forms and both confirm: slot 0 holds while slot 1 stays
    # silent. A claim at once must be TooEarly; after 16 s (past the 15 s heartbeat grace) it must win, with
    # the result read back by both players (8.2c).
    [switch]$Hold,
    # After a match forms and both confirm: slot 1 surrenders (LeaveMatch with
    # forfeit), and slot 0's Presence must carry the settled result (D18).
    [switch]$Surrender
)

$ErrorActionPreference = "Stop"
Write-Host "Environment: $Environment; protocol: $Protocol; simulation: $Sim; content: $Content"
$ProjectId = "b0178b5c-011c-4e8c-8913-6ffe4286c614"
$AuthUrl = "https://player-auth.services.api.unity.com/v1/authentication/anonymous"
$TicketsUrl = "https://matchmaker.services.api.unity.com/v2/tickets"

function New-AnonymousPlayer {
    $headers = @{ "ProjectId" = $ProjectId; "UnityEnvironment" = $Environment }
    $r = Invoke-RestMethod -Method Post -Uri $AuthUrl -Headers $headers -ContentType "application/json" -Body "{}"
    [pscustomobject]@{ Id = $r.userId; Token = $r.idToken }
}

function New-Ticket($player) {
    $body = @{
        queueName = "ranked"
        attributes = @{}
        players = @(@{ id = $player.Id; customData = @{ protocol = $Protocol; sim = $Sim; content = $Content } })
    } | ConvertTo-Json -Depth 6
    $headers = @{ "Authorization" = "Bearer $($player.Token)" }
    (Invoke-RestMethod -Method Post -Uri $TicketsUrl -Headers $headers -ContentType "application/json" -Body $body).id
}

function Get-TicketStatus($player, $ticketId) {
    $headers = @{ "Authorization" = "Bearer $($player.Token)" }
    Invoke-RestMethod -Method Get -Uri "$TicketsUrl/status?id=$ticketId" -Headers $headers
}

function Invoke-Module($player, $function, $params) {
    $url = "https://cloud-code.services.api.unity.com/v1/projects/$ProjectId/modules/NodeWarCloud/$function"
    $headers = @{ "Authorization" = "Bearer $($player.Token)" }
    $body = @{ params = $params } | ConvertTo-Json -Depth 6 -Compress
    (Invoke-RestMethod -Method Post -Uri $url -Headers $headers -ContentType "application/json" -Body $body).output
}

function Initialize-Records($player) {
    # What the lobby does on open: GetPlayerState creates the protected
    # rating/rank records that the queue's Cloud Save rules read.
    $url = "https://cloud-code.services.api.unity.com/v1/projects/$ProjectId/modules/NodeWarCloud/GetPlayerState"
    $headers = @{ "Authorization" = "Bearer $($player.Token)" }
    $r = Invoke-RestMethod -Method Post -Uri $url -Headers $headers -ContentType "application/json" -Body '{"params":{}}'
    Write-Host "Records for $($player.Id): rank $($r.output.Rank | ConvertTo-Json -Compress), rating R $($r.output.Rating.R)"
}

$players = @(New-AnonymousPlayer; New-AnonymousPlayer)
Write-Host "Players: $($players[0].Id), $($players[1].Id)"
if ($EnsureRecords) { $players | ForEach-Object { Initialize-Records $_ } }
$tickets = @($players | ForEach-Object { New-Ticket $_ })
Write-Host "Tickets: $($tickets -join ', ')"

$done = @($false, $false)
$deadline = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $deadline -and ($done -contains $false)) {
    Start-Sleep -Seconds 2
    for ($i = 0; $i -lt 2; $i++) {
        if ($done[$i]) { continue }
        $status = Get-TicketStatus $players[$i] $tickets[$i]
        $json = $status | ConvertTo-Json -Depth 10 -Compress
        Write-Host "P$i $json"
        if ($status.status -and $status.status -ne "InProgress" -and $status.status -ne "Found") {
            throw "P$i matchmaking failed: $json"
        }
        if ($status.matchId) {
            if ($foundMatchId -and $foundMatchId -ne $status.matchId) {
                throw "Players were assigned different matches."
            }
            $foundMatchId = $status.matchId
        }
        if ($status.status -and $status.status -ne "InProgress") { $done[$i] = $true }
    }
}
if ($done -contains $false) { Write-Host "Timed out after $Seconds s." ; exit 1 }
if (-not $foundMatchId) { throw "Matchmaking completed without a match ID." }

if ($Rendezvous) {
    $matchId = $foundMatchId
    if (-not $matchId) { Write-Host "No match ID to rendezvous on."; exit 1 }
    $views = @($players | ForEach-Object { Invoke-Module $_ "Rendezvous" @{ matchId = $matchId; joinCode = $null } })
    for ($i = 0; $i -lt 2; $i++) { Write-Host "Roster P$i $($views[$i] | ConvertTo-Json -Compress)" }
    $host0 = if ($views[0].slot -eq 0) { 0 } else { 1 }
    $guest = 1 - $host0
    Invoke-Module $players[$host0] "Rendezvous" @{ matchId = $matchId; joinCode = "SPIKE1" } | Out-Null
    $seen = Invoke-Module $players[$guest] "Rendezvous" @{ matchId = $matchId; joinCode = $null }
    Write-Host "Guest reads joinCode: $($seen.joinCode)"
    $leave = Invoke-Module $players[$guest] "LeaveMatch" @{ matchId = $matchId; forfeit = $false }
    Write-Host "Leave before connecting: $($leave | ConvertTo-Json -Compress)"
    $after = Invoke-Module $players[$host0] "Rendezvous" @{ matchId = $matchId; joinCode = $null }
    Write-Host "Record state afterwards: $($after.state)"
    foreach ($p in $players) {
        $s = Invoke-Module $p "GetPlayerState" @{}
        Write-Host "Active match for $($p.Id): '$($s.ActiveMatch.matchId)'"
        if ($s.ActiveMatch.matchId) { throw "Test match left an active claim for $($p.Id)." }
    }
    if ($seen.joinCode -ne "SPIKE1") { throw "Guest did not receive the host's join code." }
    if ($leave.outcome -ne 0 -or $after.state -ne 3) { throw "Unstarted test match was not voided." }
    foreach ($view in $views) {
        if ($view.slot -lt 0 -or -not $view.mapId -or $view.boardHash -eq 0) {
            throw "Server did not supply the ranked roster and map agreement."
        }
        if ($view.mapId -ne $views[0].mapId -or $view.boardHash -ne $views[0].boardHash) {
            throw "Players received different ranked maps."
        }
    }
}

if ($Hold -or $Surrender) {
    $matchId = $foundMatchId
    if (-not $matchId) { Write-Host "No match ID to play on."; exit 1 }
    $views = @($players | ForEach-Object { Invoke-Module $_ "Rendezvous" @{ matchId = $matchId; joinCode = $null } })
    $host0 = if ($views[0].slot -eq 0) { 0 } else { 1 }
    $guest = 1 - $host0
    $players | ForEach-Object { Invoke-Module $_ "ConfirmConnected" @{ matchId = $matchId } | Out-Null }
    Write-Host "Both confirmed; the match counts as started."

    if ($Hold) {
        $p = Invoke-Module $players[$host0] "Presence" @{ matchId = $matchId; holding = $true }
        Write-Host "Presence (host holding): $($p | ConvertTo-Json -Compress -Depth 6)"
        $early = Invoke-Module $players[$host0] "ResolveHold" @{ matchId = $matchId }
        Write-Host "Claim at once (expect TooEarly): $($early | ConvertTo-Json -Compress -Depth 6)"
        Start-Sleep -Seconds 16
        Invoke-Module $players[$host0] "Presence" @{ matchId = $matchId; holding = $true } | Out-Null
        $claim = Invoke-Module $players[$host0] "ResolveHold" @{ matchId = $matchId }
        Write-Host "Claim after 16 s (expect Won): outcome $($claim.outcome), cause $($claim.result.cause), won $($claim.result.won)"
        $absent = Invoke-Module $players[$guest] "GetPlayerState" @{}
        Write-Host "Absent player discipline: level $($absent.Discipline.Level), blocked until $($absent.Discipline.BlockedUntilUnixSeconds)"
    }
    else {
        $leave = Invoke-Module $players[$guest] "LeaveMatch" @{ matchId = $matchId; forfeit = $true }
        Write-Host "Guest surrenders: $($leave.outcome) $($leave.message)"
        $p = Invoke-Module $players[$host0] "Presence" @{ matchId = $matchId; holding = $true }
        Write-Host "Host presence (expect a settled Forfeit result): state $($p.result.state), cause $($p.result.cause), won $($p.result.won)"
    }

    for ($i = 0; $i -lt 2; $i++) {
        $r = Invoke-Module $players[$i] "GetMatchResult" @{ matchId = $matchId }
        Write-Host "Result for P$i : state $($r.state), cause $($r.cause), won $($r.won), rrDelta $($r.rrDelta)"
    }
}
