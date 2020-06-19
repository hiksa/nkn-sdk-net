using System.Threading.Tasks;

using Norgerman.Cryptography.Scrypt;
using Utf8Json;

using NknSdk.Common;
using NknSdk.Common.Protobuf.Transaction;
using NknSdk.Common.Exceptions;
using NknSdk.Common.Extensions;
using NknSdk.Common.Options;
using NknSdk.Common.Rpc;
using NknSdk.Common.Rpc.Results;
using NknSdk.Wallet.Models;

namespace NknSdk.Wallet
{
    public class Wallet : ITransactionSender
    {
        public const int NameRegistrationFee = 10;
        public const int DefaultVersion = 1;
        public const int MinCompatibleVersion = 1;
        public const int MaxCompatibleVersion = 2;

        private readonly int version;
        private readonly string programHash;
        private readonly string ivHex;
        private readonly string masterKey;
        private readonly string seedEncrypted;
        private readonly ScryptParams scryptParams;
        private readonly Account account;

        public Wallet(WalletOptions options = null)
        {
            options = options ?? new WalletOptions();

            this.version = options.Version ?? Wallet.DefaultVersion;

            switch (this.version)
            {
                case 2:
                    this.scryptParams = new ScryptParams();
                    this.scryptParams.Salt = this.scryptParams.Salt ?? PseudoRandom.RandomBytesAsHexString(this.scryptParams.SaltLength);
                    break;
                default:
                    break;
            }

            string passwordKey = default;
            if (options.PasswordKeys != null && options.PasswordKeys.ContainsKey(this.version))
            {
                passwordKey = options.PasswordKeys[this.version];
            }
            else
            {
                passwordKey = Wallet.ComputePasswordKey(new WalletOptions
                {
                    Version = this.version,
                    Password = options.Password,
                    Scrypt = this.scryptParams
                });
            }

            var account = new Account(options.SeedHex);

            var ivHex = options.Iv ?? PseudoRandom.RandomBytesAsHexString(16);
            var masterKeyHex = string.IsNullOrWhiteSpace(options.MasterKey) 
                ? PseudoRandom.RandomBytesAsHexString(16) 
                : options.MasterKey;

            this.Options = options;
            this.account = account;
            this.ivHex = ivHex;
            this.Address = this.account.Address;
            this.programHash = this.account.ProgramHash;

            this.masterKey = Aes.Encrypt(masterKeyHex, passwordKey, ivHex.FromHexString());
            this.seedEncrypted = Aes.Encrypt(options.SeedHex, masterKeyHex, ivHex.FromHexString());
            
            this.Options.Iv = null;
            this.Options.SeedHex = null;
            this.Options.Password = null;
            this.Options.MasterKey = null;
            this.Options.PasswordKey = null;
            this.Options.PasswordKeys.Clear();
        }

        public string Seed => this.account.Seed;

        public string PublicKey => this.account.PublicKey;

        public WalletOptions Options { get; }

        public string Address { get; }

        public static Wallet Decrypt(WalletJson wallet, WalletOptions options)
        {
            options.Iv = wallet.Iv;
            options.MasterKey = Aes.Decrypt(wallet.MasterKey, options.PasswordKey, options.Iv.FromHexString());
            options.SeedHex = Aes.Decrypt(wallet.SeedEncrypted, options.MasterKey, options.Iv.FromHexString());
            options.PasswordKeys.Add(wallet.Version.Value, options.PasswordKey);

            switch (wallet.Version)
            {
                case 2:

                    options.Scrypt = new ScryptParams
                    {
                        Salt = wallet.Scrypt.Salt,
                        N = wallet.Scrypt.N,
                        P = wallet.Scrypt.P,
                        R = wallet.Scrypt.R
                    };

                    break;

                default:
                    break;
            }

            var account = new Account(options.SeedHex);
            if (account.Address != wallet.Address)
            {
                throw new WrongPasswordException();
            }

            return new Wallet(options);
        }

        public static async Task<Wallet> FromJsonAsync(string json, WalletOptions options = null)
        {
            options = options ?? new WalletOptions();

            var wallet = WalletJson.FromJson(json);

            options.AssignFrom(wallet);

            var computeOptions = WalletOptions.FromWalletJson(wallet);
            computeOptions.Password = options.Password;

            var passwordKey = await Wallet.ComputePasswordKeyAsync(computeOptions);
            options.PasswordKey = passwordKey;

            return Wallet.Decrypt(wallet, options);
        }

        public static Wallet FromJson(string json, WalletOptions options = null)
        {
            options = options ?? new WalletOptions();

            var wallet = WalletJson.FromJson(json);

            options.AssignFrom(wallet);

            var computeOptions = WalletOptions.FromWalletJson(wallet);
            computeOptions.Password = options.Password;

            var passwordKey = Wallet.ComputePasswordKey(computeOptions);
            options.PasswordKey = passwordKey;

            return Wallet.Decrypt(wallet, options);
        }

        public static async Task<GetLatestBlockHashResult> GetLatestBlockAsync(WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            return await RpcClient.GetLatestBlockHash(options.RpcServerAddress);
        }

        public static async Task<GetRegistrantResult> GetRegistrantAsync(string name, WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            return await RpcClient.GetRegistrant(options.RpcServerAddress, name);
        }

        public static Task<GetSubscribersWithMetadataResult> GetSubscribersWithMetadataAsync(
            string topic,
            WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            return RpcClient.GetSubscribersWithMetadata(options.RpcServerAddress, topic, options.Offset, options.Limit, options.TxPool);
        }

        public static Task<GetSubscribersResult> GetSubscribersAsync(
            string topic,
            WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            return RpcClient.GetSubscribers(options.RpcServerAddress, topic, options.Offset, options.Limit, options.TxPool);
        }

        public static Task<int> GetSubscribersCountAsync(string topic, WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            return RpcClient.GetSubscribersCount(options.RpcServerAddress, topic);
        }

        public static Task<GetSubscriptionResult> GetSubscriptionAsync(
            string topic, 
            string subscriber, 
            WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            return RpcClient.GetSubscription(options.RpcServerAddress, topic, subscriber);
        }

        public static Task<GetBalanceResult> GetBalanceAsync(string address, WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            return RpcClient.GetBalanceByAddress(options.RpcServerAddress, address);
        }

        public static Task<GetNonceByAddrResult> GetNonceAsync(string address, WalletOptions options = null)
        {
            options = options ?? new WalletOptions();
            return RpcClient.GetNonceByAddress(options.RpcServerAddress, address);
        }

        public static Task<string> SendTransactionAsync(Transaction tx, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.SendRawTransaction(options.RpcServerAddress, tx);
        }

        public static bool VerifyAddress(string address) => Common.Address.Verify(address);

        public static string PublicKeyToAddress(string publicKey)
        {
            var signatureRedeem = Common.Address.PublicKeyToSignatureRedeem(publicKey);
            var programHash = Common.Address.HexStringToProgramHash(signatureRedeem);
            var result = Common.Address.FromProgramHash(programHash);

            return result;
        }

        public string ToJson()
        {
            var wallet = new WalletJson
            {
                Version = this.version,
                MasterKey = this.masterKey,
                Iv = this.ivHex,
                SeedEncrypted = this.seedEncrypted,
                Address = this.Address,
            };

            if (this.scryptParams != null)
            {
                wallet.Scrypt = new ScryptParams
                {
                    Salt = this.scryptParams.Salt,
                    N = this.scryptParams.N,
                    R = this.scryptParams.R,
                    P = this.scryptParams.P,
                };
            }

            var result = JsonSerializer.ToJsonString(wallet);

            return result;
        }

        public async Task<bool> VerifyPasswordAsync(string password)
        {
            var options = new WalletOptions
            {
                Version = this.version,
                Password = password,
                Scrypt = this.scryptParams
            };

            var passwordKey = await Wallet.ComputePasswordKeyAsync(options);

            return this.VerifyPasswordKey(passwordKey);
        }

        public bool VerifyPassword(string password)
        {
            var options = new WalletOptions
            {
                Version = this.version,
                Password = password,
                Scrypt = this.scryptParams                
            };

            var passwordKey = Wallet.ComputePasswordKey(options);

            return this.VerifyPasswordKey(passwordKey);
        }

        public Task<GetLatestBlockHashResult> GetLatestBlockAsync() => Wallet.GetLatestBlockAsync(this.Options);

        public Task<GetRegistrantResult> GetRegistrantAsync(string name) => Wallet.GetRegistrantAsync(name, this.Options);

        public Task<GetSubscribersWithMetadataResult> GetSubscribersWithMetadataAsync(string topic)
        {
            return Wallet.GetSubscribersWithMetadataAsync(topic, this.Options);
        }

        public Task<GetSubscribersResult> GetSubscribersAsync(string topic)
        {
            return Wallet.GetSubscribersAsync(topic, this.Options);
        }

        public Task<int> GetSubscribersCountAsync(string topic) => Wallet.GetSubscribersCountAsync(topic, this.Options);

        public Task<GetSubscriptionResult> GetSubscriptionAsync(string topic, string subscriber)
            => RpcClient.GetSubscription(this.Options.RpcServerAddress, topic, subscriber);

        public Task<GetBalanceResult> GetBalanceAsync(string address = "")
        {
            var addr = string.IsNullOrEmpty(address) ? this.Address : address;

            return Wallet.GetBalanceAsync(addr, this.Options);
        }

        public Task<GetNonceByAddrResult> GetNonceAsync() => Wallet.GetNonceAsync(this.account.Address, this.Options);     

        public Task<string> SendTransactionAsync(Transaction tx) => Wallet.SendTransactionAsync(tx, TransactionOptions.NewFrom(this.Options));

        public Task<string> TransferToAsync(string toAddress, decimal amount, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.TransferTo(toAddress, new Amount(amount), this, options);
        }

        public Task<string> RegisterNameAsync(string name, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.RegisterName(name, this, options);
        }

        public Task<string> TransferNameAsync(string name, string recipient, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.TransferName(name, recipient, this, options);
        }

        public Task<string> DeleteNameAsync(string name, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.DeleteName(name, this, options);
        }

        public Task<string> SubscribeAsync(string topic, int duration, string identifier, string meta, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.Subscribe(topic, duration, identifier, meta, this, options);
        }

        public Task<string> UnsubscribeAsync(string topic, string identifier, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();
            return RpcClient.Unsubscribe(topic, identifier, this, options);
        }

        public Transaction CreateOrUpdateNanoPay(string toAddress, decimal amount, int expiration, long? id, TransactionOptions options = null)
        {
            options = options ?? new TransactionOptions();

            if (Common.Address.Verify(toAddress) == false)
            {
                throw new System.Exception();
            }

            id = id ?? PseudoRandom.RandomLong();

            var payload = TransactionFactory.MakeNanoPayPayload(
                this.programHash,
                Common.Address.ToProgramHash(toAddress),
                id.Value,
                new Amount(amount).Value,
                expiration,
                expiration);

            return this.CreateTransaction(payload, 0, options);
        }

        public Transaction CreateTransaction(Payload payload, long nonce, TransactionOptions options = null)
            => TransactionFactory.MakeTransaction(
                this.account, 
                payload, 
                nonce, 
                options.Fee.GetValueOrDefault(), 
                options.Attributes);

        private static async Task<string> ComputePasswordKeyAsync(WalletOptions options)
        {
            if (options.Version == null)
            {
                throw new System.Exception();
            }

            switch (options.Version.Value)
            {
                case 1: return await Task.Run(() => Hash.DoubleSha256(options.Password));

                case 2:

                    var scrypt = await Task.Run(() =>
                    {
                        return ScryptUtil
                            .Scrypt(
                                options.Password,
                                options.Scrypt.Salt.FromHexString(),
                                options.Scrypt.N,
                                options.Scrypt.R,
                                options.Scrypt.P,
                                32)
                            .ToHexString();
                    });

                    return scrypt;

                default: throw new InvalidWalletVersionException("unsupported wallet version " + options.Version);
            }
        }

        private static string ComputePasswordKey(WalletOptions options)
        {
            if (options.Version == null)
            {
                throw new System.Exception();
            }

            switch (options.Version.Value)
            {
                case 1: return Hash.DoubleSha256(options.Password);

                case 2:
                    var scrypt = ScryptUtil.Scrypt(
                        options.Password,
                        options.Scrypt.Salt.FromHexString(),
                        options.Scrypt.N,
                        options.Scrypt.R,
                        options.Scrypt.P,
                        32);

                    return scrypt.ToHexString();

                default: throw new System.Exception();
            }
        }

        private bool VerifyPasswordKey(string passwordKey)
        {
            var masterKey = Aes.Decrypt(this.masterKey, passwordKey, this.ivHex.FromHexString());
            var seed = Aes.Decrypt(this.seedEncrypted, masterKey, this.ivHex.FromHexString());

            var account = new Account(seed);

            return account.Address == this.Address;
        }
    }
}
