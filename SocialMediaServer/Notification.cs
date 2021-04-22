using System;


namespace SocialMediaServer
{
    public class Notification : IComparable<Notification>
    {
        public DateTime time { get; set; }
        public string username { get; set; }
        public string type { get; set; }

        public int CompareTo(Notification other)
        {
            return time.CompareTo(other.time);
        }
    }
}
