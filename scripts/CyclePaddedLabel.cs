using Godot;

namespace Mosic.scripts;

public partial class CyclePaddedLabel : Label
{

    private int _paddingWidth;

    [Export]
    public string StaticText { get; set; } = "Downloading";

    [Export]
    public int PaddingCycleLength { get; set; } = 3;

    [Export]
    public bool AllowZeroPadding { get; set; }

    public override async void _Ready()
    {
        Text = StaticText;
        await YoutubeDLSharp.Utils.DownloadBinaries();
        CallDeferred(MethodName.Finish);
    }

    public override void _PhysicsProcess(double delta)
    {
        _paddingWidth = (_paddingWidth + 1) % PaddingCycleLength;
        
        int totalWidth = StaticText.Length + _paddingWidth + (AllowZeroPadding ? 0 : 1);
        Text = StaticText.PadRight(totalWidth, '.');
    }

    private void Finish()
    {
        GetTree().ChangeSceneToFile("res://scenes/mosic.tscn");
    }

}