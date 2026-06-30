/*
 * CertificateShellState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using This = Licensing.Components.Private.CertificateShellState;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains the global shell flags used when the licensing certificate
    /// subsystem applies its behavior to an interpreter shell, along with the
    /// helper methods used to query, modify, and reset those flags.
    /// </summary>
    [ObjectId("63214eaf-f52e-4cdf-8dcc-27081198a624")]
    internal static class CertificateShellState
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the shell flags stored by
        /// this class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The current shell flags maintained by this class.
        /// </summary>
        private static ShellFlags shellFlags;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Removes any forbidden shell flags from the supplied value. When
        /// dangerous flags are not allowed, the flags in the danger forbid
        /// mask are cleared; when the certificate SDK mode is enabled, the
        /// flags in the SDK forbid mask are cleared as well.
        /// </summary>
        /// <param name="danger">
        /// Non-zero if dangerous shell flags are permitted; otherwise, the
        /// dangerous flags will be forbidden (cleared).
        /// </param>
        /// <param name="shellFlags">
        /// On input, the shell flags to examine. On output, the shell flags
        /// with any forbidden flags removed.
        /// </param>
        /// <returns>
        /// Non-zero if one or more forbidden flags were removed; otherwise,
        /// zero.
        /// </returns>
        private static bool MaybeForbidFlags(
            bool danger,              /* in */
            ref ShellFlags shellFlags /* in, out */
            )
        {
            int count = 0;
            ShellFlags maskFlags; /* REUSED */

            if (!danger)
            {
                maskFlags = ShellFlags.DangerForbidMask;

                if ((shellFlags & maskFlags) != ShellFlags.None)
                {
                    shellFlags &= ~maskFlags;
                    count++;
                }
            }

            if (CertificateSdkMode.IsEnabled())
            {
                maskFlags = ShellFlags.SdkForbidMask;

                if ((shellFlags & maskFlags) != ShellFlags.None)
                {
                    shellFlags &= ~maskFlags;
                    count++;
                }
            }

            return (count > 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the supplied shell flags as the current shell flags for
        /// this class.
        /// </summary>
        /// <param name="shellFlags">
        /// The shell flags to store as the current shell flags.
        /// </param>
        private static void SetFlags(
            ShellFlags shellFlags /* in */
            )
        {
            lock (syncRoot)
            {
                This.shellFlags = shellFlags;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        #region Script Context Helper Methods
        /// <summary>
        /// Gets the current shell flags, combined with the flag indicating
        /// whether the certificate callbacks should be reset or uninstalled
        /// based on whether the specified interpreter currently has
        /// callbacks.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to determine whether callbacks are present.
        /// This parameter is optional and may be null.
        /// </param>
        /// <returns>
        /// The current shell flags combined with the appropriate callback
        /// flag.
        /// </returns>
        public static ShellFlags GetFlags(
            Interpreter interpreter /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                ShellFlags result = shellFlags;

                if (CertificateShellOps.HaveCallbacks(interpreter))
                    result |= ShellFlags.ResetCallbacks;
                else
                    result |= ShellFlags.UninstallCallbacks;

                return result;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current shell flags, as computed by the interpreter-aware
        /// GetFlags method, formatted as a string.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to determine whether callbacks are present.
        /// This parameter is optional and may be null.
        /// </param>
        /// <returns>
        /// The string representation of the current shell flags.
        /// </returns>
        public static string GetFlagsToString(
            Interpreter interpreter /* in: OPTIONAL */
            )
        {
            return GetFlags(interpreter).ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Examines the supplied shell flags and, if any flags outside of the
        /// apply mask are present, adds the flag that indicates the flags
        /// should be set.
        /// </summary>
        /// <param name="shellFlags">
        /// On input, the shell flags to examine. On output, the shell flags
        /// possibly combined with the set-flags flag.
        /// </param>
        /// <returns>
        /// Non-zero if the set-flags flag was added; otherwise, zero.
        /// </returns>
        public static bool MaybeSetFlags(
            ref ShellFlags shellFlags /* in, out */
            )
        {
            ShellFlags maskFlags = ShellFlags.ApplyMask;

            if ((shellFlags & ~maskFlags) != ShellFlags.None)
            {
                shellFlags |= ShellFlags.SetFlags;
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes any forbidden shell flags from the supplied nullable
        /// value. When the value is null, no action is taken.
        /// </summary>
        /// <param name="danger">
        /// Non-zero if dangerous shell flags are permitted; otherwise, the
        /// dangerous flags will be forbidden (cleared).
        /// </param>
        /// <param name="shellFlags">
        /// On input, the shell flags to examine, which may be null. On
        /// output, the shell flags with any forbidden flags removed.
        /// </param>
        /// <returns>
        /// Non-zero if one or more forbidden flags were removed; otherwise,
        /// zero.
        /// </returns>
        public static bool MaybeForbidFlags(
            bool danger,               /* in */
            ref ShellFlags? shellFlags /* in, out */
            )
        {
            if (shellFlags != null)
            {
                ShellFlags localShellFlags = (ShellFlags)shellFlags;

                if (MaybeForbidFlags(danger, ref localShellFlags))
                {
                    shellFlags = localShellFlags;
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies the supplied shell flags by forbidding any disallowed
        /// flags, installing, resetting, or uninstalling the certificate
        /// callbacks as indicated, and then storing the resulting flags when
        /// the set-flags flag is present.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to which the callbacks should be applied. This
        /// parameter is optional and may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the callbacks. This parameter is
        /// optional and may be null.
        /// </param>
        /// <param name="shellFlags">
        /// The shell flags to apply. This parameter is optional.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode ApplyFlags(
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: OPTIONAL */
            ShellFlags shellFlags,   /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                MaybeForbidFlags(CertificateSharedOps.HasFlags(
                    shellFlags, ShellFlags.AllowDangerousFlags,
                    true), ref shellFlags);

                if (CertificateSharedOps.HasFlags(
                        shellFlags, ShellFlags.UninstallCallbacks, true) ||
                    CertificateSharedOps.HasFlags(
                        shellFlags, ShellFlags.ResetCallbacks, true))
                {
                    if (CertificateShellOps.InstallCallbacks(
                            interpreter, pluginData, false,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                }

                if (CertificateSharedOps.HasFlags(
                        shellFlags, ShellFlags.InstallCallbacks, true) ||
                    CertificateSharedOps.HasFlags(
                        shellFlags, ShellFlags.ResetCallbacks, true))
                {
                    if (CertificateShellOps.InstallCallbacks(
                            interpreter, pluginData, true,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                }

                if (CertificateSharedOps.HasFlags(
                        shellFlags, ShellFlags.SetFlags, true))
                {
                    SetFlags(shellFlags & ~ShellFlags.ApplyMask);
                }
            }

            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Generic Helper Methods
        /// <summary>
        /// Gets the current shell flags maintained by this class.
        /// </summary>
        /// <returns>
        /// The current shell flags.
        /// </returns>
        public static ShellFlags GetFlags()
        {
            lock (syncRoot)
            {
                return shellFlags;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the current shell flags to their default value.
        /// </summary>
        public static void ResetFlags()
        {
            lock (syncRoot)
            {
                shellFlags = ShellFlags.Default;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the current shell flags so that no flags are set.
        /// </summary>
        public static void UnsetFlags()
        {
            lock (syncRoot)
            {
                shellFlags = ShellFlags.None;
            }
        }
        #endregion
        #endregion
    }
}
