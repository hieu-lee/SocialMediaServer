using MongoDB.Bson;
using System;

namespace SocialMediaServer
{
    public class Comment
    {
        public string Id { get; set; }
        public string username { get; set; }
        public byte[] userava { get; set; }
        public string content { get; set; }
        public byte[] image { get; set; }
        public DateTime time { get; set; }

        public override bool Equals(object obj)
        {
            var other = (Comment)obj;
            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
        {
            return Id.ToString().GetHashCode();
        }
    }
}
