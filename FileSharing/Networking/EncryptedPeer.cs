using System;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using LiteNetLib;
using LiteNetLib.Utils;
using FileSharing.Networking.Utils;

namespace FileSharing.Networking
{
    public sealed class EncryptedPeer : ObservableObject
    {
        private readonly NetPeer _peer;
        private readonly CryptographyModule _cryptography;
        private readonly DispatcherTimer _durationTimer;
        private readonly DispatcherTimer _disconnectTimer;
        private readonly SpeedCounter _downloadSpeedCounter;
        private readonly SpeedCounter _uploadSpeedCounter;
        private TimeSpan _connectionDuration;

        public EncryptedPeer(NetPeer peer)
        {
            _peer = peer;
            _cryptography = new CryptographyModule();
            _durationTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _durationTimer.Interval = TimeSpan.FromSeconds(1);
            _durationTimer.Tick += OnDurationTimerTick;
            _durationTimer.Start();

            _disconnectTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _disconnectTimer.Interval = TimeSpan.FromMilliseconds(Constants.DisconnectionTimeout);
            _disconnectTimer.Tick += OnDisconnectTimerTick;
            _disconnectTimer.Start();

            _downloadSpeedCounter = new SpeedCounter();
            _downloadSpeedCounter.Updated += OnDownloadSpeedCounterUpdated;
            _uploadSpeedCounter = new SpeedCounter();
            _uploadSpeedCounter.Updated += OnUploadSpeedCounterUpdated;

            StartTime = DateTime.Now;
        }

        public event EventHandler<EncryptedPeerEventArgs>? PeerDisconnected;

        public DateTime StartTime { get; }
        public int Id => _peer.Id;
        public IPEndPoint EndPoint => _peer.EndPoint;
        public bool IsSecurityEnabled => _cryptography.IsEnabled;
        public ConnectionState ConnectionStatus => _peer.ConnectionState;
        public double DownloadSpeed => _downloadSpeedCounter.Speed;
        public double UploadSpeed => _uploadSpeedCounter.Speed;
        public long BytesDownloaded => _downloadSpeedCounter.Bytes;
        public long BytesUploaded => _uploadSpeedCounter.Bytes;

        public TimeSpan ConnectionDuration
        {
            get => _connectionDuration;
            private set => SetProperty(ref _connectionDuration, value);
        }

        private void OnDisconnectTimerTick(object? sender, EventArgs e)
        {
            _disconnectTimer.Stop();
            if (!IsSecurityEnabled)
            {
                Disconnect();
            }
        }

        private void OnDurationTimerTick(object? sender, EventArgs e)
        {
            ConnectionDuration = DateTime.Now - StartTime;
        }

        private void OnUploadSpeedCounterUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(UploadSpeed));
            OnPropertyChanged(nameof(BytesUploaded));
        }

        private void OnDownloadSpeedCounterUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(DownloadSpeed));
            OnPropertyChanged(nameof(BytesDownloaded));
        }

        public void SendPublicKeys()
        {
            var keys = new NetDataWriter();
            var publicKey = _cryptography.PublicKey;
            keys.Put(publicKey.Length);
            keys.Put(publicKey);
            var signaturePublicKey = _cryptography.SignaturePublicKey;
            keys.Put(signaturePublicKey.Length);
            keys.Put(signaturePublicKey);

            _peer.Send(keys, 0, DeliveryMethod.ReliableOrdered);
        }

        public void ApplyKeys(byte[] publicKey, byte[] signaturePublicKey)
        {
            if (_cryptography.TrySetKeys(publicKey, signaturePublicKey))
            {
                OnPropertyChanged(nameof(IsSecurityEnabled));
            }
            else
            {
                Disconnect();
            }
        }

        public void SendEncrypted(NetDataWriter message, byte channelNumber)
        {
            SendEncrypted(message.Data, channelNumber);
        }

        public void SendEncrypted(byte[] message, byte channelNumber)
        {
            if (!IsSecurityEnabled)
            {
                return;
            }

            if (message.TryCompressByteArray(out byte[] compressedMessage) &&
                _cryptography.TryEncrypt(compressedMessage, out byte[] encryptedMessage, out byte[] iv) &&
                _cryptography.TryCreateSignature(encryptedMessage, out byte[] signature))
            {
                var sendingMessage = new NetDataWriter();
                sendingMessage.Put(encryptedMessage.Length);
                sendingMessage.Put(encryptedMessage);
                sendingMessage.Put(signature.Length);
                sendingMessage.Put(signature);
                sendingMessage.Put(iv.Length);
                sendingMessage.Put(iv);

                _uploadSpeedCounter.AddBytes(sendingMessage.Data.Length);
                _peer.Send(sendingMessage, channelNumber, DeliveryMethod.ReliableOrdered);
            }
        }

        public NetDataReader DecryptReceivedData(NetPacketReader message)
        {
            if (!IsSecurityEnabled)
            {
                return new NetDataReader(Array.Empty<byte>());
            }

            if (message.TryGetBytesWithLength(out byte[] encryptedMessage) &&
                message.TryGetBytesWithLength(out byte[] signature) &&
                _cryptography.TryVerifySignature(encryptedMessage, signature) &&
                message.TryGetBytesWithLength(out byte[] iv) &&
                _cryptography.TryDecrypt(encryptedMessage, iv, out byte[] decryptedMessage) &&
                decryptedMessage.TryDecompressByteArray(out byte[] data))
            {
                _downloadSpeedCounter.AddBytes(message.RawData.Length);

                return new NetDataReader(data);
            }
            else
            {
                return new NetDataReader(Array.Empty<byte>());
            }
        }

        public void Disconnect()
        {
            var id = Id;
            _peer.Disconnect();

            _durationTimer.Stop();
            _disconnectTimer.Stop();
            _downloadSpeedCounter.Stop();
            _uploadSpeedCounter.Stop();

            PeerDisconnected?.Invoke(this, new EncryptedPeerEventArgs(id));
        }

        public override string ToString()
        {
            return _peer.EndPoint.ToString();
        }
    }
}