#if !GODOT_PC
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Mosic.Scripts.Service;

public class AndroidMediaDownloader(YoutubeClient youtube) : IMediaDownloader
{
    public IReadOnlyList<string> AudioFormats { get; } = ["m4a", "webm"];

    public IReadOnlyList<string> VideoFormats { get; } = ["mp4", "webm"];

    public bool SupportsAutoUpdateAndSetup => false;

    public Task EnsureDependenciesAsync(IProgress<double> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task DownloadAsync(
        VideoId videoId,
        string cleanTitle,
        string outputFolder,
        bool isVideo,
        int formatIndex,
        IProgress<double> progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var formats = isVideo ? VideoFormats : AudioFormats;
        int clampedIndex = Math.Clamp(formatIndex, 0, formats.Count - 1);
        string requestedFormat = formats[clampedIndex];

        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId, cancellationToken);
        IStreamInfo streamInfo;
        string fileExtension;

        if (isVideo)
        {
            var muxedStreams = streamManifest.GetMuxedStreams();

            streamInfo = muxedStreams
                .Where(s => string.Equals(s.Container.Name, requestedFormat, StringComparison.OrdinalIgnoreCase))
                .TryGetWithHighestVideoQuality()
                ?? muxedStreams.TryGetWithHighestVideoQuality();

            if (streamInfo is null)
            {
                throw new InvalidOperationException("No suitable muxed video stream found for this video.");
            }

            fileExtension = streamInfo.Container.Name;
        }
        else
        {
            string targetContainer = requestedFormat == "m4a" ? "mp4" : requestedFormat;
            var audioStreams = streamManifest.GetAudioOnlyStreams();

            streamInfo = audioStreams
                .Where(s => string.Equals(s.Container.Name, targetContainer, StringComparison.OrdinalIgnoreCase))
                .TryGetWithHighestBitrate()
                ?? audioStreams.TryGetWithHighestBitrate();

            if (streamInfo is null)
            {
                throw new InvalidOperationException("No suitable audio stream found for this video.");
            }

            fileExtension = string.Equals(streamInfo.Container.Name, "mp4", StringComparison.OrdinalIgnoreCase)
                ? "m4a"
                : streamInfo.Container.Name;
        }

        Directory.CreateDirectory(outputFolder);
        string outputPath = Path.Combine(outputFolder, $"{cleanTitle}.{fileExtension}");
        await youtube.Videos.Streams.DownloadAsync(streamInfo, outputPath, progress, cancellationToken);
    }
}
#endif
