using System;

using Godot;

namespace Mosic.Scripts;

public partial class SearchResultItem : PanelContainer
{
    private readonly TextureRect _thumbnailRect;

    public event Action DownloadRequested;

    public SearchResultItem()
    {
    }

    public SearchResultItem(string title, string author, string duration, Texture2D downloadIcon, Vector2 thumbnailSize)
    {
        MouseFilter = MouseFilterEnum.Pass;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        AddChild(margin);

        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);

        _thumbnailRect = new TextureRect
        {
            MouseFilter = MouseFilterEnum.Pass,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            CustomMinimumSize = thumbnailSize,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        row.AddChild(_thumbnailRect);

        var textColumn = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        textColumn.AddThemeConstantOverride("separation", 4);
        row.AddChild(textColumn);

        var titleLabel = new Label
        {
            Text = title,
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        textColumn.AddChild(titleLabel);

        var metaLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(duration) ? author : $"{author}  •  {duration}",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Modulate = new Color(1f, 1f, 1f, 0.7f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 1,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        textColumn.AddChild(metaLabel);

        var downloadButton = new Button
        {
            Icon = downloadIcon,
            MouseFilter = MouseFilterEnum.Pass,
            FocusMode = FocusModeEnum.None,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(40, 40)
        };
        downloadButton.Pressed += () => DownloadRequested?.Invoke();
        row.AddChild(downloadButton);
    }

    public void SetThumbnail(Texture2D texture)
    {
        _thumbnailRect?.Texture = texture;
    }

    public void SetThumbnailSize(Vector2 size)
    {
        _thumbnailRect?.CustomMinimumSize = size;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, DoubleClick: true, Pressed: true })
        {
            DownloadRequested?.Invoke();
            AcceptEvent();
        }
    }
}
