using System.IO;
using System.Threading.Tasks;
using Godot;
using YoutubeDLSharp;

namespace Mosic.scripts;

[Tool]
public partial class DownloadingLabel : CyclePaddedLabel
{
    [Export] public ProgressBar DownloadProgressBar { get; set; }

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint())
        {
            _ = Init();
        }
    }

    private async Task Init()
    {
        if (!File.Exists(Path.Combine(MosicConfig.ProcessDirectory, Utils.YtDlpBinaryName)))
        {
            StaticText = "Downloading yt-dlp";
            await Utils.DownloadYtDlp(MosicConfig.ProcessDirectory);
        }

        DownloadProgressBar.Value = 1;

        if (!File.Exists(Path.Combine(MosicConfig.ProcessDirectory, Utils.FfmpegBinaryName)))
        {
            StaticText = "Downloading FFmpeg";
            await Utils.DownloadFFmpeg(MosicConfig.ProcessDirectory);
        }

        DownloadProgressBar.Value = 2;

        if (!File.Exists(Path.Combine(MosicConfig.ProcessDirectory, Utils.FfprobeBinaryName)))
        {
            StaticText = "Downloading FFprobe";
            await Utils.DownloadFFprobe(MosicConfig.ProcessDirectory);
        }

        DownloadProgressBar.Value = 3;
        CallDeferred(MethodName.ChangeSceneToMain);
    }

    private void ChangeSceneToMain()
    {
        GetTree().ChangeSceneToFile("res://scenes/mosic.tscn");
    }
}