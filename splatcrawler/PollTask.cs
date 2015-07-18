using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SquidTracker.Crawler
{
    public abstract class PollTask
    {
        public DateTime NextPollTime { get; protected set; }
        public abstract void Run();
    }
}
