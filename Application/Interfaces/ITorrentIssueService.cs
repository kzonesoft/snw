using System;

namespace Kzone.Engine.Controller.Application.Interfaces
{
    public interface ITorrentIssueService
    {
        int CleanupMaxedOutRepairs(int maxRepairCount);
        int GetLockedTorrentsCount();
        int GetRepairCount(string torrentId);
        bool IsLocked(string torrentHash);
        void LockTorrent(string torrentHash);
        void LockTorrent(string torrentHash, TimeSpan lockDuration);
        bool NeedsRepair(string torrentId, int maxRepairCount = 5, TimeSpan? repairInterval = null);
        void RemoveRepairInfo(string torrentId);
        bool ResetRepairInfo(string torrentId);
        void UnlockAllTorrents();
        void UnlockTorrent(string torrentHash);
        void Dispose();
    }
}