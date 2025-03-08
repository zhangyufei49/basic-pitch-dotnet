using BasicPitch;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ToMidiSample;

class Program
{
    static void Main(string[] args)
    {
        var pargs = new ArgsParser(args);
        if (pargs.AudioPath.Length == 0)
        {
            ArgsParser.Help();
            return;
        }

        // 加载音频
        var audioFile = pargs.AudioPath;
        Log($"Load audio file: ${audioFile}");
        AudioReader reader;
        try
        {
            reader = new AudioReader(audioFile);
        }
        catch (Exception e)
        {
            Log($"Can not load audio file: {audioFile}");
            Log($"Error: {e.Message}", ConsoleColor.DarkRed);
            return;
        }

        // 进行读取和重采样操作
        var audioBuffer = reader.ReadAll();

        // 模型加载
        Log("Load model");
        var model = new Model();

        // 进行预测
        Log("Predicting");
        var modelOutput = model.Predict(audioBuffer, (double p) =>
        {
            Log($"====> prediction progress: {p}", ConsoleColor.DarkYellow);
        });

        // 生成音符
        Log("Convert to notes with options:");
        ShowOpts(pargs.NotesOpt);
        var notesConverter = new NotesConverter(modelOutput);
        var notes = notesConverter.Convert(pargs.NotesOpt);
        foreach (var note in notes)
        {
            Log(note.ToString(), ConsoleColor.DarkYellow);
        }

        if (pargs.MidiPath.Length > 0)
        {
            Log($"Save MIDI to path [{pargs.MidiPath}] with options:");
            ShowOpts(pargs.MidiOpt);
        }
    }

    private static void Log(string msg, ConsoleColor color = ConsoleColor.DarkGreen)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    private static void ShowOpts<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(T opt)
    {
        // 获取结构体的所有字段
        FieldInfo[] fields = typeof(T).GetFields();

        // 打印字段名称和值
        Log("{", ConsoleColor.DarkMagenta);
        foreach (FieldInfo field in fields)
        {
            Log($"  {field.Name}: {field.GetValue(opt)}", ConsoleColor.Magenta);
        }
        Log("}", ConsoleColor.DarkMagenta);
    }
}
