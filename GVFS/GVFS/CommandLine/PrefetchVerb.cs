/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using CommandLine;
using GVFS.Common;
using GVFS.Common.FileSystem;
using GVFS.Common.Git;
using GVFS.Common.Http;
using GVFS.Common.Prefetch;
using GVFS.Common.Tracing;
using System;
using System.IO;

namespace GVFS.CommandLine
{
    [Verb(PrefetchVerb.PrefetchVerbName, HelpText = "Prefetch remote objects for the current head")]
    public class PrefetchVerb : GVFSVerb.ForExistingEnlistment
    {
        private const string PrefetchVerbName = "prefetch";

        private const int LockWaitTimeMs = 100;
        private const int WaitingOnLockLogThreshold = 50;
        private const int IoFailureRetryDelayMS = 50;
        private const string PrefetchCommitsAndTreesLock = "prefetch-commits-trees.lock";

        private const int ChunkSize = 4000;
        private static readonly int SearchThreadCount = Environment.ProcessorCount;
        private static readonly int DownloadThreadCount = Environment.ProcessorCount;
        private static readonly int IndexThreadCount = Environment.ProcessorCount;

        [Option(
            "files",
            Required = false,
            Default = "",
            HelpText = "A semicolon-delimited list of files to fetch. Simple prefix wildcards, e.g. *.txt, are supported.")]
        public string Files { get; set; }

        [Option(
            "folders",
            Required = false,
            Default = "",
            HelpText = "A semicolon-delimited list of folders to fetch. Wildcards are not supported.")]
        public string Folders { get; set; }

        [Option(
            "folders-list",
            Required = false,
            Default = "",
            HelpText = "A file containing line-delimited list of folders to fetch. Wildcards are not supported.")]
        public string FoldersListFile { get; set; }

        [Option(
            "hydrate",
            Required = false,
            Default = false,
            HelpText = "Specify this flag to also hydrate files in the working directory")]
        public bool HydrateFiles { get; set; }

        [Option(
            'c',
            "commits",
            Required = false,
            Default = false,
            HelpText = "Fetch the latest set of commit and tree packs. This option cannot be used with any of the file- or folder-related options.")]
        public bool Commits { get; set; }

        [Option(
            "verbose",
            Required = false,
            Default = false,
            HelpText = "Show all outputs on the console in addition to writing them to a log file")]
        public bool Verbose { get; set; }

        public bool SkipVersionCheck { get; set; }
        public CacheServerInfo ResolvedCacheServer { get; set; }
        public GVFSConfig GVFSConfig { get; set; }

        protected override string VerbName
        {
            get { return PrefetchVerbName; }
        }

        protected override void Execute(GVFSEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(GVFSConstants.GVFSEtwProviderName, "Prefetch"))
            {
                if (this.Verbose)
                {
                    tracer.AddDiagnosticConsoleEventListener(EventLevel.Informational, Keywords.Any);
                }

                string cacheServerUrl = CacheServerResolver.GetUrlFromConfig(enlistment);

                tracer.AddLogFileEventListener(
                    GVFSEnlistment.GetNewGVFSLogFileName(enlistment.GVFSLogsRoot, GVFSConstants.LogFileTypes.Prefetch),
                    EventLevel.Informational,
                    Keywords.Any);
                tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    cacheServerUrl);

                RetryConfig retryConfig = this.GetRetryConfig(tracer, enlistment, TimeSpan.FromMinutes(RetryConfig.FetchAndCloneTimeoutMinutes));

                CacheServerInfo cacheServer = this.ResolvedCacheServer;
                GVFSConfig gvfsConfig = this.GVFSConfig;
                if (!this.SkipVersionCheck)
                {
                    string authErrorMessage;
                    if (!this.ShowStatusWhileRunning(
                        () => enlistment.Authentication.TryRefreshCredentials(tracer, out authErrorMessage),
                        "Authenticating"))
                    {
                        this.ReportErrorAndExit(tracer, "Unable to prefetch because authentication failed");
                    }

                    if (gvfsConfig == null)
                    {
                        gvfsConfig = this.QueryGVFSConfig(tracer, enlistment, retryConfig);
                    }

                    if (cacheServer == null)
                    {
                        CacheServerResolver cacheServerResolver = new CacheServerResolver(tracer, enlistment);
                        cacheServer = cacheServerResolver.ResolveNameFromRemote(cacheServerUrl, gvfsConfig);
                    }

                    this.ValidateClientVersions(tracer, enlistment, gvfsConfig, showWarnings: false);

                    this.Output.WriteLine("Configured cache server: " + cacheServer);
                }

                this.InitializeLocalCacheAndObjectsPaths(tracer, enlistment, retryConfig, gvfsConfig, cacheServer);

                try
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Commits", this.Commits);
                    metadata.Add("Files", this.Files);
                    metadata.Add("Folders", this.Folders);
                    metadata.Add("FoldersListFile", this.FoldersListFile);
                    metadata.Add("HydrateFiles", this.HydrateFiles);
                    tracer.RelatedEvent(EventLevel.Informational, "PerformPrefetch", metadata);

                    GitObjectsHttpRequestor objectRequestor = new GitObjectsHttpRequestor(tracer, enlistment, cacheServer, retryConfig);

                    if (this.Commits)
                    {
                        if (!string.IsNullOrWhiteSpace(this.Files) ||
                            !string.IsNullOrWhiteSpace(this.Folders) ||
                            !string.IsNullOrWhiteSpace(this.FoldersListFile))
                        {
                            this.ReportErrorAndExit(tracer, "You cannot prefetch commits and blobs at the same time.");
                        }

                        if (this.HydrateFiles)
                        {
                            this.ReportErrorAndExit(tracer, "You can only specify --hydrate with --files or --folders");
                        }

                        this.PrefetchCommits(tracer, enlistment, objectRequestor, cacheServer);
                    }
                    else
                    {
                        this.PrefetchBlobs(tracer, enlistment, objectRequestor, cacheServer);
                    }
                }
                catch (VerbAbortedException)
                {
                    throw;
                }
                catch (AggregateException aggregateException)
                {
                    this.Output.WriteLine(
                        "Cannot prefetch {0}. " + ConsoleHelper.GetGVFSLogMessage(enlistment.EnlistmentRoot),
                        enlistment.EnlistmentRoot);
                    foreach (Exception innerException in aggregateException.Flatten().InnerExceptions)
                    {
                        tracer.RelatedError(
                            new EventMetadata
                            {
                                { "Verb", typeof(PrefetchVerb).Name },
                                { "Exception", innerException.ToString() }
                            },
                            $"Unhandled {innerException.GetType().Name}: {innerException.Message}");
                    }

                    Environment.ExitCode = (int)ReturnCode.GenericError;
                }
                catch (Exception e)
                {
                    this.Output.WriteLine(
                        "Cannot prefetch {0}. " + ConsoleHelper.GetGVFSLogMessage(enlistment.EnlistmentRoot),
                        enlistment.EnlistmentRoot);
                    tracer.RelatedError(
                        new EventMetadata
                        {
                            { "Verb", typeof(PrefetchVerb).Name },
                            { "Exception", e.ToString() }
                        },
                        $"Unhandled {e.GetType().Name}: {e.Message}");

                    Environment.ExitCode = (int)ReturnCode.GenericError;
                }
            }
        }

        private void PrefetchCommits(ITracer tracer, GVFSEnlistment enlistment, GitObjectsHttpRequestor objectRequestor, CacheServerInfo cacheServer)
        {
            bool success;
            string error = string.Empty;
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            GitRepo repo = new GitRepo(tracer, enlistment, fileSystem);
            GVFSContext context = new GVFSContext(tracer, fileSystem, repo, enlistment);
            GitObjects gitObjects = new GVFSGitObjects(context, objectRequestor);

            if (this.Verbose)
            {
                success = CommitPrefetcher.TryPrefetchCommitsAndTrees(tracer, enlistment, fileSystem, gitObjects, out error);
            }
            else
            {
                success = this.ShowStatusWhileRunning(
                    () => CommitPrefetcher.TryPrefetchCommitsAndTrees(tracer, enlistment, fileSystem, gitObjects, out error),
                    "Fetching commits and trees " + this.GetCacheServerDisplay(cacheServer));
            }

            if (!success)
            {
                this.ReportErrorAndExit(tracer, "Prefetching commits and trees failed: " + error);
            }
        }

        private void PrefetchBlobs(ITracer tracer, GVFSEnlistment enlistment, GitObjectsHttpRequestor blobRequestor, CacheServerInfo cacheServer)
        {
            BlobPrefetcher blobPrefetcher = new BlobPrefetcher(
                tracer,
                enlistment,
                blobRequestor,
                ChunkSize,
                SearchThreadCount,
                DownloadThreadCount,
                IndexThreadCount);

            string error;
            if (!BlobPrefetcher.TryLoadFolderList(enlistment, this.Folders, this.FoldersListFile, blobPrefetcher.FolderList, out error))
            {
                this.ReportErrorAndExit(tracer, error);
            }

            if (!BlobPrefetcher.TryLoadFileList(enlistment, this.Files, blobPrefetcher.FileList, out error))
            {
                this.ReportErrorAndExit(tracer, error);
            }

            if (blobPrefetcher.FolderList.Count == 0 &&
                blobPrefetcher.FileList.Count == 0)
            {
                this.ReportErrorAndExit(tracer, "Did you mean to fetch all blobs? If so, specify `--files *` to confirm.");
            }

            if (this.HydrateFiles)
            {
                if (!this.CheckIsMounted(verbose: true))
                {
                    this.ReportErrorAndExit("You can only specify --hydrate if the repo is mounted. Run 'gvfs mount' and try again.");
                }
            }

            GitProcess gitProcess = new GitProcess(enlistment);
            GitProcess.Result result = gitProcess.RevParse(GVFSConstants.DotGit.HeadName);
            if (result.HasErrors)
            {
                tracer.RelatedError(result.Errors);
                this.Output.WriteLine(result.Errors);
                Environment.ExitCode = (int)ReturnCode.GenericError;
                return;
            }

            int matchedBlobCount = 0;
            int downloadedBlobCount = 0;
            int readFileCount = 0;

            string headCommitId = result.Output;
            Func<bool> doPrefetch =
                () =>
                {
                    try
                    {
                        blobPrefetcher.PrefetchWithStats(
                            headCommitId.Trim(),
                            isBranch: false,
                            readFilesAfterDownload: this.HydrateFiles,
                            matchedBlobCount: out matchedBlobCount,
                            downloadedBlobCount: out downloadedBlobCount,
                            readFileCount: out readFileCount);
                        return !blobPrefetcher.HasFailures;
                    }
                    catch (BlobPrefetcher.FetchException e)
                    {
                        tracer.RelatedError(e.Message);
                        return false;
                    }
                };

            if (this.Verbose)
            {
                doPrefetch();
            }
            else
            {
                string message =
                    this.HydrateFiles
                    ? "Fetching blobs and hydrating files "
                    : "Fetching blobs ";
                this.ShowStatusWhileRunning(doPrefetch, message + this.GetCacheServerDisplay(cacheServer));
            }

            if (blobPrefetcher.HasFailures)
            {
                Environment.ExitCode = 1;
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Stats:");
                Console.WriteLine("  Matched blobs:    " + matchedBlobCount);
                Console.WriteLine("  Already cached:   " + (matchedBlobCount - downloadedBlobCount));
                Console.WriteLine("  Downloaded:       " + downloadedBlobCount);
                if (this.HydrateFiles)
                {
                    Console.WriteLine("  Hydrated files:   " + readFileCount);
                }
            }
        }

        private bool CheckIsMounted(bool verbose)
        {
            Func<bool> checkMount = () => this.Execute<StatusVerb>(
                    this.EnlistmentRootPathParameter,
                    verb => verb.Output = new StreamWriter(new MemoryStream())) == ReturnCode.Success;

            if (verbose)
            {
                return ConsoleHelper.ShowStatusWhileRunning(
                    checkMount,
                    "Checking that GVFS is mounted",
                    this.Output,
                    showSpinner: true,
                    gvfsLogEnlistmentRoot: null);
            }
            else
            {
                return checkMount();
            }
        }

        private string GetCacheServerDisplay(CacheServerInfo cacheServer)
        {
            if (cacheServer.Name != null && !cacheServer.Name.Equals(CacheServerInfo.ReservedNames.None))
            {
                return "from cache server";
            }

            return "from origin (no cache server)";
        }
    }
}
