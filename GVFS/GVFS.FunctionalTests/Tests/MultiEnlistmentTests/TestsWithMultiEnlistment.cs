/*
Sniperkit-Bot
- Status: analyzed
*/

﻿using GVFS.FunctionalTests.Tools;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace GVFS.FunctionalTests.Tests.MultiEnlistmentTests
{
    public class TestsWithMultiEnlistment
    {
        private List<GVFSFunctionalTestEnlistment> enlistmentsToDelete = new List<GVFSFunctionalTestEnlistment>();

        [TearDown]
        public void DeleteEnlistments()
        {
            foreach (GVFSFunctionalTestEnlistment enlistment in this.enlistmentsToDelete)
            {
                enlistment.UnmountAndDeleteAll();
            }

            this.OnTearDownEnlistmentsDeleted();

            this.enlistmentsToDelete.Clear();
        }

        /// <summary>
        /// Can be overridden for custom [TearDown] steps that occur after the test enlistements have been unmounted and deleted
        /// </summary>
        protected virtual void OnTearDownEnlistmentsDeleted()
        {
        }

        protected GVFSFunctionalTestEnlistment CreateNewEnlistment(string localCacheRoot = null, string branch = null)
        {
            GVFSFunctionalTestEnlistment output = GVFSFunctionalTestEnlistment.CloneAndMount(GVFSTestConfig.PathToGVFS, branch, localCacheRoot);
            this.enlistmentsToDelete.Add(output);
            return output;
        }
    }
}
