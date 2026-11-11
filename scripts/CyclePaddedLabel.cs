using Godot;

namespace Mosic.Scripts;

[Tool]
[GlobalClass]
public partial class CyclePaddedLabel : Label
{
    private int _paddingWidth;

    [Export] public string StaticText { get; set; } = string.Empty;
    [Export] public string PaddingChar { get; set; } = ".";
    [Export] public int PaddingCycleLength { get; set; } = 4;
    [Export] public bool AllowZeroPadding { get; set; } = true;

    public override void _Ready()
    {
        Text = StaticText;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (PaddingCycleLength <= 0 || string.IsNullOrEmpty(StaticText))
        {
            return;
        }

        _paddingWidth = (_paddingWidth + 1) % PaddingCycleLength;
        int totalWidth = StaticText.Length + _paddingWidth + (AllowZeroPadding ? 0 : 1);
        char padChar = PaddingChar is [var c, ..] ? c : ' ';
        Text = StaticText.PadRight(totalWidth, padChar);
    }
}
