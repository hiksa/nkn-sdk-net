using Ncp;

namespace NknSdk.Common.Options
{
    public class MultiClientOptions : ClientOptions
    {
        public MultiClientOptions()
        {
            this.NumberOfSubClients = 4;
            this.MessageCacheExpiration = 300 * 1000;
            this.OriginalClient = false;
            this.SessionOptions = new SessionOptions();
        }

        public int NumberOfSubClients { get; set; }

        public bool OriginalClient { get; set; }

        public int MessageCacheExpiration { get; set; }

        public SessionOptions SessionOptions { get; set; }    
    }
}
