using System;
using System.Collections.Generic;
using System.Drawing;

namespace WebMediaToolkit.Hls
{
    public static class AttributeValueUtils
    {
        public static bool IsQuotedString(string quotedString)
        {
            return (quotedString[0] == '"');
        }

        public static string GetRawOrNull(IReadOnlyDictionary<string, string> attributes, string key)
        {
            return (attributes.ContainsKey(key) ? attributes[key] : null);
        }

        public static ulong? GetDecimalIntegerOrNull(IReadOnlyDictionary<string, string> attributes, string key)
        {
            return (attributes.ContainsKey(key) ? (ulong?) ParseDecimalInteger(attributes[key]) : null);
        }

        public static float? GetFloatOrNull(IReadOnlyDictionary<string, string> attributes, string key)
        {
            // Use Xyz.Parse instead of Convert.ToXyz so an exception is thrown on null.
            return (attributes.ContainsKey(key) ? (float?) Single.Parse(attributes[key]) : null);
        }

        public static T? GetEnumOrNull<T>(IReadOnlyDictionary<string, string> attributes, string key) where T : struct, Enum
        {
            return (attributes.ContainsKey(key) ? (T?) ParseEnum<T>(attributes[key]) : null);
        }

        public static Size? GetResolutionOrNull(IReadOnlyDictionary<string, string> attributes, string key)
        {
            return (attributes.ContainsKey(key) ? (Size?) ParseResolution(attributes[key]) : null);
        }

        public static string GetStringOrNull(IReadOnlyDictionary<string, string> attributes, string key)
        {
            return (attributes.ContainsKey(key) ? ParseQuotedString(attributes[key]) : null);
        }

        public static Uri GetUriOrNull(IReadOnlyDictionary<string, string> attributes, Uri baseUri, string key)
        {
            return (attributes.ContainsKey(key) ? new Uri(baseUri, ParseQuotedString(attributes[key])) : null);
        }

        public static Uri GetUri(IReadOnlyDictionary<string, string> attributes, Uri baseUri, string key)
        {
            return new Uri(baseUri, ParseQuotedString(attributes[key]));
        }

        public static ulong ParseDecimalInteger(string value)
        {
            return UInt64.Parse(value);
        }

        public static string ParseQuotedString(string value)
        {
            return value.Substring(1, value.Length - 2);
        }

        public static T ParseEnum<T>(string value) where T : Enum
        {
            return (T) Enum.Parse(typeof(T), value.Replace("-", ""), true);
        }

        public static Size ParseResolution(string decimalResolution)
        {
            int xLoc = decimalResolution.IndexOf('x');
            string widthStr = decimalResolution.Substring(0, xLoc);
            string heightStr = decimalResolution.Substring(xLoc + 1, decimalResolution.Length - xLoc - 1);

            return new Size(Int32.Parse(widthStr), Int32.Parse(heightStr));
        }
    }
}
