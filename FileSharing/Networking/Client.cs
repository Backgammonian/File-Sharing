﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Layers;

namespace FileSharing.Networking
{
    public class Client
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

        public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
        public event AsyncEventHandler<NetEventArgs>? MessageReceived;
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

        public bool IsConnectedToServer(int serverID, out EncryptedPeer? server)
        {
            if (_servers.Has(serverID) &&
                _servers[serverID].IsSecurityEnabled)
            {
                server = _servers[serverID];

                return true;
            }

            server = null;

            return false;
        }

        public EncryptedPeer? GetServerByID(int serverID)
        {
            if (_servers.Has(serverID))
            {
                return _servers[serverID];
            }

            return null;
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
                Debug.WriteLine("Already connected to server " + serverAddress);

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
                    Debug.WriteLine($"(Client_PeerConnectedEvent) Receiving expected connection from server: {peer.EndPoint}");

                    _expectedPeers.Remove(peer.EndPoint.ToString());

                    var server = new EncryptedPeer(peer);
                    _servers.Add(server);
                    _servers[server.Id].SendPublicKeys();
                }
                else
                {
                    Debug.WriteLine($"(Client_PeerConnectedEvent_Error) Connection from UNKNOWN server: {peer.EndPoint}, disconnecting...");

                    peer.Disconnect();
                }
            };

            _listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                _servers.Remove(peer.Id);

                Debug.WriteLine($"(Client_PeerDisconnectedEvent) Server {peer.EndPoint} disconnected, info: {disconnectInfo.Reason}");
            };

            _listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                if (dataReader.AvailableBytes == 0)
                {
                    Debug.WriteLine($"(Client_NetworkReceiveEvent) Empty payload from {fromPeer.EndPoint}");

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
                    Debug.WriteLine($"(Client_NetworkReceiveEvent) No encrypted connection with this server: {fromPeer.EndPoint}");
                }
                else
                if (!_servers[fromPeer.Id].IsSecurityEnabled &&
                    dataReader.TryGetBytesWithLength(out byte[] publicKey) &&
                    dataReader.TryGetBytesWithLength(out byte[] signaturePublicKey))
                {
                    _servers[fromPeer.Id].ApplyKeys(publicKey, signaturePublicKey);
                    ServerConnected?.Invoke(this, new EncryptedPeerEventArgs(fromPeer.Id));

                    Debug.WriteLine($"(Client_NetworkReceiveEvent_Keys) Received keys from server {fromPeer.EndPoint}");
                }
                else
                {
                    Debug.WriteLine($"(Client_NetworkReceiveEvent) Unknown error with peer {fromPeer.EndPoint}" +
                        $"Is connection established: {_servers.Has(fromPeer.Id)}, " +
                        $"Security: {(_servers.Has(fromPeer.Id) ? _servers[fromPeer.Id].IsSecurityEnabled : "unknown")}");
                }

                dataReader.Recycle();
            };

            _listenTask.Start();
        }
    }
}
