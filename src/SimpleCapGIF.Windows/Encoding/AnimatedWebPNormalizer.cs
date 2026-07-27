using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Windows.Encoding;

internal static class AnimatedWebPNormalizer
{
    internal static async Task EnsureAnimatedAsync(string path, RecordedSession session, CancellationToken cancellationToken)
    {
        var data = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (data.Length < 20 || ReadFourCc(data, 0) != "RIFF" || ReadFourCc(data, 8) != "WEBP")
        {
            throw new InvalidDataException(AppStrings.WebpContainerInvalid);
        }

        var chunks = ParseChunks(data);
        if (chunks.Any(static chunk => chunk.Name == "ANIM")) return;
        var imageChunks = chunks.Where(static chunk => chunk.Name is "ALPH" or "VP8 " or "VP8L").ToArray();
        if (imageChunks.Length == 0) throw new InvalidDataException(AppStrings.WebpImageMissing);
        var (width, height) = ReadCanvasSize(data, chunks);

        await using var output = new MemoryStream();
        await output.WriteAsync("RIFF"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(new byte[4], cancellationToken).ConfigureAwait(false);
        await output.WriteAsync("WEBP"u8.ToArray(), cancellationToken).ConfigureAwait(false);

        var vp8x = new byte[10];
        vp8x[0] = (byte)(0x02 | (imageChunks.Any(static chunk => chunk.Name == "ALPH") ? 0x10 : 0));
        WriteUInt24(vp8x, 4, width - 1);
        WriteUInt24(vp8x, 7, height - 1);
        WriteChunk(output, "VP8X", vp8x);
        WriteChunk(output, "ANIM", new byte[6]);

        const int maximumFrameDurationMilliseconds = 0xFFFFFF;
        var totalDurationMilliseconds = Math.Max(1L, checked((long)Math.Round(session.Duration.TotalMilliseconds)));
        var remainingDuration = totalDurationMilliseconds;
        while (remainingDuration > 0)
        {
            var duration = (int)Math.Min(remainingDuration, maximumFrameDurationMilliseconds);
            using var framePayload = new MemoryStream();
            var header = new byte[16];
            WriteUInt24(header, 6, width - 1);
            WriteUInt24(header, 9, height - 1);
            WriteUInt24(header, 12, duration);
            framePayload.Write(header);
            foreach (var chunk in imageChunks)
            {
                framePayload.Write(data, chunk.Offset, chunk.TotalLength);
            }

            WriteChunk(output, "ANMF", framePayload.ToArray());
            remainingDuration -= duration;
        }

        var result = output.ToArray();
        BitConverter.GetBytes((uint)(result.Length - 8)).CopyTo(result, 4);
        var temporaryPath = path + ".normalize";
        await File.WriteAllBytesAsync(temporaryPath, result, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static WebPChunk[] ParseChunks(byte[] data)
    {
        var chunks = new List<WebPChunk>();
        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var size = checked((int)BitConverter.ToUInt32(data, offset + 4));
            var totalLength = checked(8 + size + (size & 1));
            if (offset + totalLength > data.Length) throw new InvalidDataException(AppStrings.WebpChunkInvalid);
            chunks.Add(new WebPChunk(ReadFourCc(data, offset), offset, size, totalLength));
            offset += totalLength;
        }

        return chunks.ToArray();
    }

    private static (int Width, int Height) ReadCanvasSize(byte[] data, WebPChunk[] chunks)
    {
        var extended = chunks.FirstOrDefault(static chunk => chunk.Name == "VP8X");
        if (extended is not null && extended.Size >= 10)
        {
            return (ReadUInt24(data, extended.Offset + 12) + 1, ReadUInt24(data, extended.Offset + 15) + 1);
        }

        var lossy = chunks.FirstOrDefault(static chunk => chunk.Name == "VP8 ");
        if (lossy is not null && lossy.Size >= 10)
        {
            var payload = lossy.Offset + 8;
            return (BitConverter.ToUInt16(data, payload + 6) & 0x3fff, BitConverter.ToUInt16(data, payload + 8) & 0x3fff);
        }

        var lossless = chunks.FirstOrDefault(static chunk => chunk.Name == "VP8L");
        if (lossless is not null && lossless.Size >= 5)
        {
            var payload = lossless.Offset + 8;
            return (
                1 + data[payload + 1] + ((data[payload + 2] & 0x3f) << 8),
                1 + ((data[payload + 2] >> 6) | (data[payload + 3] << 2) | ((data[payload + 4] & 0x0f) << 10)));
        }

        throw new InvalidDataException(AppStrings.WebpCanvasMissing);
    }

    private static void WriteChunk(Stream output, string name, byte[] payload)
    {
        output.Write(System.Text.Encoding.ASCII.GetBytes(name));
        output.Write(BitConverter.GetBytes((uint)payload.Length));
        output.Write(payload);
        if ((payload.Length & 1) != 0) output.WriteByte(0);
    }

    private static string ReadFourCc(byte[] data, int offset) => System.Text.Encoding.ASCII.GetString(data, offset, 4);
    private static int ReadUInt24(byte[] data, int offset) => data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);

    private static void WriteUInt24(byte[] data, int offset, int value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)(value >> 16);
    }

    private sealed record WebPChunk(string Name, int Offset, int Size, int TotalLength);
}
