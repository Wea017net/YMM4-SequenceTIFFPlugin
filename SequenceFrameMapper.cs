namespace SequenceTIFFPlugin;

internal static class SequenceFrameMapper
{
    public static int GetSequenceIndex(
        int itemFrame,
        int timelineFrameRate,
        int sequenceFrameRate,
        int playbackStart,
        int fileCount)
    {
        if (fileCount <= 0)
            return -1;

        var safeItemFrame = Math.Max(0, itemFrame);
        var safeTimelineFrameRate = Math.Max(1, timelineFrameRate);
        var safeSequenceFrameRate = Math.Clamp(sequenceFrameRate, 1, 240);
        var startIndex = Math.Max(0L, (long)playbackStart - 1L);
        var frame = startIndex + (long)safeItemFrame * safeSequenceFrameRate / safeTimelineFrameRate;

        return (int)Math.Clamp(frame, 0L, fileCount - 1L);
    }
}
