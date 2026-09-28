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
    [int]$Protocol = 2,
    [int]$Sim = 1,
    [int]$Content = 1966419918,
    [switch]$EnsureRecords
)

$ErrorActionPreference = "Stop"
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
        if ($status.status -and $status.status -ne "InProgress") { $done[$i] = $true }
    }
}
if ($done -contains $false) { Write-Host "Timed out after $Seconds s." ; exit 1 }
