/*
 * SandboxOps.cs --
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
using System.IO;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

#if KAPOK_PRIVATE
using Kapok.Components.Private;
#endif

using SandboxTokenPair = System.Collections.Generic.KeyValuePair<
    System.Guid, ulong?>;

using SandboxTokenDictionary = System.Collections.Generic.Dictionary<
    System.Guid, ulong?>;

using SandboxRuleSetDictionary = System.Collections.Generic.Dictionary<
    System.Guid, Eagle._Interfaces.Public.IRuleSet>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This class implements a sandbox subsystem that manages one or more
    /// interpreters for use on a server.  Generally, all of these will be
    /// "safe" interpreters with a very limited subset of commands.
    /// </summary>
    [ObjectId("be8fafeb-9590-4f4d-919f-7401edf1cd93")]
    internal static class SandboxOps
    {
        /// <summary>
        /// This class is responsible for the management of tokens, both
        /// a global token and a logical list of tokens, each of which is
        /// specific to an API key identifier.  This class is designed in
        /// such a way that if an API key identifier does not map to its
        /// own token, the global token will be used instead, whenever it
        /// is appropriate to do so.
        /// </summary>
        #region Token Management Helper Class
        [ObjectId("5df398a0-1a4f-4005-a4a0-4486cf4cef9d")]
        internal static class TokenManagement
        {
            #region Private Static Data
            /// <summary>
            /// This field is used to synchronize access to the logical list
            /// of tokens.
            /// </summary>
            private static readonly object syncRoot = new object();

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// This field is used to contain the logical list of tokens for
            /// each API key identifier that have been explicitly allowed
            /// access.
            /// </summary>
            private static SandboxTokenDictionary allowTokens = null;

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// This field is used to contain the logical list of tokens for
            /// each API key identifier that have been explicitly denied
            /// access.
            /// </summary>
            private static SandboxTokenDictionary denyTokens = null;

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// This field is used to contain the logical list of tokens for
            /// each API key identifier that have been explicitly granted
            /// administrator access.
            /// </summary>
            private static SandboxTokenDictionary administratorTokens = null;

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// This field is used to contain the logical list of tokens for
            /// each API key identifier that have been explicitly granted
            /// simulated-only access.
            /// </summary>
            private static SandboxTokenDictionary fakeTokens = null;

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// This field is used to contain the logical list of rulesets for
            /// each API key identifier that have been explicitly configured.
            /// </summary>
            private static SandboxRuleSetDictionary ruleSets = null;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Per-Thread Data
            /// <summary>
            /// This per-thread field contains the (global) token for this
            /// thread.  It will be used when a (more specific) token is not
            /// available for a particular API key identifier.
            /// available.
            /// </summary>
            [ThreadStatic()]
            private static ulong? anonymousToken = null;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Methods
            /// <summary>
            /// Attempts to grant or revoke access, based on the specified API
            /// key identifier.
            /// </summary>
            /// <param name="apiKeyId">
            /// The API key identifier that represents the access to grant or
            /// revoke.
            /// </param>
            /// <param name="changeType">
            /// The specific type of access control operation to perform, e.g.
            /// grant or revoke.
            /// </param>
            /// <param name="ruleSet">
            /// The optional <see cref="IRuleSet" /> to use when creating the
            /// associated sandbox interpreter, if any.  This parameter may be
            /// null.
            /// </param>
            /// <param name="error">
            /// Upon success, the value of this parameter is undefined.  Upon
            /// failure, this parameter will be modified to contain an
            /// appropriate error message.
            /// </param>
            /// <returns>
            /// Upon success, <see cref="ReturnCode.Ok" /> will be returned.
            /// Upon failure, <see cref="ReturnCode.Error" /> will be returned.
            /// </returns>
            public static ReturnCode Change(
                Guid apiKeyId,               /* in */
                AccessChangeType changeType, /* in */
                IRuleSet ruleSet,            /* in: OPTIONAL */
                ref Result error             /* out */
                )
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (apiKeyId.Equals(Guid.Empty))
                    {
                        error = "forbidden API key";
                        return ReturnCode.Error;
                    }

                    switch (changeType)
                    {
                        case AccessChangeType.Grant:
                            {
                                if (allowTokens == null)
                                    allowTokens = new SandboxTokenDictionary();

                                if (allowTokens.ContainsKey(apiKeyId))
                                {
                                    error = "access already granted";
                                    return ReturnCode.Error;
                                }

                                allowTokens[apiKeyId] = null;
                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Revoke:
                            {
                                if (allowTokens == null)
                                    allowTokens = new SandboxTokenDictionary();

                                if (!allowTokens.ContainsKey(apiKeyId))
                                {
                                    error = "access already revoked";
                                    return ReturnCode.Error;
                                }

                                if (!allowTokens.Remove(apiKeyId))
                                {
                                    error = "could not revoke access";
                                    return ReturnCode.Error;
                                }

                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Ban:
                            {
                                if (denyTokens == null)
                                    denyTokens = new SandboxTokenDictionary();

                                if (denyTokens.ContainsKey(apiKeyId))
                                {
                                    error = "access already banned";
                                    return ReturnCode.Error;
                                }

                                denyTokens[apiKeyId] = null;
                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Unban:
                            {
                                if (denyTokens == null)
                                    denyTokens = new SandboxTokenDictionary();

                                if (!denyTokens.ContainsKey(apiKeyId))
                                {
                                    error = "access already unbanned";
                                    return ReturnCode.Error;
                                }

                                if (!denyTokens.Remove(apiKeyId))
                                {
                                    error = "could not unban access";
                                    return ReturnCode.Error;
                                }

                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Promote:
                            {
                                if (administratorTokens == null)
                                    administratorTokens = new SandboxTokenDictionary();

                                if (administratorTokens.ContainsKey(apiKeyId))
                                {
                                    error = "access already promoted";
                                    return ReturnCode.Error;
                                }

                                administratorTokens[apiKeyId] = null;
                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Demote:
                            {
                                if (administratorTokens == null)
                                    administratorTokens = new SandboxTokenDictionary();

                                if (!administratorTokens.ContainsKey(apiKeyId))
                                {
                                    error = "access already demoted";
                                    return ReturnCode.Error;
                                }

                                if (!administratorTokens.Remove(apiKeyId))
                                {
                                    error = "could not demote access";
                                    return ReturnCode.Error;
                                }

                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Fake:
                            {
                                if (fakeTokens == null)
                                    fakeTokens = new SandboxTokenDictionary();

                                if (fakeTokens.ContainsKey(apiKeyId))
                                {
                                    error = "access already fake";
                                    return ReturnCode.Error;
                                }

                                fakeTokens[apiKeyId] = null;
                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Real:
                            {
                                if (fakeTokens == null)
                                    fakeTokens = new SandboxTokenDictionary();

                                if (!fakeTokens.ContainsKey(apiKeyId))
                                {
                                    error = "access already real";
                                    return ReturnCode.Error;
                                }

                                if (!fakeTokens.Remove(apiKeyId))
                                {
                                    error = "could not remove fake access";
                                    return ReturnCode.Error;
                                }

                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Restrict:
                            {
                                if (ruleSets == null)
                                    ruleSets = new SandboxRuleSetDictionary();

                                if (ruleSets.ContainsKey(apiKeyId))
                                {
                                    error = "access already restricted";
                                    return ReturnCode.Error;
                                }

                                ruleSets[apiKeyId] = ruleSet;
                                return ReturnCode.Ok;
                            }
                        case AccessChangeType.Unrestrict:
                            {
                                if (ruleSets == null)
                                    ruleSets = new SandboxRuleSetDictionary();

                                if (!ruleSets.ContainsKey(apiKeyId))
                                {
                                    error = "access already unrestricted";
                                    return ReturnCode.Error;
                                }

                                if (!ruleSets.Remove(apiKeyId))
                                {
                                    error = "could not remove restricted access";
                                    return ReturnCode.Error;
                                }

                                return ReturnCode.Ok;
                            }
                        default:
                            {
                                error = String.Format(
                                    "unsupported access change operation: {0}",
                                    changeType);

                                return ReturnCode.Error;
                            }
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to determine if the specified API key identifier has
            /// been explicitly allowed access.
            /// </summary>
            /// <param name="apiKeyId">
            /// The optional API key identifier associated with the cached
            /// token.
            /// </param>
            /// <returns>
            /// Non-zero if the API key identifier is allowed; otherwise, zero.
            /// </returns>
            public static bool IsAllowed(
                Guid apiKeyId /* in */
                )
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if ((allowTokens != null) &&
                        allowTokens.ContainsKey(apiKeyId))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to determine if the specified API key identifier has
            /// been explicitly denied access, i.e. is it banned?
            /// </summary>
            /// <param name="apiKeyId">
            /// The optional API key identifier associated with the cached
            /// token.
            /// </param>
            /// <returns>
            /// Non-zero if the API key identifier is banned; otherwise, zero.
            /// </returns>
            public static bool IsDenied(
                Guid apiKeyId /* in */
                )
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if ((denyTokens != null) &&
                        denyTokens.ContainsKey(apiKeyId))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to determine if the specified API key identifier has
            /// been explicitly promoted to administrator access.
            /// </summary>
            /// <param name="apiKeyId">
            /// The optional API key identifier associated with the cached
            /// token.
            /// </param>
            /// <returns>
            /// Non-zero if the API key identifier has administrator access;
            /// otherwise, zero.
            /// </returns>
            public static bool IsAdministrator(
                Guid apiKeyId /* in */
                )
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if ((administratorTokens != null) &&
                        administratorTokens.ContainsKey(apiKeyId))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to determine if the specified API key identifier has
            /// been explicitly granted simulated-only access.
            /// </summary>
            /// <param name="apiKeyId">
            /// The optional API key identifier associated with the cached
            /// token.
            /// </param>
            /// <returns>
            /// Non-zero if the API key identifier has simulated-only access;
            /// otherwise, zero.
            /// </returns>
            public static bool IsFake(
                Guid apiKeyId /* in */
                )
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if ((fakeTokens != null) &&
                        fakeTokens.ContainsKey(apiKeyId))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to determine if the specified API key identifier has
            /// been configured with an <see cref="IRuleSet" />.
            /// </summary>
            /// <param name="apiKeyId">
            /// The optional API key identifier associated with the cached
            /// token.
            /// </param>
            /// <returns>
            /// Either the <see cref="IRuleSet" /> associated with the
            /// specified API key identifier -OR- null if it cannot be
            /// determined or does not exist.
            /// </returns>
            public static IRuleSet HasRuleSet(
                Guid apiKeyId /* in */
                )
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    IRuleSet ruleSet;

                    if ((ruleSets != null) &&
                        ruleSets.TryGetValue(apiKeyId, out ruleSet))
                    {
                        return ruleSet;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to determine if a cached token is already available.
            /// </summary>
            /// <param name="apiKeyId">
            /// The optional API key identifier associated with the cached
            /// token.
            /// </param>
            /// <param name="token">
            /// Upon success, this parameter will be modified to contain the
            /// token to use.  Upon failure, the value of this parameter is
            /// undefined.
            /// </param>
            /// <param name="noAnonymous">
            /// This parameter will be modified to contain non-zero if the
            /// caller should not attempt to use the cached settings nor the
            /// (global) anonymous token.
            /// </param>
            /// <returns>
            /// Non-zero if an appropriate token was found;
            /// otherwise, zero.
            /// </returns>
            public static bool Have(
                Guid? apiKeyId,      /* in: OPTIONAL */
                out ulong token,     /* out */
                out bool noAnonymous /* out */
                )
            {
                if (apiKeyId != null)
                {
                    lock (syncRoot) /* TRANSACTIONAL */
                    {
                        Guid localApiKeyId = (Guid)apiKeyId;
                        ulong? localToken;

                        if ((allowTokens != null) &&
                            allowTokens.TryGetValue(
                                localApiKeyId, out localToken))
                        {
                            token = (localToken != null) ?
                                (ulong)localToken : 0;

                            noAnonymous = true;

                            return (localToken != null);
                        }
                    }
                }

                if (anonymousToken == null)
                {
                    token = 0;
                    noAnonymous = false;

                    return false;
                }

                token = (ulong)anonymousToken;
                noAnonymous = false;

                return true;
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to create a token, possibly for a specific API key
            /// identifier.
            /// </summary>
            /// <param name="apiKeyId">
            /// The optional API key identifier associated with the token.
            /// </param>
            /// <param name="token">
            /// Upon success, this parameter will be modified to contain the
            /// token to use.  Upon failure, the value of this parameter is
            /// undefined.
            /// </param>
            /// <param name="noAnonymous">
            /// Non-zero if the (global) anonymous token should not be used.
            /// </param>
            /// <returns>
            /// Non-zero if the token was created; otherwise, zero.
            /// </returns>
            public static bool Create(
                Guid? apiKeyId,   /* in */
                bool noAnonymous, /* in */
                out ulong token   /* out */
                )
            {
                ulong? localToken; /* REUSED */

                if (apiKeyId != null)
                {
                    lock (syncRoot) /* TRANSACTIONAL */
                    {
                        Guid localApiKeyId = (Guid)apiKeyId;

                        if ((allowTokens != null) &&
                            allowTokens.TryGetValue(
                                localApiKeyId, out localToken))
                        {
                            if (localToken != null)
                            {
                                token = 0;
                                return false;
                            }

                            localToken = Utility.GetRandomNumber();
                            allowTokens[localApiKeyId] = localToken;
                            token = (ulong)localToken;

                            return true;
                        }
                    }
                }

                if (noAnonymous || (anonymousToken != null))
                {
                    token = 0;
                    return false;
                }

                localToken = Utility.GetRandomNumber();
                anonymousToken = localToken;
                token = (ulong)localToken;

                return true;
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to reset the token, possibly specific to an API key.
            /// </summary>
            /// <param name="apiKeyId">
            /// The optional API key identifier associated with the token.
            /// </param>
            /// <param name="noAnonymous">
            /// Non-zero if the (global) anonymous token should not be used.
            /// </param>
            /// <returns>
            /// Non-zero if a cached token was reset; otherwise, zero.
            /// </returns>
            public static bool Reset(
                Guid? apiKeyId,  /* in: OPTIONAL */
                bool noAnonymous /* in */
                )
            {
                if (apiKeyId != null)
                {
                    lock (syncRoot) /* TRANSACTIONAL */
                    {
                        Guid localApiKeyId = (Guid)apiKeyId;

                        if ((allowTokens != null) &&
                            allowTokens.Remove(localApiKeyId))
                        {
                            return true;
                        }
                    }
                }

                if (noAnonymous || (anonymousToken == null))
                    return false;

                anonymousToken = null;
                return true;
            }

            ///////////////////////////////////////////////////////////////////
            /// <summary>
            /// Attempts to invoke the specified callback for each token that
            /// needs to be cleaned up.
            /// </summary>
            /// <param name="callback">
            /// This is the callback to invoke for each token that needs to be
            /// cleaned up.
            /// </param>
            /// <param name="noAnonymous">
            /// Non-zero if the (global) anonymous token should not be used.
            /// </param>
            /// <param name="errors">
            /// Upon any failures, this parameter will be modified to include
            /// the appropriate error information.
            /// </param>
            /// <returns>
            /// The total number of tokens that were successfully cleaned up.
            /// </returns>
            public static int Cleanup(
                DisposeCallback callback, /* in */
                bool noAnonymous,         /* in */
                ref ResultList errors     /* in, out */
                )
            {
                if (callback == null)
                    return Count.Invalid;

                int count = 0;
                ulong? token; /* REUSED */

                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (allowTokens != null)
                    {
                        foreach (SandboxTokenPair pair in allowTokens)
                        {
                            token = pair.Value;

                            if (token == null)
                                continue;

                            try
                            {
                                callback(token);
                                count++;
                            }
                            catch (Exception e)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(e);
                            }
                        }
                    }
                }

                if (noAnonymous)
                    return count;

                token = anonymousToken;

                if (token != null)
                {
                    try
                    {
                        callback(token);
                        count++;
                    }
                    catch (Exception e)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(e);
                    }
                }

                return count;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
        /// <summary>
        /// This is the default ruleset file name for use when creating the
        /// sandboxed interpreters.  If this value is null, the full "safe"
        /// command set will be available; otherwise, only the "safe" commands
        /// that also match against this ruleset will be available.
        ///
        /// TODO: Consider changing this file name to a list of available rule
        ///       set file names that are considered to be "safe" for server
        ///       usage.
        /// </summary>
        private const string RuleSetFileNameOnly = "tcl84.ruleSet";

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// These are the base flags to use when creating an interpreter for
        /// use by this subsystem.
        /// </summary>
        private const CreateFlags UnsafeCreateFlags =
            (CreateFlags.FastSingleUse & ~(CreateFlags.Initialize |
            CreateFlags.ThrowOnError)) | CreateFlags.IfNecessary |
            CreateFlags.IfCannotLock | CreateFlags.MeasureTime |
            CreateFlags.NoDispose;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// These are the base flags to use when creating a "safe" interpreter
        /// for use by this subsystem.
        /// </summary>
        private const CreateFlags SafeCreateFlags =
            CreateFlags.SafeAndHideUnsafe;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// These are the base flags to use when creating a "safe" interpreter
        /// that should not include any built-in commands, functions, policies,
        /// etc.
        /// </summary>
        private const CreateFlags NoBuiltInsCreateFlags =
            CreateFlags.NoCommands | CreateFlags.NoFunctions |
            CreateFlags.NoCoreTraces | CreateFlags.NoCorePolicies;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// These are the base flags to use when creating a "safe" interpreter
        /// -OR- an "unsafe" interpreter that is using only a subset of the
        /// available built-in commands, i.e. which means some other built-in
        /// functionality may need to be disabled, e.g. [namespace] support.
        /// </summary>
        private const CreateFlags NoRuleSetCreateFlags =
            CreateFlags.UseNamespaces;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// These are the host flags to use when creating a "safe" interpreter
        /// for use by this subsystem.
        /// </summary>
        private const HostCreateFlags SafeHostCreateFlags =
            HostCreateFlags.FastSingleUse;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Data
        /// <summary>
        /// This field is used to synchronize access to cached interpreter
        /// settings.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This field is used to cache of the interpreter settings that are
        /// used when creating an interpreter.
        /// </summary>
        private static IInterpreterSettings interpreterSettings;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Attempts to fetch (or create) an interpreter settings instance
        /// using the specified command line arguments, flags, and ruleset.
        /// </summary>
        /// <param name="apiKeyId">
        /// The optional API key identifier associated with the request.
        /// </param>
        /// <param name="ruleSet">
        /// The ruleset to use when creating the new interpreter.
        /// </param>
        /// <param name="ruleSetFileName">
        /// The fully qualified file name that should contain the ruleset to
        /// use when creating the new interpreter.
        /// </param>
        /// <param name="ruleSetType">
        /// The type of ruleset file being loaded.  Generally, this value will
        /// be <see cref="RuleSetType.CommandFile" />.
        /// </param>
        /// <param name="args">
        /// The list of command line arguments to provide to the script(s)
        /// being evaluated.
        /// </param>
        /// <param name="createFlags">
        /// The interpreter creation flags to use.
        /// </param>
        /// <param name="hostCreateFlags">
        /// The interpreter host creation flags to use.
        /// </param>
        /// <param name="noCache">
        /// Non-zero if the cached interpreter settings should not be used or
        /// changed.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, this parameter will be modified to contain an appropriate
        /// error message.
        /// </param>
        /// <returns>
        /// The interpreter settings instance to use.  This may have been from
        /// the cached or it may be brand new.
        /// </returns>
        private static IInterpreterSettings GetOrCreateSettings(
            Guid? apiKeyId,                  /* in: OPTIONAL */
            IRuleSet ruleSet,                /* in: OPTIONAL */
            string ruleSetFileName,          /* in: OPTIONAL */
            RuleSetType ruleSetType,         /* in */
            IEnumerable<string> args,        /* in: OPTIONAL */
            CreateFlags createFlags,         /* in */
            HostCreateFlags hostCreateFlags, /* in */
            bool noCache,                    /* in */
            ref Result error                 /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (!noCache && (interpreterSettings != null))
                    return interpreterSettings;

                IInterpreterSettings localInterpreterSettings =
                    InterpreterSettings.CreateDefault();

                IRuleSet localRuleSet;

                if (ruleSet != null)
                {
                    localRuleSet = ruleSet;
                }
                else if (ruleSetFileName != null)
                {
                    if (!Path.IsPathRooted(ruleSetFileName))
                    {
                        ruleSetFileName = Path.Combine(
                            GetRuleSetDirectory(apiKeyId),
                            ruleSetFileName);
                    }

#if TEST
                    localRuleSet = RuleSet.CreateFromFile(
                        ruleSetFileName, null, ruleSetType,
                        ref error);
#else
                    error = "not implemented";
                    localRuleSet = null;
#endif

                    if (localRuleSet == null)
                        return null;
                }
                else if (Utility.HasFlags(
                        ruleSetType, RuleSetType.BaseMask, false))
                {
#if TEST
                    localRuleSet = RuleSet.CreateFromFile(
                        GetRuleSetFileName(apiKeyId), null,
                        ruleSetType, ref error);
#else
                    error = "not implemented";
                    localRuleSet = null;
#endif

                    if (localRuleSet == null)
                        return null;
                }
                else
                {
                    localRuleSet = null;
                }

                localInterpreterSettings.RuleSet = localRuleSet;
                localInterpreterSettings.Args = args;
                localInterpreterSettings.CreateFlags = createFlags;
                localInterpreterSettings.HostCreateFlags = hostCreateFlags;

                if (!noCache)
                    interpreterSettings = localInterpreterSettings;

                return localInterpreterSettings;
            }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine the fully qualified path to the directory
        /// that contains all the ruleset files that should be used when
        /// creating an interpreter.
        /// </summary>
        /// <param name="apiKeyId">
        /// The optional API key identifier associated with the request.  For
        /// now, this is ignored.  In the future, this may be used to return
        /// a ruleset directory that is specific to this API key identifier.
        /// </param>
        /// <returns>
        /// The fully qualified path to the ruleset directory -OR- null if it
        /// cannot be determined.
        /// </returns>
        private static string GetRuleSetDirectory(
            Guid? apiKeyId /* in: NOT USED */
            )
        {
            string directory;

#if KAPOK_PRIVATE
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.DirectoryName;

            directory = WebSettingsOps.GetGlobal(
                EnvVars.XdgRuleSetDir, dataType);
#else
            directory = EnvironmentOps.GetVariableValue(
                EnvVars.XdgRuleSetDir);
#endif

            if (String.IsNullOrEmpty(directory))
                directory = null;

            return directory;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine the fully qualified path to the ruleset file
        /// that should be used when creating an interpreter.
        /// </summary>
        /// <param name="apiKeyId">
        /// The optional API key identifier associated with the request.  For
        /// now, this is ignored.  In the future, this may be used to return
        /// a ruleset that is specific to this API key identifier.
        /// </param>
        /// <returns>
        /// The fully qualified path to the ruleset file -OR- null if it
        /// cannot be determined.
        /// </returns>
        private static string GetRuleSetFileName(
            Guid? apiKeyId /* in: NOT USED */
            )
        {
            string directory = GetRuleSetDirectory(apiKeyId);

            if (String.IsNullOrEmpty(directory))
                return null;

            return Path.Combine(directory, RuleSetFileNameOnly);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to lookup the interpreter associated with the specified
        /// API key identifier.
        /// </summary>
        /// <param name="apiKeyId">
        /// The optional API key identifier to use as the basis for looking
        /// up the interpreter.  If this parameter is null, an attempt will
        /// be made to lookup the primary cached interpreter for the current
        /// thread.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, this parameter will be modified to contain an appropriate
        /// error message.
        /// </param>
        /// <returns>
        /// The interpreter to use for script evaluation -OR- null if it
        /// cannot be determined.
        /// </returns>
        private static Interpreter GetInterpreter(
            Guid? apiKeyId,  /* in */
            ref Result error /* out */
            )
        {
            ulong token;
            bool noAnonymous; /* NOT USED */

            if (!TokenManagement.Have(
                    apiKeyId, out token, out noAnonymous))
            {
                error = String.Format(
                    "interpreter token for {0} unavailable",
                    Utility.FormatWrapOrNull(apiKeyId));

                return null;
            }

            return GetInterpreter(token, ref error);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to lookup the interpreter associated with the specified
        /// interpreter token.
        /// </summary>
        /// <param name="token">
        /// The interpreter token to use as the basis for looking up the
        /// interpreter to use for script evaluation.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, this parameter will be modified to contain an appropriate
        /// error message.
        /// </param>
        /// <returns>
        /// The interpreter to use for script evaluation -OR- null if it
        /// cannot be determined.
        /// </returns>
        private static Interpreter GetInterpreter(
            ulong token,     /* in */
            ref Result error /* out */
            )
        {
            Interpreter interpreter = null;

            if (Value.GetInterpreter(
                    Interpreter.GetActive(), token.ToString(),
                    InterpreterType.Eagle | InterpreterType.Token,
                    ref interpreter, ref error) != ReturnCode.Ok)
            {
                return null;
            }

            return interpreter;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to lookup or create the "safe", sandboxed interpreter to
        /// use for client script evaluation, possibly specific to an API key.
        /// </summary>
        /// <param name="apiKeyId">
        /// The optional API key identifier associated with the cached
        /// interpreter token.
        /// </param>
        /// <param name="host">
        /// The string representation of the original host (IP address) for
        /// the client request being processed, if any.
        /// </param>
        /// <param name="allowHosts">
        /// The string representations for the host patterns that have been
        /// explicitly allowed access, if any.
        /// </param>
        /// <param name="denyHosts">
        /// The string representations for the host patterns that have been
        /// explicitly denied access, if any.
        /// </param>
        /// <param name="ruleSet">
        /// The ruleset to use when creating the new interpreter.
        /// </param>
        /// <param name="ruleSetFileName">
        /// The fully qualified file name that should contain the ruleset to
        /// use when creating the new interpreter.
        /// </param>
        /// <param name="ruleSetType">
        /// The type of ruleset file being loaded.  Generally, this value will
        /// be <see cref="RuleSetType.CommandFile" />.
        /// </param>
        /// <param name="args">
        /// The list of command line arguments to provide to the script(s)
        /// being evaluated.
        /// </param>
        /// <param name="unsafe">
        /// Non-zero if the created interpreter should be "unsafe", possibly
        /// with the full set of built-in commands.  Currently, this requires
        /// the specified API key identifier (<paramref name="apiKeyId" />) to
        /// correspond to an explicitly authorized administrator.
        /// </param>
        /// <param name="noBuiltIns">
        /// Non-zero if the created interpreter should not contain any of the
        /// built-in commands, functions, policies, etc.
        /// </param>
        /// <param name="interpreter">
        /// Upon success, this parameter will be modified to contain the
        /// interpreter that should be used for script evaluation.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, this parameter will be modified to contain an appropriate
        /// error message.
        /// </param>
        /// <returns>
        /// Upon success, <see cref="ReturnCode.Ok" /> will be returned.
        /// Upon failure, <see cref="ReturnCode.Error" /> will be returned.
        /// </returns>
        private static ReturnCode GetOrCreateInterpreter(
            Guid? apiKeyId,                 /* in: OPTIONAL */
            string host,                    /* in: OPTIONAL */
            IEnumerable<string> allowHosts, /* in: OPTIONAL */
            IEnumerable<string> denyHosts,  /* in: OPTIONAL */
            IRuleSet ruleSet,               /* in: OPTIONAL */
            string ruleSetFileName,         /* in: OPTIONAL */
            RuleSetType ruleSetType,        /* in */
            IEnumerable<string> args,       /* in: OPTIONAL */
            bool? @unsafe,                  /* in: OPTIONAL */
            bool? noBuiltIns,               /* in: OPTIONAL */
            ref Interpreter interpreter,    /* out */
            ref Result error                /* out */
            )
        {
            //
            // HACK: Do not allow "banned" API key identifiers to be used.
            //
            if ((apiKeyId != null) &&
                TokenManagement.IsDenied((Guid)apiKeyId))
            {
                error = String.Format(
                    "access via {0} is explicitly denied",
                    Utility.FormatWrapOrNull(apiKeyId));

                return ReturnCode.Error;
            }

            //
            // HACK: If provided, also check the specified host address to
            //       see if it has been explicitly allowed or denied.
            //
            if (host != null)
            {
                bool? match; /* REUSED */
                int? index; /* REUSED */

                if (allowHosts != null)
                {
                    match = Utility.MatchViaCIDR(
                        host, allowHosts, IpFlags.Default, out index,
                        ref error);

                    if (match == null)
                        return ReturnCode.Error;

                    if (!(bool)match)
                    {
                        error = String.Format(
                            "access via {0} ({1}) is not explicitly allowed",
                            Utility.FormatWrapOrNull(host),
                            Utility.FormatWrapOrNull(index));

                        return ReturnCode.Error;
                    }
                }

                if (denyHosts != null)
                {
                    match = Utility.MatchViaCIDR(
                        host, denyHosts, IpFlags.Default, out index,
                        ref error);

                    if (match == null)
                        return ReturnCode.Error;

                    if ((bool)match)
                    {
                        error = String.Format(
                            "access via {0} ({1}) is explicitly denied",
                            Utility.FormatWrapOrNull(host),
                            Utility.FormatWrapOrNull(index));

                        return ReturnCode.Error;
                    }
                }
            }

            //
            // NOTE: The (unique) interpreter token used to create and/or
            //       fetch the interpreter (for this thread) is stored on
            //       a per-thread basis using the ThreadStatic attribute;
            //       that means that all usage of the created interpreter
            //       is confined to this logical thread, which makes this
            //       method completely thread-safe.
            //
            ulong token;
            bool noAnonymous;

            if (!TokenManagement.Have(
                    apiKeyId, out token, out noAnonymous) &&
                !TokenManagement.Create(
                    apiKeyId, noAnonymous, out token))
            {
                error = String.Format(
                    "interpreter token for {0} unavailable",
                    Utility.FormatWrapOrNull(apiKeyId));

                return ReturnCode.Error;
            }

            CreateFlags createFlags = UnsafeCreateFlags;

            if ((@unsafe == null) || !(bool)@unsafe ||
                (apiKeyId == null) ||
                !TokenManagement.IsAdministrator((Guid)apiKeyId))
            {
                createFlags |= SafeCreateFlags;
            }

            if ((noBuiltIns != null) && (bool)noBuiltIns)
                createFlags |= NoBuiltInsCreateFlags;

            if (ruleSet != null)
                createFlags &= ~NoRuleSetCreateFlags;

            IInterpreterSettings interpreterSettings;
            Result localResult = null; /* REUSED */

            interpreterSettings = GetOrCreateSettings(
                apiKeyId, ruleSet, ruleSetFileName, ruleSetType,
                args, createFlags, SafeHostCreateFlags,
                noAnonymous, ref localResult);

            if (interpreterSettings == null)
            {
                error = localResult;
                return ReturnCode.Error;
            }

            localResult = null;

            interpreter = Interpreter.Create(
                token, interpreterSettings, true, ref localResult);

            if (interpreter == null)
            {
                error = localResult;
                return ReturnCode.Error;
            }

            interpreter.Unknown = null; /* HACK: Fail faster. */
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to cleanup (i.e. dispose) the interpreter identified by
        /// the interpreter token in <paramref name="object" />.
        /// </summary>
        /// <param name="object">
        /// This parameter contains the interpreter token to cleanup, which
        /// must be of the type <see cref="UInt64" />.
        /// </param>
        private static void CleanupInterpreterCallback(
            object @object /* in */
            )
        {
            if (!(@object is ulong))
                return;

            Interpreter interpreter;
            ulong token = (ulong)@object;
            Result error = null;

            interpreter = GetInterpreter(token, ref error);

            if (interpreter == null)
                throw new ScriptException(error);

            interpreter.SetDisposalEnabled(false, true); /* throw */
            interpreter.Dispose(); /* throw */
            interpreter = null;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Attempts to evaluate the specified (client) script in a "safe",
        /// sandboxed interpreter.
        /// </summary>
        /// <param name="apiKeyId">
        /// The optional API key identifier corresponding to the interpreter
        /// to use for script evaluation.  Currently, this will always be a
        /// "safe", sandboxed interpreter.
        /// </param>
        /// <param name="host">
        /// The string representation of the original host (IP address) for
        /// the client request being processed, if any.
        /// </param>
        /// <param name="allowHosts">
        /// The string representations for the host patterns that have been
        /// explicitly allowed access, if any.
        /// </param>
        /// <param name="denyHosts">
        /// The string representations for the host patterns that have been
        /// explicitly denied access, if any.
        /// </param>
        /// <param name="ruleSet">
        /// The ruleset to use when creating the new interpreter.
        /// </param>
        /// <param name="ruleSetFileName">
        /// The fully qualified file name that should contain the ruleset to
        /// use when creating the new interpreter.
        /// </param>
        /// <param name="ruleSetType">
        /// The type of ruleset file being loaded.  Generally, this value will
        /// be <see cref="RuleSetType.CommandFile" />.
        /// </param>
        /// <param name="args">
        /// The list of command line arguments to provide to the script(s)
        /// being evaluated.
        /// </param>
        /// <param name="text">
        /// The (client) script to be evaluated.  If an unknown command is
        /// used, evaluation will fail.
        /// </param>
        /// <param name="unsafe">
        /// Non-zero if the created interpreter should be "unsafe", possibly
        /// with the full set of built-in commands.  Currently, this requires
        /// the specified API key identifier (<paramref name="apiKeyId" />) to
        /// correspond to an explicitly authorized administrator.
        /// </param>
        /// <param name="noBuiltIns">
        /// Non-zero if the created interpreter should not contain any of the
        /// default commands, functions, policies, etc.
        /// </param>
        /// <param name="result">
        /// Upon success, this is the overall result of the script.  Upon
        /// failure, this parameter will be modified to contain an appropriate
        /// error message.
        /// </param>
        /// <returns>
        /// Upon success, <see cref="ReturnCode.Ok" /> will be returned.
        /// Upon failure, <see cref="ReturnCode.Error" /> will be returned.
        /// </returns>
        public static ReturnCode EvaluateScript(
            Guid? apiKeyId,                 /* in: OPTIONAL */
            string host,                    /* in: OPTIONAL */
            IEnumerable<string> allowHosts, /* in: OPTIONAL */
            IEnumerable<string> denyHosts,  /* in: OPTIONAL */
            IRuleSet ruleSet,               /* in: OPTIONAL */
            string ruleSetFileName,         /* in: OPTIONAL */
            RuleSetType ruleSetType,        /* in */
            IEnumerable<string> args,       /* in: OPTIONAL */
            string text,                    /* in */
            bool? @unsafe,                  /* in: OPTIONAL */
            bool? noBuiltIns,               /* in: OPTIONAL */
            ref Result result               /* out */
            )
        {
            Interpreter interpreter = null;

            if (GetOrCreateInterpreter(
                    apiKeyId, host, allowHosts, denyHosts,
                    ruleSet, ruleSetFileName, ruleSetType,
                    args, @unsafe, noBuiltIns, ref interpreter,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return interpreter.EvaluateScript(text, ref result);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to cleanup the cached interpreter, possibly one that is
        /// specific to an API key.
        /// </summary>
        /// <param name="apiKeyId">
        /// The optional API key identifier corresponding to the interpreter
        /// to cleanup.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, this parameter will be modified to contain an appropriate
        /// error message.
        /// </param>
        /// <returns>
        /// Upon success, <see cref="ReturnCode.Ok" /> will be returned.
        /// Upon failure, <see cref="ReturnCode.Error" /> will be returned.
        /// </returns>
        public static ReturnCode CleanupInterpreter(
            Guid? apiKeyId,  /* in: OPTIONAL */
            ref Result error /* out */
            )
        {
            ulong token;
            bool noAnonymous;

            if (!TokenManagement.Have(
                    apiKeyId, out token, out noAnonymous))
            {
                return ReturnCode.Ok;
            }

            try
            {
                CleanupInterpreterCallback(token); /* throw */

                if (!TokenManagement.Reset(apiKeyId, noAnonymous))
                {
                    error = String.Format(
                        "interpreter token for {0} not reset",
                        Utility.FormatWrapOrNull(apiKeyId));

                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to cleanup all cached interpreters.
        /// </summary>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, this parameter will be modified to contain an appropriate
        /// error message.
        /// </param>
        /// <returns>
        /// Upon success, <see cref="ReturnCode.Ok" /> will be returned.
        /// Upon failure, <see cref="ReturnCode.Error" /> will be returned.
        /// </returns>
        public static ReturnCode CleanupInterpreters(
            ref Result error /* out */
            )
        {
            ResultList errors = null;

            /* IGNORED */
            TokenManagement.Cleanup(
                CleanupInterpreterCallback, false, ref errors);

            if (errors != null)
            {
                error = errors;
                return ReturnCode.Error;
            }
            else
            {
                return ReturnCode.Ok;
            }
        }
        #endregion
    }
}
