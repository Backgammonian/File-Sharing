using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Utils;
using FileSharing.Modules;

namespace FileSharing.Models
{
    public class CryptoPeer : ObservableObject
    {
        private readonly Random _random;
        private readonly DispatcherTimer _durationTimer;
        private readonly CryptographyModule _cryptography;
        private DateTime _startTime;
        private TimeSpan _connectionDuration;
        private long _bytesUploaded;
        private long _bytesDownloaded;
        private const int _timeout = 10;
        private readonly DispatcherTimer _disconnectTimer;

        public CryptoPeer(NetPeer peer)
        {
            Peer = peer;
            _random = new Random();
            _durationTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _durationTimer.Interval = new TimeSpan(0, 0, 1);
            _durationTimer.Tick += OnDurationTimerTick;
            _cryptography = new CryptographyModule();
            _disconnectTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _disconnectTimer.Interval = new TimeSpan(0, 0, _timeout);
            _disconnectTimer.Tick += OnDisconnectTimerTick;
            _disconnectTimer.Start();
        }

        public event EventHandler<CryptoPeerEventArgs>? PeerDisconnected;

        public NetPeer Peer { get; }
        public bool IsSecurityEnabled => _cryptography.IsEnabled;

        public DateTime StartTime
        {
            get => _startTime;
            private set => SetProperty(ref _startTime, value);
        }

        public TimeSpan ConnectionDuration
        {
            get => _connectionDuration;
            private set => SetProperty(ref _connectionDuration, value);
        }

        public long BytesUploaded
        {
            get => _bytesUploaded;
            private set => SetProperty(ref _bytesUploaded, value);
        }

        public long BytesDownloaded
        {
            get => _bytesDownloaded;
            private set => SetProperty(ref _bytesDownloaded, value);
        }

        private void OnDurationTimerTick(object? sender, EventArgs e)
        {
            ConnectionDuration = DateTime.Now - StartTime;
        }

        private void OnDisconnectTimerTick(object? sender, EventArgs e)
        {
            _disconnectTimer.Stop();
            if (!IsSecurityEnabled)
            {
                Disconnect();
            }
        }

        public void SendPublicKeys()
        {
            var keys = new NetDataWriter();
            var publicKey = _cryptography.PublicKey.ToArray();
            keys.Put(publicKey.Length);
            keys.Put(publicKey);
            var signaturePublicKey = _cryptography.SignaturePublicKey.ToArray();
            keys.Put(signaturePublicKey.Length);
            keys.Put(signaturePublicKey);

            Peer.Send(keys, 0, DeliveryMethod.ReliableOrdered);
        }

        public void ApplyKeys(byte[] publicKey, byte[] signaturePublicKey)
        {
            _cryptography.SetKeys(publicKey, signaturePublicKey);
            OnPropertyChanged(nameof(IsSecurityEnabled));

            StartTime = DateTime.Now;
            _durationTimer.Start();
        }

        public bool SendEncrypted(byte[] message)
        {
            if (!IsSecurityEnabled)
            {
                Debug.WriteLine("(SendEncrypted) Encryption with peer " + Peer.EndPoint + " is not established!");

                return false;
            }

            try
            {
                BytesUploaded += message.Length;

                var compressedMessage = Compression.CompressByteArray(message);

                _cryptography.Encrypt(compressedMessage, out byte[] encryptedMessage, out byte[] iv);
                var signature = _cryptography.CreateSignature(encryptedMessage);

                var writer = new SimpleWriter();
                writer.Put(encryptedMessage.Length);
                writer.Put(encryptedMessage);
                writer.Put(signature.Length);
                writer.Put(signature);
                writer.Put(iv.Length);
                writer.Put(iv);

                var channelNumber = (byte)_random.Next(0, Peer.NetManager.ChannelsCount);
                Peer.Send(writer.Get(), channelNumber, DeliveryMethod.ReliableOrdered);

                Debug.WriteLine("(SendEncrypted) Length of raw data: " + message.Length);
                Debug.WriteLine("(SendEncrypted) Length of encrypted data: " + encryptedMessage.Length);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("(SendEncrypted_Error) Can't send an encrypted message: " + ex);

                return false;
            }
        }

        public bool SendEncrypted(SimpleWriter message)
        {
            return SendEncrypted(message.Get());
        }

        public NetDataReader DecryptReceivedData(NetPacketReader message)
        {
            if (!IsSecurityEnabled)
            {
                Debug.WriteLine("(DecryptReceivedData) Encryption is not established! " + Peer.EndPoint);

                return new NetDataReader(Array.Empty<byte>());
            }

            try
            {
                var encryptedMessage = new byte[message.GetInt()];
                message.GetBytes(encryptedMessage, encryptedMessage.Length);
                var signature = new byte[message.GetInt()];
                message.GetBytes(signature, signature.Length);

                if (!_cryptography.VerifySignature(encryptedMessage, signature))
                {
                    Debug.WriteLine("(DecryptReceivedData) Signature is NOT verified! " + Peer.EndPoint);

                    return new NetDataReader(Array.Empty<byte>());
                }

                var iv = new byte[message.GetInt()];
                message.GetBytes(iv, iv.Length);

                var decryptedMessage = _cryptography.Decrypt(encryptedMessage, iv);

                var data = Compression.DecompressByteArray(decryptedMessage);

                Debug.WriteLine("(DecryptReceivedData) Length of encrypted data: " + encryptedMessage.Length);
                Debug.WriteLine("(DecryptReceivedData) Length of decrypted data: " + data.Length);

                BytesDownloaded += data.Length;

                return new NetDataReader(data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("(DecryptReceivedData_Error) Can't decrypt received message: " + ex);

                return new NetDataReader(Array.Empty<byte>());
            }
        }

        public void Disconnect()
        {
            var id = Peer.Id;
            Peer.Disconnect();
            _durationTimer.Stop();

            PeerDisconnected?.Invoke(this, new CryptoPeerEventArgs(id));
        }
    }
}