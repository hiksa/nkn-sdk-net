using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Chaos.NaCl;

using NknSdk.Common.Exceptions;
using NknSdk.Common.Extensions;

namespace NknSdk.Common
{
    public class CryptoKey
    {
        private readonly CryptoKeyInfo keyInfo;
        private readonly IDictionary<string, byte[]> sharedKeyCache;

        public CryptoKey() : this(PseudoRandom.RandomBytes(Hash.SeedLength))
        {
        }

        public CryptoKey(string seed) : this(seed.FromHexString())
        {
        }

        public CryptoKey(byte[] seed)
        {
            if (seed != null)
            {
                try
                {
                    this.SeedHex = seed.ToHexString();
                }
                catch (Exception)
                {
                    throw new InvalidArgumentException("seed is not a valid hex string");
                }
            }
            else
            {
                this.SeedHex = PseudoRandom.RandomBytesAsHexString(Hash.SeedLength);
            }

            this.sharedKeyCache = new ConcurrentDictionary<string, byte[]>();

            this.keyInfo = CryptoKeyInfo.FromSeed(seed);
        }

        public string PublicKey => this.keyInfo.PublicKey;

        public string SeedHex { get; }

        public (byte[] Message, byte[] Nonce) Encrypt(byte[] message, string destinationPublicKey)
        {
            var nonce = PseudoRandom.RandomBytes(Hash.NonceLength);

            var sharedKey = this.GetSharedSecret(destinationPublicKey);
            var encrypted = Hash.EncryptSymmetric(message, nonce, sharedKey);

            return (encrypted, nonce);
        }

        public byte[] Decrypt(byte[] message, byte[] nonce, string sourcePublicKey)
        {
            var sharedKey = this.GetSharedSecret(sourcePublicKey);

            return Hash.DecryptSymmetric(message, nonce, sharedKey);
        }

        public byte[] Sign(byte[] message)
        {
            return Ed25519.Sign(message, this.keyInfo.PrivateKey);
        }

        public byte[] GetSharedSecret(string otherPublicKey)
        {
            if (this.sharedKeyCache.ContainsKey(otherPublicKey))
            {
                return this.sharedKeyCache[otherPublicKey];
            }
            else
            {
                var sharedKey = Chaos.NaCl.Ed25519.KeyExchange(otherPublicKey.FromHexString(), this.keyInfo.PrivateKey);

                this.sharedKeyCache[otherPublicKey] = sharedKey;

                return sharedKey;
            }
        }
    }
}
