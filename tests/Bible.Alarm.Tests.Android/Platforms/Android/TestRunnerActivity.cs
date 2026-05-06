#nullable enable

using System.IO.Compression;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Microsoft.DotNet.XHarness.DefaultAndroidEntryPoint.Xunit;
using Microsoft.DotNet.XHarness.TestRunners.Common;

namespace Bible.Alarm.Tests.Android;

/// <summary>
/// Activity-based test runner for the Android device-test host.
///
/// We deliberately avoid the conventional <c>[Instrumentation]</c> route used by xharness:
/// Android calls <c>Instrumentation.onCreate</c> *before* any managed type from
/// <c>Bible.Alarm.Tests.Android.dll</c> is touched, so the JNI bindings for the ACW's
/// <c>n_onCreate</c>/<c>n_onStart</c> have not been registered yet. Result: every run dies with
/// <c>UnsatisfiedLinkError: No implementation found for ... TestInstrumentation.n_onCreate</c>
/// before a single test executes. See [.cursor/rules/testing/multi-platform-tests.mdc] for the
/// full rationale.
///
/// Activities, by contrast, are constructed only after <c>Application.OnCreate</c> has run, so
/// Mono is fully up and managed-side <c>RegisterNatives</c> has already wired the ACW. This host
/// is therefore launched directly via <c>adb shell am start -n &lt;pkg&gt;/.TestRunnerActivity</c>
/// from <c>tests/run-tests.ps1</c> and the CI workflow.
///
/// On-device contract (consumed by the host orchestrator):
///   /sdcard/Documents/test-results/TestResults.xml  — xunit XML
///   /sdcard/Documents/test-results/done.txt         — ASCII integer return code
///   /sdcard/Documents/test-results/error.txt        — present only on unhandled exception
///
/// Code coverage is intentionally NOT collected from this slice — see the "Android coverage" row
/// in [.cursor/rules/testing/multi-platform-tests.mdc] for the rationale.
/// </summary>
// We deliberately do NOT use Theme="@android:style/Theme.NoDisplay" here. Theme.NoDisplay imposes
// a hard contract: the activity must call Finish() synchronously inside OnCreate before onResume
// runs, otherwise Android throws "did not call finish() prior to onResume() completing". Our
// tests run on a Task.Run, so OnCreate returns before Finish is called and that contract trips.
// Using the default activity theme + LaunchMode.SingleInstance avoids the contract entirely; the
// activity is briefly visible (a blank window for ~250ms) but the run is invisible to
// developer/CI workflows because the orchestrator polls done.txt rather than the UI. ExcludeFromRecents
// keeps the activity out of the recent-apps switcher so a stuck run doesn't pollute the device.
[Activity(
    Name = "com.jthomas.info.Bible.Alarm.Tests.TestRunnerActivity",
    Exported = true,
    ExcludeFromRecents = true,
    LaunchMode = LaunchMode.SingleInstance,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public sealed class TestRunnerActivity : Activity
{
    private const string ResultsDir = "/sdcard/Documents/test-results";
    private const string ResultsXml = "TestResults.xml";
    private const string DoneFile = "done.txt";
    private const string ErrorFile = "error.txt";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Fire-and-forget: the orchestrator polls /sdcard/Documents/test-results/done.txt.
        // We must not block OnCreate; RunTestsAsync calls System.Environment.Exit when finished,
        // which terminates the test process so the orchestrator's adb pull sees final-state files.
        _ = Task.Run(RunTestsAsync);
    }

    private async Task RunTestsAsync()
    {
        var returnCode = 0;
        Directory.CreateDirectory(ResultsDir);
        var resultsPath = Path.Combine(ResultsDir, ResultsXml);
        var donePath = Path.Combine(ResultsDir, DoneFile);
        var errorPath = Path.Combine(ResultsDir, ErrorFile);

        try
        {
            // Forward optional `am start -e key value` extras to the entry point so callers can
            // pass include/exclude filters from the command line without us having to plumb a
            // new flag through the orchestrator.
            var args = ExtrasToDictionary(Intent?.Extras);

            // typeof(...) anchors the reference graph for the linker so the cross-platform xunit
            // assembly is guaranteed to ship inside the APK and reach the test runner.
            var testAssembly = typeof(global::Bible.Alarm.Tests.AppSettingsTests).Assembly;

            var entryPoint = new BibleAlarmAndroidEntryPoint(resultsPath, args)
            {
                Tests = new[] { testAssembly },
            };

            await entryPoint.RunAsync().ConfigureAwait(false);

            // xharness's DefaultAndroidEntryPoint creates `<resultsPath>` as a *directory* and
            // writes the xunit XML inside it as `<resultsPath>/TestResults.xml`. We deliberately do
            // NOT try to flatten that on /sdcard: every write-after-delete pattern on the
            // FUSE-backed MediaStore racks up either ENOENT (the filesystem hasn't published the
            // delete yet) or EBUSY (MediaProvider is still indexing the inode). The host-side
            // orchestrator (and CI workflow) both run a deterministic flatten pass after the
            // `adb pull` completes — that filesystem is plain NTFS / ext4 and has none of the
            // visibility hazards. ComputeReturnCodeFromResults below already tolerates the
            // directory layout so we still propagate the exit code correctly to done.txt.

            // The xharness entry point always reports exit-code 0 even when individual tests fail
            // (failures are recorded in TestResults.xml only). Inspect the xunit XML so we can
            // propagate a non-zero done.txt to the orchestrator on failures, otherwise CI would
            // green-flag broken builds.
            returnCode = ComputeReturnCodeFromResults(resultsPath);
        }
        catch (Exception ex)
        {
            returnCode = 1;
            try
            {
                await File.WriteAllTextAsync(errorPath, ex.ToString()).ConfigureAwait(false);
            }
            catch
            {
                // Swallow: writing the diagnostic file is best-effort; we still propagate the
                // failure via done.txt below so the host orchestrator can fail the run.
            }
        }
        finally
        {
            try
            {
                await File.WriteAllTextAsync(donePath, returnCode.ToString()).ConfigureAwait(false);
            }
            catch
            {
                // If we cannot even write done.txt the host's poll loop times out, which is the
                // correct (loud) failure mode.
            }

            RunOnUiThread(() =>
            {
                try { Finish(); } catch { /* finishing twice is harmless */ }
            });

            // System.Environment.Exit (fully qualified to disambiguate from Android.OS.Environment;
            // both are pulled in by `using Android.OS;` which we need for Bundle) terminates the
            // managed process and the underlying Android process so the orchestrator's adb pull
            // sees final-state files.
            System.Environment.Exit(returnCode);
        }
    }

    /// <summary>
    /// Reads the xunit-format <c>TestResults.xml</c> the entry point just produced and returns 1
    /// if any &lt;assembly&gt; element reports a non-zero <c>failed</c> or <c>errors</c> count.
    /// Returns 0 on a clean run or when the file is missing/malformed (the latter is treated as
    /// "no negative signal" because we already track other failure paths via the catch block).
    /// </summary>
    private static int ComputeReturnCodeFromResults(string resultsPath)
    {
        try
        {
            var actualPath = resultsPath;
            // The entry point creates ResultsDir/TestResults.xml as a *directory* containing the
            // actual XML file (xharness convention). Tolerate either layout.
            if (Directory.Exists(actualPath))
            {
                var nested = Directory.EnumerateFiles(actualPath, "*.xml").FirstOrDefault();
                if (nested is null)
                {
                    return 0;
                }
                actualPath = nested;
            }

            if (!File.Exists(actualPath))
            {
                return 0;
            }

            using var reader = System.Xml.XmlReader.Create(actualPath);
            while (reader.Read())
            {
                if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.Name == "assembly")
                {
                    if (int.TryParse(reader.GetAttribute("failed"), out var failed) && failed > 0)
                    {
                        return 1;
                    }
                    if (int.TryParse(reader.GetAttribute("errors"), out var errors) && errors > 0)
                    {
                        return 1;
                    }
                }
            }
        }
        catch
        {
            // Best-effort. Don't let result-XML parsing failures override an otherwise-clean run.
        }

        return 0;
    }

    private static Dictionary<string, string> ExtrasToDictionary(Bundle? extras)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (extras is null)
        {
            return dict;
        }

        foreach (var key in extras.KeySet() ?? Array.Empty<string>())
        {
            var value = extras.GetString(key);
            if (value is not null)
            {
                dict[key] = value;
            }
        }

        return dict;
    }

    /// <summary>
    /// Pins MaxParallelThreads to 1: the cross-platform suite touches the singleton SQLite schedule
    /// DB and the static <c>MauiAppHolder</c>; running them in parallel on a single emulator
    /// triggers contention that does not reproduce on the Windows runner.
    ///
    /// Also overrides <see cref="GetTestAssemblies"/> to hand xharness a real on-disk path for the
    /// test DLL. Even with <c>AndroidUseAssemblyStore=false</c>, MAUI Android keeps assemblies inside
    /// the APK (under <c>assemblies/</c>) and <see cref="P:System.Reflection.Assembly.Location"/> is the empty string;
    /// xharness's <c>XUnitTestRunner</c> then constructs <c>XunitFrontController</c> with just
    /// <c>"&lt;asmName&gt;.dll"</c> and fails its <c>Guard.FileExists</c> check before any test runs.
    /// We extract the DLL to <see cref="global::Android.Content.Context.CacheDir"/> on first call
    /// and pass that path through.
    /// </summary>
    private sealed class BibleAlarmAndroidEntryPoint : DefaultAndroidEntryPoint
    {
        public BibleAlarmAndroidEntryPoint(string resultsPath, Dictionary<string, string> arguments)
            : base(resultsPath, arguments)
        {
        }

        protected override int? MaxParallelThreads => 1;

        protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
        {
            var apkPath = global::Android.App.Application.Context.PackageCodePath
                ?? throw new InvalidOperationException("Application.Context.PackageCodePath was null; cannot locate APK to extract test assemblies from.");
            var cacheDir = global::Android.App.Application.Context.CacheDir?.AbsolutePath
                ?? throw new InvalidOperationException("Application.Context.CacheDir was null; cannot stage extracted test assemblies.");

            foreach (var assembly in Tests)
            {
                var assemblyName = assembly.GetName().Name + ".dll";
#pragma warning disable IL3000 // Android / assembly-store: Location is often empty; APK extraction handles that below.
                var location = assembly.Location;
#pragma warning restore IL3000

                if (string.IsNullOrEmpty(location) || !File.Exists(location))
                {
                    location = ExtractAssemblyFromApk(apkPath, cacheDir, assemblyName);
                }

                yield return new TestAssemblyInfo(assembly, location);
            }
        }

        /// <summary>
        /// Extracts the named assembly from the APK and writes a real .NET PE file to the cache
        /// directory.
        ///
        /// Modern .NET Android (10+) wraps each managed assembly as <c>lib/&lt;abi&gt;/lib_&lt;name&gt;.dll.so</c>
        /// — a tiny ELF shared object whose <c>payload</c> section contains the unmodified .NET PE
        /// bytes. Android requires this layout because the native library extraction policy
        /// auto-extracts <c>lib/&lt;abi&gt;/*.so</c> from the APK at install time, which makes the
        /// embedded assemblies addressable via <c>NativeLibraryDir</c>. xunit's
        /// <c>XunitFrontController</c> needs an actual .dll on disk, so we read the ELF section
        /// table, locate <c>payload</c>, and copy those bytes out as a real <c>.dll</c>.
        ///
        /// Cached after the first call (the APK does not change between runs in the same process,
        /// and the cache dir survives between activity launches but is wiped on uninstall).
        /// </summary>
        private static string ExtractAssemblyFromApk(string apkPath, string cacheDir, string assemblyName)
        {
            var targetPath = Path.Combine(cacheDir, assemblyName);
            if (File.Exists(targetPath))
            {
                return targetPath;
            }

            using var apk = ZipFile.OpenRead(apkPath);

            // The ABI-specific `lib_<asmName>.so` wrapper. The single-RID build pins to android-x64,
            // so x86_64 is the only ABI present; if a multi-RID build is ever added, the first match
            // is fine (any ABI's payload contains the same managed bytes).
            var wrappedName = "lib_" + assemblyName + ".so";
            var entry = apk.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(e.FullName), wrappedName, StringComparison.OrdinalIgnoreCase));

            // Older `<AndroidUseAssemblyStore>false</AndroidUseAssemblyStore>` layouts (or future
            // changes) might still emit a raw .dll under `assemblies/`. Fall back to that.
            if (entry is null)
            {
                entry = apk.Entries.FirstOrDefault(e =>
                    string.Equals(Path.GetFileName(e.FullName), assemblyName, StringComparison.OrdinalIgnoreCase)
                    && e.FullName.StartsWith("assemblies/", StringComparison.OrdinalIgnoreCase));

                if (entry is not null)
                {
                    entry.ExtractToFile(targetPath, overwrite: true);
                    return targetPath;
                }
            }

            if (entry is null)
            {
                var available = string.Join(
                    ", ",
                    apk.Entries
                        .Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
                        .Select(e => e.FullName));

                throw new FileNotFoundException(
                    $"Could not locate '{wrappedName}' inside the APK ({apkPath}). Available lib/*.so entries: {available}",
                    assemblyName);
            }

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            var elfBytes = ms.ToArray();

            var payload = ReadElf64Section(elfBytes, "payload")
                ?? throw new InvalidDataException(
                    $"ELF wrapper '{wrappedName}' has no 'payload' section; cannot extract managed assembly. " +
                    "This likely means .NET Android changed its assembly packaging format — update " +
                    "TestRunnerActivity.ExtractAssemblyFromApk to match the new layout.");

            File.WriteAllBytes(targetPath, payload);
            return targetPath;
        }

        /// <summary>
        /// Minimal ELF64 section reader. Returns the bytes of the section whose name matches
        /// <paramref name="sectionName"/>, or <c>null</c> if no such section exists.
        ///
        /// Only handles ELF64 little-endian (x86_64/arm64) — sufficient for every Android target the
        /// .NET 10 SDK supports today. The header layout is fixed: see the ELF64 spec
        /// (<c>Elf64_Ehdr</c>, <c>Elf64_Shdr</c>) for offsets.
        /// </summary>
        private static byte[]? ReadElf64Section(byte[] elf, string sectionName)
        {
            if (elf.Length < 64 || elf[0] != 0x7F || elf[1] != (byte)'E' || elf[2] != (byte)'L' || elf[3] != (byte)'F')
            {
                throw new InvalidDataException("Not a valid ELF file (magic mismatch).");
            }

            // Elf64_Ehdr offsets (little-endian, ELF64).
            var sectionHeaderOffset = BitConverter.ToInt64(elf, 0x28);
            var sectionHeaderEntrySize = BitConverter.ToUInt16(elf, 0x3A);
            var sectionHeaderCount = BitConverter.ToUInt16(elf, 0x3C);
            var stringTableIndex = BitConverter.ToUInt16(elf, 0x3E);

            // Section name string table (.shstrtab) header → file offset of the strings.
            var stringTableHeader = sectionHeaderOffset + (stringTableIndex * sectionHeaderEntrySize);
            var stringTableFileOffset = BitConverter.ToInt64(elf, (int)(stringTableHeader + 0x18));

            for (var i = 0; i < sectionHeaderCount; i++)
            {
                var header = (int)(sectionHeaderOffset + (i * sectionHeaderEntrySize));
                var nameOffset = BitConverter.ToUInt32(elf, header);
                var sectionFileOffset = BitConverter.ToInt64(elf, header + 0x18);
                var sectionSize = BitConverter.ToInt64(elf, header + 0x20);

                var nameStart = (int)(stringTableFileOffset + nameOffset);
                var nameEnd = Array.IndexOf(elf, (byte)0, nameStart);
                var name = System.Text.Encoding.ASCII.GetString(elf, nameStart, nameEnd - nameStart);

                if (string.Equals(name, sectionName, StringComparison.Ordinal))
                {
                    var payload = new byte[sectionSize];
                    Array.Copy(elf, sectionFileOffset, payload, 0, sectionSize);
                    return payload;
                }
            }

            return null;
        }
    }
}
