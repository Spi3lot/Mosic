using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Godot;

using Microsoft.Extensions.DependencyInjection;

using Mosic.Scripts.GitHub;
using Mosic.Scripts.Service;

using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;

namespace Mosic.Scripts;

public partial class Mosic : Control
{
    private readonly MosicConfig _config = MosicConfig.Load();
    private readonly IGitHubApi _gitHubApi = ServiceManager.Provider.GetRequiredService<IGitHubApi>();
    private readonly IMediaDownloader _mediaDownloader = ServiceManager.Provider.GetRequiredService<IMediaDownloader>();
    private readonly YoutubeClient _youtube = ServiceManager.Provider.GetRequiredService<YoutubeClient>();

    private CancellationTokenSource _cts = new();
    private Vector2 _thumbnailSize = new(160, 90);

    [Export] public MarginContainer RootMarginContainer { get; set; }
    [Export] public BoxContainer HeaderContainer { get; set; }
    [Export] public BoxContainer OptionsContainer { get; set; }
    [Export] public LineEdit SearchBar { get; set; }
    [Export] public BoxContainer SearchResultContainer { get; set; }
    [Export] public Button SearchButton { get; set; }
    [Export] public Button DownloadSingleButton { get; set; }
    [Export] public Button DownloadPlaylistButton { get; set; }
    [Export] public ProgressBar DownloadProgressBar { get; set; }
    [Export] public Button CancelDownloadButton { get; set; }
    [Export] public OptionButton FormatOptionButton { get; set; }
    [Export] public CheckButton VideoCheckButton { get; set; }
    [Export] public Label StatusLabel { get; set; }
    [Export] public Texture2D DownloadIcon { get; set; }

    [ExportGroup("Download Path")]
    [Export] public Button DownloadPathDialogButton { get; set; }
    [Export] public FileDialog DownloadPathDialog { get; set; }

    public override void _Ready()
    {
        if (OS.HasFeature("pc"))
        {
            SearchBar.GrabFocus();
        }

        SearchBar.TextChanged += DetectSearchOrDownload;
        SearchBar.TextSubmitted += async text => await HandlePrimaryActionAsync(text);

        OnViewportSizeChanged();
        GetViewport().SizeChanged += OnViewportSizeChanged;

        SearchButton.Pressed += async () => await SearchAsync(SearchBar.Text);
        DownloadSingleButton.Pressed += async () => await DownloadAsync(SearchBar.Text, false);
        DownloadPlaylistButton.Pressed += async () => await DownloadAsync(SearchBar.Text, true);

        FormatOptionButton.ItemSelected += index =>
        {
            if (VideoCheckButton.ButtonPressed)
            {
                _config.VideoFormatIndex = (int) index;
            }
            else
            {
                _config.AudioFormatIndex = (int) index;
            }

            _config.Save();
        };

        FillFormatOptionButton(VideoCheckButton.ButtonPressed);
        VideoCheckButton.Toggled += FillFormatOptionButton;

        DownloadPathDialogButton.Text = _config.OutputFolder;
        DownloadPathDialog.CurrentDir = _config.OutputFolder;
        DownloadPathDialogButton.Pressed += () =>
        {
            DownloadPathDialog.CurrentDir = _config.OutputFolder;
            DownloadPathDialog.PopupFileDialog();
        };

        DownloadPathDialog.DirSelected += dir =>
        {
            string fullPath = MosicConfig.ResolvePath(dir);
            DownloadPathDialogButton.Text = fullPath;
            _config.OutputFolder = fullPath;
            _config.Save();
        };

        CancelDownloadButton.Pressed += () =>
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        };
    }

    private async Task HandlePrimaryActionAsync(string query)
    {
        if (DownloadSingleButton.Visible)
        {
            await DownloadAsync(query, false);
        }
        else if (DownloadPlaylistButton.Visible)
        {
            await DownloadAsync(query, true);
        }
        else
        {
            await SearchAsync(query);
        }
    }

    private void OnViewportSizeChanged()
    {
        var windowSize = DisplayServer.WindowGetSize();
        bool isPortrait = windowSize.X < windowSize.Y;

        var targetScaleSize = isPortrait ? new Vector2I(450, 800) : new Vector2I(800, 450);
        var rootWindow = GetTree().Root;

        if (rootWindow.ContentScaleSize != targetScaleSize)
        {
            rootWindow.ContentScaleSize = targetScaleSize;
        }

        HeaderContainer?.Vertical = isPortrait;
        OptionsContainer?.Vertical = !isPortrait;
        float thumbWidth = isPortrait ? 120f : 160f;
        _thumbnailSize = new Vector2(thumbWidth, Mathf.Round(thumbWidth * 9f / 16f));

        if (SearchResultContainer != null)
        {
            foreach (var child in SearchResultContainer.GetChildren())
            {
                if (child is SearchResultItem item)
                {
                    item.SetThumbnailSize(_thumbnailSize);
                }
            }
        }

        UpdateSafeAreaMargins(windowSize);
    }

    private void UpdateSafeAreaMargins(Vector2I windowSize)
    {
        if (RootMarginContainer == null)
        {
            return;
        }

        const int baseMargin = 6;
        if (OS.HasFeature("pc") || windowSize.X <= 0 || windowSize.Y <= 0)
        {
            RootMarginContainer.AddThemeConstantOverride("margin_left", baseMargin);
            RootMarginContainer.AddThemeConstantOverride("margin_top", baseMargin);
            RootMarginContainer.AddThemeConstantOverride("margin_right", baseMargin);
            RootMarginContainer.AddThemeConstantOverride("margin_bottom", baseMargin);
            return;
        }

        var safeArea = DisplayServer.GetDisplaySafeArea();
        var viewportSize = GetViewportRect().Size;
        var scale = viewportSize / windowSize;

        int left = baseMargin + Mathf.Max(0, Mathf.RoundToInt(safeArea.Position.X * scale.X));
        int top = baseMargin + Mathf.Max(0, Mathf.RoundToInt(safeArea.Position.Y * scale.Y));
        int right = baseMargin + Mathf.Max(0, Mathf.RoundToInt((windowSize.X - safeArea.End.X) * scale.X));
        int bottom = baseMargin + Mathf.Max(0, Mathf.RoundToInt((windowSize.Y - safeArea.End.Y) * scale.Y));

        RootMarginContainer.AddThemeConstantOverride("margin_left", left);
        RootMarginContainer.AddThemeConstantOverride("margin_top", top);
        RootMarginContainer.AddThemeConstantOverride("margin_right", right);
        RootMarginContainer.AddThemeConstantOverride("margin_bottom", bottom);
    }

    private void DetectSearchOrDownload(string query)
    {
        bool containsYoutube = query.Contains("youtube.") || query.Contains("youtu.be");

        bool isWellFormedUrl = Uri.IsWellFormedUriString($"https://{query.TrimPrefix("https://")}", UriKind.Absolute)
                               || Uri.IsWellFormedUriString($"http://{query.TrimPrefix("http://")}", UriKind.Absolute);

        if (containsYoutube && isWellFormedUrl)
        {
            SearchButton.Visible = false;
            DownloadPlaylistButton.Visible = query.Contains("?list=") || query.Contains("&list=");

            DownloadSingleButton.Visible = query.Contains("youtu.be")
                                           || query.Contains("/watch")
                                           || query.Contains("/shorts");
        }
        else
        {
            SearchButton.Visible = !string.IsNullOrWhiteSpace(query);
            DownloadSingleButton.Visible = false;
            DownloadPlaylistButton.Visible = false;
        }
    }

    private async Task SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || !SearchButton.Visible)
        {
            return;
        }

        HideStatus();
        ClearSearchResults();
        DownloadProgressBar.Indeterminate = true;
        DownloadProgressBar.Visible = true;
        CancelDownloadButton.Visible = true;

        try
        {
            await foreach (var video in _youtube.Search.GetVideosAsync(query, _cts.Token))
            {
                string durationText = video.Duration?.ToString() ?? "Live";
                var item = new SearchResultItem(
                    video.Title,
                    video.Author.ChannelTitle,
                    durationText,
                    DownloadIcon,
                    _thumbnailSize
                );

                string videoUrl = video.Url;
                item.DownloadRequested += async () => await DownloadAsync(videoUrl, false);
                SearchResultContainer.AddChild(item);

                if (video.Thumbnails.TryGetWithHighestResolution() is { } bestThumbnail)
                {
                    _ = FetchAndSetThumbnailAsync(item, bestThumbnail.Url);
                }
            }
        }
        catch (OperationCanceledException)
        {
            GD.Print("Search cancelled.");
        }
        catch (Exception ex)
        {
            ShowError($"Search failed: {ex.Message}");
            GD.PushError(ex);
        }
        finally
        {
            CancelDownloadButton.Visible = false;
            DownloadProgressBar.Visible = false;
        }
    }

    private void ClearSearchResults()
    {
        foreach (var child in SearchResultContainer.GetChildren())
        {
            child.QueueFree();
        }
    }

    private async Task FetchAndSetThumbnailAsync(SearchResultItem item, string thumbnailUrl)
    {
        try
        {
            byte[] bytes = await _gitHubApi.GetByteArrayAsync(thumbnailUrl);
            if (!IsInstanceValid(item))
            {
                return;
            }

            var image = new Image();
            var error = bytes switch
            {
                [(byte) 'R', (byte) 'I', (byte) 'F', (byte) 'F', _, _, _, _, (byte) 'W', (byte) 'E', (byte) 'B', (byte) 'P', ..]
                    => image.LoadWebpFromBuffer(bytes),
                [0x89, (byte) 'P', (byte) 'N', (byte) 'G', (byte) '\r', (byte) '\n', 0x1A, (byte) '\n', ..]
                    => image.LoadPngFromBuffer(bytes),
                [0xFF, 0xD8, ..]
                    => image.LoadJpgFromBuffer(bytes),
                _
                    => Error.FileUnrecognized
            };

            if (error != Error.Ok)
            {
                GD.PrintErr($"Failed to load image (Error: {error}) from {thumbnailUrl}");
                return;
            }

            item.SetThumbnail(ImageTexture.CreateFromImage(image));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to fetch thumbnail from {thumbnailUrl}: {ex.Message}");
        }
    }

    private async Task DownloadAsync(string url, bool playlist)
    {
        HideStatus();
        DownloadProgressBar.Indeterminate = true;
        DownloadProgressBar.Value = 0;
        DownloadProgressBar.Visible = true;
        CancelDownloadButton.Visible = true;

        try
        {
            if (playlist)
            {
                await foreach (var video in _youtube.Playlists.GetVideosAsync(url, _cts.Token))
                {
                    await ProcessDownloadAsync(video.Id, video.Title);
                }
            }
            else
            {
                var video = await _youtube.Videos.GetAsync(url, _cts.Token);
                await ProcessDownloadAsync(video.Id, video.Title);
            }

            DisplayServer.Beep();
        }
        catch (OperationCanceledException)
        {
            GD.Print("Download cancelled.");
        }
        catch (Exception ex)
        {
            ShowError($"Download failed: {ex.Message}");
            GD.PushError(ex);
        }
        finally
        {
            CancelDownloadButton.Visible = false;
            DownloadProgressBar.Visible = false;
            DownloadProgressBar.Value = 0;
        }
    }

    private async Task ProcessDownloadAsync(VideoId id, string title)
    {
        string cleanTitle = string.Join('_', title.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(cleanTitle))
        {
            cleanTitle = id.Value;
        }

        var progress = new Progress<double>(p =>
        {
            DownloadProgressBar.Indeterminate = false;
            DownloadProgressBar.Value = Mathf.Lerp(DownloadProgressBar.MinValue, DownloadProgressBar.MaxValue, p);
        });

        await _mediaDownloader.DownloadAsync(
            id,
            cleanTitle,
            _config.OutputFolder,
            VideoCheckButton.ButtonPressed,
            FormatOptionButton.Selected,
            progress,
            _cts.Token
        );
    }

    private void FillFormatOptionButton(bool video)
    {
        FormatOptionButton.Clear();

        var formats = video
            ? _mediaDownloader.VideoFormats
            : _mediaDownloader.AudioFormats;

        foreach (string formatName in formats)
        {
            FormatOptionButton.AddItem($".{formatName.ToLowerInvariant()}");
        }

        if (video)
        {
            FormatOptionButton.Selected = Mathf.Clamp(_config.VideoFormatIndex, 0, Math.Max(0, formats.Count - 1));
            DownloadSingleButton.Text = "DOWNLOAD_VIDEO";
        }
        else
        {
            FormatOptionButton.Selected = Mathf.Clamp(_config.AudioFormatIndex, 0, Math.Max(0, formats.Count - 1));
            DownloadSingleButton.Text = "DOWNLOAD_AUDIO";
        }
    }

    private void ShowError(string message)
    {
        StatusLabel?.Text = message;
        StatusLabel?.Visible = true;
    }

    private void HideStatus()
    {
        StatusLabel?.Visible = false;
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= OnViewportSizeChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
