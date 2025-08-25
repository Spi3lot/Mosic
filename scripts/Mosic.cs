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

    public override void _Ready()
    {
        SearchBar.GrabFocus();
        SearchBar.Connect(LineEdit.SignalName.TextSubmitted, Callable.From(void (string query) => Search(query)));
        SearchBar.PlaceholderText = Tr("SEARCH");

        SearchResultList.FixedIconSize = DisplayServer.ScreenGetSize() / 10;
        SearchResultList.Connect(ItemList.SignalName.ItemActivated, Callable.From(void (int index) => Download(index)));
    }

    private async Task Download(int index)
    {
        DownloadProgressBar.Indeterminate = true;
        await _ytdl.RunAudioDownload(_videos[index].Url, progress: _progress);
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

        foreach (var video in result.Results)
        {
            if (video.ThumbnailUrl.Contains("png", StringComparison.OrdinalIgnoreCase))
            {
                GD.PushWarning(video.ThumbnailUrl + " is probably png");
            }

            var image = new Image();
            image.LoadJpgFromBuffer(await _httpClient.GetByteArrayAsync(video.ThumbnailUrl));

            string text = $"{video.Title} | {video.Author} | {video.Duration}";
            SearchResultList.AddItem(text, ImageTexture.CreateFromImage(image));
            _videos.Add(video);
        }

        DownloadProgressBar.Indeterminate = false;
    }
}