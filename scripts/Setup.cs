using System;
using System.Threading.Tasks;

using Godot;

using Microsoft.Extensions.DependencyInjection;

using Mosic.Scripts.Service;

namespace Mosic.Scripts;

public partial class Setup : Control
{
    private readonly IMediaDownloader _mediaDownloader = ServiceManager.Provider.GetRequiredService<IMediaDownloader>();

    [Export] public CyclePaddedLabel DownloadLabel { get; set; }
    [Export] public ProgressBar DownloadProgressBar { get; set; }
    [Export] public UpdateWindow UpdateWindow { get; set; }

    public override void _EnterTree()
    {
        Engine.GetMainLoop().OnRequestPermissionsResult += (permission, granted) =>
        {
            GD.Print($"Permission {permission} was {(granted ? "granted" : "not granted")}");
        };

        if (OS.IsUserfsPersistent())
        {
            OS.RequestPermissions();
        }

        if (!_mediaDownloader.SupportsAutoUpdateAndSetup)
        {
            CallDeferred(MethodName.ChangeSceneToMain);
            return;
        }

        UpdateWindow.UpdateAborted += async () =>
        {
            await DownloadBinariesAsync();
            CallDeferred(MethodName.ChangeSceneToMain);
        };

        UpdateWindow.UpdateAvailable += () => DownloadLabel.StaticText = "Waiting for a decision";
        UpdateWindow.UpdateAccepted += () => DownloadLabel.StaticText = "Updating";
        _ = UpdateWindow.PopupIfUpdateAvailableAsync();
    }

    private async Task DownloadBinariesAsync()
    {
        DownloadLabel.StaticText = "Downloading FFmpeg";
        DownloadProgressBar.Indeterminate = false;

        var progress = new Progress<double>(p =>
            DownloadProgressBar.Value = Mathf.Lerp(DownloadProgressBar.MinValue, DownloadProgressBar.MaxValue, p));

        await _mediaDownloader.EnsureDependenciesAsync(progress);
    }

    private void ChangeSceneToMain() =>
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/mosic.tscn");
}

