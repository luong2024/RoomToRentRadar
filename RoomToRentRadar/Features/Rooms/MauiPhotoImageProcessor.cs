using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics.Platform;
using IImage = Microsoft.Maui.Graphics.IImage;
using ImageFormat = Microsoft.Maui.Graphics.ImageFormat;

namespace RoomToRentRadar.Features.Rooms
{
    /// <summary>
    /// MAUI implementation of <see cref="IPhotoImageProcessor"/> for US04 / T04.1.
    /// Uses the platform codec behind Microsoft.Maui.Graphics (Android BitmapFactory, Windows Win2D/WIC),
    /// so no extra NuGet package is needed. PhotoGalleryService (Infrastructure) stays free of MAUI types.
    /// </summary>
    public sealed class MauiPhotoImageProcessor : IPhotoImageProcessor
    {
        private readonly ILogger<MauiPhotoImageProcessor> _logger;

        public MauiPhotoImageProcessor(ILogger<MauiPhotoImageProcessor> logger)
        {
            _logger = logger;
        }

        public Task<ProcessedPhoto?> ProcessAsync(
            byte[] imageBytes, int thumbnailMaxWidth, float jpegQuality, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imageBytes);
            if (thumbnailMaxWidth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(thumbnailMaxWidth));
            }

            // Decoding/encoding a 15 MB photo is CPU-heavy: keep it off the UI thread.
            return Task.Run(() => Process(imageBytes, thumbnailMaxWidth, jpegQuality, cancellationToken), cancellationToken);
        }

        private ProcessedPhoto? Process(byte[] imageBytes, int thumbnailMaxWidth, float jpegQuality, CancellationToken cancellationToken)
        {
            IImage? image;
            try
            {
                using var input = new MemoryStream(imageBytes, writable: false);
                // The format hint is ignored by the Android/Windows decoders; they detect JPEG/PNG/WebP from the bytes.
                image = PlatformImage.FromStream(input, ImageFormat.Jpeg);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Platform decoder could not read the uploaded image.");
                return null;
            }

            if (image is null || image.Width < 1 || image.Height < 1)
            {
                image?.Dispose();
                return null;
            }

            using (image)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Re-encoding drops all metadata (EXIF/GPS). Note: the EXIF orientation tag is lost too.
                var original = EncodeJpeg(image, jpegQuality);

                byte[] thumbnail;
                if (image.Width > thumbnailMaxWidth)
                {
                    float scale = thumbnailMaxWidth / image.Width;
                    using var small = image.Downsize(thumbnailMaxWidth, image.Height * scale, disposeOriginal: false);
                    thumbnail = EncodeJpeg(small, jpegQuality);
                }
                else
                {
                    thumbnail = original; // already small: the thumbnail is the same image
                }

                return new ProcessedPhoto((int)image.Width, (int)image.Height, original, thumbnail);
            }
        }

        private static byte[] EncodeJpeg(IImage image, float quality)
        {
            using var output = new MemoryStream();
            image.Save(output, ImageFormat.Jpeg, quality);
            return output.ToArray();
        }
    }
}
