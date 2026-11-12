using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace RoomToRentRadar.Api.Services.Photos;

public record ProcessedImage(byte[] Content, byte[] Thumbnail, int Width, int Height);

public interface IImageProcessor
{
    /// <summary>Kiểm tra nội dung thật là JPEG/PNG, (nếu 360 thì tỉ lệ ~2:1), rồi nén + tạo thumbnail.</summary>
    Task<ProcessedImage> ProcessAsync(byte[] original, string fileName, bool is360, CancellationToken ct);
}

public class ImageSharpProcessor(IOptions<PhotoOptions> options) : IImageProcessor
{
    private readonly PhotoOptions _o = options.Value;

    public async Task<ProcessedImage> ProcessAsync(byte[] original, string fileName, bool is360, CancellationToken ct)
    {
        Image image;
        try
        {
            var format = Image.DetectFormat(original);
            if (format is null || (format.Name != "JPEG" && format.Name != "PNG"))
                throw new PhotoValidationException($"'{fileName}' không phải ảnh JPEG/PNG hợp lệ.");

            image = Image.Load(original);
        }
        catch (PhotoValidationException) { throw; }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new PhotoValidationException($"'{fileName}' không phải ảnh hợp lệ hoặc đã bị hỏng.");
        }

        using (image)
        {
            if (is360)
            {
                double ratio = image.Width / (double)image.Height;
                if (ratio < 1.9 || ratio > 2.1)
                    throw new PhotoValidationException($"Ảnh 360° '{fileName}' phải có tỉ lệ 2:1 (equirectangular).");
            }

            int maxWidth = is360 ? _o.Max360Width : _o.MaxImageWidth;

            image.Mutate(x =>
            {
                x.AutoOrient();
                x.BackgroundColor(Color.White);
            });

            if (image.Width > maxWidth)
                image.Mutate(x => x.Resize(maxWidth, 0));

            var encoder = new JpegEncoder { Quality = _o.JpegQuality };

            using var main = new MemoryStream();
            await image.SaveAsJpegAsync(main, encoder, ct);

            using var thumb = image.Clone(x =>
            {
                if (image.Width > _o.ThumbnailWidth) x.Resize(_o.ThumbnailWidth, 0);
            });
            using var thumbMs = new MemoryStream();
            await thumb.SaveAsJpegAsync(thumbMs, new JpegEncoder { Quality = 70 }, ct);

            return new ProcessedImage(main.ToArray(), thumbMs.ToArray(), image.Width, image.Height);
        }
    }
}
