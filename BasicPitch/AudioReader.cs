using NAudio.Wave;
namespace BasicPitch;

public class AudioReader
{
    static readonly WaveFormat TargetFormat = WaveFormat.CreateIeeeFloatWaveFormat(Constants.AUDIO_SAMPLE_RATE, 1);

    private AudioFileReader reader;
    private MediaFoundationResampler? resampler;
    public readonly long Frames;

    public AudioReader(string fileName)
    {
        reader = new AudioFileReader(fileName);
        var fmt = reader.WaveFormat;
        Frames = reader.Length / fmt.BlockAlign;

        // 处理重采样
        if (fmt.Channels != TargetFormat.Channels || fmt.SampleRate != TargetFormat.SampleRate || fmt.Encoding != TargetFormat.Encoding)
        {
            resampler = new MediaFoundationResampler(reader, TargetFormat);
            Frames = (long)Math.Round((Double)Frames * TargetFormat.SampleRate / (Double)fmt.SampleRate);
        }
    }

    public WaveBuffer ReadAll()
    {
        var buffer = new byte[Frames * sizeof(float)];
        WaveBuffer rbuf = new WaveBuffer(buffer);
        int nread;
        if (resampler == null)
        {
            // 不需要重采样，则直接读取
            nread = reader.Read(buffer, 0, buffer.Length);
        }
        else
        {
            // 执行重采样
            nread = resampler.Read(buffer, 0, buffer.Length);
        }
        rbuf.FloatBufferCount = nread / sizeof(float);
        return rbuf;
    }
}
