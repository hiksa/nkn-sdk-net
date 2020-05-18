using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using NknSdk.Common.Exceptions;
using NknSdk.Common.Protobuf.Payloads;
using NSec;
using NSec.Cryptography;

namespace NknSdk.Common
{
    public class CryptoKey
    {
        private readonly byte[] privateKey;
        private byte[] curvePrivateKey;
        private IDictionary<string, byte[]> SharedKeyCache;
        private Key realKey;

        public CryptoKey()
            : this(PseudoRandom.RandomBytes(Crypto.SeedLength))
        {
        }

        public CryptoKey(string seed)
            : this(seed.FromHexString())
        {
        }

        public CryptoKey(byte[] seed)
        {
            if (seed != null)
            {
                try
                {
                    this.Seed = seed.ToHexString();
                }
                catch (Exception)
                {
                    throw new InvalidArgumentException("seed is not a valid hex string");
                }
            }
            else
            {
                this.Seed = PseudoRandom.RandomBytesAsHexString(Crypto.SeedLength);
            }

            this.SharedKeyCache = new Dictionary<string, byte[]>();

            var keyPair = Crypto.MakeKeyPair(seed);

            this.realKey = keyPair.RealKey;
            this.PublicKey = keyPair.PublicKey;
            this.privateKey = keyPair.PrivateKey;
            this.curvePrivateKey = keyPair.CurvePrivateKey;
        }

        public string PublicKey { get; }

        public string Seed { get; }

        public (byte[] Message, byte[] Nonce) Encrypt(byte[] message, string destinationPublicKey)
        {
            var nonce = PseudoRandom.RandomBytes(Crypto.NonceLength);

            var sharedKey = this.GetSharedSecret(destinationPublicKey);
            var encrypted = Crypto.EncryptSymmetric(message, nonce, sharedKey);

            return (encrypted, nonce);
        }

        public byte[] Decrypt(byte[] message, byte[] nonce, string sourcePublicKey)
        {
            var sharedKey = this.GetSharedSecret(sourcePublicKey);
            return Crypto.DecryptSymmetric(message, nonce, sharedKey);
        }

        public byte[] Sign(byte[] message)
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var signed = algorithm.Sign(this.realKey, message);

            return signed;
        }

        public byte[] GetSharedSecret(string otherPublicKey)
        {
            if (this.SharedKeyCache.ContainsKey(otherPublicKey))
            {
                return this.SharedKeyCache[otherPublicKey];
            }
            else
            {
                var sharedKey = Chaos.NaCl.Ed25519.KeyExchange(otherPublicKey.FromHexString(), this.privateKey);
                this.SharedKeyCache[otherPublicKey] = sharedKey;
                return sharedKey;
            }
        }
    }
}
