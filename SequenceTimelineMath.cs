namespace SequenceTIFFPlugin;

internal static class SequenceTimelineMath
{
    public static int GetItemLength(int fileCount, int timelineFrameRate, int sequenceFrameRate)
    {
        if (fileCount <= 0)
            return 1;

        var timelineFps = Math.Max(1, timelineFrameRate);
        var sequenceFps = Math.Clamp(sequenceFrameRate, 1, 240);
        var numerator = (long)fileCount * timelineFps;
        return (int)Math.Clamp((numerator + sequenceFps - 1) / sequenceFps, 1L, int.MaxValue);
    }

    public static TimeSpan GetContentOffset(int playbackStart, int fileCount, int sequenceFrameRate)
    {
        if (fileCount <= 0)
            return TimeSpan.Zero;

        var sequenceFps = Math.Clamp(sequenceFrameRate, 1, 240);
        var sourceFrame = Math.Max(0L, (long)playbackStart - 1L);
        return TimeSpan.FromSeconds((sourceFrame - fileCount) / (double)sequenceFps);
    }

    public static int GetPlaybackStart(TimeSpan contentOffset, int fileCount, int sequenceFrameRate)
    {
        if (fileCount <= 0)
            return 1;

        var sequenceFps = Math.Clamp(sequenceFrameRate, 1, 240);
        var sourcePosition = (contentOffset.TotalSeconds + fileCount / (double)sequenceFps) * sequenceFps;
        // TimeSpanは100ns単位なので、フレーム境界の直前へ丸められた誤差だけを吸収します。
        var zeroBased = (long)Math.Floor(sourcePosition + 1e-4);
        return (int)Math.Clamp(zeroBased + 1L, 1L, int.MaxValue);
    }

    public static int AdvancePlaybackStart(
        int playbackStart,
        int elapsedTimelineFrames,
        int timelineFrameRate,
        int sequenceFrameRate)
    {
        var timelineFps = Math.Max(1, timelineFrameRate);
        var sequenceFps = Math.Clamp(sequenceFrameRate, 1, 240);
        var advanced = (long)Math.Max(0, elapsedTimelineFrames) * sequenceFps / timelineFps;
        return (int)Math.Clamp((long)Math.Max(1, playbackStart) + advanced, 1L, int.MaxValue);
    }
}
