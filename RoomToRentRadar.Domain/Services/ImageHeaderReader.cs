namespace RoomToRentRadar.Domain.Services;

/// <summary>
/// Reads the pixel size of a JPEG (SOF0/SOF1/SOF2) or PNG (IHDR) from its binary header without
/// any third-party image library (zero-NuGet decision, BR-MED-02). Never throws: malformed or
/// unsupported input returns false so the caller can answer <c>415 UNSUPPORTED_MEDIA_TYPE</c>.
/// </summary>
public static class ImageHeaderReader
{
    private const int MaxBufferedBytes = 16 * 1024 * 1024;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Reads <paramref name="width"/> and <paramref name="height"/> from the stream header. A seekable
    /// stream is read from offset 0 and its original position is restored; a non-seekable stream is
    /// buffered internally (and therefore consumed).
    /// </summary>
    public static bool TryReadSize(Stream content, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (content is null)
        {
            return false;
        }

        try
        {
            if (!content.CanRead)
            {
                return false;
            }

            if (content.CanSeek)
            {
                var originalPosition = content.Position;
                try
                {
                    content.Position = 0;
                    return TryReadSizeCore(content, ref width, ref height);
                }
                finally
                {
                    content.Position = originalPosition;
                }
            }

            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while (buffer.Length < MaxBufferedBytes &&
                   (read = content.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Write(chunk, 0, read);
            }

            buffer.Position = 0;
            return TryReadSizeCore(buffer, ref width, ref height);
        }
        catch (Exception)
        {
            width = 0;
            height = 0;
            return false;
        }
    }

    private static bool TryReadSizeCore(Stream stream, ref int width, ref int height)
    {
        if (!stream.CanSeek)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[8];
        var read = ReadUpTo(stream, header);
        if (read < 2)
        {
            return false;
        }

        if (header[0] == 0xFF && header[1] == 0xD8)
        {
            stream.Position = 2;
            return TryReadJpegSize(stream, ref width, ref height);
        }

        if (read == 8 && header.SequenceEqual(PngSignature))
        {
            stream.Position = 8;
            return TryReadPngSize(stream, ref width, ref height);
        }

        return false;
    }

    private static bool TryReadJpegSize(Stream stream, ref int width, ref int height)
    {
        while (true)
        {
            var prefix = stream.ReadByte();
            if (prefix != 0xFF)
            {
                return false;
            }

            var marker = stream.ReadByte();
            while (marker == 0xFF)
            {
                marker = stream.ReadByte();
            }

            if (marker < 0)
            {
                return false;
            }

            if (marker == 0xD8 || marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                continue; // standalone markers carry no length
            }

            if (marker == 0xD9)
            {
                return false; // EOI before any SOF segment
            }

            var lengthHi = stream.ReadByte();
            var lengthLo = stream.ReadByte();
            if (lengthHi < 0 || lengthLo < 0)
            {
                return false;
            }

            var length = (lengthHi << 8) | lengthLo;
            if (length < 2)
            {
                return false;
            }

            if (marker is 0xC0 or 0xC1 or 0xC2) // SOF0 baseline, SOF1 extended, SOF2 progressive
            {
                var precision = stream.ReadByte();
                var heightHi = stream.ReadByte();
                var heightLo = stream.ReadByte();
                var widthHi = stream.ReadByte();
                var widthLo = stream.ReadByte();
                if (precision < 0 || heightHi < 0 || heightLo < 0 || widthHi < 0 || widthLo < 0)
                {
                    return false;
                }

                var parsedHeight = (heightHi << 8) | heightLo;
                var parsedWidth = (widthHi << 8) | widthLo;
                if (parsedWidth <= 0 || parsedHeight <= 0)
                {
                    return false;
                }

                width = parsedWidth;
                height = parsedHeight;
                return true;
            }

            if (!Skip(stream, length - 2))
            {
                return false;
            }
        }
    }

    private static bool TryReadPngSize(Stream stream, ref int width, ref int height)
    {
        var chunkLength = ReadBigEndianUInt32(stream);
        if (chunkLength < 8)
        {
            return false;
        }

        var chunkType = new byte[4];
        if (ReadUpTo(stream, chunkType) != 4 ||
            chunkType[0] != (byte)'I' || chunkType[1] != (byte)'H' ||
            chunkType[2] != (byte)'D' || chunkType[3] != (byte)'R')
        {
            return false;
        }

        var parsedWidth = ReadBigEndianUInt32(stream);
        var parsedHeight = ReadBigEndianUInt32(stream);
        if (parsedWidth is <= 0 or > int.MaxValue || parsedHeight is <= 0 or > int.MaxValue)
        {
            return false;
        }

        width = (int)parsedWidth;
        height = (int)parsedHeight;
        return true;
    }

    private static long ReadBigEndianUInt32(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (ReadUpTo(stream, bytes) != 4)
        {
            return -1;
        }

        return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
    }

    private static int ReadUpTo(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static bool Skip(Stream stream, int count)
    {
        if (count <= 0)
        {
            return count == 0;
        }

        if (stream.CanSeek)
        {
            if (stream.Position + count > stream.Length)
            {
                return false;
            }

            stream.Position += count;
            return true;
        }

        Span<byte> scratch = stackalloc byte[256];
        var remaining = count;
        while (remaining > 0)
        {
            var read = stream.Read(scratch[..Math.Min(remaining, scratch.Length)]);
            if (read <= 0)
            {
                return false;
            }

            remaining -= read;
        }

        return true;
    }
}
