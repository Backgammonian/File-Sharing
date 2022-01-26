using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Layers;
using LiteNetLib.Utils;
using FileSharing.Models;

namespace FileSharing.Networking
{
    public class Server
    {
        private readonly EventBasedNetListener _listener;
        private readonly XorEncryptLayer _xor;
        private readonly NetManager _server;
        private readonly CryptoPeers _clients;
        private readonly int _port;
        private Task _listenTask;
        private CancellationTokenSource _tokenSource;

        public Server(int port)
        {
            _listener = new EventBasedNetListener();
            _xor = new XorEncryptLayer("VerySecretSymmetricXorPassword");
            _server = new NetManager(_listener, _xor)
            {
                ChannelsCount = 8
            };
            _port = port;
            _clients = new CryptoPeers();
            _clients.PeerAdded += OnClientAdded;
            _clients.PeerRemoved += OnClientRemoved;
        }

        public event EventHandler<NetEventArgs>? MessageReceived;
        public event EventHandler<CryptoPeerEventArgs>? ClientAdded;
        public event EventHandler<CryptoPeerEventArgs>? ClientRemoved;

        public int LocalPort => _server.LocalPort;
        public byte ChannelsCount => _server.ChannelsCount;
        public IEnumerable<CryptoPeer> Clients => _clients.List;

        private void OnClientAdded(object? sender, CryptoPeerEventArgs e)
        {
            ClientAdded?.Invoke(this, e);
        }

        private void OnClientRemoved(object? sender, CryptoPeerEventArgs e)
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

        public void Stop()
        {
            _tokenSource.Cancel();
            _server.Stop();
        }

        public void DisconnectAll()
        {
            _server.DisconnectAll();
        }

        public void SendToAll(NetDataWriter netDataWriter)
        {
            foreach (var client in _clients.EstablishedList)
            {
                client.SendEncrypted(netDataWriter);
            }
        }

        public void StartListening()
        {
            _server.Start(_port);

            _listener.ConnectionRequestEvent += request => request.AcceptIfKey("ToFileServer");

            _listener.PeerConnectedEvent += peer =>
            {
                Debug.WriteLine("(Server_PeerConnectedEvent) New connection: {0}", peer.EndPoint);

                var client = new CryptoPeer();
                client.ChangePeer(peer);
                _clients.Add(client);
            };

            _listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                Debug.WriteLine("(Server_PeerDisconnectedEvent) Peer {0} disconnected, info: {1}",
                    peer.EndPoint,
                    disconnectInfo.Reason);

                _clients.Remove(peer.Id);
            };

            _listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                if (dataReader.AvailableBytes == 0)
                {
                    Debug.WriteLine("(Server_NetworkReceiveEvent) Empty payload from " + fromPeer.EndPoint);

                    return;
                }

                if (_clients.Has(fromPeer.Id) &&
                    _clients[fromPeer.Id].IsEstablished)
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
                if (!_clients[fromPeer.Id].IsEstablished)
                {
                    Debug.WriteLine("(Server_NetworkReceiveEvent_Keys) Trying to get keys from client " + fromPeer.EndPoint);

                    try
                    {
                        var publicKey = new byte[dataReader.GetInt()];
                        dataReader.GetBytes(publicKey, publicKey.Length);
                        var signaturePublicKey = new byte[dataReader.GetInt()];
                        dataReader.GetBytes(signaturePublicKey, signaturePublicKey.Length);
                        _clients[fromPeer.Id].ApplyKeys(publicKey, signaturePublicKey);
                        _clients[fromPeer.Id].SendPublicKeys();

                        Debug.WriteLine("(Server_NetworkReceiveEvent_Keys) Received keys from client " + _clients[fromPeer.Id].EndPoint);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("(Server_NetworkReceiveEvent_Keys_Error) Couldn't get keys from message: " + e);
                    }
                }
                else
                {
                    Debug.WriteLine("(Server_NetworkReceiveEvent) Unknown error with peer: " + fromPeer.EndPoint);
                }

                dataReader.Recycle();
            };

            _tokenSource = new CancellationTokenSource();
            var token = _tokenSource.Token;
            _listenTask = new Task(() => Run(token));
            _listenTask.Start();
        }
    }
}
