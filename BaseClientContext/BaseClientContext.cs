using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Signal
{
    internal abstract class BaseClientContext : IBaseClientContext
    {
        protected string _header = "[BaseClient] ";
        protected int _maxSendBufferLength = 65536;
        protected TcpClient _client = null;
        protected Stream _dataStream = null;
        protected Task _dataReceiver = null;
        protected CancellationTokenSource _tokenSource = new();
        protected CancellationToken _token;
        protected Statistics _statistics = null;
        protected KeepaliveSettings _keepaliveSettings = null;
        protected DebugLogger _debugLogger = null; //set
        protected DateTime _lastActivity = DateTime.MinValue;

        private event EventHandler<ResponseResultReceivedArgs> _eventResponseReceived;

        internal Semaphore _writeLock = new();
        internal Semaphore _readLock = new();
        internal void HandleResponseReceived(object sender, Message msg, byte[] data)
            => _eventResponseReceived?.Invoke(sender, new ResponseResultReceivedArgs(msg, data));
        internal abstract Task DataReceiver();

        internal BaseClientContext(Statistics statistics, KeepaliveSettings keepaliveSettings, DebugLogger debugLogger)
        {
            _statistics = statistics;
            _keepaliveSettings = keepaliveSettings;
            _debugLogger = debugLogger;
            _token = _tokenSource.Token;
        }

        //
        #region PUBLIC SYNC SEND
        public bool Send(Header headerPacket, object obj = null)
        {
            try
            {
                byte[] data = obj == null ? (new byte[0]) : obj.Serialize();
                StreamCommon.BytesToStream(data, 0, out int contentLength, out Stream stream);
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                stream ??= new MemoryStream(new byte[0]);
                return SendInternal(new Message(headerPacket, contentLength, stream, MessageType.BroadcastPack, default, null), contentLength, stream);
            }
            catch (InvalidOperationException)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(Send) + " [exception] " + nameof(ConnectionException));
#if DEBUG
                throw new ConnectionException("Connection Error");
#elif RELEASE
                return false;
#endif
            }
            catch (NotSupportedException)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(Send) + " [exception] " + nameof(NotSupportedException));
#if DEBUG
                throw new NotSupportedException("This object not support in this protocol");
#elif RELEASE
                return false;
#endif
            }
            catch (NullReferenceException)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(Send) + " [exception] " + nameof(NullReferenceException));
#if DEBUG
                throw new NotSupportedException("Data is null");
#elif RELEASE
                return false;           
#endif
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(Send) + " [exception] " + nameof(Exception));
#if DEBUG
                throw new Exception(e.Message);
#elif RELEASE
                return false;
#endif
            }
        }

        public bool SendStream(Header headerPacket, Stream stream, long contentLength)
        {
            try
            {
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                stream ??= new MemoryStream(new byte[0]);
                return SendInternal(new Message(headerPacket, contentLength, stream, MessageType.StreamPack, default, null), contentLength, stream);
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(SendStream) + " [exception] " + nameof(Exception));
#if DEBUG
                throw new Exception(e.Message);
#elif RELEASE
                return false;
#endif
            }
        }
        #endregion
        //
        #region  PUBLIC PUBLIC SEND ASYNC
        public async Task<bool> SendAsync(Header header, object obj = null)
        {
            try
            {
                byte[] data = obj == null ? (new byte[0]) : obj.Serialize();
                data ??= new byte[0];
                StreamCommon.BytesToStream(data, 0, out int contentLength, out Stream stream);
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                stream ??= new MemoryStream(new byte[0]);
                return await SendInternalAsync(new Message(header, contentLength, stream, MessageType.BroadcastPack, default, null), contentLength, stream).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(SendAsync) + " [exception] " + nameof(ConnectionException));
#if DEBUG
                throw new ConnectionException("Connection Error");
#elif RELEASE
                return false;
#endif
            }
            catch (NotSupportedException)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(SendAsync) + " [exception] " + nameof(NotSupportedException));
#if DEBUG
                throw new NotSupportedException("This object not support in this protocol");
#elif RELEASE
                return false;
#endif
            }
            catch (NullReferenceException)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(SendAsync) + " [exception] " + nameof(NullReferenceException));
#if DEBUG
                throw new NotSupportedException("Data is null");
#elif RELEASE
                return false;           
#endif
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(SendAsync) + " [exception] " + nameof(Exception));
#if DEBUG
                throw new Exception(e.Message);
#elif RELEASE
                return false;
#endif
            }

        }

        public async Task<bool> SendStreamAsync(Header headerPacket, long contentLength, Stream stream)
        {
            try
            {
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                stream ??= new MemoryStream(new byte[0]);
                return await SendInternalAsync(new Message(headerPacket, contentLength, stream, MessageType.StreamPack, default, null), contentLength, stream).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(SendStreamAsync) + " [exception] " + nameof(Exception));
#if DEBUG
                throw new Exception(e.Message);
#elif RELEASE
                return false;
#endif
            }
        }
        #endregion
        //
        #region PUBLIC  SEND AND WAIT RESULT


        public async Task<Response> RpcRequest(int timeoutMs, Header header, object obj = null)
        {

            if (header == null) throw new ArgumentNullException("header is null");
            try
            {
                if (timeoutMs < 1000) throw new ArgumentException("Timeout milliseconds must be 1000 or greater.");
                byte[] data = obj == null ? (new byte[0]) : obj.Serialize();
                StreamCommon.BytesToStream(data, 0, out int contentLength, out Stream stream);
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                if (timeoutMs < 1000) throw new ArgumentException("Timeout milliseconds must be 1000 or greater.");
                stream ??= new MemoryStream(new byte[0]);
                DateTime expiration = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                return await SendAndWaitInternalAsync(new Message(header, contentLength, stream, MessageType.RequestPack, expiration, Guid.NewGuid().ToString()), timeoutMs, contentLength, stream);
            }
            catch (InvalidOperationException)
            {
                throw new ConnectionException("Connection Error");
            }
            catch (NotSupportedException)
            {
                throw new NotSupportedException("This object not support for protocol");
            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Data is null");
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("Too slow ... server not response.");
            }
            catch (TaskCanceledException e)
            {
                throw new TaskCanceledException(e.Message);
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + nameof(RpcRequest) + " [exception] " + e.Message);
                throw new Exception(e.Message);
            }
        }


        public async Task<Response<T>> RpcRequest<T>(int timeOut, Header header, object data = null)
        {
            try
            {
                var result = await RpcRequest(timeOut, header, data).ConfigureAwait(false);
                if (result.Header == null)
                {
                    return new Response<T>(ResponseStatusCode.HeaderNull, default);
                }
                var replyStatus = result.Header.GetStatusCode();
                return replyStatus == ResponseStatusCode.Ok
                    ? new Response<T>(replyStatus, result.Data.Deserialize<T>())
                    : new Response<T>(replyStatus, default);
            }
            catch (ConnectionException)
            {
                return new Response<T>(ResponseStatusCode.ConnectionError, default);
            }
            catch (NotSupportedException)
            {
                return new Response<T>(ResponseStatusCode.Unsupport, default);
            }
            catch (TimeoutException)
            {
                return new Response<T>(ResponseStatusCode.Timeout, default);
            }
            catch (NullReferenceException)
            {
                return new Response<T>(ResponseStatusCode.NullValue, default);
            }
            catch (TaskCanceledException e)
            {
                return new Response<T>(ResponseStatusCode.TaskCancel, default);
            }
            catch (Exception)
            {
                return new Response<T>(ResponseStatusCode.Unknow, default);
            }
        }

        public async Task<Response<T>> RpcRequest<T, TEnum>(int timeOut, TEnum dataTag, object data = null) where TEnum : struct
        {
            return await RpcRequest<T>(timeOut, HeaderEx.BuildTag(dataTag), data).ConfigureAwait(false);
        }

        public async Task<ResponseStatusCode> RpcRequest<TEnum>(int timeOut, TEnum dataTag, object data = null) where TEnum : struct
        {
            try
            {
                var result = await RpcRequest(timeOut, HeaderEx.BuildTag(dataTag), data).ConfigureAwait(false);
                return result.Header.GetStatusCode();
            }
            catch (ConnectionException)
            {
                return ResponseStatusCode.ConnectionError;
            }
            catch (NotSupportedException)
            {
                return ResponseStatusCode.Unsupport;
            }
            catch (TimeoutException)
            {
                return ResponseStatusCode.Timeout;
            }
            catch (NullReferenceException)
            {
                return ResponseStatusCode.NullValue;
            }
            catch (TaskCanceledException e)
            {
                return ResponseStatusCode.TaskCancel;
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine($"{e.Message}{Environment.NewLine}{e.StackTrace}");
#endif
                return ResponseStatusCode.Unknow;
            }

        }
        public async Task<Response<T>> RpcRequest<T, TEnum>(TEnum dataTag, object data = null) where TEnum : struct
        {
            return await RpcRequest<T, TEnum>(30000, dataTag, data).ConfigureAwait(false);
        }

        public async Task<ResponseStatusCode> RpcRequest<TEnum>(TEnum dataTag, object data = null) where TEnum : struct
        {
            return await RpcRequest(30000, dataTag, data).ConfigureAwait(false);
        }

        #endregion
        //
        #region PUBLIC RESPONSE RESULT WHEN RECEIVED
        public Task<Response> Ok(object data = null) => Response(ResponseStatusCode.Ok, data);
        public Task<Response> Authorize(object data = null) => Response(ResponseStatusCode.Authorize, data);
        public Task<Response> UnAuthorize(object data = null) => Response(ResponseStatusCode.Unauthorize, data);
        public Task<Response> NotFound(object data = null) => Response(ResponseStatusCode.NotFound, data);
        public Task<Response> BadRequest(object data = null) => Response(ResponseStatusCode.BadRequest, data);
        public Task<Response> SessionExpired(object data = null) => Response(ResponseStatusCode.SessionExpired, data);
        public Task<Response> Newest(object data = null) => Response(ResponseStatusCode.Newest, data);
        public Task<Response> OutOfDate(object data = null) => Response(ResponseStatusCode.OutOfDate, data);
        public Task<Response> Unsupport(object data = null) => Response(ResponseStatusCode.Unsupport, data);
        public Task<Response> UnAvailable(object data = null) => Response(ResponseStatusCode.UnAvailable, data);
        public Task<Response> Block(object data = null) => Response(ResponseStatusCode.Block, data);
        public Task<Response> Reject(object data = null) => Response(ResponseStatusCode.Reject, data);
        public Task<Response> SessionFull(object data = null) => Response(ResponseStatusCode.SessionFull, data);
        public Task<Response> Conflict(object data = null) => Response(ResponseStatusCode.Conflict, data);
        public Task<Response> Response(ResponseStatusCode reponseStatus, object data)
        {
#if NET40

            return TaskEx.FromResult(new Response(HeaderEx.BuildResponse(reponseStatus), data));
#else
            return Task.FromResult(new Response(HeaderEx.BuildResponse(reponseStatus), data));
#endif
        }
        #endregion
        //
        #region INTERNAL SEND

        internal bool SendInternal(Message msg, long contentLength, Stream stream)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            if (contentLength > 0 && (stream == null || !stream.CanRead))
            {
                throw new ArgumentException("Cannot read from supplied stream.");
            }
            _writeLock.Wait(_token);

            try
            {
                SendHeaders(msg);
                SendDataStream(contentLength, stream);

                _statistics.IncrementSentMessages();
                _statistics.AddSentBytes(contentLength);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Error,
                    _header + "failed to write message: " +
                    Environment.NewLine +
                    e.Message);

                _debugLogger.ExceptionRecord?.Invoke(e);
                return false;
            }
            finally
            {
                _writeLock?.Release();
            }
        }

        internal async Task<bool> SendInternalAsync(Message msg, long contentLength, Stream stream)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            if (contentLength > 0 && (stream == null || !stream.CanRead))
            {
                throw new ArgumentException("Cannot read from supplied stream.");
            }

            await _writeLock.WaitAsync(_token).ConfigureAwait(false);

            try
            {
                await SendHeadersAsync(msg, _token).ConfigureAwait(false);
                await SendDataStreamAsync(contentLength, stream, _token).ConfigureAwait(false);

                _statistics.IncrementSentMessages();
                _statistics.AddSentBytes(contentLength);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Error,
                    _header + "failed to write message: " +
                    Environment.NewLine +
                    e.Message);

                _debugLogger.ExceptionRecord?.Invoke(e);
                return false;
            }
            finally
            {
                _writeLock?.Release();
            }
        }

        internal async Task<Response> SendAndWaitInternalAsync(Message msg, int timeoutMs, long contentLength, Stream stream)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            if (contentLength > 0 && (stream == null || !stream.CanRead))
            {
                throw new ArgumentException("Cannot read from supplied stream.");
            }
            await _writeLock.WaitAsync(_token).ConfigureAwait(false);
            Response ret = null;
            var responsed = new AsyncAutoResetEvent(false);
            void handler(object sender, ResponseResultReceivedArgs e)
            {
                if (e.Message.ConversationGuid == msg.ConversationGuid)
                {
                    ret = new Response(e.Message.Expiration, e.Message.Header, e.BytesData);
                    responsed.Set();
                }
            }
            // Subscribe                
            _eventResponseReceived += handler;
            try
            {
                await SendHeadersAsync(msg, _token).ConfigureAwait(false);
                await SendDataStreamAsync(contentLength, stream, _token).ConfigureAwait(false);

                _statistics.IncrementSentMessages();
                _statistics.AddSentBytes(contentLength);
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Error,
                    _header + "failed to write message,due to exception: " +
                    Environment.NewLine +
                    e.Message);

                _debugLogger.ExceptionRecord?.Invoke(e);
                throw new UnknowException(e.Message);
            }
            finally
            {
                _writeLock?.Release();
            }
            var waitResult = await responsed.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), _token).ConfigureAwait(false);
            try
            {
                if (waitResult)
                {
                    return ret;
                }
                if (_token.IsCancellationRequested)
                {
                    _debugLogger.Logger?.Invoke(Severity.Error, _header + "Task has been canceled.");
                    throw new TaskCanceledException("Task has been canceled.");
                }
                _debugLogger.Logger?.Invoke(Severity.Error, _header + "synchronous response not received within the timeout window");
                throw new TimeoutException("A response to a synchronous request was not received within the timeout window.");
            }
            finally
            {
                // Unsubscribe  
                _eventResponseReceived -= handler;
            }
        }

        #endregion
        //
        #region SEND HEADER
        private void SendHeaders(Message msg)
        {
            byte[] headerBytes = MessageBuilder.GetHeaderBytes(msg);
            _dataStream.Write(headerBytes, 0, headerBytes.Length);
            _dataStream.Flush();
        }

        private async Task SendHeadersAsync(Message msg, CancellationToken token)
        {
            byte[] headerBytes = MessageBuilder.GetHeaderBytes(msg);
            await _dataStream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);
            await _dataStream.FlushAsync(token).ConfigureAwait(false);
        }
        #endregion
        //
        #region SEND STREAM
        private void SendDataStream(long contentLength, Stream stream)
        {
            try
            {
                if (contentLength <= 0) return;

                long bytesRemaining = contentLength;
                int bytesRead = 0;

                // Tạo buffer cố định một lần
                byte[] buffer = new byte[_maxSendBufferLength];

                while (bytesRemaining > 0)
                {
                    // Điều chỉnh kích thước của buffer nếu bytesRemaining nhỏ hơn _maxSendBufferLength
                    int bufferLength = (int)Math.Min(bytesRemaining, _maxSendBufferLength);

                    // Đọc từ stream vào buffer
                    bytesRead = stream.Read(buffer, 0, bufferLength);
                    if (bytesRead > 0)
                    {
                        // Ghi buffer vào _dataStream
                        _dataStream.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                    }
                    else
                    {
                        break; // Không còn dữ liệu để đọc
                    }
                }
            }
            finally
            {
                // Nếu sở hữu stream, hãy gọi Dispose ở đây
                stream?.Dispose();
                // Đảm bảo flush _dataStream
                _dataStream?.Flush();
            }
        }
        private async Task SendDataStreamAsync(long contentLength, Stream stream, CancellationToken token)
        {
            try
            {
                if (contentLength <= 0) return;
               
                long bytesRemaining = contentLength;
                int bytesRead = 0;

                // Tạo buffer cố định một lần với kích thước tối đa
                byte[] buffer = new byte[_maxSendBufferLength];

                while (bytesRemaining > 0)
                {
                    // Nếu bytesRemaining nhỏ hơn buffer, chỉ đọc đúng số byte còn lại
                    int bufferLength = (int)Math.Min(bytesRemaining, _maxSendBufferLength);

                    // Đọc từ stream vào buffer
                    bytesRead = await stream.ReadAsync(buffer, 0, bufferLength, token);
                    if (bytesRead > 0)
                    {
                        // Ghi buffer vào _dataStream
                        await _dataStream.WriteAsync(buffer, 0, bytesRead, token);

                        // Giảm số byte còn lại
                        bytesRemaining -= bytesRead;
                    }
                    else
                    {
                        break; // Không còn dữ liệu để đọc
                    }
                }

            }
            finally
            {
                stream?.Dispose();
                await _dataStream.FlushAsync(token).ConfigureAwait(false);
            }
        }


        #endregion
        //
        #region KEEP ALIVE
        protected void EnableKeepalives()
        {
            try
            {
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER

                // NETCOREAPP3_1_OR_GREATER catches .NET 5.0

                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                _client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _keepaliveSettings.TcpKeepAliveTime);
                _client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _keepaliveSettings.TcpKeepAliveInterval);
                _client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _keepaliveSettings.TcpKeepAliveRetryCount);

#elif NETFRAMEWORK

                // .NET Framework expects values in milliseconds

                byte[] keepAlive = new byte[12];
                Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes((uint)(_keepaliveSettings.TcpKeepAliveTime * 1000)), 0, keepAlive, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes((uint)(_keepaliveSettings.TcpKeepAliveInterval * 1000)), 0, keepAlive, 8, 4);
                _client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
            }
            catch (Exception)
            {
                _debugLogger.Logger?.Invoke(Severity.Error, _header + "keepalives not supported on this platform, disabled");
                _keepaliveSettings.EnableTcpKeepAlives = false;
            }
        }


    }
    #endregion
}
