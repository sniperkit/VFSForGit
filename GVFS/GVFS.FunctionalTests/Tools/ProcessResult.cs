/*
Sniperkit-Bot
- Status: analyzed
*/

﻿namespace GVFS.FunctionalTests.Tools
{
    public class ProcessResult
    {
        public ProcessResult(string output, string errors, int exitCode)
        {
            this.Output = output;
            this.Errors = errors;
            this.ExitCode = exitCode;
        }

        public string Output { get; }
        public string Errors { get; }
        public int ExitCode { get; }
    }
}
