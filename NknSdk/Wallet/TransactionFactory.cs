using NknSdk.Common;
using NknSdk.Common.Extensions;
using NknSdk.Common.Protobuf;
using NknSdk.Common.Protobuf.Transaction;
using NknSdk.Wallet.Models;

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

            return TransactionFactory.MakePayload(transfer, PayloadType.TransferAsset);
        }

        public static Payload MakeRegisterNamePayload(string registrant, string name, long registrationFee)
        {
            var registerName = new RegisterName
            {
                Registrant = registrant.FromHexString(),
                Name = name,
                RegistrationFee = registrationFee
            };

            return TransactionFactory.MakePayload(registerName, PayloadType.RegisterName);
        }

        public static Payload MakeTransferNamePayload(string name, string registrant, string recipient)
        {
            var transferName = new TransferName
            {
                Name = name,
                Recipient = recipient.FromHexString(),
                Registrant = registrant.FromHexString()
            };

            return TransactionFactory.MakePayload(transferName, PayloadType.TransferName);
        }

        public static Payload MakeDeleteNamePayload(string registrant, string name)
        {
            var deleteName = new DeleteName
            {
                Name = name,
                Registrant = registrant.FromHexString()
            };

            return TransactionFactory.MakePayload(deleteName, PayloadType.DeleteName);
        }

        public static Payload MakeSubscribePayload(
            string subscriber,
            string identifier,
            string topic,
            int duration,
            string meta)
        {
            var subscribe = new Subscribe
            {
                Subscriber = subscriber.FromHexString(),
                Identifier = identifier,
                Topic = topic,
                Duration = duration,
                Meta = meta
            };

            return TransactionFactory.MakePayload(subscribe, PayloadType.Subscribe);
        }

        public static Payload MakeUnsubscribePayload(
            string subscriber,
            string identifier,
            string topic)
        {
            var unsubscribe = new Unsubscribe
            {
                Identifier = identifier,
                Subscriber = subscriber.FromHexString(),
                Topic = topic
            };

            return TransactionFactory.MakePayload(unsubscribe, PayloadType.Unsubscribe);
        }

        public static Payload MakeNanoPayPayload(
            string sender,
            string recipient,
            long id,
            long amount,
            int nanoPayExpiration,
            int transactionExpiration)
        {
            var nanoPay = new NanoPay
            {
                Sender = sender.FromHexString(),
                Recipient = recipient.FromHexString(),
                Id =  (ulong)id,
                NanoPayExpiration = nanoPayExpiration,
                TransactionExpiration = transactionExpiration,
                Amount = amount
            };

            return TransactionFactory.MakePayload(nanoPay, PayloadType.NanoPay);
        }

        public static Transaction MakeTransaction(
            Account account,
            Payload payload,
            long nonce,
            long fee = 0,
            string attributes = "")
        {
            var unsigned = new UnsignedTransaction
            {
                Payload = payload,
                Nonce = (ulong)nonce,
                Fee =  fee,
                Attributes = attributes.FromHexString()
            };

            var transaction = new Transaction
            {
                UnsignedTransaction = unsigned
            };

            TransactionFactory.SignTransaction(account, transaction);

            return transaction;
        }

        public static void SignTransaction(Account account, Transaction transaction)
        {
            var hex = transaction.UnsignedTransaction.EncodeHex();
            transaction.Hash = Hash.DoubleSha256(hex);

            var digest = Hash.Sha256Hex(hex);
            var signature = account.Sign(digest.FromHexString());

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
