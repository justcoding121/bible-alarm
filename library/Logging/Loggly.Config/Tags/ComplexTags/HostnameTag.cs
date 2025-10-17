using System;

namespace Loggly
{
    public class HostnameTag : ComplexTag
    {
        public override string InputValue
        {
            get { return Environment.MachineName; }
        }
    }
}
