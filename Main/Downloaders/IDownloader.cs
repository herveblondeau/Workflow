namespace Main.Downloaders;

public interface IDownloader : IDisposable
{
    DownloaderState State { get; }

    Task<Stream> Download(string sourceUrl, int targetSampleRate, int targetNbChannels, int targetBitsPerSample);
}
