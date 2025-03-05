using Windows.AI.MachineLearning;

namespace BasicPitch;

public class Tensor
{
    public readonly float[]? Data;
    public readonly long[]? Shape;

    public Tensor(List<TensorFloat> t)
    {
        if (t.Count > 0)
        {
            // 确定形状
            var s = t[0].Shape;
            this.Shape = s.ToArray();
            this.Shape[0] = this.Shape[0] * t.Count;
            // 确定内存大小
            var total = t[0].GetAsVectorView().Count * t.Count;
            this.Data = new float[total];
            // 拷贝数据
            int offset = 0;
            foreach (var i in t)
            {
                var src = i.GetAsVectorView();
                if (src is ICollection<float> source)
                {
                    source.CopyTo(this.Data, offset);
                    offset += source.Count;
                }
                else
                {
                    foreach (var j in src)
                    {
                        this.Data[offset] = j;
                        offset += 1;
                    }
                }
            }

        }
    }
}

public class ModelOutput
{
    public readonly Tensor contours;
    public readonly Tensor notes;
    public readonly Tensor onsets;

    public ModelOutput(Tensor c, Tensor n, Tensor o)
    {
        contours = c;
        notes = n;
        onsets = o;
    }
}
