/*
 * CertificateSandboxState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

using TokenPair = System.Collections.Generic.KeyValuePair<ulong, object>;
using TokenList = System.Collections.Generic.Dictionary<ulong, object>;

using FileNameAnyPair = Eagle._Components.Public.AnyPair<
    Eagle._Interfaces.Public.IPluginData, Eagle._Components.Public.Result>;

using FileNamePair = System.Collections.Generic.KeyValuePair<string,
    Eagle._Components.Public.AnyPair<Eagle._Interfaces.Public.IPluginData,
    Eagle._Components.Public.Result>>;

using FileNameList = System.Collections.Generic.Dictionary<string,
    Eagle._Components.Public.AnyPair<Eagle._Interfaces.Public.IPluginData,
    Eagle._Components.Public.Result>>;

using ResultDictionary = System.Collections.Generic.Dictionary<string,
    Eagle._Components.Public.Result>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains the per-<see cref="AppDomain" /> state used to sandbox
    /// certificate scripts, including the primary sandbox token, the set of
    /// all sandbox tokens, and the lists of file names that have succeeded or
    /// failed during sandbox evaluation.
    /// </summary>
    [ObjectId("654707da-e360-4a68-ab65-a056d07d9192")]
    internal static class CertificateSandboxState
    {
        #region Private Data
        //
        // NOTE: This is used to synchronize access to all sandbox tokens
        //       managed by this class.
        //
        /// <summary>
        /// The object used to synchronize access to all sandbox tokens and
        /// file name lists managed by this class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Use one "sandbox" interpreter for all plugins loaded into
        //       this AppDomain.
        //
        /// <summary>
        /// The token identifying the primary sandbox interpreter shared by all
        /// plugins loaded into this <see cref="AppDomain" />, or null if one
        /// has not yet been established.
        /// </summary>
        private static ulong? primaryToken = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The list of sandbox tokens for all plugins loaded into
        //       this AppDomain.
        //
        /// <summary>
        /// The list of sandbox tokens for all plugins loaded into this
        /// <see cref="AppDomain" />, or null if none have been added.
        /// </summary>
        private static TokenList tokens;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The list of file names for all sandbox scripts that have
        //       been evaluated in this AppDomain and fully succeeded.
        //
        /// <summary>
        /// The list of file names for all sandbox scripts that have been
        /// evaluated in this <see cref="AppDomain" /> and fully succeeded, or
        /// null if there are none.
        /// </summary>
        private static FileNameList okFileNames;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The list of file names for all sandbox scripts that have
        //       been evaluated in this AppDomain and failed somehow.
        //
        /// <summary>
        /// The list of file names for all sandbox scripts that have been
        /// evaluated in this <see cref="AppDomain" /> and failed somehow, or
        /// null if there are none.
        /// </summary>
        private static FileNameList errorFileNames;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IConfiguration Support Methods
        #region Primary Token Management Methods
        /// <summary>
        /// Returns the primary sandbox token, generating and storing a new
        /// random token if one has not yet been established.
        /// </summary>
        /// <returns>
        /// The primary sandbox token shared by all plugins loaded into this
        /// <see cref="AppDomain" />.
        /// </returns>
        public static ulong GetPrimaryToken() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (primaryToken == null)
                    primaryToken = Utility.GetRandomNumber();

                return (ulong)primaryToken;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified token is the primary sandbox
        /// token.
        /// </summary>
        /// <param name="token">
        /// The token to compare against the primary sandbox token.
        /// </param>
        /// <returns>
        /// Non-zero if a primary token has been established and it matches
        /// <paramref name="token" />; otherwise, zero.
        /// </returns>
        public static bool IsPrimaryToken( /* CORE */
            ulong token /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (primaryToken == null)
                    return false;

                return token == (ulong)primaryToken;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the primary sandbox token if it matches the specified token.
        /// </summary>
        /// <param name="token">
        /// The token that must match the primary token in order for it to be
        /// reset.
        /// </param>
        /// <returns>
        /// Non-zero if the primary token was set and matched
        /// <paramref name="token" /> and was therefore reset; otherwise, zero.
        /// </returns>
        private static bool MaybeResetPrimaryToken( /* CORE */
            ulong token /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (primaryToken == null)
                    return false;

                if (token == (ulong)primaryToken)
                {
                    primaryToken = null;
                    return true;
                }

                return false;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Token Management Methods
        /// <summary>
        /// Creates a snapshot of the keys of the sandbox token list.
        /// </summary>
        /// <returns>
        /// A new list containing the keys of all sandbox tokens, or null if no
        /// tokens have been added.
        /// </returns>
        public static IEnumerable<ulong> CopyTokenKeys() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (tokens == null)
                    return null;

                return new UlongList(tokens.Keys);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds a sandbox token, associating it with the specified plugin
        /// data.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to add.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to associate with the token.  This value may be
        /// null.
        /// </param>
        /// <returns>
        /// Non-zero if the token was added; zero if it was already present.
        /// </returns>
        public static bool AddToken( /* CORE */
            ulong token,           /* in */
            IPluginData pluginData /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (tokens == null)
                    tokens = new TokenList();

                if (tokens.ContainsKey(token))
                    return false;

                tokens.Add(token, pluginData);
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes a sandbox token from the token list, discarding the list
        /// entirely when it becomes empty.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to remove.
        /// </param>
        /// <returns>
        /// Non-zero if the token was found and removed; otherwise, zero.
        /// </returns>
        public static bool RemoveToken( /* CORE */
            ulong token /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (tokens == null)
                    return false;

                if (!tokens.Remove(token))
                    return false;

                if (tokens.Count == 0)
                    tokens = null;

                return true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region File Name Management Methods
        /// <summary>
        /// Creates a snapshot of the file names in the specified list,
        /// optionally including the associated plugin and result information
        /// for each file name.
        /// </summary>
        /// <param name="fileNames">
        /// The file name list to copy from.
        /// </param>
        /// <param name="pluginData">
        /// When not null, limits the results to file names associated with the
        /// specified plugin data.
        /// </param>
        /// <param name="flags">
        /// The configuration file flags that control whether the plugin and
        /// result information is included for each file name.
        /// </param>
        /// <param name="hasFlags">
        /// The configuration file flags that must be present in
        /// <paramref name="flags" /> in order for the plugin and result
        /// information to be included.
        /// </param>
        /// <returns>
        /// A new list of file names, or null if <paramref name="fileNames" />
        /// is null.  When the required flags are present, each entry is
        /// expanded into the plugin string, the file name, and the result
        /// string.
        /// </returns>
        private static IEnumerable<string> CopyFileNames( /* CORE */
            FileNameList fileNames,         /* in */
            IPluginData pluginData,         /* in */
            ConfigurationFileFlags flags,   /* in */
            ConfigurationFileFlags hasFlags /* in */
            )
        {
            if (fileNames == null)
                return null;

            if (!CertificateSharedOps.HasFlags(
                    flags, hasFlags, true))
            {
                return new StringList(fileNames.Keys);
            }

            StringList list = new StringList();

            foreach (FileNamePair pair in fileNames)
            {
                FileNameAnyPair anyPair = pair.Value;

                if ((pluginData != null) && ((anyPair == null) ||
                    !Object.ReferenceEquals(anyPair.X, pluginData)))
                {
                    continue;
                }

                if ((anyPair != null) && (anyPair.X != null))
                    list.Add(anyPair.X.ToString());
                else
                    list.Add((string)null);

                list.Add(pair.Key);

                if ((anyPair != null) && (anyPair.Y != null))
                    list.Add(anyPair.Y.ToString());
                else
                    list.Add((string)null);
            }

            return list;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes file names from the specified list, optionally limiting the
        /// removal to those associated with a particular plugin.
        /// </summary>
        /// <param name="fileNames">
        /// The file name list to remove entries from.
        /// </param>
        /// <param name="pluginData">
        /// When not null, limits the removal to file names associated with the
        /// specified plugin data.
        /// </param>
        /// <returns>
        /// The number of file names that were removed.
        /// </returns>
        private static int RemoveFileNames( /* CORE */
            FileNameList fileNames, /* in */
            IPluginData pluginData  /* in */
            )
        {
            int count = 0;

            if (fileNames != null)
            {
                FileNameList localFileNames = new FileNameList(
                    fileNames);

                foreach (FileNamePair pair in localFileNames)
                {
                    if ((pluginData != null) &&
                        !Object.ReferenceEquals(
                            pair.Value, pluginData))
                    {
                        continue;
                    }

                    string fileName = pair.Key;

                    if (String.IsNullOrEmpty(fileName))
                        continue;

                    if (fileNames.Remove(fileName))
                        count++;
                }
            }

            return count;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the success and error file name lists.  When plugin data is
        /// specified, only the file names associated with that plugin are
        /// removed; otherwise, both lists are cleared entirely.
        /// </summary>
        /// <param name="pluginData">
        /// When not null, limits the removal to file names associated with the
        /// specified plugin data.
        /// </param>
        /// <returns>
        /// The number of file names that were removed.
        /// </returns>
        public static int ClearFileNames( /* CORE */
            IPluginData pluginData /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                int count = 0;

                if (pluginData != null)
                {
                    if (okFileNames != null)
                    {
                        count += RemoveFileNames(
                            okFileNames, pluginData);
                    }

                    if (errorFileNames != null)
                    {
                        count += RemoveFileNames(
                            errorFileNames, pluginData);
                    }
                }
                else
                {
                    if (okFileNames != null)
                    {
                        count += okFileNames.Count;
                        okFileNames.Clear();
                    }

                    if (errorFileNames != null)
                    {
                        count += errorFileNames.Count;
                        errorFileNames.Clear();
                    }
                }

                return count;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a snapshot of the file names for sandbox scripts that have
        /// succeeded, optionally including the associated plugin and result
        /// information.
        /// </summary>
        /// <param name="pluginData">
        /// When not null, limits the results to file names associated with the
        /// specified plugin data.
        /// </param>
        /// <param name="flags">
        /// The configuration file flags that control whether the plugin and
        /// result information is included for each file name.
        /// </param>
        /// <returns>
        /// A new list of successful file names, or null if there are none.
        /// </returns>
        public static IEnumerable<string> CopyOkFileNames( /* CORE */
            IPluginData pluginData,      /* in */
            ConfigurationFileFlags flags /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return CopyFileNames(
                    okFileNames, pluginData, flags,
                    ConfigurationFileFlags.WithOkResults);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a snapshot of the file names for sandbox scripts that have
        /// failed, optionally including the associated plugin and result
        /// information.
        /// </summary>
        /// <param name="pluginData">
        /// When not null, limits the results to file names associated with the
        /// specified plugin data.
        /// </param>
        /// <param name="flags">
        /// The configuration file flags that control whether the plugin and
        /// result information is included for each file name.
        /// </param>
        /// <returns>
        /// A new list of failed file names, or null if there are none.
        /// </returns>
        public static IEnumerable<string> CopyErrorFileNames( /* CORE */
            IPluginData pluginData,      /* in */
            ConfigurationFileFlags flags /* in */
            ) /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return CopyFileNames(
                    errorFileNames, pluginData, flags,
                    ConfigurationFileFlags.WithErrorResults);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records a file name as having succeeded during sandbox evaluation,
        /// along with its associated plugin data and result.
        /// </summary>
        /// <param name="fileName">
        /// The file name of the sandbox script that succeeded.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to associate with the file name.  This value may be
        /// null.
        /// </param>
        /// <param name="result">
        /// The result to associate with the file name.
        /// </param>
        /// <returns>
        /// Non-zero if the file name was recorded; zero if
        /// <paramref name="fileName" /> is null or empty.
        /// </returns>
        public static bool AddOkFileName(
            string fileName,        /* in */
            IPluginData pluginData, /* in: OPTIONAL */
            Result result           /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (okFileNames == null)
                    okFileNames = new FileNameList();

                if (String.IsNullOrEmpty(fileName))
                    return false;

                okFileNames[fileName] = new FileNameAnyPair(
                    pluginData, result);

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records a file name as having failed during sandbox evaluation,
        /// along with its associated plugin data and result.
        /// </summary>
        /// <param name="fileName">
        /// The file name of the sandbox script that failed.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to associate with the file name.  This value may be
        /// null.
        /// </param>
        /// <param name="result">
        /// The result to associate with the file name.
        /// </param>
        /// <returns>
        /// Non-zero if the file name was recorded; zero if
        /// <paramref name="fileName" /> is null or empty.
        /// </returns>
        public static bool AddErrorFileName(
            string fileName,        /* in */
            IPluginData pluginData, /* in: OPTIONAL */
            Result result           /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (errorFileNames == null)
                    errorFileNames = new FileNameList();

                if (String.IsNullOrEmpty(fileName))
                    return false;

                errorFileNames[fileName] = new FileNameAnyPair(
                    pluginData, result);

                return true;
            }
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Interpreter Cleanup Methods
        /// <summary>
        /// Creates a snapshot of the sandbox token list.
        /// </summary>
        /// <returns>
        /// A new copy of the sandbox token list, or null if no tokens have
        /// been added.
        /// </returns>
        private static TokenList CopyTokens() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (tokens == null)
                    return null;

                return new TokenList(tokens);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Cleans up the sandbox interpreters associated with the tracked
        /// tokens, removing each token and resetting the primary token as
        /// appropriate.  When plugin data is specified, only the interpreters
        /// associated with that plugin are cleaned up.
        /// </summary>
        /// <param name="pluginData">
        /// When not null, limits the cleanup to interpreters associated with
        /// the specified plugin data.
        /// </param>
        /// <returns>
        /// A count of the cleanup actions that were performed across all
        /// affected tokens.
        /// </returns>
        public static int CleanupInterpreters( /* CORE */
            IPluginData pluginData /* in: OPTIONAL */
            )
        {
            int count = 0;
            TokenList localTokens = CopyTokens();

            if (localTokens != null)
            {
                foreach (TokenPair pair in localTokens)
                {
                    if ((pluginData != null) &&
                        !Object.ReferenceEquals(pair.Value, pluginData))
                    {
                        continue;
                    }

                    ulong token = pair.Key;

                    if (CertificateScriptOps.CleanupInterpreter(token))
                        count++;

                    if (RemoveToken(token))
                        count++;

                    if (MaybeResetPrimaryToken(token))
                        count++;
                }
            }

            return count;
        }
        #endregion
    }
}
