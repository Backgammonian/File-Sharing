using System;
using System.Net;
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
        private NetPeer? _peer;
        private DateTime _startTime;
        private TimeSpan _connectionDuration;
        private long _bytesUploaded;
        private long _bytesDownloaded;
        private bool _isConnectionExpected;
        private NetPeer? _expectedServer;

        public CryptoPeer()
        {
            _random = new Random();
            _durationTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _durationTimer.Interval = new TimeSpan(0, 0, 1);
            _durationTimer.Tick += OnDurationTimerTick;
            _cryptography = new CryptographyModule();
        }

        public event EventHandler<CryptoPeerEventArgs>? PeerDisconnected;

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

        public NetPeer? Peer
        {
            get => _peer;
            private set => SetProperty(ref _peer, value);
        }

        public bool IsSecurityEnabled => Peer != null && _cryptography.IsEnabled;

        private void OnDurationTimerTick(object? sender, EventArgs e)
        {
            ConnectionDuration = DateTime.Now - StartTime;
        }

        public void ExpectConnectionFromServer(NetPeer server)
        {
            _expectedServer = server;
            _isConnectionExpected = true;
        }

        public bool ApproveExpectedConnection(NetPeer server)
        {
            return server != null &&
                _expectedServer != null &&
                _isConnectionExpected &&
                _expectedServer.EndPoint.ToString() == server.EndPoint.ToString() &&
                _expectedServer.Id == server.Id;
        }

        public void SetPeer(NetPeer peer)
        {
            Peer = peer;

            _expectedServer = null;
            _isConnectionExpected = false;
        }

        public bool SendPublicKeys()
        {
            if (Peer == null)
            {
                return false;
            }

            var keys = new NetDataWriter();
            var publicKey = _cryptography.PublicKey.ToArray();
            keys.Put(publicKey.Length);
            keys.Put(publicKey);
            var signaturePublicKey = _cryptography.SignaturePublicKey.ToArray();
            keys.Put(signaturePublicKey.Length);
            keys.Put(signaturePublicKey);

            Peer.Send(keys, 0, DeliveryMethod.ReliableOrdered);

            return true;
        }

        public bool ApplyKeys(byte[] publicKey, byte[] signaturePublicKey)
        {
            if (Peer == null)
            {
                return false;
            }

            _cryptography.SetKeys(publicKey, signaturePublicKey);
            OnPropertyChanged(nameof(IsSecurityEnabled));

            StartTime = DateTime.Now;
            _durationTimer.Start();

            return true;
        }

        public bool SendEncrypted(byte[] message)
        {
            if (Peer == null)
            {
                Debug.WriteLine("(SendEncrypted) Peer is not set!");

                return false;
            }

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
            if (Peer == null)
            {
                Debug.WriteLine("(DecryptReceivedData) Peer is not set!");

                return new NetDataReader(Array.Empty<byte>());
            }

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
            if (Peer == null)
            {
                return;
            }

            var id = Peer.Id;
            Peer.Disconnect();
            _durationTimer.Stop();

            PeerDisconnected?.Invoke(this, new CryptoPeerEventArgs(id));
        }
    }
}