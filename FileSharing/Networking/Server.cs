using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Layers;
using FileSharing.Models;
using FileSharing.Utils;

namespace FileSharing.Networking
{
    public class Server
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

        public void SendToAll(SimpleWriter message)
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
                Debug.WriteLine("(Server_PeerConnectedEvent) New connection: {0}", peer.EndPoint);

                var client = new EncryptedPeer(peer);
                _clients.Add(client);
            };

            _listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                _clients.Remove(peer.Id);

                Debug.WriteLine("(Server_PeerDisconnectedEvent) Peer {0} disconnected, info: {1}",
                    peer.EndPoint,
                    disconnectInfo.Reason);
            };

            _listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                if (dataReader.AvailableBytes == 0)
                {
                    Debug.WriteLine("(Server_NetworkReceiveEvent) Empty payload from " + fromPeer.EndPoint);

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
                    Debug.WriteLine("(Server_NetworkReceiveEvent) Unknown client: " + fromPeer.EndPoint);
                }
                else
                if (!_clients[fromPeer.Id].IsSecurityEnabled)
                {
                    Debug.WriteLine("(Client_NetworkReceiveEvent_Keys) Trying to get keys from server " + fromPeer.EndPoint);

                    if (dataReader.TryGetBytesWithLength(out byte[] publicKey) &&
                        dataReader.TryGetBytesWithLength(out byte[] signaturePublicKey))
                    {
                        _clients[fromPeer.Id].ApplyKeys(publicKey, signaturePublicKey);
                        _clients[fromPeer.Id].SendPublicKeys();

                        Debug.WriteLine("(Client_NetworkReceiveEvent_Keys) Received keys from server " + fromPeer.EndPoint);
                    }
                    else
                    {
                        Debug.WriteLine("(Client_NetworkReceiveEvent_Keys_Error) Couldn't get keys from client " + fromPeer.EndPoint);
                    }
                }
                else
                {
                    Debug.WriteLine("(Client_NetworkReceiveEvent) Unknown error with peer " +
                        fromPeer.EndPoint + " " +
                        "Client connected: " + _clients.Has(fromPeer.Id) + " " +
                        (_clients.Has(fromPeer.Id) ? "Security: " + _clients[fromPeer.Id].IsSecurityEnabled : "no security"));
                }

                dataReader.Recycle();
            };

            _listenTask.Start();
        }
    }
}
