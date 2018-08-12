/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using GVFS.FunctionalTests.Properties;
using GVFS.Tests.Should;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GVFS.FunctionalTests.Tools
{
    public static class GitHelpers
    {
        public const string AlwaysExcludeFilePath = @".git\info\always_exclude";

        /// <summary>
        /// This string must match the command name provided in the
        /// GVFS.FunctionalTests.LockHolder program.
        /// </summary>
        private const string LockHolderCommandName = @"GVFS.FunctionalTests.LockHolder";
        private const string LockHolderCommand = @"GVFS.FunctionalTests.LockHolder.exe";

        private static string LockHolderCommandPath
        {
            get
            {
                return Path.Combine(Settings.Default.CurrentDirectory, LockHolderCommand);
            }
        }

        public static void CheckGitCommand(string virtualRepoRoot, string command, params string[] expectedLinesInResult)
        {
            ProcessResult result = GitProcess.InvokeProcess(virtualRepoRoot, command);
            result.Errors.ShouldBeEmpty();
            foreach (string line in expectedLinesInResult)
            {
                result.Output.ShouldContain(line);
            }
        }

        public static void CheckGitCommandAgainstGVFSRepo(string virtualRepoRoot, string command, params string[] expectedLinesInResult)
        {
            ProcessResult result = InvokeGitAgainstGVFSRepo(virtualRepoRoot, command);
            result.Errors.ShouldBeEmpty();
            foreach (string line in expectedLinesInResult)
            {
                result.Output.ShouldContain(line);
            }
        }

        public static ProcessResult InvokeGitAgainstGVFSRepo(
            string gvfsRepoRoot,
            string command,
            Dictionary<string, string> environmentVariables = null,
            bool cleanErrors = true)
        {
            ProcessResult result = GitProcess.InvokeProcess(gvfsRepoRoot, command, environmentVariables);

            string errors = result.Errors;
            if (cleanErrors)
            {
                string[] lines = errors.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                errors = string.Join("\r\n", lines.Where(line => !line.StartsWith("Waiting for ")));

                if (errors.Length > 0 && string.IsNullOrWhiteSpace(errors))
                {
                    errors = string.Empty;
                }
            }

            return new ProcessResult(
                result.Output,
                errors,
                result.ExitCode);
        }

        public static void ValidateGitCommand(
            GVFSFunctionalTestEnlistment enlistment,
            ControlGitRepo controlGitRepo,
            string command,
            params object[] args)
        {
            command = string.Format(command, args);
            string controlRepoRoot = controlGitRepo.RootPath;
            string gvfsRepoRoot = enlistment.RepoRoot;

            ProcessResult expectedResult = GitProcess.InvokeProcess(controlRepoRoot, command);
            ProcessResult actualResult = GitHelpers.InvokeGitAgainstGVFSRepo(gvfsRepoRoot, command);

            ErrorsShouldMatch(command, expectedResult, actualResult);
            actualResult.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ShouldMatchInOrder(expectedResult.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), LinesAreEqual, command + " Output Lines");

            if (command != "status")
            {
                ValidateGitCommand(enlistment, controlGitRepo, "status");
            }
        }

        /// <summary>
        /// Acquire the GVFSLock. This method will return once the GVFSLock has been acquired.
        /// </summary>
        /// <param name="processId">The ID of the process that acquired the lock.</param>
        /// <returns><see cref="ManualResetEvent"/> that can be signaled to exit the lock acquisition program.</returns>
        public static ManualResetEventSlim AcquireGVFSLock(
            GVFSFunctionalTestEnlistment enlistment,
            out int processId,
            int resetTimeout = Timeout.Infinite,
            bool skipReleaseLock = false)
        {
            return RunCommandWithWaitAndStdIn(enlistment, resetTimeout, LockHolderCommandPath, skipReleaseLock ? "--skip-release-lock" : string.Empty, GitHelpers.LockHolderCommandName, "done", out processId);
        }

        /// <summary>
        /// Run the specified Git command. This method will return once the GVFSLock has been acquired.
        /// </summary>
        /// <param name="processId">The ID of the process that acquired the lock.</param>
        /// <returns><see cref="ManualResetEvent"/> that can be signaled to exit the lock acquisition program.</returns>
        public static ManualResetEventSlim RunGitCommandWithWaitAndStdIn(
            GVFSFunctionalTestEnlistment enlistment,
            int resetTimeout,
            string command,
            string stdinToQuit,
            out int processId)
        {
            return
                RunCommandWithWaitAndStdIn(
                    enlistment,
                    resetTimeout,
                    Properties.Settings.Default.PathToGit,
                    command,
                    "git " + command,
                    stdinToQuit,
                    out processId);
        }

        public static void ErrorsShouldMatch(string command, ProcessResult expectedResult, ProcessResult actualResult)
        {
            actualResult.Errors.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ShouldMatchInOrder(expectedResult.Errors.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), LinesAreEqual, command + " Errors Lines");
        }

        /// <summary>
        /// Run the specified command as an external program. This method will return once the GVFSLock has been acquired.
        /// </summary>
        /// <param name="processId">The ID of the process that acquired the lock.</param>
        /// <returns><see cref="ManualResetEvent"/> that can be signaled to exit the lock acquisition program.</returns>
        private static ManualResetEventSlim RunCommandWithWaitAndStdIn(
            GVFSFunctionalTestEnlistment enlistment,
            int resetTimeout,
            string pathToCommand,
            string args,
            string lockingProcessCommandName,
            string stdinToQuit,
            out int processId)
        {
            ManualResetEventSlim resetEvent = new ManualResetEventSlim(initialState: false);

            ProcessStartInfo processInfo = new ProcessStartInfo(pathToCommand);
            processInfo.WorkingDirectory = enlistment.RepoRoot;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardInput = true;
            processInfo.Arguments = args;

            Process holdingProcess = Process.Start(processInfo);
            StreamWriter stdin = holdingProcess.StandardInput;
            processId = holdingProcess.Id;

            enlistment.WaitForLock(lockingProcessCommandName);

            Task.Run(
                () =>
                {
                    resetEvent.Wait(resetTimeout);

                    // Make sure to let the holding process end.
                    if (stdin != null)
                    {
                        stdin.WriteLine(stdinToQuit);
                        stdin.Close();
                    }

                    if (holdingProcess != null)
                    {
                        bool holdingProcessHasExited = holdingProcess.WaitForExit(10000);

                        if (!holdingProcess.HasExited)
                        {
                            holdingProcess.Kill();
                        }

                        holdingProcess.Dispose();

                        holdingProcessHasExited.ShouldBeTrue("Locking process did not exit in time.");
                    }

                    resetEvent.Set();
                });

            return resetEvent;
        }

        private static bool LinesAreEqual(string actualLine, string expectedLine)
        {
            return actualLine.Equals(expectedLine);
        }
    }
}
