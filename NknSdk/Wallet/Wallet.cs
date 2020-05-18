using System.Threading.Tasks;

using Utf8Json;

using NknSdk.Common;
using NknSdk.Common.Protobuf.Transaction;
using NknSdk.Common.Rpc;
using NknSdk.Common.Exceptions;

namespace NknSdk.Wallet
{
    public class Wallet
    {
        private readonly Account account;
        private readonly string passwordHash;
        private readonly string iv;
        private readonly WalletOptions options;
        private readonly string masterKey;
        private readonly string seedEncrypted;
        private readonly int version;

        public Wallet(WalletOptions options)
        {
            var account = new Account(options.SeedHex);

            var passwordHash = Crypto.DoubleSha256(options.Password);
            var iv = options.Iv?.ToHexString() ?? PseudoRandom.RandomBytesAsHexString(16);
            var masterKey = options.MasterKey?.ToHexString() ?? PseudoRandom.RandomBytesAsHexString(16);

            this.options = options;
            this.account = account;
            this.passwordHash = Crypto.Sha256Hex(passwordHash);
            this.iv = iv;
            this.masterKey = Aes.Encrypt(masterKey, passwordHash, iv.FromHexString());
            this.seedEncrypted = Aes.Encrypt(options.SeedHex, masterKey, iv.FromHexString());

            this.version = Constants.Version;
        }

        public string Seed => this.account.Seed;

        public string PublicKey => this.account.PublicKey;

        public static Wallet FromJson(string json, WalletOptions options)
        {
            var wallet = JsonSerializer.Deserialize<WalletJson>(json);
            var passwordHash = Crypto.DoubleSha256(options.Password);
            var iv = wallet.Iv.FromHexString();

            options.MasterKey = Aes.Decrypt(wallet.MasterKey, passwordHash,iv ).FromHexString();
            options.SeedHex = Aes.Decrypt(wallet.SeedEncrypted, options.MasterKey.ToHexString(), iv);

            return new Wallet(options);
        }

        public string ToJson()
        {
            var wallet = new WalletJson
            {
                Version = this.version,
                PasswordHash = this.passwordHash,
                MasterKey = this.masterKey,
                Iv = this.iv,
                SeedEncrypted = this.seedEncrypted,
                Address = this.account.Address,
                ProgramHash = this.account.ProgramHash,
                ContractData = this.account.Contract
            };

            var result = JsonSerializer.ToJsonString(wallet);

            return result;
        }

        public bool IsCorrectAddress(string address) => Address.IsCorrectAddress(address);

        public bool IsCorrectPassword(string password)
        {
            var passwordHash = Crypto.DoubleSha256(password);
            return this.passwordHash == Crypto.Sha256Hex(passwordHash);
        }

        public static async Task<ulong> GetNonce(string address, WalletOptions options = null)
        {
            if (options == null)
            {
                options = WalletOptions.Default;
            }

            options.TxPool = true;

            var data = await RpcClient.GetNonceByAddress(options.RpcServer, address);
            if (data.Nonce == null)
            {
                throw new InvalidResponseException("nonce is null");
            }

            var nonce = data.Nonce.Value;
            if (options.TxPool && data.NonceInTxPool != null && data.NonceInTxPool > nonce)
            {
                nonce = data.NonceInTxPool.Value;
            }

            return nonce;
        }

        public Task<ulong> GetNonce()
        {
            return GetNonce(this.account.Address, this.options);
        }

        public async Task TransferTo(string toAddress, decimal amount, WalletOptions options = null)
        {
            if (Address.IsCorrectAddress(toAddress) == false)
            {
                //TODO: throw
            }

            //var payload = TransactionPayloadsFactory.Transfer()
        }

        public async Task RegisterName(string name)
        {

        }

        //private void CreateTransaction(byte[] payload,)

        public static async Task<string> SendTransaction(WalletOptions options, Transaction tx)
        {
            var result = await RpcClient.SendRawTransaction(options.RpcServer, tx);
            return result;
        }

        private Task<string> SendTransaction(Transaction tx)
        {
            return SendTransaction(this.options, tx);
        }

        public static string PublicKeyToAddress(string publicKey)
        {
            var signatureRedeem = Address.PublicKeyToSignatureRedeem(publicKey);
            var programHash = Address.HexStringToProgramHash(signatureRedeem);
            var result = Address.ProgramHashStringToAddress(programHash);
            return result;
        }

        public static async Task<long> GetLatestBlock(WalletOptions options = null)
        {
            var result = await RpcClient.GetLatestBlockHash(options.RpcServer);
            return result.Height;
        }

        public Task<long> GetLatestBlock() => GetLatestBlock(this.options);
    }
}
