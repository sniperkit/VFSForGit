/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using System.Runtime.InteropServices;

namespace PrjFSLib.Mac.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Callbacks
    {
        public EnumerateDirectoryCallback OnEnumerateDirectory;
        public GetFileStreamCallback OnGetFileStream;
        public NotifyPreDeleteEvent OnNotifyPreDelete;
    }
}
