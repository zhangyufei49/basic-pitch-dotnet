using Windows.AI.MachineLearning;

namespace BasicPitch;

public class Tensor
{
    public readonly float[]? Data;
    public readonly nint[]? Shape;

    public Tensor(float[]? data, nint[]? shape)
    {
        Data = data;
        Shape = shape;
    }

    // 深拷贝
    public Tensor DeepClone()
    {
        float[]? data = null;
        nint[]? shape = null;

        if (Data != null)
        {
            data = new float[Data.Length];
            Data.CopyTo(data, 0);
        }

        if (Shape != null)
        {
            shape = new nint[Shape.Length];
            Shape.CopyTo(shape, 0);
        }

        return new Tensor(data, shape);
    }
}

public class ModelOutput
{
    public readonly Tensor Contours;
    public readonly Tensor Notes;
    public readonly Tensor Onsets;

    public ModelOutput(Tensor c, Tensor n, Tensor o)
    {
        Contours = c;
        Notes = n;
        Onsets = o;
    }
}
