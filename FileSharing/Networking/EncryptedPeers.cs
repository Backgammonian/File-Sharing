﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;

namespace FileSharing.Networking
{
    public sealed class EncryptedPeers
    {
        private readonly ConcurrentDictionary<int, EncryptedPeer> _cryptoPeers;

        public EncryptedPeers()
        {
            _cryptoPeers = new ConcurrentDictionary<int, EncryptedPeer>();
        }

        public event EventHandler<EncryptedPeerEventArgs>? PeerAdded;
        public event EventHandler<EncryptedPeerEventArgs>? PeerRemoved;

        public IEnumerable<EncryptedPeer> List =>
            _cryptoPeers.Values.OrderBy(peer => peer.StartTime);
        public IEnumerable<EncryptedPeer> EstablishedList =>
            _cryptoPeers.Values.Where(peer => peer.IsSecurityEnabled).OrderBy(peer => peer.StartTime);

        public EncryptedPeer? Get(int peerId)
        {
            return Has(peerId) ? _cryptoPeers[peerId] : null;
        }

        public bool Has(int peerId)
        {
            return _cryptoPeers.ContainsKey(peerId);
        }

        public bool Has(EncryptedPeer cryptoPeer)
        {
            return _cryptoPeers.ContainsKey(cryptoPeer.Id);
        }

        public bool IsConnectedToEndPoint(IPEndPoint endPoint)
        {
            return _cryptoPeers.Values.Any(cryptoPeer =>
                cryptoPeer.EndPoint.Address.ToString() == endPoint.Address.ToString() &&
                cryptoPeer.EndPoint.Port == endPoint.Port);
        }

        public void Add(EncryptedPeer cryptoPeer)
        {
            if (!Has(cryptoPeer.Id) &&
                _cryptoPeers.TryAdd(cryptoPeer.Id, cryptoPeer))
            {
                _cryptoPeers[cryptoPeer.Id].PeerDisconnected += OnCryptoPeerDisconnected;
                PeerAdded?.Invoke(this, new EncryptedPeerEventArgs(cryptoPeer.Id));
            }
        }

        public void Remove(int peerID)
        {
            if (Has(peerID) &&
                _cryptoPeers.TryRemove(peerID, out EncryptedPeer? removedPeer) &&
                removedPeer != null)
            {
                removedPeer.PeerDisconnected -= OnCryptoPeerDisconnected;
                PeerRemoved?.Invoke(this, new EncryptedPeerEventArgs(peerID));
            }
        }

        private void OnCryptoPeerDisconnected(object? sender, EncryptedPeerEventArgs e)
        {
            PeerRemoved?.Invoke(this, e);
        }
    }
}
