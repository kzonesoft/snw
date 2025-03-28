using System;

namespace Kzone.Signal.Server
{
    public interface IClient : IBaseClientContext
    {
        Guid Id { get; }
        string IpPort { get; }
        string IpOnly { get; }
        string IdentityId { get; set; }
        string Role { get; set; }
        int Channel { get; set; }
        string Group { get; set; }
        bool AllowRequest { get; set; }
        string AppVersion { get; set; }
        DateTime LastAuth { get; set; }

        void Disconnect(MessageType status = MessageType.Disconnect, bool sendNotice = false);
        bool IsConnected();
        void Dispose();
        double CalculatorLastActivity();
    }
}