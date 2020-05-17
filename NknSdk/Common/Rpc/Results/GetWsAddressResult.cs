using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

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
