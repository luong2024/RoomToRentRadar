namespace RoomToRentRadar.Domain.Services;

public static class ImageHeaderReader
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static (int Width, int Height)? TryReadSize(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanSeek)
        {
            return null;
        }

        var origin = content.Position;
        try
        {
            content.Seek(0, SeekOrigin.Begin);

            var signature = new byte[8];
            var read = ReadUpTo(content, signature);
            if (read == signature.Length && signature.AsSpan().SequenceEqual(PngSignature))
            {
                return ReadPngSize(content);
            }

            if (read >= 2 && signature[0] == 0xFF && signature[1] == 0xD8)
            {
                return ReadJpegSize(content);
            }

            return null;
        }
        finally
        {
            content.Seek(origin, SeekOrigin.Begin);
        }
    }

    private static (int Width, int Height)? ReadPngSize(Stream content)
    {
        // 4-byte chunk length + "IHDR" + 4-byte width + 4-byte height.
        var chunk = new byte[16];
        if (!TryReadExactly(content, chunk))
        {
            return null;
        }

        if (chunk[4] != (byte)'I' || chunk[5] != (byte)'H' || chunk[6] != (byte)'D' || chunk[7] != (byte)'R')
        {
            return null;
        }

        var width = ReadBigEndianInt32(chunk, 8);
        var height = ReadBigEndianInt32(chunk, 12);
        return width > 0 && height > 0 ? (width, height) : null;
    }

    private static (int Width, int Height)? ReadJpegSize(Stream content)
    {
        content.Seek(2, SeekOrigin.Begin);

        while (true)
        {
            if (content.ReadByte() != 0xFF)
            {
                return null;
            }

            var marker = content.ReadByte();
            while (marker == 0xFF)
            {
                marker = content.ReadByte();
            }

            if (marker < 0)
            {
                return null;
            }

            if (marker is 0xD9)
            {
                return null;
            }

            // SOI, TEM and RST0-RST7 carry no length field.
            if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            var lengthBytes = new byte[2];
            if (!TryReadExactly(content, lengthBytes))
            {
                return null;
            }

            var length = (lengthBytes[0] << 8) | lengthBytes[1];
            if (length < 2)
            {
                return null;
            }

            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                var frame = new byte[5];
                if (!TryReadExactly(content, frame))
                {
                    return null;
                }

                var height = (frame[1] << 8) | frame[2];
                var width = (frame[3] << 8) | frame[4];
                return width > 0 && height > 0 ? (width, height) : null;
            }

            content.Seek(length - 2, SeekOrigin.Current);
        }
    }

    private static bool TryReadExactly(Stream content, byte[] buffer)
        => ReadUpTo(content, buffer) == buffer.Length;

    private static int ReadUpTo(Stream content, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = content.Read(buffer, offset, buffer.Length - offset);
            if (read <= 0)
            {
                break;
            }

            offset += read;
        }

        return offset;
    }

    private static int ReadBigEndianInt32(byte[] buffer, int offset)
        => (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];
}
