using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using YoutubeSearchApi.Net.Models.Youtube;

namespace Mosic.scripts;

public partial class Mosic : Control
{
    private readonly YoutubeSearchApi.Net.Services.YoutubeSearchClient _youtubeSearchClient;

    private readonly System.Net.Http.HttpClient _httpClient = new();

    private readonly YoutubeDLSharp.YoutubeDL _ytdl = new();

    private readonly IList<YoutubeVideo> _videos = [];

    private readonly IProgress<YoutubeDLSharp.DownloadProgress> _progress;

    private MosicConfig _config;

    public Mosic()
    {
        _progress = new Progress<YoutubeDLSharp.DownloadProgress>(progress =>
        {
            DownloadProgressBar.Value = progress.Progress;
        });

        _youtubeSearchClient = new(_httpClient);
    }

    [Export]
    public LineEdit SearchBar { get; set; }

    [Export]
    public ItemList SearchResultList { get; set; }

    [Export]
    public Button SearchButton { get; set; }

    [Export]
    public Button DownloadAvButton { get; set; }

    [Export]
    public Button DownloadPlaylistButton { get; set; }

    [Export]
    public ProgressBar DownloadProgressBar { get; set; }

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
        SearchBar.TextChanged += ToggleSearchDownload;
        SearchBar.TextSubmitted += Search;

        SearchResultList.FixedIconSize = DisplayServer.ScreenGetSize() / 10;
        SearchResultList.ItemActivated += index => DownloadSingle(_videos[(int) index].Url);

        SearchButton.Pressed += () => Search(SearchBar.Text);
        DownloadAvButton.Pressed += () => DownloadSingle(SearchBar.Text);
        DownloadPlaylistButton.Pressed += () => DownloadPlaylist(SearchBar.Text);

        FillFormatOptionButton(FormatOptionButton.ButtonPressed);
        VideoCheckButton.Toggled += FillFormatOptionButton;

        _config = (ResourceLoader.Exists(MosicConfig.Path))
            ? ResourceLoader.Load<MosicConfig>(MosicConfig.Path)
            : new MosicConfig();

        _ytdl.OutputFileTemplate = _config.OutputFileTemplate;
        _ytdl.OutputFolder = _config.OutputFolder;
        DownloadPathDialogButton.Text = _config.OutputFolder;
        DownloadPathDialogButton.Pressed += () => DownloadPathDialog.PopupCentered();

        DownloadPathDialog.DirSelected += dir =>
        {
            string fullPath = Path.GetFullPath(dir);
            DownloadPathDialogButton.Text = fullPath;
            _ytdl.OutputFolder = fullPath;
            _config.OutputFolder = fullPath;
            ResourceSaver.Save(_config, MosicConfig.Path);
        };
    }

    private void ToggleSearchDownload(string query)
    {
        bool couldBeUrl = query.Contains("youtube.")
                          || query.Contains("youtu.be");

        bool isWellFormedUrl = Uri.IsWellFormedUriString("https://" + query.TrimPrefix("https://"), UriKind.Absolute)
                               || Uri.IsWellFormedUriString("http://" + query.TrimPrefix("http://"), UriKind.Absolute);

        if (couldBeUrl && isWellFormedUrl)
        {
            SearchButton.Visible = false;
            DownloadPlaylistButton.Visible = query.Contains("?list=") || query.Contains("&list=");
            DownloadAvButton.Visible = query.Contains("/watch") || query.Contains("youtu.be");
        }
        else
        {
            SearchButton.Visible = !string.IsNullOrWhiteSpace(query);
            DownloadAvButton.Visible = false;
        }
    }

    private void Search(string query) => _ = SearchAsync(query);

    private async Task SearchAsync(string query)
    {
        if (!SearchButton.Visible || string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        _videos.Clear();
        SearchResultList.Clear();
        DownloadProgressBar.Indeterminate = true;
        var result = await _youtubeSearchClient.SearchAsync(query);
        int index = 0;

        foreach (var video in result.Results)
        {
            _videos.Add(video);
            SearchResultList.AddItem($"{video.Title} | {video.Author} | {video.Duration}");
            _ = FetchAndSetThumbnail(index, video.ThumbnailUrl);
            index++;
        }

        DownloadProgressBar.Indeterminate = false;
    }

    private async Task FetchAndSetThumbnail(int index, string thumbnailUrl)
    {
        var image = new Image();
        image.LoadJpgFromBuffer(await _httpClient.GetByteArrayAsync(thumbnailUrl));
        SearchResultList.SetItemIcon(index, ImageTexture.CreateFromImage(image));
    }

    private void DownloadSingle(string url) => _ = DownloadSingleAsync(url);

    private async Task DownloadSingleAsync(string url)
    {
        DownloadProgressBar.Indeterminate = true;

        if (VideoCheckButton.ButtonPressed)
        {
            var format = (YoutubeDLSharp.Options.VideoRecodeFormat) FormatOptionButton.Selected;
            await _ytdl.RunVideoDownload(url, recodeFormat: format, progress: _progress);
        }
        else
        {
            var format = (YoutubeDLSharp.Options.AudioConversionFormat) FormatOptionButton.Selected;
            await _ytdl.RunAudioDownload(url, format: format, progress: _progress);
        }

        DownloadProgressBar.Indeterminate = false;
    }

    private void DownloadPlaylist(string url) => _ = DownloadPlaylistAsync(url);

    private async Task DownloadPlaylistAsync(string url)
    {
        DownloadProgressBar.Indeterminate = true;

        if (VideoCheckButton.ButtonPressed)
        {
            var format = (YoutubeDLSharp.Options.VideoRecodeFormat) FormatOptionButton.Selected;
            await _ytdl.RunVideoPlaylistDownload(url, recodeFormat: format, progress: _progress);
        }
        else
        {
            var format = (YoutubeDLSharp.Options.AudioConversionFormat) FormatOptionButton.Selected;
            await _ytdl.RunAudioPlaylistDownload(url, format: format, progress: _progress);
        }

        DownloadProgressBar.Indeterminate = false;
    }

    private void FillFormatOptionButton(bool toggledOn)
    {
        string[] names = (toggledOn)
            ? Enum.GetNames<YoutubeDLSharp.Options.VideoRecodeFormat>()
            : Enum.GetNames<YoutubeDLSharp.Options.AudioConversionFormat>();

        FormatOptionButton.Clear();

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

        if (toggledOn)
        {
            DownloadAvButton.Text = "DOWNLOAD_VIDEO";
            FormatOptionButton.SetItemText(0, "Original");
        }
        else
        {
            DownloadAvButton.Text = "DOWNLOAD_AUDIO";
        }
    }
}