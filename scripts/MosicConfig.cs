using System;
using System.IO;

using Godot;

namespace Mosic.Scripts;

public partial class MosicConfig : Resource
{
    private const string ConfigPath = "user://config.tres";

    public static readonly string ProcessPath = OS.GetExecutablePath();
    public static readonly string ProcessDirectory = Path.GetDirectoryName(ProcessPath) ?? string.Empty;
    public static readonly string DefaultOutputFolder = GetDefaultOutputFolder();
    public static readonly string FFmpegPath = Path.Combine(ProcessDirectory, OS.GetName() == "Windows" ? "ffmpeg.exe" : "ffmpeg");

    public static string Digest => field ??= Godot.FileAccess.GetSha256(ProcessPath);

    public static string Version { get; set; }

    [Export] public string OutputFolder { get; set; } = DefaultOutputFolder;
    [Export] public int AudioFormatIndex { get; set; }
    [Export] public int VideoFormatIndex { get; set; }

    public static MosicConfig Load()
    {
        var config = ResourceLoader.Exists(ConfigPath)
            ? ResourceLoader.Load<MosicConfig>(ConfigPath)
            : new MosicConfig();

        config.OutputFolder = ResolvePath(config.OutputFolder);

        if (string.IsNullOrWhiteSpace(config.OutputFolder) || !Directory.Exists(config.OutputFolder))
        {
            config.OutputFolder = DefaultOutputFolder;
        }

        return config;
    }

    public static string ResolvePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return DefaultOutputFolder;
        }

        string trimmed = rawPath.Trim();

        if (trimmed.StartsWith("content://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("/content:/", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("/com.android.", StringComparison.OrdinalIgnoreCase))
        {
            string unescaped = Uri.UnescapeDataString(trimmed);

            if (unescaped.IndexOf("raw:", StringComparison.OrdinalIgnoreCase) is >= 0 and int rawIndex)
            {
                string rawTarget = unescaped[(rawIndex + "raw:".Length)..];

                return rawTarget.IndexOf("/document/", StringComparison.OrdinalIgnoreCase) is >= 0 and int docSplit
                    ? rawTarget[..docSplit]
                    : rawTarget;
            }

            string specifier = unescaped;

            if (specifier.LastIndexOf("/document/", StringComparison.OrdinalIgnoreCase) is >= 0 and int docIndex)
            {
                specifier = specifier[(docIndex + "/document/".Length)..];
            }
            else if (specifier.LastIndexOf("/tree/", StringComparison.OrdinalIgnoreCase) is >= 0 and int treeIndex)
            {
                specifier = specifier[(treeIndex + "/tree/".Length)..];
            }

            specifier = specifier.Trim('/');

            if (string.Equals(specifier, "downloads", StringComparison.OrdinalIgnoreCase)
                && OS.GetSystemDir(OS.SystemDir.Downloads) is { Length: > 0 } downloads)
            {
                return downloads;
            }

            if (specifier.IndexOf(':') is >= 0 and var colonIndex)
            {
                string volume = specifier[..colonIndex];
                string relativePath = specifier[(colonIndex + 1)..].Trim('/');
                string storageRoot = string.Equals(volume, "primary", StringComparison.OrdinalIgnoreCase)
                    ? GetAndroidPrimaryStorageRoot()
                    : $"/storage/{volume}";

                return string.IsNullOrEmpty(relativePath)
                    ? storageRoot
                    : Path.Combine(storageRoot, relativePath);
            }
        }

        return Path.GetFullPath(trimmed);
    }

    public void Save() => ResourceSaver.Save(this, ConfigPath);

    private static string GetAndroidPrimaryStorageRoot()
    {
        if (OS.GetSystemDir(OS.SystemDir.Music) is { Length: > 0 } musicDir
            && Path.GetDirectoryName(musicDir.TrimEnd('/', '\\')) is { Length: > 0 } parent)
        {
            return parent;
        }

        return "/storage/emulated/0";
    }

    private static string GetDefaultOutputFolder()
    {
        if (OS.GetSystemDir(OS.SystemDir.Music) is { Length: > 0 } musicDir)
        {
            return Path.GetFullPath(musicDir);
        }

        if (OS.GetSystemDir(OS.SystemDir.Downloads) is { Length: > 0 } downloadsDir)
        {
            return Path.GetFullPath(downloadsDir);
        }

        return ProcessDirectory;
    }
}
