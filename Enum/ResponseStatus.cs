
using System.ComponentModel;

namespace Kzone.Signal
{
    [DefaultValue(Default)]
    public enum ResponseStatusCode
    {
        Default = 0,

        RequestError = 100,

        ResponseError = 101,

        ObjectParseFail = 102,

        UnknowError = 103,

        HeaderNull = 104,

        TokenExpired = 105,

        TokenInvalid = 106,

        HandleReject = 107,

        IpBlocked = 108,

        Unknown = 109,

        Object_Existed = 110,

        Object_NotFound = 111,

        Session = 112,

        SessionFull = 113,

        SessionExpired = 114,

        BadRequest = 115,

        Unauthorize = 116,

        Authorize = 117,

        ConnectionError = 118,

        NullValue = 119,

        Block = 120,

        Kick = 121,

        Reject = 122,

        NotFound = 123,

        UnAvailable = 124,

        Unformatted = 125,

        Unsupport = 126,

        OutOfDate = 127,

        Newest = 128,

        Timeout = 129,

        Accept = 130,

        Ok = 131,

        Conflict = 132,

        TaskCancel = 133,

        ErrorOccured = 134
    }
}
