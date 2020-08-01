using System.Collections.Generic;

namespace WebMediaToolkit.Hls
{
    public static class AttributeList
    {
        public static IReadOnlyDictionary<string, string> Parse(string list)
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>();

            int curLoc = 0;
            while (curLoc != -1 && curLoc < list.Length)
            {
                int eqLoc = list.IndexOf('=', curLoc);
                string key = list.Substring(curLoc, eqLoc - curLoc), value;

                bool isQuotedString = (list[eqLoc + 1] == '"');
                if (isQuotedString)
                {
                    int quotLoc = list.IndexOf('"', eqLoc + 2);
                    value = list.Substring(eqLoc + 1, quotLoc + 1 - (eqLoc + 1));
                    curLoc = quotLoc + 2;
                }
                else
                {
                    int endLoc = list.IndexOf(',', eqLoc + 1);
                    if (endLoc == -1)
                        endLoc = list.Length;
                    value = list.Substring(eqLoc + 1, endLoc - (eqLoc + 1));
                    curLoc = endLoc + 1;
                }

                attributes.Add(key, value);
            }

            return attributes;
        }
    }
}
