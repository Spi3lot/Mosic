using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using Mosic.Scripts.Service;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosic.Scripts.GitHub;

public static class Api
{
    private const string BaseUrl = "https://api.github.com/repos/Spi3lot/Mosic/";

    private static class Endpoint
    {
        public const string Releases = "releases";

        public const string LatestRelease = "releases/latest";
    }

    public static class Helper
    {
        private static readonly System.Net.Http.HttpClient HttpClient = new();
        
        private static readonly ArchiveInstaller Installer = new();

        static Helper()
        {
            HttpClient.BaseAddress = new Uri(BaseUrl);
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(nameof(Mosic));
        }

        public static async Task<string> DetermineCurrentVersionAsync()
        {
            foreach (var release in await GetReleasesAsync())
            {
                foreach (var asset in release["assets"]!)
                {
                    var hash = Asset.GetHash(asset);

                    if (hash.Algorithm != Constants.DefaultHashAlgorithm)
                    {
                        GD.PushError($"Unknown hash algorithm: {hash.Algorithm}");
                        continue;
                    }

                    if (hash.Digest == MosicConfig.Digest)
                    {
                        return release["tag_name"]!.ToString();
                    }
                }
            }

            return "???";
        }

        public static async Task<string> DownloadAndInstallUpdateAsync(string downloadUrl)
        {
            string path = Path.Combine(MosicConfig.ProcessDirectory, Path.GetFileName(downloadUrl));
            byte[] bytes = await HttpClient.GetByteArrayAsync(downloadUrl);
            return await Installer.InstallAsync(path, bytes);
        }
        
        public static async Task<JToken> GetReleasesAsync()
        {
            string json = await HttpClient.GetStringAsync(Endpoint.Releases);
            return JsonConvert.DeserializeObject<JToken>(json);
        }
        
        public static async Task<JToken> GetLatestReleaseAsync()
        {
            string json = await HttpClient.GetStringAsync(Endpoint.LatestRelease);
            return JsonConvert.DeserializeObject<JToken>(json);
        }
    }
}