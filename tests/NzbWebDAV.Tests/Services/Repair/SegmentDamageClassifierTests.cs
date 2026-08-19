using NzbWebDAV.Services.Repair;

namespace NzbWebDAV.Tests.Services.Repair;

public class SegmentDamageClassifierTests
{
    private static readonly SegmentDamageCaps Caps = new(2, 5, 1);

    [Fact]
    public void Classify_BoundedTailHole_IsDegraded()
    {
        var verdict = SegmentDamageClassifier.Classify(
            [3], 4, [100L, 100, 100, 1], MediaContainerClass.ResyncTolerant, Caps, out var reason);

        Assert.Equal(SegmentDamageVerdict.Degraded, verdict);
        Assert.Contains("largest run 1", reason);
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
}
