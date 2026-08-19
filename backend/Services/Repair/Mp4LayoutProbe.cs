using System.Buffers.Binary;
using System.Text;

namespace NzbWebDAV.Services.Repair;

public static class Mp4LayoutProbe
{
    public static MediaContainerClass ClassifyMp4Head(ReadOnlySpan<byte> head)
    {
        var offset = 0;
        var sawMoov = false;

        while (offset < head.Length)
        {
            if (head.Length - offset < 8) return MediaContainerClass.Unknown;

            var size = BinaryPrimitives.ReadUInt32BigEndian(head[offset..]);
            var type = Encoding.ASCII.GetString(head.Slice(offset + 4, 4));
            var headerSize = 8;
            ulong boxSize = size;

            if (size == 1)
            {
                if (head.Length - offset < 16) return MediaContainerClass.Unknown;
                boxSize = BinaryPrimitives.ReadUInt64BigEndian(head[(offset + 8)..]);
                headerSize = 16;
                if (boxSize < (ulong)headerSize) return MediaContainerClass.Unknown;
            }
            else if (size == 0)
            {
                return type == "mdat" && !sawMoov
                    ? MediaContainerClass.Mp4MoovAtEnd
                    : MediaContainerClass.Unknown;
            }
            else if (boxSize < (ulong)headerSize)
            {
                return MediaContainerClass.Unknown;
            }

            // Decision points trigger on sight, regardless of the declared payload size
            // (mdat's size is the media payload — typically far beyond the probed span).
            if (type == "mdat")
                return sawMoov ? MediaContainerClass.Mp4FastStart : MediaContainerClass.Mp4MoovAtEnd;
            if (type == "moof") return MediaContainerClass.ResyncTolerant;

            if (boxSize > (ulong)(head.Length - offset))
                // A moov we cannot see past still classifies: fMP4 init moovs are small, so
                // a moov spanning past the span means faststart. A skipped box (ftyp, free,
                // uuid, …) beyond the span leaves the layout unclassifiable.
                return type == "moov" ? MediaContainerClass.Mp4FastStart : MediaContainerClass.Unknown;

            if (type == "moov") sawMoov = true;

            offset += checked((int)boxSize);
        }

        return sawMoov ? MediaContainerClass.Mp4FastStart : MediaContainerClass.Unknown;
    }
}
