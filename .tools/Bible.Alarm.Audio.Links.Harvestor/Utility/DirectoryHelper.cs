using System;
using System.IO;

namespace AudioLinkHarvestor.Utility
{
    public static class DirectoryHelper
    {
        internal static string IndexDirectory => indexDirectory.Value;

        private static Lazy<string> indexDirectory = new Lazy<string>(() =>
         {
             var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

             while (currentDir.Name != "bible-alarm")
             {
                 currentDir = currentDir.Parent;
             }

             return Path.Combine(currentDir.FullName, "src", "_tools", "_index");
         });

        public static void Ensure(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
