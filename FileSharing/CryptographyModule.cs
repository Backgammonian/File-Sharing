using System;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace FileSharing
{
    public class CryptographyModule : ObservableObject
    {
        private readonly ECDiffieHellmanCng _ecdh;
        private readonly byte[] _publicKey;
        private byte[] _privateKey;
        private readonly CngKey _signature;
        private readonly byte[] _signaturePublicKey;
        private byte[] _othersSignaturePublicKey;
        private bool _isEnabled;
        
        public CryptographyModule()
        {
            _ecdh = new ECDiffieHellmanCng();
            _ecdh.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            _ecdh.HashAlgorithm = CngAlgorithm.Sha256;
            _publicKey = _ecdh.PublicKey.ToByteArray();
            _privateKey = Array.Empty<byte>();
            _signature = CngKey.Create(CngAlgorithm.ECDsaP256);
            _signaturePublicKey = _signature.Export(CngKeyBlobFormat.GenericPublicBlob);
            _othersSignaturePublicKey = Array.Empty<byte>();

            IsEnabled = false;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            private set => SetProperty(ref _isEnabled, value);
        }

        public byte[] PublicKey => (byte[])_publicKey.Clone();
        public byte[] SignaturePublicKey => (byte[])_signaturePublicKey.Clone();

        public void SetKeys(byte[] publicKey, byte[] signaturePublicKey)
        {
            _privateKey = _ecdh.DeriveKeyMaterial(CngKey.Import(publicKey, CngKeyBlobFormat.EccPublicBlob));
            _othersSignaturePublicKey = (byte[])signaturePublicKey.Clone();

            IsEnabled = true;
        }

        public byte[] CreateSignature(byte[] data)
        {
            var signingAlgorithm = new ECDsaCng(_signature);
            var newSignature = signingAlgorithm.SignData(data);
            signingAlgorithm.Clear();

            return newSignature;
        }

        public bool VerifySignature(byte[] data, byte[] signature)
        {
            using var key = CngKey.Import(_othersSignaturePublicKey, CngKeyBlobFormat.GenericPublicBlob);
            var signingAlg = new ECDsaCng(key);
            var result = signingAlg.VerifyData(data, signature);
            signingAlg.Clear();

            return result;
        }

        public void Encrypt(byte[] secretMessage, out byte[] encryptedMessage, out byte[] iv)
        {
            using Aes aes = new AesCryptoServiceProvider();
            aes.Key = _privateKey;
            iv = aes.IV;
            using var ciphertext = new MemoryStream();
            using var cs = new CryptoStream(ciphertext, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(secretMessage, 0, secretMessage.Length);
            cs.Close();

            encryptedMessage = ciphertext.ToArray();
        }

        public byte[] Decrypt(byte[] encryptedMessage, byte[] iv)
        {
            using Aes aes = new AesCryptoServiceProvider();
            aes.Key = _privateKey;
            aes.IV = iv;
            using var plaintext = new MemoryStream();
            using var cs = new CryptoStream(plaintext, aes.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(encryptedMessage, 0, encryptedMessage.Length);
            cs.Close();

            return plaintext.ToArray();
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        public static string ComputeSHA256Hash(string data)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha.ComputeHash(bytes);

            return BitConverter.ToString(hash).ToLower().Replace("-", "");
        }
    }
}
