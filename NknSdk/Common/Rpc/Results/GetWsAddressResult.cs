using System.Runtime.Serialization;

namespace NknSdk.Common.Rpc.Results
{
    public class GetWsAddressResult
    {
        [DataMember(Name = "addr")]
        public string Address { get; set; }

        [DataMember(Name = "id")]
        public string Identifier { get; set; }

        [DataMember(Name = "pubkey")]
        public string Publickey { get; set; }
    }
}
