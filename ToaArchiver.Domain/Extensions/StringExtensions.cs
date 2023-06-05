using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToaArchiver.Domain.Extensions
{
    internal static class StringExtensions
    {
        internal static bool ContainsAny(this string str, IEnumerable<string> strings)
        {
            foreach(string s in strings)
            {
                if (str.Contains(s)) return true;
            }
            return false;
        }
    }
}
