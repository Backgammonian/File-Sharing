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
            _xor = new XorEncryptLayer("VerySecretSymmetricXorPassword");
            _client = new NetManager(_listener, _xor)
            {
                ChannelsCount = 8
            };
            _servers = new CryptoPeers();
            _servers.PeerAdded += OnServerAdded;
            _servers.PeerRemoved += OnServerRemoved;
        }

        public event EventHandler<NetEventArgs>? MessageReceived;
        public event EventHandler<CryptoPeerEventArgs>? ServerAdded;
        public event EventHandler<CryptoPeerEventArgs>? ServerRemoved;

        public int LocalPort => _client.LocalPort;
        public byte ChannelsCount => _client.ChannelsCount;
        public IEnumerable<CryptoPeer> Servers => _servers.List;

        private void OnServerAdded(object? sender, CryptoPeerEventArgs e)
        {
            ServerAdded?.Invoke(this, e);
        }

        private void OnServerRemoved(object? sender, CryptoPeerEventArgs e)
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
            if (server.Peer == null)
            {
                return;
            }

            server.Disconnect();
            _servers.Remove(server.Peer.Id);
        }

        public void ConnectToServer(string ip, int port)
        {
            var netPeer = _client.Connect(ip, port, "ToFileServer");
            if (netPeer.ConnectionState == ConnectionState.Connected)
            {
                Debug.WriteLine("Already connected to peer " + ip + ":" + port);

                return;
            }

            var server = new CryptoPeer();
            server.ExpectConnectionFromServer(netPeer);
            _servers.Add(server);
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

                    _servers[peer.Id].SetPeer(peer);
                    _servers[peer.Id].SendPublicKeys();
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
                    _servers[fromPeer.Id].IsSecurityEnabled)
                {
                    var data = _servers[fromPeer.Id].DecryptReceivedData(dataReader);

                    MessageReceived?.Invoke(this, new NetEventArgs(_servers[fromPeer.Id], data));
                }
                else
                if (!_servers.Has(fromPeer.Id))
                {
                    Debug.WriteLine("(Client_NetworkReceiveEvent) No encrypted connection with this server: " + fromPeer.EndPoint);
                }
                else
                if (!_servers[fromPeer.Id].IsSecurityEnabled)
                {
                    Debug.WriteLine("(Client_NetworkReceiveEvent_Keys) Trying to get keys from server " + fromPeer.EndPoint);

                    try
                    {
                        var publicKey = new byte[dataReader.GetInt()];
                        dataReader.GetBytes(publicKey, publicKey.Length);
                        var signaturePublicKey = new byte[dataReader.GetInt()];
                        dataReader.GetBytes(signaturePublicKey, signaturePublicKey.Length);
                        _servers[fromPeer.Id].ApplyKeys(publicKey, signaturePublicKey);

                        Debug.WriteLine("(Client_NetworkReceiveEvent_Keys) Received keys from server " + _servers[fromPeer.Id].Peer.EndPoint);
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
