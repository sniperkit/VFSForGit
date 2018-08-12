/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace GVFS.PreBuild
{
    public class GenerateG4WNugetReference : Task
    {
        [Required]
        public string GitPackageVersion { get; set; }

        public override bool Execute()
        {
            this.Log.LogMessage(MessageImportance.High, "Creating packages.config for G4W package version " + this.GitPackageVersion);

            File.WriteAllText(
                Path.Combine(Environment.CurrentDirectory, "packages.config"),
                string.Format(
@"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- This file is autogenerated by GVFS.PreBuild.CreateG4WNugetReference. Any changes made directly in this file will be lost. -->
<packages>
  <package id=""GitForWindows.GVFS.Installer"" version=""{0}"" targetFramework=""net461"" />
  <package id=""GitForWindows.GVFS.Portable"" version=""{0}"" targetFramework=""net461"" />
</packages>",
                    this.GitPackageVersion));

            return true;
        }
    }
}
