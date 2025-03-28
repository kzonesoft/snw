
using System.Linq;


namespace Kzone.Signal
{
    internal class PjCol
    {
        private static string[] _listProject = new string[]
        {
            "KzoneSyncService",
            "KzoneViewControl",
            "KzoneRdcServer",
            "KzoneWksRunner"

        };

        internal static bool GetName(string basePubInfo)
        {
            if (string.IsNullOrEmpty(basePubInfo)) return false;
            var name = basePubInfo.Split(',').FirstOrDefault();
            if (string.IsNullOrEmpty(name)) return false;
            return _listProject.Contains(name);
        }
    }
}
