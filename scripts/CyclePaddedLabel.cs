using System;
using Godot;

namespace Mosic.scripts;

[Tool]
public partial class CyclePaddedLabel : Label
{
    private int _paddingWidth;

    [Export]
    public string StaticText { get; set; }

    [Export]
    public int PaddingCycleLength { get; set; }

    [Export]
    public bool AllowZeroPadding { get; set; }

    public override void _Ready()
    {
        Text = StaticText;
    }

    public override void _PhysicsProcess(double delta)
    {
        _paddingWidth = (_paddingWidth + 1) % PaddingCycleLength;
        
        int totalWidth = StaticText.Length + _paddingWidth + (AllowZeroPadding ? 0 : 1);
        Text = StaticText.PadRight(totalWidth, '.');
    }
}