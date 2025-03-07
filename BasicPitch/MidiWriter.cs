namespace BasicPitch;

public record struct MidiWriteOptions(
    int Tempo = 120, // 拍速
    int Program = 4, // 音色
    bool MultiplePitchBends = false // 每个 pitch 单独一个音轨
    );

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
