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

    private readonly MosicConfig _config = MosicConfig.Load();

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
        SearchResultList.ItemActivated += index => Download(_videos[(int) index].Url, false);

        SearchButton.Pressed += () => Search(SearchBar.Text);
        DownloadSingleButton.Pressed += () => Download(SearchBar.Text, false);
        DownloadPlaylistButton.Pressed += () => Download(SearchBar.Text, true);

        FormatOptionButton.ItemSelected += index =>
        {
            if (VideoCheckButton.ButtonPressed) _config.VideoFormatIndex = (int) index;
            else _config.AudioFormatIndex = (int) index;
            _config.Save();
        };
        
        FillFormatOptionButton(FormatOptionButton.ButtonPressed);
        VideoCheckButton.Toggled += FillFormatOptionButton;

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
    }

    private void ToggleSearchDownload(string query)
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


        if (DownloadPlaylistButton.Visible || DownloadSingleButton.Visible)
        {
            return;
        }

        SearchButton.Visible = !string.IsNullOrWhiteSpace(query);
        DownloadSingleButton.Visible = false;
    }

    private void Search(string query) => _ = SearchAsync(query);

    private async Task SearchAsync(string query)
    {
        if (!SearchButton.Visible)
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

    private void Download(string url, bool playlist) => _ = DownloadAsync(url, playlist);

    private async Task DownloadAsync(string url, bool playlist)
    {
        DownloadProgressBar.Indeterminate = true;
        Task task;

        if (VideoCheckButton.ButtonPressed)
        {
            var format = (YoutubeDLSharp.Options.VideoRecodeFormat) FormatOptionButton.Selected;

            task = (playlist)
                ? _ytdl.RunVideoPlaylistDownload(url, recodeFormat: format)
                : _ytdl.RunVideoDownload(url, recodeFormat: format);
        }
        else
        {
            var format = (YoutubeDLSharp.Options.AudioConversionFormat) FormatOptionButton.Selected;

            task = (playlist)
                ? _ytdl.RunAudioPlaylistDownload(url, format: format)
                : _ytdl.RunAudioDownload(url, format: format);
        }

        await task;
        DownloadProgressBar.Indeterminate = false;
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
}