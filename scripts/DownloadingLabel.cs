using System.Threading.Tasks;
using Godot;
using YoutubeDLSharp;

namespace Mosic.scripts;

[Tool]
public partial class DownloadingLabel : CyclePaddedLabel
{
    public override void _EnterTree()
    {
        _ = Init();
    }

    private async Task Init()
    {
        await Utils.DownloadBinaries(true, MosicConfig.ProcessDirectory);
        CallDeferred(MethodName.ChangeSceneToMain);
    }

    private void ChangeSceneToMain()
    {
        GetTree().ChangeSceneToFile("res://scenes/mosic.tscn");
    }
}