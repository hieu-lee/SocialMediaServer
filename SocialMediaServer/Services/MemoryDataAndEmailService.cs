using FluentEmail.Core;
using FluentEmail.Razor;
using FluentEmail.Smtp;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace SocialMediaServer.Services
{
    public class MemoryDataAndEmailService
    {
        public Dictionary<string, int> ResetAccounts { get; set; } = new();
        public Dictionary<string, Timer> TimerReset { get; set; } = new();
        public Dictionary<string, Timer> OnlineTrack { get; set; } = new();
        public Dictionary<string, string> AccessTokens { get; set; } = new();
        public Dictionary<string, DateTime> SessionTracker { get; set; } = new();
        public HashSet<string> VerifiedUsers { get; set; } = new();
        public Timer SessionTimer = new(86400000);

        public MemoryDataAndEmailService()
        {
            SessionTimer.Elapsed += (s, e) =>
            {
                Parallel.ForEach(SessionTracker.Keys, username =>
                {
                    if (SessionTracker[username].AddDays(1) > DateTime.Now)
                    {
                        SessionTracker.Remove(username);
                        AccessTokens.Remove(username);
                    }
                });
            };
            SessionTimer.AutoReset = true;
            SessionTimer.Enabled = true;
        }

        public void CreateSessionTracker(string username)
        {
            SessionTracker[username] = DateTime.Now;
        }

        public async Task SendEmailAsync(string username, string mail)
        {
            var code = new Random().Next(100000, 999999);
            var login = new NetworkCredential() { UserName = "bobleechatroom@gmail.com", Password = "Bob@2002" };
            var sender = new SmtpSender(() => new SmtpClient()
            {
                Host = "smtp.gmail.com",
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Port = 587,
                UseDefaultCredentials = false,
                Credentials = login
            });

            StringBuilder template = new();
            template.AppendLine("Dear @Model.FirstName,");
            template.AppendLine("<p>Welcome to @Model.SocialMedia. @Model.Code is your verification code, the code will be expired after 5 minutes.</p>");
            template.AppendLine("- Bob Lee");

            Email.DefaultSender = sender;
            Email.DefaultRenderer = new RazorRenderer();

            await Email
            .From("bobleechatroom@gmail.com")
            .To(mail, username)
            .Subject("Verification Code")
            .UsingTemplate(template.ToString(), new { FirstName = username, SocialMedia = "Bornpook", Code = code.ToString() })
            .SendAsync();
            ResetAccounts.Add(username, code);
            Timer timer = new(300000);
            timer.Elapsed += (s, e) =>
            {
                ResetAccounts.Remove(username);
                timer.Enabled = false;
                timer.Close();
                TimerReset.Remove(username);
            };
            timer.Enabled = true;
            TimerReset[username] = timer;
        }

        public async Task AddNotificationAsync(IMongoCollection<Account> accounts, string username, string relateuser, string type, Dictionary<string, Comment> comments = null)
        {
            if (type != "comment")
            {
                var filter = Builders<Account>.Filter.Eq("_id", relateuser);
                var relate = accounts.Find(filter).FirstOrDefault();
                relate.newnotis.Add(new Notification() { username = username, type = type, time = DateTime.Now });
                var update = Builders<Account>.Update.Set("newnotis", relate.newnotis);
                await accounts.UpdateOneAsync(filter, update);
                return;
            }
            var time = DateTime.Now;
            var acc = accounts.Find(s => s.username == username).FirstOrDefault();
            var relatefriends = new HashSet<string>();
            relatefriends.Add(relateuser);
            foreach (Comment cmt in comments.Values)
            {
                if (acc.friendlist.Contains(cmt.username))
                {
                    relatefriends.Add(cmt.username);
                }
            }
            var tasks = new HashSet<Task>();
            Parallel.ForEach(relatefriends, fr =>
            {
                var filter = Builders<Account>.Filter.Eq("_id", fr);
                var relate = accounts.Find(filter).FirstOrDefault();
                relate.newnotis.Add(new Notification() { username = username, type = "comment", time = time });
                var update = Builders<Account>.Update.Set("newnotis", relate.newnotis);
                var t = accounts.UpdateOneAsync(filter, update);
                tasks.Add(t);
            });
            foreach (var t in tasks)
            {
                await t;
            }
        }
    }
}
