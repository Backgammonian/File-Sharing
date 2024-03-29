﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Layers;
using LiteNetLib.Utils;
using Extensions;

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
            return _clients.Get(clientID);
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
            };

            _listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                _clients.Remove(peer.Id);
            };

            _listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                if (dataReader.AvailableBytes == 0)
                {
                    return;
                }

                var client = _clients.Get(fromPeer.Id);

                if (client != null &&
                    client.IsSecurityEnabled)
                {
                    var data = client.DecryptReceivedData(dataReader);

                    MessageReceived?.Invoke(this, new NetEventArgs(client, data));
                }
                else
                if (client != null &&
                    !client.IsSecurityEnabled &&
                    dataReader.TryGetBytesWithLength(out byte[] publicKey) &&
                    dataReader.TryGetBytesWithLength(out byte[] signaturePublicKey))
                {
                    client.ApplyKeys(publicKey, signaturePublicKey);
                    client.SendPublicKeys();
                }

                dataReader.Recycle();
            };

            _listenTask.Start();
        }
    }
}
