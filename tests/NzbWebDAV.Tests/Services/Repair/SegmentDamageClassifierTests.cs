using NzbWebDAV.Services.Repair;

namespace NzbWebDAV.Tests.Services.Repair;

public class SegmentDamageClassifierTests
{
    private static readonly SegmentDamageCaps Caps = new(MaxConsecutiveMissing: 2, MaxTotalMissing: 5, MaxMissingBytePercent: 1);

    [Fact]
    public void Classify_NoMissingSegments_IsClean()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [], 4, [100L, 100, 100, 100], MediaContainerClass.ResyncTolerant, Caps, out var reason);

        Assert.Equal(SegmentDamageVerdict.Clean, verdict);
        Assert.Contains("0 missing segment(s)", reason);
    }

    [Fact]
    public void Classify_BoundedTailHole_IsDegraded()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [3], 4, [100L, 100, 100, 1], MediaContainerClass.ResyncTolerant, Caps, out var reason);

        Assert.Equal(SegmentDamageVerdict.Degraded, verdict);
        Assert.Contains("largest run 1", reason);
    }

    [Fact]
    public void Classify_FastStartMp4_MidFileMiss_IsDegraded()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [2], 4, [100L, 100, 1, 100], MediaContainerClass.Mp4FastStart, Caps, out _);

        Assert.Equal(SegmentDamageVerdict.Degraded, verdict);
    }

    [Theory]
    [InlineData(MediaContainerClass.Unknown)]
    [InlineData(MediaContainerClass.Mp4MoovAtEnd)]
    public void Classify_UnsafeContainer_IsFailed(MediaContainerClass containerClass)
    {
        var verdict = SegmentDamageClassifier.Classify(
            [2], 4, [100L, 100, 100, 100], containerClass, Caps, out _);

        Assert.Equal(SegmentDamageVerdict.Failed, verdict);
    }

    [Fact]
    public void Classify_HoleAtSegmentZero_IsFailed()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [0], 4, [100L, 100, 100, 100], MediaContainerClass.Mp4FastStart, Caps, out _);

        Assert.Equal(SegmentDamageVerdict.Failed, verdict);
    }

    [Fact]
    public void Classify_RunAtCap_IsDegraded()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [5, 6], 20, Sizes(20, (5, 10), (6, 10)), MediaContainerClass.ResyncTolerant, Caps, out _);

        Assert.Equal(SegmentDamageVerdict.Degraded, verdict);
    }

    [Fact]
    public void Classify_RunOverCap_IsFailed()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [5, 6, 7], 20, Sizes(20, (5, 10), (6, 10), (7, 10)), MediaContainerClass.ResyncTolerant, Caps, out var reason);

        Assert.Equal(SegmentDamageVerdict.Failed, verdict);
        Assert.Contains("largest run 3", reason);
    }

    [Fact]
    public void Classify_TotalAtCap_IsDegraded()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [2, 5, 8, 11, 14], 20, Sizes(20, (2, 10), (5, 10), (8, 10), (11, 10), (14, 10)),
            MediaContainerClass.ResyncTolerant, Caps, out _);

        Assert.Equal(SegmentDamageVerdict.Degraded, verdict);
    }

    [Fact]
    public void Classify_TotalOverCap_IsFailed()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [2, 5, 8, 11, 14, 17], 20,
            Sizes(20, (2, 10), (5, 10), (8, 10), (11, 10), (14, 10), (17, 10)),
            MediaContainerClass.ResyncTolerant, Caps, out _);

        Assert.Equal(SegmentDamageVerdict.Failed, verdict);
    }

    [Fact]
    public void Classify_ByteShareAtCap_IsDegraded()
    {
        // Exactly 1.0% of the file's bytes missing: the cap comparison is strictly-greater.
        var verdict = SegmentDamageClassifier.Classify(
            [1], 2, [9900L, 100], MediaContainerClass.ResyncTolerant, Caps, out _);

        Assert.Equal(SegmentDamageVerdict.Degraded, verdict);
    }

    [Fact]
    public void Classify_ByteShareOverCap_IsFailed()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [1], 2, [9899L, 101], MediaContainerClass.ResyncTolerant, Caps, out var reason);

        Assert.Equal(SegmentDamageVerdict.Failed, verdict);
        Assert.Contains("% of file", reason);
    }

    [Fact]
    public void Classify_ReasonReportsCountRunAndByteShare()
    {
        SegmentDamageClassifier.Classify(
            [4, 5], 10, Sizes(10, (4, 100), (5, 100)), MediaContainerClass.ResyncTolerant, Caps,
            out var reason);

        Assert.Contains("2 missing segment(s)", reason);
        Assert.Contains("largest run 2", reason);
        Assert.Contains("of file", reason);
    }

    private static long[] Sizes(int count, params (int Index, long Size)[] holes)
    {
        var sizes = Enumerable.Repeat(100_000L, count).ToArray();
        foreach (var (index, size) in holes)
            sizes[index] = size;
        return sizes;
    }
}
