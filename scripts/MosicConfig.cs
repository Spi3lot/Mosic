using System.IO;
using Godot;

namespace Mosic.Scripts;

public partial class MosicConfig : Resource
{
    private const string ConfigPath = "user://config.tres";

    public static readonly string ProcessPath = OS.GetExecutablePath();

    public static readonly string ProcessDirectory = Path.GetDirectoryName(ProcessPath);

    public static readonly string Digest = Godot.FileAccess.GetSha256(ProcessPath);

    public static string Version { get; set; }

    [Export] public string OutputFolder { get; set; } = ProcessDirectory;

    [Export] public string OutputFileTemplate { get; set; } = "%(title)s.%(ext)s";

    [Export] public int AudioFormatIndex { get; set; }

    [Export] public int VideoFormatIndex { get; set; }

    public static MosicConfig Load()
    {
        return ResourceLoader.Exists(ConfigPath)
            ? ResourceLoader.Load<MosicConfig>(ConfigPath)
            : new MosicConfig();
    }

    public void Save()
    {
        ResourceSaver.Save(this, ConfigPath);
    }
}