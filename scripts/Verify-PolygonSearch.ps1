<#
.SYNOPSIS
Checks polygon search against listings in a running API, without changing data.
.DESCRIPTION
Builds an eight-vertex polygon around a published listing returned by the API.
Checks that both price and distance searches return that listing and preserve
the requested order. This checks HTTP/database behavior, not device gestures.
#>
[CmdletBinding()]
param([uri] $BaseUri = 'http://localhost:5080/')

$ErrorActionPreference = 'Stop'
if ($BaseUri.Scheme -notin @('http', 'https')) { throw 'Use HTTP or HTTPS.' }
$page = Invoke-RestMethod -Uri ([uri]::new($BaseUri, 'api/v1/listings?limit=12')) -TimeoutSec 30
$target = @($page.data | Where-Object { [Math]::Abs($_.lat) -lt 85 -and [Math]::Abs($_.lng) -lt 179 }) | Select-Object -First 1
if ($null -eq $target) { throw 'The running API has no published listing suitable for this check.' }

$latitude = [double]$target.lat
$longitude = [double]$target.lng
$ring = [System.Collections.Generic.List[object]]::new()
for ($index = 0; $index -lt 8; $index++) {
    $angle = 2 * [Math]::PI * $index / 8
    $ring.Add([double[]]@(
        ($longitude + 0.003 * [Math]::Cos($angle)),
        ($latitude + 0.003 * [Math]::Sin($angle))))
}
$ring.Add($ring[0])

function Get-DistanceKm([double] $lat, [double] $lng) {
    $radians = [Math]::PI / 180
    $deltaLat = ($lat - $latitude) * $radians
    $deltaLng = ($lng - $longitude) * $radians
    $a = [Math]::Pow([Math]::Sin($deltaLat / 2), 2) +
        [Math]::Cos($latitude * $radians) * [Math]::Cos($lat * $radians) *
        [Math]::Pow([Math]::Sin($deltaLng / 2), 2)
    return 6371 * 2 * [Math]::Asin([Math]::Min(1, [Math]::Sqrt($a)))
}

foreach ($sort in @('price_asc', 'distance_asc')) {
    $body = @{
        area = @{ type = 'Polygon'; coordinates = @(, $ring.ToArray()) }
        origin = @{ lat = $latitude; lng = $longitude }
        sort = $sort
        limit = 100
    } | ConvertTo-Json -Depth 10
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $result = Invoke-RestMethod -Uri ([uri]::new($BaseUri, 'api/v1/listings/search/area')) `
        -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 30
    $timer.Stop()
    $foundTarget = @($result.data | Where-Object { $_.id -eq $target.id }).Count -eq 1
    if (-not $foundTarget) { throw "The $sort polygon search did not return the listing inside the polygon." }
    $previous = [double]::NegativeInfinity
    foreach ($listing in $result.data) {
        $value = if ($sort -eq 'price_asc') { [double]$listing.price }
            else { Get-DistanceKm ([double]$listing.lat) ([double]$listing.lng) }
        if ($value + 0.0000001 -lt $previous) { throw "Results are not ordered by $sort." }
        $previous = $value
    }
    [PSCustomObject]@{
        Sort = $sort
        Vertices = 8
        Matches = $result.totalCount
        TargetFound = $foundTarget
        Ordered = $true
        HttpMs = [Math]::Round($timer.Elapsed.TotalMilliseconds, 1)
    }
}
