using System;
using System.Net;
using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Utils;

namespace FileSharing.Models
{
    public class CryptoPeer : ObservableObject
    {
        private NetPeer _peer;
        private CryptographyModule _cryptography;
        private readonly CompressModule _compress;
        private readonly Random _random;
        private bool _isConnectionExpected;
        private string _expectedAddress;
        private int _expectedId;
        private bool _isSet;
        private DateTime _startTime;
        private TimeSpan _connectionDuration;
        private long _bytesUploaded;
        private readonly DispatcherTimer _durationTimer;

        public CryptoPeer()
        {
            _compress = new CompressModule();
            _random = new Random();
            _durationTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _durationTimer.Interval = new TimeSpan(0, 0, 1);
            _durationTimer.Tick += OnDurationTimerTick;
            IsSet = false;
            BytesUploaded = 0;
        }

        private void OnDurationTimerTick(object sender, EventArgs e)
        {
            ConnectionDuration = DateTime.Now - StartTime;
        }

        public NetPeer Peer
        {
            get => _peer;
            private set => SetProperty(ref _peer, value);
        }

        public bool IsSet
        {
            get => _isSet;
            private set => SetProperty(ref _isSet, value);
        }

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

        public bool IsEstablished => IsSet && _cryptography.IsEnabled;
        public int Id => IsSet ? _peer.Id : -1;
        public IPEndPoint EndPoint => IsSet ? _peer.EndPoint : new IPEndPoint(0, 0);
        public ConnectionState ConnectionState => IsSet ? _peer.ConnectionState : ConnectionState.Disconnected;

        public void ExpectConnectionFromServer(string ip, int port, int id)
        {
            _expectedAddress = ip + ":" + port;
            _isConnectionExpected = true;
            _expectedId = id;
        }

        public bool ApproveExpectedConnection(NetPeer server)
        {
            return _isConnectionExpected && 
            _expectedAddress == server.EndPoint.ToString() &&
            server.Id == _expectedId;
        }

        public void ChangePeer(NetPeer peer)
        {
            _peer = peer;
            _cryptography = new CryptographyModule();
            IsSet = true;

            _expectedAddress = "";
            _isConnectionExpected = false;
            _expectedId = -1;
        }

        public bool SendPublicKeys()
        {
            if (!IsSet)
            {
                return false;
            }

            var keys = new NetDataWriter();
            var publicKey = _cryptography.PublicKey;
            keys.Put(publicKey.Length);
            keys.Put(publicKey);
            var signaturePublicKey = _cryptography.SignaturePublicKey;
            keys.Put(signaturePublicKey.Length);
            keys.Put(signaturePublicKey);
            _peer.Send(keys, 0, DeliveryMethod.ReliableOrdered);

            return true;
        }

        public bool ApplyKeys(byte[] publicKey, byte[] signaturePublicKey)
        {
            if (!IsSet)
            {
                return false;
            }

            _cryptography.SetKeys(publicKey, signaturePublicKey);
            OnPropertyChanged(nameof(IsEstablished));

            StartTime = DateTime.Now;
            _durationTimer.Start();

            return true;
        }

        public bool SendEncrypted(byte[] message)
        {
            if (!IsSet)
            {
                Debug.WriteLine("(SendEncrypted) Cryptography module is not set!");

                return false;
            }

            if (!IsEstablished)
            {
                Debug.WriteLine("(SendEncrypted) Encryption is not established! " + EndPoint);

                return false;
            }

            try
            {
                var compressedMessage = _compress.CompressByteArray(message);

                _cryptography.Encrypt(compressedMessage, out byte[] encryptedMessage, out byte[] iv);
                var signature = _cryptography.CreateSignature(encryptedMessage);

                var writer = new SimpleWriter();
                writer.Put(encryptedMessage.Length);
                writer.Put(encryptedMessage);
                writer.Put(signature.Length);
                writer.Put(signature);
                writer.Put(iv.Length);
                writer.Put(iv);

                BytesUploaded += writer.Length;

                var channelNumber = (byte)_random.Next(0, _peer.NetManager.ChannelsCount);
                _peer.Send(writer.Get(), channelNumber, DeliveryMethod.ReliableOrdered);

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

        public bool SendEncrypted(NetDataWriter message)
        {
            return SendEncrypted(message.Data);
        }

        public NetDataReader DecryptReceivedData(NetPacketReader message)
        {
            if (!IsSet)
            {
                Debug.WriteLine("(DecryptReceivedData) Cryptography module is not set!");

                return new NetDataReader(Array.Empty<byte>());
            }

            if (!IsEstablished)
            {
                Debug.WriteLine("(DecryptReceivedData) Encryption is not established! " + EndPoint);

                return new NetDataReader(Array.Empty<byte>());
            }

            try
            {
                var decompressedMessage = _compress.DecompressByteArray(message.RawData);
                var incomingMessage = new NetDataReader(decompressedMessage);

                var encryptedMessage = new byte[incomingMessage.GetInt()];
                incomingMessage.GetBytes(encryptedMessage, encryptedMessage.Length);
                var signature = new byte[incomingMessage.GetInt()];
                incomingMessage.GetBytes(signature, signature.Length);

                if (!_cryptography.VerifySignature(encryptedMessage, signature))
                {
                    Debug.WriteLine("(DecryptReceivedData) Signature is NOT verified! " + EndPoint);

                    return new NetDataReader(Array.Empty<byte>());
                }

                var iv = new byte[incomingMessage.GetInt()];
                incomingMessage.GetBytes(iv, iv.Length);

                var data = _cryptography.Decrypt(encryptedMessage, iv);

                Debug.WriteLine("(DecryptReceivedData) Length of encrypted data: " + encryptedMessage.Length);
                Debug.WriteLine("(DecryptReceivedData) Length of decrypted data: " + data.Length);

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
            if (IsSet)
            {
                _peer.Disconnect();
            }
        }
    }
}
