namespace SequenceTIFFPlugin;

internal static class SequenceFrameMapper
{
    public static int GetSequenceIndex(
        int itemFrame,
        int timelineFrameRate,
        int sequenceFrameRate,
        int playbackStart,
        int fileCount,
        bool isLooped = false,
        int loopOriginFrame = 1,
        int loopEndFrame = 0)
    {
        if (fileCount <= 0)
            return -1;

        var safeItemFrame = Math.Max(0, itemFrame);
        var safeTimelineFrameRate = Math.Max(1, timelineFrameRate);
        var safeSequenceFrameRate = Math.Clamp(sequenceFrameRate, 1, 240);
        var startIndex = Math.Max(0L, (long)playbackStart - 1L);
        var frame = startIndex + (long)safeItemFrame * safeSequenceFrameRate / safeTimelineFrameRate;

        if (!isLooped)
            return (int)Math.Clamp(frame, 0L, fileCount - 1L);

        return NormalizeLoopFrame(
            (int)Math.Clamp(frame + 1L, 1L, int.MaxValue),
            fileCount,
            loopOriginFrame,
            loopEndFrame) - 1;
    }

    public static int NormalizeLoopFrame(
        int frame,
        int fileCount,
        int loopOriginFrame,
        int loopEndFrame)
    {
        if (fileCount <= 0)
            return 1;

        var start = Math.Clamp(loopOriginFrame, 1, fileCount);
        var end = GetLoopEndFrame(fileCount, start, loopEndFrame);
        var length = (long)end - start + 1L;
        var offset = Math.Max(0L, (long)frame - start);
        return (int)(start + offset % length);
    }

    public static int GetLoopEndFrame(int fileCount, int loopOriginFrame, int loopEndFrame)
    {
        if (fileCount <= 0)
            return 1;

        var start = Math.Clamp(loopOriginFrame, 1, fileCount);
        var end = loopEndFrame == 0 ? fileCount : Math.Clamp(loopEndFrame, 1, fileCount);
        return Math.Max(start, end);
    }
}
