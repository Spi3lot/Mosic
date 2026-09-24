using System;
using System.Collections.Generic;
using System.Linq;

namespace Mosic.Scripts.GitHub;

public record GitHubAsset(string Name, string Digest, string BrowserDownloadUrl)
{
    public static GitHubAsset FindByFileExtension(string extension, IEnumerable<GitHubAsset> assets)
    {
        return assets.FirstOrDefault(asset => asset.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    public (string Algorithm, string Digest) GetHash()
    {
        string[] parts = Digest.Split(':');
        return (parts[0], parts[1]);
    }
}
