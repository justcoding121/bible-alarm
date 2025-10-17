using System;
using System.IO;

namespace Bible.Alarm.VersionPatcher
{
    public static class DirectoryHelper
    {
        public static string IndexDirectory => indexDirectory.Value;

        private static Lazy<string> indexDirectory = new Lazy<string>(() =>
         {
             var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

             while (currentDir.Name != "bible-alarm")
             {
                 currentDir = currentDir.Parent;
             }

             return Path.Combine(currentDir.FullName);
         });
    }
}
