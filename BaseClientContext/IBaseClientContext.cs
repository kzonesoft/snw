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
        Task<Response> RpcRequest(int timeoutMs, Header header, object obj = null);
        Task<Response<T>> RpcRequest<T>(int timeOut, Header header, object data = null);
        Task<Response<T>> RpcRequest<T, TEnum>(int timeOut, TEnum dataTag, object data = null) where TEnum : struct;
        Task<Response<T>> RpcRequest<T, TEnum>(TEnum dataTag, object data = null) where TEnum : struct;
        Task<ResponseStatusCode> RpcRequest<TEnum>(int timeOut, TEnum dataTag, object data = null) where TEnum : struct;
        Task<ResponseStatusCode> RpcRequest<TEnum>(TEnum dataTag, object data = null) where TEnum : struct;
        Task<Response> Authorize(object data = null);
        Task<Response> BadRequest(object data = null);
        Task<Response> Block(object data = null);
        Task<Response> NotFound(object data = null);
        Task<Response> Ok(object data = null);
        Task<Response> Newest(object data = null);
        Task<Response> OutOfDate(object data = null);
        Task<Response> SessionExpired(object data = null);
        Task<Response> UnAuthorize(object data = null);
        Task<Response> UnAvailable(object data = null);
        Task<Response> Unsupport(object data = null);
        Task<Response> Reject(object data = null);
        Task<Response> SessionFull(object data = null);
        Task<Response> Response(ResponseStatusCode reponseStatus, object data);
        Task<Response> Conflict(object data = null);
    }
}