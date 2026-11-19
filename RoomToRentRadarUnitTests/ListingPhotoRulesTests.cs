using RoomToRentRadar.Features.ListingManagement.Models;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingPhotoRulesTests
{
    [TestCase("image/jpeg", ListingPhotoRules.Jpeg)]
    [TestCase("IMAGE/PNG", ListingPhotoRules.Png)]
    [TestCase("image/jpeg; charset=binary", ListingPhotoRules.Jpeg)]
    [TestCase("jpg", ListingPhotoRules.Jpeg)]
    [TestCase(".jpeg", ListingPhotoRules.Jpeg)]
    [TestCase(".png", ListingPhotoRules.Png)]
    [TestCase("image/gif", null)]
    [TestCase("image/webp", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void Normalize_MapsSupportedTypesAndExtensions(string? value, string? expected)
        => Assert.That(ListingPhotoRules.Normalize(value), Is.EqualTo(expected));

    [Test]
    public void IsAllowedType_WithContentType_AcceptsJpegAndPngOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ListingPhotoRules.IsAllowedType("image/jpeg"), Is.True);
            Assert.That(ListingPhotoRules.IsAllowedType("image/png"), Is.True);
            Assert.That(ListingPhotoRules.IsAllowedType("image/gif"), Is.False);
            Assert.That(ListingPhotoRules.IsAllowedType(null), Is.False);
        });
    }

    [Test]
    public void IsAllowedType_FallsBackToTheFileNameWhenThePickerReportsNoContentType()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ListingPhotoRules.IsAllowedType(null, "phong-ngu.JPG"), Is.True);
            Assert.That(ListingPhotoRules.IsAllowedType("application/octet-stream", "anh.png"), Is.True);
            Assert.That(ListingPhotoRules.IsAllowedType("image/gif", "anh.gif"), Is.False);
            Assert.That(ListingPhotoRules.IsAllowedType(null, "tailieu.pdf"), Is.False);
            Assert.That(ListingPhotoRules.IsAllowedType(null, null), Is.False);
        });
    }

    [TestCase("anh.jpg", ListingPhotoRules.Jpeg)]
    [TestCase("ANH.JPEG", ListingPhotoRules.Jpeg)]
    [TestCase("anh.chup.PNG", ListingPhotoRules.Png)]
    [TestCase("anh.gif", null)]
    [TestCase("anh", null)]
    [TestCase(null, null)]
    public void FromExtension_MapsTheFileExtension(string? fileName, string? expected)
        => Assert.That(ListingPhotoRules.FromExtension(fileName), Is.EqualTo(expected));

    [Test]
    public void IsWithinSizeLimit_AcceptsUnknownSizesAndUpToTenMegabytes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ListingPhotoRules.IsWithinSizeLimit(null), Is.True);
            Assert.That(ListingPhotoRules.IsWithinSizeLimit(1), Is.True);
            Assert.That(ListingPhotoRules.IsWithinSizeLimit(ListingPhotoRules.MaxPhotoBytes), Is.True);
            Assert.That(ListingPhotoRules.IsWithinSizeLimit(ListingPhotoRules.MaxPhotoBytes + 1), Is.False);
            Assert.That(ListingPhotoRules.IsWithinSizeLimit(0), Is.False);
            Assert.That(ListingPhotoRules.IsWithinSizeLimit(-1), Is.False);
        });
    }

    [Test]
    public void Limits_MatchTheUs08Rules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ListingPhotoRules.MinPhotosOnCreate, Is.EqualTo(3));
            Assert.That(ListingPhotoRules.MaxPhotoBytes, Is.EqualTo(10 * 1024 * 1024));
        });
    }
}
