using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SocialMediaServer.Validation;
using SocialMediaServer.Services;
using MongoDB.Bson;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace SocialMediaServer.Controllers
{
    [ApiKeyAuth]
    [Route("[controller]")]
    [ApiController]
    public class accountsController : ControllerBase
    {
        private IMongoDatabase walls;
        EncryptionAndCompressService encryptService;
        MemoryDataAndEmailService memoryService;
        IMongoCollection<Account> accounts;

        public accountsController(IMongoClient client, MemoryDataAndEmailService data, EncryptionAndCompressService encrypt)
        {
            var database = client.GetDatabase("SocialMediaManagement");
            memoryService = data;
            encryptService = encrypt;
            accounts = database.GetCollection<Account>("accounts");
            walls = client.GetDatabase("Walls");
        }

        [HttpGet("{username}/search")]
        public Account SearchByUsername(string username, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken)
            {
                var acc = accounts.Find(s => s.username == username).FirstOrDefault();
                if (acc is not null)
                {
                    return new Account() { username = acc.username, avatar = encryptService.Compress(acc.avatar) };
                }
                return null;
            }
            return null;
        }

        [HttpGet("{username}/rec")]
        public RecResult GetFriendsRec(string username, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken)
            {
                HashSet<string> seenusers = new();
                List<Account> res = new();
                var myacc = accounts.Find(s => s.username != username).FirstOrDefault();
                var accs = accounts.Find(s => s.username != username && !myacc.friendlist.Contains(s.username)).Limit(100).ToList();
                foreach (var acc in accs)
                {
                    seenusers.Add(acc.username);
                    Account account = new() { username = acc.username, avatar = encryptService.Compress(acc.avatar) };
                    res.Add(account);
                }
                return new RecResult() { recommendation = res, seenusers = seenusers };
            }
            return null;
        }

        [HttpGet("{username}/avatar")]
        public byte[] GetMyAvatar(string username, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var acc = accounts.Find(filter).FirstOrDefault();
                var avatar = Encoding.UTF8.GetBytes(encryptService.Decrypt(Encoding.UTF8.GetString(acc.avatar)));
                return avatar;
            }
            return null;
        }

        [HttpGet("{username}/cover")]
        public byte[] GetMyCover(string username, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var acc = accounts.Find(filter).FirstOrDefault();
                var cover = Encoding.UTF8.GetBytes(encryptService.Decrypt(Encoding.UTF8.GetString(acc.cover)));
                return cover;
            }
            return null;
        }

        [HttpGet("{username}/newnoti")]
        public int GetNewNotifications(string username, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                return accounts.Find(filter).FirstOrDefault().newnotis.Count;
            }
            return 0;
        }

        [HttpGet("{username}/noti")]
        public async Task<NotiResult> GetNotifications(string username, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var acc = accounts.Find(filter).FirstOrDefault();
                var newnotis = acc.newnotis;
                var seennotis = acc.seennotis;
                acc.newnotis.Clear();
                acc.seennotis.AddRange(newnotis);
                var task = accounts.FindOneAndReplaceAsync(filter, acc);
                newnotis.Sort();
                seennotis.Sort();
                await task;
                return new NotiResult() { newnotis = newnotis, seennotis = seennotis };
            }
            return null;
        }

        [HttpGet("{username}")]
        public Account GetMyAccount(string username, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var acc = accounts.Find(filter).FirstOrDefault();
                acc.avatar = Encoding.UTF8.GetBytes(encryptService.Decrypt(Encoding.UTF8.GetString(acc.avatar)));
                acc.avatar = encryptService.Compress(acc.avatar);
                acc.cover = Encoding.UTF8.GetBytes(encryptService.Decrypt(Encoding.UTF8.GetString(acc.cover)));
                acc.cover = encryptService.Compress(acc.cover);
                acc.password = string.Empty;
                return acc;
            }
            return null;
        }

        [HttpPost("rec/{username}")]
        public RecResult LoadFriendsRec(string username, [FromBody] HashSet<string> seenusers, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                List<Account> res = new();
                var myacc = accounts.Find(s => s.username != username).FirstOrDefault();
                var accs = accounts.Find(s => s.username != username && !myacc.friendlist.Contains(s.username) && !seenusers.Contains(s.username)).Limit(100).ToList();
                foreach (var acc in accs)
                {
                    seenusers.Add(acc.username);
                    Account account = new() { username = acc.username, avatar = encryptService.Compress(acc.avatar) };
                    res.Add(account);
                }
                return new RecResult() { recommendation = res, seenusers = seenusers };
            }
            return null;
        }

        [HttpPost("sendcode/{username}")]
        public async Task SendVerificationEmailAsync(string username, [FromBody] string email)
        {
            if (memoryService.TimerReset.ContainsKey(username))
            {
                memoryService.TimerReset[username].Enabled = false;
                memoryService.TimerReset.Remove(username);
            }
            if (memoryService.ResetAccounts.ContainsKey(username))
            {
                memoryService.ResetAccounts.Remove(username);
            }
            await memoryService.SendEmailAsync(username, email);
        }

        [HttpPost("verify/{username}")]
        public SignResult VerifyCode(string username, [FromBody] int code)
        {
            if (memoryService.ResetAccounts.ContainsKey(username))
            {
                if (code == memoryService.ResetAccounts[username])
                {
                    memoryService.ResetAccounts.Remove(username);
                    memoryService.TimerReset[username].Enabled = false;
                    memoryService.TimerReset[username].Close();
                    memoryService.TimerReset.Remove(username);
                    return new SignResult() { success = true };
                }
                return new SignResult() { success = false, err = "Your verification code is incorrect" };
            }
            return new SignResult() { success = false, err = "Your verification code has expired" };
        }

        [HttpPost("verify-signup")]
        public async Task<string> SignUpAfterVerify([FromBody] Account acc)
        {
            var filter = Builders<Account>.Filter.Eq("_id", acc.username);
            var update = Builders<Account>.Update.Set("online", true);
            acc.password = encryptService.Encrypt(acc.password);
            var task = walls.CreateCollectionAsync(acc.username);
            var task2 = accounts.UpdateOneAsync(filter, update);
            var task3 = accounts.InsertOneAsync(acc);
            memoryService.OnlineTrack[acc.username] = new(600000);
            memoryService.OnlineTrack[acc.username].Elapsed += (s, e) =>
            {
                var filter = Builders<Account>.Filter.Eq("_id", acc.username);
                var update = Builders<Account>.Update.Set("online", false);
                accounts.UpdateOne(filter, update);
                memoryService.OnlineTrack[acc.username].Stop();
                memoryService.OnlineTrack.Remove(acc.username);
                memoryService.OnlineTrack[acc.username].Close();
            };
            memoryService.OnlineTrack[acc.username].Enabled = true;
            await task;
            await task2;
            await task3;
            var accesstoken = ObjectId.GenerateNewId().ToString();
            memoryService.AccessTokens[acc.username] = accesstoken;
            memoryService.CreateSessionTracker(acc.username);
            return accesstoken;
        }

        [HttpPost("signin")]
        public async Task<SignResult> SignIn([FromBody] Account acc)
        {
            var filter = Builders<Account>.Filter.Eq("_id", acc.username);
            var update = Builders<Account>.Update.Set("online", true);
            var myacc = accounts.Find(filter).FirstOrDefault();
            if (myacc is not null)
            {
                if (encryptService.Decrypt(myacc.password) == acc.password)
                {
                    var task = accounts.UpdateOneAsync(filter, update);
                    memoryService.OnlineTrack[acc.username] = new(600000);
                    memoryService.OnlineTrack[acc.username].Elapsed += (s, e) =>
                    {
                        var filter = Builders<Account>.Filter.Eq("_id", acc.username);
                        var update = Builders<Account>.Update.Set("online", false);
                        accounts.UpdateOne(filter, update);
                        memoryService.OnlineTrack[acc.username].Stop();
                        memoryService.OnlineTrack.Remove(acc.username);
                        memoryService.OnlineTrack[acc.username].Close();
                    };
                    memoryService.OnlineTrack[acc.username].Enabled = true;
                    await task;
                    string accessToken;
                    if (memoryService.AccessTokens.ContainsKey(acc.username))
                    {
                        accessToken = memoryService.AccessTokens[acc.username];
                    }
                    else
                    {
                        accessToken = ObjectId.GenerateNewId().ToString();
                        memoryService.AccessTokens[acc.username] = accessToken;
                    }
                    memoryService.CreateSessionTracker(acc.username);
                    return new SignResult() { success = true, avatar = encryptService.Compress(myacc.avatar), cover = encryptService.Compress(myacc.cover), bio = myacc.bio, newnotis_count = myacc.newnotis.Count, waitinglist_count = myacc.waitinglist.Count, access_token = accessToken };
                }
                return new SignResult() { success = false, err = "Incorrect password" };
            }
            return new SignResult() { success = false, err = "Incorrect username" };
        }

        [HttpPost("signup")]
        public async Task<SignResult> SignUp([FromBody] Account acc)
        {
            var filter = Builders<Account>.Filter.Eq("_id", acc.username);
            var myacc = accounts.Find(filter).FirstOrDefault();
            if (myacc is not null)
            {
                return new SignResult() { success = false, err = "Your username has been taken" };
            }
            if (memoryService.TimerReset.ContainsKey(acc.username))
            {
                memoryService.TimerReset[acc.username].Enabled = false;
                memoryService.TimerReset.Remove(acc.username);
            }
            if (memoryService.ResetAccounts.ContainsKey(acc.username))
            {
                memoryService.ResetAccounts.Remove(acc.username);
            }
            await memoryService.SendEmailAsync(acc.username, acc.email);
            return new SignResult() { success = true };
        }

        [HttpPut("connection/{username}")]
        public void UpdateConnection(string username, [FromBody] bool connected, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                if (connected)
                {
                    memoryService.CreateSessionTracker(username);
                    if (!memoryService.OnlineTrack.ContainsKey(username))
                    {
                        var filter = Builders<Account>.Filter.Eq("_id", username);
                        var update = Builders<Account>.Update.Set("online", true);
                        accounts.UpdateOne(filter, update);
                        memoryService.OnlineTrack[username] = new(600000);
                        memoryService.OnlineTrack[username].Elapsed += (s, e) =>
                        {
                            var filter = Builders<Account>.Filter.Eq("_id", username);
                            var update = Builders<Account>.Update.Set("online", false);
                            accounts.UpdateOne(filter, update);
                            memoryService.OnlineTrack[username].Stop();
                            memoryService.OnlineTrack.Remove(username);
                            memoryService.OnlineTrack[username].Close();
                        };
                        memoryService.OnlineTrack[username].Enabled = true;
                        return;
                    }
                    else
                    {
                        memoryService.OnlineTrack[username].Stop();
                        memoryService.OnlineTrack[username].Close();
                        memoryService.OnlineTrack[username] = new(600000);
                        memoryService.OnlineTrack[username].Elapsed += (s, e) =>
                        {
                            var filter = Builders<Account>.Filter.Eq("_id", username);
                            var update = Builders<Account>.Update.Set("online", false);
                            accounts.UpdateOne(filter, update);
                            memoryService.OnlineTrack[username].Stop();
                            memoryService.OnlineTrack.Remove(username);
                            memoryService.OnlineTrack[username].Close();
                        };
                        memoryService.OnlineTrack[username].Enabled = true;
                        return;
                    }
                }
                else
                {
                    var filter = Builders<Account>.Filter.Eq("_id", username);
                    var update = Builders<Account>.Update.Set("online", false);
                    accounts.UpdateOne(filter, update);
                    memoryService.OnlineTrack[username].Stop();
                    memoryService.OnlineTrack[username].Close();
                    memoryService.OnlineTrack.Remove(username);
                    return;
                }
            }
        }

        [HttpPut("send/{username}")]
        public void SendFriendRequest(string username, [FromBody] string newfriend, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.AddToSet("waitinglist", newfriend);
                accounts.UpdateOne(filter, update);
            }
        }

        [HttpPut("accept/{username}")]
        public void AcceptFriendRequest(string username, [FromBody] string newfriend, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var myacc = accounts.Find(filter).FirstOrDefault();
                myacc.friendlist.Add(newfriend);
                myacc.waitinglist.Remove(newfriend);
                accounts.FindOneAndReplace(filter, myacc);
            }
        }

        [HttpPut("bio/{username}")]
        public void UpdateBio(string username, [FromBody] string bio, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.Set("bio", bio);
                accounts.UpdateOne(filter, update);
            }
        }

        [HttpPut("password/{username}")]
        public void UpdatePassword(string username, [FromBody] string newpassword, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.Set("password", encryptService.Encrypt(newpassword));
                accounts.UpdateOne(filter, update);
            }
        }

        [HttpPut("avatar/{username}")]
        public void UpdateAvatar(string username, [FromBody]byte[] avatar, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                avatar = Encoding.UTF8.GetBytes(encryptService.Encrypt(avatar.ToString()));
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.Set("avatar", avatar);
                accounts.UpdateOne(filter, update);
            }   
        }

        [HttpPut("cover/{username}")]
        public void UpdateCover(string username, [FromBody] byte[] cover, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken) 
            {
                cover = Encoding.UTF8.GetBytes(encryptService.Encrypt(cover.ToString()));
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.Set("cover", cover);
                accounts.UpdateOne(filter, update);
            }
        }

        [HttpDelete("{username}")]
        public async Task Delete(string username)
        {
            var task = walls.DropCollectionAsync(username);
            var filter = Builders<Account>.Filter.Eq("_id", username);
            accounts.DeleteOne(filter);
            await task;
        }
    }
}
