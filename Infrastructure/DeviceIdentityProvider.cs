namespace ProjectTimeTracker.Infrastructure;

public sealed class DeviceIdentityProvider
{
    private readonly string _filePath;

    public DeviceIdentityProvider()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectTimeTracker");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "device.id");
    }

    public string GetOrCreateDeviceId()
    {
        if (File.Exists(_filePath))
        {
            string existing = File.ReadAllText(_filePath).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }

        string created = Guid.NewGuid().ToString("N");
        File.WriteAllText(_filePath, created);
        return created;
    }
}

