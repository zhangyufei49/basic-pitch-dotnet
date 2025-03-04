using Microsoft.AI.MachineLearning;
using NAudio.Wave;
using System.Diagnostics;

namespace BasicPitch;

class ModelInput
{
    private readonly WaveBuffer waveBuffer;
    private readonly ShapeHelper inputInfo;
    private float[] tensorData;

    public ModelInput(WaveBuffer waveBuffer, ILearningModelFeatureDescriptor descriptor)
    {
        this.waveBuffer = waveBuffer;
        this.inputInfo = new ShapeHelper(descriptor);
        this.tensorData = new float[inputInfo.Count];
    }

    public IEnumerable<(TensorFloat, Double)> Enumerate()
    {
        int cursor = Constants.OVERLAP_LEN / -2;
        int offset = -cursor;
        int totalFrames = waveBuffer.FloatBufferCount;
        var data = waveBuffer.FloatBuffer;

        int n, j;
        // cursor < 0 的部分填充 0
        this.tensorData.AsSpan().Slice(0, offset).Fill(0);
        while (cursor < totalFrames)
        {
            j = Math.Max(0, cursor);
            n = Math.Min(inputInfo.Count - offset, totalFrames - j);
            waveBuffer.FloatBuffer.AsSpan().Slice(j, n).CopyTo(this.tensorData.AsSpan().Slice(offset, n));
            offset += n;

            Debug.WriteLine($"ModelInput processed: [{cursor}, {cursor + inputInfo.Count}]");
            cursor += Constants.HOP_SIZE;
            Debug.WriteLine($"ModelInput progress: {cursor}/{totalFrames} = {(double)cursor / (double)totalFrames}");
            if (offset == inputInfo.Count)
            {
                yield return CreateResult((double)cursor / (double)totalFrames);
            }
            else
            {
                // 最后一次，数据不足的情况补0
                this.tensorData.AsSpan().Slice(offset).Fill(0);
                yield return CreateResult(1.0);
            }
            offset = 0;
        }
    }

    private (TensorFloat, Double) CreateResult(double progress)
    {
        return (TensorFloat.CreateFromArray(inputInfo.Shape, tensorData), Math.Clamp(progress, 0, 1));
    }
}
class ShapeHelper
{
    public readonly long[] Shape;
    public readonly int Count = 1;
    public ShapeHelper(ILearningModelFeatureDescriptor descriptor)
    {
        var featureDescriptor = (descriptor as TensorFeatureDescriptor)!;
        var shape = featureDescriptor.Shape;

        this.Shape = new long[shape.Count];
        for (int i = 0; i < shape.Count; i++)
        {
            // abs 解决负数的问题
            var n = Math.Abs(shape[i]);
            this.Shape[i] = n;
            Count *= (int)n;
        }
    }
}

