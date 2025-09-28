namespace Mosic.Scripts.Service;

public static class CmdlineUserArgs
{
    public const string UserArgDelimiter = "++";
    
    public const string Replace = "--replace";
    
    public static string Set(string key, string value) => $"{key}=\"{value}\"";
}