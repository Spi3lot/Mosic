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

    private readonly IList<YoutubeVideo> _videos = [];

    private readonly YoutubeDLSharp.YoutubeDL _ytdl = new();

    private readonly IProgress<YoutubeDLSharp.DownloadProgress> _progress;

    public Mosic()
    {
        _progress = new Progress<YoutubeDLSharp.DownloadProgress>(progress =>
        {
            DownloadProgressBar.Value = progress.Progress;
        });

        _youtubeSearchClient = new(_httpClient);
    }

    [Export] public LineEdit SearchBar { get; set; }

    [Export] public ProgressBar DownloadProgressBar { get; set; }

    [Export] public ItemList SearchResultList { get; set; }

    [Export] public OptionButton AudioFormatOptionButton { get; set; }

    public override void _Ready()
    {
        SearchBar.GrabFocus();
        SearchBar.Connect(LineEdit.SignalName.TextSubmitted, Callable.From(void (string query) => Search(query)));
        SearchBar.PlaceholderText = Tr("SEARCH");

        SearchResultList.FixedIconSize = DisplayServer.ScreenGetSize() / 10;
        SearchResultList.Connect(ItemList.SignalName.ItemActivated, Callable.From(void (int index) => Download(index)));

        foreach (string formatName in Enum.GetNames<YoutubeDLSharp.Options.AudioConversionFormat>())
        {
            AudioFormatOptionButton.AddItem(formatName);
        }
    }

    private async Task Download(int index)
    {
        DownloadProgressBar.Indeterminate = true;

        if (!Enum.TryParse<YoutubeDLSharp.Options.AudioConversionFormat>(AudioFormatOptionButton.Text, out var format))
        {
            GD.PushError("Audio format option not supported: " + AudioFormatOptionButton.Text);
        }
        
        await _ytdl.RunAudioDownload(_videos[index].Url, format: format, progress: _progress);
        DownloadProgressBar.Indeterminate = false;
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
            if (video.ThumbnailUrl.Contains("png", StringComparison.OrdinalIgnoreCase))
            {
                GD.PushWarning(video.ThumbnailUrl + " is probably png");
            }

            _videos.Add(video);
            SearchResultList.AddItem($"{video.Title} | {video.Author} | {video.Duration}");
            FetchAndSetThumbnail(index, video.ThumbnailUrl);
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
}