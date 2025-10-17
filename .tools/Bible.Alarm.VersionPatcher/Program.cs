using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Bible.Alarm.VersionPatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootDir = DirectoryHelper.IndexDirectory;

            patchUwp(rootDir);
            patchiOS(rootDir);
            patchAndroid(rootDir);
        }

        private static void patchAndroid(string rootDir)
        {
            var manifestFile = Path.Combine(rootDir, "src", "Bible.Alarm", "Bible.Alarm.Droid", "Properties", "AndroidManifest.xml");
            var doc = new XmlDocument();
            doc.Load(manifestFile);

            var sdkNode = doc.SelectSingleNode("/manifest");

            var attrs = sdkNode.Attributes;

            var versionCode = attrs["android:versionCode"].Value;
            var versionName = attrs["android:versionName"].Value;

            var newVersionCode = $"{int.Parse(versionCode) + 1}";

            var versionNameSplit = versionName.Split(".");
            var oldVersionMinor = int.Parse(versionNameSplit[1]);
            var oldVersionMajor = int.Parse(versionNameSplit[0]);
            
            var newVersionMinor = oldVersionMinor == 99 ? 0 : oldVersionMinor + 1;
            var newVersionMajor = newVersionMinor == 99 ? oldVersionMajor + 1 : oldVersionMajor;
          
            var newVersionName = $"{newVersionMajor}.{newVersionMinor}";

            attrs["android:versionCode"].Value = newVersionCode;
            attrs["android:versionName"].Value = newVersionName;
            doc.Save(manifestFile);
        }

        private static void patchiOS(string rootDir)
        {
            var manifestFile = Path.Combine(rootDir, "src", "Bible.Alarm", "Bible.Alarm.iOS", "Info.plist");
            
            var file =
                new StreamReader(manifestFile);

            string line;

            var output = new StringBuilder();
            while ((line = file.ReadLine()) != null)
            {
               if(line.Trim() == "<key>CFBundleVersion</key>")
                {
                    output.AppendLine(line);

                    var nextLine = file.ReadLine();
                    var oldVersion = nextLine.Replace("<string>", string.Empty)
                                            .Replace("</string>", string.Empty)
                                            .Trim();

                    var oldVersionSplit = oldVersion.Split(".");
                    var oldVersionMinor = int.Parse(oldVersionSplit[1]);
                    var oldVersionMajor = int.Parse(oldVersionSplit[0]);

                    var newVersionMinor = oldVersionMinor == 99 ? 0 : oldVersionMinor + 1;
                    var newVersionMajor = newVersionMinor == 99 ? oldVersionMajor + 1 : oldVersionMajor;

                    var newVersion = $"{newVersionMajor}.{newVersionMinor}";
                    var matchRegex = new Regex(@"<string>.*<\/string>");
                    var newLine = matchRegex.Replace(nextLine, $"<string>{newVersion}</string>");
                    output.AppendLine(newLine);

                    continue;
                }

                output.AppendLine(line);
            }

            file.Close();

            File.WriteAllText(manifestFile, output.ToString());
        }

        private static void patchUwp(string rootDir)
        {
            var manifestFile = Path.Combine(rootDir, "src", "Bible.Alarm", "Bible.Alarm.UWP", "Package.appxmanifest");
            var doc = new XmlDocument();
            doc.Load(manifestFile);

            var identityNode = doc.LastChild.FirstChild;
            var attrs = identityNode.Attributes;

            var versionName = attrs["Version"].Value;

            var versionNameSplit = versionName.Split(".");
            var oldVersionMinor = int.Parse(versionNameSplit[1]);
            var oldVersionMajor = int.Parse(versionNameSplit[0]);

            var newVersionMinor = oldVersionMinor == 99 ? 0 : oldVersionMinor + 1;
            var newVersionMajor = newVersionMinor == 99 ? oldVersionMajor + 1 : oldVersionMajor;

            var newVersionName = $"{newVersionMajor}.{newVersionMinor}.0.0";

            attrs["Version"].Value = newVersionName;
            doc.Save(manifestFile);
        }

    }
}
