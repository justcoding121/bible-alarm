using Loggly.Config;
using System.Collections.Generic;

namespace Loggly
{
    public class EventOptions
    {
        /// <summary>
        /// Custom tags per event
        /// </summary>
        public List<ITag> Tags { get; set; }

        public EventOptions()
        {
            Tags = new List<ITag>();
        }
    }
}