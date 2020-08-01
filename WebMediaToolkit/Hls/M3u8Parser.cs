using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WebMediaToolkit.Hls
{
    // Spec is at https://tools.ietf.org/html/draft-pantos-http-live-streaming-23
    public class M3u8Parser
    {
        private readonly bool _isStrictMode;
        private readonly Uri _baseUri;
        private readonly TextReader _textReader;

        private bool? _isMasterPlaylist;
        private HlsMediaPlaylist _mediaPlaylist;
        private HlsMasterPlaylist _masterPlaylist;
        private HlsMediaSegment _mediaSegment;
        private HlsStream _stream;
        private HlsMediaSegmentKey _mediaSegmentKey;

        private int _version;
        private bool _isIndependentSegments;
        private string _start;

        public bool HasEndlist { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textReader"></param>
        /// <param name="baseUri"></param>
        /// <param name="isStrictMode">Strict mode throws an exception if the parser encounters an unrecognized tag.</param>
        private M3u8Parser(TextReader textReader, Uri baseUri, bool isStrictMode)
        {
            _textReader = textReader;
            _baseUri = baseUri;
            _isStrictMode = isStrictMode;
        }

        private async Task ParseAsync()
        {
            string curLine = await ReadNextDataLineAsync();
            if (curLine == null || curLine != "#EXTM3U")
                throw new HlsException("Stream declares unexpected format.");

            _version = 1;
            _isIndependentSegments = false;

            while ((curLine = await ReadNextDataLineAsync()) != null)
            {
                // Skip any comments.
                if (curLine[0] == '#' && !curLine.StartsWith("#EXT"))
                    continue;

                // Handle non-tag lines directly.
                if (curLine[0] != '#')
                {
                    if (_isMasterPlaylist == false)
                        HandleMediaPlaylistLine(curLine);
                    else if (_isMasterPlaylist == true)
                        HandleMasterPlaylistLine(curLine);
                    else
                        throw new HlsException("Unexpected non-tag line encountered.");
                    continue;
                }

                string[] parts = curLine.Split(':');
                string tag = parts[0].Substring(1);
                string value = (parts.Length > 1 ? parts[1] : null);

                switch (tag)
                {
                    // Basic tags.
                    case "EXT-X-VERSION":
                        _version = Convert.ToInt32(value);
                        break;

                    // Both master and media playlist tags.
                    case "EXT-X-INDEPENDENT-SEGMENTS":
                        _isIndependentSegments = true;
                        break;
                    case "EXT-X-START":
                        _start = value;
                        break;

                    // Media segment tags.
                    case "EXTINF":
                    case "EXT-X-BYTERANGE":
                    case "EXT-X-DISCONTINUITY":
                    case "EXT-X-KEY":
                    case "EXT-X-MAP":
                    case "EXT-X-PROGRAM-DATE-TIME":
                    case "EXT-X-ALLOW-CACHE": // Deprecated in version 7 and removed in version 14. Note that VLS used to output a wrong EXT-X-ALLOWCACHE, some files still use it.
                    case "EXT-X-DATERANGE":

                    // Media playlist tags.
                    case "EXT-X-TARGETDURATION":
                    case "EXT-X-MEDIA-SEQUENCE":
                    case "EXT-X-DISCONTINUITY-SEQUENCE":
                    case "EXT-X-ENDLIST":
                    case "EXT-X-PLAYLIST-TYPE":
                    case "EXT-X-I-FRAMES-ONLY":
                        HandleMediaPlaylistLine(curLine);
                        break;

                    // Master playlist tags.
                    case "EXT-X-MEDIA":
                    case "EXT-X-STREAM-INF":
                    case "EXT-X-I-FRAME-STREAM-INF":
                    case "EXT-X-SESSION-DATA":
                    case "EXT-X-SESSION-KEY":
                        HandleMasterPlaylistLine(curLine);
                        break;

                    default:
                        if (_isStrictMode)
                            throw new HlsException($"Unrecognized tag encountered ({tag}).");
                        else
                            break;
                }
            }
        }

        private void HandleMediaPlaylistLine(string line)
        {
            if (_isMasterPlaylist.HasValue && _isMasterPlaylist.Value)
                throw new HlsException("Unexpected tag in media playlist.");

            if (!_isMasterPlaylist.HasValue)
            {
                _isMasterPlaylist = false;
                _mediaPlaylist = new HlsMediaPlaylist { Version = _version, IsIndependentSegments = _isIndependentSegments, Start = _start };
            }

            if (line[0] != '#')
            {
                // A URI line for a media segment should be the only case when this happens.
                _mediaSegment.Uri = new Uri(_baseUri, line);
                _mediaSegment = null;
                return;
            }

            int firstColonIndex = line.IndexOf(':');
            string tag = (firstColonIndex > -1 ? line.Substring(1, firstColonIndex - 1) : line.Substring(1));
            string value = (firstColonIndex > -1 ? line.Substring(firstColonIndex + 1, line.Length - firstColonIndex - 1) : null);

            switch (tag)
            {
                // Media segment tags.
                case "EXTINF":
                    int commaIndex = value.IndexOf(',');
                    _mediaSegment = new HlsMediaSegment
                    {
                        SequenceNumber = _mediaPlaylist.MediaSequence + _mediaPlaylist._mediaSegments.Count,
                        Duration = Convert.ToSingle((commaIndex != -1 ? value.Substring(0, commaIndex) : value)),
                        Title = (commaIndex != -1 ? value.Substring(commaIndex + 1) : null),
                        Key = _mediaSegmentKey
                    };
                    _mediaPlaylist._mediaSegments.Add(_mediaSegment);
                    break;

                case "EXT-X-BYTERANGE":
                    _mediaSegment.ByteRange = value;
                    break;

                case "EXT-X-DISCONTINUITY":
                    _mediaSegment.HasDiscontinuityAfter = true;
                    break;

                case "EXT-X-KEY":
                    IReadOnlyDictionary<string, string> keyAttributes = AttributeList.Parse(value);
                    _mediaSegmentKey = new HlsMediaSegmentKey
                    {
                        Method = AttributeValueUtils.ParseEnum<HlsKeyMethod>(keyAttributes["METHOD"]),
                        Uri = AttributeValueUtils.GetUriOrNull(keyAttributes, _baseUri, "URI"),
                        KeyFormat = (AttributeValueUtils.GetStringOrNull(keyAttributes, "KEYFORMAT") ?? "identity"),
                    };
                    break;

                case "EXT-X-MAP":
                    break;

                case "EXT-X-PROGRAM-DATE-TIME":
                    _mediaSegment.ProgramDateTime = value;
                    break;

                case "EXT-X-ALLOW-CACHE":
                    break;

                case "EXT-X-DATERANGE":
                    break;

                // Media playlist tags.
                case "EXT-X-TARGETDURATION":
                    _mediaPlaylist.TargetDuration = Convert.ToInt32(value);
                    break;

                case "EXT-X-MEDIA-SEQUENCE":
                    _mediaPlaylist.MediaSequence = Convert.ToInt32(value);
                    break;

                case "EXT-X-DISCONTINUITY-SEQUENCE":
                    _mediaPlaylist.DiscontinuitySequence = Convert.ToInt32(value);
                    break;

                case "EXT-X-ENDLIST":
                    _mediaSegment = null;
                    HasEndlist = true;
                    break;

                case "EXT-X-PLAYLIST-TYPE":
                    _mediaPlaylist.PlaylistType = value;
                    break;

                case "EXT-X-I-FRAMES-ONLY":
                    _mediaPlaylist.IsIFramesOnly = true;
                    break;
            }
        }

        private void HandleMasterPlaylistLine(string line)
        {
            if (_isMasterPlaylist.HasValue && !_isMasterPlaylist.Value)
                throw new HlsException("Unexpected tag in master playlist.");

            if (!_isMasterPlaylist.HasValue)
            {
                _isMasterPlaylist = true;
                _masterPlaylist = new HlsMasterPlaylist { Version = _version, IsIndependentSegments = _isIndependentSegments, Start = _start };
            }

            if (line[0] != '#')
            {
                // A URI line for a stream should be the only case when this happens.
                _stream.Uri = new Uri(_baseUri, line);
                _stream = null;
                return;
            }

            string[] parts = line.Split(':');
            string tag = parts[0].Substring(1);
            string value = (parts.Length > 1 ? parts[1] : null);

            switch (tag)
            {
                case "EXT-X-MEDIA":
                    IReadOnlyDictionary<string, string> mediaAttributes = AttributeList.Parse(value);
                    HlsMedia media = new HlsMedia
                    {
                        Type = AttributeValueUtils.ParseEnum<HlsMediaType>(mediaAttributes["TYPE"]),
                        Uri = AttributeValueUtils.GetUriOrNull(mediaAttributes, _baseUri, "URI"),
                        GroupId = AttributeValueUtils.ParseQuotedString(mediaAttributes["GROUP-ID"]),
                        Language = AttributeValueUtils.GetStringOrNull(mediaAttributes, "LANGUAGE"),
                        AssocLanguage = AttributeValueUtils.GetStringOrNull(mediaAttributes, "ASSOC-LANGUAGE"),
                        Name = AttributeValueUtils.ParseQuotedString(mediaAttributes["NAME"]),
                        Default = (AttributeValueUtils.GetStringOrNull(mediaAttributes, "DEFAULT") == "YES"),
                        Autoselect = (AttributeValueUtils.GetStringOrNull(mediaAttributes, "AUTOSELECT") == "YES"),
                        Forced = (AttributeValueUtils.GetStringOrNull(mediaAttributes, "FORCED") == "YES"),
                        InstreamId = AttributeValueUtils.GetStringOrNull(mediaAttributes, "INSTREAM-ID"),
                        Characteristics = AttributeValueUtils.GetStringOrNull(mediaAttributes, "CHARACTERISTICS"),
                        Channels = AttributeValueUtils.GetStringOrNull(mediaAttributes, "CHANNELS")
                    };
                    _masterPlaylist._mediae.Add(media);
                    break;

                case "EXT-X-STREAM-INF":
                    IReadOnlyDictionary<string, string> streamAttributes = AttributeList.Parse(value);
                    _stream = new HlsStream
                    {
                        Bandwidth = (int) AttributeValueUtils.ParseDecimalInteger(streamAttributes["BANDWIDTH"]),
                        AverageBandwidth = (int?) AttributeValueUtils.GetDecimalIntegerOrNull(streamAttributes, "AVERAGE-BANDWIDTH"),
                        Codecs = AttributeValueUtils.GetStringOrNull(streamAttributes, "CODECS"),
                        Resolution = AttributeValueUtils.GetResolutionOrNull(streamAttributes, "RESOLUTION"),
                        FrameRate = AttributeValueUtils.GetFloatOrNull(streamAttributes, "FRAME-RATE"),
                        HdcpLevel = AttributeValueUtils.GetRawOrNull(streamAttributes, "HDCP-LEVEL"),
                        Audio = AttributeValueUtils.GetStringOrNull(streamAttributes, "AUDIO"),
                        Video = AttributeValueUtils.GetStringOrNull(streamAttributes, "VIDEO"),
                        Subtitles = AttributeValueUtils.GetStringOrNull(streamAttributes, "SUBTITLES"),
                        //TODO ClosedCaptions =
                    };
                    _masterPlaylist._streams.Add(_stream);
                    break;

                case "EXT-X-I-FRAME-STREAM-INF":
                    IReadOnlyDictionary<string, string> iFrameStreamAttributes = AttributeList.Parse(value);
                    HlsIFrameStream iFrameStream = new HlsIFrameStream
                    {
                        Bandwidth = (int) AttributeValueUtils.ParseDecimalInteger(iFrameStreamAttributes["BANDWIDTH"]),
                        AverageBandwidth = (int?) AttributeValueUtils.GetDecimalIntegerOrNull(iFrameStreamAttributes, "AVERAGE-BANDWIDTH"),
                        Codecs = AttributeValueUtils.GetStringOrNull(iFrameStreamAttributes, "CODECS"),
                        Resolution = AttributeValueUtils.GetResolutionOrNull(iFrameStreamAttributes, "RESOLUTION"),
                        HdcpLevel = AttributeValueUtils.GetRawOrNull(iFrameStreamAttributes, "HDCP-LEVEL"),
                        Video = AttributeValueUtils.GetStringOrNull(iFrameStreamAttributes, "VIDEO"),
                        Uri = AttributeValueUtils.GetUri(iFrameStreamAttributes, _baseUri, "URI")
                    };
                    _masterPlaylist._iFrameStreams.Add(iFrameStream);
                    break;

                case "EXT-X-SESSION-DATA":
                    IReadOnlyDictionary<string, string> sessionDataAttributes = AttributeList.Parse(value);
                    HlsSessionData sessionData = new HlsSessionData
                    {
                        DataId = sessionDataAttributes["DATA-ID"],
                        Value = (sessionDataAttributes.ContainsKey("VALUE") ? sessionDataAttributes["VALUE"] : null),
                        Uri = (sessionDataAttributes.ContainsKey("URI") ? new Uri(_baseUri, sessionDataAttributes["URI"]) : null),
                        Language = (sessionDataAttributes.ContainsKey("LANGUAGE") ? sessionDataAttributes["LANGUAGE"] : null),
                    };
                    _masterPlaylist._sessionDatas.Add(sessionData);
                    break;

                case "EXT-X-SESSION-KEY":
                    break;
            }
        }

        public static async Task<HlsPlaylist> ParseAsync(TextReader streamReader, Uri baseUri, bool isStrictMode = true)
        {
            M3u8Parser parser = new M3u8Parser(streamReader, baseUri, isStrictMode);
            await parser.ParseAsync();

            if (!parser._isMasterPlaylist.HasValue)
                throw new HlsException("Unable to identify playlist type.");

            return (parser._isMasterPlaylist.Value ? (HlsPlaylist) parser._masterPlaylist : parser._mediaPlaylist);
        }

        private async Task<string> ReadNextDataLineAsync()
        {
            string curLine;
            do
                curLine = await _textReader.ReadLineAsync();
            while (curLine == String.Empty);

            return curLine;
        }
    }

    public abstract class HlsPlaylist
    {
        public int Version { get; internal set; }
        public bool IsIndependentSegments { get; internal set; }
        public string Start { get; internal set; }
    }

    public enum HlsBoolean
    {
        Yes,
        No
    }
}
