using RoomToRentRadar.UseCases.Reports;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class ReportDetailsSanitizerTests
{
    [Test]
    public void Sanitize_NullOrWhitespace_ReturnsNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReportDetailsSanitizer.Sanitize(null), Is.Null);
            Assert.That(ReportDetailsSanitizer.Sanitize("   "), Is.Null);
            Assert.That(ReportDetailsSanitizer.Sanitize("\r\n\t"), Is.Null);
        });
    }

    [Test]
    public void Sanitize_TrimsAndKeepsPlainText()
    {
        var sanitized = ReportDetailsSanitizer.Sanitize("  Căn hộ không đúng mô tả.  ");

        Assert.That(sanitized, Is.EqualTo("Căn hộ không đúng mô tả."));
    }

    [Test]
    public void Sanitize_RemovesTagsButKeepsText()
    {
        var sanitized = ReportDetailsSanitizer.Sanitize(
            "<b>Hello</b> <a href=\"https://example.test\">world</a>");

        Assert.That(sanitized, Is.EqualTo("Hello world"));
    }

    [Test]
    public void Sanitize_RemovesScriptAndStyleBlocks()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ReportDetailsSanitizer.Sanitize("Nice room<script>alert('x')</script>"),
                Is.EqualTo("Nice room"));
            Assert.That(
                ReportDetailsSanitizer.Sanitize("<style>body{display:none}</style>Visible"),
                Is.EqualTo("Visible"));
        });
    }

    [Test]
    public void Sanitize_DecodesAndStripsEncodedMarkup()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ReportDetailsSanitizer.Sanitize("&lt;b&gt;bold&lt;/b&gt;"),
                Is.EqualTo("bold"));
            Assert.That(
                ReportDetailsSanitizer.Sanitize("&lt;script&gt;alert(1)&lt;/script&gt;"),
                Is.EqualTo("alert(1)"));
        });
    }

    [Test]
    public void Sanitize_DropsControlCharactersButKeepsNewlines()
    {
        var sanitized = ReportDetailsSanitizer.Sanitize("line1\u0000line2\r\nline3");

        Assert.That(sanitized, Is.EqualTo("line1line2\nline3"));
    }

    [Test]
    public void Sanitize_AcceptsExactlyMaximumLength()
    {
        var details = new string('a', ReportDetailsSanitizer.MaximumLength);

        var sanitized = ReportDetailsSanitizer.Sanitize(details);

        Assert.That(sanitized, Has.Length.EqualTo(ReportDetailsSanitizer.MaximumLength));
    }

    [Test]
    public void Sanitize_KeepsLongTextForValidationByCaller()
    {
        var details = new string('a', ReportDetailsSanitizer.MaximumLength + 1);

        var sanitized = ReportDetailsSanitizer.Sanitize(details);

        Assert.That(sanitized, Has.Length.EqualTo(ReportDetailsSanitizer.MaximumLength + 1));
    }
}
