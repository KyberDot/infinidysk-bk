using System.Diagnostics.CodeAnalysis;

namespace NzbWebDAV.Services.Repair;

public enum SegmentDamageVerdict
{
    Clean,
    Degraded,
    Failed,
}

[SuppressMessage("Design", "CA1028:Enum Storage should be Int32", Justification = "Persisted in a compact metadata blob.")]
public enum MediaContainerClass : byte
{
    Unknown = 0,
    ResyncTolerant = 1,
    Mp4FastStart = 2,
    Mp4MoovAtEnd = 3,
}

public sealed record SegmentDamageCaps(
    int MaxConsecutiveMissing,
    int MaxTotalMissing,
    double MaxMissingBytePercent);

public static class SegmentDamageClassifier
{
    public static SegmentDamageVerdict Classify(
        IReadOnlyList<int> missingIndices,
        int totalSegments,
        IReadOnlyList<long> exactSegmentSizes,
        MediaContainerClass containerClass,
        SegmentDamageCaps caps,
        out string reason)
    {
        var missingBytes = missingIndices.Sum(index => exactSegmentSizes[index]);
        var totalBytes = exactSegmentSizes.Sum();
        var bytePercent = totalBytes == 0 ? 100d : missingBytes * 100d / totalBytes;
        var longestRun = GetLongestRun(missingIndices);
        reason = $"{missingIndices.Count} missing segment(s) (largest run {longestRun}, {bytePercent:0.##}% of file)";

        if (missingIndices.Count == 0) return SegmentDamageVerdict.Clean;
        if (containerClass is MediaContainerClass.Unknown or MediaContainerClass.Mp4MoovAtEnd)
            return SegmentDamageVerdict.Failed;
        if (missingIndices[0] == 0) return SegmentDamageVerdict.Failed;
        if (longestRun > caps.MaxConsecutiveMissing) return SegmentDamageVerdict.Failed;
        if (missingIndices.Count > caps.MaxTotalMissing) return SegmentDamageVerdict.Failed;
        if (bytePercent > caps.MaxMissingBytePercent) return SegmentDamageVerdict.Failed;

        return SegmentDamageVerdict.Degraded;
    }

    private static int GetLongestRun(IReadOnlyList<int> missingIndices)
    {
        var longestRun = 0;
        var currentRun = 0;
        var previous = int.MinValue;
        foreach (var index in missingIndices)
        {
            currentRun = index == previous + 1 ? currentRun + 1 : 1;
            longestRun = Math.Max(longestRun, currentRun);
            previous = index;
        }

        return longestRun;
    }
}

public static class MediaContainerClassMapping
{
    /// <summary>
    /// The container class fixed by the file extension, or null for the MP4 family
    /// (.mp4/.m4v/.mov), whose layout (faststart / moov-at-end / fragmented) must be
    /// probed from the file head. Only meaningful for
    /// <see cref="Utils.FilenameUtil.IsDegradedToleranceEligible"/> files.
    /// </summary>
    public static MediaContainerClass? ByExtension(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".mkv" or ".mk3d" or ".webm" or ".ts" or ".m2ts" => MediaContainerClass.ResyncTolerant,
            _ => null,
        };

    /// <summary>Human-readable container phrase for health-result messages.</summary>
    public static string Describe(MediaContainerClass containerClass) => containerClass switch
    {
        MediaContainerClass.ResyncTolerant => "a resync-tolerant container",
        MediaContainerClass.Mp4FastStart => "a fast-start MP4 container",
        MediaContainerClass.Mp4MoovAtEnd => "an MP4 container with the moov atom at the end",
        _ => "an unrecognized container",
    };
}
