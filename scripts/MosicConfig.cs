using Godot;

namespace Mosic.scripts;

public partial class MosicConfig : Resource
{
    public const string Path = "user://config.tres";

    [Export] public string OutputFolder { get; set; }

    [Export] public string OutputFileTemplate { get; set; } = "%(title)s.%(ext)s";
}