using System.IO;
using System.Threading.Tasks;
using Godot;
using YoutubeDLSharp;

namespace Mosic.Scripts;

public partial class Setup : Control
{
    [Export] public CyclePaddedLabel DownloadLabel { get; set; }

    [Export] public ProgressBar DownloadProgressBar { get; set; }

    [Export] public UpdateWindow UpdateWindow { get; set; }

    public override void _EnterTree()
    {
        UpdateWindow.UpdateAborted += async () =>
        {
            await DownloadBinariesAsync(MosicConfig.ProcessDirectory);
            CallDeferred(MethodName.ChangeSceneToMain);
        };

        UpdateWindow.UpdateAvailable += () => DownloadLabel.StaticText = "Waiting for a decision";
        UpdateWindow.UpdateAccepted += () => DownloadLabel.StaticText = "Updating";
        _ = UpdateWindow.PopupIfUpdateAvailableAsync();
    }

    private async Task DownloadBinariesAsync(string directoryPath)
    {
        if (!File.Exists(Path.Combine(directoryPath, Utils.YtDlpBinaryName)))
        {
            DownloadLabel.StaticText = "Downloading yt-dlp";
            await Utils.DownloadYtDlp(directoryPath);
        }

        DownloadProgressBar.Value = 1;

        if (!File.Exists(Path.Combine(directoryPath, Utils.FfmpegBinaryName)))
        {
            DownloadLabel.StaticText = "Downloading FFmpeg";
            await Utils.DownloadFFmpeg(directoryPath);
        }

        DownloadProgressBar.Value = 2;

        if (!File.Exists(Path.Combine(directoryPath, Utils.FfprobeBinaryName)))
        {
            DownloadLabel.StaticText = "Downloading FFprobe";
            await Utils.DownloadFFprobe(directoryPath);
        }

        DownloadProgressBar.Value = 3;
    }

    private void ChangeSceneToMain()
    {
        GetTree().ChangeSceneToFile("res://scenes/mosic.tscn");
    }
}