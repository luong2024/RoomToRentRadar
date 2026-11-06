---
name: cosmos-db-geo-queries
description: Configuration and execution of Azure Cosmos DB spatial GeoJSON Point-in-Polygon queries for US05.
---

# Azure Cosmos DB Spatial Queries (Point-in-Polygon)

Directly supports **US05 (Students draw a polygon on the map to query listings within that boundary)**.

## 1. GeoJSON Ordering in Azure Cosmos DB
- Cosmos DB NoSQL containers support native GeoJSON spatial indexing.
- **Coordinate Order:** `[Longitude, Latitude]` (Longitude first, Latitude second). Reverse the order when ingesting mobile device coordinates, which are typically formatted as `(Latitude, Longitude)`.

```csharp
public class RoomDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("location")]
    public Point Location { get; set; } = null!; // Microsoft.Azure.Cosmos.Spatial.Point
}
```

## 2. Point-in-Polygon Query Execution (`ST_WITHIN`)

When the user finishes drawing a polygon on Google Maps (minimum 3 vertices, closing vertex matching the initial vertex):

```csharp
public async Task<IReadOnlyList<RoomDocument>> SearchWithinPolygonAsync(
    IReadOnlyList<GeoPointDto> polygonPoints,
    Container container)
{
    // 1. Convert client coordinates to Cosmos DB Position ring
    var linearRingCoordinates = polygonPoints
        .Select(p => new Position(p.Longitude, p.Latitude))
        .ToList();

    // Ensure the ring is closed
    if (linearRingCoordinates.First() != linearRingCoordinates.Last())
    {
        linearRingCoordinates.Add(linearRingCoordinates.First());
    }

    var polygon = new Polygon(new LinearRing(linearRingCoordinates));

    // 2. Build spatial query using ST_WITHIN
    var query = new QueryDefinition(
        "SELECT * FROM c WHERE ST_WITHIN(c.location, @polygon) AND c.isVerified = true")
        .WithParameter("@polygon", polygon);

    var results = new List<RoomDocument>();
    using var feedIterator = container.GetItemQueryIterator<RoomDocument>(query);

    while (feedIterator.HasMoreResults)
    {
        var response = await feedIterator.ReadNextAsync();
        results.AddRange(response);
    }

    return results;
}
```
