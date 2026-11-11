using System.Text;
using SkiaSharp;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public static class TestImageFactory
{
    public const int DefaultWidth = 800;
    public const int DefaultHeight = 600;

    public static byte[] CreateJpeg(int width = DefaultWidth, int height = DefaultHeight)
        => Encode(width, height, SKEncodedImageFormat.Jpeg);

    public static byte[] CreatePng(int width = DefaultWidth, int height = DefaultHeight)
        => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] CreateWebp(int width = DefaultWidth, int height = DefaultHeight)
        => Encode(width, height, SKEncodedImageFormat.Webp);

    public static byte[] CreateTransparentPng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
        }

        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    public static byte[] CreateCorruptJpeg()
    {
        var content = new byte[512];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;

        for (var index = 3; index < content.Length; index++)
        {
            content[index] = (byte)(index * 37 % 251);
        }

        return content;
    }

    public static byte[] CreateJpegWithOrientation(int width, int height, byte orientation)
    {
        var jpeg = CreateJpeg(width, height);
        var app1 = CreateExifApp1(orientation);

        var content = new byte[jpeg.Length + app1.Length];
        content[0] = jpeg[0];
        content[1] = jpeg[1];
        app1.CopyTo(content, 2);
        jpeg.AsSpan(2).CopyTo(content.AsSpan(2 + app1.Length));
        return content;
    }

    public static byte[] CreateText()
        => Encoding.UTF8.GetBytes("this is not an image");

    private static byte[] CreateExifApp1(byte orientation) =>
    [
        0xFF, 0xE1, 0x00, 0x22,
        (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
        0x49, 0x49, 0x2A, 0x00,
        0x08, 0x00, 0x00, 0x00,
        0x01, 0x00,
        0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, orientation, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00
    ];

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
            using var paint = new SKPaint { Color = SKColors.OrangeRed, IsAntialias = true };
            canvas.DrawRect(new SKRect(0, 0, width / 2f, height / 2f), paint);
        }

        return Encode(bitmap, format);
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        Assert.That(data, Is.Not.Null, $"SkiaSharp could not encode {format}.");
        return data!.ToArray();
    }
}
