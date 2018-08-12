/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using CommandLine;
using GVFS.CommandLine;
using GVFS.Common;
using GVFS.PlatformLoader;
using System;
using System.IO;
using System.Linq;

namespace GVFS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GVFSPlatformLoader.Initialize();

            Type[] verbTypes = new Type[]
            {
                typeof(CacheServerVerb),
                typeof(CloneVerb),
                typeof(DehydrateVerb),
                typeof(DiagnoseVerb),
                typeof(LogVerb),
                typeof(MountVerb),
                typeof(PrefetchVerb),
                typeof(RepairVerb),
                typeof(ServiceVerb),
                typeof(StatusVerb),
                typeof(UnmountVerb),
            };

            int consoleWidth = 80;

            // Running in a headless environment can result in a Console with a
            // WindowWidth of 0, which causes issues with CommandLineParser
            try
            {
                if (Console.WindowWidth > 0)
                {
                    consoleWidth = Console.WindowWidth;
                }
            }
            catch (IOException)
            {
            }

            try
            {
                new Parser(
                    settings =>
                    {
                        settings.CaseSensitive = false;
                        settings.EnableDashDash = true;
                        settings.IgnoreUnknownArguments = false;
                        settings.HelpWriter = Console.Error;
                        settings.MaximumDisplayWidth = consoleWidth;
                    })
                    .ParseArguments(args, verbTypes)
                    .WithNotParsed(
                        errors =>
                        {
                            if (errors.Any(error => error is TokenError))
                            {
                                Environment.Exit((int)ReturnCode.ParsingError);
                            }
                        })
                    .WithParsed<CloneVerb>(
                        clone =>
                        {
                            // We handle the clone verb differently, because clone cares if the enlistment path
                            // was not specified vs if it was specified to be the current directory
                            clone.Execute();
                            Environment.Exit((int)ReturnCode.Success);
                        })
                    .WithParsed<ServiceVerb>(
                        service =>
                        {
                            // The service verb doesn't operate on a repo, so it doesn't use the enlistment
                            // path at all.
                            service.Execute();
                            Environment.Exit((int)ReturnCode.Success);
                        })
                    .WithParsed<GVFSVerb>(
                        verb =>
                        {
                            // For all other verbs, they don't care if the enlistment root is explicitly
                            // specified or implied to be the current directory
                            if (string.IsNullOrEmpty(verb.EnlistmentRootPathParameter))
                            {
                                verb.EnlistmentRootPathParameter = Environment.CurrentDirectory;
                            }

                            verb.Execute();
                            Environment.Exit((int)ReturnCode.Success);
                        });
            }
            catch (GVFSVerb.VerbAbortedException e)
            {
                // Calling Environment.Exit() is required, to force all background threads to exit as well
                Environment.Exit((int)e.Verb.ReturnCode);
            }
        }
    }
}
