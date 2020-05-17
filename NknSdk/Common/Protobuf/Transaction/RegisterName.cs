﻿using ProtoBuf;

namespace NknSdk.Common.Protobuf.Transaction
{
    [ProtoContract]
    public class RegisterName
    {
        [ProtoMember(1)]
        public byte[] Registrant { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public long RegistrationFee { get; set; }
    }
}