using System.Runtime.Serialization;

namespace NknSdk.Client.Messages
{
    public class SetClientMessage : Message
    {
        public SigChainBlockHashResult Result { get; set; }

        public class SigChainBlockHashResult
        {
            [DataMember(Name = "node")]
            public NodeResult Node { get; set; }

            [DataMember(Name = "sigChainBlockHash")]
            public string SigChainBlockHash { get; set; }

            public class NodeResult
            {
                [DataMember(Name = "addr")]
                public string Address { get; set; }

                [DataMember(Name = "id")]
                public string Id { get; set; }

                [DataMember(Name = "pubkey")]
                public string PublicKey { get; set; }
            }
        }
    }
}
