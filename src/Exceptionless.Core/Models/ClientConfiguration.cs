namespace Exceptionless.Core.Models;

public class ClientConfiguration
{
    public int Version { get; set; }
    public SettingsDictionary Settings { get; private set; } = new();

    public void IncrementVersion()
    {
        Version++;
    }
}
