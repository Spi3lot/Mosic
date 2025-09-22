using System.Threading.Tasks;

namespace Mosic.Scripts.Service;

public interface IInstaller
{
    /// <returns>The path to the installed executable, or <c>null</c> if none could be determined.</returns>
    Task<string> InstallAsync(string path, byte[] bytes);
}