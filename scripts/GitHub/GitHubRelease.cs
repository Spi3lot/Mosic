using System.Collections.Generic;

namespace Mosic.Scripts.GitHub;

public record GitHubRelease(string TagName, List<GitHubAsset> Assets);
