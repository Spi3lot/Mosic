using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Godot;

namespace Mosic.Scripts.Service;

public class ArchiveInstaller : IInstaller
{
    public async Task<string> InstallAsync(string path, byte[] bytes)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();

        switch (extension)
        {
            case ".zip":
                await File.WriteAllBytesAsync(path, bytes);
                return await InstallZipAsync(path);
            case ".gz" when path.EndsWith(".tar.gz"):
            case ".tar" or ".tgz":
                return await InstallTarAsync(path, bytes, gzip: extension.EndsWith("gz"));
            default:
                await File.WriteAllBytesAsync(path, bytes);

                if (IsExecutable(bytes))
                {
                    return path;
                }

                GD.PushError($"Unknown update format: {extension}");
                return null;
        }
    }

    private static async Task<string> InstallZipAsync(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        using var reader = new ZipReader();
        var error = reader.Open(path);

        if (error != Error.Ok)
        {
            GD.PushError($"Error opening ZipReader for '{path}': {error}");
            return null;
        }

        string executablePath = null;
        var tasks = new List<Task>();

        foreach (string file in reader.GetFiles())
        {
            string destinationPath = Path.Combine(directory, file);
            byte[] bytes = reader.ReadFile(file);
            var task = File.WriteAllBytesAsync(destinationPath, bytes);
            tasks.Add(task);

            if (IsExecutable(bytes))
            {
                executablePath = destinationPath;
            }
        }

        await Task.WhenAll(tasks);
        return executablePath;
    }

    private static async Task<string> InstallTarAsync(string path, byte[] bytes, bool gzip)
    {
        Stream stream = new MemoryStream(bytes);

        if (gzip)
        {
            stream = new GZipStream(stream, CompressionMode.Decompress);
        }

        string directory = Path.GetDirectoryName(path)!;
        string executablePath = null;
        await using var reader = new TarReader(stream);

        while (await reader.GetNextEntryAsync() is { } entry)
        {
            if (entry.Length == 0)
            {
                continue;
            }

            string destinationPath = Path.Combine(directory, entry.Name);
            await entry.ExtractToFileAsync(destinationPath, overwrite: true);

            await using var fileStream = File.OpenRead(destinationPath);
            byte[] fileHeader = new byte[4];
            _ = await fileStream.ReadAsync(fileHeader);

            if (IsExecutable(fileHeader))
            {
                executablePath = destinationPath;
            }
        }

        await stream.DisposeAsync();
        return (executablePath != null) ? Path.GetFullPath(executablePath) : null;
    }

    private static bool IsExecutable(byte[] bytes) => IsWindowsExecutable(bytes) || IsUnixExecutable(bytes);

    private static bool IsWindowsExecutable(byte[] bytes)
    {
        return bytes.Length >= 2
               && bytes[0] == (byte) 'M'
               && bytes[1] == (byte) 'Z';
    }

    private static bool IsUnixExecutable(byte[] bytes)
    {
        return bytes.Length >= 4
               && bytes[0] == 0x7F
               && bytes[1] == (byte) 'E'
               && bytes[2] == (byte) 'L'
               && bytes[3] == (byte) 'F';
    }
}