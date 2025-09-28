using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Mosic.Scripts.Service;
using YoutubeDLSharp;

namespace Mosic.Scripts;

public partial class Setup : Control
{
    [Export] public CyclePaddedLabel DownloadLabel { get; set; }

    [Export] public ProgressBar DownloadProgressBar { get; set; }

    [Export] public UpdateWindow UpdateWindow { get; set; }

    public override void _EnterTree()
    {
        if (ReplacePredecessor())
        {
            return;
        }

        UpdateWindow.UpdateAborted += async () =>
        {
            await DownloadBinariesAsync(MosicConfig.ProcessDirectory);
            CallDeferred(MethodName.ChangeSceneToMain);
        };

        UpdateWindow.UpdateAvailable += () => DownloadLabel.StaticText = "Waiting for a decision";
        UpdateWindow.UpdateAccepted += () => DownloadLabel.StaticText = "Updating";
        _ = UpdateWindow.PopupIfUpdateAvailableAsync();
    }

    private static bool ReplacePredecessor()
    {
        string replacePath = OS.GetCmdlineUserArgs()
            .Select(arg => arg.Split('='))
            .Select(kv => KeyValuePair.Create(kv[0], kv[1]))
            .Cast<KeyValuePair<string, string>?>()
            .FirstOrDefault(pair => pair!.Value.Key == CmdlineUserArgs.Replace)
            ?.Value;

        if (replacePath == null)
        {
            return false;
        }

        File.Delete(replacePath);
        GD.Print($"Deleted old version at {replacePath}.");
        return true;
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