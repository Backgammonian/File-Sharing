using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSharing.Models
{
    public class CryptoPeers
    {
        private readonly Dictionary<int, CryptoPeer> _cryptoPeers;

        public CryptoPeers()
        {
            _cryptoPeers = new Dictionary<int, CryptoPeer>();
        }

        public event EventHandler<CryptoPeerEventArgs> PeerAdded;
        public event EventHandler<CryptoPeerEventArgs> PeerRemoved;

        public IEnumerable<CryptoPeer> List => _cryptoPeers.Values;

        public IEnumerable<CryptoPeer> EstablishedList =>
            _cryptoPeers.Values.Where(cryptoChannel => cryptoChannel.IsEstablished);

        public CryptoPeer this[int peerId]
        {
            get { return _cryptoPeers[peerId]; }
            private set { _cryptoPeers[peerId] = value; }
        }

        public bool Has(int peerId)
        {
            return _cryptoPeers.ContainsKey(peerId);
        }

        public bool Has(CryptoPeer cryptoPeer)
        {
            return _cryptoPeers.ContainsKey(cryptoPeer.Id);
        }

        public bool Has(string ip, int port, out int peerId)
        {
            try
            {
                var endPoint = ip + ":" + port;
                var peer = _cryptoPeers.Values.Single(peer => peer.Peer.EndPoint.ToString() == endPoint);
                peerId = peer.Id;
                return true;
            }
            catch (Exception)
            {
                peerId = -1;
                return false;
            }
        }

        public void Add(CryptoPeer cryptoChannel)
        {
            if (!Has(cryptoChannel.Id))
            {
                _cryptoPeers.Add(cryptoChannel.Id, cryptoChannel);
                PeerAdded?.Invoke(this, new CryptoPeerEventArgs(cryptoChannel.Id));
            }
        }

        public void Remove(int peerId)
        {
            if (Has(peerId))
            {
                _cryptoPeers.Remove(peerId, out _);
                PeerRemoved?.Invoke(this, new CryptoPeerEventArgs(peerId));
            }
        }
    }
}
