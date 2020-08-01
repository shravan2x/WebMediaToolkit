using System;

namespace WebMediaToolkit.Hls
{
    public class HlsException : Exception
    {
        public HlsException(string message)
            : base(message) { }
    }
}
