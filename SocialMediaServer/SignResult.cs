namespace SocialMediaServer
{
    public class SignResult
    {
        public bool success { get; set; }
        public string err { get; set; } = null;
        public byte[] avatar { get; set; }
        public byte[] cover { get; set; }
        public string bio { get; set; }
        public int newnotis_count { get; set; }
        public int waitinglist_count { get; set; }
        public string access_token { get; set; }
    }
}
