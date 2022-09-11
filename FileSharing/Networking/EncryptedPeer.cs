using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using System.Collections.Generic;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using LiteNetLib;
using LiteNetLib.Utils;
using FileSharing.Utils;

namespace FileSharing.Networking
{
    public class EncryptedPeer : ObservableObject
    {
        private const double _interval = 100.0;
        private const int _timeout = 30;

        private readonly CryptographyModule _cryptography;
        private readonly DispatcherTimer _durationTimer;
        private readonly DispatcherTimer _disconnectTimer;
        private DateTime _startTime;
        private TimeSpan _connectionDuration;

        private readonly DispatcherTimer _downloadSpeedCounter;
        private readonly Queue<double> _downloadSpeedValues;
        private long _oldAmountOfDownloadedBytes, _newAmountOfDownloadedBytes;
        private DateTime _oldDownloadTimeStamp, _newDownloadTimeStamp;
        private long _bytesDownloaded;
        private double _downloadSpeed;
        
        private readonly DispatcherTimer _uploadSpeedCounter;
        private readonly Queue<double> _uploadSpeedValues;
        private long _oldAmountOfUploadedBytes, _newAmountOfUploadedBytes;
        private DateTime _oldUploadTimeStamp, _newUploadTimeStamp;
        private long _bytesUploaded;
        private double _uploadSpeed;

        public EncryptedPeer(NetPeer peer)
        {
            Peer = peer;
            StartTime = DateTime.Now;
            _cryptography = new CryptographyModule();
            _durationTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _durationTimer.Interval = new TimeSpan(0, 0, 1);
            _durationTimer.Tick += OnDurationTimerTick;
            _durationTimer.Start();

            _disconnectTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _disconnectTimer.Interval = new TimeSpan(0, 0, _timeout);
            _disconnectTimer.Tick += OnDisconnectTimerTick;
            _disconnectTimer.Start();

            _downloadSpeedValues = new Queue<double>();
            _downloadSpeedCounter = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _downloadSpeedCounter.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(_interval));
            _downloadSpeedCounter.Tick += OnDownloadSpeedCounterTick;
            _downloadSpeedCounter.Start();

            _uploadSpeedValues = new Queue<double>();
            _uploadSpeedCounter = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _uploadSpeedCounter.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(_interval));
            _uploadSpeedCounter.Tick += OnUploadSpeedCounterTick;
            _uploadSpeedCounter.Start();
        }

        public event EventHandler<EncryptedPeerEventArgs>? PeerDisconnected;

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

        public double DownloadSpeed
        {
            get => _downloadSpeed;
            private set => SetProperty(ref _downloadSpeed, value);
        }

        public double UploadSpeed
        {
            get => _uploadSpeed;
            private set => SetProperty(ref _uploadSpeed, value);
        }

        private void OnUploadSpeedCounterTick(object? sender, EventArgs e)
        {
            _oldAmountOfUploadedBytes = _newAmountOfUploadedBytes;
            _newAmountOfUploadedBytes = BytesUploaded;

            _oldUploadTimeStamp = _newUploadTimeStamp;
            _newUploadTimeStamp = DateTime.Now;

            var value = (_newAmountOfUploadedBytes - _oldAmountOfUploadedBytes) / (_newUploadTimeStamp - _oldUploadTimeStamp).TotalSeconds;
            _uploadSpeedValues.Enqueue(value);

            if (_uploadSpeedValues.Count > 20)
            {
                _uploadSpeedValues.Dequeue();
            }

            UploadSpeed = CalculateMovingAverageUploadSpeed();
        }

        private void OnDownloadSpeedCounterTick(object? sender, EventArgs e)
        {
            _oldAmountOfDownloadedBytes = _newAmountOfDownloadedBytes;
            _newAmountOfDownloadedBytes = BytesDownloaded;

            _oldDownloadTimeStamp = _newDownloadTimeStamp;
            _newDownloadTimeStamp = DateTime.Now;

            var value = (_newAmountOfDownloadedBytes - _oldAmountOfDownloadedBytes) / (_newDownloadTimeStamp - _oldDownloadTimeStamp).TotalSeconds;
            _downloadSpeedValues.Enqueue(value);

            if (_downloadSpeedValues.Count > 20)
            {
                _downloadSpeedValues.Dequeue();
            }

            DownloadSpeed = CalculateMovingAverageDownloadSpeed();
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

        private double CalculateMovingAverageDownloadSpeed()
        {
            var result = 0.0;
            foreach (var value in _downloadSpeedValues)
            {
                result += value;
            }

            return result / _downloadSpeedValues.Count;
        }

        private double CalculateMovingAverageUploadSpeed()
        {
            var result = 0.0;
            foreach (var value in _uploadSpeedValues)
            {
                result += value;
            }

            return result / _uploadSpeedValues.Count;
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

            Peer.Send(keys, 0, DeliveryMethod.ReliableOrdered);
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

        public void SendEncrypted(byte[] message, byte channelNumber)
        {
            if (!IsSecurityEnabled)
            {
                Debug.WriteLine($"(SendEncrypted) Encryption with peer {Peer.EndPoint} is not established!");

                return;
            }

            if (message.TryCompressByteArray(out byte[] compressedMessage) &&
                _cryptography.TryEncrypt(compressedMessage, out byte[] encryptedMessage, out byte[] iv) &&
                _cryptography.TryCreateSignature(encryptedMessage, out byte[] signature))
            {
                var sentMessage = new SimpleWriter();
                sentMessage.Put(encryptedMessage.Length);
                sentMessage.Put(encryptedMessage);
                sentMessage.Put(signature.Length);
                sentMessage.Put(signature);
                sentMessage.Put(iv.Length);
                sentMessage.Put(iv);

                Debug.WriteLine($"(SendEncrypted) Length of sent data: {sentMessage.Length}");

                BytesUploaded += sentMessage.Length;
                Peer.Send(sentMessage.Get(), channelNumber, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                Debug.WriteLine($"(SendEncrypted_Error) Can't send an encrypted message to {Peer.EndPoint}");
            }
        }

        public void SendEncrypted(SimpleWriter message, byte channelNumber)
        {
            SendEncrypted(message.Get(), channelNumber);
        }

        public NetDataReader DecryptReceivedData(NetPacketReader message)
        {
            if (!IsSecurityEnabled)
            {
                Debug.WriteLine($"(DecryptReceivedData) Encryption with {Peer.EndPoint} is not established!");

                return new NetDataReader(Array.Empty<byte>());
            }

            var messageLength = message.AvailableBytes;

            if (message.TryGetBytesWithLength(out byte[] encryptedMessage) &&
                message.TryGetBytesWithLength(out byte[] signature) &&
                _cryptography.TryVerifySignature(encryptedMessage, signature) &&
                message.TryGetBytesWithLength(out byte[] iv) &&
                _cryptography.TryDecrypt(encryptedMessage, iv, out byte[] decryptedMessage) &&
                decryptedMessage.TryDecompressByteArray(out byte[] data))
            {
                Debug.WriteLine($"(DecryptReceivedData) Length of decrypted data: {messageLength}");

                BytesDownloaded += messageLength;

                return new NetDataReader(data);
            }
            else
            {
                Debug.WriteLine($"(DecryptReceivedData_Error) Can't decrypt received message from {Peer.EndPoint}");

                return new NetDataReader(Array.Empty<byte>());
            }
        }

        public void Disconnect()
        {
            var id = Peer.Id;
            Peer.Disconnect();

            _durationTimer.Stop();
            _disconnectTimer.Stop();
            _downloadSpeedCounter.Stop();
            _uploadSpeedCounter.Stop();

            PeerDisconnected?.Invoke(this, new EncryptedPeerEventArgs(id));
        }
    }
}