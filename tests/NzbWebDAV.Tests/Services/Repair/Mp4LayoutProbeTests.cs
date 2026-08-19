using System.Buffers.Binary;
using System.Text;
using NzbWebDAV.Services.Repair;

namespace NzbWebDAV.Tests.Services.Repair;

public class Mp4LayoutProbeTests
{
    [Fact]
    public void FastStart_MoovBeforeMdat()
    {
        var head = Concat(Box("ftyp", 16), Box("moov", 24), BoxHeader("mdat", 4_000_000_000u));
        Assert.Equal(MediaContainerClass.Mp4FastStart, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void MoovAtEnd_MdatBeforeMoov()
    {
        var head = Concat(Box("ftyp", 16), Box("mdat", 100), Box("moov", 24));
        Assert.Equal(MediaContainerClass.Mp4MoovAtEnd, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void MoovAtEnd_MdatWithHugeDeclaredSize()
    {
        // The common case: mdat's declared size is the whole media payload, far
        // beyond the probed span. The decision triggers on sight, not on bounds.
        var head = Concat(Box("ftyp", 16), BoxHeader("mdat", 4_000_000_000u));
        Assert.Equal(MediaContainerClass.Mp4MoovAtEnd, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void Fragmented_MoofAfterMoov_IsResyncTolerant()
    {
        var head = Concat(Box("ftyp", 16), Box("moov", 24), Box("moof", 32));
        Assert.Equal(MediaContainerClass.ResyncTolerant, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void Fragmented_MoofBeforeMoov_IsResyncTolerant()
    {
        var head = Concat(Box("ftyp", 16), Box("moof", 32));
        Assert.Equal(MediaContainerClass.ResyncTolerant, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void FreeSkipWideUuidPrefixes_AreSkipped()
    {
        var head = Concat(
            Box("free", 8), Box("skip", 8), Box("wide", 0), Box("uuid", 16),
            BoxHeader("mdat", 4_000_000_000u));
        Assert.Equal(MediaContainerClass.Mp4MoovAtEnd, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void SixtyFourBitLargesize_IsParsed()
    {
        var head = Concat(Box("ftyp", 16), LargeBoxHeader("mdat", 5_000_000_000UL));
        Assert.Equal(MediaContainerClass.Mp4MoovAtEnd, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void SixtyFourBitLargesize_SkippedBox_FastStart()
    {
        var head = Concat(LargeBox("free", 24), Box("moov", 24), BoxHeader("mdat", 4_000_000_000u));
        Assert.Equal(MediaContainerClass.Mp4FastStart, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void ZeroSize_MdatWithoutMoov_ExtendsToEndOfFile()
    {
        var head = Concat(Box("ftyp", 16), ZeroSizeBox("mdat"));
        Assert.Equal(MediaContainerClass.Mp4MoovAtEnd, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void ZeroSize_NonMdat_IsUnknown()
    {
        var head = Concat(Box("ftyp", 16), ZeroSizeBox("free"));
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void SizeBelowHeader_IsUnknown()
    {
        var head = BoxHeader("ftyp", 4);
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void LargesizeBelowHeader_IsUnknown()
    {
        var head = LargeBoxHeader("mdat", 8);
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void TruncatedHeader_IsUnknown()
    {
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head([0, 0, 0, 16]));
        var head = Concat(Box("ftyp", 16), new byte[] { 0, 0, 0, 8 });
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void TruncatedLargesizeHeader_IsUnknown()
    {
        // size == 1 but only 12 of the 16 largesize header bytes are available.
        var head = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(head, 1);
        Encoding.ASCII.GetBytes("ftyp").CopyTo(head, 4);
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void SkipPastAvailableData_IsUnknown()
    {
        var head = BoxHeader("ftyp", 1_000_000);
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void Garbage_IsUnknown()
    {
        var head = Enumerable.Repeat((byte)0xFF, 64).ToArray();
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void MoovSpanningPastSpan_IsFastStart()
    {
        // A giant faststart moov we cannot see past still classifies; fMP4 init
        // moovs are small, so the fragmented case is always reached within the span.
        var head = Concat(Box("ftyp", 16), BoxHeader("moov", 1_000_000));
        Assert.Equal(MediaContainerClass.Mp4FastStart, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void MoovThenEndOfData_IsFastStart()
    {
        var head = Concat(Box("ftyp", 16), Box("moov", 24));
        Assert.Equal(MediaContainerClass.Mp4FastStart, Mp4LayoutProbe.ClassifyMp4Head(head));
    }

    [Fact]
    public void EmptyBuffer_IsUnknown()
    {
        Assert.Equal(MediaContainerClass.Unknown, Mp4LayoutProbe.ClassifyMp4Head([]));
    }

    private static byte[] Box(string type, int payloadSize)
    {
        var bytes = new byte[8 + payloadSize];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)(8 + payloadSize));
        Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] BoxHeader(string type, uint declaredSize)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, declaredSize);
        Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] LargeBox(string type, ulong declaredSize)
    {
        var bytes = new byte[16 + ((int)declaredSize - 16)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 1);
        Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), declaredSize);
        return bytes;
    }

    private static byte[] LargeBoxHeader(string type, ulong declaredSize)
    {
        var bytes = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 1);
        Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), declaredSize);
        return bytes;
    }

    private static byte[] ZeroSizeBox(string type)
    {
        var bytes = new byte[8];
        Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] Concat(params byte[][] arrays) =>
        arrays.SelectMany(array => array).ToArray();
}
