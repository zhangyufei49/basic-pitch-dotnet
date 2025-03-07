using Windows.AI.MachineLearning;
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
            output.Contours.Add(GetResult(result, outputName.Contour));
            output.Notes.Add(GetResult(result, outputName.Note));
            output.Onsets.Add(GetResult(result, outputName.Onset));
            // 进度回调
            progressHandler?.Invoke(i.Item2);
        }
        // 将收集的结果转换后返回
        return output.Create(waveBuffer.FloatBufferCount);
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
            stream.ReadExactly(buffer);
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
    public readonly List<TensorFloat> Contours = new List<TensorFloat>();
    public readonly List<TensorFloat> Notes = new List<TensorFloat>();
    public readonly List<TensorFloat> Onsets = new List<TensorFloat>();

    public ModelOutput Create(int totalFrames)
    {
        return new ModelOutput(Unwrap(Contours, totalFrames), Unwrap(Notes, totalFrames), Unwrap(Onsets, totalFrames));
    }

    private static Tensor Unwrap(IList<TensorFloat> t, int totalFrames)
    {
        if (t.Count == 0)
        {
            return new Tensor(null, null);
        }
        var nOlap = Constants.N_OVERLAPPING_FRAMES / 2;
        var nOutputFramesOri = totalFrames * Constants.ANNOTATIONS_FPS / Constants.AUDIO_SAMPLE_RATE;
        var step = (int)t[0].Shape.Last();
        int[] oriShape = [t.Count, t[0].GetAsVectorView().Count / step];
        var shape0 = Math.Min(oriShape[0] * oriShape[1] - nOlap * 2, nOutputFramesOri);
        var rangeStart = nOlap * step;
        var rangeCount = (oriShape[1] - nOlap) * step - rangeStart;

        // 确定形状和需要的内存
        var shape = new nint[] { shape0, step };
        var data = new float[shape[0] * shape[1]];
        // 填充数据
        int size = 0;
        foreach (var i in t)
        {
            var src = i.GetAsVectorView().Skip(rangeStart).Take(rangeCount);
            foreach (var v in src)
            {
                data[size] = v;
                size += 1;
                if (size == data.Length)
                {
                    break;
                }
            }
        }
        return new Tensor(data, shape);
    }

}
