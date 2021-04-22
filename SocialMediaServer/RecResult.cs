using System.Collections.Generic;

namespace SocialMediaServer
{
    public class RecResult
    {
        public List<Account> recommendation { get; set; }
        public HashSet<string> seenusers { get; set; }
    }
}
