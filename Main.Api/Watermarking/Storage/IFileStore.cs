namespace Main.Api.Watermarking.Storage;

public interface IFileStore
{
    string GetNewFilePath();
    void Delete(string filePath);
    IEnumerable<string> GetAllFiles();
}

public class LocalFileStore : IFileStore
{
    private readonly string _storagePath;

    public LocalFileStore(IConfiguration configuration)
    {
        _storagePath = configuration["Filigrane:StoragePath"]
            ?? Path.Combine(Path.GetTempPath(), "workflow-filigrane-storage");

        Directory.CreateDirectory(_storagePath);
    }

    public string GetNewFilePath() =>
        Path.Combine(_storagePath, $"{Guid.NewGuid()}.pdf");

    public void Delete(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public IEnumerable<string> GetAllFiles() =>
        Directory.EnumerateFiles(_storagePath, "*.pdf");
}
