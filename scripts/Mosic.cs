using System;
using System.Threading.Tasks;
using Godot;

namespace Mosic.scripts;

public partial class Mosic : Control
{
    private readonly IProgress<YoutubeDLSharp.DownloadProgress> _progress;

    private readonly System.Net.Http.HttpClient _httpClient = new();

    private readonly YoutubeSearchApi.Net.Services.YoutubeSearchClient _youtubeSearchClient;

    public Mosic()
    {
        _progress = new Progress<YoutubeDLSharp.DownloadProgress>(progress =>
            DownloadProgressBar.Value = progress.Progress);
        _youtubeSearchClient = new(_httpClient);
    }

    [Export] public LineEdit SearchBar { get; set; }

    [Export] public ProgressBar DownloadProgressBar { get; set; }

    [Export] public ItemList SearchResultList { get; set; }

    public override void _Ready()
    {
        SearchBar.GrabFocus();
        
        SearchBar.Connect(
            LineEdit.SignalName.TextSubmitted,
            Callable.From(void (string query) => Search(query))
        );
    }

    private void Download(string url)
    {
        var ytdl = new YoutubeDLSharp.YoutubeDL();
        ytdl.RunAudioDownload(url, progress: _progress);
    }

    private async Task Search(string query)
    {
        SearchResultList.Clear();
        DownloadProgressBar.Indeterminate = true;
        var result = await _youtubeSearchClient.SearchAsync(query);

        foreach (var video in result.Results)
        {
            GD.Print(video.ThumbnailUrl);
            byte[] bytes = await _httpClient.GetByteArrayAsync(video.ThumbnailUrl);
            var image = new Image();
            image.LoadJpgFromBuffer(bytes);
            ResizeImageKeepAspect(image, 100, false);

            string text = $"{video.Title}\n{video.Author}\n{video.Duration}";
            SearchResultList.AddItem(text, ImageTexture.CreateFromImage(image));
        }
        
        DownloadProgressBar.Indeterminate = false;
    }

    private static void ResizeImageKeepAspect(Image image, int size, bool setWidth)
    {
        if (setWidth)
        {
            image.Resize(size, size * image.GetHeight() / image.GetWidth());
        }
        else
        {
            image.Resize(size * image.GetWidth() / image.GetHeight(), size);
        }
    }
}