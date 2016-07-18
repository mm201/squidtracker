using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SquidTracker
{
    public static class Common
    {
        public static T Min<T>(T first, T second) where T : IComparable<T>
        {
            return first.CompareTo(second) > 0 ? second : first;
        }

        public static T Max<T>(T first, T second) where T : IComparable<T>
        {
            return first.CompareTo(second) > 0 ? first : second;
        }
    }
}
