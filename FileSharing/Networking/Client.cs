using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using LiteNetLib;
using LiteNetLib.Layers;

namespace FileSharing.Networking
{
    public sealed class Client
    {
        private readonly EventBasedNetListener _listener;
        private readonly XorEncryptLayer _xor;
        private readonly NetManager _client;
        private readonly EncryptedPeers _servers;
        private readonly List<string> _expectedPeers; //list of IP:port
        private readonly Task _listenTask;
        private readonly CancellationTokenSource _tokenSource;

        public Client()
        {
            _listener = new EventBasedNetListener();
            _xor = new XorEncryptLayer("VerySecretSymmetricXorPassword");
            _client = new NetManager(_listener, _xor);
            _client.ChannelsCount = Constants.ChannelsCount;
            _client.DisconnectTimeout = Constants.DisconnectionTimeout;
            _servers = new EncryptedPeers();
            _servers.PeerAdded += OnServerAdded;
            _servers.PeerRemoved += OnServerRemoved;
            _expectedPeers = new List<string>();
            _tokenSource = new CancellationTokenSource();
            var token = _tokenSource.Token;
            _listenTask = new Task(() => Run(token));
        }

        public event EventHandler<NetEventArgs>? MessageReceived;
        public event EventHandler<EncryptedPeerEventArgs>? ServerAdded;
        public event EventHandler<EncryptedPeerEventArgs>? ServerConnected;
        public event EventHandler<EncryptedPeerEventArgs>? ServerRemoved;

        public int LocalPort => _client.LocalPort;
        public byte ChannelsCount => _client.ChannelsCount;
        public IEnumerable<EncryptedPeer> Servers => _servers.List;

        private void OnServerAdded(object? sender, EncryptedPeerEventArgs e)
        {
            ServerAdded?.Invoke(this, e);
        }

        private void OnServerRemoved(object? sender, EncryptedPeerEventArgs e)
        {
            ServerRemoved?.Invoke(this, e);
        }

        private void Run(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _client.PollEvents();
                Thread.Sleep(15);
            }
        }

        public bool IsConnectedToServer(int serverID, out EncryptedPeer? result)
        {
            result = null;
            var server = _servers.Get(serverID);

            if (server != null &&
                server.IsSecurityEnabled)
            {
                result = server;

                return true;
            }

            return false;
        }

        public EncryptedPeer? GetServerByID(int serverID)
        {
            return _servers.Get(serverID);
        }

        public void Stop()
        {
            _tokenSource.Cancel();
            _client.Stop();
        }

        public void DisconnectFromServer(EncryptedPeer server)
        {
            server.Disconnect();
            _servers.Remove(server.Id);
        }

        public void DisconnectAll()
        {
            _client.DisconnectAll();
        }

        public void ConnectToServer(IPEndPoint serverAddress)
        {
            if (_servers.IsConnectedToEndPoint(serverAddress))
            {
                return;
            }

            _expectedPeers.Add(serverAddress.ToString());
            _client.Connect(serverAddress, "ToFileServer");
        }

        public void StartListening()
        {
            _client.Start();

            _listener.ConnectionRequestEvent += request => request.AcceptIfKey("ToFileClient");

            _listener.PeerConnectedEvent += peer =>
            {
                var peerString = peer.EndPoint.ToString();

                if (_expectedPeers.Contains(peerString))
                {
                    _expectedPeers.Remove(peer.EndPoint.ToString());

                    var server = new EncryptedPeer(peer);
                    _servers.Add(server);
                    server.SendPublicKeys();
                }
                else
                {
                    peer.Disconnect();
                }
            };

            _listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                _servers.Remove(peer.Id);
            };

            _listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                if (dataReader.AvailableBytes == 0)
                {
                    return;
                }

                var server = _servers.Get(fromPeer.Id);

                if (server != null &&
                    server.IsSecurityEnabled)
                {
                    var data = server.DecryptReceivedData(dataReader);

                    MessageReceived?.Invoke(this, new NetEventArgs(server, data));
                }
                else
                if (server != null &&
                    !server.IsSecurityEnabled &&
                    dataReader.TryGetBytesWithLength(out byte[] publicKey) &&
                    dataReader.TryGetBytesWithLength(out byte[] signaturePublicKey))
                {
                    server.ApplyKeys(publicKey, signaturePublicKey);
                    ServerConnected?.Invoke(this, new EncryptedPeerEventArgs(fromPeer.Id));
                }

                dataReader.Recycle();
            };

            _listenTask.Start();
        }
    }
}
