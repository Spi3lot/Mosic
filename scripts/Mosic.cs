using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using YoutubeSearchApi.Net.Models.Youtube;

namespace Mosic.Scripts;

public partial class Mosic : Control
{
    private readonly YoutubeSearchApi.Net.Services.YoutubeSearchClient _youtubeSearchClient;

    private readonly System.Net.Http.HttpClient _httpClient = new();

    private readonly YoutubeDLSharp.YoutubeDL _ytdl = new();

    private readonly IList<YoutubeVideo> _videos = [];

    private readonly MosicConfig _config = MosicConfig.Load();

    private CancellationTokenSource _cts = new();

    public Mosic()
    {
        _youtubeSearchClient = new(_httpClient);
    }

    [Export]
    public LineEdit SearchBar { get; set; }

    [Export]
    public ItemList SearchResultList { get; set; }

    [Export]
    public Button SearchButton { get; set; }

    [Export]
    public Button DownloadSingleButton { get; set; }

    [Export]
    public Button DownloadPlaylistButton { get; set; }

    [Export]
    public ProgressBar DownloadProgressBar { get; set; }

    [Export]
    public Button CancelDownloadButton { get; set; }

    [Export]
    public OptionButton FormatOptionButton { get; set; }

    [Export]
    public CheckButton VideoCheckButton { get; set; }

    [Export]
    [ExportGroup("Download Path")]
    public Button DownloadPathDialogButton { get; set; }

    [Export]
    public FileDialog DownloadPathDialog { get; set; }

    public override void _Ready()
    {
        SearchBar.GrabFocus();
        SearchBar.TextChanged += DetectSearchOrDownload;
        SearchBar.TextSubmitted += async text => await SearchAsync(text);

        SearchResultList.FixedIconSize = DisplayServer.ScreenGetSize() / 10;
        SearchResultList.ItemActivated += async index => await DownloadAsync(_videos[(int) index].Url, false);

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

        FillFormatOptionButton(FormatOptionButton.ButtonPressed);
        VideoCheckButton.Toggled += FillFormatOptionButton;

        _ytdl.YoutubeDLPath = Path.Combine(MosicConfig.ProcessDirectory, _ytdl.YoutubeDLPath);
        _ytdl.FFmpegPath = Path.Combine(MosicConfig.ProcessDirectory, _ytdl.FFmpegPath);
        _ytdl.OutputFileTemplate = _config.OutputFileTemplate;
        _ytdl.OutputFolder = _config.OutputFolder;
        DownloadPathDialogButton.Text = _ytdl.OutputFolder;
        DownloadPathDialogButton.Pressed += () => DownloadPathDialog.PopupCentered();

        DownloadPathDialog.DirSelected += dir =>
        {
            string fullPath = Path.GetFullPath(dir);
            DownloadPathDialogButton.Text = fullPath;
            _ytdl.OutputFolder = fullPath;
            _config.OutputFolder = fullPath;
            _config.Save();
        };

        CancelDownloadButton.Pressed += () =>
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
        };
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
        if (!SearchButton.Visible)
        {
            return;
        }

        _videos.Clear();
        SearchResultList.Clear();
        DownloadProgressBar.Visible = true;
        var result = await _youtubeSearchClient.SearchAsync(query, 1);
        int index = 0;

        foreach (var video in result.Results)
        {
            _videos.Add(video);
            SearchResultList.AddItem($"{video.Title} | {video.Author} | {video.Duration}");
            _ = FetchAndSetThumbnailAsync(index, video.ThumbnailUrl);
            index++;
        }

        DownloadProgressBar.Visible = false;
    }

    private async Task FetchAndSetThumbnailAsync(int index, string thumbnailUrl)
    {
        var image = new Image();
        image.LoadJpgFromBuffer(await _httpClient.GetByteArrayAsync(thumbnailUrl));
        SearchResultList.SetItemIcon(index, ImageTexture.CreateFromImage(image));
    }

    private async Task DownloadAsync(string url, bool playlist)
    {
        DownloadProgressBar.Visible = true;
        CancelDownloadButton.Visible = true;
        Task task;

        if (VideoCheckButton.ButtonPressed)
        {
            var format = (YoutubeDLSharp.Options.VideoRecodeFormat) FormatOptionButton.Selected;

            task = (playlist)
                ? _ytdl.RunVideoPlaylistDownload(url, recodeFormat: format, ct: _cts.Token)
                : _ytdl.RunVideoDownload(url, recodeFormat: format, ct: _cts.Token);
        }
        else
        {
            var format = (YoutubeDLSharp.Options.AudioConversionFormat) FormatOptionButton.Selected;

            task = (playlist)
                ? _ytdl.RunAudioPlaylistDownload(url, format: format, ct: _cts.Token)
                : _ytdl.RunAudioDownload(url, format: format, ct: _cts.Token);
        }

        try
        {
            await task;
            DisplayServer.Beep();
        }
        catch (OperationCanceledException)
        {
            GD.Print("Download cancelled.");
        }

        CancelDownloadButton.Visible = false;
        DownloadProgressBar.Visible = false;
    }

    private void FillFormatOptionButton(bool video)
    {
        FormatOptionButton.Clear();

        string[] names = (video)
            ? Enum.GetNames<YoutubeDLSharp.Options.VideoRecodeFormat>()
            : Enum.GetNames<YoutubeDLSharp.Options.AudioConversionFormat>();

        foreach (string formatName in names)
        {
            string label = formatName;

            if (label == "Vorbis")
            {
                label = ".ogg";
            }
            else if (FormatOptionButton.ItemCount > 0)
            {
                label = $".{formatName.ToLower()}";
            }

            FormatOptionButton.AddItem(label);
        }

        if (video)
        {
            FormatOptionButton.SetItemText(0, "Original");
            FormatOptionButton.Selected = _config.VideoFormatIndex;
            DownloadSingleButton.Text = "DOWNLOAD_VIDEO";
        }
        else
        {
            FormatOptionButton.Selected = _config.AudioFormatIndex;
            DownloadSingleButton.Text = "DOWNLOAD_AUDIO";
        }
    }

    public override void _ExitTree()
    {
        _cts.Cancel();
        _cts.Dispose();
        _httpClient.Dispose();
    }
}