using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace FileSharing.Modules
{
    public class CryptographyModule : ObservableObject
    {
        private readonly ECDiffieHellmanCng _ecdh;
        private readonly byte[] _publicKey;
        private readonly CngKey _signature;
        private readonly byte[] _signaturePublicKey;
        private byte[] _privateKey;
        private byte[] _recipientsSignaturePublicKey;
        private bool _isEnabled;
        
        public CryptographyModule()
        {
            _ecdh = new ECDiffieHellmanCng();
            _ecdh.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            _ecdh.HashAlgorithm = CngAlgorithm.Sha256;
            _publicKey = _ecdh.PublicKey.ToByteArray();
            _signature = CngKey.Create(CngAlgorithm.ECDsaP256);
            _signaturePublicKey = _signature.Export(CngKeyBlobFormat.GenericPublicBlob);
            _privateKey = Array.Empty<byte>();
            _recipientsSignaturePublicKey = Array.Empty<byte>();

            IsEnabled = false;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            private set => SetProperty(ref _isEnabled, value);
        }

        public ReadOnlySpan<byte> PublicKey => new ReadOnlySpan<byte>(_publicKey);
        public ReadOnlySpan<byte> SignaturePublicKey => new ReadOnlySpan<byte>(_signaturePublicKey);

        public void SetKeys(byte[] publicKey, byte[] signaturePublicKey)
        {
            _privateKey = _ecdh.DeriveKeyMaterial(CngKey.Import(publicKey, CngKeyBlobFormat.EccPublicBlob));
            _recipientsSignaturePublicKey = (byte[])signaturePublicKey.Clone();

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
            using var key = CngKey.Import(_recipientsSignaturePublicKey, CngKeyBlobFormat.GenericPublicBlob);
            var signingAlgorithm = new ECDsaCng(key);
            var result = signingAlgorithm.VerifyData(data, signature);
            signingAlgorithm.Clear();

            return result;
        }

        public void Encrypt(byte[] secretMessage, out byte[] encryptedMessage, out byte[] iv)
        {
            using Aes aes = new AesCryptoServiceProvider();
            aes.Key = _privateKey;
            iv = aes.IV;
            using var cipherText = new MemoryStream();
            using var cryptoStream = new CryptoStream(cipherText, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cryptoStream.Write(secretMessage, 0, secretMessage.Length);
            cryptoStream.Close();

            encryptedMessage = cipherText.ToArray();
        }

        public byte[] Decrypt(byte[] encryptedMessage, byte[] iv)
        {
            using Aes aes = new AesCryptoServiceProvider();
            aes.Key = _privateKey;
            aes.IV = iv;
            using var plainText = new MemoryStream();
            using var cryptoStream = new CryptoStream(plainText, aes.CreateDecryptor(), CryptoStreamMode.Write);
            cryptoStream.Write(encryptedMessage, 0, encryptedMessage.Length);
            cryptoStream.Close();

            return plainText.ToArray();
        }

        public void Disable()
        {
            IsEnabled = false;
        }
    }
}
