using System;
using System.Collections.Generic;
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
    public ProgressBar DownloadProgressBar { get; set; }

    [Export]
    public ItemList SearchResultList { get; set; }

    [Export]
    public OptionButton FormatOptionButton { get; set; }

    [Export]
    public CheckButton UrlCheckButton { get; set; }

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
        SearchBar.TextSubmitted += query => _ = Search(query);
        
        SearchResultList.FixedIconSize = DisplayServer.ScreenGetSize() / 10;
        SearchResultList.ItemActivated += index => _ = Download((int) index);
        
        FillFormatOptionButton(FormatOptionButton.ButtonPressed);
        VideoCheckButton.Toggled += FillFormatOptionButton;
        
        UrlCheckButton.Toggled += toggledOn => SearchBar.PlaceholderText = (toggledOn) ? "URL" : "SEARCH";

        DownloadPathDialogButton.Text = SceneFilePath;
        DownloadPathDialogButton.Pressed += () => DownloadPathDialog.PopupCentered();
    }

    private async Task Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
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

    private async Task Download(int index)
    {
        DownloadProgressBar.Indeterminate = true;

        if (VideoCheckButton.ButtonPressed)
        {
            var format = (YoutubeDLSharp.Options.VideoRecodeFormat) FormatOptionButton.Selected;
            await _ytdl.RunVideoDownload(_videos[index].Url, recodeFormat: format, progress: _progress);
        }
        else
        {
            var format = (YoutubeDLSharp.Options.AudioConversionFormat) FormatOptionButton.Selected;
            await _ytdl.RunAudioDownload(_videos[index].Url, format: format, progress: _progress);
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
            FormatOptionButton.AddItem(formatName);
        }
        
        if (toggledOn)
        {
            FormatOptionButton.SetItemText(0, "Original");
        }
    }
}