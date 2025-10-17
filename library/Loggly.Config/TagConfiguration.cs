using System.Collections.Generic;

namespace Loggly.Config
{
    public class TagConfiguration : ITagConfiguration
    {
        public List<ITag> Tags { get; private set; } = new List<ITag>();
    }
}