#if GODOT_PC
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xabe.FFmpeg.Downloader;

using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Mosic.Scripts.Service;

public class DesktopMediaDownloader(YoutubeClient youtube) : IMediaDownloader
{
    public IReadOnlyList<string> AudioFormats { get; } =
        [.. Enum.GetNames<Formats.Audio>().Select(f => f.ToLowerInvariant())];

    public IReadOnlyList<string> VideoFormats { get; } =
        [.. Enum.GetNames<Formats.Video>().Select(f => f.ToLowerInvariant())];

    public bool SupportsAutoUpdateAndSetup => true;

    public async Task EnsureDependenciesAsync(IProgress<double> progress = null, CancellationToken cancellationToken = default)
    {
        if (File.Exists(MosicConfig.FFmpegPath))
        {
            return;
        }

        var ffmpegProgress = new Progress<Xabe.FFmpeg.ProgressInfo>(info =>
        {
            if (info.TotalBytes > 0)
            {
                progress?.Report((double) info.DownloadedBytes / info.TotalBytes);
            }
        });

        await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, MosicConfig.ProcessDirectory, ffmpegProgress);
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
        string containerName = formats[clampedIndex];

        Directory.CreateDirectory(outputFolder);
        string outputPath = Path.Combine(outputFolder, $"{cleanTitle}.{containerName}");

        await youtube.Videos.DownloadAsync(
            videoId,
            outputPath,
            builder => builder
                .SetFFmpegPath(MosicConfig.FFmpegPath)
                .SetContainer(new Container(containerName))
                .SetPreset(ConversionPreset.UltraFast),
            progress,
            cancellationToken
        );
    }
}
#endif
