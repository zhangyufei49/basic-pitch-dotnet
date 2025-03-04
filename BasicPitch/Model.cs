using Microsoft.AI.MachineLearning;
using NAudio.Wave;
using System.Reflection;
using Windows.Storage.Streams;

namespace BasicPitch;

public class Model
{
    private LearningModel model;
    private LearningModelSession session;
    private OutputName outputName;

    public Model()
    {
        // 模型很小，同步加载即可
        model = LoadModel();
        // 这个模型本身很多算符不支持 gpu，所以不用考虑 gpu 加速了，直接用默认设备创建
        session = new LearningModelSession(model);
        // 模型输出的名字是固定的，可以提前获取排序好
        outputName = new OutputName(model);
    }

    public ModelOutput Predict(WaveBuffer waveBuffer, Action<double>? progressHandler = null)
    {
        // 创建输入绑定
        var binding = new LearningModelBinding(session);
        var output = new ModelOutputHelper();

        // 迭代预测
        var it = new ModelInput(waveBuffer, model.InputFeatures[0]);
        foreach (var i in it.Enumerate())
        {
            // 预测一次
            binding.Bind(model.InputFeatures[0].Name, i.Item1);
            var result = session.Evaluate(binding, "");
            // 获取输出结果
            output.contours.Add(GetResult(result, outputName.Contour));
            output.notes.Add(GetResult(result, outputName.Note));
            output.onsets.Add(GetResult(result, outputName.Onset));
            // 进度回调
            progressHandler?.Invoke(i.Item2);
        }
        // 将收集的结果转换后返回
        return output.Create();
    }

    private static TensorFloat GetResult(LearningModelEvaluationResult result, string name)
    {
        return (result.Outputs[name] as TensorFloat)!;
    }

    private static LearningModel LoadModel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = "nmp.onnx";

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(resourceName))
            {
                resourceName = name;
                break;
            }
        }

        using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
        {
            var rstream = ConvertToRandomAccessStream(stream).GetAwaiter().GetResult();
            var s = RandomAccessStreamReference.CreateFromStream(rstream);
            return LearningModel.LoadFromStream(s);
        }
    }

    private static async Task<IRandomAccessStream> ConvertToRandomAccessStream(Stream stream)
    {
        InMemoryRandomAccessStream randomAccessStream = new InMemoryRandomAccessStream();
        using (DataWriter writer = new DataWriter(randomAccessStream.GetOutputStreamAt(0)))
        {
            byte[] buffer = new byte[stream.Length];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            writer.WriteBytes(buffer);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }
        randomAccessStream.Seek(0);
        return randomAccessStream;
    }
}

class OutputName
{
    public readonly string Contour;
    public readonly string Note;
    public readonly string Onset;

    public OutputName(LearningModel model)
    {
        var names = model.OutputFeatures.Select(i => i.Name).ToList();
        names.Sort();

        Contour = names[0];
        Note = names[1];
        Onset = names[2];
    }
}

file class ModelOutputHelper
{
    public List<TensorFloat> contours = new List<TensorFloat>();
    public List<TensorFloat> notes = new List<TensorFloat>();
    public List<TensorFloat> onsets = new List<TensorFloat>();

    public ModelOutput Create()
    {
        return new ModelOutput(new Tensor(contours), new Tensor(notes), new Tensor(onsets));
    }
}
