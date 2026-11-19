namespace RoomToRentRadar.Features.ListingManagement.Models;

public static class ListingPhotoSelectionValidator
{
    public const long MaxFileBytes = 10L * 1024 * 1024;

    public static async Task<string> CopyValidatedAsync(string fileName, Stream source, Stream destination,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
            throw new InvalidDataException("Chỉ chọn ảnh JPG hoặc PNG.");
        var buffer = new byte[81920];
        var header = new byte[8];
        var headerCount = 0;
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxFileBytes) throw new InvalidDataException("Mỗi ảnh phải có dung lượng tối đa 10 MB.");
            var take = Math.Min(8 - headerCount, read);
            buffer.AsSpan(0, take).CopyTo(header.AsSpan(headerCount));
            headerCount += take;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var isPng = headerCount == 8 && header.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var isJpeg = headerCount >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        if ((extension == ".png" && !isPng) || (extension != ".png" && !isJpeg))
            throw new InvalidDataException("Nội dung ảnh không khớp định dạng JPG hoặc PNG.");
        return isPng ? "image/png" : "image/jpeg";
    }
}
