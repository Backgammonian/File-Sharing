using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Layers;
using LiteNetLib.Utils;

namespace FileSharing.Networking
{
    public sealed class Server
    {
        private readonly EventBasedNetListener _listener;
        private readonly XorEncryptLayer _xor;
        private readonly NetManager _server;
        private readonly EncryptedPeers _clients;
        private readonly Task _listenTask;
        private readonly CancellationTokenSource _tokenSource;

        public Server()
        {
            _listener = new EventBasedNetListener();
            _xor = new XorEncryptLayer("VerySecretSymmetricXorPassword");
            _server = new NetManager(_listener, _xor);
            _server.ChannelsCount = Constants.ChannelsCount;
            _server.DisconnectTimeout = Constants.DisconnectionTimeout;
            _clients = new EncryptedPeers();
            _clients.PeerAdded += OnClientAdded;
            _clients.PeerRemoved += OnClientRemoved;
            _tokenSource = new CancellationTokenSource();
            var token = _tokenSource.Token;
            _listenTask = new Task(() => Run(token));
        }

        public event EventHandler<NetEventArgs>? MessageReceived;
        public event EventHandler<EncryptedPeerEventArgs>? ClientAdded;
        public event EventHandler<EncryptedPeerEventArgs>? ClientRemoved;

        public int LocalPort => _server.LocalPort;
        public byte ChannelsCount => _server.ChannelsCount;
        public IEnumerable<EncryptedPeer> Clients => _clients.List;

        private void OnClientAdded(object? sender, EncryptedPeerEventArgs e)
        {
            ClientAdded?.Invoke(this, e);
        }

        private void OnClientRemoved(object? sender, EncryptedPeerEventArgs e)
        {
            ClientRemoved?.Invoke(this, e);
        }

        private void Run(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _server.PollEvents();
                Thread.Sleep(15);
            }
        }

        public EncryptedPeer? GetClientByID(int clientID)
        {
            if (_clients.Has(clientID))
            {
                return _clients[clientID];
            }

            return null;
        }

        public void Stop()
        {
            _tokenSource.Cancel();
            _server.Stop();
        }

        public void DisconnectAll()
        {
            _server.DisconnectAll();
        }

        public void SendToAll(NetDataWriter message)
        {
            foreach (var client in _clients.EstablishedList)
            {
                client.SendEncrypted(message, 0);
            }
        }

        public void StartListening(int port)
        {
            _server.Start(port);

            _listener.ConnectionRequestEvent += request => request.AcceptIfKey("ToFileServer");

            _listener.PeerConnectedEvent += peer =>
            {
                var client = new EncryptedPeer(peer);
                _clients.Add(client);

                Debug.WriteLine($"(Server_PeerConnectedEvent) New connection: {peer.EndPoint}");
            };

            _listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                _clients.Remove(peer.Id);

                Debug.WriteLine($"(Server_PeerDisconnectedEvent) Peer {peer.EndPoint} disconnected, info: {disconnectInfo.Reason}");
            };

            _listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                if (dataReader.AvailableBytes == 0)
                {
                    Debug.WriteLine($"(Server_NetworkReceiveEvent) Empty payload from {fromPeer.EndPoint}");

                    return;
                }

                if (_clients.Has(fromPeer.Id) &&
                    _clients[fromPeer.Id].IsSecurityEnabled)
                {
                    var data = _clients[fromPeer.Id].DecryptReceivedData(dataReader);
                    MessageReceived?.Invoke(this, new NetEventArgs(_clients[fromPeer.Id], data));
                }
                else
                if (!_clients.Has(fromPeer.Id))
                {
                    Debug.WriteLine($"(Server_NetworkReceiveEvent) Unknown client: {fromPeer.EndPoint}");
                }
                else
                if (!_clients[fromPeer.Id].IsSecurityEnabled &&
                    dataReader.TryGetBytesWithLength(out byte[] publicKey) &&
                    dataReader.TryGetBytesWithLength(out byte[] signaturePublicKey))
                {
                    _clients[fromPeer.Id].ApplyKeys(publicKey, signaturePublicKey);
                    _clients[fromPeer.Id].SendPublicKeys();

                    Debug.WriteLine($"(Server_NetworkReceiveEvent_Keys) Received keys from server {fromPeer.EndPoint}");
                }
                else
                {
                    Debug.WriteLine($"(Server_NetworkReceiveEvent) Unknown error with peer {fromPeer.EndPoint}" +
                        $"Is connection established: {_clients.Has(fromPeer.Id)}, " +
                        $"Security: {(_clients.Has(fromPeer.Id) ? _clients[fromPeer.Id].IsSecurityEnabled : "unknown")}");
                }

                dataReader.Recycle();
            };

            _listenTask.Start();
        }
    }
}
