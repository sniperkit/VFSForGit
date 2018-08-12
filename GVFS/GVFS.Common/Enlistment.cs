/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using GVFS.Common.Git;
using System;
using System.IO;

namespace GVFS.Common
{
    public abstract class Enlistment
    {       
        protected Enlistment(
            string enlistmentRoot,
            string workingDirectoryRoot,
            string repoUrl,
            string gitBinPath,
            string gvfsHooksRoot,
            bool flushFileBuffersForPacks)
        {
            if (string.IsNullOrWhiteSpace(gitBinPath))
            {
                throw new ArgumentException("Path to git.exe must be set");
            }

            this.EnlistmentRoot = enlistmentRoot;
            this.WorkingDirectoryRoot = workingDirectoryRoot;
            this.DotGitRoot = Path.Combine(this.WorkingDirectoryRoot, GVFSConstants.DotGit.Root);
            this.GitBinPath = gitBinPath;
            this.GVFSHooksRoot = gvfsHooksRoot;
            this.FlushFileBuffersForPacks = flushFileBuffersForPacks;

            GitProcess gitProcess = new GitProcess(this);
            if (repoUrl != null)
            {
                this.RepoUrl = repoUrl;
            }
            else
            {
                GitProcess.Result originResult = gitProcess.GetOriginUrl();
                if (originResult.HasErrors)
                {
                    if (originResult.Errors.Length == 0)
                    {
                        throw new InvalidRepoException("Could not get origin url. remote 'origin' is not configured for this repo.'");
                    }

                    throw new InvalidRepoException("Could not get origin url. git error: " + originResult.Errors);
                }

                this.RepoUrl = originResult.Output.Trim();
            }
            
            this.Authentication = new GitAuthentication(gitProcess, this.RepoUrl);
        }

        public string EnlistmentRoot { get; }
        public string WorkingDirectoryRoot { get; }
        public string DotGitRoot { get; private set; }
        public abstract string GitObjectsRoot { get; protected set; }
        public abstract string LocalObjectsRoot { get; protected set; }
        public abstract string GitPackRoot { get; protected set; }
        public string RepoUrl { get; }
        public bool FlushFileBuffersForPacks { get; }

        public string GitBinPath { get; }
        public string GVFSHooksRoot { get; }

        public GitAuthentication Authentication { get; }

        public static string GetNewLogFileName(string logsRoot, string prefix)
        {
            if (!Directory.Exists(logsRoot))
            {
                Directory.CreateDirectory(logsRoot);
            }

            string name = prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fullPath = Path.Combine(
                logsRoot,
                name + ".log");

            if (File.Exists(fullPath))
            {
                fullPath = Path.Combine(
                    logsRoot,
                    name + "_" + Guid.NewGuid().ToString("N") + ".log");
            }

            return fullPath;
        }

        public virtual GitProcess CreateGitProcess()
        {
            return new GitProcess(this);
        }
    }
}