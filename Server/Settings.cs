using System;
using System.Collections.Generic;
using System.Net;

namespace Kzone.Signal.Server
{

    public class Settings
    {
        public int StreamBufferSize
        {
            get => _streamBufferSize;
            set
            {
                if (value < 1) throw new ArgumentException("Stream buffer size must be greater than zero.");
                _streamBufferSize = value;
            }
        }


        public int MaxProxiedStreamSize
        {
            get => _maxProxiedStreamSize;
            set
            {
                if (value < 1) throw new ArgumentException("MaxProxiedStreamSize must be greater than zero.");
                _maxProxiedStreamSize = value;
            }
        }


        public string PresharedKey
        {
            get => _preshareKey;
            set => _preshareKey = value;
        }

        public string PublicKey
        {
            get => _publicKey;
            set => _publicKey = value;
        }

        public int IdleClientTimeoutSeconds
        {
            get => _idleClientTimeoutSeconds;
            set
            {
                if (value < 0) throw new ArgumentException("IdleClientTimeoutSeconds must be zero or greater.");
                _idleClientTimeoutSeconds = value;
            }
        }


        public int MaxConnections
        {
            get => _maxConnections;
            set
            {
                if (value < 1) throw new ArgumentException("Max connections must be greater than zero.");
                _maxConnections = value;
            }
        }


        public List<string> PermittedIPs
        {
            get => _permittedIPs;
            set
            {
                if (value == null) _permittedIPs = new List<string>();
                else _permittedIPs = value;
            }
        }


        public List<string> BlockedIPs
        {
            get => _blockedIPs;
            set
            {
                if (value == null) _blockedIPs = new List<string>();
                else _blockedIPs = value;
            }
        }


        public bool NoDelay
        {
            get { return _noDelay; }
            set { _noDelay = value; }
        }


        public KeepaliveSettings Keepalive => _keepalive;


        public IPAddress ListenIp
        {
            get => _listenerIp;
            set => _listenerIp = value;
        }

        public int ListenPort
        {
            get => _listenerPort;
            set => _listenerPort = value;
        }



        public void SetListenner(string listenerIp, int listenerPort)
        {
            if (listenerPort < 1) throw new ArgumentOutOfRangeException(nameof(listenerPort));
            if (string.IsNullOrEmpty(listenerIp))
            {
                _listenerIp = IPAddress.Any;
            }
            else if (listenerIp.Equals("localhost") || listenerIp.Equals("127.0.0.1") || listenerIp.Equals("::1"))
            {
                _listenerIp = IPAddress.Loopback;
            }
            else
            {
                _listenerIp = IPAddress.Parse(listenerIp);
            }
            _listenerPort = listenerPort;
        }


        private readonly KeepaliveSettings _keepalive = new();
        private IPAddress _listenerIp = IPAddress.Loopback;
        private int _listenerPort = 9999;
        private bool _noDelay = false;
        private string _preshareKey = null;
        private string _publicKey = null;
        private int _streamBufferSize = 65535;
        private int _maxProxiedStreamSize = 67108864;

        private int _maxConnections = 65535;
        private int _idleClientTimeoutSeconds = 0;

        private List<string> _permittedIPs = new();
        private List<string> _blockedIPs = new();
    }
}
