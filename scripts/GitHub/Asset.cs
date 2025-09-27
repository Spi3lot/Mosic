using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Mosic.Scripts.GitHub;

public static class Asset
{
    public static (string Algorithm, string Digest) GetHash(JToken asset)
    {
        string[] digest = asset["digest"].ToString().Split(':');
        return (digest[0], digest[1]);
    }

    public static JToken FindByFileExtension(string extension, IEnumerable<JToken> assets)
    {
        return assets.FirstOrDefault(asset => asset["name"].ToString().EndsWith(
            extension,
            StringComparison.OrdinalIgnoreCase
        ));
    }
}