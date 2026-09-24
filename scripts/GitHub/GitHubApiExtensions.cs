using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Godot;

using Microsoft.Extensions.DependencyInjection;

using Mosic.Scripts.Service;

namespace Mosic.Scripts.GitHub;

public static class GitHubApiExtensions
{
    private static readonly IInstaller Installer = ServiceManager.Provider.GetRequiredService<IInstaller>();

    extension(IGitHubApi api)
    {
        public async Task<string> DetermineCurrentVersionAsync()
        {
            foreach (var release in await api.GetMosicReleasesAsync())
            {
                foreach (var hash in release.Assets.Select(asset => asset.GetHash()))
                {
                    if (hash.Algorithm != GitHubConstants.DefaultHashAlgorithm)
                    {
                        GD.PushError($"Unknown hash algorithm: {hash.Algorithm}");
                        continue;
                    }

                    if (hash.Digest == MosicConfig.Digest)
                    {
                        return release.TagName;
                    }
                }
            }

            return "???";
        }

        public async Task<string> DownloadAndInstallUpdateAsync(string downloadUrl)
        {
            string path = Path.Combine(MosicConfig.ProcessDirectory, Path.GetFileName(downloadUrl));
            byte[] bytes = await api.GetByteArrayAsync(downloadUrl);
            return await Installer.InstallAsync(path, bytes);
        }
    }
}
