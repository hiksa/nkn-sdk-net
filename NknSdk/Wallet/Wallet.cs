using System.Threading.Tasks;

using Norgerman.Cryptography.Scrypt;
using Utf8Json;

using NknSdk.Common;
using NknSdk.Common.Protobuf.Transaction;
using NknSdk.Common.Rpc;
using NknSdk.Common.Exceptions;
using NknSdk.Common.Rpc.Results;
using NknSdk.Common.Extensions;
using NknSdk.Wallet.Models;

namespace NknSdk.Wallet
{
    public class Wallet
    {
        public const int NameRegistrationFee = 10;
        public const int DefaultVersion = 1;
        public const int MinCompatibleVersion = 1;
        public const int MaxCompatibleVersion = 2;

        private readonly int version;
        private readonly string address;
        private readonly string programHash;
        private readonly string iv;
        private readonly string masterKey;
        private readonly string seedEncrypted;
        private readonly ScryptParams scryptParams;
        private readonly WalletOptions options;
        private readonly Account account;

        public Wallet(WalletOptions options)
        {
            this.version = options.Version ?? Wallet.DefaultVersion;

            switch (this.version)
            {
                case 2:
                    this.scryptParams = new ScryptParams();
                    this.scryptParams.Salt ??= PseudoRandom.RandomBytesAsHexString(this.scryptParams.SaltLength);
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

            var iv = options.Iv ?? PseudoRandom.RandomBytesAsHexString(16);
            var masterKeyHex = string.IsNullOrWhiteSpace(options.MasterKey) 
                ? PseudoRandom.RandomBytesAsHexString(16) 
                : options.MasterKey;

            this.options = options;
            this.account = account;
            this.iv = iv;
            this.address = this.account.Address;
            this.programHash = this.account.ProgramHash;

            this.masterKey = Aes.Encrypt(masterKeyHex, passwordKey, iv.FromHexString());
            this.seedEncrypted = Aes.Encrypt(options.SeedHex, masterKeyHex, iv.FromHexString());
        }

        public string Seed => this.account.Seed;

        public string PublicKey => this.account.PublicKey;

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

        public static async Task<Wallet> FromJsonAsync(string json, WalletOptions options)
        {
            var wallet = ParseAndValidateWalletJson(json);

            var computeOptions = WalletOptions.FromWalletJson(wallet);
            computeOptions.Password = options.Password;

            var passwordKey = await Wallet.ComputePasswordKeyAsync(computeOptions);
            options.PasswordKey = passwordKey;

            return Wallet.Decrypt(wallet, options);
        }

        public static Wallet FromJson(string json, WalletOptions options)
        {
            var wallet = ParseAndValidateWalletJson(json);

            // TODO: ....
            options = WalletOptions.FromWalletJson(wallet);

            var computeOptions = WalletOptions.FromWalletJson(wallet);
            computeOptions.Password = options.Password;

            var passwordKey = Wallet.ComputePasswordKey(computeOptions);
            options.PasswordKey = passwordKey;

            return Wallet.Decrypt(wallet, options);
        }

        public static async Task<GetLatestBlockHashResult> GetLatestBlock(WalletOptions options)
        {
            return await RpcClient.GetLatestBlockHash(options.RpcServer);
        }

        public static async Task<GetRegistrantResult> GetRegistrantAsync(string name, WalletOptions options)
        {
            return await RpcClient.GetRegistrant(options.RpcServer, name);
        }

        public static Task<GetSubscribersResult> GetSubscribers(
            string topic,
            WalletOptions options,
            int offset = 0,
            int limit = 1000,
            bool meta = false,
            bool txPool = false)
        {
            return RpcClient.GetSubscribers(options.RpcServer, topic, offset, limit, meta, txPool);
        }

        public static Task<int> GetSubscribersCount(string topic, WalletOptions options)
        {
            return RpcClient.GetSubscribersCount(options.RpcServer, topic);
        }

        public static Task<GetSubscriptionResult> GetSubscription(string topic, string subscriber, WalletOptions options)
        {
            return RpcClient.GetSubscription(options.RpcServer, topic, subscriber);
        }

        public static Task<GetBalanceResult> GetBalance(string address, WalletOptions options)
        {
            return RpcClient.GetBalanceByAddress(options.RpcServer, address);
        }

        public static Task<GetNonceByAddrResult> GetNonce(string address, WalletOptions options)
            => RpcClient.GetNonceByAddress(options.RpcServer, address);

        public static Task<string> SendTransaction(WalletOptions options, Transaction tx)
            => RpcClient.SendRawTransaction(options.RpcServer, tx);

        public static bool AddressIsValid(string address) => Address.IsValid(address);

        public static string PublicKeyToAddress(string publicKey)
        {
            var signatureRedeem = Address.PublicKeyToSignatureRedeem(publicKey);
            var programHash = Address.HexStringToProgramHash(signatureRedeem);
            var result = Address.FromProgramHash(programHash);

            return result;
        }

        public string ToJson()
        {
            var wallet = new WalletJson
            {
                Version = this.version,
                MasterKey = this.masterKey,
                Iv = this.iv,
                SeedEncrypted = this.seedEncrypted,
                Address = this.address,
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

        public async Task<bool> IsCorrectPasswordAsync(string password)
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

        public bool IsCorrectPassword(string password)
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

        public Task<GetLatestBlockHashResult> GetLatestBlock() => Wallet.GetLatestBlock(this.options);

        public Task<GetRegistrantResult> GetRegistrantAsync(string name) => Wallet.GetRegistrantAsync(name, this.options);

        public Task<GetSubscribersResult> GetSubscribers(
            string topic,
            int offset = 0,
            int limit = 1000,
            bool meta = false,
            bool txPool = false)
        {
            return Wallet.GetSubscribers(topic, this.options, offset, limit, meta, txPool);
        }

        public Task<int> GetSubscribersCount(string topic) => Wallet.GetSubscribersCount(topic, this.options);

        public Task<GetSubscriptionResult> GetSubscription(string topic, string subscriber)
            => RpcClient.GetSubscription(this.options.RpcServer, topic, subscriber);

        public Task<GetNonceByAddrResult> GetNonceAsync() => Wallet.GetNonce(this.account.Address, this.options);     

        public Task<string> SendTransactionAsync(Transaction tx) 
            => Wallet.SendTransaction(this.options, tx);

        public Task<string> TransferTo(string toAddress, decimal amount, WalletOptions options)
            => RpcClient.TransferTo(toAddress, new Amount(amount).Value, this, options);

        public Task<string> RegisterName(string name, WalletOptions options) 
            => RpcClient.RegisterName(name, this, options);

        public Task<string> TransferName(string name, string recipient, WalletOptions options)
            => RpcClient.TransferName(name, recipient, this, options);

        public Task<string> DeleteName(string name, WalletOptions options)
            => RpcClient.DeleteName(name, this, options);

        public Task<string> Subscribe(string topic, int duration, string identifier, string meta, WalletOptions options)
            => RpcClient.Subscribe(topic, duration, identifier, meta, this, options);

        public Task<string> Unsubscribe(string topic, string identifier, WalletOptions options)
            => RpcClient.Unsubscribe(topic, identifier, this, options);

        public Transaction CreateOrUpdateNanoPay(string toAddress, decimal amount, int expiration, long? id, WalletOptions options)
        {
            if (Address.IsValid(toAddress) == false)
            {
                throw new System.Exception();
            }

            id ??= PseudoRandom.RandomLong();

            var payload = TransactionFactory.MakeNanoPayPayload(
                this.programHash,
                Address.ToProgramHash(toAddress),
                id.Value,
                new Amount(amount).Value,
                expiration,
                expiration);

            return this.CreateTransaction(payload, 0, options);
        }

        public Transaction CreateTransaction(Payload payload, ulong nonce, WalletOptions options)
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
            var masterKey = Aes.Decrypt(this.masterKey, passwordKey, this.iv.FromHexString());
            var seed = Aes.Decrypt(this.seedEncrypted, masterKey, this.iv.FromHexString());

            var account = new Account(seed);

            return account.Address == this.address;
        }

        private static WalletJson ParseAndValidateWalletJson(string json)
        {
            var wallet = JsonSerializer.Deserialize<WalletJson>(json);

            if (wallet.Version == null 
                || wallet.Version < Wallet.MinCompatibleVersion 
                || wallet.Version > Wallet.MaxCompatibleVersion)
            {
                throw new InvalidWalletVersionException(
                    $"Invalid wallet version {wallet.Version}. " +
                    $"Should be between {Wallet.MinCompatibleVersion} and {Wallet.MaxCompatibleVersion}");
            }

            if (string.IsNullOrWhiteSpace(wallet.MasterKey))
            {
                throw new InvalidWalletFormatException("Missing MasterKey property");
            }

            if (string.IsNullOrWhiteSpace(wallet.Iv))
            {
                throw new InvalidWalletFormatException("Missing Iv property");
            }

            if (string.IsNullOrWhiteSpace(wallet.SeedEncrypted))
            {
                throw new InvalidWalletFormatException("Missing SeedEncrypted property");
            }

            if (string.IsNullOrWhiteSpace(wallet.Address))
            {
                throw new InvalidWalletFormatException("Missing Address property");
            }

            return wallet;
        }
    }
}
