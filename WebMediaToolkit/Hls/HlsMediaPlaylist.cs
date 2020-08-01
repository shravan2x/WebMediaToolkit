using System;
using System.Collections.Generic;

namespace WebMediaToolkit.Hls
{
    public class HlsMediaPlaylist : HlsPlaylist
    {
        public int? TargetDuration { get; internal set; }
        public int MediaSequence { get; internal set; } = 0;
        public int DiscontinuitySequence { get; internal set; } = 0;
        public string PlaylistType { get; internal set; }
        public bool IsIFramesOnly { get; internal set; } = false;
        public IReadOnlyList<HlsMediaSegment> MediaSegments => _mediaSegments;

        internal List<HlsMediaSegment> _mediaSegments = new List<HlsMediaSegment>();
    }

    public class HlsMediaSegment
    {
        public int SequenceNumber { get; internal set; }
        public float Duration { get; internal set; }
        public string Title { get; internal set; }
        public string ByteRange { get; internal set; }
        public bool HasDiscontinuityAfter { get; internal set; } = false;
        public string ProgramDateTime { get; internal set; }
        public Uri Uri { get; internal set; }
        public HlsMediaSegmentKey Key { get; internal set; }
    }

    public class HlsMediaSegmentKey
    {
        public HlsKeyMethod Method { get; internal set; }
        public Uri Uri { get; internal set; }
        public string KeyFormat { get; internal set; }
    }

    public enum HlsKeyMethod
    {
        None,
        Aes128,
        SampleAes
    }
}
