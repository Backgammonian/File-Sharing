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
    public class Client
    {
        private readonly EventBasedNetListener _listener;
        private readonly XorEncryptLayer _xor;
        private readonly NetManager _client;
        private readonly CryptoPeers _servers;
        private Task _listenTask;
        private CancellationTokenSource _tokenSource;

        public Client()
        {
            _listener = new EventBasedNetListener();
            _xor = new XorEncryptLayer("HaveYouHeardOfTheHighElves");
            _client = new NetManager(_listener, _xor)
            {
                ChannelsCount = 8
            };
            _servers = new CryptoPeers();
            _servers.PeerAdded += OnServerUpdated;
            _servers.PeerRemoved += OnServerRemoved;
        }

        public event EventHandler<NetEventArgs> MessageReceived;
        public event EventHandler<CryptoPeerEventArgs> ServerUpdated;
        public event EventHandler<CryptoPeerEventArgs> ServerRemoved;

        public int LocalPort => _client.LocalPort;
        public byte ChannelsCount => _client.ChannelsCount;
        public IEnumerable<CryptoPeer> Servers => _servers.List;

        private void OnServerUpdated(object sender, CryptoPeerEventArgs e)
        {
            ServerUpdated?.Invoke(this, e);
        }

        private void OnServerRemoved(object sender, CryptoPeerEventArgs e)
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

        public void Stop()
        {
            _tokenSource.Cancel();
            _client.Stop();
        }

        public void DisconnectFromServer(CryptoPeer server)
        {
            server.Disconnect();
            _servers.Remove(server.Id);
        }

        public void DisconnectFromServer(string ip, int port)
        {
            if (_servers.Has(ip, port, out int Id))
            {
                _servers[Id].Disconnect();
                _servers.Remove(Id);
            }
        }

        public void ConnectToServer(string ip, int port)
        {
            DisconnectFromServer(ip, port); //Maybe shouldn't disconnect?

            var netPeer = _client.Connect(ip, port, "ToFileServer");
            var server = new CryptoPeer();
            server.ExpectConnectionFromServer(ip, port, netPeer.Id);
            _servers.Add(server);
        }

        public void SendToServer(CryptoPeer server, NetDataWriter message)
        {
            if (server.IsEstablished)
            {
                server.SendEncrypted(message);
            }
            else
            {
                Debug.WriteLine("(Client_SendToServer) Can't send encrypted data to server " + server.EndPoint);
            }
        }

        public void StartListening()
        {
            _client.Start();

            _listener.ConnectionRequestEvent += request => request.AcceptIfKey("ToFileClient");

            _listener.PeerConnectedEvent += peer =>
            {
                if (_servers[peer.Id].ApproveExpectedConnection(peer))
                {
                    Debug.WriteLine("(Client_PeerConnectedEvent) Receiving expected connection from server: {0}", peer.EndPoint);

                    _servers[peer.Id].ChangePeer(peer);
                    _servers[peer.Id].SendPublicKeys();

                    ServerUpdated?.Invoke(this, new CryptoPeerEventArgs(peer.Id));
                }
                else
                {
                    Debug.WriteLine("(Client_PeerConnectedEvent_Error) Connection from UNKNOWN server: {0}, disconnecting...", peer.EndPoint);

                    peer.Disconnect();
                }
            };

            _listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                _servers.Remove(peer.Id);
                ServerRemoved?.Invoke(this, new CryptoPeerEventArgs(peer.Id));

                Debug.WriteLine("(Client_PeerDisconnectedEvent) Server {0} disconnected, info: {1}",
                    peer.EndPoint,
                    disconnectInfo.Reason);
            };

            _listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                if (dataReader.AvailableBytes == 0)
                {
                    Debug.WriteLine("(Client_NetworkReceiveEvent) Empty payload from " + fromPeer.EndPoint);

                    return;
                }

                if (_servers.Has(fromPeer.Id) &&
                    _servers[fromPeer.Id].IsEstablished)
                {
                    var data = _servers[fromPeer.Id].DecryptReceivedData(dataReader);

                    MessageReceived?.Invoke(this, new NetEventArgs(_servers[fromPeer.Id], data));
                }
                else
                if (!_servers.Has(fromPeer.Id))
                {
                    Debug.WriteLine("(Client_NetworkReceiveEvent) No encrypted connection with any server");
                }
                else
                if (!_servers[fromPeer.Id].IsEstablished)
                {
                    Debug.WriteLine("(Client_NetworkReceiveEvent_Keys) Trying to get keys from server " + fromPeer.EndPoint);

                    try
                    {
                        var publicKey = new byte[dataReader.GetInt()];
                        dataReader.GetBytes(publicKey, publicKey.Length);
                        var signaturePublicKey = new byte[dataReader.GetInt()];
                        dataReader.GetBytes(signaturePublicKey, signaturePublicKey.Length);
                        _servers[fromPeer.Id].ApplyKeys(publicKey, signaturePublicKey);

                        Debug.WriteLine("(Client_NetworkReceiveEvent_Keys) Received keys from server " + _servers[fromPeer.Id].EndPoint);

                        ServerUpdated?.Invoke(this, new CryptoPeerEventArgs(fromPeer.Id));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("(Client_NetworkReceiveEvent_Keys_Error) Couldn't get keys from message. " + e);
                    }
                }
                else
                {
                    Debug.WriteLine("(Client_NetworkReceiveEvent) Unknown error with peer " + fromPeer.EndPoint);
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
