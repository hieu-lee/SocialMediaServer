using System;
using System.Collections.Generic;

namespace SocialMediaServer
{
    public class PostResult
    {
        public List<Post> posts { get; set; }
        public DateTime checkpoint { get; set; }
    }
}
