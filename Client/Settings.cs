using System;

namespace Kzone.Signal.Client
{

    public class Settings
    {
        private string _host = "localhost";
        private int _port = 9999;
        private int _autoReconnect = 0;
        private bool _noDelay = false;
        private string _preshareKey = null;
        private string _publicKey = null;
        private int _streamBufferSize = 65536;
        private int _maxProxiedStreamSize = 67108864;
        private int _connectTimeoutSeconds = 10;
        private int _idleServerTimeoutSeconds = 0;
        private int _localPort = 0;
        private int _channel = 0;


        public string Host
        {
            get => _host;
            set => _host = value;
        }

        public int Port
        {
            get => _port;
            set => _port = value;
        }

        internal int AutoReconnectSeconds
        {
            get => _autoReconnect;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException("AutoReconnect must be one or greater.");
                _autoReconnect = value;
            }
        }

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

        public int ConnectTimeoutSeconds
        {
            get => _connectTimeoutSeconds;
            set
            {
                if (value < 1) throw new ArgumentException("ConnectTimeoutSeconds must be greater than zero.");
                _connectTimeoutSeconds = value;
            }
        }


        public int IdleServerTimeoutSeconds
        {
            get => _idleServerTimeoutSeconds;
            set
            {
                if (value < 0) throw new ArgumentException("IdleClientTimeoutMs must be zero or greater.");
                _idleServerTimeoutSeconds = value;
            }
        }

        public int LocalPort
        {
            get => _localPort;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Valid values for LocalPort are 0, 1024-65535.");
                }
                else if (value > 0 && value < 1024)
                {
                    throw new ArgumentException("Valid values for LocalPort are 0, 1024-65535.");
                }
                else if (value > 65535)
                {
                    throw new ArgumentException("Valid values for LocalPort are 0, 1024-65535.");
                }

                _localPort = value;
            }
        }

        public bool NoDelay
        {
            get { return _noDelay; }
            set { _noDelay = value; }
        }


        public int Channel
        {
            get { return _channel; }
            set { _channel = value; }
        }

        public void EnableAutoReconnect(int seconds) => AutoReconnectSeconds = seconds;

    }
}
