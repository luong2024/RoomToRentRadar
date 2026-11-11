using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace RoomToRentRadar.Api.Services.Photos;

public class ImageSharpProcessor(IOptions<PhotoOptions> options) : IImageProcessor
{
    private readonly PhotoOptions _opt = options.Value;

    public async Task<ProcessedImage> ProcessAsync(Stream input, bool is360, CancellationToken cancellationToken = default)
    {
        // 1) Nhận dạng định dạng từ NỘI DUNG (magic bytes) - chặn file giả mạo đuôi .jpg.
        var format = await Image.DetectFormatAsync(input, cancellationToken);
        if (format is not (JpegFormat or PngFormat))
            throw new PhotoValidationException("Chỉ chấp nhận ảnh JPEG hoặc PNG hợp lệ.");

        input.Position = 0;

        Image image;
        try
        {
            image = await Image.LoadAsync(input, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException or NotSupportedException)
        {
            throw new PhotoValidationException("Tệp ảnh bị hỏng hoặc không đọc được.");
        }

        using (image)
        {
            // 2) Xoay đúng chiều theo EXIF rồi XÓA metadata (EXIF có thể chứa GPS - riêng tư).
            image.Mutate(x => x.AutoOrient());
            var xmp = is360 ? image.Metadata.XmpProfile : null; // giữ XMP (GPano) để viewer 360 nhận ra panorama
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = xmp;

            // 3) Giảm kích thước, chỉ thu nhỏ - không phóng to.
            var max = is360 ? _opt.Max360Dimension : _opt.MaxDimension;
            if (image.Width > max || image.Height > max)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(max, max),
                    Mode = ResizeMode.Max
                }));
            }

            // 4) Nén sang JPEG (PNG ảnh chụp phòng rất nặng). Ảnh PNG trong suốt được nền trắng.
            if (format is PngFormat)
                image.Mutate(x => x.BackgroundColor(Color.White));

            using var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = _opt.JpegQuality }, cancellationToken);

            return new ProcessedImage(ms.ToArray(), image.Width, image.Height, "image/jpeg", ".jpg");
        }
    }
}
