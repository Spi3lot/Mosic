using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Microsoft.VisualBasic.FileIO;
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
    
    public event Action UpdateAvailable;

    public event Action UpdateAccepted;

    public async Task PopupIfUpdateAvailableAsync()
    {
        if (ReplacePredecessor())
        {
            UpdateAborted?.Invoke();
            return;
        }
        
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

        UpdateAvailable?.Invoke();
        await SetupUiAsync(latestRelease);
        SetupEventHandlers(asset["browser_download_url"]!.ToString());
        Popup();
        RequestAttention();
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

        FileSystem.DeleteFile(
            replacePath,
            UIOption.AllDialogs,
            RecycleOption.SendToRecycleBin,
            UICancelOption.DoNothing
        );

        GD.Print($"Moved old version from '{replacePath}' to recycle bin.");
        return true;
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
            OS.CreateProcess(executablePath, args);
            GetTree().Quit();
        };
    }
}