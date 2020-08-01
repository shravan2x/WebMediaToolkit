using System;
using System.Collections.Generic;
using System.Drawing;

namespace WebMediaToolkit.Hls
{
    public class HlsMasterPlaylist : HlsPlaylist
    {
        public IReadOnlyList<HlsMedia> HlsMediae => _mediae;
        public IReadOnlyList<HlsStream> Streams => _streams;
        public IReadOnlyList<HlsIFrameStream> IFrameStreams => _iFrameStreams;
        public IReadOnlyList<HlsSessionData> SessionDatas => _sessionDatas;

        internal List<HlsMedia> _mediae = new List<HlsMedia>();
        internal List<HlsStream> _streams = new List<HlsStream>();
        internal List<HlsIFrameStream> _iFrameStreams = new List<HlsIFrameStream>();
        internal List<HlsSessionData> _sessionDatas = new List<HlsSessionData>();
    }

    public class HlsMedia
    {
        public HlsMediaType Type { get; internal set; }
        public Uri Uri { get; internal set; }
        public string GroupId { get; internal set; }
        public string Language { get; internal set; }
        public string AssocLanguage { get; internal set; }
        public string Name { get; internal set; }
        public bool Default { get; internal set; }
        public bool Autoselect { get; internal set; }
        public bool Forced { get; internal set; }
        public string InstreamId { get; internal set; } // We could use a massive enum for this, but it's unnecessary. // TODO: Wrong capitalization?
        public string Characteristics { get; internal set; }
        public string Channels { get; internal set; }
    }

    public class HlsStream
    {
        public int Bandwidth { get; internal set; }
        public int? AverageBandwidth { get; internal set; }
        public string Codecs { get; internal set; }
        public Size? Resolution { get; internal set; }
        public float? FrameRate { get; internal set; }
        public string HdcpLevel { get; internal set; } // We won't use an enum for this since the supported levels will only keep increasing. TYPE-1 already exists.
        public string Audio { get; internal set; }
        public string Video { get; internal set; }
        public string Subtitles { get; internal set; }
        public string ClosedCaptions { get; internal set; }
        public Uri Uri { get; internal set; }
    }

    public class HlsIFrameStream
    {
        public int Bandwidth { get; internal set; }
        public int? AverageBandwidth { get; internal set; }
        public string Codecs { get; internal set; }
        public Size? Resolution { get; internal set; }
        public string HdcpLevel { get; internal set; }
        public string Video { get; internal set; }
        public Uri Uri { get; internal set; }
    }

    public class HlsSessionData
    {
        public string DataId { get; internal set; }
        public string Value { get; internal set; }
        public Uri Uri { get; internal set; }
        public string Language { get; internal set; }
    }

    public enum HlsMediaType
    {
        Audio,
        Video,
        Subtitles,
        ClosedCaptions
    }
}
