using NAudio.Midi;
using System.Numerics.Tensors;

namespace BasicPitch;

public record struct MidiWriteOptions
{
    public int Tempo = 120; // 拍速
    public int Patch = 4; // 音色
    public bool MultiplePitchBends = false; // 每个 pitch 单独一个音轨

    public MidiWriteOptions() { }
}

public class MidiWriter
{
    private List<Note> notes;

    public MidiWriter(List<Note> notes)
    {
        this.notes = notes;
    }

    public MidiEventCollection Write(MidiWriteOptions opt)
    {
        // 不变的参数
        var tm = new MidiTempoMap(bpm: opt.Tempo);
        float fNPitchBendTicks = Constants.N_PITCH_BEND_TICKS;
        float pitchBendTicksScalar = 4096f / Constants.CONTOURS_BINS_PER_SEMITONE;
        float bendLimit = Constants.N_PITCH_BEND_TICKS * 2 - 1;

        // 要变的参数
        var events = new MidiEventCollection(1, tm.TPQ);
        var noteList = opt.MultiplePitchBends ? notes : DropOverlappingPitchBends();
        // pitch 到 event 列表的映射
        var tracks = new Dictionary<int, List<MidiEvent>>();
        int channelCounter = -1;
        int channel;
        List<int> orderedTrackIdxs = new List<int>();

        foreach (var note in noteList)
        {
            var trackIdx = opt.MultiplePitchBends ? note.Pitch : 0;
            List<MidiEvent> track;

            // 确定音轨
            if (tracks.ContainsKey(trackIdx))
            {
                track = tracks[trackIdx];
                channel = track.Last().Channel;
            }
            else
            {
                track = new List<MidiEvent>();
                tracks[trackIdx] = track;
                channelCounter += 1;
                channel = channelCounter % 16 + 1;
                orderedTrackIdxs.Add(trackIdx);
                // 设置上乐器
                track.Add(new PatchChangeEvent(0L, channel, opt.Patch));
            }

            // 添加 note on
            int velocity = (int)Math.Round(127 * note.Amplitude);
            var noteonTicks = tm.SecsToTicks(note.StartTime);
            var noteon = new NoteOnEvent(noteonTicks, channel, note.Pitch, velocity, (int)(tm.SecsToTicks(note.EndTime) - noteonTicks));
            track.Add(noteon);

            // pitch bend
            if (note.PitchBend != null)
            {
                var pitchBendTimes = MathTool.ARange((note.StartTime, note.EndTime), note.PitchBend.Length);
                var fticks = new float[note.PitchBend.Length];
                TensorPrimitives.Multiply(note.PitchBend, pitchBendTicksScalar, fticks);
                // 这里不同于 pretty midi [-8192, 8191] 的 bend pitch 范围
                // NAudio.Midi 可用的是GM 中的设定的 [0, 0x3fff] 所以，进行了一个转换
                TensorPrimitives.Round(new ReadOnlySpan<float>(fticks), fticks);
                TensorPrimitives.Add(new ReadOnlySpan<float>(fticks), fNPitchBendTicks, fticks);
                // 限制一下范围，保证数据有效
                TensorPrimitives.Max(new ReadOnlySpan<float>(fticks), 0f, fticks);
                TensorPrimitives.Min(new ReadOnlySpan<float>(fticks), bendLimit, fticks);

                for (int i = 0; i < fticks.Length; i++)
                {
                    track.Add(new PitchWheelChangeEvent(tm.SecsToTicks(pitchBendTimes[i]), channel, (int)fticks[i]));
                }
            }

            // 添加 note off，没有使用单独的 0x80，使用了 0x90 配合力度 0
            var noteoff = new NoteOnEvent(noteon.OffEvent.AbsoluteTime, channel, note.Pitch, 0, 0);
            track.Add(noteoff);
        }

        // 添加 meta track
        events.AddEvent(new TempoEvent(60000000 / opt.Tempo, 0L), 0);
        events.AddEvent(new TimeSignatureEvent(0L, 4, 2, 24, 8), 0);

        // 添加其它 event
        for (int i = 0; i < orderedTrackIdxs.Count; ++i)
        {
            var pitch = orderedTrackIdxs[i];
            var track = tracks[pitch];
            var idx = i + 1;

            track.Sort(CmpMidiEvent);
            foreach (var e in track)
            {
                events.AddEvent(e, idx);
            }
        }

        events.PrepareForExport();
        return events;
    }

    private List<Note> DropOverlappingPitchBends()
    {
        if (notes.Count == 0)
        {
            return notes;
        }
        var ret = notes.Order().ToList();
        for (int i = 0; i < (ret.Count - 1); ++i)
        {
            var inote = ret[i];
            for (int j = (i + 1); j < ret.Count; ++j)
            {
                var jnote = ret[j];
                if (jnote.StartTime >= inote.EndTime)
                {
                    break;
                }
                inote.PitchBend = null;
                jnote.PitchBend = null;
            }
        }
        return ret;
    }

    private static int GetMidiEventScore(MidiEvent e)
    {
        switch (e.CommandCode)
        {
            case MidiCommandCode.NoteOn:
            case MidiCommandCode.NoteOff:
                var n = (NoteEvent)e;
                return ((n.Channel - 1) + (int)n.NoteNumber) * 1000 + n.Velocity;
            case MidiCommandCode.PitchWheelChange:
                return ((PitchWheelChangeEvent)e).Pitch;
            default:
                return 0;
        }
    }

    private static int CmpMidiEvent(MidiEvent l, MidiEvent r)
    {
        if (l.AbsoluteTime < r.AbsoluteTime)
        {
            return -1;
        }
        if (l.AbsoluteTime > r.AbsoluteTime)
        {
            return 1;
        }
        return Math.Sign(GetMidiEventScore(l) - GetMidiEventScore(r));
    }
}

// 一个简化的 tempo map，不考虑 bpm 和 beatsUnit 的变化
// 只适用于 basic-pitch 转换 midi 的场景
struct MidiTempoMap
{
    public readonly int BPM;
    public readonly int TPQ;
    public readonly int BeatUnit;

    public MidiTempoMap(int bpm = 120, int tpq = 480, int beatUnit = 4)
    {
        BPM = bpm;
        TPQ = tpq;
        BeatUnit = beatUnit;
    }

    public long SecsToTicks(float secs)
    {
        if (secs <= 0)
        {
            return 0;
        }

        var r = (float)(secs * TPQ * BPM) / (float)(15 * BeatUnit);
        return (long)Math.Round(r);
    }
}

