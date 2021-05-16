using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SocialMediaServer.Validation;
using SocialMediaServer.Services;
using MongoDB.Bson;
using Microsoft.AspNetCore.Cors;
using System.Threading;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace SocialMediaServer.Controllers
{
    [EnableCors("MyPolicy")]
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
        public Account SearchByUsername(string username)
        {
            var acc = accounts.Find(s => s.username == username).FirstOrDefault();
            if (acc is not null)
            {
                return new Account() { username = acc.username, avatar = encryptService.Compress(acc.avatar) };
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
        public byte[] GetMyAvatar(string username)
        {
            var filter = Builders<Account>.Filter.Eq("_id", username);
            var acc = accounts.Find(filter).FirstOrDefault();
            var avatar = Encoding.UTF8.GetBytes(encryptService.Decrypt(Encoding.UTF8.GetString(acc.avatar)));
            return avatar;
        }

        [HttpGet("{username}/cover")]
        public byte[] GetMyCover(string username)
        {
            var filter = Builders<Account>.Filter.Eq("_id", username);
            var acc = accounts.Find(filter).FirstOrDefault();
            var cover = Encoding.UTF8.GetBytes(encryptService.Decrypt(Encoding.UTF8.GetString(acc.cover)));
            return cover;
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
                Thread t = new(() => { accounts.FindOneAndReplace(filter, acc); });
                t.Start();
                var task = Task.Factory.StartNew(() => { t.Join(); });
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
                    memoryService.VerifiedUsers.Add(username);
                    return new SignResult() { success = true };
                }
                return new SignResult() { success = false, err = "Your verification code is incorrect" };
            }
            return new SignResult() { success = false, err = "Your verification code has expired" };
        }

        [HttpPost("verify-signup/{code:int}")]
        public async Task<IActionResult> SignUpAfterVerify(int code, [FromBody] Account acc)
        {
            if (memoryService.ResetAccounts.ContainsKey(acc.username))
            {
                if (code == memoryService.ResetAccounts[acc.username])
                {
                    memoryService.ResetAccounts.Remove(acc.username);
                    memoryService.TimerReset[acc.username].Enabled = false;
                    memoryService.TimerReset[acc.username].Close();
                    memoryService.TimerReset.Remove(acc.username);
                    memoryService.VerifiedUsers.Add(acc.username);
                }
                else
                {
                    return StatusCode(403, "Your verification code is incorrect");
                }
            }
            else
            {
                return NotFound("Your verification code has expired");
            }
            if (memoryService.VerifiedUsers.Contains(acc.username))
            {
                var filter = Builders<Account>.Filter.Eq("_id", acc.username);
                var update = Builders<Account>.Update.Set("online", true);
                acc.password = encryptService.Encrypt(acc.password);
                Thread t = new(() => { walls.CreateCollection(acc.username); });
                t.Start();
                Thread t2 = new(() => { accounts.UpdateOne(filter, update); });
                t2.Start();
                Thread t3 = new(() => { accounts.InsertOne(acc); });
                t3.Start();
                var task = Task.Factory.StartNew(() =>
                {
                    t.Join();
                    t2.Join();
                    t3.Join();
                });
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
                var accesstoken = ObjectId.GenerateNewId().ToString();
                memoryService.AccessTokens[acc.username] = accesstoken;
                memoryService.CreateSessionTracker(acc.username);
                memoryService.VerifiedUsers.Remove(acc.username);
                await task;
                return Ok(accesstoken);
            }
            return Unauthorized("Your account hasn't been verified");

        }

        [HttpPost("signin")]
        public async Task<SignResult> SignIn([FromBody] Account acc)
        {
            var filter = Builders<Account>.Filter.Eq("_id", acc.username);
            var update = Builders<Account>.Update.Set("online", true);
            var myacc = await accounts.Find(filter).FirstOrDefaultAsync();
            if (myacc is not null)
            {
                if (encryptService.Decrypt(myacc.password) == acc.password)
                {
                    Thread t = new(() => { accounts.UpdateOne(filter, update); });
                    t.Start();
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
                    t.Join();
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
            var myacc = await accounts.Find(filter).FirstOrDefaultAsync();
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

        [HttpPut("update-account")]
        public async Task<IActionResult> UpdateAccount([FromHeader] string AccessToken, [FromBody] Account account)
        {
            if (memoryService.AccessTokens[account.username] == AccessToken)
            {
                var filter = Builders<Account>.Filter.Eq("_id", account.username);
                await accounts.FindOneAndReplaceAsync(filter, account);
                return Ok("Update complete");
            }
            return Unauthorized("Invalid AccessToken");
        }

        [HttpPut("connection/{username}")]
        public async Task<IActionResult> UpdateConnection(string username, [FromBody] bool connected, [FromHeader] string AccessToken)
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
                        memoryService.OnlineTrack[username].Elapsed += async (s, e) =>
                        {
                            var filter = Builders<Account>.Filter.Eq("_id", username);
                            var update = Builders<Account>.Update.Set("online", false);
                            await accounts.UpdateOneAsync(filter, update);
                            memoryService.OnlineTrack[username].Stop();
                            memoryService.OnlineTrack.Remove(username);
                            memoryService.OnlineTrack[username].Close();
                        };
                        memoryService.OnlineTrack[username].Enabled = true;
                        return Ok();
                    }
                    else
                    {
                        memoryService.OnlineTrack[username].Stop();
                        memoryService.OnlineTrack[username].Close();
                        memoryService.OnlineTrack[username] = new(600000);
                        memoryService.OnlineTrack[username].Elapsed += async (s, e) =>
                        {
                            var filter = Builders<Account>.Filter.Eq("_id", username);
                            var update = Builders<Account>.Update.Set("online", false);
                            await accounts.UpdateOneAsync(filter, update);
                            memoryService.OnlineTrack[username].Stop();
                            memoryService.OnlineTrack.Remove(username);
                            memoryService.OnlineTrack[username].Close();
                        };
                        memoryService.OnlineTrack[username].Enabled = true;
                        return Ok();
                    }
                }
                else
                {
                    var filter = Builders<Account>.Filter.Eq("_id", username);
                    var update = Builders<Account>.Update.Set("online", false);
                    await accounts.UpdateOneAsync(filter, update);
                    memoryService.OnlineTrack[username].Stop();
                    memoryService.OnlineTrack[username].Close();
                    memoryService.OnlineTrack.Remove(username);
                    return Ok();
                }
            }
            return Unauthorized("Invaid AccessToken");
        }

        [HttpPut("send/{username}")]
        public async Task<IActionResult> SendFriendRequest(string username, [FromBody] string newfriend, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken)
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.AddToSet("waitinglist", newfriend);
                await accounts.UpdateOneAsync(filter, update);
                return Ok();
            }
            return Unauthorized("Invalid AccessToken");
        }

        [HttpPut("accept/{username}")]
        public async Task<IActionResult> AcceptFriendRequest(string username, [FromBody] string newfriend, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken)
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var myacc = await accounts.Find(filter).FirstOrDefaultAsync();
                myacc.friendlist.Add(newfriend);
                myacc.waitinglist.Remove(newfriend);
                await accounts.FindOneAndReplaceAsync(filter, myacc);
                return Ok();
            }
            return Unauthorized("Invalid AccessToken");
        }

        [HttpPut("bio/{username}")]
        public async Task<IActionResult> UpdateBio(string username, [FromBody] string bio, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken)
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.Set("bio", bio);
                await accounts.UpdateOneAsync(filter, update);
                return Ok();
            }
            return Unauthorized("Invalid AccessToken");
        }

        [HttpPut("password/{username}")]
        public async Task<IActionResult> UpdatePassword(string username, [FromBody] string newpassword, [FromHeader] string AccessToken)
        {
            if (memoryService.VerifiedUsers.Contains(username) && memoryService.AccessTokens[username] == AccessToken)
            {
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.Set("password", encryptService.Encrypt(newpassword));
                await accounts.UpdateOneAsync(filter, update);
                memoryService.VerifiedUsers.Remove(username);
                return Ok("Your password has been updated");
            }
            return Unauthorized("You haven't been verified");
        }

        [HttpPut("avatar/{username}")]
        public async Task<IActionResult> UpdateAvatar(string username, [FromBody] byte[] avatar, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken)
            {
                avatar = Encoding.UTF8.GetBytes(encryptService.Encrypt(avatar.ToString()));
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.Set("avatar", avatar);
                await accounts.UpdateOneAsync(filter, update);
                return Ok();
            }
            return Unauthorized("Invalid AccessToken");
        }

        [HttpPut("cover/{username}")]
        public async Task<IActionResult> UpdateCover(string username, [FromBody] byte[] cover, [FromHeader] string AccessToken)
        {
            if (memoryService.AccessTokens[username] == AccessToken)
            {
                cover = Encoding.UTF8.GetBytes(encryptService.Encrypt(cover.ToString()));
                var filter = Builders<Account>.Filter.Eq("_id", username);
                var update = Builders<Account>.Update.Set("cover", cover);
                await accounts.UpdateOneAsync(filter, update);
                return Ok();
            }
            return Unauthorized("Invalid AccessToken");
        }

        [HttpDelete("{username}")]
        public async Task Delete(string username)
        {
            Thread t = new(() => { walls.DropCollectionAsync(username); });
            t.Start();
            var task = Task.Factory.StartNew(() => { t.Join(); });
            var filter = Builders<Account>.Filter.Eq("_id", username);
            accounts.DeleteOne(filter);
            if (memoryService.AccessTokens.ContainsKey(username))
            {
                memoryService.AccessTokens.Remove(username);
            }
            if (memoryService.ResetAccounts.ContainsKey(username))
            {
                memoryService.ResetAccounts.Remove(username);
            }
            if (memoryService.SessionTracker.ContainsKey(username))
            {
                memoryService.SessionTracker.Remove(username);
            }
            if (memoryService.TimerReset.ContainsKey(username))
            {
                memoryService.TimerReset.Remove(username);
            }
            await task;
        }
    }
}
