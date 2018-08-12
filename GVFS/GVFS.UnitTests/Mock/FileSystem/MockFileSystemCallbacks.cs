/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using GVFS.Common;
using GVFS.Common.Git;
using GVFS.Virtualization;
using GVFS.Virtualization.Background;
using GVFS.Virtualization.BlobSize;
using GVFS.Virtualization.FileSystem;
using GVFS.Virtualization.Projection;

namespace GVFS.UnitTests.Mock.FileSystem
{
    public class MockFileSystemCallbacks : FileSystemCallbacks
    {
        public MockFileSystemCallbacks(
            GVFSContext context,
            GVFSGitObjects gitObjects,
            RepoMetadata repoMetadata,
            FileSystemVirtualizer fileSystemVirtualizer)
            : base(context, gitObjects, repoMetadata, fileSystemVirtualizer)
        {
        }

        public MockFileSystemCallbacks(
            GVFSContext context,
            GVFSGitObjects gitObjects,
            RepoMetadata repoMetadata,
            BlobSizes blobSizes,
            GitIndexProjection gitIndexProjection,
            BackgroundFileSystemTaskRunner backgroundFileSystemTaskRunner,
            FileSystemVirtualizer fileSystemVirtualizer)
            : base(context, gitObjects, repoMetadata, blobSizes, gitIndexProjection, backgroundFileSystemTaskRunner, fileSystemVirtualizer)
        {
        }

        public int OnFileRenamedCallCount { get; set; }
        public int OnFolderRenamedCallCount { get; set; }
        public int OnIndexFileChangeCallCount { get; set; }
        public int OnLogsHeadChangeCallCount { get; set; }

        public override void OnFileRenamed(string oldRelativePath, string newRelativePath)
        {
            this.OnFileRenamedCallCount++;
        }

        public override void OnFolderRenamed(string oldRelativePath, string newRelativePath)
        {
            this.OnFolderRenamedCallCount++;
        }

        public override void OnIndexFileChange()
        {
            this.OnIndexFileChangeCallCount++;
        }

        public override void OnLogsHeadChange()
        {
            this.OnLogsHeadChangeCallCount++;
        }

        public void ResetCalls()
        {
            this.OnFileRenamedCallCount = 0;
            this.OnIndexFileChangeCallCount = 0;
            this.OnLogsHeadChangeCallCount = 0;
            this.OnFolderRenamedCallCount = 0;
        }
    }
}
