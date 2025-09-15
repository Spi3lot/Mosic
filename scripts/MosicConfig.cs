using Godot;

namespace Mosic.scripts;

public partial class MosicConfig : Resource
{
    private const string Path = "user://config.tres";

    public static readonly string ProcessPath = System.IO.Path.GetDirectoryName(System.Environment.ProcessPath);

    [Export] public string OutputFolder { get; set; } = ProcessPath;

    [Export] public string OutputFileTemplate { get; set; } = "%(title)s.%(ext)s";

    [Export] public int AudioFormatIndex { get; set; }

    [Export] public int VideoFormatIndex { get; set; }

    public static MosicConfig Load() => (ResourceLoader.Exists(Path))
        ? ResourceLoader.Load<MosicConfig>(Path)
        : new MosicConfig();

    public void Save() => ResourceSaver.Save(this, Path);
}