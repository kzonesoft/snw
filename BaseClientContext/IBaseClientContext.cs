using System.IO;
using System.Threading.Tasks;

namespace Kzone.Signal
{
    public interface IBaseClientContext
    {
        bool Send(Header headerPacket, object obj = null);
        bool SendStream(Header headerPacket, Stream stream, long contentLength);
        Task<bool> SendAsync(Header header, object obj = null);
        Task<bool> SendStreamAsync(Header headerPacket, long contentLength, Stream stream);
        Task<ResponseResult> RpcSendAsync(int timeoutMs, Header header, object obj = null);
        Task<ResponseResult<T>> RpcSendAsync<T>(int timeOut, Header header, object data = null);
        Task<ResponseResult<T>> RpcSendAsync<T, TEnum>(int timeOut, TEnum dataTag, object data = null) where TEnum : struct;
        Task<ResponseResult<T>> RpcSendAsync<T, TEnum>(TEnum dataTag, object data = null) where TEnum : struct;
        Task<ResponseStatusCode> RpcSendAsync<TEnum>(int timeOut, TEnum dataTag, object data = null) where TEnum : struct;
        Task<ResponseStatusCode> RpcSendAsync<TEnum>(TEnum dataTag, object data = null) where TEnum : struct;
        Task<ResponseResult> Authorize(object data = null);
        Task<ResponseResult> BadRequest(object data = null);
        Task<ResponseResult> Block(object data = null);
        Task<ResponseResult> NotFound(object data = null);
        Task<ResponseResult> Ok(object data = null);
        Task<ResponseResult> Newest(object data = null);
        Task<ResponseResult> OutOfDate(object data = null);
        Task<ResponseResult> SessionExpired(object data = null);
        Task<ResponseResult> UnAuthorize(object data = null);
        Task<ResponseResult> UnAvailable(object data = null);
        Task<ResponseResult> Unsupport(object data = null);
        Task<ResponseResult> Reject(object data = null);
        Task<ResponseResult> SessionFull(object data = null);
        Task<ResponseResult> Response(ResponseStatusCode reponseStatus, object data);
        Task<ResponseResult> Conflict(object data = null);
    }
}