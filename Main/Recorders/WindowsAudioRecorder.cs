using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Main.Recorders;

public class WindowsAudioRecorder : IRecorder
{
    private WasapiLoopbackCapture _audioCapture = null!;
    private MemoryStream _audioStream = null!;

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        _audioStream = new MemoryStream();

        _audioCapture = new WasapiLoopbackCapture(device);
        _audioCapture.DataAvailable += _audioCapture_DataAvailable;
        _audioCapture.WaveFormat = new WaveFormat(sampleRate, bitsPerSample, nbChannels);
        _audioCapture.StartRecording();
    }

    private void _audioCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0)
        {
            _audioStream.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    public async Task<Stream> Stop()
    {
        // Wait for the recording to stop
        // Note: calling StopRecording() only requests a stoppage. We have to query the object state to ensure it's actually stopped
        _audioCapture.StopRecording();
        while (_audioCapture.CaptureState != CaptureState.Stopped)
        {
            await Task.Delay(50);
        }

        // Clean up
        _audioCapture.DataAvailable -= _audioCapture_DataAvailable;
        _audioCapture.Dispose();

        return GetRecordedStream();
    }

    public Stream GetRecordedStream()
    {
        _audioStream.Position = 0;
        return _audioStream;
    }

    public void Dispose()
    {
        _audioCapture?.Dispose();
        _audioStream?.Dispose();
    }
}
