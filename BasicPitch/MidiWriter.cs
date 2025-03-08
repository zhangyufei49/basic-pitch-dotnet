namespace BasicPitch;

public record struct MidiWriteOptions
{
    public int Tempo = 120; // 拍速
    public int Program = 4; // 音色
    public bool MultiplePitchBends = false; // 每个 pitch 单独一个音轨

    public MidiWriteOptions() { }
}

public class MidiWriter
{
    private IList<Note> notes;

    public MidiWriter(IList<Note> notes)
    {
        this.notes = notes;
    }

    public void Write(MidiWriteOptions opt)
    { }
}
