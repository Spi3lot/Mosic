using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using Mosic.Scripts.Service;
using Newtonsoft.Json.Linq;

namespace Mosic.Scripts;

public partial class UpdateWindow : Window
{
    [Export]
    public RichTextLabel UpdateInfoLabel { get; set; }

    [Export]
    public Button UpdateButton { get; set; }

    public event Action UpdateAborted;

    public event Action UpdateAccepted;

    public async Task PopupIfUpdateAvailableAsync()
    {
        string extension = Path.GetExtension(OS.GetExecutablePath());
        var latestRelease = await GitHub.Api.Helper.GetLatestReleaseAsync();
        var asset = GitHub.Asset.FindByFileExtension(extension, latestRelease["assets"]);
        var hash = GitHub.Asset.GetHash(asset);

        if (hash.Algorithm != GitHub.Constants.DefaultHashAlgorithm)
        {
            GD.PushError($"Unknown hash algorithm: {hash.Algorithm}");
            UpdateAborted?.Invoke();
            return;
        }

        if (hash.Digest == MosicConfig.Digest)
        {
            UpdateAborted?.Invoke();
            return;
        }

        await SetupUiAsync(latestRelease);
        SetupEventHandlers(asset["browser_download_url"]!.ToString());
        Popup();
        RequestAttention();
    }

    private async Task SetupUiAsync(JToken latestRelease)
    {
        string info = Tr("UPDATE_INFO");
        MosicConfig.Version = await GitHub.Api.Helper.DetermineCurrentVersionAsync();
        UpdateInfoLabel.Text = string.Format(info, latestRelease["tag_name"], MosicConfig.Version);
    }

    private void SetupEventHandlers(string downloadUrl)
    {
        CloseRequested += Hide + UpdateAborted;
        UpdateButton.Pressed += Hide + UpdateAccepted;
        
        UpdateButton.Pressed += async () =>
        {
            string executablePath = await GitHub.Api.Helper.DownloadAndInstallUpdateAsync(downloadUrl);

            if (executablePath == null)
            {
                UpdateAborted?.Invoke();
                return;
            }

            string replace = CmdlineUserArgs.Set(CmdlineUserArgs.Replace, MosicConfig.ProcessPath);
            string[] args = [..OS.GetCmdlineArgs(), CmdlineUserArgs.UserArgDelimiter, replace];
            OS.CreateProcess(executablePath, args, openConsole: true);
            GetTree().Quit();
        };
    }
}