using System.Numerics.Tensors;

namespace BasicPitch;

using TP = TensorPrimitives;

public record struct NotesConvertOptions(
    float OnsetThreshold = 0.5f, // 分割 - 合并 音符的力度 [0.05, 0.95]
    float FrameThreshold = 0.3f, // 多 - 少 模型生成音符的置信度 [0.05, 0.95]
    int MinNoteLength = 11, // 最短音符时长限制 [3, 50] ms
    int EnergyThreshold = 11, // 能量限制
    float? MinFreq = null, // 音调下限 [0, 2000] Hz
    float? MaxFreq = null, // 音调上限 [40, 3000] Hz
    bool InferOnsets = true, // 是否通过后处理算法生成音符
    bool IncludePitchBends = true, // 弯音检测
    bool MelodiaTrick = true // 泛音检测
    );

public class NotesConverter
{
    private ModelOutput input;

    public NotesConverter(ModelOutput input)
    {
        this.input = input;
    }

    public IList<Note> Convert(NotesConvertOptions opt)
    {
        var notes = ToNotesPolyphonic(opt);
        if (opt.IncludePitchBends)
        {
            GetPitchBend(ref notes);
        }
        return ToNoteList(notes);
    }

    // 将模型识别数据转换为复音音符
    private IList<InterNote> ToNotesPolyphonic(NotesConvertOptions opt)
    {
        var (onsets, frames) = NotesHelper.ConstrainFrequency(input.Onsets, input.Notes, opt.MaxFreq, opt.MinFreq);
        if (opt.InferOnsets)
        {
            onsets = NotesHelper.GetInferedOnsets(onsets, frames);
        }

        var notes = new List<InterNote>();
        if (frames.Data == null)
        {
            return notes;
        }
        var remainingEnergy = new float[frames.Data.Length];
        var frameData = frames.Data!;
        frameData.CopyTo(remainingEnergy, 0);
        var onsetIdxs = NotesHelper.FindValidOnsetIndexs(onsets, opt.OnsetThreshold).Reverse();

        var frameStep = (int)frames.Shape!.Last();
        var nFrames = frames.Shape!.First();
        var nFramesMinus1 = nFrames - 1;
        var energySpan = remainingEnergy.AsTensorSpan(frames.Shape);
        foreach (var idx in onsetIdxs)
        {
            var noteStartIdx = idx / frameStep;
            var freqIdx = idx % frameStep;

            if (noteStartIdx >= nFramesMinus1)
            {
                continue;
            }

            var i = noteStartIdx + 1;
            var k = 0;
            while ((i < nFrames - 1) && (k < opt.EnergyThreshold))
            {
                if (remainingEnergy[i * frameStep + freqIdx] < opt.FrameThreshold)
                {
                    k += 1;
                }
                else
                {
                    k = 0;
                }
                i += 1;
            }

            i -= k;

            if (i - noteStartIdx <= opt.MinNoteLength)
            {
                continue;
            }

            // 清空子矩阵
            var colLeft = freqIdx;
            var colRight = freqIdx + 1;
            if (freqIdx < Constants.MAX_FREQ_IDX)
            {
                colRight += 1;
            }
            if (freqIdx > 0)
            {
                colLeft -= 1;
            }
            energySpan.Slice([noteStartIdx..i, colLeft..colRight]).Clear();

            // 求 idx 开始到最后一行的那一列中数据的平均值当作 amplitude
            var amplitude = MathTool.Mean<float>(frameData, idx, frameStep, i - noteStartIdx);
            notes.Add(new InterNote(noteStartIdx, i, freqIdx + Constants.MIDI_OFFSET, amplitude));
        }

        if (opt.MelodiaTrick)
        {
            float maxValue = 0;
            int maxIdx = 0;
            float amplitude = 0;
            int i = 0;
            int k = 0;
            int startPos = 0;


            while (true)
            {
                maxIdx = TP.IndexOfMax(remainingEnergy);
                maxValue = remainingEnergy[maxIdx];
                if (maxValue <= opt.FrameThreshold)
                {
                    break;
                }

                var iMid = maxIdx / frameStep;
                var freqIdx = maxIdx % frameStep;
                remainingEnergy[iMid * frameStep + freqIdx] = 0;

                i = iMid + 1;
                k = 0;
                while ((i < nFrames - 1) && (k < opt.EnergyThreshold))
                {
                    startPos = i * frameStep + freqIdx;
                    if (remainingEnergy[startPos] < opt.FrameThreshold)
                    {
                        k += 1;
                    }
                    else
                    {
                        k = 0;
                    }
                    remainingEnergy[startPos] = 0;
                    if (freqIdx < Constants.MAX_FREQ_IDX)
                    {
                        remainingEnergy[startPos + 1] = 0;
                    }
                    if (freqIdx > 0)
                    {
                        remainingEnergy[startPos - 1] = 0;
                    }
                    i += 1;
                }
                var iEnd = i - 1 - k;

                i = iMid - 1;
                k = 0;
                while (i > 0 && k < opt.EnergyThreshold)
                {
                    startPos = i * frameStep + freqIdx;
                    if (remainingEnergy[startPos] < opt.FrameThreshold)
                    {
                        k += 1;
                    }
                    else
                    {
                        k = 0;
                    }
                    remainingEnergy[startPos] = 0;
                    if (freqIdx < Constants.MAX_FREQ_IDX)
                    {
                        remainingEnergy[startPos + 1] = 0;
                    }
                    if (freqIdx > 0)
                    {
                        remainingEnergy[startPos - 1] = 0;
                    }
                    i -= 1;
                }
                var iStart = i + 1 + k;

                if (iStart < 0)
                {
                    throw new Exception($"iStart is: {iStart}");
                }
                if (iEnd >= nFrames)
                {
                    throw new Exception($"iEnd is: {iEnd}, nFrames is: {nFrames}");
                }

                var iLen = iEnd - iStart;
                if (iLen <= opt.MinNoteLength)
                {
                    continue;
                }

                amplitude = MathTool.Mean<float>(frameData, iStart * frameStep + freqIdx, frameStep, iLen);
                notes.Add(new InterNote(iStart, iEnd, freqIdx + Constants.MIDI_OFFSET, amplitude));
            }
        }
        return notes;
    }

    private void GetPitchBend(ref IList<InterNote> notes, int nBinsTolerance = 25)
    {
        if (input.Contours.Data == null || notes.Count == 0) return;
        var contourSpan = input.Contours.Data!.AsSpan();
        var contourStep = (int)input.Contours.Shape!.Last();

        var windowLen = nBinsTolerance * 2 + 1;
        var freqGaussianSpan = NotesHelper.MakeGaussianWindow(windowLen, 5).AsSpan();
        int freqIdx;
        int freqStartIdx;
        int freqEndIdx;
        int gaussianIdxStart;
        int gaussianIdxEnd;
        int cols;
        int rows;
        float pbShift;
        float maxValue = 0;
        int maxIdx = 0;
        int mulLength;

        var pitchBendSubMatrix = new float[Constants.N_FREQ_BINS_CONTOURS];
        var bends = new List<float>();
        foreach (InterNote note in notes)
        {
            freqIdx = (int)Math.Round(NotesHelper.MidiPitchToContourBin(note.Pitch));
            freqStartIdx = Math.Max(freqIdx - nBinsTolerance, 0);
            freqEndIdx = Math.Min(Constants.N_FREQ_BINS_CONTOURS, freqIdx + nBinsTolerance + 1);

            rows = note.IEndTime - note.IStartTime;
            cols = freqEndIdx - freqStartIdx;
            if (pitchBendSubMatrix.Length < cols)
            {
                pitchBendSubMatrix = new float[cols];
            }
            pitchBendSubMatrix.AsSpan().Fill(float.MinValue);

            // gaussian 向量
            gaussianIdxStart = Math.Max(nBinsTolerance - freqIdx, 0);
            gaussianIdxEnd = windowLen - Math.Max(freqIdx - (Constants.N_FREQ_BINS_CONTOURS - nBinsTolerance - 1), 0);
            if (gaussianIdxStart >= freqGaussianSpan.Length || gaussianIdxEnd > freqGaussianSpan.Length)
            {
                throw new Exception($"GetPitchBend faild, guassian idx error: [{gaussianIdxStart},{gaussianIdxEnd}] {freqGaussianSpan.Length}");
            }

            //将子矩阵拆成行向量，和 gaussian 进行星乘运算
            bends.Clear();
            pbShift = -(float)(nBinsTolerance - Math.Max(0, nBinsTolerance - freqIdx));
            for (int i = 0; i < rows; ++i)
            {
                var start = (note.IStartTime + i) * contourStep + freqStartIdx;
                mulLength = Math.Min(cols, gaussianIdxEnd - gaussianIdxStart);
                var pstart = contourSpan.Slice(start, mulLength);
                var gaussianStart = freqGaussianSpan.Slice(gaussianIdxStart, mulLength);
                TP.Multiply(pstart, gaussianStart, pitchBendSubMatrix);
                // 求1个 bend
                maxIdx = TP.IndexOfMax(pitchBendSubMatrix.AsSpan().Slice(0, mulLength));
                maxValue = pitchBendSubMatrix[maxIdx];
                bends.Add((float)maxIdx);
            }
            if (bends.Count > 0)
            {
                note.PitchBend = bends.ToArray();
                TP.Add(note.PitchBend!, pbShift, note.PitchBend!);
            }
        }
    }

    private IList<Note> ToNoteList(in IList<InterNote> notes)
    {
        if (notes.Count == 0 || input.Contours.Shape == null)
        {
            // 返回一个空数组
            return new List<Note>();
        }

        var i = (int)(input.Contours.Shape!.First());
        var times = NotesHelper.ModelFrameToTime(i);

        return notes.Select(i => new Note(
            times[i.IStartTime],
            times[i.IEndTime],
            i.Pitch,
            i.Amplitude,
            i.PitchBend
        )).ToList();
    }
}

class NotesHelper
{
    public static int HzToMidi(float freq)
    {
        return (int)Math.Round(12 * (Math.Log2(freq) - Math.Log2(440.0)) + 69);
    }

    public static float MidiToHz(int pitch)
    {
        return (float)(Math.Pow(2, (pitch - 69) / 12f) * 440);
    }

    public static float[] ModelFrameToTime(int n)
    {
        var frames = MathTool.ARange(0f, 1f, n);
        var oriTimes = FramesToTime(frames);

        TP.Divide(frames, (float)Constants.ANNOT_N_FRAMES, frames);
        TP.Floor(new ReadOnlySpan<float>(frames), frames);

        var windowOffset = (float)Constants.FFT_HOP / (float)Constants.AUDIO_SAMPLE_RATE
            * ((float)Constants.ANNOT_N_FRAMES - (float)Constants.AUDIO_N_SAMPLES / (float)Constants.FFT_HOP)
            + 0.0018f;

        TP.Multiply(frames, windowOffset, frames);
        TP.Subtract(oriTimes, frames, frames);
        return frames;
    }

    public static (Tensor, Tensor) ConstrainFrequency(in Tensor onsets, in Tensor frames, float? maxFreq, float? minFreq)
    {
        if (maxFreq == null && minFreq == null)
        {
            return (onsets, frames);
        }

        // 如果设置了最大或者最小，就需要复制内存
        var newOnsets = onsets.DeepClone();
        var newFrames = frames.DeepClone();

        if (maxFreq != null)
        {
            var pitch = HzToMidi(maxFreq.Value) - Constants.MIDI_OFFSET;
            var r = Range.StartAt(pitch);
            ZeroPitch(ref newOnsets, r);
            ZeroPitch(ref newFrames, r);
        }

        if (minFreq != null)
        {
            var pitch = HzToMidi(minFreq.Value) - Constants.MIDI_OFFSET;
            var r = Range.EndAt(pitch);
            ZeroPitch(ref newOnsets, r);
            ZeroPitch(ref newFrames, r);
        }

        return (newOnsets, newFrames);
    }

    public static Tensor GetInferedOnsets(in Tensor onsets, in Tensor frames, int nDiff = 2)
    {
        if (frames.Data == null)
        {
            return new Tensor(null, null);
        }

        // 计算差异
        var frameData = frames.Data!;
        int frameSize = (int)frames.Shape!.Last();
        int totalFrameSize = frameData.Length;
        float[] diffs = new float[nDiff * totalFrameSize];
        var diffsSpan = diffs.AsSpan();
        for (int i = 0; i < nDiff; i++)
        {
            var start = i * totalFrameSize;
            var offset = frameSize * (i + 1);
            var length = Math.Max(totalFrameSize - offset, 0);
            if (length > 0)
            {
                Array.Copy(frameData, 0, diffs, start + offset, length);
            }
            var dest = diffsSpan.Slice(start, totalFrameSize);
            TP.Subtract(frameData, dest, dest);
        }

        // 求每一列的最小值, 存到 diffs 中的第一行。numpy: frame_diff = np.min(diffs, axis = 0)
        var frameDiff = diffsSpan.Slice(0, totalFrameSize);
        for (int i = 1; i < nDiff; i++)
        {
            TP.Min(diffsSpan.Slice(i * totalFrameSize, totalFrameSize), frameDiff, frameDiff);
        }

        // 设定阈值。numpy: frame_diff[frame_diff < 0] = 0
        TP.Max(frameDiff, 0f, frameDiff);

        // numpy: frame_diff[:n_diff, :] = 0
        diffsSpan.Slice(0, nDiff * frameSize).Clear();

        // numpy: frame_diff = np.max(onsets) * frame_diff / np.max(frame_diff)
        var onsetData = onsets.Data!;
        {
            var maxDiff = TP.Max(frameDiff);
            float i = TP.Max(onsetData);
            if (maxDiff != 0f)
            {
                i = i / maxDiff;
            }
            TP.Multiply(frameDiff, i, frameDiff);
        }

        // numpy: max_onsets_diff = np.max([onsets, frame_diff], axis = 0)
        float[] ret = new float[onsetData.Length];
        TP.Max(frameDiff, onsetData, ret);
        nint[] shape = new nint[onsets.Shape!.Length];
        onsets.Shape!.CopyTo(shape, 0);
        return new Tensor(ret, shape);
    }

    public static IList<int> FindValidOnsetIndexs(in Tensor onsets, float threshold)
    {
        if (onsets.Shape!.First() < 3)
        {
            return [];
        }

        // 这个算法过程就是查找矩阵中每一列中的极大值位置(scipy.signal.argrelmax)
        // 对于找出来的极大值通过一个阈值 threshold 再过滤一下来完成。
        // 基于这个启发，也可以先过滤，生成一个只有大于阈值位置的列表，再找极大值。这样大数据量就能极大的减少迭代的次数。
        // 但是 .net 没有提供类似于 vDSP.compress 这样根据掩码提炼数组的接口，所以能提供的加速比较有限。
        // 这里就只生成了一个掩码数组，然后根据掩码做普通的求极大值过程。
        var data = onsets.Data!;
        float[] mask = new float[data.Length];
        TP.Min(data, threshold, mask);

        // 极大值只能在中间位置，遍历的时候只需要遍历 [1, n - 2] 
        var step = (int)onsets.Shape!.Last();
        var limit = mask.Length - step;
        float v;
        var ret = new List<int>();
        for (int i = step; i < limit; ++i)
        {
            if (mask[i] < threshold) continue;
            v = data[i];
            if ((v > data[i - step]) && (v > data[i + step]))
            {
                ret.Add(i);
            }
        }
        return ret;
    }

    // 生成高斯信号窗口：scipy.signal.windows.gaussian
    public static float[] MakeGaussianWindow(int count, int std)
    {
        if (count <= 0)
        {
            return [];
        }
        if (count == 1)
        {
            return [1.0f];
        }
        // n = np.arange(0, M) - (M - 1.0) / 2.0
        var n = MathTool.ARange(-0.5f * (count - 1), 1.0f, count);
        var sig2 = (float)(std * std * 2);
        // w = np.exp(-n ** 2 / sig2)
        TP.Multiply(n, n, n);
        TP.Divide(n, -sig2, n);
        TP.Exp(n, n);

        return n;
    }

    public static float MidiPitchToContourBin(int pitch)
    {
        var hz = MidiToHz(pitch);
        return 12f * Constants.CONTOURS_BINS_PER_SEMITONE * (float)Math.Log2(hz / Constants.ANNOTATIONS_BASE_FREQUENCY);
    }

    private static float[] FramesToTime(in float[] frames)
    {
        float sr = (float)Constants.AUDIO_SAMPLE_RATE;
        float hopLength = (float)Constants.FFT_HOP;
        var samples = new float[frames.Length];

        // frames to samples
        TP.Multiply(frames, hopLength, samples);
        TP.Floor(new ReadOnlySpan<float>(samples), samples);

        // samples to time
        TP.Divide(samples, sr, samples);

        return samples;
    }

    private static void ZeroPitch(ref Tensor t, Range pitchRange)
    {
        if (t.Data == null) return;

        var limit = t.Shape![1];
        var l = pitchRange.Start.Value;
        if (l < 0 || l > limit) return;
        var r = pitchRange.End.Equals(Index.End) ? limit : pitchRange.End.Value;
        if (r < 0 || r > limit || r < l) return;

        var span = t.Data.AsTensorSpan(t.Shape);
        span.Slice([Range.All, pitchRange]).Clear();
    }
}


record InterNote(int IStartTime, int IEndTime, int Pitch, float Amplitude, float[]? PitchBend = null)
{
    public float[]? PitchBend { get; set; } = PitchBend;
}