/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using GVFS.Common.Http;

namespace GVFS.UnitTests.Mock
{
    public class MockCacheServerInfo : CacheServerInfo
    {
        public MockCacheServerInfo() : base("https://mock", "mock")
        {
        }
    }
}
