using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialMediaServer
{
    public class Account
    {
        [BsonId]
        public string username { get; set; }
        [BsonElement("email")]
        public string email { get; set; }
        [BsonElement("name")]
        public string name { get; set; }
        [BsonElement("password")]
        public string password { get; set; }
        [BsonElement("avatar")]
        public byte[] avatar { get; set; }
        [BsonElement("cover")]
        public byte[] cover { get; set; }
        [BsonElement("friendlist")]
        public HashSet<string> friendlist { get; set; } = new();
        [BsonElement("waitinglist")]
        public HashSet<string> waitinglist { get; set; } = new();
        [BsonElement("bio")]
        public string bio { get; set; } = string.Empty;
        [BsonElement("online")]
        public bool online { get; set; } = false;
        [BsonElement("seennotis")]
        public List<Notification> seennotis = new();
        [BsonElement("newnotis")]
        public List<Notification> newnotis = new();

        public override bool Equals(object obj)
        {
            var other = (Account)obj;
            return username.Equals(other.username);
        }

        public override int GetHashCode()
        {
            return username.GetHashCode();
        }
    }
}
