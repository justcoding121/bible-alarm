using System;
using System.Diagnostics;

namespace Loggly
{
    class EnvironmentProvider : IEnvironmentProvider
    {
        public int ProcessId
        {
            get
            {
                return Process.GetCurrentProcess().Id;
            }
        }

        public string MachineName
        {
            get
            {
                return Environment.MachineName;
            }
        }
    }
}
