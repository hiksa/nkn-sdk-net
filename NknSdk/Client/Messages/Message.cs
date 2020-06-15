using System.Runtime.Serialization;

namespace NknSdk.Client.Messages
{
    public class Message
    {
        public string Action { get; set; }

        [DataMember(Name = "Desc")]
        public string Description { get; set; }

        public int Error { get; set; }

        public int Version { get; set; }

        public bool HasError => this.Error != 0;
    }
}
