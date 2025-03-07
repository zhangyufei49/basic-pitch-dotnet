﻿using System.Numerics;
using System.Numerics.Tensors;

namespace BasicPitch;

class MathTool
{
    public static float[] ARange((float, float) range, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0) return Array.Empty<float>();
        if (count == 1) return new[] { range.Item2 };

        float step = (range.Item2 - range.Item1) / (count - 1);

        var data = new float[count];
        for (int i = 0; i < data.Length - 1; i++)
        {
            data[i] = range.Item1 + i * step;
        }
        data[data.Length - 1] = range.Item2;
        return data;
    }

    public static float[] ARange(float start, float step, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0) return Array.Empty<float>();
        if (count == 1) return new[] { start };

        var data = new float[count];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = start + i * step;
        }

        return data;
    }

    public static T Mean<T>(in ReadOnlySpan<T> data, int skip, int step, int length) where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        dynamic sum = default(T)!;
        if (length <= 0) return sum;

        for (int i = 0; i < length; ++i)
        {
            sum += data[skip + i * step];
        }
        return sum / length;
    }
}
