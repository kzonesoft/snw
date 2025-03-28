using System;
using System.Collections.Generic;

namespace Kzone.Signal.Server
{
    public interface IClientManager
    {
        int TotalConnections { get; }
        bool IdentityAuthenticate(string identityId, IClient client);
        void RemoveIdentitySession(string identityId);
        void AddClient(IClient client);
        void DisconnectAll();
        void DisconnectByIdentityId(string identityId);
        void DisconnectByIp(string ipPort);
        void Dispose();
        IEnumerable<IClient> GetAllClient();
        IClient GetClient(Func<IClient, bool> filter);
        IClient GetClientByIdentityId(string identityId);
        IClient GetClientByIp(string ipPort);
        IEnumerable<IClient> GetClients(Func<IClient, bool> filter);
        IEnumerator<KeyValuePair<string, IClient>> ToEnumerator();
       
    }
}