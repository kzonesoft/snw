
using System.ComponentModel;

namespace Kzone.Signal
{
    [DefaultValue(Unknow)]
    public enum MessageType
    {
        Unknow,

        Success,

        Failure,

        AuthRequired,

        AuthRequested,

        AuthSuccess,

        AuthFailure,

        Removed,

        Shutdown,

        Disconnect,

        ConnectionReady,

        PingPack,

        RequestPack,

        ResponsePack,

        BroadcastPack,

        StreamPack,

        RegisterChannel
    }
}