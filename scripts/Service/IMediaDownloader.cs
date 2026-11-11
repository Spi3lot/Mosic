using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using YoutubeExplode.Videos;

namespace Mosic.Scripts.Service;

public interface IMediaDownloader
{
    IReadOnlyList<string> AudioFormats { get; }

    IReadOnlyList<string> VideoFormats { get; }

    bool SupportsAutoUpdateAndSetup { get; }

    Task EnsureDependenciesAsync(IProgress<double> progress = null, CancellationToken cancellationToken = default);

    Task DownloadAsync(
        VideoId videoId,
        string cleanTitle,
        string outputFolder,
        bool isVideo,
        int formatIndex,
        IProgress<double> progress = null,
        CancellationToken cancellationToken = default
    );
}
