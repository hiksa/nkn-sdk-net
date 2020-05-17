using System.Text;

using NknSdk.Common;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Transaction;

namespace NknSdk.Wallet
{
    public class TransactionFactory
    {
        public static Payload MakeTransferPayload(string sender, string recipient, long amount)
        {
            var transfer = new TransferAsset
            {
                Sender = sender.FromHexString(),
                Recipient = recipient.FromHexString(),
                Amount = amount
            };

            return MakePayload(transfer, PayloadType.TransferAsset);
        }

        public static Payload MakeRegisterNamePayload(string registrant, string name, long registrationFee)
        {
            var registerName = new RegisterName
            {
                Registrant = registrant.FromHexString(),
                Name = name,
                RegistrationFee = registrationFee
            };

            return MakePayload(registerName, PayloadType.RegisterName);
        }

        public static Payload MakeTransferNamePayload(string name, string registrant, string recipient)
        {
            var transferName = new TransferName
            {
                Name = name,
                Recipient = recipient.FromHexString(),
                Registrant = registrant.FromHexString()
            };

            return MakePayload(transferName, PayloadType.TransferName);
        }

        public static Payload MakeDeleteNamePayload(string registrant, string name)
        {
            var deleteName = new DeleteName
            {
                Name = name,
                Registrant = registrant.FromHexString()
            };

            return MakePayload(deleteName, PayloadType.DeleteName);
        }

        public static Payload MakeSubscribePayload(string subscriber, string identifier, string topic, int duration, string meta)
        {
            var subscribe = new Subscribe
            {
                Subscriber = subscriber.FromHexString(),
                Identifier = identifier,
                Topic = topic,
                Duration = duration,
                Meta = meta
            };

            return MakePayload(subscribe, PayloadType.Subscribe);
        }

        public static Payload MakeUnsubscribePayload(string subscriber, string identifier, string topic)
        {
            var unsubscribe = new Unsubscribe
            {
                Identifier = identifier,
                Subscriber = subscriber.FromHexString(),
                Topic = topic
            };

            return MakePayload(unsubscribe, PayloadType.Unsubscribe);
        }

        public static Payload MakeNanoPayPayload(string sender, string recipient, ulong id, int nanoPayExpiration, int transactionExpiration)
        {
            var nanoPay = new NanoPay
            {
                Sender = sender.FromHexString(),
                Recipient = recipient.FromHexString(),
                Id = id,
                NanoPayExpiration = nanoPayExpiration,
                TransactionExpiration = transactionExpiration,
                // TODO: Amount
            };

            return MakePayload(nanoPay, PayloadType.NanoPay);
        }

        public static Transaction MakeTransaction(Account account, Payload payload, ulong nonce, decimal fee = 0, string attributes = "")
        {
            var unsigned = new UnsignedTransaction
            {
                Payload = payload,
                Nonce = nonce,
                // TODO: ...
                Attributes = attributes.FromHexString()
            };

            var transaction = new Transaction
            {
                UnsignedTransaction = unsigned
            };

            SignTransaction(account, transaction);

            return transaction;
        }

        public static string SerializePayload(Payload payload)
        {
            var result = ((int)payload.Type).EncodeHex();
            result += payload.Data.EncodeHex();

            return result;
        }

        public static string SerializeUnsigned(UnsignedTransaction unsigned)
        {
            // TODO: ...
            var sb = new StringBuilder();
            sb.Append(SerializePayload(unsigned.Payload));
            sb.Append(unsigned.Nonce.EncodeHex());
            sb.Append(unsigned.Fee.EncodeHex());
            sb.Append(unsigned.Attributes.EncodeHex());

            return sb.ToString();
        }

        public static void SignTransaction(Account account, Transaction transaction)
        {
            // TODO: ...
            var hex = SerializeUnsigned(transaction.UnsignedTransaction);
            var digest = Crypto.Sha256Hex(hex);
            // TODO:
            var signature = account.Sign(digest.FromHexString());
            var txHash = Crypto.DoubleSha256(hex);
            transaction.Hash = txHash;

            var program = new Program
            {
                Code = account.SignatureRedeem.FromHexString(),
                Parameter = Address.SignatureToParameter(signature.ToHexString()).FromHexString()
            };

            transaction.Programs = new Program[] { program };
        }

        private static Payload MakePayload<T>(T data, PayloadType type) where T : class
        {
            return new Payload 
            { 
                Type = type, 
                Data = ProtoSerializer.Serialize(data) 
            };
        }
    }
}
