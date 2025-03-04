using System.Diagnostics;

namespace ToMidiSample;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            // 获取当前进程
            var processName = Process.GetCurrentProcess().MainModule?.FileName ?? "ToMidiSample";
            Log($"Usage: {processName} <audio file path>");
            return;
        }

        // 加载音频
        var audioFile = args[0];
        Log($"Load audio file: ${audioFile}");
        BasicPitch.AudioReader reader;
        try
        {
            reader = new BasicPitch.AudioReader(audioFile);
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
        var model = new BasicPitch.Model();

        // 进行预测
        Log("Predicting");
        model.Predict(audioBuffer, (double p) =>
        {
            Log($"====> prediction progress: {p}", ConsoleColor.DarkYellow);
        });

        // 保存 midi 文件
        Log("Save MIDI");
    }

    private static void Log(string msg, ConsoleColor color = ConsoleColor.DarkGreen)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = ConsoleColor.Gray;
    }
}
