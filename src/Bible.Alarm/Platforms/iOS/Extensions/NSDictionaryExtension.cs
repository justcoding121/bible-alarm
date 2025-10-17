using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Foundation;
using UIKit;

namespace Bible.Alarm.iOS.Extensions
{
    public static class NSDictionaryExtension
    {
        public static NSDictionary ToNSDictionary(this Dictionary<string, string> input)
        {
            return NSDictionary.FromObjectsAndKeys(input.Values.ToArray()
                                               , input.Keys.ToArray());
        }

        public static Dictionary<string, string> ToDictionary(this NSDictionary input)
        {
            return input.ToDictionary(x => x.Key.ToString(), x => x.Value.ToString());
        }
    }
}
