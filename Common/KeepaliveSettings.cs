﻿using System;

namespace Kzone.Signal
{

    public class KeepaliveSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable TCP-based keepalive probes.
        /// TCP keepalives are only supported in .NET Core and .NET Framework projects.  .NET Standard does not provide facilities to support TCP keepalives.
        /// </summary>
        public bool EnableTcpKeepAlives = false;

        /// <summary>
        /// TCP keepalive interval, i.e. the number of seconds a TCP connection will wait for a keepalive response before sending another keepalive probe.
        /// Default is 5 seconds.  Value must be greater than zero.
        /// </summary>
        public int TcpKeepAliveInterval
        {
            get
            {
                return _TcpKeepAliveInterval;
            }
            set
            {
                if (value < 1) throw new ArgumentException("TcpKeepAliveInterval must be greater than zero.");
                _TcpKeepAliveInterval = value;
            }
        }

        /// <summary>
        /// TCP keepalive time, i.e. the number of seconds a TCP connection will remain alive/idle before keepalive probes are sent to the remote. 
        /// Default is 5 seconds.  Value must be greater than zero.
        /// </summary>
        public int TcpKeepAliveTime
        {
            get
            {
                return _TcpKeepAliveTime;
            }
            set
            {
                if (value < 1) throw new ArgumentException("TcpKeepAliveTime must be greater than zero.");
                _TcpKeepAliveTime = value;
            }
        }

        /// <summary>
        /// TCP keepalive retry count, i.e. the number of times a TCP probe will be sent in effort to verify the connection.
        /// After the specified number of probes fail, the connection will be terminated.
        /// </summary>
        public int TcpKeepAliveRetryCount
        {
            get
            {
                return _TcpKeepAliveRetryCount;
            }
            set
            {
                if (value < 1) throw new ArgumentException("TcpKeepAliveRetryCount must be greater than zero.");
                _TcpKeepAliveRetryCount = value;
            }
        }

        #endregion

        #region Private-Members

        private int _TcpKeepAliveInterval = 5;
        private int _TcpKeepAliveTime = 5;
        private int _TcpKeepAliveRetryCount = 5;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public KeepaliveSettings()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
