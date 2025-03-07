namespace BasicPitch;

public sealed class Note : IComparable<Note>
{
    public readonly float StartTime;
    public readonly float EndTime;
    public readonly int Pitch;
    public readonly float Amplitude;
    public readonly float[]? PitchBend;

    public Note(float startTime, float endTime, int pitch, float amplitude, float[]? pitchBend)
    {
        StartTime = startTime;
        EndTime = endTime;
        Pitch = pitch;
        Amplitude = amplitude;
        PitchBend = pitchBend;
    }

    public override string ToString()
    {
        var nbend = PitchBend != null ? PitchBend!.Length : 0;
        return $"start: {StartTime}, end: {EndTime}, pitch: {Pitch}, amplitude: {Amplitude}, bend: ${nbend}[{string.Join(",", PitchBend ?? [])}]";
    }

    public int CompareTo(Note? other)
    {
        if (other == null) return 1;

        float fcmp = StartTime - other.StartTime;
        if (fcmp != 0f) return Math.Sign(fcmp);

        fcmp = EndTime - other.EndTime;
        if (fcmp != 0f) return Math.Sign(fcmp);

        var icmp = Pitch - other.Pitch;
        if (icmp != 0) return Math.Sign(icmp);

        fcmp = Amplitude - other.Amplitude;
        if (fcmp != 0f) return Math.Sign(fcmp);

        var l = PitchBend == null ? -1 : PitchBend.Length;
        var r = other.PitchBend == null ? -1 : other.PitchBend.Length;

        return Math.Sign(l - r);
    }
}
