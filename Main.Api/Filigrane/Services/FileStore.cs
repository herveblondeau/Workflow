namespace Main.Api.Filigrane.Services;

public interface IFileStore
{
    string GetNewFilePath(string extension);
    void Delete(string filePath);
    IEnumerable<string> GetAllFiles(string extension);
}

public class LocalFileStore : IFileStore
{
    private readonly string _storagePath;

    public LocalFileStore(IConfiguration configuration)
    {
        _storagePath = configuration["Filigrane:StoragePath"]
            ?? throw new InvalidOperationException("Filigrane:StoragePath is not configured.");
        Directory.CreateDirectory(_storagePath);
    }

    public string GetNewFilePath(string extension)
    {
        if (!extension.StartsWith('.')) extension = "." + extension;
        return Path.Combine(_storagePath, $"{Guid.NewGuid()}{extension}");
    }

    public void Delete(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public IEnumerable<string> GetAllFiles(string extension)
    {
        if (!extension.StartsWith('.')) extension = "." + extension;
        return Directory.EnumerateFiles(_storagePath, $"*{extension}");
    }
}
