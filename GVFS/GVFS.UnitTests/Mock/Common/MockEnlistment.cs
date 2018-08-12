/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using GVFS.Common;
using GVFS.Common.Git;
using GVFS.UnitTests.Mock.Git;

namespace GVFS.UnitTests.Mock.Common
{
    public class MockEnlistment : Enlistment
    {
        private MockGitProcess gitProcess;

        public MockEnlistment()
            : base("mock:\\path", "mock:\\path", "mock://repoUrl", "mock:\\git", null, flushFileBuffersForPacks: false)
        {
            this.GitObjectsRoot = "mock:\\path\\.git\\objects";
            this.LocalObjectsRoot = this.GitObjectsRoot;
            this.GitPackRoot = "mock:\\path\\.git\\objects\\pack";
        }

        public MockEnlistment(MockGitProcess gitProcess)
            : this()
        {
            this.gitProcess = gitProcess;
        }

        public override string GitObjectsRoot { get; protected set; }

        public override string LocalObjectsRoot { get; protected set; }

        public override string GitPackRoot { get; protected set; }

        public override GitProcess CreateGitProcess()
        {
            return this.gitProcess ?? new MockGitProcess();
        }
    }
}
