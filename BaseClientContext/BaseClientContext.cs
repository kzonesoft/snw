using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

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

        // Thay thế event bằng ConcurrentDictionary để quản lý các yêu cầu đang chờ
        private ConcurrentDictionary<string, TaskCompletionSource<Response>> _pendingRequests =
            new ConcurrentDictionary<string, TaskCompletionSource<Response>>();

        internal Semaphore _writeLock = new();
        internal Semaphore _readLock = new();

        // Phương thức xử lý phản hồi nhận được
        internal void HandleResponseReceived(object sender, Message msg, byte[] data)
        {
            if (msg.ConversationGuid != null &&
                _pendingRequests.TryRemove(msg.ConversationGuid, out var tcs))
            {
                tcs.TrySetResult(new Response(msg.Expiration, msg.Header, data));
            }
        }

        internal abstract Task DataReceiver();

        internal BaseClientContext(Statistics statistics, KeepaliveSettings keepaliveSettings, DebugLogger debugLogger)
        {
            _statistics = statistics;
            _keepaliveSettings = keepaliveSettings;
            _debugLogger = debugLogger;
            _token = _tokenSource.Token;
        }

        #region EXCEPTION HANDLING HELPERS

        private bool HandleSendException(Exception ex, string methodName)
        {
            switch (ex)
            {
                case InvalidOperationException:
                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + methodName + " [exception] " + nameof(ConnectionException));
#if DEBUG
                    throw new ConnectionException("Connection Error");
#elif RELEASE
                    return false;
#endif
                case NotSupportedException:
                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + methodName + " [exception] " + nameof(NotSupportedException));
#if DEBUG
                    throw new NotSupportedException("This object not support in this protocol");
#elif RELEASE
                    return false;
#endif
                case NullReferenceException:
                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + methodName + " [exception] " + nameof(NullReferenceException));
#if DEBUG
                    throw new NotSupportedException("Data is null");
#elif RELEASE
                    return false;
#endif
                default:
                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + methodName + " [exception] " + nameof(Exception));
#if DEBUG
                    throw new Exception(ex.Message);
#elif RELEASE
                    return false;
#endif
            }
        }

        private Task<bool> HandleSendAsyncException(Exception ex, string methodName)
        {
            switch (ex)
            {
                case InvalidOperationException:
                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + methodName + " [exception] " + nameof(ConnectionException));
#if DEBUG
                    throw new ConnectionException("Connection Error");
#elif RELEASE
#if NET40
                    return TaskEx.FromResult(false);
#else
                    return Task.FromResult(false);
#endif
#endif
                case NotSupportedException:
                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + methodName + " [exception] " + nameof(NotSupportedException));
#if DEBUG
                    throw new NotSupportedException("This object not support in this protocol");
#elif RELEASE
#if NET40
                    return TaskEx.FromResult(false);
#else
                    return Task.FromResult(false);
#endif
#endif
                case NullReferenceException:
                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + methodName + " [exception] " + nameof(NullReferenceException));
#if DEBUG
                    throw new NotSupportedException("Data is null");
#elif RELEASE
#if NET40
                    return TaskEx.FromResult(false);
#else
                    return Task.FromResult(false);
#endif
#endif
                default:
                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + " method " + methodName + " [exception] " + nameof(Exception));
#if DEBUG
                    throw new Exception(ex.Message);
#elif RELEASE
#if NET40
                    return TaskEx.FromResult(false);
#else
                    return Task.FromResult(false);
#endif
#endif
            }
        }



        private ResponseStatusCode HandleRpcRequestException(Exception ex)
        {
            if (ex is ConnectionException)
                return ResponseStatusCode.ConnectionError;
            if (ex is NotSupportedException)
                return ResponseStatusCode.Unsupport;
            if (ex is TimeoutException)
                return ResponseStatusCode.Timeout;
            if (ex is NullReferenceException)
                return ResponseStatusCode.NullValue;
            if (ex is TaskCanceledException)
                return ResponseStatusCode.TaskCancel;

            return ResponseStatusCode.Unknown;
        }

        #endregion

        #region PUBLIC SYNC SEND

        public bool Send(Header headerPacket, object obj = null)
        {
            try
            {
                StreamCommon.ObjectToStream(obj, out int contentLength, out Stream stream);
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                stream ??= new MemoryStream(new byte[0]);
                return SendInternal(new Message(headerPacket, contentLength, stream, MessageType.BroadcastPack, default, null), contentLength, stream);
            }
            catch (Exception e)
            {
                return HandleSendException(e, nameof(Send));
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

        #region PUBLIC SEND ASYNC
        public async Task<bool> SendAsync(Header header, object obj = null)
        {
            try
            {
                StreamCommon.ObjectToStream(obj, out int contentLength, out Stream stream);
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                stream ??= new MemoryStream(new byte[0]);
                return await SendInternalAsync(new Message(header, contentLength, stream, MessageType.BroadcastPack, default, null), contentLength, stream).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                return await HandleSendAsyncException(e, nameof(SendAsync));
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

        #region PUBLIC SEND AND WAIT RESULT
        public async Task<Response> RpcRequest(int timeoutMs, Header header, object obj = null)
        {
            if (header == null) throw new ArgumentNullException("header is null");
            try
            {
                if (timeoutMs < 1000) throw new ArgumentException("Timeout milliseconds must be 1000 or greater.");
                StreamCommon.ObjectToStream(obj, out int contentLength, out Stream stream);
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                stream ??= new MemoryStream(new byte[0]);
                DateTime expiration = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                string conversationGuid = Guid.NewGuid().ToString();
                return await SendAndWaitInternalAsync(new Message(header, contentLength, stream, MessageType.RequestPack, expiration, conversationGuid), timeoutMs, contentLength, stream).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (e is TaskCanceledException) throw new TaskCanceledException(e.Message);
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
                    ? new Response<T>(replyStatus, result.BytesData.Deserialize<T>())
                    : new Response<T>(replyStatus, default);
            }
            catch (Exception e)
            {
                return new Response<T>(HandleRpcRequestException(e), default);
            }
        }

        public async Task<ResponseStatusCode> RpcRequest<TEnum>(int timeOut, TEnum dataTag, object data = null) where TEnum : struct
        {
            try
            {
                var result = await RpcRequest(timeOut, HeaderEx.BuildTag(dataTag), data).ConfigureAwait(false);
                return result.Header.GetStatusCode();
            }
            catch (Exception e)
            {
                return HandleRpcRequestException(e);
            }
        }

        public async Task<Response<T>> RpcRequest<T, TEnum>(int timeOut, TEnum dataTag, object data = null) where TEnum : struct
        {
            return await RpcRequest<T>(timeOut, HeaderEx.BuildTag(dataTag), data).ConfigureAwait(false);
        }

        public async Task<Response<T>> RpcRequest<T, TEnum>(TEnum dataTag, object data = null) where TEnum : struct
        {
            return await RpcRequest<T, TEnum>(60000, dataTag, data).ConfigureAwait(false);
        }

        public async Task<ResponseStatusCode> RpcRequest<TEnum>(TEnum dataTag, object data = null) where TEnum : struct
        {
            return await RpcRequest(60000, dataTag, data).ConfigureAwait(false);
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
        }

        internal async Task<Response> SendAndWaitInternalAsync(Message msg, int timeoutMs, long contentLength, Stream stream)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            if (contentLength > 0 && (stream == null || !stream.CanRead))
            {
                throw new ArgumentException("Cannot read from supplied stream.");
            }

            string conversationId = msg.ConversationGuid;
            if (string.IsNullOrEmpty(conversationId))
            {
                throw new ArgumentException("Message must have a ConversationGuid");
            }

            var tcs = new TaskCompletionSource<Response>();
            if (!_pendingRequests.TryAdd(conversationId, tcs))
            {
                throw new InvalidOperationException($"A request with conversation ID {conversationId} is already pending");
            }

            await _writeLock.WaitAsync(_token).ConfigureAwait(false);

            try
            {
                await SendHeadersAsync(msg, _token).ConfigureAwait(false);
                await SendDataStreamAsync(contentLength, stream, _token).ConfigureAwait(false);

                _statistics.IncrementSentMessages();
                _statistics.AddSentBytes(contentLength);
            }
            catch (Exception e)
            {
                _pendingRequests.TryRemove(conversationId, out _);
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

            try
            {
                // Sử dụng cách tạo delay task tương thích với cả .NET 4.0
#if NET40
                var timeoutTask = TaskEx.Delay(timeoutMs);
                var completedTask = await TaskEx.WhenAny(tcs.Task, timeoutTask);
#else
                var timeoutTask = Task.Delay(timeoutMs);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
#endif

                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }

                // Xử lý timeout
                bool removed = _pendingRequests.TryRemove(conversationId, out _);
                _debugLogger.Logger?.Invoke(Severity.Debug,
                    _header + $"Request {conversationId} timed out. Request removed: {removed}");

                if (_token.IsCancellationRequested)
                {
                    _debugLogger.Logger?.Invoke(Severity.Error, _header + "Task has been canceled.");
                    throw new TaskCanceledException("Task has been canceled.");
                }

                _debugLogger.Logger?.Invoke(Severity.Error, _header + "synchronous response not received within the timeout window");
                throw new TimeoutException("A response to a synchronous request was not received within the timeout window.");
            }
            catch (Exception ex) when (!(ex is TimeoutException || ex is TaskCanceledException))
            {
                _pendingRequests.TryRemove(conversationId, out _);
                _debugLogger.Logger?.Invoke(Severity.Error, _header + $"Error waiting for response: {ex.Message}");
                throw;
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
            if (contentLength <= 0)
            {
                return;
            }

            // Xác định quyền sở hữu stream
            bool isExternalStream = stream != null && stream != _dataStream;
            bool streamDisposed = false;

            try
            {
                // Kiểm tra token trước khi bắt đầu công việc
                token.ThrowIfCancellationRequested();

                if (stream == null || !stream.CanRead)
                {
                    throw new ArgumentException("Stream không hợp lệ hoặc không thể đọc");
                }

                long bytesRemaining = contentLength;
                long totalBytesProcessed = 0;
                int bytesRead;

                // Tối ưu kích thước buffer dựa trên nhu cầu thực tế
                int bufferSize = (int)Math.Min(_maxSendBufferLength, Math.Max(4096, Math.Min(contentLength, 65536)));
                byte[] buffer = new byte[bufferSize];

                // Lưu vị trí stream đầu vào để có thể reset nếu cần
                long? initialPosition = stream.CanSeek ? (long?)stream.Position : null;

                while (bytesRemaining > 0)
                {
                    // Kiểm tra hủy bỏ
                    if (token.IsCancellationRequested)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Debug, _header + "Truyền dữ liệu bị hủy sau khi đã xử lý " + totalBytesProcessed + " bytes");
                        token.ThrowIfCancellationRequested();
                    }

                    // Tính toán kích thước đọc tối ưu
                    int readSize = (int)Math.Min(buffer.Length, bytesRemaining);

                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, readSize, token).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Error, _header + "Stream đã bị dispose trong quá trình đọc");
                        throw new IOException("Stream đã bị đóng trong quá trình đọc");
                    }
                    catch (IOException ioEx)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Error, _header + $"Lỗi IO khi đọc từ stream: {ioEx.Message}");
                        throw;
                    }

                    // Kiểm tra kết thúc stream sớm
                    if (bytesRead == 0)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Warn,
                            _header + $"Stream kết thúc sớm sau khi đọc {totalBytesProcessed} bytes. Yêu cầu {contentLength} bytes.");

                        // Nếu stream có thể seek, thử reset về vị trí ban đầu
                        if (initialPosition.HasValue && stream.CanSeek)
                        {
                            _debugLogger.Logger?.Invoke(Severity.Debug, _header + "Thử reset stream về vị trí ban đầu");
                            stream.Position = initialPosition.Value;
                        }

                        break;
                    }

                    totalBytesProcessed += bytesRead;
                    bytesRemaining -= bytesRead;

                    try
                    {
                        await _dataStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Error, _header + "Stream đích đã bị dispose trong quá trình ghi");
                        throw new IOException("Stream đích đã bị đóng trong quá trình ghi");
                    }
                    catch (IOException ioEx)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Error, _header + $"Lỗi IO khi ghi vào stream đích: {ioEx.Message}");
                        throw;
                    }
                }

                // Kiểm tra xem đã xử lý đủ dữ liệu chưa
                if (bytesRemaining > 0 && totalBytesProcessed < contentLength)
                {
                    _debugLogger.Logger?.Invoke(Severity.Warn,
                        _header + $"Không thể đọc đủ dữ liệu. Yêu cầu: {contentLength}, Đã đọc: {totalBytesProcessed}");
                }

                // Flush dữ liệu nếu cần
                if (totalBytesProcessed > 0 && !token.IsCancellationRequested)
                {
                    await _dataStream.FlushAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _debugLogger.Logger?.Invoke(Severity.Info, _header + "Quá trình truyền dữ liệu bị hủy bỏ");
                throw;
            }
            catch (Exception ex)
            {
                _debugLogger.Logger?.Invoke(Severity.Error,
                    _header + $"Lỗi không xác định trong quá trình truyền dữ liệu: {ex.GetType().Name}: {ex.Message}");

                // Ghi chi tiết stack trace cho debug
                _debugLogger.ExceptionRecord?.Invoke(ex);
                throw;
            }
            finally
            {
                // Xử lý dọn dẹp stream đầu vào nếu cần
                if (isExternalStream && !streamDisposed)
                {
                    try
                    {
                        streamDisposed = true;
                        stream.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        // Chỉ log, không throw exception từ finally
                        _debugLogger.Logger?.Invoke(Severity.Debug,
                            _header + $"Lỗi khi giải phóng stream: {disposeEx.Message}");
                    }
                }
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

#endif
            }
            catch (Exception)
            {
                _debugLogger.Logger?.Invoke(Severity.Error, _header + "keepalives not supported on this platform, disabled");
                _keepaliveSettings.EnableTcpKeepAlives = false;
            }
        }
        #endregion
    }
}
