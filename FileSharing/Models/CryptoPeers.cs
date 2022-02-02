using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections;
using System.Diagnostics;
using System.Net;

namespace FileSharing.Models
{
    public class CryptoPeers : IEnumerable<CryptoPeer>
    {
        private readonly ConcurrentDictionary<int, CryptoPeer> _cryptoPeers;

        public CryptoPeers()
        {
            _cryptoPeers = new ConcurrentDictionary<int, CryptoPeer>();
        }

        public event EventHandler<CryptoPeerEventArgs>? PeerAdded;
        public event EventHandler<CryptoPeerEventArgs>? PeerRemoved;

        public IEnumerable<CryptoPeer> List => _cryptoPeers.Values;
        public IEnumerable<CryptoPeer> EstablishedList =>
            _cryptoPeers.Values.Where(cryptoPeer => cryptoPeer.IsSecurityEnabled);

        public CryptoPeer this[int peerId]
        {
            get => _cryptoPeers[peerId];
            private set => _cryptoPeers[peerId] = value;
        }

        public bool Has(int peerId)
        {
            return _cryptoPeers.ContainsKey(peerId);
        }

        public bool Has(CryptoPeer cryptoPeer)
        {
            return _cryptoPeers.ContainsKey(cryptoPeer.Peer.Id);
        }

        public bool IsConnectedToEndPoint(IPEndPoint endPoint)
        {
            return _cryptoPeers.Values.Any(cryptoPeer =>
                cryptoPeer.Peer.EndPoint.Address.ToString() == endPoint.Address.ToString() &&
                cryptoPeer.Peer.EndPoint.Port == endPoint.Port);
        }

        public void Add(CryptoPeer cryptoPeer)
        {
            if (!Has(cryptoPeer.Peer.Id))
            {
                if (_cryptoPeers.TryAdd(cryptoPeer.Peer.Id, cryptoPeer))
                {
                    Debug.WriteLine("(CryptoPeers_Add) Adding peer " + cryptoPeer.Peer.EndPoint + " with id " + cryptoPeer.Peer.Id);

                    _cryptoPeers[cryptoPeer.Peer.Id].PeerDisconnected += OnCryptoPeerDisconnected;

                    PeerAdded?.Invoke(this, new CryptoPeerEventArgs(cryptoPeer.Peer.Id));
                }
            }
        }

        public void Remove(int peerID)
        {
            if (Has(peerID))
            {
                if (_cryptoPeers.TryRemove(peerID, out CryptoPeer? removedPeer) &&
                    removedPeer != null)
                {
                    removedPeer.PeerDisconnected -= OnCryptoPeerDisconnected;

                    PeerRemoved?.Invoke(this, new CryptoPeerEventArgs(peerID));
                }
            }
        }

        private void OnCryptoPeerDisconnected(object? sender, CryptoPeerEventArgs e)
        {
            PeerRemoved?.Invoke(this, e);
        }

        public IEnumerator<CryptoPeer> GetEnumerator()
        {
            return _cryptoPeers.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_cryptoPeers.Values).GetEnumerator();
        }
    }
}
