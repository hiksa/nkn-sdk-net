using Ncp;

namespace NknSdk.Client
{
    public class MultiClientOptions : ClientOptions
    {
        public MultiClientOptions()
        {
            this.NumberOfSubClients = 4;
            this.OriginalClient = false;
            this.SessionConfiguration = new SessionConfiguration();
        }

        public int NumberOfSubClients { get; set; }

        public bool OriginalClient { get; set; }

        public int MessageCacheExpiration { get; set; }

        public SessionConfiguration SessionConfiguration { get; set; }    
    }
}
