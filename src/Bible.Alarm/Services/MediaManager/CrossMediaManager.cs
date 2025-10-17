using System;
using System.Diagnostics;

namespace MediaManager
{
    /// <summary>
    /// Cross MediaManager
    /// </summary>
    [DebuggerStepThrough]
    public static class CrossMediaManager
    {
        public static Lazy<IMediaManager> Implementation;
          
        /// <summary>
        /// Gets if the plugin is supported on the current platform.
        /// </summary>
        public static bool IsSupported => Implementation.Value != null;

        /// <summary>
        /// Current plugin implementation to use
        /// </summary>
        public static IMediaManager Current
        {
            get
            {
                var ret = Implementation?.Value;
                if (ret == null)
                {
                    throw NotImplementedInReferenceAssembly();
                }
                return ret;
            }
        }

        internal static Exception NotImplementedInReferenceAssembly() =>
            new NotImplementedException("This functionality is not implemented in the portable version of this assembly. You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
    }
}
