using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace Kzone.Engine.Controller.Infrastructure.CoreProcess
{
    public class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        public SafeLibraryHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return FreeLibrary(handle);
        }
    }
}
