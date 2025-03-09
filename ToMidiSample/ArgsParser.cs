using BasicPitch;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace ToMidiSample;

struct ArgsParser
{
    public NotesConvertOptions NotesOpt;
    public MidiWriteOptions MidiOpt;
    public readonly string AudioPath;
    public readonly string MidiPath;

    public ArgsParser(string[] args)
    {
        var conf = ParseArgs(args);
        AudioPath = conf["audio_path"] ?? "";
        MidiPath = conf["midi_path"] ?? "";
        NotesOpt = new NotesConvertOptions();
        MidiOpt = new MidiWriteOptions();

        NotesOpt.IncludePitchBends = ParseBool(conf["pitch_bend"], false);
        NotesOpt.OnsetThreshold = ParseFloat(conf["onset_threshold"], 0.05f, 0.95f, NotesOpt.OnsetThreshold);
        NotesOpt.FrameThreshold = ParseFloat(conf["frame_threshold"], 0.05f, 0.95f, NotesOpt.FrameThreshold);
        MidiOpt.MultiplePitchBends = ParseBool(conf["multi_midi_track"], false);
    }

    private static float ParseFloat(string? raw, float min, float max, float def)
    {
        if (raw == null) return def;
        try
        {
            float v = float.Parse(raw);
            return Math.Clamp(v, min, max);
        }
        catch
        {
            return def;
        }
    }

    private static bool ParseBool(string? raw, bool def)
    {
        if (raw == null) return def;
        try
        {
            return bool.Parse(raw);
        }
        catch
        {
            return def;
        }
    }

    private static IConfigurationRoot ParseArgs(string[] args)
    {
        var switchMappings = new Dictionary<string, string>()
           {
               { "-a", "audio_path" },
               { "-m", "midi_path" },
               { "-o", "onset_threshold" },
               { "-f", "frame_threshold" },
               { "--pitch_bend", "pitch_bend" },
               { "--multi_midi_track", "multi_midi_track" },
           };
        var builder = new ConfigurationBuilder();
        builder.AddCommandLine(args, switchMappings);

        return builder.Build();
    }

    public static void Help()
    {
        // 获取当前进程
        var processPath = Process.GetCurrentProcess().MainModule?.FileName ?? "ToMidiSample";
        var processName = Path.GetFileNameWithoutExtension(processPath);
        Console.WriteLine($"Usage: {processName} -a <audio file path> [-m midi save path] [-o onset threshold] [-f frame threshold] [--pitch_bend true] [--multi_midi_track true]");
        Console.WriteLine($"onset_threshold: 0.05 - 0.95");
        Console.WriteLine($"frame_threshold: 0.05 - 0.95");
    }
}
