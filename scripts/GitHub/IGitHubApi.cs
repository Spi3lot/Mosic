using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Refit;

namespace Mosic.Scripts.GitHub;

[Headers("User-Agent: Mosic")]
public interface IGitHubApi
{
    [Get("/repos/Spi3lot/Mosic/releases")]
    Task<List<GitHubRelease>> GetMosicReleasesAsync();

    [Get("/repos/Spi3lot/Mosic/releases/latest")]
    Task<GitHubRelease> GetLatestMosicReleaseAsync();

    [Get("")]
    Task<Stream> GetStreamAsync([Url] string url);
}
