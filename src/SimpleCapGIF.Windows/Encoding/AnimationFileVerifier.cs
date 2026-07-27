using System.Diagnostics;
using System.Text.Json;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Windows.Encoding;

internal static class AnimationFileVerifier
{
    internal static async Task VerifyAsync(FfmpegToolchain toolchain, AnimationFormat format, string path, RecordedSession session, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            throw new InvalidDataException(AppStrings.EmptyEncodingResult);
        }

        if (format == AnimationFormat.WebP)
        {
            VerifyAnimatedWebP(path, session);
            return;
        }

        VerifyGifInfiniteLoop(path);

        var arguments = new[]
        {
            "-v", "error", "-select_streams", "v:0", "-count_frames",
            "-show_entries", "stream=codec_name,nb_read_frames:format=format_name,duration",
            "-of", "json", path,
        };
        using var process = FfmpegProcess.Start(toolchain.FfprobePath, arguments, redirectInput: false);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var exitCode = await FfmpegProcess.WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidDataException(AppStrings.Format(AppStrings.GifVerificationFailedFormat, error));
        }

        using var document = JsonDocument.Parse(output);
        var stream = document.RootElement.GetProperty("streams")[0];
        var codec = stream.GetProperty("codec_name").GetString();
        var frameCount = long.Parse(stream.GetProperty("nb_read_frames").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var duration = double.Parse(document.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        ValidateTimeline(codec == "gif", frameCount, TimeSpan.FromSeconds(duration), session, allowFrameCoalescing: false);
    }

    private static void VerifyGifInfiniteLoop(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 13 ||
            !data.AsSpan(0, 6).SequenceEqual("GIF87a"u8) &&
            !data.AsSpan(0, 6).SequenceEqual("GIF89a"u8))
        {
            throw new InvalidDataException(AppStrings.GifHeaderMissing);
        }

        var position = 13;
        var logicalScreenPacked = data[10];
        if ((logicalScreenPacked & 0x80) != 0)
        {
            position = checked(position + (3 * (2 << (logicalScreenPacked & 0x07))));
        }

        while (position < data.Length)
        {
            var marker = data[position++];
            if (marker == 0x3B) break;
            if (marker == 0x2C)
            {
                EnsureRemaining(data, position, 9);
                var imagePacked = data[position + 8];
                position += 9;
                if ((imagePacked & 0x80) != 0)
                {
                    position = checked(position + (3 * (2 << (imagePacked & 0x07))));
                }

                EnsureRemaining(data, position, 1);
                position++;
                SkipSubBlocks(data, ref position);
                continue;
            }

            if (marker != 0x21)
            {
                throw new InvalidDataException(AppStrings.GifBlockInvalid);
            }

            EnsureRemaining(data, position, 1);
            var extensionLabel = data[position++];
            if (extensionLabel != 0xFF)
            {
                SkipSubBlocks(data, ref position);
                continue;
            }

            EnsureRemaining(data, position, 1);
            var applicationLength = data[position++];
            EnsureRemaining(data, position, applicationLength);
            var application = data.AsSpan(position, applicationLength);
            position += applicationLength;
            var loopApplication = application.SequenceEqual("NETSCAPE2.0"u8) || application.SequenceEqual("ANIMEXTS1.0"u8);
            while (true)
            {
                EnsureRemaining(data, position, 1);
                var blockLength = data[position++];
                if (blockLength == 0) break;
                EnsureRemaining(data, position, blockLength);
                if (loopApplication && blockLength >= 3 && data[position] == 1 && data[position + 1] == 0 && data[position + 2] == 0)
                {
                    return;
                }

                position += blockLength;
            }
        }

        throw new InvalidDataException(AppStrings.GifLoopMissing);
    }

    private static void SkipSubBlocks(byte[] data, ref int position)
    {
        while (true)
        {
            EnsureRemaining(data, position, 1);
            var length = data[position++];
            if (length == 0) return;
            EnsureRemaining(data, position, length);
            position += length;
        }
    }

    private static void EnsureRemaining(byte[] data, int position, int length)
    {
        if (position < 0 || length < 0 || position > data.Length - length)
        {
            throw new InvalidDataException(AppStrings.GifTruncated);
        }
    }

    private static void VerifyAnimatedWebP(string path, RecordedSession session)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: false);
        if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException(AppStrings.WebpRiffMissing);
        _ = reader.ReadUInt32();
        if (new string(reader.ReadChars(4)) != "WEBP") throw new InvalidDataException(AppStrings.WebpContainerInvalid);

        long frames = 0;
        long durationMilliseconds = 0;
        var hasAnimation = false;
        var infiniteLoop = false;
        while (stream.Position + 8 <= stream.Length)
        {
            var chunk = new string(reader.ReadChars(4));
            var size = reader.ReadUInt32();
            var payloadStart = stream.Position;
            if (payloadStart + size > stream.Length) throw new InvalidDataException(AppStrings.WebpChunkInvalid);
            if (chunk == "ANIM" && size >= 6)
            {
                hasAnimation = true;
                _ = reader.ReadUInt32();
                infiniteLoop = reader.ReadUInt16() == 0;
            }
            else if (chunk == "ANMF" && size >= 16)
            {
                frames++;
                stream.Position = payloadStart + 12;
                durationMilliseconds += reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadByte() << 16);
            }

            stream.Position = payloadStart + size + (size & 1);
        }

        if (!hasAnimation || !infiniteLoop) throw new InvalidDataException(AppStrings.WebpAnimationMissing);
        ValidateTimeline(true, frames, TimeSpan.FromMilliseconds(durationMilliseconds), session, allowFrameCoalescing: true);
    }

    private static void ValidateTimeline(bool correctFormat, long frameCount, TimeSpan duration, RecordedSession session, bool allowFrameCoalescing)
    {
        if (!correctFormat) throw new InvalidDataException(AppStrings.WrongAnimationFormat);
        if (allowFrameCoalescing)
        {
            if (frameCount <= 0 || frameCount > session.FrameCount)
            {
                throw new InvalidDataException(AppStrings.Format(AppStrings.WebpFrameCountFormat, session.FrameCount, frameCount));
            }
        }
        else if (frameCount != session.FrameCount)
        {
            throw new InvalidDataException(AppStrings.Format(AppStrings.FrameCountFormat, session.FrameCount, frameCount));
        }

        var tolerance = TimeSpan.FromSeconds(1d / session.FramesPerSecond);
        if ((duration - session.Duration).Duration() > tolerance)
        {
            throw new InvalidDataException(AppStrings.Format(AppStrings.DurationFormat, session.Duration, duration));
        }
    }
}
