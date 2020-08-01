using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace WebMediaToolkit.Hls
{
    public class HlsDownloader
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly HlsMediaPlaylist _mediaPlaylist;
        private readonly Dictionary<Uri, byte[]> _keyCache;

        private int _nextSegment;

        public Func<HlsMediaSegmentKey, Task<byte[]>> OnKeyFetch { get; set; }
        public Func<HlsMediaSegment, Task<byte[]>> OnSegmentFetch { get; set; }

        public HlsDownloader(HlsMediaPlaylist mediaPlaylist)
        {
            _mediaPlaylist = mediaPlaylist;

            _keyCache = new Dictionary<Uri, byte[]>();
            _nextSegment = 0;
        }

        public async Task<ReadOnlyMemory<byte>> GetNextSegmentAsync()
        {
            if (_nextSegment >= _mediaPlaylist.MediaSegments.Count)
                throw new Exception();

            HlsMediaSegment segment = _mediaPlaylist.MediaSegments[_nextSegment];

            if (segment.Key != null && segment.Key.Method != HlsKeyMethod.None && !_keyCache.ContainsKey(segment.Key.Uri))
            {
                byte[] keyData;
                if (OnKeyFetch != null)
                    keyData = await OnKeyFetch(segment.Key);
                else
                    keyData = await HttpClient.GetByteArrayAsync(segment.Key.Uri);

                if (keyData.Length != 16)
                    throw new Exception($"Key length not 16 ({keyData.Length}).");

                _keyCache[segment.Key.Uri] = keyData;
            }

            byte[] segmentBytes;
            if (OnSegmentFetch != null)
                segmentBytes = await OnSegmentFetch(segment);
            else
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, segment.Uri.ToString());
                if (segment.ByteRange != null)
                {
                    string[] rangeParts = segment.ByteRange.Split('@');
                    int len = Int32.Parse(rangeParts[0]), start = Int32.Parse(rangeParts[1]);
                    request.Headers.Range = new RangeHeaderValue(start, start + len - 1);
                }

                HttpResponseMessage response = await HttpClient.SendAsync(request);
                segmentBytes = await response.Content.ReadAsByteArrayAsync();
            }

            ReadOnlyMemory<byte> segmentData = segmentBytes;
            if (segment.Key != null && segment.Key.Method == HlsKeyMethod.Aes128)
            {
                byte[] iv = new byte[16];
                if (segment.Key.KeyFormat == "identity")
                    Buffer.BlockCopy(BitConverter.GetBytes(segment.SequenceNumber).Reverse().ToArray(), 0, iv, 12, 4);
                segmentData = DecryptSegmentData(segmentBytes, segment, _keyCache[segment.Key.Uri], iv);
            }

            _nextSegment++;
            return segmentData;
        }

        public static ReadOnlyMemory<byte> DecryptSegmentData(byte[] encryptedSegmentData, HlsMediaSegment segment, byte[] key, byte[] iv)
        {
            AesEngine engine = new AesEngine(); // TODO: See if this +2 can be made static.
            CbcBlockCipher blockCipher = new CbcBlockCipher(engine);
            PaddedBufferedBlockCipher cipher = new PaddedBufferedBlockCipher(blockCipher, new Pkcs7Padding());
            KeyParameter keyParam = new KeyParameter(key);
            ParametersWithIV keyParamWithIv = new ParametersWithIV(keyParam, iv, 0, 16);

            cipher.Init(false, keyParamWithIv);
            byte[] decryptedSegmentData = new byte[cipher.GetOutputSize(encryptedSegmentData.Length)];
            int length = cipher.ProcessBytes(encryptedSegmentData, decryptedSegmentData, 0);
            length += cipher.DoFinal(decryptedSegmentData, length);

            return new ReadOnlyMemory<byte>(decryptedSegmentData, 0, length);
        }
    }
}
