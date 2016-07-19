using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SquidTracker.Data;

namespace SquidTracker.Crawler
{
    /// <summary>
    /// The purpose of this class is to keep NNID login details in a separete
    /// file.
    /// This makes it easier to stage changes on Git without worrying about
    /// login details getting committed, and reduces occasions where login
    /// details are visible on my screen and someone can look over my
    /// shoulder.
    /// </summary>
    internal static class NnidLogins
    {
        internal static List<Nnid> GetLogins()
        {
            return new List<Nnid>()
            {
                new Nnid("username1", "password1", NnidRegions.America),
            };
        }
    }
}
