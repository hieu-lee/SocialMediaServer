using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SocialMediaServer.Services;
using SocialMediaServer.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace SocialMediaServer.Controllers
{
    [EnableCors("MyPolicy")]
    [ApiKeyAuth]
    [Route("[controller]")]
    [ApiController]
    public class postsController : ControllerBase
    {
        private EncryptionAndCompressService encryptService;
        private IMongoCollection<Post> posts;
        private IMongoCollection<Account> accounts;
        private IMongoDatabase walls;
        private MemoryDataAndEmailService memoryService;

        public postsController(IMongoClient client, EncryptionAndCompressService encrypt, MemoryDataAndEmailService memoryService)
        {
            encryptService = encrypt;
            this.memoryService = memoryService;
            var database = client.GetDatabase("SocialMediaManagement");
            accounts = database.GetCollection<Account>("accounts");
            posts = database.GetCollection<Post>("posts");
            walls = client.GetDatabase("Walls");
        }

        [HttpGet("newsfeed/{username}")]
        public async Task<PostResult> GetNewsFeedPosts(string username)
        {
            var myacc = accounts.Find(s => s.username == username).FirstOrDefault();
            var postfilter1 = Builders<Post>.Filter.Where(s => !myacc.friendlist.Contains(s.username));
            var task = posts.Find(postfilter1).SortByDescending(s => s.time).Limit(25).ToListAsync();
            var postfilter = Builders<Post>.Filter.Where(s => myacc.friendlist.Contains(s.username));
            var res = posts.Find(postfilter).SortByDescending(s => s.time).Limit(25).ToList();
            var n = res.Count;
            DateTime checkpoint = res[n - 1].time;
            res.Sort((a,b) => b.CompareTo(a));
            if (n < 25)
            {
                var backup = await task;
                backup.Sort();
                foreach (var post in backup)
                {
                    if (n == 25)
                    {
                        break;
                    }
                    res.Add(post);
                    if (post.time < checkpoint)
                    {
                        checkpoint = post.time;
                    }
                    n++;
                }
                return new PostResult() { posts = res, checkpoint = checkpoint };
            }
            return new PostResult() { posts = res, checkpoint = checkpoint };
        }

        [HttpGet("wall/{username}")]
        public PostResult GetWallPosts(string username)
        {
            var mywall = walls.GetCollection<Post>(username);
            var posts = mywall.Find(s => true).SortByDescending(s => s.time).Limit(30).ToList();
            var n = posts.Count;
            return new PostResult() { posts = posts, checkpoint = posts[n - 1].time };
        }

        [HttpGet("loadnewsfeed/{username}/{time}")]
        public async Task<PostResult> LoadNewsFeedPosts(string username, DateTime time)
        {
            var myacc = accounts.Find(s => s.username == username).FirstOrDefault();
            var postfilter1 = Builders<Post>.Filter.Where(s => !myacc.friendlist.Contains(s.username) && s.time < time);
            var task = posts.Find(postfilter1).SortByDescending(s => s.time).Limit(25).ToListAsync();
            var postfilter = Builders<Post>.Filter.Where(s => myacc.friendlist.Contains(s.username) && s.time < time);
            var res = posts.Find(postfilter).SortByDescending(s => s.time).Limit(25).ToList();
            var n = res.Count;
            DateTime checkpoint = res[n - 1].time;
            res.Sort((a, b) => b.CompareTo(a));
            if (n < 25)
            {
                var backup = await task;
                backup.Sort();
                foreach (var post in backup)
                {
                    if (n == 25)
                    {
                        break;
                    }
                    res.Add(post);
                    if (post.time < checkpoint)
                    {
                        checkpoint = post.time;
                    }
                    n++;
                }
                return new PostResult() { posts = res, checkpoint = checkpoint };
            }
            return new PostResult() { posts = res, checkpoint = checkpoint };
        }

        [HttpGet("loadwall/{username}/{time}")]
        public PostResult LoadWallPosts(string username, DateTime time)
        {
            var mywall = walls.GetCollection<Post>(username);
            var posts = mywall.Find(s => s.time < time).SortByDescending(s => s.time).Limit(30).ToList();
            var n = posts.Count;
            return new PostResult() { posts = posts, checkpoint = posts[n - 1].time };
        }

        [HttpGet("post/{postid}")]
        public Post GetPost(string postid)
        {
            return posts.Find(s => s.Id == postid).FirstOrDefault();
        }

        [HttpPost("post/{username}")]
        public async Task<string> AddNewPost(string username, [FromBody] Post post)
        {
            post.Id = ObjectId.GenerateNewId().ToString();
            var myacc = accounts.Find(s => s.username == username).FirstOrDefault();
            post.userava = encryptService.Compress(myacc.avatar);
            var task = posts.InsertOneAsync(post);
            walls.GetCollection<Post>(username).InsertOne(post);
            await task;
            return post.Id;
        }

        [HttpPost("share/{username}/{postid}")]
        public async Task ShareNewPost(string username, string postid, [FromBody] string caption)
        {
            if (caption is null)
            {
                caption = string.Empty;
            }
            var task = posts.Find(s => s.Id == postid).FirstOrDefaultAsync();
            var myacc = accounts.Find(s => s.username == username).FirstOrDefault();
            var userava = encryptService.Compress(myacc.avatar);
            var sharedpost = await task;
            var notitask = memoryService.AddNotificationAsync(accounts, username, sharedpost.username, "shared");
            Post post = new() { username = username, userava = userava, caption = caption, time = DateTime.Now, sharedpost = sharedpost };
            var task1 = posts.InsertOneAsync(post);
            walls.GetCollection<Post>(username).InsertOne(post);
            await task1;
            await notitask;
        }

        [HttpPost("comment/{postid}")]
        public async Task<string> AddNewComment(string postid, [FromBody] Comment comment)
        {
            comment.Id = ObjectId.GenerateNewId().ToString();
            var filter = Builders<Post>.Filter.Eq("_id", postid);
            var task = Task.Factory.StartNew(() => { return walls.GetCollection<Post>(comment.username); });
            var post = posts.Find(filter).FirstOrDefault();
            var notitask = memoryService.AddNotificationAsync(accounts, comment.username, post.username, "comment", post.comments);
            post.NewComment(comment);
            var update = Builders<Post>.Update.Set("comments", post.comments);
            var update1 = Builders<Post>.Update.Set("commentscount", post.commentscount);
            var task1 = posts.UpdateOneAsync(filter, update);
            var task2 = posts.UpdateOneAsync(filter, update1);
            var wall = await task;
            var task3 = wall.UpdateOneAsync(filter, update);
            wall.UpdateOne(filter, update1);
            await task1;
            await task2;
            await task3;
            await notitask;
            return comment.Id;
        }

        [HttpPut("caption/{username}/{postid}")]
        public async Task UpdateCaption(string username, string postid, [FromBody] string caption)
        {
            var filter = Builders<Post>.Filter.Eq("_id", postid);
            var update = Builders<Post>.Update.Set("caption", caption);
            var task = posts.UpdateOneAsync(filter, update);
            walls.GetCollection<Post>(username).UpdateOne(filter, update);
            await task;
        }

        [HttpPut("comment/{username}/{postid}/{commentid}")]
        public async Task UpdateCommentContent(string username, string postid, string commentid, [FromBody] string content)
        {
            var task = Task.Factory.StartNew(() => { return walls.GetCollection<Post>(username); });
            var filter = Builders<Post>.Filter.Eq("_id", postid);
            var post = posts.Find(filter).FirstOrDefault();
            post.comments[commentid].content = content;
            var update = Builders<Post>.Update.Set("comments", post.comments);
            var task1 = posts.UpdateOneAsync(filter, update);
            var wall = await task;
            wall.UpdateOne(filter, update);
            await task1;
        }

        [HttpPut("upvote/{username}")]
        public async Task UpvotePost(string username, [FromBody] Post post)
        {
            var notitask = memoryService.AddNotificationAsync(accounts, username, post.username, "upvote");
            var task = Task.Factory.StartNew(() => { return walls.GetCollection<Post>(username); });
            post.NewUpvote(username);
            var filter = Builders<Post>.Filter.Eq("_id", post.Id);
            var update = Builders<Post>.Update.Set("upvotes", post.upvotes);
            var update1 = Builders<Post>.Update.Set("upvotescount", post.upvotescount);
            var task1 = posts.UpdateOneAsync(filter, update);
            var task2 = posts.UpdateOneAsync(filter, update1);
            var mywall = await task;
            mywall.UpdateOne(filter, update);
            mywall.UpdateOne(filter, update1);
            await task1;
            await task2;
            await notitask;
        }

        [HttpPut("unupvote/{username}")]
        public async Task UnUpvotePost(string username, [FromBody] Post post)
        {
            var task = Task.Factory.StartNew(() => { return walls.GetCollection<Post>(username); });
            post.UnUpvote(username);
            var filter = Builders<Post>.Filter.Eq("_id", post.Id);
            var update = Builders<Post>.Update.Set("upvotes", post.upvotes);
            var update1 = Builders<Post>.Update.Set("upvotescount", post.upvotescount);
            var task1 = posts.UpdateOneAsync(filter, update);
            var task2 = posts.UpdateOneAsync(filter, update1);
            var mywall = await task;
            mywall.UpdateOne(filter, update);
            mywall.UpdateOne(filter, update1);
            await task1;
            await task2;
        }

        [HttpPut("downvote/{username}")]
        public async Task DownvotePost(string username, [FromBody] Post post)
        {
            var notitask =  memoryService.AddNotificationAsync(accounts, username, post.username, "downvote");
            var task = Task.Factory.StartNew(() => { return walls.GetCollection<Post>(username); });
            post.NewDownvote(username);
            var filter = Builders<Post>.Filter.Eq("_id", post.Id);
            var update = Builders<Post>.Update.Set("downvotes", post.downvotes);
            var update1 = Builders<Post>.Update.Set("downvotescount", post.downvotescount);
            var task1 = posts.UpdateOneAsync(filter, update);
            var task2 = posts.UpdateOneAsync(filter, update1);
            var mywall = await task;
            mywall.UpdateOne(filter, update);
            mywall.UpdateOne(filter, update1);
            await task1;
            await task2;
            await notitask;
        }

        [HttpPut("undownvote/{username}")]
        public async Task UnDownvotePost(string username, [FromBody] Post post)
        {
            var task = Task.Factory.StartNew(() => { return walls.GetCollection<Post>(username); });
            post.UnDownvote(username);
            var filter = Builders<Post>.Filter.Eq("_id", post.Id);
            var update = Builders<Post>.Update.Set("downvotes", post.downvotes);
            var update1 = Builders<Post>.Update.Set("downvotescount", post.downvotescount);
            var task1 = posts.UpdateOneAsync(filter, update);
            var task2 = posts.UpdateOneAsync(filter, update1);
            var mywall = await task;
            mywall.UpdateOne(filter, update);
            mywall.UpdateOne(filter, update1);
            await task1;
            await task2;
        }

        [HttpDelete("post/{username}/{postid}")]
        public async Task DeletePost(string username, string id)
        {
            var filter = Builders<Post>.Filter.Eq("_id", id);
            var task = posts.DeleteOneAsync(filter);
            walls.GetCollection<Post>(username).DeleteOne(filter);
            await task;
        }

        [HttpDelete("comment/{username}/{postid}/{commentid}")]
        public async Task DeleteCommentFromPost(string username, string postid, string commentid)
        {
            var task = Task.Factory.StartNew(() => { return walls.GetCollection<Post>(username); });
            var filter = Builders<Post>.Filter.Eq("_id", postid);
            var post = posts.Find(filter).FirstOrDefault();
            post.DeleteComment(commentid);
            var update = Builders<Post>.Update.Set("comments", post.comments);
            var update1 = Builders<Post>.Update.Set("commentscount", post.commentscount);
            var task1 = posts.UpdateOneAsync(filter, update);
            var task2 = posts.UpdateOneAsync(filter, update1);
            var wall = await task;
            var task3 = wall.UpdateOneAsync(filter, update);
            wall.UpdateOne(filter, update1);
            await task1;
            await task2;
            await task3;
        }
    }
}
