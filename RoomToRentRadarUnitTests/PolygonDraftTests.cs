using RoomToRentRadar.Models;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class PolygonDraftTests
{
    private static readonly GeoPoint A = new(10.0, 106.0);
    private static readonly GeoPoint B = new(10.0, 107.0);
    private static readonly GeoPoint C = new(11.0, 107.0);

    [Test]
    public void Close_RequiresThreeVertices()
    {
        var draft = new PolygonDraft();
        draft.Add(A);
        draft.Add(B);

        Assert.That(draft.CanClose, Is.False);
        Assert.That(draft.Close(), Is.False);
        Assert.That(draft.IsClosed, Is.False);

        draft.Add(C);

        Assert.That(draft.CanClose, Is.True);
        Assert.That(draft.Close(), Is.True);
        Assert.That(draft.IsClosed, Is.True);
    }

    [Test]
    public void AddAndRemoveLast_AreIgnoredAfterClosing()
    {
        var draft = CreateClosedDraft();

        Assert.That(draft.Add(new GeoPoint(12, 108)), Is.False);
        Assert.That(draft.RemoveLast(), Is.False);
        Assert.That(draft.Vertices, Has.Count.EqualTo(3));
    }

    [Test]
    public void AddFiveVertices_DoesNotAutoCloseAtThree_ClosesFullRingOnRequest()
    {
        var vertices = new[]
        {
            new GeoPoint(10.77, 106.69), new GeoPoint(10.77, 106.70),
            new GeoPoint(10.775, 106.705), new GeoPoint(10.78, 106.70),
            new GeoPoint(10.78, 106.69)
        };
        var draft = new PolygonDraft();
        foreach (var vertex in vertices)
        {
            Assert.That(draft.Add(vertex), Is.True);
            Assert.That(draft.IsClosed, Is.False);
        }

        Assert.That(draft.Close(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(draft.Vertices, Is.EqualTo(vertices));
            Assert.That(draft.Ring, Is.EqualTo(vertices.Append(vertices[0])));
            Assert.That(draft.ToGeoJsonCoordinates()[0], Has.Length.EqualTo(6));
        });
        draft.Clear();
        Assert.Multiple(() =>
        {
            Assert.That(draft.Vertices, Is.Empty);
            Assert.That(draft.Ring, Is.Empty);
            Assert.That(draft.IsClosed, Is.False);
        });
    }

    [Test]
    public void Add_RejectsDuplicateConsecutiveVerticesAndInvalidCoordinates()
    {
        var draft = new PolygonDraft();

        Assert.That(draft.Add(A), Is.True);
        Assert.That(draft.Add(A), Is.False);
        Assert.That(draft.Add(new GeoPoint(95, 106)), Is.False);
        Assert.That(draft.Add(new GeoPoint(10, 200)), Is.False);
        Assert.That(draft.Add(new GeoPoint(double.NaN, 106)), Is.False);
        Assert.That(draft.Vertices, Has.Count.EqualTo(1));
    }

    [Test]
    public void RemoveLast_RemovesTheMostRecentVertex()
    {
        var draft = new PolygonDraft();
        draft.Add(A);
        draft.Add(B);

        Assert.That(draft.RemoveLast(), Is.True);
        Assert.That(draft.Vertices, Is.EqualTo(new[] { A }));

        Assert.That(draft.RemoveLast(), Is.True);
        Assert.That(draft.RemoveLast(), Is.False);
    }

    [Test]
    public void ToGeoJsonCoordinates_ReturnsOneClosedRingOfLngLatPositions()
    {
        var draft = CreateClosedDraft();

        var coordinates = draft.ToGeoJsonCoordinates();

        Assert.That(coordinates, Has.Length.EqualTo(1));
        Assert.That(coordinates[0], Has.Length.EqualTo(4));
        Assert.That(coordinates[0][0], Is.EqualTo(new[] { 106.0, 10.0 }));
        Assert.That(coordinates[0][^1], Is.EqualTo(coordinates[0][0]));
    }

    [Test]
    public void ToGeoJsonCoordinates_RequiresAClosedPolygon()
    {
        var draft = new PolygonDraft();
        draft.Add(A);
        draft.Add(B);
        draft.Add(C);

        Assert.That(() => draft.ToGeoJsonCoordinates(), Throws.InvalidOperationException);
    }

    [Test]
    public void Centroid_IsAreaWeighted()
    {
        var draft = new PolygonDraft();
        draft.Add(new GeoPoint(10, 106));
        draft.Add(new GeoPoint(10, 108));
        draft.Add(new GeoPoint(12, 108));
        draft.Add(new GeoPoint(12, 106));
        draft.Close();

        var centroid = draft.Centroid;

        Assert.That(centroid.Lat, Is.EqualTo(11).Within(1e-9));
        Assert.That(centroid.Lng, Is.EqualTo(107).Within(1e-9));
    }

    [Test]
    public void Centroid_OfTriangleIsTheVertexAverage()
    {
        var centroid = CreateClosedDraft().Centroid;

        Assert.That(centroid.Lat, Is.EqualTo(10.3333333333).Within(1e-9));
        Assert.That(centroid.Lng, Is.EqualTo(106.6666666667).Within(1e-9));
    }

    [Test]
    public void Load_ReplacesContentAndRequiresAValidPolygon()
    {
        var draft = CreateClosedDraft();

        Assert.That(draft.Load([A, B]), Is.False);
        Assert.That(draft.IsClosed, Is.False);
        Assert.That(draft.Vertices, Has.Count.EqualTo(2));

        Assert.That(draft.Load([A, B, C]), Is.True);
        Assert.That(draft.IsClosed, Is.True);
        Assert.That(draft.Vertices, Has.Count.EqualTo(3));
    }

    private static PolygonDraft CreateClosedDraft()
    {
        var draft = new PolygonDraft();
        draft.Add(A);
        draft.Add(B);
        draft.Add(C);
        draft.Close();
        return draft;
    }
}
