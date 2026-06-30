/*
 * CertificateDemoState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using Eagle._Attributes;
using Eagle._Components.Public;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods for managing the demo licensing state and the
    /// pending key ring file used during the demo license workflow.
    /// </summary>
    [ObjectId("d3331c4f-92e8-47ee-9956-3e083db2d29d")]
    internal static class CertificateDemoState
    {
        #region Private File Name Methods
        /// <summary>
        /// Gets the name of the environment variable used to store the
        /// pending key ring file name.
        /// </summary>
        /// <returns>
        /// The environment variable name, or null if it cannot be determined.
        /// </returns>
        private static string GetPendingEnvVarName() /* CORE? */
        {
            return CertificateSharedOps.GetEnvVarName(
                typeof(CertificateDemoState).Name,
                Constants.PendingKeyRingFileNameEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the pending key ring file name from its associated
        /// environment variable.
        /// </summary>
        /// <returns>
        /// The pending key ring file name, or null if it is not set.
        /// </returns>
        private static string GetPendingFileName() /* CORE? */
        {
            string envVarName = GetPendingEnvVarName();

            if (envVarName == null)
                return null;

            return Configuration.GetVariable(envVarName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public File Name Methods
        /// <summary>
        /// Records the specified file name as the pending key ring file
        /// name by storing it in its associated environment variable.
        /// </summary>
        /// <param name="fileName">
        /// The pending key ring file name to record.
        /// </param>
        public static void BeginPendingFileName( /* CORE? */
            string fileName /* in */
            )
        {
            string envVarName = GetPendingEnvVarName();

            if (envVarName == null)
                return;

            Configuration.SetVariable(envVarName, fileName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the pending key ring file name by unsetting its associated
        /// environment variable.  In debug builds, this verifies that the
        /// given file name matches the previously recorded pending file name.
        /// </summary>
        /// <param name="fileName">
        /// The pending key ring file name expected to be cleared.
        /// </param>
        public static void EndPendingFileName( /* CORE? */
            string fileName /* in */
            )
        {
#if DEBUG || FORCE_TRACE
            string localFileName = GetPendingFileName();

            if (!CertificateDataOps.StringEquals(
                    fileName, localFileName))
            {
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Pending key ring file name mismatch: {0} versus {1}",
                        Utility.FormatWrapOrNull(fileName),
                        Utility.FormatWrapOrNull(localFileName)),
                    typeof(CertificateDemoState).Name,
                    TracePriority.MediumHigh, 0);
            }
#endif

            string envVarName = GetPendingEnvVarName();

            if (envVarName == null)
                return;

            Configuration.UnsetVariable(envVarName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Support Methods
        /// <summary>
        /// Determines whether a demo license is currently pending, based on
        /// the key ring state and the name of the pending key ring file.
        /// </summary>
        /// <returns>
        /// True if a demo license is pending; otherwise, false.
        /// </returns>
        public static bool IsLicensePending()
        {
            if (CertificateKeyRingState.IsLicensePending())
            {
                string fileName = GetPendingFileName();

                if (!String.IsNullOrEmpty(fileName))
                {
                    string fileNameOnly = Path.GetFileName(fileName);

                    if (CertificateDataOps.StringEquals(fileNameOnly,
                            Constants.KeyRingDemoGeneralFileName))
                    {
                        return true;
                    }

                    if (CertificateDataOps.StringEquals(fileNameOnly,
                            Constants.KeyRingDemoLicenseFileName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        #endregion
    }
}
