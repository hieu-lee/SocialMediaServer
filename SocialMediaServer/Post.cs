using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace SocialMediaServer
{
    public class Post : IComparable<Post>
    {
        [BsonId]
        public string Id { get; set; }
        [BsonElement("userava")]
        public byte[] userava { get; set; }
        [BsonElement("username")]
        public string username { get; set; }
        [BsonElement("caption")]
        public string caption { get; set; } = string.Empty;
        [BsonElement("image")]
        public byte[] image { get; set; }
        [BsonElement("comments")]
        public Dictionary<string, Comment> comments { get; set; } = new();
        [BsonElement("shares")]
        public HashSet<Account> shares = new();
        [BsonElement("upvotes")]
        public HashSet<string> upvotes { get; set; } = new();
        [BsonElement("downvotes")]
        public HashSet<string> downvotes { get; set; } = new();
        [BsonElement("sharedpost")]
        public Post sharedpost { get; set; }
        [BsonElement("time")]
        public DateTime time { get; set; }
        [BsonElement("upvotescount")]
        public int upvotescount { get; set; } = 0;
        [BsonElement("downvotescount")]
        public int downvotescount { get; set; } = 0;
        [BsonElement("commentscount")]
        public int commentscount { get; set; } = 0;

        public int CompareTo(Post other)
        {
            return (shares.Count * 5 + comments.Count * 3 + upvotes.Count - 2 * downvotes.Count).CompareTo(other.shares.Count * 5 + other.comments.Count * 3 + other.upvotes.Count - 2 * other.downvotes.Count);
        }

        public void NewComment(Comment comment)
        {
            comments.Add(comment.Id, comment);
            commentscount++;
        }
        
        public void DeleteComment(string id)
        {
            comments.Remove(id);
            commentscount--;
        }

        public void NewUpvote(string username)
        {
            upvotes.Add(username);
            upvotescount++;
        }

        public void UnUpvote(string username)
        {
            upvotes.Remove(username);
            upvotescount--;
        }

        public void NewDownvote(string username)
        {
            downvotes.Add(username);
            downvotescount++;
        }

        public void UnDownvote(string username)
        {
            downvotes.Remove(username);
            downvotescount--;
        }
    }
}
