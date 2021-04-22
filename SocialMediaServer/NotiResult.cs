using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialMediaServer
{
    public class NotiResult
    {
        public List<Notification> newnotis { get; set; } = new();
        public List<Notification> seennotis { get; set; } = new();
    }
}
