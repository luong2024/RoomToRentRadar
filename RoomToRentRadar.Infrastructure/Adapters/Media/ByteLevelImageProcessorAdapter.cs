using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Media;

/// <summary>
/// Zero-NuGet EXIF stripper (BR-MED-04, ADR-008 user decision): removes JPEG <c>APP1</c> segments
/// (Exif/GPS and XMP metadata) and PNG <c>eXIf</c> chunks without resizing or re-encoding pixels.
/// Malformed input is returned unchanged so the upload use case can reject it on its own checks.
/// </summary>
public sealed class ByteLevelImageProcessorAdapter : IImageProcessorPort
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public async Task<Stream> ProcessAndStripExifAsync(Stream input, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct).ConfigureAwait(false);
        var original = buffer.ToArray();

        var cleaned = ChooseFormat(extension, original) switch
        {
            ImageFormat.Jpeg => StripJpegMetadata(original),
            ImageFormat.Png => StripPngMetadata(original),
            _ => original
        };

        return new MemoryStream(cleaned, writable: false);
    }

    private static ImageFormat ChooseFormat(string extension, byte[] data)
    {
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
            data.AsSpan().StartsWith(PngSignature))
        {
            return ImageFormat.Png;
        }

        if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
            (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF))
        {
            return ImageFormat.Jpeg;
        }

        return ImageFormat.Unknown;
    }

    // ----------------------------------------------------------------- JPEG

    private static byte[] StripJpegMetadata(byte[] data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return data;
        }

        var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2); // SOI

        var index = 2;
        while (index < data.Length)
        {
            if (data[index] != 0xFF)
            {
                return data; // malformed header: leave the file untouched
            }

            var markerStart = index;
            index++;
            while (index < data.Length && data[index] == 0xFF)
            {
                index++; // optional fill bytes
            }

            if (index >= data.Length)
            {
                return data;
            }

            var marker = data[index];
            index++;

            if (marker == 0xD8 || marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                output.Write(data, markerStart, index - markerStart); // standalone marker
                continue;
            }

            if (marker == 0xD9)
            {
                output.Write(data, markerStart, index - markerStart); // EOI
                return output.ToArray();
            }

            if (index + 2 > data.Length)
            {
                return data;
            }

            var segmentLength = (data[index] << 8) | data[index + 1];
            if (segmentLength < 2)
            {
                return data;
            }

            var segmentEnd = index + segmentLength;
            if (segmentEnd > data.Length)
            {
                return data;
            }

            // APP1 carries EXIF (incl. GPS) and XMP; dropping it is the metadata strip of BR-MED-04.
            if (marker != 0xE1)
            {
                output.Write(data, markerStart, segmentEnd - markerStart);
            }

            if (marker == 0xDA)
            {
                // Start of scan: the entropy-coded image data follows and is copied verbatim.
                output.Write(data, segmentEnd, data.Length - segmentEnd);
                return output.ToArray();
            }

            index = segmentEnd;
        }

        return data; // no EOI -> not a complete JPEG, keep the original
    }

    // ----------------------------------------------------------------- PNG

    private static byte[] StripPngMetadata(byte[] data)
    {
        if (data.Length < 8 || !data.AsSpan().StartsWith(PngSignature))
        {
            return data;
        }

        var output = new MemoryStream(data.Length);
        output.Write(data, 0, 8); // signature

        var index = 8;
        while (index + 8 <= data.Length)
        {
            var dataLength = ReadBigEndianUInt32(data, index);
            var chunkLength = 4L + 4L + dataLength + 4L; // length + type + data + CRC
            if (chunkLength > data.Length - index)
            {
                return data; // truncated chunk
            }

            var typeOffset = index + 4;
            var isExifChunk =
                data[typeOffset] == (byte)'e' &&
                data[typeOffset + 1] == (byte)'X' &&
                data[typeOffset + 2] == (byte)'I' &&
                data[typeOffset + 3] == (byte)'f';
            var isEnd = data[typeOffset] == (byte)'I' &&
                        data[typeOffset + 1] == (byte)'E' &&
                        data[typeOffset + 2] == (byte)'N' &&
                        data[typeOffset + 3] == (byte)'D';

            if (!isExifChunk)
            {
                output.Write(data, index, (int)chunkLength);
            }

            index += (int)chunkLength;

            if (isEnd)
            {
                return output.ToArray();
            }
        }

        return data; // no IEND -> incomplete PNG, keep the original
    }

    private static long ReadBigEndianUInt32(byte[] data, int offset) =>
        ((long)data[offset] << 24) |
        ((long)data[offset + 1] << 16) |
        ((long)data[offset + 2] << 8) |
        data[offset + 3];

    private enum ImageFormat
    {
        Unknown,
        Jpeg,
        Png
    }
}
