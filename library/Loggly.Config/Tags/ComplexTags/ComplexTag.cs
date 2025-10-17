using Loggly.Config;
using System;

namespace Loggly
{
    public abstract class ComplexTag : ITag
    {
        public string Formatter { get; set; } = "{0}";

        public abstract string InputValue { get; }

        public string Value
        {
            get { return String.Format(Formatter, InputValue); }
        }
    }
}