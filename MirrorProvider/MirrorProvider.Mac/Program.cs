/*
Sniperkit-Bot
- Status: analyzed
*/

﻿namespace MirrorProvider.Mac
{
    class Program
    {
        static void Main(string[] args)
        {
            MirrorProviderCLI.Run(args, new MacFileSystemVirtualizer());
        }
    }
}
