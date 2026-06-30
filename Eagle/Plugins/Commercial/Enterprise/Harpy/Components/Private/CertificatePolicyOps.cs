/*
 * CertificatePolicyOps.cs --
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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

#if !NET_STANDARD_20
using Microsoft.Win32;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Components.Public.Delegates;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using Helpers = Licensing.Components.Private.Commands.Helpers;

using PolicyDictionary =
    System.Collections.Generic.Dictionary<
        Eagle._Components.Public.PolicyType,
        Eagle._Components.Public.ExecutionPolicy>;

using KeyNameDictionary =
    System.Collections.Generic.Dictionary<
    Eagle._Components.Public.PolicyType, string>;

using KeyRingNameDictionary =
    System.Collections.Generic.Dictionary<
    Eagle._Components.Public.PolicyType, string>;

using ScriptFlagsDictionary =
    System.Collections.Generic.Dictionary<
        Eagle._Components.Public.PolicyType,
        Eagle._Components.Public.ScriptFlags>;

using PathFlagsDictionary =
    System.Collections.Generic.Dictionary<
        Eagle._Components.Public.PolicyType,
        Eagle._Components.Public.PathFlags>;

using NetworkFlagsDictionary =
    System.Collections.Generic.Dictionary<
        Eagle._Components.Public.PolicyType,
        Eagle._Components.Public.NetworkFlags>;

using ClientDataPair =
    Eagle._Interfaces.Public.IAnyPair<
        Eagle._Components.Public.Interpreter,
        Eagle._Interfaces.Public.IClientData>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the core implementation of the certificate-based execution
    /// policies (script, file, and stream callbacks) used by the licensing
    /// subsystem, along with helpers for managing the associated per-policy
    /// state (certificates, assemblies, key names, key ring names, and the
    /// various policy flags).
    /// </summary>
    [ObjectId("d32ad573-1508-436d-b2e2-efeae211139d")]
    internal static class CertificatePolicyOps
    {
        #region Private Constants
        /* CORE? */
        /// <summary>
        /// The complete set of <see cref="PolicyType" /> values that are
        /// managed by this class.
        /// </summary>
        private static readonly PolicyType[] allPolicyTypes = {
            PolicyType.Script, PolicyType.File, PolicyType.Stream,
            PolicyType.License, PolicyType.KeyPair, PolicyType.Trace,
            PolicyType.Other
        };
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the complete set of <see cref="PolicyType" /> values that are
        /// managed by this class.
        /// </summary>
        /// <returns>
        /// An enumerable of all supported <see cref="PolicyType" /> values.
        /// </returns>
        public static IEnumerable<PolicyType> GetPolicyTypes() /* CORE? */
        {
            return allPolicyTypes;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the licensing plugin associated with the specified
        /// interpreter.  This overload discards any error information.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose associated plugin is being queried.
        /// </param>
        /// <param name="plugin">
        /// Upon success, receives the located plugin.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode GetPlugin( /* CORE? */
            Interpreter interpreter, /* in */
            ref IPlugin plugin       /* out */
            )
        {
            Result error = null; /* NOT USED */

            return GetPlugin(interpreter, ref plugin, ref error);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the licensing plugin associated with the specified
        /// interpreter, first checking for a pending plugin and then
        /// falling back to looking it up by name within the interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose associated plugin is being queried.
        /// </param>
        /// <param name="plugin">
        /// Upon success, receives the located plugin.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode GetPlugin( /* CORE? */
            Interpreter interpreter, /* in */
            ref IPlugin plugin,      /* out */
            ref Result error         /* out */
            )
        {
            ResultList errors = null;
            IPlugin localPlugin; /* REUSED */
            Result localError; /* REUSED */

#if LICENSING
            localError = null;

            localPlugin = CertificatePluginState.GetPending(
                interpreter, Utility.GetCurrentThreadId(),
                ref localError);

            if (localPlugin != null)
            {
                plugin = localPlugin;
                return ReturnCode.Ok;
            }
            else if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }
#endif

            if (interpreter != null)
            {
                string name = null; /* NOT USED */
                long token = 0; /* NOT USED */

                localPlugin = null;
                localError = null;

                if (interpreter.GetPlugin(
                        CertificateAssemblyOps.GetName(),
                        LookupFlags.NoWrapper |
                            LookupFlags.WithPolicies,
                        ref name, ref token, ref localPlugin,
                        ref localError) == ReturnCode.Ok)
                {
                    plugin = localPlugin;
                    return ReturnCode.Ok;
                }
                else if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            error = errors;
            return ReturnCode.Error;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the script used to require (load) the core licensing
        /// package from the certificate assembly directory.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which the require script is being built.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// The package require script, or null if it could not be built.
        /// </returns>
        public static string GetPackageRequireScript( /* CORE? */
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            IEnumerable<string> paths = new string[] {
                CertificateAssemblyOps.GetDirectory()
            };

            string text = Utility.GetPackageScanCommand(
                interpreter, null, paths, ref error);

            if (text == null) /* IMPOSSIBLE? */
                return null;

            return String.Format(
                Constants.RequireCorePackageScript, text);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the licensing plugin associated with the specified
        /// interpreter, attempting to load the package and locate the
        /// plugin if it is not already present.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose associated plugin is being queried or
        /// loaded.
        /// </param>
        /// <param name="plugin">
        /// Upon success, receives the located plugin.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the accumulated error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode GetOrLoadPlugin( /* CORE? */
            Interpreter interpreter, /* in */
            ref IPlugin plugin,      /* out */
            ref Result error         /* out */
            )
        {
            ResultList errors = null;
            Result localResult = null; /* REUSED */

            if (GetPlugin(
                    interpreter, ref plugin,
                    ref localResult) == ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }
            else if (localResult != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localResult);
            }

            if (interpreter != null)
            {
                localResult = null;

                string text = GetPackageRequireScript(
                    interpreter, ref localResult);

                if (text != null)
                {
                    localResult = null;

                    if (interpreter.EvaluateTrustedScript(
                            text, Constants.ScriptTrustFlags,
                            ref localResult) != ReturnCode.Ok)
                    {
                        if (localResult != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localResult);
                        }
                    }
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            localResult = null;

            if (GetPlugin(
                    interpreter, ref plugin,
                    ref localResult) == ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }
            else if (localResult != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localResult);
            }

            error = errors;
            return ReturnCode.Error;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified public key token is present on
        /// the trusted key ring named <paramref name="keyRingName" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to look up the trusted key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the trusted key ring to check.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token to look for on the trusted key ring.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// Non-zero if the public key token is trusted; otherwise, zero.
        /// </returns>
        public static bool IsTrustedPublicKeyToken( /* CORE? */
            Interpreter interpreter, /* in */
            string keyRingName,      /* in */
            byte[] publicKeyToken,   /* in */
            ref Result error         /* out */
            )
        {
            if (publicKeyToken == null)
            {
                error = "invalid public key token";
                return false;
            }

            IKeyRing keyRing;

            keyRing = CertificateKeyRingState.GetTrusted(
                interpreter, keyRingName, ref error);

            if (keyRing == null)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Could not get trusted key ring, error: {0}",
                        Utility.FormatWrapOrNull(true, false, error)),
                    typeof(CertificatePolicyOps).Name,
                    TracePriority.MediumHigh, 0); /* EXEMPT */
#endif

                return false;
            }

            if (!keyRing.IsPresentByToken(publicKeyToken))
            {
                error = String.Format(
                    "public key token {0} is not trusted",
                    CertificateDataOps.FormatPublicKeyToken(
                    publicKeyToken, true, true));

                return false;
            }

            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the plugins associated with the two specified
        /// interpreters reside in different application domains.
        /// </summary>
        /// <param name="sourceInterpreter">
        /// The first (source) interpreter to compare.
        /// </param>
        /// <param name="targetInterpreter">
        /// The second (target) interpreter to compare.
        /// </param>
        /// <returns>
        /// Non-zero if the two interpreters' plugins reside in different
        /// application domains; otherwise, zero.
        /// </returns>
        public static bool IsCrossAppDomain( /* CORE? */
            Interpreter sourceInterpreter, /* in */
            Interpreter targetInterpreter  /* in */
            )
        {
            if ((sourceInterpreter == null) || (targetInterpreter == null))
                return false;

            IPlugin sourcePlugin = null;

            if (GetPlugin(
                    sourceInterpreter, ref sourcePlugin) != ReturnCode.Ok)
            {
                return false;
            }

            IPlugin targetPlugin = null;

            if (GetPlugin(
                    targetInterpreter, ref targetPlugin) != ReturnCode.Ok)
            {
                return false;
            }

            return !Utility.IsSameAppDomain(sourcePlugin, targetPlugin);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Forces the supplied auxiliary data dictionary to be remoted back
        /// to the plugin's application domain, but only when the plugin is
        /// isolated.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose auxiliary data is being updated.
        /// </param>
        /// <param name="auxiliaryData">
        /// The auxiliary data dictionary to assign to the plugin.
        /// </param>
        private static void MaybeUpdateAuxiliaryData( /* CORE? */
            IPluginData pluginData,        /* in */
            ObjectDictionary auxiliaryData /* in */
            )
        {
            if (pluginData == null)
                return;

#if ISOLATED_PLUGINS
            if (!Utility.HasFlags(
                    pluginData.Flags, PluginFlags.Isolated, true))
            {
                return;
            }
#endif

            //
            // HACK: Force the entire auxiliary data dictionary to be
            //       remoted back to the other application domain.
            //
            pluginData.AuxiliaryData = auxiliaryData; /* ISOLATED */
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the <see cref="ExecutionPolicy" /> that should be applied for
        /// commands of the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose command execution policy is requested.
        /// </param>
        /// <returns>
        /// The execution policy for the specified policy type, or
        /// <see cref="ExecutionPolicy.Undefined" /> if it is not recognized.
        /// </returns>
        private static ExecutionPolicy GetPolicyForCommand( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                case PolicyType.File:
                case PolicyType.Stream:
                case PolicyType.License:
                case PolicyType.KeyPair:
                case PolicyType.Trace:
                case PolicyType.Other:
                    {
#if LIMITED_EDITION
                        return GetLimitedPolicy(policyType);
#else
                        return GetSimplePolicy(policyType);
#endif
                    }
                default:
                    {
                        return ExecutionPolicy.Undefined;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the currently configured execution policy for
        /// the specified policy type matches the policy that would be applied
        /// for commands of that type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose policy is examined, or null to consult the
        /// global policy.
        /// </param>
        /// <param name="policyType">
        /// The policy type being checked.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to consult only the plugin data policy rather than the
        /// global policy.
        /// </param>
        /// <param name="exactMatch">
        /// Non-zero to require an exact policy match; otherwise, a flag
        /// subset match is performed.
        /// </param>
        /// <returns>
        /// Non-zero if the configured policy satisfies the command policy;
        /// otherwise, zero.
        /// </returns>
        public static bool HavePolicyForCommand( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            bool localOnly,         /* in */
            bool exactMatch         /* in */
            )
        {
            ExecutionPolicy policy = ExecutionPolicy.Undefined;
            bool havePluginData = (pluginData != null);

            if (localOnly || havePluginData)
            {
                if (!GetPolicy(
                        pluginData, policyType, ref policy))
                {
                    return false;
                }
            }
            else
            {
                policy = GetPolicy(policyType);
            }

            if (exactMatch)
            {
                return policy == GetPolicyForCommand(policyType);
            }
            else
            {
                return Utility.HasFlags(
                    policy, GetPolicyForCommand(policyType), true);
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables or disables the command execution policy for every
        /// supported policy type, optionally targeting plugin data and
        /// optionally ignoring individual failures.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose policies are modified, or null to modify the
        /// global policies.
        /// </param>
        /// <param name="enable">
        /// Non-zero to enable the command policies; zero to disable them.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to modify only the plugin data policies rather than the
        /// global policies.
        /// </param>
        /// <param name="ignoreErrors">
        /// Non-zero to continue past individual failures instead of returning
        /// an error.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to treat a missing policy as an error when disabling.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode EnableForCommand( /* CORE? */
            IPluginData pluginData, /* in */
            bool enable,            /* in */
            bool localOnly,         /* in */
            bool ignoreErrors,      /* in */
            bool errorOnNotFound,   /* in */
            ref Result error        /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
            {
                error = "policy types not available";
                return ReturnCode.Error;
            }

            bool havePluginData = (pluginData != null);

            foreach (PolicyType policyType in policyTypes)
            {
                if (enable)
                {
                    if (localOnly || havePluginData)
                    {
                        if (!SetPolicy(
                                pluginData, policyType,
                                GetPolicyForCommand(policyType)))
                        {
                            if (!ignoreErrors)
                            {
                                error = String.Format(
                                    "could not enable plugin {0} policy",
                                    Utility.FormatWrapOrNull(policyType));

                                return ReturnCode.Error;
                            }
                        }
                    }
                    else
                    {
                        if (!SetPolicy(
                                policyType, GetPolicyForCommand(
                                policyType)))
                        {
                            if (!ignoreErrors)
                            {
                                error = String.Format(
                                    "could not enable {0} policy",
                                    Utility.FormatWrapOrNull(policyType));

                                return ReturnCode.Error;
                            }
                        }
                    }
                }
                else
                {
                    if (localOnly || havePluginData)
                    {
                        if (!UnsetPolicy(
                                pluginData, policyType, errorOnNotFound))
                        {
                            if (!ignoreErrors)
                            {
                                error = String.Format(
                                    "could not disable plugin {0} policy",
                                    Utility.FormatWrapOrNull(policyType));

                                return ReturnCode.Error;
                            }
                        }
                    }
                    else
                    {
                        if (!UnsetPolicy(policyType))
                        {
                            if (!ignoreErrors)
                            {
                                error = String.Format(
                                    "could not disable {0} policy",
                                    Utility.FormatWrapOrNull(policyType));

                                return ReturnCode.Error;
                            }
                        }
                    }
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Gets the plugin data associated with the policy of the specified
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose plugin data is requested.
        /// </param>
        /// <returns>
        /// The plugin data for the specified policy type, or null if there is
        /// none.
        /// </returns>
        private static IPluginData GetPluginData(
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.PluginData;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.PluginData;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.PluginData;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.PluginData;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.PluginData;
                    }
                case PolicyType.Trace:
                    {
                        return null; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return null; /* NOP */
                    }
                default:
                    {
                        return null;
                    }
            }
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Sets the plugin data associated with the policy of the specified
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose plugin data is being set.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data to associate with the policy.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin data was set; otherwise, zero.
        /// </returns>
        private static bool SetPluginData(
            PolicyType policyType, /* in */
            IPluginData pluginData /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.PluginData = pluginData;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.PluginData = pluginData;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.PluginData = pluginData;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.PluginData = pluginData;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.PluginData = pluginData;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        return false; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return false; /* NOP */
                    }
                default:
                    {
                        return false;
                    }
            }
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Clears the plugin data associated with the policy of the specified
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose plugin data is being reset.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin data was reset; otherwise, zero.
        /// </returns>
        private static bool ResetPluginData(
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.PluginData = null;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.PluginData = null;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.PluginData = null;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.PluginData = null;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.PluginData = null;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        return false; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return false; /* NOP */
                    }
                default:
                    {
                        return false;
                    }
            }
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Sets the plugin data for all of the standard policy types to the
        /// specified value.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to associate with each policy.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin data was set for every policy; otherwise,
        /// zero.
        /// </returns>
        public static bool SetPluginDatas(
            IPluginData pluginData /* in */
            )
        {
            bool result = true;

            if (!SetPluginData(PolicyType.Script, pluginData))
                result = false;

            if (!SetPluginData(PolicyType.File, pluginData))
                result = false;

            if (!SetPluginData(PolicyType.Stream, pluginData))
                result = false;

            if (!SetPluginData(PolicyType.License, pluginData))
                result = false;

            if (!SetPluginData(PolicyType.KeyPair, pluginData))
                result = false;

            return result;
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Clears the plugin data for all of the standard policy types.
        /// </summary>
        /// <returns>
        /// Non-zero if the plugin data was reset for every policy; otherwise,
        /// zero.
        /// </returns>
        public static bool ResetPluginDatas()
        {
            bool result = true;

            if (!ResetPluginData(PolicyType.Script))
                result = false;

            if (!ResetPluginData(PolicyType.File))
                result = false;

            if (!ResetPluginData(PolicyType.Stream))
                result = false;

            if (!ResetPluginData(PolicyType.License))
                result = false;

            if (!ResetPluginData(PolicyType.KeyPair))
                result = false;

            return result;
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate associated with the policy of the specified
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose certificate is requested.
        /// </param>
        /// <returns>
        /// The certificate for the specified policy type, or null if there is
        /// none.
        /// </returns>
        public static ICertificate GetCertificate(
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.Certificate;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.Certificate;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.Certificate;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.Certificate;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.Certificate;
                    }
                case PolicyType.Trace:
                    {
                        return null; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return null; /* NOP */
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the certificate associated with the policy of the specified
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose certificate is being set.
        /// </param>
        /// <param name="certificate">
        /// The certificate to associate with the policy.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate was set; otherwise, zero.
        /// </returns>
        public static bool SetCertificate(
            PolicyType policyType,   /* in */
            ICertificate certificate /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.Certificate = certificate;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.Certificate = certificate;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.Certificate = certificate;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.Certificate = certificate;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.Certificate = certificate;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        return false; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return false; /* NOP */
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the certificate associated with the policy of the specified
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose certificate is being reset.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate was reset; otherwise, zero.
        /// </returns>
        private static bool ResetCertificate(
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.Certificate = null;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.Certificate = null;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.Certificate = null;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.Certificate = null;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.Certificate = null;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        return false; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return false; /* NOP */
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Sets the certificate for all of the standard policy types to the
        /// specified value.
        /// </summary>
        /// <param name="certificate">
        /// The certificate to associate with each policy.
        /// </param>
        /// <returns>
        /// Non-zero if the certificate was set for every policy; otherwise,
        /// zero.
        /// </returns>
        public static bool SetCertificates(
            ICertificate certificate /* in */
            )
        {
            bool result = true;

            if (!SetCertificate(PolicyType.Script, certificate))
                result = false;

            if (!SetCertificate(PolicyType.File, certificate))
                result = false;

            if (!SetCertificate(PolicyType.Stream, certificate))
                result = false;

            if (!SetCertificate(PolicyType.License, certificate))
                result = false;

            if (!SetCertificate(PolicyType.KeyPair, certificate))
                result = false;

            return result;
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Clears the certificate for all of the standard policy types.
        /// </summary>
        /// <returns>
        /// Non-zero if the certificate was reset for every policy; otherwise,
        /// zero.
        /// </returns>
        public static bool ResetCertificates()
        {
            bool result = true;

            if (!ResetCertificate(PolicyType.Script))
                result = false;

            if (!ResetCertificate(PolicyType.File))
                result = false;

            if (!ResetCertificate(PolicyType.Stream))
                result = false;

            if (!ResetCertificate(PolicyType.License))
                result = false;

            if (!ResetCertificate(PolicyType.KeyPair))
                result = false;

            return result;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the certificate state via the plugin for every policy type
        /// that has associated plugin data.
        /// </summary>
        public static void UnsetCertificatesViaPlugin()
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
                return;

            foreach (PolicyType policyType in policyTypes)
            {
                IPluginData pluginData = GetPluginData(policyType);

                if (pluginData == null)
                    continue;

                /* NO RESULT */
                CertificateSharedOps.UnsetViaPlugin(pluginData);
            }
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the assembly associated with the policy of the specified
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose assembly is requested.
        /// </param>
        /// <returns>
        /// The assembly for the specified policy type, or null if there is
        /// none.
        /// </returns>
        public static Assembly GetAssembly(
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.Assembly;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.Assembly;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.Assembly;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.Assembly;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.Assembly;
                    }
                case PolicyType.Trace:
                    {
                        return null; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return null; /* NOP */
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the assembly associated with the policy of the specified
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose assembly is being set.
        /// </param>
        /// <param name="assembly">
        /// The assembly to associate with the policy.
        /// </param>
        /// <returns>
        /// Non-zero if the assembly was set; otherwise, zero.
        /// </returns>
        public static bool SetAssembly(
            PolicyType policyType, /* in */
            Assembly assembly      /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.Assembly = assembly;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.Assembly = assembly;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.Assembly = assembly;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.Assembly = assembly;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.Assembly = assembly;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        return false; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return false; /* NOP */
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default <see cref="ExecutionPolicy" /> for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose default execution policy is requested.
        /// </param>
        /// <returns>
        /// The default execution policy for the specified policy type, or
        /// <see cref="ExecutionPolicy.Undefined" /> if it is not recognized.
        /// </returns>
        private static ExecutionPolicy GetDefaultPolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Constants.DefaultScriptExecutionPolicy;
                    }
                case PolicyType.File:
                    {
                        return Constants.DefaultFileExecutionPolicy;
                    }
                case PolicyType.Stream:
                    {
                        return Constants.DefaultStreamExecutionPolicy;
                    }
                case PolicyType.License:
                    {
                        return Constants.DefaultLicenseExecutionPolicy;
                    }
                case PolicyType.KeyPair:
                    {
                        return Constants.DefaultKeyPairExecutionPolicy;
                    }
                case PolicyType.Trace:
                    {
                        return Constants.DefaultTraceExecutionPolicy;
                    }
                case PolicyType.Other:
                    {
                        return Constants.DefaultOtherExecutionPolicy;
                    }
                default:
                    {
                        return ExecutionPolicy.Undefined;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the simple (non-default, non-limited) execution policy for
        /// the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose simple execution policy is requested.
        /// </param>
        /// <returns>
        /// The simple execution policy for the specified policy type, or
        /// <see cref="ExecutionPolicy.Undefined" /> if it is not recognized.
        /// </returns>
        private static ExecutionPolicy GetSimplePolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Constants.SimpleScriptExecutionPolicy;
                    }
                case PolicyType.File:
                    {
                        return Constants.SimpleFileExecutionPolicy;
                    }
                case PolicyType.Stream:
                    {
                        return Constants.SimpleStreamExecutionPolicy;
                    }
                case PolicyType.License:
                    {
                        return Constants.SimpleLicenseExecutionPolicy;
                    }
                case PolicyType.KeyPair:
                    {
                        return Constants.SimpleKeyPairExecutionPolicy;
                    }
                case PolicyType.Trace:
                    {
                        return Constants.SimpleTraceExecutionPolicy;
                    }
                case PolicyType.Other:
                    {
                        return Constants.SimpleOtherExecutionPolicy;
                    }
                default:
                    {
                        return ExecutionPolicy.Undefined;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Dead Code
#if LIMITED_EDITION
#if DEAD_CODE
        /// <summary>
        /// Determines whether the current execution policy for the specified
        /// policy type matches the limited-edition policy for that type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if the current policy matches the limited policy;
        /// otherwise, zero.
        /// </returns>
        private static bool HaveLimitedPolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetPolicy(policyType) == GetLimitedPolicy(policyType);
        }
#endif
#endif
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if LIMITED_EDITION
        /// <summary>
        /// Gets the limited-edition <see cref="ExecutionPolicy" /> for the
        /// specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose limited execution policy is requested.
        /// </param>
        /// <returns>
        /// The limited execution policy for the specified policy type, or
        /// <see cref="ExecutionPolicy.Undefined" /> if it is not recognized.
        /// </returns>
        private static ExecutionPolicy GetLimitedPolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Constants.LimitedScriptExecutionPolicy;
                    }
                case PolicyType.File:
                    {
                        return Constants.LimitedFileExecutionPolicy;
                    }
                case PolicyType.Stream:
                    {
                        return Constants.LimitedStreamExecutionPolicy;
                    }
                case PolicyType.License:
                    {
                        return Constants.LimitedLicenseExecutionPolicy;
                    }
                case PolicyType.KeyPair:
                    {
                        return Constants.LimitedKeyPairExecutionPolicy;
                    }
                case PolicyType.Trace:
                    {
                        return Constants.LimitedTraceExecutionPolicy;
                    }
                case PolicyType.Other:
                    {
                        return Constants.LimitedOtherExecutionPolicy;
                    }
                default:
                    {
                        return ExecutionPolicy.Undefined;
                    }
            }
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the current execution policy for the specified
        /// policy type matches its default policy.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if the current policy matches the default policy;
        /// otherwise, zero.
        /// </returns>
        public static bool HaveDefaultPolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetPolicy(policyType) == GetDefaultPolicy(policyType);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Determines whether the current execution policy for the specified
        /// policy type matches its simple policy.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if the current policy matches the simple policy;
        /// otherwise, zero.
        /// </returns>
        private static bool HaveSimplePolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetPolicy(policyType) == GetSimplePolicy(policyType);
        }
#endif
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether any execution policy is currently configured
        /// for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if a policy is configured for the specified policy type;
        /// otherwise, zero.
        /// </returns>
        public static bool HavePolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetPolicy(policyType) != ExecutionPolicy.None; /* EXEMPT */
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current execution policy for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose current execution policy is requested.
        /// </param>
        /// <returns>
        /// The current execution policy for the specified policy type, or
        /// <see cref="ExecutionPolicy.Undefined" /> if it is not recognized.
        /// </returns>
        public static ExecutionPolicy GetPolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.CurrentPolicy;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.CurrentPolicy;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.CurrentPolicy;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.CurrentPolicy;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.CurrentPolicy;
                    }
                case PolicyType.Trace:
                    {
                        return Policies.Trace.CurrentPolicy;
                    }
                case PolicyType.Other:
                    {
                        return Policies.Other.CurrentPolicy;
                    }
                default:
                    {
                        return ExecutionPolicy.Undefined;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the current execution policy for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose current execution policy is being set.
        /// </param>
        /// <param name="policy">
        /// The execution policy to assign.
        /// </param>
        /// <returns>
        /// Non-zero if the policy was set; otherwise, zero.
        /// </returns>
        public static bool SetPolicy( /* CORE? */
            PolicyType policyType, /* in */
            ExecutionPolicy policy /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.CurrentPolicy = policy;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.CurrentPolicy = policy;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.CurrentPolicy = policy;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.CurrentPolicy = policy;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.CurrentPolicy = policy;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        Policies.Trace.CurrentPolicy = policy;
                        return true;
                    }
                case PolicyType.Other:
                    {
                        Policies.Other.CurrentPolicy = policy;
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the current execution policy for the specified policy type
        /// back to its default value.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose execution policy is being reset.
        /// </param>
        /// <returns>
        /// Non-zero if the policy was reset; otherwise, zero.
        /// </returns>
        public static bool ResetPolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return SetPolicy(policyType, GetDefaultPolicy(policyType));
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets (clears) the current execution policy for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose execution policy is being unset.
        /// </param>
        /// <returns>
        /// Non-zero if the policy was unset; otherwise, zero.
        /// </returns>
        public static bool UnsetPolicy( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return SetPolicy(policyType, ExecutionPolicy.None /* EXEMPT */);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the per-policy-type dictionary stored under the given name in
        /// the supplied auxiliary data, optionally creating it if it is not
        /// already present.
        /// </summary>
        /// <param name="auxiliaryData">
        /// The auxiliary data dictionary that holds the named dictionary.
        /// </param>
        /// <param name="name">
        /// The key under which the per-policy-type dictionary is stored.
        /// </param>
        /// <param name="createIfNeeded">
        /// Non-zero to create (and store) the dictionary if it does not
        /// already exist.
        /// </param>
        /// <param name="dictionary">
        /// Upon return, receives the located or newly created dictionary, or
        /// null if it was not found and was not created.
        /// </param>
        public static void GetOrCreateDictionary<T>( /* CORE? */
            ObjectDictionary auxiliaryData,          /* in */
            string name,                             /* in */
            bool createIfNeeded,                     /* in */
            out Dictionary<PolicyType, T> dictionary /* out */
            )
        {
            object value;

            if (auxiliaryData.TryGetValue(name, out value))
            {
                dictionary = value as Dictionary<PolicyType, T>;

                if (createIfNeeded && (dictionary == null))
                {
                    dictionary = new Dictionary<PolicyType, T>();
                    auxiliaryData[name] = dictionary;
                }
            }
            else if (createIfNeeded)
            {
                dictionary = new Dictionary<PolicyType, T>();
                auxiliaryData[name] = dictionary;
            }
            else
            {
                dictionary = null;
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the execution policy stored in the specified
        /// plugin data for the given policy type matches its default policy.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored policy is examined.
        /// </param>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if the stored policy matches the default policy;
        /// otherwise, zero.
        /// </returns>
        public static bool HaveDefaultPolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            ExecutionPolicy policy = ExecutionPolicy.Undefined;

            if (!GetPolicy(pluginData, policyType, ref policy))
                return false;

            return policy == GetDefaultPolicy(policyType);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Determines whether the execution policy stored in the specified
        /// plugin data for the given policy type matches its simple policy.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored policy is examined.
        /// </param>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if the stored policy matches the simple policy;
        /// otherwise, zero.
        /// </returns>
        private static bool HaveSimplePolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            ExecutionPolicy policy = ExecutionPolicy.Undefined;

            if (!GetPolicy(pluginData, policyType, ref policy))
                return false;

            return policy == GetSimplePolicy(policyType);
        }
#endif
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether an execution policy is stored in the specified
        /// plugin data for the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored policy is examined.
        /// </param>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if a policy is stored for the specified policy type;
        /// otherwise, zero.
        /// </returns>
        public static bool HavePolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            ExecutionPolicy policy = ExecutionPolicy.Undefined;

            return GetPolicy(pluginData, policyType, ref policy);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the execution policy stored in the specified plugin data for
        /// the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored policy is retrieved.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored policy is requested.
        /// </param>
        /// <param name="policy">
        /// Upon success, receives the stored execution policy.
        /// </param>
        /// <returns>
        /// Non-zero if a stored policy was found; otherwise, zero.
        /// </returns>
        public static bool GetPolicy( /* CORE? */
            IPluginData pluginData,    /* in */
            PolicyType policyType,     /* in */
            ref ExecutionPolicy policy /* out */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                "Policy", typeof(PolicyDictionary));

            if (name == null)
                return false;

            PolicyDictionary policies;

            GetOrCreateDictionary<ExecutionPolicy>(
                auxiliaryData, name, false, out policies);

            ExecutionPolicy localPolicy;

            if ((policies != null) && policies.TryGetValue(
                    policyType, out localPolicy))
            {
                policy = localPolicy;
                return true;
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified execution policy in the supplied plugin data
        /// for the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data in which the policy is stored.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored policy is being set.
        /// </param>
        /// <param name="policy">
        /// The execution policy to store.
        /// </param>
        /// <returns>
        /// Non-zero if the policy was stored; otherwise, zero.
        /// </returns>
        public static bool SetPolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            ExecutionPolicy policy  /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                "Policy", typeof(PolicyDictionary));

            if (name == null)
                return false;

            PolicyDictionary policies;

            GetOrCreateDictionary<ExecutionPolicy>(
                auxiliaryData, name, true, out policies);

            policies[policyType] = policy;
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the stored execution policy for the given policy type from
        /// the specified plugin data, treating a missing policy as an error.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data from which the policy is removed.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored policy is being removed.
        /// </param>
        /// <returns>
        /// Non-zero if the policy was removed; otherwise, zero.
        /// </returns>
        public static bool UnsetPolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            return UnsetPolicy(pluginData, policyType, true);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the stored execution policy for the given policy type from
        /// the specified plugin data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data from which the policy is removed.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored policy is being removed.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to treat a missing policy as a failure; zero to treat it
        /// as success.
        /// </param>
        /// <returns>
        /// Non-zero if the policy was removed (or absence was tolerated);
        /// otherwise, zero.
        /// </returns>
        public static bool UnsetPolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            bool errorOnNotFound    /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                "Policy", typeof(PolicyDictionary));

            if (name == null)
                return false;

            PolicyDictionary policies;

            GetOrCreateDictionary<ExecutionPolicy>(
                auxiliaryData, name, true, out policies);

            bool result = policies.Remove(policyType);
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            if (!result && !errorOnNotFound)
                result = true;

            return result;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the effective execution policy for the given policy type,
        /// based on the plugin associated with the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose plugin policy is consulted.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose execution policy is requested.
        /// </param>
        /// <returns>
        /// The effective execution policy for the specified policy type.
        /// </returns>
        public static ExecutionPolicy GetPolicy( /* CORE? */
            Interpreter interpreter, /* in */
            PolicyType policyType    /* in */
            )
        {
            IPlugin plugin = null;

            /* IGNORED */
            GetPlugin(interpreter, ref plugin);

            return GetPolicy(plugin, policyType);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the effective execution policy for the given policy type,
        /// preferring any policy stored in the specified plugin data and
        /// otherwise falling back to the global policy.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored policy is preferred.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose execution policy is requested.
        /// </param>
        /// <returns>
        /// The effective execution policy for the specified policy type.
        /// </returns>
        public static ExecutionPolicy GetPolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            bool local;

            return GetPolicy(pluginData, policyType, out local);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the effective execution policy for the given policy type,
        /// preferring any policy stored in the specified plugin data and
        /// otherwise falling back to the global policy, and reports which
        /// source supplied the result.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored policy is preferred.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose execution policy is requested.
        /// </param>
        /// <param name="local">
        /// Upon return, non-zero if the policy came from the plugin data;
        /// zero if it came from the global policy.
        /// </param>
        /// <returns>
        /// The effective execution policy for the specified policy type.
        /// </returns>
        public static ExecutionPolicy GetPolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            out bool local          /* out */
            )
        {
            ExecutionPolicy policy = ExecutionPolicy.Undefined;

            if (GetPolicy(pluginData, policyType, ref policy))
            {
                local = true;
                return policy;
            }

            local = false;
            return GetPolicy(policyType);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Computes a new execution policy by applying the specified mask to
        /// the effective policy for the given policy type, either setting or
        /// clearing the masked bits.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose effective policy is used as the basis.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose policy is being masked.
        /// </param>
        /// <param name="policyMask">
        /// The execution policy bits to set or clear.
        /// </param>
        /// <param name="enable">
        /// Non-zero to set the masked bits; zero to clear them.
        /// </param>
        /// <returns>
        /// The resulting masked execution policy.
        /// </returns>
        public static ExecutionPolicy MaskPolicy(
            IPluginData pluginData,     /* in */
            PolicyType policyType,      /* in */
            ExecutionPolicy policyMask, /* in */
            bool enable                 /* in */
            )
        {
            ExecutionPolicy policy = GetPolicy(pluginData, policyType);

            if (enable)
                policy |= policyMask;
            else
                policy &= ~policyMask;

            return policy;
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the current execution policies for every supported policy
        /// type into the supplied dictionary.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored policies are saved, or null to save
        /// the global policies.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to save only the plugin data policies rather than the
        /// global policies.
        /// </param>
        /// <param name="policies">
        /// The dictionary that receives the saved policies; it is created if
        /// null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode SavePolicies( /* CORE? */
            IPluginData pluginData,        /* in */
            bool localOnly,                /* in */
            ref PolicyDictionary policies, /* out */
            ref Result error               /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
            {
                error = "policy types not available";
                return ReturnCode.Error;
            }

            if (policies == null)
                policies = new PolicyDictionary();

            bool havePluginData = (pluginData != null);

            foreach (PolicyType policyType in policyTypes)
            {
                ExecutionPolicy policy;

                if (localOnly || havePluginData)
                {
                    policy = ExecutionPolicy.Undefined;

                    if (GetPolicy(pluginData, policyType, ref policy))
                        policies[policyType] = policy;
                    else
                        policies.Remove(policyType);
                }
                else
                {
                    policy = GetPolicy(policyType);

                    if (policy != ExecutionPolicy.Undefined)
                        policies[policyType] = policy;
                    else
                        policies.Remove(policyType);
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the execution policies for every supported policy type
        /// from the supplied dictionary, setting those that are present and
        /// unsetting those that are absent.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose policies are restored, or null to restore
        /// the global policies.
        /// </param>
        /// <param name="policies">
        /// The dictionary of policies to restore.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to restore only the plugin data policies rather than the
        /// global policies.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to treat a missing policy as an error when unsetting.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode RestorePolicies( /* CORE? */
            IPluginData pluginData,    /* in */
            PolicyDictionary policies, /* out */
            bool localOnly,            /* in */
            bool errorOnNotFound,      /* in */
            ref Result error           /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
            {
                error = "policy types not available";
                return ReturnCode.Error;
            }

            if (policies == null)
            {
                error = "invalid policies";
                return ReturnCode.Error;
            }

            bool havePluginData = (pluginData != null);

            foreach (PolicyType policyType in policyTypes)
            {
                ExecutionPolicy policy;

                if (policies.TryGetValue(policyType, out policy))
                {
                    if (localOnly || havePluginData)
                    {
                        if (!SetPolicy(pluginData, policyType, policy))
                        {
                            error = String.Format(
                                "could not restore (via set) plugin {0} policy",
                                Utility.FormatWrapOrNull(policyType));

                            return ReturnCode.Error;
                        }
                    }
                    else
                    {
                        if (!SetPolicy(policyType, policy))
                        {
                            error = String.Format(
                                "could not restore (via set) {0} policy",
                                Utility.FormatWrapOrNull(policyType));

                            return ReturnCode.Error;
                        }
                    }
                }
                else
                {
                    if (localOnly || havePluginData)
                    {
                        if (!UnsetPolicy(pluginData, policyType, errorOnNotFound))
                        {
                            error = String.Format(
                                "could not restore (via unset) plugin {0} policy",
                                Utility.FormatWrapOrNull(policyType));

                            return ReturnCode.Error;
                        }
                    }
                    else
                    {
                        if (!UnsetPolicy(policyType))
                        {
                            error = String.Format(
                                "could not restore (via unset) {0} policy",
                                Utility.FormatWrapOrNull(policyType));

                            return ReturnCode.Error;
                        }
                    }
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /* POLICY IMPLEMENTATION USE ONLY */
        /// <summary>
        /// Verifies that the specified base execution policy contains exactly
        /// one of the recognized base policy values.
        /// </summary>
        /// <param name="policyType">
        /// The policy type associated with the base policy being checked.
        /// </param>
        /// <param name="basePolicy">
        /// The base execution policy to validate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the base policy is valid;
        /// otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode CheckBasePolicy( /* CORE? */
            PolicyType policyType,      /* in */
            ExecutionPolicy basePolicy, /* in */
            ref Result error            /* out */
            )
        {
            if ((basePolicy == ExecutionPolicy.None /* EXEMPT */) ||
                (basePolicy == ExecutionPolicy.AllowNone) ||
                (basePolicy == ExecutionPolicy.AllowSignedOnly) ||
                (basePolicy == ExecutionPolicy.AllowAny))
            {
                return ReturnCode.Ok;
            }

            error = String.Format(
                "base {0} policy {1} must have exactly one of {2}, " +
                "{3}, and {4}",
                Utility.FormatWrapOrNull(policyType),
                Utility.FormatWrapOrNull(basePolicy),
                Utility.FormatWrapOrNull(ExecutionPolicy.AllowNone),
                Utility.FormatWrapOrNull(ExecutionPolicy.AllowSignedOnly),
                Utility.FormatWrapOrNull(ExecutionPolicy.AllowAny));

            return ReturnCode.Error;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves and validates the effective and base execution policies
        /// for the specified policy type.  This overload discards any error
        /// information.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to resolve and validate.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data whose stored policy is preferred.
        /// </param>
        /// <param name="policy">
        /// On input, the candidate policy; on output, the resolved effective
        /// policy.
        /// </param>
        /// <param name="basePolicy">
        /// Upon return, receives the resolved base execution policy.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode CheckPolicy( /* CORE? */
            PolicyType policyType,         /* in */
            Interpreter interpreter,       /* in: NOT USED */
            IPluginData pluginData,        /* in: OPTIONAL */
            ref ExecutionPolicy policy,    /* in, out */
            ref ExecutionPolicy basePolicy /* out */
            )
        {
            Result error = null;

            return CheckPolicy(
                policyType, interpreter, pluginData, ref policy,
                ref basePolicy, ref error);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves and validates the effective and base execution policies
        /// for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to resolve and validate.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data whose stored policy is preferred.
        /// </param>
        /// <param name="policy">
        /// On input, the candidate policy; on output, the resolved effective
        /// policy.
        /// </param>
        /// <param name="basePolicy">
        /// Upon return, receives the resolved base execution policy.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode CheckPolicy( /* CORE? */
            PolicyType policyType,           /* in */
            Interpreter interpreter,         /* in: NOT USED */
            IPluginData pluginData,          /* in: OPTIONAL */
            ref ExecutionPolicy policy,      /* in, out */
            ref ExecutionPolicy basePolicy,  /* out */
            ref Result error                 /* out */
            )
        {
            ExecutionPolicy localPolicy = ExecutionPolicy.Undefined;

            return CheckPolicy(
                policyType, interpreter, pluginData, ref policy,
                ref localPolicy, ref basePolicy, ref error);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /* POLICY IMPLEMENTATION USE ONLY */
        /// <summary>
        /// Resolves and validates the effective, local, and base execution
        /// policies for the specified policy type, combining any "Other"
        /// policy flags as needed.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to resolve and validate.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data whose stored policy is preferred.
        /// </param>
        /// <param name="policy">
        /// On input, the candidate policy; on output, the resolved effective
        /// policy.
        /// </param>
        /// <param name="localPolicy">
        /// Upon return, receives the policy that was stored locally in the
        /// plugin data, if any.
        /// </param>
        /// <param name="basePolicy">
        /// Upon return, receives the resolved base execution policy.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode CheckPolicy( /* CORE? */
            PolicyType policyType,           /* in */
            Interpreter interpreter,         /* in: NOT USED */
            IPluginData pluginData,          /* in: OPTIONAL */
            ref ExecutionPolicy policy,      /* in, out */
            ref ExecutionPolicy localPolicy, /* out */
            ref ExecutionPolicy basePolicy,  /* out */
            ref Result error                 /* out */
            )
        {
            if (pluginData != null)
            {
                ExecutionPolicy localPluginPolicy = ExecutionPolicy.Undefined;

                if (GetPolicy(
                        pluginData, policyType, ref localPluginPolicy))
                {
                    ExecutionPolicy localBasePolicy1 =
                        localPluginPolicy & ExecutionPolicy.BasePolicyMask;

                    if (CheckBasePolicy(
                            policyType, localBasePolicy1,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    ExecutionPolicy localOtherPolicy = ExecutionPolicy.Undefined;

                    //
                    // BUGFIX: Make sure the local "Other" policy flags are
                    //         combined with the local "File" and "Script"
                    //         policies.  For the global policy flags, this
                    //         is not necessary because the "Other" policy
                    //         flags are already combined by the associated
                    //         "Licensing.Policies" static classes.
                    //
                    if (policyType != PolicyType.Other)
                    {
                        /* IGNORED */
                        GetPolicy(pluginData,
                            PolicyType.Other, ref localOtherPolicy);
                    }

                    policy = localPluginPolicy | localOtherPolicy;
                    localPolicy = localPluginPolicy;
                    basePolicy = localBasePolicy1;

                    return ReturnCode.Ok;
                }
            }

            ExecutionPolicy localBasePolicy2 =
                policy & ExecutionPolicy.BasePolicyMask;

            if (CheckBasePolicy(
                    policyType, localBasePolicy2,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            basePolicy = localBasePolicy2;
            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the base execution policy for any supported
        /// policy type is configured to allow signed content only.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data whose stored policies are preferred.
        /// </param>
        /// <returns>
        /// Non-zero if any policy type allows signed content only; otherwise,
        /// zero.
        /// </returns>
        public static bool IsAnyBasePolicyAllowSignedOnly( /* CORE? */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData   /* in: OPTIONAL */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
                return false;

            foreach (PolicyType policyType in policyTypes)
            {
                Result error = null; /* NOT USED */

                if (IsBasePolicyAllowSignedOnly(
                        policyType, interpreter, pluginData,
                        ref error))
                {
                    return true;
                }
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the base execution policy for the specified
        /// policy type is configured to allow signed content only.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data whose stored policy is preferred.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// Non-zero if the policy allows signed content only; otherwise,
        /// zero.
        /// </returns>
        public static bool IsBasePolicyAllowSignedOnly( /* CORE? */
            PolicyType policyType,   /* in */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            ExecutionPolicy policy = GetPolicy(policyType);
            ExecutionPolicy basePolicy = ExecutionPolicy.Undefined;

            if (CheckPolicy(
                    policyType, interpreter, pluginData, ref policy,
                    ref basePolicy, ref error) != ReturnCode.Ok)
            {
                return false;
            }

            if (!Utility.HasFlags(
                    basePolicy, ExecutionPolicy.AllowSignedOnly, false))
            {
                error = String.Format(
                    "base {0} policy {1} does not include {2}",
                    Utility.FormatWrapOrNull(policyType),
                    Utility.FormatWrapOrNull(basePolicy),
                    Utility.FormatWrapOrNull(
                        ExecutionPolicy.AllowSignedOnly));

                return false;
            }

            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables or disables policy tracing by adjusting the trace and the
        /// other execution policies using the default policy tracing mask.
        /// </summary>
        /// <param name="enable">
        /// Non-zero to enable policy tracing; zero to disable it.
        /// </param>
        public static void EnablePolicyTracing( /* CORE? */
            bool enable /* in */
            )
        {
            ExecutionPolicy tracePolicy = GetPolicy(PolicyType.Trace);
            ExecutionPolicy otherPolicy = GetPolicy(PolicyType.Other);

            if (enable)
            {
                tracePolicy |= Constants.EnablePolicyTracingDefaultMask;
                otherPolicy |= Constants.EnablePolicyTracingDefaultMask;
            }
            else
            {
                tracePolicy &= ~Constants.EnablePolicyTracingDefaultMask;
                otherPolicy &= ~Constants.EnablePolicyTracingDefaultMask;
            }

            SetPolicy(PolicyType.Trace, tracePolicy);
            SetPolicy(PolicyType.Other, otherPolicy);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables or disables full plugin policy tracing by adjusting the
        /// trace and other execution policies using the full plugin policy
        /// tracing mask.
        /// </summary>
        /// <param name="enable">
        /// Non-zero to enable full plugin policy tracing; zero to disable it.
        /// </param>
        public static void EnableFullPluginPolicyTracing( /* CORE? */
            bool enable /* in */
            )
        {
            ExecutionPolicy tracePolicy = GetPolicy(PolicyType.Trace);
            ExecutionPolicy otherPolicy = GetPolicy(PolicyType.Other);

            if (enable)
            {
                tracePolicy |= Constants.EnableFullPluginPolicyTracingMask;
                otherPolicy |= Constants.EnableFullPluginPolicyTracingMask;
            }
            else
            {
                tracePolicy &= ~Constants.EnableFullPluginPolicyTracingMask;
                otherPolicy &= ~Constants.EnableFullPluginPolicyTracingMask;
            }

            SetPolicy(PolicyType.Trace, tracePolicy);
            SetPolicy(PolicyType.Other, otherPolicy);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a human-readable status string describing the effective
        /// policy scope and security state for the specified policy type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose security state is reported.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose policy and type name are reported.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose status is reported.
        /// </param>
        /// <returns>
        /// A formatted status string describing the policy and security
        /// state.
        /// </returns>
        public static string GetStatus( /* CORE? */
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            PolicyType policyType    /* in */
            )
        {
            string typeName = null;

            if (pluginData != null)
                typeName = pluginData.TypeName;

            ExecutionPolicy policy;
            bool local;

            policy = GetPolicy(pluginData, policyType, out local);

            ExecutionPolicy basePolicy = ExecutionPolicy.Undefined;

            /* IGNORED */
            CheckPolicy(
                policyType, interpreter, pluginData, ref policy,
                ref basePolicy);

            string scopeName;
            bool allowSignedOnly;

            if (basePolicy == ExecutionPolicy.AllowSignedOnly)
            {
                scopeName = local ? "Plugin " : "Global ";
                allowSignedOnly = true;
            }
            else
            {
                scopeName = null;
                allowSignedOnly = false;
            }

            bool securityWasEnabled;

            if (interpreter.SetSecurityWasEnabled(null))
                securityWasEnabled = true;
            else
                securityWasEnabled = false;

            return String.Format("{0} {1}", String.Format("{0} {1}{2}",
                (typeName != null) ? typeName : "<Unknown>", scopeName,
                allowSignedOnly ? "AllowSignedOnly" : "DISABLED"),
                String.Format("Interpreter {0}", securityWasEnabled ?
                "SecurityWasEnabled" : "INSECURE"));
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a key name is configured for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if a key name is configured; otherwise, zero.
        /// </returns>
        public static bool HaveKeyName( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetKeyName(policyType) != null;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key name configured for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose key name is requested.
        /// </param>
        /// <returns>
        /// The key name for the specified policy type, or null if there is
        /// none.
        /// </returns>
        public static string GetKeyName( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.KeyName;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.KeyName;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.KeyName;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.KeyName;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.KeyName;
                    }
                case PolicyType.Trace:
                    {
                        return null; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return null; /* NOP */
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the key name configured for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose key name is being set.
        /// </param>
        /// <param name="keyName">
        /// The key name to associate with the policy.
        /// </param>
        /// <returns>
        /// Non-zero if the key name was set; otherwise, zero.
        /// </returns>
        public static bool SetKeyName(
            PolicyType policyType, /* in */
            string keyName         /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.KeyName = keyName;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.KeyName = keyName;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.KeyName = keyName;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.KeyName = keyName;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.KeyName = keyName;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        return false; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return false; /* NOP */
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets (clears) the key name configured for the specified policy
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose key name is being unset.
        /// </param>
        /// <returns>
        /// Non-zero if the key name was unset; otherwise, zero.
        /// </returns>
        public static bool UnsetKeyName(
            PolicyType policyType /* in */
            )
        {
            return SetKeyName(policyType, null);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a key name is stored in the specified plugin
        /// data for the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored key name is examined.
        /// </param>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if a key name is stored for the specified policy type;
        /// otherwise, zero.
        /// </returns>
        public static bool HaveKeyName( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            string keyName = null;

            return GetKeyName(
                pluginData, policyType, ref keyName);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key name stored in the specified plugin data for the
        /// given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored key name is retrieved.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored key name is requested.
        /// </param>
        /// <param name="keyName">
        /// Upon success, receives the stored key name.
        /// </param>
        /// <returns>
        /// Non-zero if a stored key name was found; otherwise, zero.
        /// </returns>
        public static bool GetKeyName( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            ref string keyName      /* out */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.KeyNamePropertyName, typeof(KeyNameDictionary));

            if (name == null)
                return false;

            KeyNameDictionary keyNames;

            GetOrCreateDictionary<string>(
                auxiliaryData, name, false, out keyNames);

            string localKeyName;

            if ((keyNames != null) && keyNames.TryGetValue(
                    policyType, out localKeyName))
            {
                keyName = localKeyName;
                return true;
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified key name in the supplied plugin data for the
        /// given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data in which the key name is stored.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored key name is being set.
        /// </param>
        /// <param name="keyName">
        /// The key name to store.
        /// </param>
        /// <returns>
        /// Non-zero if the key name was stored; otherwise, zero.
        /// </returns>
        public static bool SetKeyName( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            string keyName          /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.KeyNamePropertyName, typeof(KeyNameDictionary));

            if (name == null)
                return false;

            KeyNameDictionary keyNames;

            GetOrCreateDictionary<string>(
                auxiliaryData, name, true, out keyNames);

            keyNames[policyType] = keyName;
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the stored key name for the given policy type from the
        /// specified plugin data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data from which the key name is removed.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored key name is being removed.
        /// </param>
        /// <returns>
        /// Non-zero if the key name was removed; otherwise, zero.
        /// </returns>
        public static bool UnsetKeyName(
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.KeyNamePropertyName, typeof(KeyNameDictionary));

            if (name == null)
                return false;

            KeyNameDictionary keyNames;

            GetOrCreateDictionary<string>(
                auxiliaryData, name, true, out keyNames);

            bool result = keyNames.Remove(policyType);
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return result;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the effective key name for the specified policy type,
        /// preferring any key name stored in the supplied plugin data.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose key name is resolved.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose stored key name is preferred.
        /// </param>
        /// <param name="keyName">
        /// On input, the candidate key name; on output, the resolved key
        /// name.
        /// </param>
        /// <param name="result">
        /// Reserved for error information; not currently used.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success.
        /// </returns>
        private static ReturnCode CheckKeyName(
            PolicyType policyType,   /* in */
            Interpreter interpreter, /* in: NOT USED */
            IPluginData pluginData,  /* in */
            ref string keyName,      /* in, out */
            ref Result result        /* out: NOT USED */
            )
        {
            if (pluginData != null)
            {
                string localKeyName = null;

                if (GetKeyName(
                        pluginData, policyType, ref localKeyName))
                {
                    keyName = localKeyName;
                    return ReturnCode.Ok;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a key ring name is configured for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if a key ring name is configured; otherwise, zero.
        /// </returns>
        public static bool HaveKeyRingName( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetKeyRingName(policyType) != null;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key ring name configured for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose key ring name is requested.
        /// </param>
        /// <returns>
        /// The key ring name for the specified policy type, or null if there
        /// is none.
        /// </returns>
        public static string GetKeyRingName( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.KeyRingName;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.KeyRingName;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.KeyRingName;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.KeyRingName;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.KeyRingName;
                    }
                case PolicyType.Trace:
                    {
                        return null; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return null; /* NOP */
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the key ring name configured for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose key ring name is being set.
        /// </param>
        /// <param name="keyRingName">
        /// The key ring name to associate with the policy.
        /// </param>
        /// <returns>
        /// Non-zero if the key ring name was set; otherwise, zero.
        /// </returns>
        public static bool SetKeyRingName(
            PolicyType policyType, /* in */
            string keyRingName     /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.KeyRingName = keyRingName;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.KeyRingName = keyRingName;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.KeyRingName = keyRingName;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.KeyRingName = keyRingName;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.KeyRingName = keyRingName;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        return false; /* NOP */
                    }
                case PolicyType.Other:
                    {
                        return false; /* NOP */
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets (clears) the key ring name configured for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose key ring name is being unset.
        /// </param>
        /// <returns>
        /// Non-zero if the key ring name was unset; otherwise, zero.
        /// </returns>
        public static bool UnsetKeyRingName(
            PolicyType policyType /* in */
            )
        {
            return SetKeyRingName(policyType, null);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a key ring name is stored in the specified
        /// plugin data for the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored key ring name is examined.
        /// </param>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if a key ring name is stored for the specified policy
        /// type; otherwise, zero.
        /// </returns>
        public static bool HaveKeyRingName( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            string keyRingName = null;

            return GetKeyRingName(
                pluginData, policyType, ref keyRingName);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key ring name stored in the specified plugin data for the
        /// given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored key ring name is retrieved.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored key ring name is requested.
        /// </param>
        /// <param name="keyRingName">
        /// Upon success, receives the stored key ring name.
        /// </param>
        /// <returns>
        /// Non-zero if a stored key ring name was found; otherwise, zero.
        /// </returns>
        public static bool GetKeyRingName( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            ref string keyRingName  /* out */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.KeyRingNamePropertyName, typeof(KeyRingNameDictionary));

            if (name == null)
                return false;

            KeyRingNameDictionary keyRingNames;

            GetOrCreateDictionary<string>(
                auxiliaryData, name, false, out keyRingNames);

            string localKeyRingName;

            if ((keyRingNames != null) && keyRingNames.TryGetValue(
                    policyType, out localKeyRingName))
            {
                keyRingName = localKeyRingName;
                return true;
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified key ring name in the supplied plugin data for
        /// the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data in which the key ring name is stored.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored key ring name is being set.
        /// </param>
        /// <param name="keyRingName">
        /// The key ring name to store.
        /// </param>
        /// <returns>
        /// Non-zero if the key ring name was stored; otherwise, zero.
        /// </returns>
        public static bool SetKeyRingName( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            string keyRingName      /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.KeyRingNamePropertyName, typeof(KeyRingNameDictionary));

            if (name == null)
                return false;

            KeyRingNameDictionary keyRingNames;

            GetOrCreateDictionary<string>(
                auxiliaryData, name, true, out keyRingNames);

            keyRingNames[policyType] = keyRingName;
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the stored key ring name for the given policy type from
        /// the specified plugin data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data from which the key ring name is removed.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored key ring name is being removed.
        /// </param>
        /// <returns>
        /// Non-zero if the key ring name was removed; otherwise, zero.
        /// </returns>
        public static bool UnsetKeyRingName(
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.KeyRingNamePropertyName, typeof(KeyRingNameDictionary));

            if (name == null)
                return false;

            KeyRingNameDictionary keyRingNames;

            GetOrCreateDictionary<string>(
                auxiliaryData, name, true, out keyRingNames);

            bool result = keyRingNames.Remove(policyType);
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return result;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the effective key ring name for the given policy type,
        /// preferring any key ring name stored in the supplied plugin data.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose key ring name is resolved.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose stored key ring name is preferred.
        /// </param>
        /// <param name="keyRingName">
        /// On input, the candidate key ring name; on output, the resolved
        /// key ring name.
        /// </param>
        /// <param name="result">
        /// Reserved for error information; not currently used.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success.
        /// </returns>
        private static ReturnCode CheckKeyRingName(
            PolicyType policyType,   /* in */
            Interpreter interpreter, /* in: NOT USED */
            IPluginData pluginData,  /* in */
            ref string keyRingName,  /* in, out */
            ref Result result        /* out: NOT USED */
            )
        {
            if (pluginData != null)
            {
                string localKeyRingName = null;

                if (GetKeyRingName(
                        pluginData, policyType, ref localKeyRingName))
                {
                    keyRingName = localKeyRingName;
                    return ReturnCode.Ok;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default <see cref="ScriptFlags" /> for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose default script flags are requested.
        /// </param>
        /// <returns>
        /// The default script flags for the specified policy type, or
        /// <see cref="ScriptFlags.None" /> if it is not recognized.
        /// </returns>
        private static ScriptFlags GetDefaultScriptFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Constants.DefaultScriptScriptFlags;
                    }
                case PolicyType.File:
                    {
                        return Constants.DefaultFileScriptFlags;
                    }
                case PolicyType.Stream:
                    {
                        return Constants.DefaultStreamScriptFlags;
                    }
                case PolicyType.License:
                    {
                        return Constants.DefaultLicenseScriptFlags;
                    }
                case PolicyType.KeyPair:
                    {
                        return Constants.DefaultKeyPairScriptFlags;
                    }
                case PolicyType.Trace:
                    {
                        return Constants.DefaultTraceScriptFlags;
                    }
                case PolicyType.Other:
                    {
                        return Constants.DefaultOtherScriptFlags;
                    }
                default:
                    {
                        return ScriptFlags.None;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Determines whether the current script flags for the specified
        /// policy type match its default script flags.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if the current script flags match the default flags;
        /// otherwise, zero.
        /// </returns>
        private static bool HaveDefaultScriptFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetScriptFlags(policyType) == GetDefaultScriptFlags(policyType);
        }
#endif
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether any script flags are configured for the
        /// specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if script flags are configured; otherwise, zero.
        /// </returns>
        public static bool HaveScriptFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetScriptFlags(policyType) != ScriptFlags.None;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current script flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose current script flags are requested.
        /// </param>
        /// <returns>
        /// The current script flags for the specified policy type, or
        /// <see cref="ScriptFlags.None" /> if it is not recognized.
        /// </returns>
        public static ScriptFlags GetScriptFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.ScriptFlags;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.ScriptFlags;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.ScriptFlags;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.ScriptFlags;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.ScriptFlags;
                    }
                case PolicyType.Trace:
                    {
                        return Policies.Trace.ScriptFlags;
                    }
                case PolicyType.Other:
                    {
                        return Policies.Other.ScriptFlags;
                    }
                default:
                    {
                        return ScriptFlags.None;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the current script flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose script flags are being set.
        /// </param>
        /// <param name="scriptFlags">
        /// The script flags to assign.
        /// </param>
        /// <returns>
        /// Non-zero if the script flags were set; otherwise, zero.
        /// </returns>
        public static bool SetScriptFlags(
            PolicyType policyType,  /* in */
            ScriptFlags scriptFlags /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.ScriptFlags = scriptFlags;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.ScriptFlags = scriptFlags;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.ScriptFlags = scriptFlags;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.ScriptFlags = scriptFlags;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.ScriptFlags = scriptFlags;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        Policies.Trace.ScriptFlags = scriptFlags;
                        return true;
                    }
                case PolicyType.Other:
                    {
                        Policies.Other.ScriptFlags = scriptFlags;
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the script flags for the given policy type back to their
        /// default value as defined in the constants class.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose script flags are being reset.
        /// </param>
        /// <returns>
        /// Non-zero if the script flags were reset; otherwise, zero.
        /// </returns>
        public static bool ResetScriptFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            //
            // HACK: Must fallback to the default value here, which is defined
            //       in the Constants class, not simply the value of "None".
            //
            return SetScriptFlags(policyType, GetDefaultScriptFlags(policyType));
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets (clears) the script flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose script flags are being unset.
        /// </param>
        /// <returns>
        /// Non-zero if the script flags were unset; otherwise, zero.
        /// </returns>
        public static bool UnsetScriptFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return SetScriptFlags(policyType, ScriptFlags.None);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether script flags are stored in the specified plugin
        /// data for the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored script flags are examined.
        /// </param>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if script flags are stored for the specified policy type;
        /// otherwise, zero.
        /// </returns>
        public static bool HaveScriptFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            ScriptFlags scriptFlags = ScriptFlags.None;

            return GetScriptFlags(
                pluginData, policyType, ref scriptFlags);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the script flags stored in the specified plugin data for the
        /// given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored script flags are retrieved.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored script flags are requested.
        /// </param>
        /// <param name="scriptFlags">
        /// Upon success, receives the stored script flags.
        /// </param>
        /// <returns>
        /// Non-zero if stored script flags were found; otherwise, zero.
        /// </returns>
        public static bool GetScriptFlags( /* CORE? */
            IPluginData pluginData,     /* in */
            PolicyType policyType,      /* in */
            ref ScriptFlags scriptFlags /* out */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.ScriptFlagsPropertyName, typeof(ScriptFlagsDictionary));

            if (name == null)
                return false;

            ScriptFlagsDictionary flags;

            GetOrCreateDictionary<ScriptFlags>(
                auxiliaryData, name, false, out flags);

            ScriptFlags localScriptFlags;

            if ((flags != null) && flags.TryGetValue(
                    policyType, out localScriptFlags))
            {
                scriptFlags = localScriptFlags;
                return true;
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified script flags in the supplied plugin data for
        /// the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data in which the script flags are stored.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored script flags are being set.
        /// </param>
        /// <param name="scriptFlags">
        /// The script flags to store.
        /// </param>
        /// <returns>
        /// Non-zero if the script flags were stored; otherwise, zero.
        /// </returns>
        public static bool SetScriptFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            ScriptFlags scriptFlags /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.ScriptFlagsPropertyName, typeof(ScriptFlagsDictionary));

            if (name == null)
                return false;

            ScriptFlagsDictionary flags;

            GetOrCreateDictionary<ScriptFlags>(
                auxiliaryData, name, true, out flags);

            flags[policyType] = scriptFlags;
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the stored script flags for the given policy type from the
        /// specified plugin data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data from which the script flags are removed.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored script flags are being removed.
        /// </param>
        /// <returns>
        /// Non-zero if the script flags were removed; otherwise, zero.
        /// </returns>
        public static bool UnsetScriptFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.ScriptFlagsPropertyName, typeof(ScriptFlagsDictionary));

            if (name == null)
                return false;

            ScriptFlagsDictionary flags;

            GetOrCreateDictionary<ScriptFlags>(
                auxiliaryData, name, true, out flags);

            bool result = flags.Remove(policyType);
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return result;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /* POLICY IMPLEMENTATION USE ONLY */
        /// <summary>
        /// Resolves the effective script flags for the specified policy type,
        /// preferring any script flags stored in the supplied plugin data and
        /// combining them with the local "Other" script flags as needed.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose script flags are resolved.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose stored script flags are preferred.
        /// </param>
        /// <param name="scriptFlags">
        /// On input, the candidate script flags; on output, the resolved
        /// script flags.
        /// </param>
        /// <param name="error">
        /// Reserved for error information; not currently used.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success.
        /// </returns>
        private static ReturnCode CheckScriptFlags( /* CORE? */
            PolicyType policyType,       /* in */
            Interpreter interpreter,     /* in: NOT USED */
            IPluginData pluginData,      /* in */
            ref ScriptFlags scriptFlags, /* in, out */
            ref Result error             /* out: NOT USED */
            )
        {
            if (pluginData != null)
            {
                ScriptFlags localScriptFlags = ScriptFlags.Default;

                if (GetScriptFlags(
                        pluginData, policyType, ref localScriptFlags))
                {
                    ScriptFlags localOtherScriptFlags = ScriptFlags.None;

                    //
                    // BUGFIX: Make sure the local "Other" script flags are
                    //         combined with the local "File" and "Script"
                    //         policies.  For the global script flags, this
                    //         is not necessary because the "Other" script
                    //         flags are already combined by the associated
                    //         "Licensing.Policies" static classes.
                    //
                    if (policyType != PolicyType.Other)
                    {
                        /* IGNORED */
                        GetScriptFlags(pluginData,
                            PolicyType.Other, ref localOtherScriptFlags);
                    }

                    scriptFlags = localScriptFlags | localOtherScriptFlags;
                    return ReturnCode.Ok;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default <see cref="PathFlags" /> for the specified policy
        /// type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose default path flags are requested.
        /// </param>
        /// <returns>
        /// The default path flags for the specified policy type, or
        /// <see cref="PathFlags.None" /> if it is not recognized.
        /// </returns>
        private static PathFlags GetDefaultPathFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Constants.MachinePathFlags;
                    }
                case PolicyType.File:
                    {
                        return Constants.MachinePathFlags;
                    }
                case PolicyType.Stream:
                    {
                        return Constants.MachinePathFlags;
                    }
                case PolicyType.License:
                    {
                        return Constants.VerifyPathFlags;
                    }
                case PolicyType.KeyPair:
                    {
                        return Constants.MachinePathFlags;
                    }
                case PolicyType.Trace:
                    {
                        return Constants.MachinePathFlags;
                    }
                case PolicyType.Other:
                    {
                        return Constants.MachinePathFlags;
                    }
                default:
                    {
                        return PathFlags.None;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Determines whether the current path flags for the specified policy
        /// type match its default path flags.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if the current path flags match the default flags;
        /// otherwise, zero.
        /// </returns>
        private static bool HaveDefaultPathFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetPathFlags(policyType) == GetDefaultPathFlags(policyType);
        }
#endif
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether any path flags are configured for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if path flags are configured; otherwise, zero.
        /// </returns>
        public static bool HavePathFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetPathFlags(policyType) != PathFlags.None;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current path flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose current path flags are requested.
        /// </param>
        /// <returns>
        /// The current path flags for the specified policy type, or
        /// <see cref="PathFlags.None" /> if it is not recognized.
        /// </returns>
        public static PathFlags GetPathFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.PathFlags;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.PathFlags;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.PathFlags;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.PathFlags;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.PathFlags;
                    }
                case PolicyType.Trace:
                    {
                        return Policies.Trace.PathFlags;
                    }
                case PolicyType.Other:
                    {
                        return Policies.Other.PathFlags;
                    }
                default:
                    {
                        return PathFlags.None;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the current path flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose path flags are being set.
        /// </param>
        /// <param name="pathFlags">
        /// The path flags to assign.
        /// </param>
        /// <returns>
        /// Non-zero if the path flags were set; otherwise, zero.
        /// </returns>
        public static bool SetPathFlags(
            PolicyType policyType, /* in */
            PathFlags pathFlags    /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.PathFlags = pathFlags;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.PathFlags = pathFlags;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.PathFlags = pathFlags;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.PathFlags = pathFlags;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.PathFlags = pathFlags;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        Policies.Trace.PathFlags = pathFlags;
                        return true;
                    }
                case PolicyType.Other:
                    {
                        Policies.Other.PathFlags = pathFlags;
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the path flags for the specified policy type back to their
        /// default value as defined in the constants class.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose path flags are being reset.
        /// </param>
        /// <returns>
        /// Non-zero if the path flags were reset; otherwise, zero.
        /// </returns>
        public static bool ResetPathFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            //
            // HACK: Must fallback to the default value here, which is defined
            //       in the Constants class, not simply the value of "None".
            //
            return SetPathFlags(policyType, GetDefaultPathFlags(policyType));
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets (clears) the path flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose path flags are being unset.
        /// </param>
        /// <returns>
        /// Non-zero if the path flags were unset; otherwise, zero.
        /// </returns>
        public static bool UnsetPathFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return SetPathFlags(policyType, PathFlags.None);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether path flags are stored in the specified plugin
        /// data for the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored path flags are examined.
        /// </param>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if path flags are stored for the specified policy type;
        /// otherwise, zero.
        /// </returns>
        public static bool HavePathFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            PathFlags PathFlags = PathFlags.None;

            return GetPathFlags(
                pluginData, policyType, ref PathFlags);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the path flags stored in the specified plugin data for the
        /// given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored path flags are retrieved.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored path flags are requested.
        /// </param>
        /// <param name="PathFlags">
        /// Upon success, receives the stored path flags.
        /// </param>
        /// <returns>
        /// Non-zero if stored path flags were found; otherwise, zero.
        /// </returns>
        public static bool GetPathFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            ref PathFlags PathFlags /* out */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.PathFlagsPropertyName, typeof(PathFlagsDictionary));

            if (name == null)
                return false;

            PathFlagsDictionary flags;

            GetOrCreateDictionary<PathFlags>(
                auxiliaryData, name, false, out flags);

            PathFlags localPathFlags;

            if ((flags != null) && flags.TryGetValue(
                    policyType, out localPathFlags))
            {
                PathFlags = localPathFlags;
                return true;
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified path flags in the supplied plugin data for
        /// the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data in which the path flags are stored.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored path flags are being set.
        /// </param>
        /// <param name="PathFlags">
        /// The path flags to store.
        /// </param>
        /// <returns>
        /// Non-zero if the path flags were stored; otherwise, zero.
        /// </returns>
        public static bool SetPathFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            PathFlags PathFlags     /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.PathFlagsPropertyName, typeof(PathFlagsDictionary));

            if (name == null)
                return false;

            PathFlagsDictionary flags;

            GetOrCreateDictionary<PathFlags>(
                auxiliaryData, name, true, out flags);

            flags[policyType] = PathFlags;
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the stored path flags for the given policy type from the
        /// specified plugin data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data from which the path flags are removed.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored path flags are being removed.
        /// </param>
        /// <returns>
        /// Non-zero if the path flags were removed; otherwise, zero.
        /// </returns>
        public static bool UnsetPathFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.PathFlagsPropertyName, typeof(PathFlagsDictionary));

            if (name == null)
                return false;

            PathFlagsDictionary flags;

            GetOrCreateDictionary<PathFlags>(
                auxiliaryData, name, true, out flags);

            bool result = flags.Remove(policyType);
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return result;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default <see cref="NetworkFlags" /> for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose default network flags are requested.
        /// </param>
        /// <returns>
        /// The default network flags for the specified policy type, or
        /// <see cref="NetworkFlags.None" /> if it is not recognized.
        /// </returns>
        private static NetworkFlags GetDefaultNetworkFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Constants.ScriptNetworkFlags;
                    }
                case PolicyType.File:
                    {
                        return Constants.ScriptNetworkFlags;
                    }
                case PolicyType.Stream:
                    {
                        return Constants.ScriptNetworkFlags;
                    }
                case PolicyType.License:
                    {
                        return Constants.LicenseNetworkFlags;
                    }
                case PolicyType.KeyPair:
                    {
                        return Constants.ScriptNetworkFlags;
                    }
                case PolicyType.Trace:
                    {
                        return Constants.ScriptNetworkFlags;
                    }
                case PolicyType.Other:
                    {
                        return Constants.ScriptNetworkFlags;
                    }
                default:
                    {
                        return NetworkFlags.None;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Determines whether the current network flags for the specified
        /// policy type match its default network flags.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if the current network flags match the default flags;
        /// otherwise, zero.
        /// </returns>
        private static bool HaveDefaultNetworkFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetNetworkFlags(policyType) == GetDefaultNetworkFlags(policyType);
        }
#endif
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether any network flags are configured for the
        /// specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if network flags are configured; otherwise, zero.
        /// </returns>
        public static bool HaveNetworkFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return GetNetworkFlags(policyType) != NetworkFlags.None;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current network flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose current network flags are requested.
        /// </param>
        /// <returns>
        /// The current network flags for the specified policy type, or
        /// <see cref="NetworkFlags.None" /> if it is not recognized.
        /// </returns>
        public static NetworkFlags GetNetworkFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.NetworkFlags;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.NetworkFlags;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.NetworkFlags;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.NetworkFlags;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.NetworkFlags;
                    }
                case PolicyType.Trace:
                    {
                        return Policies.Trace.NetworkFlags;
                    }
                case PolicyType.Other:
                    {
                        return Policies.Other.NetworkFlags;
                    }
                default:
                    {
                        return NetworkFlags.None;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the current network flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose network flags are being set.
        /// </param>
        /// <param name="networkFlags">
        /// The network flags to assign.
        /// </param>
        /// <returns>
        /// Non-zero if the network flags were set; otherwise, zero.
        /// </returns>
        public static bool SetNetworkFlags(
            PolicyType policyType,    /* in */
            NetworkFlags networkFlags /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.NetworkFlags = networkFlags;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.NetworkFlags = networkFlags;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.NetworkFlags = networkFlags;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.NetworkFlags = networkFlags;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.NetworkFlags = networkFlags;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        Policies.Trace.NetworkFlags = networkFlags;
                        return true;
                    }
                case PolicyType.Other:
                    {
                        Policies.Other.NetworkFlags = networkFlags;
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the network flags for the specified policy type back to
        /// their default value as defined in the constants class.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose network flags are being reset.
        /// </param>
        /// <returns>
        /// Non-zero if the network flags were reset; otherwise, zero.
        /// </returns>
        public static bool ResetNetworkFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            //
            // HACK: Must fallback to the default value here, which is defined
            //       in the Constants class, not simply the value of "None".
            //
            return SetNetworkFlags(policyType, GetDefaultNetworkFlags(policyType));
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets (clears) the network flags for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose network flags are being unset.
        /// </param>
        /// <returns>
        /// Non-zero if the network flags were unset; otherwise, zero.
        /// </returns>
        public static bool UnsetNetworkFlags( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            return SetNetworkFlags(policyType, NetworkFlags.None);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether network flags are stored in the given plugin
        /// data for the specified policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored network flags are examined.
        /// </param>
        /// <param name="policyType">
        /// The policy type to check.
        /// </param>
        /// <returns>
        /// Non-zero if network flags are stored for the given policy type;
        /// otherwise, zero.
        /// </returns>
        public static bool HaveNetworkFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            NetworkFlags networkFlags = NetworkFlags.None;

            return GetNetworkFlags(
                pluginData, policyType, ref networkFlags);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the network flags stored in the specified plugin data for the
        /// given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored network flags are retrieved.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored network flags are requested.
        /// </param>
        /// <param name="networkFlags">
        /// Upon success, receives the stored network flags.
        /// </param>
        /// <returns>
        /// Non-zero if stored network flags were found; otherwise, zero.
        /// </returns>
        public static bool GetNetworkFlags( /* CORE? */
            IPluginData pluginData,       /* in */
            PolicyType policyType,        /* in */
            ref NetworkFlags networkFlags /* out */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.NetworkFlagsPropertyName, typeof(NetworkFlagsDictionary));

            if (name == null)
                return false;

            NetworkFlagsDictionary flags;

            GetOrCreateDictionary<NetworkFlags>(
                auxiliaryData, name, false, out flags);

            NetworkFlags localNetworkFlags;

            if ((flags != null) && flags.TryGetValue(
                    policyType, out localNetworkFlags))
            {
                networkFlags = localNetworkFlags;
                return true;
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified network flags in the supplied plugin data for
        /// the given policy type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data in which the network flags are stored.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored network flags are being set.
        /// </param>
        /// <param name="networkFlags">
        /// The network flags to store.
        /// </param>
        /// <returns>
        /// Non-zero if the network flags were stored; otherwise, zero.
        /// </returns>
        public static bool SetNetworkFlags( /* CORE? */
            IPluginData pluginData,   /* in */
            PolicyType policyType,    /* in */
            NetworkFlags networkFlags /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.NetworkFlagsPropertyName, typeof(NetworkFlagsDictionary));

            if (name == null)
                return false;

            NetworkFlagsDictionary flags;

            GetOrCreateDictionary<NetworkFlags>(
                auxiliaryData, name, true, out flags);

            flags[policyType] = networkFlags;
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the stored network flags for the given policy type from
        /// the specified plugin data.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data from which the network flags are removed.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose stored network flags are being removed.
        /// </param>
        /// <returns>
        /// Non-zero if the network flags were removed; otherwise, zero.
        /// </returns>
        public static bool UnsetNetworkFlags( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType   /* in */
            )
        {
            if (pluginData == null)
                return false;

            ObjectDictionary auxiliaryData = pluginData.AuxiliaryData;

            if (auxiliaryData == null)
                return false;

            string name = CertificateSharedOps.GetNameForAuxiliaryData(
                Constants.NetworkFlagsPropertyName, typeof(NetworkFlagsDictionary));

            if (name == null)
                return false;

            NetworkFlagsDictionary flags;

            GetOrCreateDictionary<NetworkFlags>(
                auxiliaryData, name, true, out flags);

            bool result = flags.Remove(policyType);
            MaybeUpdateAuxiliaryData(pluginData, auxiliaryData);

            return result;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /* POLICY IMPLEMENTATION USE ONLY */
        /// <summary>
        /// Resolves the effective network flags for the given policy type,
        /// preferring any network flags stored in the supplied plugin data
        /// and combining them with the local "Other" network flags as needed.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose network flags are resolved.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with the request, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose stored network flags are preferred.
        /// </param>
        /// <param name="networkFlags">
        /// On input, the candidate network flags; on output, the resolved
        /// network flags.
        /// </param>
        /// <param name="error">
        /// Reserved for error information; not currently used.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success.
        /// </returns>
        public static ReturnCode CheckNetworkFlags( /* CORE? */
            PolicyType policyType,         /* in */
            Interpreter interpreter,       /* in: NOT USED */
            IPluginData pluginData,        /* in */
            ref NetworkFlags networkFlags, /* in, out */
            ref Result error               /* out: NOT USED */
            )
        {
            if (pluginData != null)
            {
                NetworkFlags localNetworkFlags = NetworkFlags.Default;

                if (GetNetworkFlags(
                        pluginData, policyType, ref localNetworkFlags))
                {
                    NetworkFlags localOtherNetworkFlags = NetworkFlags.None;

                    //
                    // BUGFIX: Make sure the local "Other" network flags are
                    //         combined with the local "File" and "Script"
                    //         policies.  For the global script flags, this
                    //         is not necessary because the "Other" script
                    //         flags are already combined by the associated
                    //         "Licensing.Policies" static classes.
                    //
                    if (policyType != PolicyType.Other)
                    {
                        /* IGNORED */
                        GetNetworkFlags(pluginData,
                            PolicyType.Other, ref localOtherNetworkFlags);
                    }

                    networkFlags = localNetworkFlags | localOtherNetworkFlags;
                    return ReturnCode.Ok;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the renew callback configured for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose renew callback is requested.
        /// </param>
        /// <returns>
        /// The renew callback for the specified policy type, or null if there
        /// is none.
        /// </returns>
        public static RenewCallback GetRenewCallback( /* CORE? */
            PolicyType policyType /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return Policies.Script.RenewCallback;
                    }
                case PolicyType.File:
                    {
                        return Policies.File.RenewCallback;
                    }
                case PolicyType.Stream:
                    {
                        return Policies.Stream.RenewCallback;
                    }
                case PolicyType.License:
                    {
                        return Policies.License.RenewCallback;
                    }
                case PolicyType.KeyPair:
                    {
                        return Policies.KeyPair.RenewCallback;
                    }
                case PolicyType.Trace:
                    {
                        return Policies.Trace.RenewCallback;
                    }
                case PolicyType.Other:
                    {
                        return Policies.Other.RenewCallback;
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the renew callback configured for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose renew callback is being set.
        /// </param>
        /// <param name="renewCallback">
        /// The renew callback to associate with the policy.
        /// </param>
        /// <returns>
        /// Non-zero if the renew callback was set; otherwise, zero.
        /// </returns>
        public static bool SetRenewCallback(
            PolicyType policyType,      /* in */
            RenewCallback renewCallback /* in */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        Policies.Script.RenewCallback = renewCallback;
                        return true;
                    }
                case PolicyType.File:
                    {
                        Policies.File.RenewCallback = renewCallback;
                        return true;
                    }
                case PolicyType.Stream:
                    {
                        Policies.Stream.RenewCallback = renewCallback;
                        return true;
                    }
                case PolicyType.License:
                    {
                        Policies.License.RenewCallback = renewCallback;
                        return true;
                    }
                case PolicyType.KeyPair:
                    {
                        Policies.KeyPair.RenewCallback = renewCallback;
                        return true;
                    }
                case PolicyType.Trace:
                    {
                        Policies.Trace.RenewCallback = renewCallback;
                        return true;
                    }
                case PolicyType.Other:
                    {
                        Policies.Other.RenewCallback = renewCallback;
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets (clears) the renew callback configured for the specified
        /// policy type.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose renew callback is being unset.
        /// </param>
        /// <returns>
        /// Non-zero if the renew callback was unset; otherwise, zero.
        /// </returns>
        public static bool UnsetRenewCallback(
            PolicyType policyType /* in */
            )
        {
            return SetRenewCallback(policyType, null);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the policy, key name, key ring name, and flag data for the
        /// specified policy type from one plugin data instance to another.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose data is being copied.
        /// </param>
        /// <param name="sourcePluginData">
        /// The plugin data from which the data is read.
        /// </param>
        /// <param name="targetPluginData">
        /// The plugin data to which the data is written.
        /// </param>
        /// <param name="extraPolicy">
        /// Additional execution policy flags to combine with the copied
        /// policy.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to copy only data stored locally in the source plugin
        /// data, rather than falling back to the global values.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode CopyData( /* CORE? */
            PolicyType policyType,        /* in */
            IPluginData sourcePluginData, /* in */
            IPluginData targetPluginData, /* in */
            ExecutionPolicy extraPolicy,  /* in */
            bool localOnly,               /* in */
            ref Result error              /* out */
            )
        {
            if (sourcePluginData == null)
            {
                error = "invalid source plugin data";
                return ReturnCode.Error;
            }

            if (targetPluginData == null)
            {
                error = "invalid target plugin data";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            bool setPolicy = false;
            ExecutionPolicy policy = ExecutionPolicy.Undefined;

            if (GetPolicy(sourcePluginData, policyType, ref policy))
            {
                policy |= extraPolicy;
                setPolicy = true;
            }
            else if (!localOnly)
            {
                policy = GetPolicy(policyType);
                policy |= extraPolicy;
                setPolicy = true;
            }

            if (setPolicy &&
                !SetPolicy(targetPluginData, policyType, policy))
            {
                error = String.Format(
                    "could not set target {0} policy",
                    Utility.FormatWrapOrNull(policyType));

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            bool setKeyName = false;
            string keyName = null;

            if (GetKeyName(sourcePluginData, policyType, ref keyName))
            {
                setKeyName = true;
            }
            else if (!localOnly)
            {
                keyName = GetKeyName(policyType);
                setKeyName = true;
            }

            if (setKeyName &&
                !SetKeyName(targetPluginData, policyType, keyName))
            {
                error = String.Format(
                    "could not set target {0} key name",
                    Utility.FormatWrapOrNull(policyType));

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            bool setKeyRingName = false;
            string keyRingName = null;

            if (GetKeyRingName(sourcePluginData, policyType, ref keyRingName))
            {
                setKeyRingName = true;
            }
            else if (!localOnly)
            {
                keyRingName = GetKeyRingName(policyType);
                setKeyRingName = true;
            }

            if (setKeyRingName &&
                !SetKeyRingName(targetPluginData, policyType, keyRingName))
            {
                error = String.Format(
                    "could not set target {0} key ring name",
                    Utility.FormatWrapOrNull(policyType));

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            bool setScriptFlags = false;
            ScriptFlags scriptFlags = ScriptFlags.None;

            if (GetScriptFlags(sourcePluginData, policyType, ref scriptFlags))
            {
                setScriptFlags = true;
            }
            else if (!localOnly)
            {
                scriptFlags = GetScriptFlags(policyType);
                setScriptFlags = true;
            }

            if (setScriptFlags &&
                !SetScriptFlags(targetPluginData, policyType, scriptFlags))
            {
                error = String.Format(
                    "could not set target {0} script flags",
                    Utility.FormatWrapOrNull(policyType));

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            bool setPathFlags = false;
            PathFlags pathFlags = PathFlags.None;

            if (GetPathFlags(sourcePluginData, policyType, ref pathFlags))
            {
                setPathFlags = true;
            }
            else if (!localOnly)
            {
                pathFlags = GetPathFlags(policyType);
                setScriptFlags = true;
            }

            if (setPathFlags &&
                !SetPathFlags(targetPluginData, policyType, pathFlags))
            {
                error = String.Format(
                    "could not set target {0} path flags",
                    Utility.FormatWrapOrNull(policyType));

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            bool setNetworkFlags = false;
            NetworkFlags networkFlags = NetworkFlags.None;

            if (GetNetworkFlags(sourcePluginData, policyType, ref networkFlags))
            {
                setNetworkFlags = true;
            }
            else if (!localOnly)
            {
                networkFlags = GetNetworkFlags(policyType);
                setNetworkFlags = true;
            }

            if (setNetworkFlags &&
                !SetNetworkFlags(targetPluginData, policyType, networkFlags))
            {
                error = String.Format(
                    "could not set target {0} path flags",
                    Utility.FormatWrapOrNull(policyType));

                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the policy and associated data for every supported policy
        /// type from one plugin data instance to another.
        /// </summary>
        /// <param name="sourcePluginData">
        /// The plugin data from which the data is read.
        /// </param>
        /// <param name="targetPluginData">
        /// The plugin data to which the data is written.
        /// </param>
        /// <param name="extraPolicy">
        /// Additional execution policy flags to combine with each copied
        /// policy.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to copy only data stored locally in the source plugin
        /// data, rather than falling back to the global values.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode CopyData( /* CORE? */
            IPluginData sourcePluginData, /* in */
            IPluginData targetPluginData, /* in */
            ExecutionPolicy extraPolicy,  /* in */
            bool localOnly,               /* in */
            ref Result error              /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
            {
                error = "policy types not available";
                return ReturnCode.Error;
            }

            foreach (PolicyType policyType in policyTypes)
            {
                if (CopyData(
                        policyType, sourcePluginData,
                        targetPluginData, extraPolicy,
                        localOnly, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the global policy and associated data for the specified
        /// policy type back to their default or unset values.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose global data is being reset.
        /// </param>
        /// <param name="stopOnError">
        /// Non-zero to stop and return on the first failure; zero to continue
        /// and accumulate errors.
        /// </param>
        /// <param name="errors">
        /// Receives any accumulated error messages.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode ResetData( /* CORE? */
            PolicyType policyType, /* in */
            bool stopOnError,      /* in */
            ref ResultList errors  /* out */
            )
        {
            if (!ResetPolicy(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not reset global {0} policy",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!UnsetKeyName(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} key name",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!UnsetKeyRingName(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} key ring name",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: Do not call UnsetScriptFlags;
            //       this must revert to default,
            //       not none.
            //
            if (!ResetScriptFlags(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} script flags",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: Do not call UnsetPathFlags;
            //       this must revert to default,
            //       not none.
            //
            if (!ResetPathFlags(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} path flags",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: Do not call UnsetNetworkFlags;
            //       this must revert to default,
            //       not none.
            //
            if (!ResetNetworkFlags(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} network flags",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the policy and associated data stored in the specified
        /// plugin data for the given policy type by unsetting each value.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose plugin data is being reset.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose stored values are unset.
        /// </param>
        /// <param name="stopOnError">
        /// Non-zero to stop and return on the first failure; zero to continue
        /// and accumulate errors.
        /// </param>
        /// <param name="errors">
        /// Receives any accumulated error messages.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode ResetData( /* CORE? */
            PolicyType policyType,  /* in */
            IPluginData pluginData, /* in */
            bool stopOnError,       /* in */
            ref ResultList errors   /* out */
            )
        {
            if (HavePolicy(pluginData, policyType))
            {
                if (!UnsetPolicy(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not reset plugin {0} policy",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HaveKeyName(pluginData, policyType))
            {
                if (!UnsetKeyName(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not reset plugin {0} key name",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HaveKeyRingName(pluginData, policyType))
            {
                if (!UnsetKeyRingName(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not reset plugin {0} key ring name",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HaveScriptFlags(pluginData, policyType))
            {
                if (!UnsetScriptFlags(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not reset plugin {0} script flags",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HavePathFlags(pluginData, policyType))
            {
                if (!UnsetPathFlags(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not reset plugin {0} path flags",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HaveNetworkFlags(pluginData, policyType))
            {
                if (!UnsetNetworkFlags(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not reset plugin {0} network flags",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the global policy and associated data for every supported
        /// policy type back to their default or unset values.
        /// </summary>
        /// <param name="stopOnError">
        /// Non-zero to stop and return on the first failure; zero to continue
        /// and accumulate errors.
        /// </param>
        /// <param name="errors">
        /// Receives any accumulated error messages.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode ResetData( /* CORE? */
            bool stopOnError,     /* in */
            ref ResultList errors /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("policy types not available");
                return ReturnCode.Error;
            }

            foreach (PolicyType policyType in policyTypes)
            {
                if (ResetData(
                        policyType, stopOnError,
                        ref errors) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the policy and associated data for every supported policy
        /// type, both in the specified plugin data and, optionally, in the
        /// global state.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored values are reset.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to reset only the plugin data values; zero to also reset
        /// the global values.
        /// </param>
        /// <param name="stopOnError">
        /// Non-zero to stop and return on the first failure; zero to continue
        /// and accumulate errors.
        /// </param>
        /// <param name="errors">
        /// Receives any accumulated error messages.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode ResetData( /* CORE? */
            IPluginData pluginData, /* in */
            bool localOnly,         /* in */
            bool stopOnError,       /* in */
            ref ResultList errors   /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("policy types not available");
                return ReturnCode.Error;
            }

            foreach (PolicyType policyType in policyTypes)
            {
                if (ResetData(
                        policyType, pluginData, stopOnError,
                        ref errors) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!localOnly && ResetData(
                        policyType, stopOnError,
                        ref errors) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the global policy and associated data for the specified
        /// policy type, clearing each value to none.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose global data is being unset.
        /// </param>
        /// <param name="stopOnError">
        /// Non-zero to stop and return on the first failure; zero to continue
        /// and accumulate errors.
        /// </param>
        /// <param name="errors">
        /// Receives any accumulated error messages.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode UnsetData( /* CORE? */
            PolicyType policyType, /* in */
            bool stopOnError,      /* in */
            ref ResultList errors  /* out */
            )
        {
            if (!UnsetPolicy(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} policy",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!UnsetKeyName(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} key name",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!UnsetKeyRingName(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} key ring name",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!UnsetScriptFlags(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} script flags",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!UnsetPathFlags(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} path flags",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            if (!UnsetNetworkFlags(policyType))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "could not unset global {0} network flags",
                    Utility.FormatWrapOrNull(policyType)));

                if (stopOnError)
                    return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the policy and associated data stored in the specified
        /// plugin data for the given policy type, clearing each value that is
        /// present.
        /// </summary>
        /// <param name="policyType">
        /// The policy type whose plugin data is being unset.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose stored values are cleared.
        /// </param>
        /// <param name="stopOnError">
        /// Non-zero to stop and return on the first failure; zero to continue
        /// and accumulate errors.
        /// </param>
        /// <param name="errors">
        /// Receives any accumulated error messages.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode UnsetData( /* CORE? */
            PolicyType policyType,  /* in */
            IPluginData pluginData, /* in */
            bool stopOnError,       /* in */
            ref ResultList errors   /* out */
            )
        {
            if (HavePolicy(pluginData, policyType))
            {
                if (!UnsetPolicy(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not unset plugin {0} policy",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HaveKeyName(pluginData, policyType))
            {
                if (!UnsetKeyName(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not unset plugin {0} key name",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HaveKeyRingName(pluginData, policyType))
            {
                if (!UnsetKeyRingName(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not unset plugin {0} key ring name",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HaveScriptFlags(pluginData, policyType))
            {
                if (!UnsetScriptFlags(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not unset plugin {0} script flags",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HavePathFlags(pluginData, policyType))
            {
                if (!UnsetPathFlags(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not unset plugin {0} path flags",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (HaveNetworkFlags(pluginData, policyType))
            {
                if (!UnsetNetworkFlags(pluginData, policyType))
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "could not unset plugin {0} network flags",
                        Utility.FormatWrapOrNull(policyType)));

                    if (stopOnError)
                        return ReturnCode.Error;
                }
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the global policy and associated data for every supported
        /// policy type, clearing each value to none.
        /// </summary>
        /// <param name="stopOnError">
        /// Non-zero to stop and return on the first failure; zero to continue
        /// and accumulate errors.
        /// </param>
        /// <param name="errors">
        /// Receives any accumulated error messages.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode UnsetData( /* CORE? */
            bool stopOnError,     /* in */
            ref ResultList errors /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("policy types not available");
                return ReturnCode.Error;
            }

            foreach (PolicyType policyType in policyTypes)
            {
                if (UnsetData(
                        policyType, stopOnError,
                        ref errors) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the policy and associated data for every supported policy
        /// type, both in the specified plugin data and, optionally, in the
        /// global state.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose stored values are cleared.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to unset only the plugin data values; zero to also unset
        /// the global values.
        /// </param>
        /// <param name="stopOnError">
        /// Non-zero to stop and return on the first failure; zero to continue
        /// and accumulate errors.
        /// </param>
        /// <param name="errors">
        /// Receives any accumulated error messages.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode UnsetData( /* CORE? */
            IPluginData pluginData, /* in */
            bool localOnly,         /* in */
            bool stopOnError,       /* in */
            ref ResultList errors   /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("policy types not available");
                return ReturnCode.Error;
            }

            foreach (PolicyType policyType in policyTypes)
            {
                if (UnsetData(
                        policyType, pluginData, stopOnError,
                        ref errors) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!localOnly && UnsetData(
                        policyType, stopOnError,
                        ref errors) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Command Support Methods
        /// <summary>
        /// Optionally enables or disables script security for the specified
        /// interpreter, enforcing enterprise lockdown and security-core
        /// restrictions, and updating the interpreter's security state.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose security state is being changed.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the request.
        /// </param>
        /// <param name="enabled">
        /// Non-zero to enable security, zero to disable it, or null to leave
        /// the enabled state unchanged.
        /// </param>
        /// <param name="allowAnyPlugin">
        /// Non-zero to allow non-security-core plugins to perform the change.
        /// </param>
        /// <param name="localOnly">
        /// Non-zero to modify only the plugin data policies rather than the
        /// global policies.
        /// </param>
        /// <param name="ignoreErrors">
        /// Non-zero to ignore individual policy failures.
        /// </param>
        /// <param name="result">
        /// Receives the previous security-enabled state on success, or the
        /// error information on failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode MaybeEnableOrDisable( /* CORE? */
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            bool? enabled,           /* in: OPTIONAL */
            bool allowAnyPlugin,     /* in */
            bool localOnly,          /* in */
            bool ignoreErrors,       /* in */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

#if ENTERPRISE_LOCKDOWN || MAYBE_ENTERPRISE_LOCKDOWN
            //
            // NOTE: When the "Enterprise Lockdown" feature has been
            //       included at compile-time, prevent a script from
            //       ever disabling script security.
            //
            if ((enabled != null) && !(bool)enabled)
            {
                if (Utility.IsEnterpriseLockdownEnabled())
                {
                    result = "cannot disable security: lockdown";
                    return ReturnCode.Error;
                }
            }
#endif

            if (!CertificatePluginOps.IsSecurityCore(pluginData))
            {
                if (!allowAnyPlugin)
                {
#if DEBUG
                    if (interpreter.HasRuntimeOption(
                            Constants.ForbidNonSecurityCoreOption))
#else
                    if (!interpreter.HasRuntimeOption(
                            Constants.AllowNonSecurityCoreOption))
#endif
                    {
                        result = Constants.SecurityCoreOnlyError;
                        return ReturnCode.Error;
                    }
                }

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "MaybeEnableOrDisable: allowAnyPlugin = {0}, error = {1}",
                    allowAnyPlugin, Constants.SecurityCoreOnlyError),
                    typeof(CertificatePolicyOps).Name,
                    TracePriority.MediumHigh);
#endif
            }

#if DEMO_EDITION
            //
            // NOTE: In the "Demo Edition" SKU, make sure that all
            //       renewal callbacks are setup by default.  Since
            //       the "Demo" SKU is always time-limited, this is
            //       logically needed.
            //
            IEnumerable<PolicyType> policyTypes = GetPolicyTypes();

            if (policyTypes != null)
            {
                RenewCallback renewCallback = null;

#if NETWORK && CERTIFICATE_RENEWAL
                renewCallback = CertificateRenewalOps.GetRenewCallback(
                    pluginData, true);
#endif

                foreach (PolicyType policyType in policyTypes)
                {
                    /* IGNORED */
                    SetRenewCallback(policyType, renewCallback);
                }
            }
#endif

            if (enabled != null)
            {
                if (EnableForCommand(pluginData,
                        (bool)enabled, localOnly, ignoreErrors,
                        true, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
            }

            result = interpreter.SetSecurityWasEnabled(enabled);
            return ReturnCode.Ok;
        }
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Policy Support Methods
#if DEBUG || EXTRA_DIAGNOSTICS
        /// <summary>
        /// Appends a fake machine GUID value from the environment to the
        /// specified builder, when such a value is configured (used for
        /// diagnostics and testing).
        /// </summary>
        /// <param name="builder">
        /// The string builder to which the fake machine GUID is appended.
        /// </param>
        /// <returns>
        /// Non-zero if a value was appended; otherwise, zero.
        /// </returns>
        private static bool MaybeAppendFakeMachineGuid(
            StringBuilder builder /* in */
            )
        {
            if (builder != null)
            {
                string value = Configuration.GetVariable(
                    Constants.MachineGuidEnvVarName);

                if (!String.IsNullOrEmpty(value))
                {
                    if (builder.Length > 0)
                        builder.Append(Characters.HorizontalTab);

                    builder.Append(value);
                    return true;
                }
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends a fake machine volume serial number value from the
        /// environment to the specified builder, when such a value is
        /// configured (used for diagnostics and testing).
        /// </summary>
        /// <param name="builder">
        /// The string builder to which the fake volume serial number is
        /// appended.
        /// </param>
        /// <returns>
        /// Non-zero if a value was appended; otherwise, zero.
        /// </returns>
        private static bool MaybeAppendFakeMachineVolumeSerialNumber(
            StringBuilder builder /* in */
            )
        {
            if (builder != null)
            {
                string value = Configuration.GetVariable(
                    Constants.MachineVolumeSerialNumberEnvVarName);

                if (!String.IsNullOrEmpty(value))
                {
                    if (builder.Length > 0)
                        builder.Append(Characters.HorizontalTab);

                    builder.Append(value);
                    return true;
                }
            }

            return false;
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Reads the machine GUID from the Windows registry and parses it as
        /// a <see cref="Guid" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the request; not currently used.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing the registry value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// The machine GUID, or null if it could not be read or parsed.
        /// </returns>
        private static Guid? GetMachineGuid(
            Interpreter interpreter, /* in: NOT USED */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            string keyName = Constants.MachineGuidKeyName;

            if (String.IsNullOrEmpty(keyName))
            {
                error = "invalid machine key name";
                return null;
            }

            try
            {
                RegistryKey rootKey = Registry.LocalMachine;

                if (rootKey == null)
                {
                    error = "invalid machine root key";
                    return null;
                }

                string fullKeyName = CertificateDataOps.JoinKeyNames(
                    rootKey.ToString(), keyName);

                object value = Registry.GetValue(
                    fullKeyName, Constants.MachineGuidValueName,
                    RegistryOps.GetDefaultValue()); /* throw */

                if (value == null)
                {
                    error = "machine key not found";
                    return null;
                }

                if (RegistryOps.IsDefaultValue(value))
                {
                    error = "machine value not set";
                    return null;
                }

                if (!(value is string))
                {
                    error = "machine value is not string";
                    return null;
                }

                Guid machineId = Guid.Empty;

                if (Value.GetGuid((string)value,
                        cultureInfo, ref machineId,
                        ref error) != ReturnCode.Ok)
                {
                    return null;
                }

                return machineId;
            }
            catch (Exception e)
            {
                error = e;
                return null;
            }
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a list describing the machine identifier and its components,
        /// using the specified path flags.  This overload discards any error
        /// information.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing identifier values.
        /// </param>
        /// <param name="flags">
        /// The path flags controlling how the machine identifier is computed.
        /// </param>
        /// <param name="list">
        /// Upon success, receives the machine identifier and its components.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode GetMachine(
            Interpreter interpreter, /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            PathFlags flags,         /* in */
            ref StringList list      /* out */
            )
        {
            Result error = null;

            return GetMachine(
                interpreter, cultureInfo, flags, ref list,
                ref error);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a list describing the machine identifier and its components,
        /// using the specified path flags.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing identifier values.
        /// </param>
        /// <param name="flags">
        /// The path flags controlling how the machine identifier is computed.
        /// </param>
        /// <param name="list">
        /// Upon success, receives the machine identifier and its components.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode GetMachine(
            Interpreter interpreter, /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            PathFlags flags,         /* in */
            ref StringList list,     /* out */
            ref Result error         /* out */
            )
        {
            StringList localList = new StringList();

            Guid? machineId = GetMachineId(
                interpreter, localList, cultureInfo, flags,
                ref error);

            if (machineId == null)
                return ReturnCode.Error;

            localList.Insert(0, flags.ToString());
            localList.Insert(0, "flags");
            localList.Insert(0, machineId.ToString());
            localList.Insert(0, "id");

            list = localList;
            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the machine identifier using the default path flags.
        /// This overload discards any error information.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="list">
        /// The optional list that receives the identifier components.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing identifier values.
        /// </param>
        /// <returns>
        /// The computed machine identifier, or null on failure.
        /// </returns>
        public static Guid? GetMachineId(
            Interpreter interpreter, /* in: OPTIONAL */
            StringList list,         /* in: OPTIONAL */
            CultureInfo cultureInfo  /* in: OPTIONAL */
            )
        {
            Result error = null;

            return GetMachineId(
                interpreter, list, cultureInfo, ref error);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the machine identifier using the specified path flags.
        /// This overload discards any error information.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="list">
        /// The optional list that receives the identifier components.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing identifier values.
        /// </param>
        /// <param name="flags">
        /// The path flags controlling how the machine identifier is computed.
        /// </param>
        /// <returns>
        /// The computed machine identifier, or null on failure.
        /// </returns>
        private static Guid? GetMachineId(
            Interpreter interpreter, /* in: OPTIONAL */
            StringList list,         /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            PathFlags flags          /* in */
            )
        {
            Result error = null;

            return GetMachineId(
                interpreter, list, cultureInfo, flags, ref error);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the machine identifier using the default path flags.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="list">
        /// The optional list that receives the identifier components.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing identifier values.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// The computed machine identifier, or null on failure.
        /// </returns>
        public static Guid? GetMachineId(
            Interpreter interpreter, /* in: OPTIONAL */
            StringList list,         /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            return GetMachineId(interpreter, list, cultureInfo,
                CertificatePolicyState.GetPathFlagsOrDefault(),
                ref error);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the machine identifier by gathering the configured
        /// components (registry GUID, volume serial number, process, user,
        /// machine, and domain names) according to the specified path flags
        /// and hashing them into a <see cref="Guid" />.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter associated with the request.
        /// </param>
        /// <param name="list">
        /// The optional list that receives the individual identifier
        /// components.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing identifier values.
        /// </param>
        /// <param name="flags">
        /// The path flags that control which components are included.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// The computed machine identifier, or null on failure.
        /// </returns>
        public static Guid? GetMachineId(
            Interpreter interpreter, /* in: OPTIONAL */
            StringList list,         /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            PathFlags flags,         /* in */
            ref Result error         /* out */
            )
        {
            StringBuilder builder = new StringBuilder();

#if DEBUG || EXTRA_DIAGNOSTICS
            if (!MaybeAppendFakeMachineGuid(builder))
#endif
            {
#if !NET_STANDARD_20
                if (!Utility.HasFlags(
                        flags, PathFlags.NoRegistry, true) &&
                    Utility.IsWindowsOperatingSystem())
                {
                    Guid? registryId = GetMachineGuid(
                        interpreter, cultureInfo, ref error);

                    if (registryId == null)
                        return null;

                    if (Utility.HasFlags(
                            flags, PathFlags.RegistryOnly, true))
                    {
                        return registryId;
                    }

                    if (builder.Length > 0)
                        builder.Append(Characters.HorizontalTab);

                    builder.Append(registryId.ToString());
                }
#endif
            }

#if DEBUG || EXTRA_DIAGNOSTICS
            if (!MaybeAppendFakeMachineVolumeSerialNumber(builder))
#endif
            {
#if NATIVE && (WINDOWS || UNIX)
                if (!Utility.HasFlags(
                        flags, PathFlags.NoSerialNumber, true))
                {
                    string serialNumber = null;

                    if (Utility.TryGetPathSerialNumber(
                            CertificateAssemblyOps.GetDirectory(),
                            flags, ref serialNumber, ref error))
                    {
                        if (builder.Length > 0)
                            builder.Append(Characters.HorizontalTab);

                        builder.Append(serialNumber);
                    }
                    else
                    {
                        return null;
                    }

                    if (Utility.HasFlags(
                            flags, PathFlags.SerialNumberOnly, true))
                    {
                        goto done;
                    }
                }
#endif
            }

            byte[] bytes; /* REUSED */

            if (Utility.HasFlags(flags, PathFlags.PerProcess, true))
            {
                string fileName = Utility.GetCurrentProcessFileName();

                if (fileName != null)
                {
                    if (Utility.HasFlags(
                            flags, PathFlags.ProcessHashCode, true))
                    {
                        bytes = Utility.HashFile(
                            null, fileName, null, ref error);

                        if (bytes == null)
                            return null;

                        string hashString =
                            CertificateDataOps.FormatHexadecimal(bytes);

                        if (builder.Length > 0)
                            builder.Append(Characters.HorizontalTab);

                        builder.Append(hashString);

                        if (list != null)
                            list.Add("processHash", hashString);
                    }

                    fileName = Path.GetFileName(fileName);

                    if (fileName != null)
                    {
                        if (builder.Length > 0)
                            builder.Append(Characters.HorizontalTab);

                        builder.Append(fileName);

                        if (list != null)
                            list.Add("processName", fileName);
                    }
                }
            }

            bool perUser = Utility.HasFlags(
                flags, PathFlags.PerUser, true);

            string userName;
            string machineName;
            string domainName;

            /* IGNORED */
            Utility.GetLocalNames(
                perUser, null, out userName, out machineName,
                out domainName);

            if (perUser)
            {
                if (builder.Length > 0)
                    builder.Append(Characters.HorizontalTab);

                builder.Append(userName);

                if (list != null)
                    list.Add("userName", userName);
            }

            if (builder.Length > 0)
                builder.Append(Characters.HorizontalTab);

            builder.Append(machineName);

            if (list != null)
                list.Add("machineName", machineName);

            if (perUser)
            {
                if (builder.Length > 0)
                    builder.Append(Characters.HorizontalTab);

                builder.Append(domainName);

                if (list != null)
                    list.Add("domainName", domainName);
            }

#if NATIVE && (WINDOWS || UNIX)
        done:
#endif

            if (builder.Length == 0)
            {
                error = "no machine identifier components available";
                return null;
            }

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.MaybeLogAndDebugTrace(
                String.Format(
                    "Machine identifier components ({0}): {1}",
                    Utility.FormatWrapOrNull(flags),
                    Utility.FormatWrapOrNull(builder)),
                typeof(CertificatePolicyOps).Name,
                TracePriority.MediumHigh, 0); /* EXEMPT */
#endif

            bytes = Utility.HashString(
                interpreter, builder.ToString(), EncodingType.Policy,
                ref error);

            if (bytes == null)
                return null;

            Array.Resize(ref bytes, Constants.SizeOfGuid);
            return new Guid(bytes);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified string as a machine identifier.
        /// This overload discards any error information.
        /// </summary>
        /// <param name="value">
        /// The string value to parse.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing the value.
        /// </param>
        /// <returns>
        /// The parsed machine identifier, or null if it could not be parsed.
        /// </returns>
        private static Guid? TryParseAsMachineId(
            string value,           /* in */
            CultureInfo cultureInfo /* in: OPTIONAL */
            )
        {
            Result error = null;

            return TryParseAsMachineId(value, cultureInfo, ref error);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to parse the specified string as a machine identifier.
        /// </summary>
        /// <param name="value">
        /// The string value to parse.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used when parsing the value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// The parsed machine identifier, or null if it could not be parsed.
        /// </returns>
        public static Guid? TryParseAsMachineId(
            string value,            /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            Guid guidValue = Guid.Empty;

            if (Value.GetGuid(
                    value, cultureInfo, ref guidValue,
                    ref error) == ReturnCode.Ok)
            {
                return guidValue;
            }
            else
            {
                return null;
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key domain, when parsed as a
        /// machine identifier, matches the current machine identifier.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter used to compute the machine identifier.
        /// </param>
        /// <param name="keyDomain">
        /// The key domain value to parse and compare.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing identifier values.
        /// </param>
        /// <returns>
        /// Non-zero if the key domain matches the current machine identifier;
        /// otherwise, zero.
        /// </returns>
        public static bool MatchKeyDomainToMachineId(
            Interpreter interpreter, /* in: OPTIONAL */
            string keyDomain,        /* in */
            CultureInfo cultureInfo  /* in */
            )
        {
            Result error; /* REUSED */
            Guid? machineId1;

            error = null;

            machineId1 = GetMachineId(
                interpreter, null, cultureInfo, ref error);

            if (machineId1 == null)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Could not get machine identifier: {0}",
                        Utility.FormatWrapOrNull(error)),
                    typeof(CertificatePolicyOps).Name,
                    TracePriority.Medium, 0); /* EXEMPT */
#endif

                return false;
            }

            Guid? machineId2;

            error = null;

            machineId2 = TryParseAsMachineId(
                keyDomain, cultureInfo, ref error);

            if (machineId2 == null)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    String.Format(
                        "Could not parse {0} as machine identifier: {1}",
                        Utility.FormatWrapOrNull(keyDomain),
                        Utility.FormatWrapOrNull(error)),
                    typeof(CertificatePolicyOps).Name,
                    TracePriority.Low, 0); /* EXEMPT */
#endif

                return false;
            }

            return ((Guid)machineId1).Equals((Guid)machineId2);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether any of the specified key domains can be parsed
        /// as a machine identifier (GUID).
        /// </summary>
        /// <param name="keyDomains">
        /// The list of key domains to examine.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when parsing identifier values.
        /// </param>
        /// <returns>
        /// Non-zero if any key domain parses as a machine identifier;
        /// otherwise, zero.
        /// </returns>
        public static bool HasAnyGuidKeyDomain(
            IList<string> keyDomains, /* in */
            CultureInfo cultureInfo   /* in */
            )
        {
            if (keyDomains == null)
                return false;

            foreach (string keyDomain in keyDomains)
            {
                if (keyDomain == null)
                    continue;

                if (TryParseAsMachineId(keyDomain, cultureInfo) != null)
                    return true;
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the specified key pair matches at least one of its
        /// configured key domains for the given URI.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose key domains are checked.
        /// </param>
        /// <param name="uri">
        /// The URI to match against the key pair's key domains.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when matching identifier values.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if a key domain matched; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode CheckKeyDomains(
            IKeyPair keyPair,        /* in */
            Uri uri,                 /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            if (!keyPair.MatchAnyKeyDomain(uri, cultureInfo, ref error))
                return ReturnCode.Error;

            return ReturnCode.Ok;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the policy context, encoding, script, and timeout from
        /// the client data associated with a script policy callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the callback.
        /// </param>
        /// <param name="clientData">
        /// The client data from which the script context is extracted.
        /// </param>
        /// <param name="policyContext">
        /// Upon success, receives the extracted policy context.
        /// </param>
        /// <param name="encoding">
        /// Upon success, receives the extracted encoding.
        /// </param>
        /// <param name="script">
        /// Upon success, receives the extracted script.
        /// </param>
        /// <param name="timeout">
        /// Upon success, receives the extracted timeout, if any.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode ExtractScriptContextData(
            Interpreter interpreter,          /* in */
            IClientData clientData,           /* in */
            ref IPolicyContext policyContext, /* out */
            ref Encoding encoding,            /* out */
            ref IScript script,               /* out */
            ref int? timeout,                 /* out */
            ref Result error                  /* out */
            )
        {
            IPolicyContext localPolicyContext = null;
            Encoding localEncoding = null;
            IScript localScript = null;
            int? localTimeout = null;
            Result scriptError = null;

            if (Utility.ExtractPolicyContextAndScript(
                    interpreter, clientData, ref localPolicyContext,
                    ref localEncoding, ref localScript,
                    ref localTimeout, ref scriptError) == ReturnCode.Ok)
            {
                policyContext = localPolicyContext;
                encoding = localEncoding;
                script = localScript;
                timeout = localTimeout;

                return ReturnCode.Ok;
            }
            else
            {
                error = scriptError;
                return ReturnCode.Error;
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if XML
        /// <summary>
        /// Extracts the policy context, file name, timeout, encoding, text,
        /// hash value, and raw bytes from the client data associated with a
        /// file or stream policy callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the callback.
        /// </param>
        /// <param name="clientData">
        /// The client data from which the file context is extracted.
        /// </param>
        /// <param name="policyContext">
        /// Upon success, receives the extracted policy context.
        /// </param>
        /// <param name="fileName">
        /// Upon success, receives the extracted file name.
        /// </param>
        /// <param name="timeout">
        /// Upon success, receives the extracted timeout, if any.
        /// </param>
        /// <param name="encoding">
        /// Upon success, receives the extracted encoding.
        /// </param>
        /// <param name="text">
        /// Upon success, receives the extracted text.
        /// </param>
        /// <param name="hashValue">
        /// Upon success, receives the extracted hash value.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the extracted raw bytes.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the accumulated error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        private static ReturnCode ExtractFileContextData(
            Interpreter interpreter,          /* in */
            IClientData clientData,           /* in */
            ref IPolicyContext policyContext, /* out */
            ref string fileName,              /* out */
            ref int? timeout,                 /* out */
            ref Encoding encoding,            /* out */
            ref string text,                  /* out */
            ref byte[] hashValue,             /* out */
            ref ByteList bytes,               /* out */
            ref Result error                  /* out */
            )
        {
            IPolicyContext localPolicyContext = null;
            string localFileName = null;
            int? localTimeout = null;
            Encoding localEncoding = null;
            string localText = null;
            byte[] localHashValue = null;
            ByteList localBytes = null;
            Result fileError = null;
            Result textError = null;

            if ((Utility.ExtractPolicyContextAndFileName(
                    interpreter, clientData, ref localPolicyContext,
                    ref localFileName, ref localTimeout,
                    ref fileError) == ReturnCode.Ok) &&
                (Utility.ExtractPolicyContextAndTextAndBytes(
                    interpreter, clientData, ref localPolicyContext,
                    ref localEncoding, ref localText, ref localHashValue,
                    ref localBytes, ref textError) == ReturnCode.Ok))
            {
                policyContext = localPolicyContext;
                fileName = localFileName;
                timeout = localTimeout;
                encoding = localEncoding;
                text = localText;
                hashValue = localHashValue;
                bytes = localBytes;

                return ReturnCode.Ok;
            }
            else
            {
                ResultList errors = null;

                if (fileError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(fileError);
                }

                if (textError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(textError);
                }

                error = errors;
                return ReturnCode.Error;
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //
        // TODO: This method may need more complex semantics.
        //
        /// <summary>
        /// Selects the primary key pair to report, preferring the file key
        /// pair when present and otherwise falling back to the string key
        /// pair.
        /// </summary>
        /// <param name="stringKeyPair">
        /// The key pair derived from verifying the string content.
        /// </param>
        /// <param name="fileKeyPair">
        /// The key pair derived from verifying the file content.
        /// </param>
        /// <returns>
        /// The selected primary key pair.
        /// </returns>
        private static IKeyPair SelectPrimaryKeyPair(
            IKeyPair stringKeyPair, /* in */
            IKeyPair fileKeyPair    /* in */
            )
        {
            if (fileKeyPair != null)
                return fileKeyPair;

            return stringKeyPair;
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the hash algorithm to use for the specified key
        /// pairs, certificate, and hash algorithm type.
        /// </summary>
        /// <param name="keyPairs">
        /// The key pairs that influence the hash algorithm selection.
        /// </param>
        /// <param name="certificate">
        /// The certificate that influences the hash algorithm selection.
        /// </param>
        /// <param name="hashAlgorithmType">
        /// The requested hash algorithm type.
        /// </param>
        /// <returns>
        /// The name of the selected hash algorithm.
        /// </returns>
        private static string GetHashAlgorithm( /* POLICY USE ONLY */
            IEnumerable<IKeyPair> keyPairs,     /* in */
            ICertificate certificate,           /* in */
            HashAlgorithmType hashAlgorithmType /* in */
            )
        {
            return CertificateSharedOps.GetHashAlgorithm(
                null, keyPairs, certificate, hashAlgorithmType);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the entity type of the specified certificate is
        /// permitted for the content currently being checked, allowing key
        /// ring entities and, when not loading a pending key ring, file and
        /// script entities.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the verification.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose entity type is verified.
        /// </param>
        /// <param name="entityType">
        /// On input, the candidate entity type; on output, the certificate's
        /// entity type.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the accumulated error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the entity type is permitted;
        /// otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode VerifyEntityType( /* POLICY USE ONLY */
            Interpreter interpreter,   /* in */
            ICertificate certificate,  /* in */
            ref EntityType entityType, /* in, out */
            ref Result error           /* out */
            )
        {
            if (certificate == null)
            {
                error = "invalid certificate";
                return ReturnCode.Error;
            }

            entityType = certificate.EntityType;

            if (entityType == EntityType.Any)
                return ReturnCode.Ok;

            //
            // HACK: *POLICY* If the script being verified
            //       is actually a key ring, then no other
            //       entity types are allowed.
            //
            ResultList errors = null;
            Result result; /* REUSED */

            if (!CertificateKeyRingState.IsPending() ||
                !Utility.IsScriptFileForSettingsPending(interpreter))
            {
                result = null;

                if (CertificateSharedOps.MatchEntityType(
                        entityType, EntityType.File,
                        EntityType.None, true, false,
                        ref result) == ReturnCode.Ok)
                {
                    return ReturnCode.Ok;
                }

                if (result != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(result);
                }

                result = null;

                if (CertificateSharedOps.MatchEntityType(
                        entityType, EntityType.Script,
                        EntityType.None, true, false,
                        ref result) == ReturnCode.Ok)
                {
                    return ReturnCode.Ok;
                }

                if (result != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(result);
                }
            }

            result = null;

            if (CertificateSharedOps.MatchEntityType(
                    entityType, EntityType.KeyRing,
                    EntityType.None, true, false,
                    ref result) == ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }

            if (result != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(result);
            }

            if (errors != null)
                error = errors;

            return ReturnCode.Error;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Hashes the certificate content and attempts to verify the
        /// resulting hash against each of the supplied key pairs, returning
        /// the first key pair that successfully verifies.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing.
        /// </param>
        /// <param name="certificate">
        /// The certificate whose content is hashed and verified.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags that control how the certificate is hashed.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when hashing the content.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to verify the hash against.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require a matching public key token during
        /// verification.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check for revocation during verification.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the hash.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the verification result; upon failure,
        /// receives the accumulated error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if verification succeeded; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode VerifyScript(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in */
            Encoding encoding,                          /* in */
            IEnumerable<IKeyPair> keyPairs,             /* in */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref IKeyPair keyPair,                       /* out */
            ref Result result                           /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            byte[] hashBytes = null;

            if (CertificateSharedOps.Hash(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding,
                    ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (CertificateSharedOps.VerifyHash(
                        "script", hashBytes, hashAlgorithmName,
                        certificate, localKeyPair, matchPublicKeyToken,
                        checkRevocation, ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify script";

            return ReturnCode.Error;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records the specified policy context in the active client data so
        /// it can later be recognized as an approved context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose active policy context is being collected.
        /// </param>
        /// <param name="policyContext">
        /// The policy context to record.
        /// </param>
        /// <returns>
        /// Non-zero if the policy context was collected; otherwise, zero.
        /// </returns>
        private static bool CollectContext(
            Interpreter interpreter,     /* in */
            IPolicyContext policyContext /* in */
            )
        {
            if ((interpreter == null) || (policyContext == null))
                return false;

            ClientDataPair anyPair = Interpreter.GetActivePair();

            if (anyPair == null)
                return false;

            if (!Object.ReferenceEquals(
                    interpreter, policyContext.Interpreter))
            {
                return false;
            }

            IClientData clientData = anyPair.Y;

            if (clientData == null)
                return false;

            object data = clientData.Data;
            IList<object> objects = data as IList<object>;

            if (objects == null)
            {
                if (data != null)
                    return false;

                objects = new List<object>();
                clientData.Data = objects;
            }

            objects.Add(policyContext);
            return true;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the policy contexts recorded in the specified
        /// client data have all been approved.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the client data.
        /// </param>
        /// <param name="clientData">
        /// The client data whose collected policy contexts are examined.
        /// </param>
        /// <returns>
        /// Non-zero if the recorded contexts are approved; otherwise, zero.
        /// </returns>
        public static bool AreApprovedContexts(
            Interpreter interpreter, /* in */
            IClientData clientData   /* in */
            )
        {
            if ((interpreter == null) || (clientData == null))
                return false;

            object data = clientData.Data;

            if (data == null)
                return false;

            IEnumerable<object> collection = data as IEnumerable<object>;

            if (collection == null)
                return false;

            int count = 0;

            foreach (object item in collection)
            {
                IPolicyContext policyContext = item as IPolicyContext;

                if (policyContext == null)
                    return false;

                if (!Object.ReferenceEquals(
                        policyContext.Interpreter, interpreter))
                {
                    return false;
                }

                if (!policyContext.IsApproved())
                    return false;

                count++;
            }

            return (count > 0);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if XML
        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Hashes the specified string value and attempts to verify the
        /// resulting hash against each of the supplied key pairs, returning
        /// the first key pair that successfully verifies.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing.
        /// </param>
        /// <param name="certificate">
        /// The optional certificate associated with the hash.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags that control how the content is hashed.
        /// </param>
        /// <param name="encoding">
        /// The optional encoding used when hashing the value.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to verify the hash against.
        /// </param>
        /// <param name="value">
        /// The optional string value to hash and verify.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require a matching public key token during
        /// verification.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check for revocation during verification.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the hash.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the verification result; upon failure,
        /// receives the accumulated error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if verification succeeded; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode VerifyString( /* NOT USED */
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,             /* in */
            string value,                               /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref IKeyPair keyPair,                       /* out */
            ref Result result                           /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            byte[] hashBytes = null;

            if (CertificateSharedOps.HashString(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, value,
                    ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (CertificateSharedOps.VerifyHash(
                        "string", hashBytes, hashAlgorithmName,
                        certificate, localKeyPair, matchPublicKeyToken,
                        checkRevocation, ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify string";

            return ReturnCode.Error;
        }
#endif
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Hashes the specified string value together with the supplied bytes
        /// and attempts to verify the resulting hash against each of the
        /// supplied key pairs, returning the first key pair that successfully
        /// verifies.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing.
        /// </param>
        /// <param name="certificate">
        /// The optional certificate associated with the hash.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags that control how the content is hashed.
        /// </param>
        /// <param name="encoding">
        /// The optional encoding used when hashing the value.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to verify the hash against.
        /// </param>
        /// <param name="value">
        /// The optional string value to hash and verify.
        /// </param>
        /// <param name="bytes">
        /// The optional bytes to hash and verify along with the value.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require a matching public key token during
        /// verification.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check for revocation during verification.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the hash.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the verification result; upon failure,
        /// receives the accumulated error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if verification succeeded; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode VerifyStringAndBytes(
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in: OPTIONAL */
            ICertificate certificate,                   /* in: OPTIONAL */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,             /* in */
            string value,                               /* in: OPTIONAL */
            ByteList bytes,                             /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref IKeyPair keyPair,                       /* out */
            ref Result result                           /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            byte[] hashBytes = null;

            if (CertificateSharedOps.HashStringAndBytes(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, value,
                    bytes, ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (CertificateSharedOps.VerifyHash(
                        "stringAndBytes", hashBytes, hashAlgorithmName,
                        certificate, localKeyPair, matchPublicKeyToken,
                        checkRevocation, ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify string and bytes";

            return ReturnCode.Error;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Hashes the contents of the specified file and attempts to verify
        /// the resulting hash against each of the supplied key pairs,
        /// returning the first key pair that successfully verifies.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing.
        /// </param>
        /// <param name="certificate">
        /// The certificate associated with the hash.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags that control how the content is hashed.
        /// </param>
        /// <param name="encoding">
        /// The optional encoding used when hashing the file.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to verify the hash against.
        /// </param>
        /// <param name="fileName">
        /// The name (local or remote) of the file to hash and verify.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds, used when reading the file.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require a matching public key token during
        /// verification.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check for revocation during verification.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the hash.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the verification result; upon failure,
        /// receives the accumulated error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if verification succeeded; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode VerifyFile( /* LOCAL OR REMOTE */
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,             /* in */
            string fileName,                            /* in */
            int? timeout,                               /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref IKeyPair keyPair,                       /* out */
            ref Result result                           /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            byte[] hashBytes = null;

            if (CertificateSharedOps.HashFile(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, fileName,
                    timeout, ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (CertificateSharedOps.VerifyHash(
                        "file", hashBytes, hashAlgorithmName,
                        certificate, localKeyPair, matchPublicKeyToken,
                        checkRevocation, ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify file";

            return ReturnCode.Error;
        }
#endif

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts the specified certificate hash flags from script-oriented
        /// flags to file-oriented flags, clearing
        /// <see cref="CertificateHashFlags.Script" /> and setting
        /// <see cref="CertificateHashFlags.File" />.
        /// </summary>
        /// <param name="certificateHashFlags">
        /// On input, the script-oriented hash flags; on output, the
        /// file-oriented hash flags.  No change is made when null.
        /// </param>
        private static void FlagsFromScriptToFile(
            ref CertificateHashFlags? certificateHashFlags /* in, out */
            )
        {
            if (certificateHashFlags == null)
                return;

            CertificateHashFlags localCertificateHashFlags =
                (CertificateHashFlags)certificateHashFlags;

            localCertificateHashFlags &= ~CertificateHashFlags.Script;
            localCertificateHashFlags |= CertificateHashFlags.File;

            certificateHashFlags = localCertificateHashFlags;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Hashes the contents of the specified file (using the supplied
        /// bytes when available) and attempts to verify the resulting hash
        /// against each of the supplied key pairs, returning the first key
        /// pair that successfully verifies.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use.
        /// </param>
        /// <param name="hashKey">
        /// The optional key used when hashing.
        /// </param>
        /// <param name="certificate">
        /// The certificate associated with the hash.
        /// </param>
        /// <param name="certificateHashFlags">
        /// The optional flags that control how the content is hashed.
        /// </param>
        /// <param name="encoding">
        /// The optional encoding used when hashing the file.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs to verify the hash against.
        /// </param>
        /// <param name="fileName">
        /// The name (local or remote) of the file being verified.
        /// </param>
        /// <param name="fileBytes">
        /// The already-read contents of the file to hash and verify.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds, used when reading the file.
        /// </param>
        /// <param name="matchPublicKeyToken">
        /// Non-zero to require a matching public key token during
        /// verification.
        /// </param>
        /// <param name="checkRevocation">
        /// Non-zero to check for revocation during verification.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the hash.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the verification result; upon failure,
        /// receives the accumulated error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if verification succeeded; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode VerifyFile( /* LOCAL OR REMOTE */
            string hashAlgorithmName,                   /* in */
            byte[] hashKey,                             /* in */
            ICertificate certificate,                   /* in */
            CertificateHashFlags? certificateHashFlags, /* in: OPTIONAL */
            Encoding encoding,                          /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,             /* in */
            string fileName,                            /* in */
            byte[] fileBytes,                           /* in */
            int? timeout,                               /* in: OPTIONAL */
            bool matchPublicKeyToken,                   /* in */
            bool checkRevocation,                       /* in */
            ref IKeyPair keyPair,                       /* out */
            ref Result result                           /* out */
            )
        {
            if (keyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            byte[] hashBytes = null;

            if (CertificateSharedOps.HashFile(
                    hashAlgorithmName, hashKey, certificate,
                    certificateHashFlags, encoding, fileName, fileBytes,
                    timeout, ref hashBytes, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            ResultList errors = null;

            foreach (IKeyPair localKeyPair in keyPairs) /* VERIFY LOOP */
            {
                Result localResult = null;

                if (CertificateSharedOps.VerifyHash(
                        "file", hashBytes, hashAlgorithmName,
                        certificate, localKeyPair, matchPublicKeyToken,
                        checkRevocation, ref localResult) == ReturnCode.Ok)
                {
                    keyPair = localKeyPair;
                    result = localResult;

                    return ReturnCode.Ok;
                }
                else if (localResult != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localResult);
                }
            }

            if (errors != null)
                result = errors;
            else
                result = "failed to verify file";

            return ReturnCode.Error;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the specified policy context as approved and records it as a
        /// collected context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the policy context.
        /// </param>
        /// <param name="policyContext">
        /// The policy context to approve.
        /// </param>
        private static void Approved(
            Interpreter interpreter,     /* in */
            IPolicyContext policyContext /* in */
            )
        {
            if (policyContext != null)
                policyContext.Approved();

            /* IGNORED */
            CollectContext(interpreter, policyContext);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the specified policy context as denied and records it as a
        /// collected context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the policy context.
        /// </param>
        /// <param name="policyContext">
        /// The policy context to deny.
        /// </param>
        private static void Denied(
            Interpreter interpreter,     /* in */
            IPolicyContext policyContext /* in */
            )
        {
            if (policyContext != null)
                policyContext.Denied();

            /* IGNORED */
            CollectContext(interpreter, policyContext);
        }
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Policy Implementations
        /// <summary>
        /// Implements the script execution policy callback, verifying the
        /// script's signature against the configured certificate and key
        /// pairs and approving or denying the policy context accordingly.
        /// </summary>
        /// <param name="policy">
        /// The local execution policy in effect for this callback.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the callback.
        /// </param>
        /// <param name="hasFlags">
        /// The string describing the flags that triggered the callback.
        /// </param>
        /// <param name="certificate">
        /// The certificate used to verify the script.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the policy.
        /// </param>
        /// <param name="keyName">
        /// The key name used to locate verification keys.
        /// </param>
        /// <param name="keyRingName">
        /// The key ring name used to locate verification keys.
        /// </param>
        /// <param name="scriptFlags">
        /// The script flags associated with the policy; not currently used.
        /// </param>
        /// <param name="interpreter">
        /// The optional interpreter associated with the callback (may be
        /// null).
        /// </param>
        /// <param name="renewCallback">
        /// The optional renewal callback associated with the policy.
        /// </param>
        /// <param name="clientData">
        /// The client data carrying the policy context and script content.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the callback; not currently used.
        /// </param>
        /// <param name="ignoreBasePolicy">
        /// Non-zero to perform verification even when no base policy is set.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode ScriptCallback( /* POLICY IMPLEMENTATION */
            ExecutionPolicy policy,      /* in: LOCAL */
            PolicyType policyType,       /* in */
            string hasFlags,             /* in */
            ICertificate certificate,    /* in */
            Assembly assembly,           /* in */
            string keyName,              /* in */
            string keyRingName,          /* in */
            ScriptFlags scriptFlags,     /* in: NOT USED */
            Interpreter interpreter,     /* in: OPTIONAL, MAY BE NULL. */
            RenewCallback renewCallback, /* in: OPTIONAL */
            IClientData clientData,      /* in */
            ArgumentList arguments,      /* in: NOT USED */
            bool ignoreBasePolicy,       /* in */
            ref Result result            /* out */
            )
        {
            CultureInfo cultureInfo;
            bool disposed;

            /* NO RESULT */
            CertificateDataOps.SafeGetCultureInfo(
                interpreter, out cultureInfo, out disposed);

            if (disposed)
            {
                result = "interpreter is disposed";
                return ReturnCode.Error;
            }

            CertificatePolicyState.BeginPending();

            try
            {
                ILogClientData logClientData = null;

                try
                {
                    if (Configuration.DoesVariableExist(
                            Constants.ForceLogScriptEnvVarName))
                    {
                        logClientData = new ScriptLogClientData(
                            interpreter, null, null, policyType,
                            policy);
                    }

                    int pushed = 0;

                    try
                    {
                        Utility.MaybePushActiveLogClientData(
                            interpreter, logClientData, ref pushed);

                        ExecutionPolicy? tracePolicy = policy;
                        bool wasEnabled = false;
                        TracePriority? savedBasePriority = null;
                        TracePriority? savedPriorities1 = null;
                        TracePriority? savedPriorities2 = null;
                        IPolicyContext policyContext = null;

                        bool fullTracing = CertificateTraceOps.ShouldForceFullForPolicy() ||
                            Utility.HasFlags(tracePolicy, ExecutionPolicy.FullTracing, true);

                        try
                        {
                            CertificateTraceOps.MaybeChangeExecutionPolicy(
                                interpreter, Constants.ScriptExecutionPolicyEnvVarName,
                                Constants.EnablePolicyTracingLimitMask.ToString(),
                                cultureInfo, ref tracePolicy);

                            fullTracing = CertificateTraceOps.ShouldForceFullForPolicy() ||
                                Utility.HasFlags(tracePolicy, ExecutionPolicy.FullTracing, true); /* REFRESH */

                            CertificateTraceOps.MaybeEnableOrDisableTextWriter(
                                interpreter, cultureInfo, tracePolicy, true,
                                ref wasEnabled, ref savedBasePriority,
                                ref savedPriorities1, ref savedPriorities2);

                            IPlugin plugin = null;

                            if (Utility.ExtractPolicyContextAndPlugin(
                                    interpreter, clientData, ref policyContext,
                                    ref plugin, ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

                            //
                            // HACK: Update the log now using the plugin we just found.
                            //
                            if (logClientData != null)
                                logClientData.Plugin = plugin;

                            PolicyFlags policyFlags = policyContext.Flags;

                            if (!Utility.HasFlags(policyFlags, PolicyFlags.BeforeScript, true))
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Ok;
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Starting {0} policy is {1}, trace policy is {2}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(policy),
                                Utility.FormatWrapOrNull(tracePolicy)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            ExecutionPolicy localPolicy = ExecutionPolicy.Undefined;
                            ExecutionPolicy basePolicy = ExecutionPolicy.Undefined;

                            if (CheckPolicy(
                                    policyType, interpreter, plugin, ref policy,
                                    ref  localPolicy, ref basePolicy, ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Final {0} policy is {1} with a local policy of {2}, a base policy of {3}, and a trace policy of {4}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(policy),
                                Utility.FormatWrapOrNull(localPolicy),
                                Utility.FormatWrapOrNull(basePolicy),
                                Utility.FormatWrapOrNull(tracePolicy)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            //
                            // NOTE: We should bypass the the policy machinery here if there is
                            //       no explicit policy set because it does a bunch of work that
                            //       will then just be thrown away.
                            //
                            if (!ignoreBasePolicy && !Utility.HasFlags(
                                    basePolicy, ExecutionPolicy.BasePolicyMask, false))
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Ok;
                            }

                            Encoding encoding = null;
                            IScript script = null;
                            int? timeout = null;

                            if (ExtractScriptContextData(
                                    interpreter, clientData, ref policyContext,
                                    ref encoding, ref script, ref timeout,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

                            if (timeout == null)
                                timeout = CertificateSharedOps.GetTimeout(interpreter, null);

                            //
                            // HACK: Update the log now using the script we just found.
                            //
                            if ((logClientData != null) && (script != null))
                                logClientData.FileName = script.FileName;

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Starting {0} key name is {1} / {2}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(keyName),
                                Utility.FormatWrapOrNull(keyRingName)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            if (CheckKeyName(
                                    policyType, interpreter, plugin, ref keyName,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

                            if (CheckKeyRingName(
                                    policyType, interpreter, plugin, ref keyRingName,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Final {0} key name is {1} / {2}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(keyName),
                                Utility.FormatWrapOrNull(keyRingName)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);

                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Starting {0} script flags are {1}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(scriptFlags)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            if (CheckScriptFlags(
                                    policyType, interpreter, plugin, ref scriptFlags,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

                            NetworkFlags networkFlags = Helpers.GetNetworkFlags(
                                policyType);

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Final {0} script flags are {1}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(scriptFlags)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);

                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Starting {0} network flags are {1}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(networkFlags)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            if (CheckNetworkFlags(
                                    policyType, interpreter, plugin, ref networkFlags,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Final {0} network flags are {1}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(networkFlags)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            Result localResult; /* REUSED */

#if LICENSING
                            string skipReason = null;

                            localResult = null;

                            if (!CertificateKeyRingState.CanSkipPolicyFeatureChecks(
                                    ref skipReason) &&
                                CertificateSharedOps.MatchFlags(
                                    (certificate != null) ? certificate :
                                        CertificateSharedOps.GetViaPlugin(plugin),
                                    FlagType.Feature, Utility.DefaultAttributeFlagsKey(),
                                    hasFlags, null, false, false, true,
                                    ref localResult) != ReturnCode.Ok)
                            {
                                result = localResult;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

#if DEBUG || FORCE_TRACE
                            if (!String.IsNullOrEmpty(skipReason))
                            {
                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Policy {0} feature checks skipped because {1}",
                                    Utility.FormatWrapOrNull(policyType), skipReason),
                                    typeof(CertificatePolicyOps).Name,
                                    TracePriority.Low, 0);
                            }
#endif
#endif

                            if (!ignoreBasePolicy && (basePolicy == ExecutionPolicy.AllowNone))
                            {
                                result = null;

                                Denied(interpreter, policyContext);
                                return ReturnCode.Ok;
                            }

                            if (!ignoreBasePolicy && (basePolicy == ExecutionPolicy.AllowAny))
                            {
                                result = null;

                                Approved(interpreter, policyContext);
                                return ReturnCode.Ok;
                            }

                            bool allowEmbedded = Utility.HasFlags(
                                policy, ExecutionPolicy.AllowEmbedded, true);

                            bool validateXml = Utility.HasFlags(
                                policy, ExecutionPolicy.ValidateXml, true);

                            CertificateHashFlags? certificateHashFlags = null;

                            localResult = null;

                            ICertificate localCertificate = Certificate.CreateFromScript(
                                script, ref localResult);

                            if (localCertificate != null)
                                certificateHashFlags = CertificateHashFlags.Script;

                            Result extractError = null;

                            if (allowEmbedded && (localCertificate == null))
                            {
                                localCertificate = Certificate.ExtractFromScript(
                                    validateXml, ref script, ref extractError);
                            }

                            Certificate.MaybeAdjustHashFlagsForAuthority(
                                localCertificate, ref certificateHashFlags);

                            if (localCertificate != null)
                            {
                                //
                                // NOTE: Only scripts included with Eagle Enterprise Edition itself
                                //       can ever be signed with the assembly signing key.  This legacy
                                //       usage for the assembly signing key being phased out.  In the
                                //       future, it will only be used to sign license certificates as
                                //       well as the assembly itself.
                                //
                                bool allowAssemblyPublicKey = Utility.HasFlags(
                                    policy, ExecutionPolicy.AllowAssemblyPublicKey, true);

                                ///////////////////////////////////////////////////////////////////////

                                bool allowEmbeddedPublicKey = Utility.HasFlags(
                                    policy, ExecutionPolicy.AllowEmbeddedPublicKey, true);

                                bool allowRingPublicKey = Utility.HasFlags(
                                    policy, ExecutionPolicy.AllowRingPublicKey, true);

                                bool allowAnyPublicKey = Utility.HasFlags(
                                    policy, ExecutionPolicy.AllowAnyPublicKey, true);

                                bool enforceKeyUsage = Utility.HasFlags(
                                    policy, ExecutionPolicy.EnforceKeyUsage, true);

                                AssemblyName assemblyName = (assembly != null) ?
                                    assembly.GetName() : null;

                                IEnumerable<IKeyPair> keyPairs = null;

                                localResult = null;

                                if (CertificateKeyPairOps.GetPublicOnly( /* OK */
                                        keyRingName, PolicyType.Script, false,
                                        assembly, assemblyName, null, keyName, false,
                                        interpreter, localCertificate.EntityType,
                                        allowAssemblyPublicKey, allowEmbeddedPublicKey,
                                        allowRingPublicKey, !allowAnyPublicKey,
                                        enforceKeyUsage, ref keyPairs,
                                        ref localResult) == ReturnCode.Ok)
                                {
                                    bool checkEntityType = Utility.HasFlags(
                                        policy, ExecutionPolicy.CheckEntityType, true);

                                    bool checkPublicKeyToken = Utility.HasFlags(
                                        policy, ExecutionPolicy.CheckPublicKeyToken, true);

                                    bool checkRevocation = Utility.HasFlags(
                                        policy, ExecutionPolicy.CheckRevocation, true);

                                    bool checkQuantity = Utility.HasFlags(
                                        policy, ExecutionPolicy.CheckQuantity, true);

                                    bool protectQuantity = Utility.HasFlags(
                                        policy, ExecutionPolicy.ProtectQuantity, true);

                                    bool perMachine = Utility.HasFlags(
                                        policy, ExecutionPolicy.PerMachine, true);

                                    bool checkExpiry = Utility.HasFlags(
                                        policy, ExecutionPolicy.CheckExpiry, true);

                                    //
                                    // NOTE: Initially, no renewal has been performed.
                                    //       This flag will be set upon renewal being
                                    //       completed successfully -AND- before the
                                    //       script certificate is checked again by
                                    //       jumping to the "retry" label.
                                    //
                                    bool wasRenewed = false;

                                    //
                                    // NOTE: Initially, there is no primary key pair
                                    //       used to verify the certificate and/or
                                    //       returned from the renewal processing.
                                    //
                                    IKeyPair keyPair = null;

                                retry:

                                    EntityType entityType = EntityType.None;
                                    Result entityTypeResult = null;

                                    if (!checkEntityType || (VerifyEntityType(
                                            interpreter, localCertificate, ref entityType,
                                            ref entityTypeResult) == ReturnCode.Ok))
                                    {
                                        IEnumerable<IKeyPair> verifyKeyPairs = (keyPair != null) ?
                                            new IKeyPair[] { keyPair } : keyPairs;

#if DEBUG || FORCE_TRACE
                                        DebugOnlyOps.DumpKeyPairs(
                                            interpreter, logClientData, "ScriptCallback", null,
                                            verifyKeyPairs, typeof(CertificatePolicyOps).Name,
                                            policyType, TracePriority.MediumLow);
#endif

                                        string localHashAlgorithmName = GetHashAlgorithm(
                                            verifyKeyPairs, localCertificate, HashAlgorithmType.Legacy);

                                        ReturnCode verifyCode;
                                        string verifyFileName;
                                        byte[] verifyFileBytes;
                                        Result verifyResult = null;

                                        if ((script != null) && script.ShouldTreatAsFile(
                                                out verifyFileName, out verifyFileBytes))
                                        {
                                            FlagsFromScriptToFile(ref certificateHashFlags);

                                            verifyCode = VerifyFile(
                                                localHashAlgorithmName, null, localCertificate,
                                                certificateHashFlags, encoding, verifyKeyPairs,
                                                verifyFileName, verifyFileBytes, timeout,
                                                checkPublicKeyToken, checkRevocation, ref keyPair,
                                                ref verifyResult);
                                        }
                                        else
                                        {
                                            verifyCode = VerifyScript(
                                                localHashAlgorithmName, null, localCertificate,
                                                certificateHashFlags, encoding, verifyKeyPairs,
                                                checkPublicKeyToken, checkRevocation, ref keyPair,
                                                ref verifyResult);
                                        }

                                        if (verifyCode == ReturnCode.Ok)
                                        {
                                            Result requirementResult = null;
                                            Result processResult = null;

                                            if ((CertificateVerifyOps.CheckForRequirement(
                                                    interpreter, localCertificate, null, cultureInfo,
                                                    ref requirementResult) == ReturnCode.Ok) &&
                                                (CertificateVerifyOps.CheckForProcess(
                                                    interpreter, localCertificate, null, cultureInfo,
                                                    ref processResult) == ReturnCode.Ok))
                                            {
                                                //
                                                // HACK: When the global "force network" flag is set for
                                                //       policy checking, all revocation and expiration
                                                //       checks will require network access.
                                                //
                                                if (CertificatePolicyState.GetForceNetwork())
                                                    networkFlags |= NetworkFlags.ForceMask;

                                                //
                                                // HACK: Maybe invoke the fail-safe checking, which will
                                                //       perform an asynchronous forced remote check to
                                                //       determine if the certificate -OR- its signing
                                                //       key pair has been actively revoked.
                                                //
                                                CertificateRevocationOps.MaybePerformFailSafeChecks( /* OK */
                                                    interpreter, assembly, plugin, localHashAlgorithmName,
                                                    null, encoding, keyPairs, localCertificate, keyPair,
                                                    cultureInfo, Utility.GetUtcNow(), timeout, networkFlags);

                                                Result revocationResult = null;

                                                if (!checkRevocation ||
                                                    (CertificateRevocationOps.IsRevoked( /* OK */
                                                        interpreter, assembly, plugin,
                                                        localHashAlgorithmName, null,
                                                        encoding, keyPairs, localCertificate,
                                                        cultureInfo, timeout, networkFlags,
                                                        ref revocationResult) == ReturnCode.Ok))
                                                {
                                                    Result quantityResult = null;

                                                    if (wasRenewed || !checkQuantity ||
                                                        (CertificateSharedOps.ProcessQuantity(
                                                            interpreter, plugin, localHashAlgorithmName,
                                                            null, localCertificate, cultureInfo, null,
                                                            encoding, null, protectQuantity, perMachine,
                                                            ref quantityResult) == ReturnCode.Ok))
                                                    {
                                                        bool canRenew = true;
                                                        Result activationResult = null;
                                                        Result expiredResult = null;

                                                        if (!CertificateSharedOps.NeedsActivation(
                                                                localCertificate, ref activationResult) &&
                                                            (!checkExpiry || (CertificateSharedOps.IsExpired(
                                                                interpreter, assembly, plugin, localCertificate,
                                                                keyPairs, keyPair, cultureInfo, null, timeout,
                                                                policyType, (wasRenewed ?
                                                                    NetworkFlags.ViaRenewal :
                                                                    NetworkFlags.None) | networkFlags,
                                                                ref canRenew,
                                                                ref expiredResult) == ReturnCode.Ok)))
                                                        {
                                                            // bool saveApprovedData = Utility.HasFlags(
                                                            //     policy, ExecutionPolicy.SaveApprovedData, true);

                                                            result = null;

                                                            Approved(interpreter, policyContext);

                                                            //
                                                            // HACK: Since the "approved key pair" data is only
                                                            //       used for key ring integration, only record
                                                            //       the file policy result for now (i.e. since
                                                            //       key rings always originate from a file).
                                                            //
                                                            // if (saveApprovedData &&
                                                            //     (script != null) && (keyPair != null))
                                                            // {
                                                            //     CertificateKeyPairState.AddApproved(
                                                            //         interpreter, script, keyPair, true);
                                                            // }

#if DEBUG || FORCE_TRACE
                                                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                logClientData, String.Format(
                                                                "Verification of script success, " +
                                                                "localCertificate = {0}, script = {1} ({2}), " +
                                                                "localResult = {3}, result = {4}",
                                                                DebugOnlyOps.FormatCertificate(localCertificate),
                                                                Utility.FormatWrapOrNull(true, true, script),
                                                                DebugOnlyOps.ScriptLength(script),
                                                                Utility.FormatWrapOrNull(true, false, localResult),
                                                                Utility.FormatWrapOrNull(true, false, result)),
                                                                typeof(CertificatePolicyOps).Name,
                                                                TracePriority.MediumLow, 0);
#endif

                                                            return ReturnCode.Ok;
                                                        }
                                                        else if (canRenew && !wasRenewed && (renewCallback != null))
                                                        {
                                                            byte[] oldPublicKeyToken = Certificate.MaybeCopyKey(
                                                                localCertificate);

                                                            string localFileName = null; /* NOT USED */
                                                            Result renewResult = null;

                                                            if (renewCallback(
                                                                    interpreter, assembly, assemblyName,
                                                                    plugin, localHashAlgorithmName, null,
                                                                    null, encoding, keyPairs,
                                                                    new AnyClientData(clientData, false),
                                                                    hasFlags, null, policy, policyType,
                                                                    keyName, keyRingName, timeout,
                                                                    allowEmbedded, validateXml,
                                                                    ref localFileName, ref localCertificate,
                                                                    ref renewResult) == ReturnCode.Ok)
                                                            {
                                                                byte[] newPublicKeyToken = Certificate.MaybeCopyKey(
                                                                    localCertificate);

                                                                if (CertificateDataOps.MatchPublicKeyToken(
                                                                        newPublicKeyToken, oldPublicKeyToken))
                                                                {
                                                                    wasRenewed = true;
                                                                    goto retry;
                                                                }
                                                                else
                                                                {
                                                                    //
                                                                    // NOTE: If the new public key token does not match
                                                                    //       the old one, make sure the new public key
                                                                    //       token is present in the currently loaded
                                                                    //       (and valid) list.
                                                                    //
                                                                    keyPair = CertificateSharedOps.GetKeyPairByPublicKeyToken(
                                                                        keyPairs, newPublicKeyToken);

                                                                    if (keyPair != null)
                                                                    {
                                                                        wasRenewed = true;
                                                                        goto retry;
                                                                    }
                                                                    else
                                                                    {
                                                                        //
                                                                        // NOTE: Next, see if the trusted key ring for
                                                                        //       the interpreter has been updated with
                                                                        //       the new key pair.  This requires the
                                                                        //       associated execution policy flag to be
                                                                        //       enabled.
                                                                        //
                                                                        Result keyRingError = null;

                                                                        if (allowRingPublicKey)
                                                                        {
                                                                            keyPair = CertificateKeyRingOps.GetKeyPair(
                                                                                interpreter, keyRingName, PolicyType.Script,
                                                                                newPublicKeyToken, ref keyRingError);

                                                                            if (keyPair != null)
                                                                            {
                                                                                wasRenewed = true;
                                                                                goto retry;
                                                                            }
                                                                        }

                                                                        ResultList errors = new ResultList();

                                                                        if (activationResult != null)
                                                                            errors.Add(activationResult);

                                                                        if (expiredResult != null)
                                                                            errors.Add(expiredResult);

                                                                        if (renewResult != null)
                                                                            errors.Add(renewResult);

                                                                        if (keyRingError != null)
                                                                            errors.Add(keyRingError);

                                                                        errors.Add("renewal public key token is not present");
                                                                        result = errors;

#if DEBUG || FORCE_TRACE
                                                                        CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                            logClientData, String.Format(
                                                                            "Certificate renewal public key token is not present, " +
                                                                            "localCertificate = {0}, activationResult = {1}, " +
                                                                            "expiredResult = {2}, renewResult = {3}, " +
                                                                            "result = {4}",
                                                                            DebugOnlyOps.FormatCertificate(localCertificate),
                                                                            Utility.FormatWrapOrNull(true, false, activationResult),
                                                                            Utility.FormatWrapOrNull(true, false, expiredResult),
                                                                            Utility.FormatWrapOrNull(true, false, renewResult),
                                                                            Utility.FormatWrapOrNull(true, false, result)),
                                                                            typeof(CertificatePolicyOps).Name,
                                                                            TracePriority.MediumHigh, 0);
#endif
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                ResultList errors = new ResultList();

                                                                if (activationResult != null)
                                                                    errors.Add(activationResult);

                                                                if (expiredResult != null)
                                                                    errors.Add(expiredResult);

                                                                if (renewResult != null)
                                                                    errors.Add(renewResult);

                                                                result = errors;

#if DEBUG || FORCE_TRACE
                                                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                    logClientData, String.Format(
                                                                    "Certificate does not pass renewal check, " +
                                                                    "localCertificate = {0}, activationResult = {1}, " +
                                                                    "expiredResult = {2}, renewResult = {3}, " +
                                                                    "result = {4}",
                                                                    DebugOnlyOps.FormatCertificate(localCertificate),
                                                                    Utility.FormatWrapOrNull(true, false, activationResult),
                                                                    Utility.FormatWrapOrNull(true, false, expiredResult),
                                                                    Utility.FormatWrapOrNull(true, false, renewResult),
                                                                    Utility.FormatWrapOrNull(true, false, result)),
                                                                    typeof(CertificatePolicyOps).Name,
                                                                    TracePriority.MediumHigh, 0);
#endif
                                                            }
                                                        }
                                                        else
                                                        {
                                                            ResultList errors = new ResultList();

                                                            if (activationResult != null)
                                                                errors.Add(activationResult);

                                                            if (expiredResult != null)
                                                                errors.Add(expiredResult);

                                                            result = errors;

#if DEBUG || FORCE_TRACE
                                                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                logClientData, String.Format(
                                                                "Certificate does not pass expiration check, " +
                                                                "localCertificate = {0}, activationResult = {1}, " +
                                                                "expiredResult = {2}, result = {3}",
                                                                DebugOnlyOps.FormatCertificate(localCertificate),
                                                                Utility.FormatWrapOrNull(true, false, activationResult),
                                                                Utility.FormatWrapOrNull(true, false, expiredResult),
                                                                Utility.FormatWrapOrNull(true, false, result)),
                                                                typeof(CertificatePolicyOps).Name,
                                                                TracePriority.MediumHigh, 0);
#endif
                                                        }
                                                    }
                                                    else
                                                    {
                                                        ResultList errors = new ResultList();

                                                        if (quantityResult != null)
                                                            errors.Add(quantityResult);

                                                        result = errors;

#if DEBUG || FORCE_TRACE
                                                        CertificateTraceOps.MaybeLogAndDebugTrace(
                                                            logClientData, String.Format(
                                                            "Certificate does not pass quantity check, " +
                                                            "localCertificate = {0}, quantityResult = {1}, " +
                                                            "result = {2}",
                                                            DebugOnlyOps.FormatCertificate(localCertificate),
                                                            Utility.FormatWrapOrNull(true, false, quantityResult),
                                                            Utility.FormatWrapOrNull(true, false, result)),
                                                            typeof(CertificatePolicyOps).Name,
                                                            TracePriority.MediumHigh, 0);
#endif
                                                    }
                                                }
                                                else
                                                {
                                                    ResultList errors = new ResultList();

                                                    if (revocationResult != null)
                                                        errors.Add(revocationResult);

                                                    result = errors;

#if DEBUG || FORCE_TRACE
                                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                                        logClientData, String.Format(
                                                        "Certificate does not pass revocation check, " +
                                                        "localCertificate = {0}, revocationResult = {1}, " +
                                                        "result = {2}",
                                                        DebugOnlyOps.FormatCertificate(localCertificate),
                                                        Utility.FormatWrapOrNull(true, false, revocationResult),
                                                        Utility.FormatWrapOrNull(true, false, result)),
                                                        typeof(CertificatePolicyOps).Name,
                                                        TracePriority.MediumHigh, 0);
#endif
                                                }
                                            }
                                            else
                                            {
                                                ResultList errors = new ResultList();

                                                if (requirementResult != null)
                                                    errors.Add(requirementResult);

                                                if (processResult != null)
                                                    errors.Add(processResult);

                                                result = errors;

#if DEBUG || FORCE_TRACE
                                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                                    logClientData, String.Format(
                                                    "Certificate does not pass requirement check, " +
                                                    "localCertificate = {0}, requirementResult = {1}, " +
                                                    "processResult = {2}, result = {3}",
                                                    DebugOnlyOps.FormatCertificate(localCertificate),
                                                    Utility.FormatWrapOrNull(true, false, requirementResult),
                                                    Utility.FormatWrapOrNull(true, false, processResult),
                                                    Utility.FormatWrapOrNull(true, false, result)),
                                                    typeof(CertificatePolicyOps).Name,
                                                    TracePriority.MediumHigh, 0);
#endif
                                            }
                                        }
                                        else
                                        {
                                            ResultList errors = new ResultList();

                                            if (verifyResult != null)
                                                errors.Add(verifyResult);

                                            result = errors;

#if DEBUG || FORCE_TRACE
                                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                                logClientData, String.Format(
                                                "Verification of script failure, localCertificate = {0}, " +
                                                "script = {1} ({2}), verifyResult  = {3}, result = {4}",
                                                DebugOnlyOps.FormatCertificate(localCertificate),
                                                Utility.FormatWrapOrNull(true, true, script),
                                                DebugOnlyOps.ScriptLength(script),
                                                Utility.FormatWrapOrNull(true, false, verifyResult),
                                                Utility.FormatWrapOrNull(true, false, result)),
                                                typeof(CertificatePolicyOps).Name,
                                                TracePriority.MediumHigh, 0);
#endif
                                        }
                                    }
                                    else
                                    {
                                        ResultList errors = new ResultList();

                                        if (entityTypeResult != null)
                                            errors.Add(entityTypeResult);

                                        result = errors;

#if DEBUG || FORCE_TRACE
                                        CertificateTraceOps.MaybeLogAndDebugTrace(
                                            logClientData, String.Format(
                                            "Certificate does not pass entity type check, " +
                                            "localCertificate = {0}, entityTypeResult = {1}, " +
                                            "result = {2}",
                                            DebugOnlyOps.FormatCertificate(localCertificate),
                                            Utility.FormatWrapOrNull(true, false, entityTypeResult),
                                            Utility.FormatWrapOrNull(true, false, result)),
                                            typeof(CertificatePolicyOps).Name,
                                            TracePriority.MediumHigh, 0);
#endif
                                    }
                                }
                                else
                                {
                                    result = localResult;
                                }
                            }
                            else
                            {
                                ResultList errors = new ResultList();

                                if (localResult != null)
                                    errors.Add(localResult);

                                if (extractError != null)
                                    errors.Add(extractError);

                                result = errors;

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "No script certificate was found, " +
                                    "localCertificate = {0}, script = {1}, " +
                                    "localResult = {2}, extractError = {3}, " +
                                    "result = {4}",
                                    DebugOnlyOps.FormatCertificate(localCertificate),
                                    Utility.FormatWrapOrNull(true, true, script),
                                    Utility.FormatWrapOrNull(true, false, localResult),
                                    Utility.FormatWrapOrNull(true, false, extractError),
                                    Utility.FormatWrapOrNull(true, false, result)),
                                    typeof(CertificatePolicyOps).Name,
                                    TracePriority.MediumHigh, 0);
#endif
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Verification of script failure, " +
                                "localCertificate = {0}, script = {1} ({2}), " +
                                "result = {3}",
                                DebugOnlyOps.FormatCertificate(localCertificate),
                                Utility.FormatWrapOrNull(true, true, script),
                                DebugOnlyOps.ScriptLength(script),
                                Utility.FormatWrapOrNull(true, false, result)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.MediumHigh, 0);
#endif

                            Denied(interpreter, policyContext);

                            CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                            return ReturnCode.Ok;
                        }
                        finally
                        {
                            if (policyContext != null)
                            {
                                policyContext.Trace(
                                    "ScriptPolicyCallback", TracePriority.PolicyDebug,
                                    fullTracing);
                            }

                            CertificateTraceOps.MaybeEnableOrDisableTextWriter(
                                interpreter, cultureInfo, tracePolicy, false,
                                ref wasEnabled, ref savedBasePriority,
                                ref savedPriorities1, ref savedPriorities2);
                        }
                    }
                    finally
                    {
                        Utility.MaybePopActiveLogClientData(ref pushed);
                    }
                }
                finally
                {
                    if (logClientData != null)
                    {
                        logClientData.Dispose();
                        logClientData = null;
                    }
                }
            }
            finally
            {
                CertificatePolicyState.EndPending();
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if XML && SERIALIZATION
        /// <summary>
        /// Implements the file execution policy callback, verifying the
        /// file's signature against the configured certificate and key pairs
        /// and approving or denying the policy context accordingly.
        /// </summary>
        /// <param name="policy">
        /// The local or remote execution policy in effect for this callback.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the callback.
        /// </param>
        /// <param name="hasFlags">
        /// The string describing the flags that triggered the callback.
        /// </param>
        /// <param name="certificate">
        /// The certificate used to verify the file.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the policy.
        /// </param>
        /// <param name="keyName">
        /// The key name used to locate verification keys.
        /// </param>
        /// <param name="keyRingName">
        /// The key ring name used to locate verification keys.
        /// </param>
        /// <param name="scriptFlags">
        /// The script flags associated with the policy.
        /// </param>
        /// <param name="interpreter">
        /// The optional interpreter associated with the callback (may be
        /// null).
        /// </param>
        /// <param name="renewCallback">
        /// The optional renewal callback associated with the policy.
        /// </param>
        /// <param name="clientData">
        /// The client data carrying the policy context and file content.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the callback; not currently used.
        /// </param>
        /// <param name="ignoreBasePolicy">
        /// Non-zero to perform verification even when no base policy is set.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode FileCallback( /* POLICY IMPLEMENTATION */
            ExecutionPolicy policy,      /* in: LOCAL OR REMOTE */
            PolicyType policyType,       /* in */
            string hasFlags,             /* in */
            ICertificate certificate,    /* in */
            Assembly assembly,           /* in */
            string keyName,              /* in */
            string keyRingName,          /* in */
            ScriptFlags scriptFlags,     /* in */
            Interpreter interpreter,     /* in: OPTIONAL, MAY BE NULL. */
            RenewCallback renewCallback, /* in: OPTIONAL */
            IClientData clientData,      /* in */
            ArgumentList arguments,      /* in: NOT USED */
            bool ignoreBasePolicy,       /* in */
            ref Result result            /* out */
            )
        {
            CultureInfo cultureInfo;
            bool disposed;

            /* NO RESULT */
            CertificateDataOps.SafeGetCultureInfo(
                interpreter, out cultureInfo, out disposed);

            if (disposed)
            {
                result = "interpreter is disposed";
                return ReturnCode.Error;
            }

            CertificatePolicyState.BeginPending();

            try
            {
                ILogClientData logClientData = null;

                try
                {
                    if (Configuration.DoesVariableExist(
                            Constants.ForceLogScriptEnvVarName))
                    {
                        logClientData = new ScriptLogClientData(
                            interpreter, null, null, policyType,
                            policy);
                    }

                    int pushed = 0;

                    try
                    {
                        Utility.MaybePushActiveLogClientData(
                            interpreter, logClientData, ref pushed);

                        ExecutionPolicy? tracePolicy = policy;
                        bool wasEnabled = false;
                        TracePriority? savedBasePriority = null;
                        TracePriority? savedPriorities1 = null;
                        TracePriority? savedPriorities2 = null;
                        IPolicyContext policyContext = null;

                        bool fullTracing = CertificateTraceOps.ShouldForceFullForPolicy() ||
                            Utility.HasFlags(tracePolicy, ExecutionPolicy.FullTracing, true);

                        try
                        {
                            CertificateTraceOps.MaybeChangeExecutionPolicy(
                                interpreter, Constants.ScriptExecutionPolicyEnvVarName,
                                Constants.EnablePolicyTracingLimitMask.ToString(),
                                cultureInfo, ref tracePolicy);

                            fullTracing = CertificateTraceOps.ShouldForceFullForPolicy() ||
                                Utility.HasFlags(tracePolicy, ExecutionPolicy.FullTracing, true); /* REFRESH */

                            CertificateTraceOps.MaybeEnableOrDisableTextWriter(
                                interpreter, cultureInfo, tracePolicy, true,
                                ref wasEnabled, ref savedBasePriority,
                                ref savedPriorities1, ref savedPriorities2);

                            IPlugin plugin = null;

                            if (Utility.ExtractPolicyContextAndPlugin(
                                    interpreter, clientData, ref policyContext,
                                    ref plugin, ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

                            //
                            // HACK: Update the log now using the plugin we just found.
                            //
                            if (logClientData != null)
                                logClientData.Plugin = plugin;

                            //
                            // HACK: We require the hash value from the policy engine;
                            //       therefore, monitor the "after" callbacks only.
                            //
                            PolicyFlags policyFlags = policyContext.Flags;

                            if (!Utility.HasFlags(policyFlags, PolicyFlags.AfterFile, true) &&
                                !Utility.HasFlags(policyFlags, PolicyFlags.AfterStream, true))
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Ok;
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Starting {0} policy is {1}, trace policy is {2}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(policy),
                                Utility.FormatWrapOrNull(tracePolicy)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            ExecutionPolicy localPolicy = ExecutionPolicy.Undefined;
                            ExecutionPolicy basePolicy = ExecutionPolicy.Undefined;

                            if (CheckPolicy(
                                    policyType, interpreter, plugin, ref policy,
                                    ref localPolicy, ref basePolicy, ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Final {0} policy is {1} with a local policy of {2}, a base policy of {3}, and a trace policy of {4}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(policy),
                                Utility.FormatWrapOrNull(localPolicy),
                                Utility.FormatWrapOrNull(basePolicy),
                                Utility.FormatWrapOrNull(tracePolicy)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            //
                            // NOTE: We need to bypass the the policy machinery here if there is
                            //       no explicit policy set because it validates things like the
                            //       file name being a fully qualified (i.e. it turns operations
                            //       that should succeed into errors).
                            //
                            if (!ignoreBasePolicy && !Utility.HasFlags(
                                    basePolicy, ExecutionPolicy.BasePolicyMask, false))
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Ok;
                            }

                            string fileName = null;
                            Encoding encoding = null;
                            string text = null;
                            byte[] hashValue = null;
                            ByteList bytes = null;
                            int? timeout = null;

                            if (ExtractFileContextData(
                                    interpreter, clientData, ref policyContext, ref fileName,
                                    ref timeout, ref encoding, ref text, ref hashValue, ref bytes,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

                            if (timeout == null)
                                timeout = CertificateSharedOps.GetTimeout(interpreter, null);

                            //
                            // HACK: Update the log now using the file name we just found.
                            //
                            if (logClientData != null)
                                logClientData.FileName = fileName;

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Starting {0} key name is {1} / {2}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(keyName),
                                Utility.FormatWrapOrNull(keyRingName)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            if (CheckKeyName(
                                    policyType, interpreter, plugin, ref keyName,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

                            if (CheckKeyRingName(
                                    policyType, interpreter, plugin, ref keyRingName,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Final {0} key name is {1} / {2}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(keyName),
                                Utility.FormatWrapOrNull(keyRingName)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);

                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Starting {0} script flags are {1}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(scriptFlags)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            if (CheckScriptFlags(
                                    policyType, interpreter, plugin, ref scriptFlags,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

                            NetworkFlags networkFlags = Helpers.GetNetworkFlags(
                                policyType);

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Final {0} script flags are {1}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(scriptFlags)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);

                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Starting {0} network flags are {1}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(networkFlags)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            if (CheckNetworkFlags(
                                    policyType, interpreter, plugin, ref networkFlags,
                                    ref result) != ReturnCode.Ok)
                            {
                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Final {0} network flags are {1}",
                                Utility.FormatWrapOrNull(policyType),
                                Utility.FormatWrapOrNull(networkFlags)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Lower, 0);
#endif

                            Result localResult; /* REUSED */

#if LICENSING
                            string skipReason = null;

                            localResult = null;

                            if (!CertificateKeyRingState.CanSkipPolicyFeatureChecks(
                                    ref skipReason) &&
                                CertificateSharedOps.MatchFlags(
                                    (certificate != null) ? certificate :
                                        CertificateSharedOps.GetViaPlugin(plugin),
                                    FlagType.Feature, Utility.DefaultAttributeFlagsKey(),
                                    hasFlags, null, false, false, true,
                                    ref localResult) != ReturnCode.Ok)
                            {
                                result = localResult;

                                CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                                return ReturnCode.Error;
                            }

#if DEBUG || FORCE_TRACE
                            if (!String.IsNullOrEmpty(skipReason))
                            {
                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Policy {0} feature checks skipped because {1}",
                                    Utility.FormatWrapOrNull(policyType), skipReason),
                                    typeof(CertificatePolicyOps).Name,
                                    TracePriority.Low, 0);
                            }
#endif
#endif

                            bool allowRemoteUri = Utility.HasFlags(
                                policy, ExecutionPolicy.AllowRemoteUri, true);

                            bool remoteUri;
                            Uri uri = null;

                            remoteUri = Utility.IsRemoteUri(fileName, ref uri);

                            if (remoteUri)
                            {
                                if (!allowRemoteUri)
                                {
                                    result = "file name cannot be a remote uri";

                                    return ReturnCode.Error;
                                }

#if NETWORK && TEST
                                if (Utility.SetWebSecurityProtocol(
                                        false, ref result) != ReturnCode.Ok)
                                {
                                    return ReturnCode.Error;
                                }
#endif
                            }

                            bool skipExists = Utility.HasFlags(
                                policy, ExecutionPolicy.SkipExists, true);

                            if (!skipExists && !remoteUri && !Path.IsPathRooted(fileName))
                            {
                                result = "file name must be fully qualified";

                                return ReturnCode.Error;
                            }

                            if (!skipExists && !remoteUri && !File.Exists(fileName))
                            {
                                result = "file does not exist";

                                return ReturnCode.Error;
                            }

                            if (!ignoreBasePolicy && (basePolicy == ExecutionPolicy.AllowNone))
                            {
                                result = null;

                                Denied(interpreter, policyContext);
                                return ReturnCode.Ok;
                            }

                            if (!ignoreBasePolicy && (basePolicy == ExecutionPolicy.AllowAny))
                            {
                                result = null;

                                Approved(interpreter, policyContext);
                                return ReturnCode.Ok;
                            }

                            string renewedCertificateFileName = CertificateSharedOps.GetHashFileName(
                                plugin, hashValue, false);

                            string certificateFileNameOnly;

                            string certificateFileName = CertificateDataOps.FormatFileName(
                                fileName, cultureInfo, encoding, remoteUri,
                                out certificateFileNameOnly);

                            bool certificateRemoteUri = Utility.IsRemoteUri(
                                certificateFileName); /* EXEMPT */

                            string hashCertificateFileNameOnly;

                            string hashCertificateFileName = CertificateDataOps.FormatHashFileName(
                                fileName, cultureInfo, encoding, hashValue, remoteUri,
                                out hashCertificateFileNameOnly);

                            bool hashCertificateRemoteUri = Utility.IsRemoteUri(
                                hashCertificateFileName); /* EXEMPT */

#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                logClientData, String.Format(
                                "Candidate certificate file names are: {0}, {1}, {2}, {3}, {4}",
                                Utility.FormatWrapOrNull(renewedCertificateFileName),
                                Utility.FormatWrapOrNull(certificateFileName),
                                Utility.FormatWrapOrNull(certificateFileNameOnly),
                                Utility.FormatWrapOrNull(hashCertificateFileName),
                                Utility.FormatWrapOrNull(hashCertificateFileNameOnly)),
                                typeof(CertificatePolicyOps).Name,
                                TracePriority.Low, 0);
#endif

                            //
                            // HACK: Yes, this if statement is somewhat redundant.
                            //       It checks the "skipExists" variable thrice;
                            //       however, this is to help document the intent.
                            //       We are trying to check each candidate file to
                            //       see if it exists, unless "skipExists" is true
                            //       -OR- the candidate file is actually a remote
                            //       URI.
                            //
                            if ((skipExists ||
                                    File.Exists(renewedCertificateFileName)) ||
                                (skipExists || certificateRemoteUri ||
                                    File.Exists(certificateFileName)) ||
                                (skipExists || hashCertificateRemoteUri ||
                                    File.Exists(hashCertificateFileName)))
                            {
                                bool validateXml = Utility.HasFlags(
                                    policy, ExecutionPolicy.ValidateXml, true);

                                bool skipFile = Utility.HasFlags(
                                    policy, ExecutionPolicy.SkipFile, true);

                                bool skipHost = Utility.HasFlags(
                                    policy, ExecutionPolicy.SkipHost, true);

                                bool skipRenewedName = Utility.HasFlags(
                                    policy, ExecutionPolicy.SkipRenewedName, true);

                                bool skipHashName = Utility.HasFlags(
                                    policy, ExecutionPolicy.SkipHashName, true);

                                bool skipPlainName = Utility.HasFlags(
                                    policy, ExecutionPolicy.SkipPlainName, true);

                                bool allowEmbedded = Utility.HasFlags(
                                    policy, ExecutionPolicy.AllowEmbedded, true);

                                bool anyResourcePublicKey = Utility.HasFlags(
                                    policy, ExecutionPolicy.AnyResourcePublicKey, true);

                                ICertificate renewedCertificate = null;
                                Result renewedFileResult = null;
                                Result renewedHostResult = null;

                                ICertificate localCertificate = null;

                                Result fileResult = null;
                                Result hostResult = null;
                                Result hostOnlyResult = null;

                                Result hashFileResult = null;
                                Result hashHostResult = null;
                                Result hashHostOnlyResult = null;

                                ICertificate extractCertificate = null;
                                string extractText = text;
                                Result extractError = null;

                                if ((!skipFile && !skipRenewedName &&
                                    (CertificateXmlOps.Import(
                                        renewedCertificateFileName, anyResourcePublicKey,
                                        false, validateXml, ref renewedCertificate,
                                        ref renewedFileResult) == ReturnCode.Ok)) ||
                                    (!skipFile && !skipHashName &&
                                    (CertificateXmlOps.Import(
                                        hashCertificateFileName, anyResourcePublicKey,
                                        false, validateXml, ref localCertificate,
                                        ref hashFileResult) == ReturnCode.Ok)) ||
                                    (!skipFile && !skipPlainName &&
                                    (CertificateXmlOps.Import(
                                        certificateFileName, anyResourcePublicKey,
                                        false, validateXml, ref localCertificate,
                                        ref fileResult) == ReturnCode.Ok)) ||
                                    (!skipHost && !skipRenewedName &&
                                    (CertificateXmlOps.ImportFromHost(
                                        interpreter, renewedCertificateFileName,
                                        scriptFlags, validateXml,
                                        ref renewedCertificate,
                                        ref renewedHostResult) == ReturnCode.Ok)) ||
                                    (!skipHost && !skipHashName &&
                                    ((CertificateXmlOps.ImportFromHost(
                                        interpreter, hashCertificateFileName,
                                        scriptFlags, validateXml,
                                        ref localCertificate,
                                        ref hashHostResult) == ReturnCode.Ok) ||
                                    ((hashCertificateFileNameOnly != null) &&
                                    (CertificateXmlOps.ImportFromHost(
                                        interpreter, hashCertificateFileNameOnly,
                                        scriptFlags, validateXml,
                                        ref localCertificate,
                                        ref hashHostOnlyResult) == ReturnCode.Ok)))) ||
                                    (!skipHost && !skipPlainName &&
                                    ((CertificateXmlOps.ImportFromHost(
                                        interpreter, certificateFileName,
                                        scriptFlags, validateXml,
                                        ref localCertificate,
                                        ref hostResult) == ReturnCode.Ok) ||
                                    ((certificateFileNameOnly != null) &&
                                    (CertificateXmlOps.ImportFromHost(
                                        interpreter, certificateFileNameOnly,
                                        scriptFlags, validateXml,
                                        ref localCertificate,
                                        ref hostOnlyResult) == ReturnCode.Ok)))) ||
                                    (allowEmbedded &&
                                    (CertificateXmlOps.Extract(
                                        fileName, validateXml, ref extractText,
                                        ref extractCertificate,
                                        ref extractError) == ReturnCode.Ok)))
                                {
                                    CertificateHashFlags? certificateHashFlags = null;
                                    bool extracted = false;

                                    if (extractCertificate != null)
                                    {
                                        extractCertificate.EntityValue = extractText;
                                        localCertificate = extractCertificate;
                                        certificateHashFlags = CertificateHashFlags.Embedded;
                                        extracted = true;
                                    }
                                    else if (allowEmbedded && (renewedCertificate != null))
                                    {
                                        Result discardError = null;

                                        if (CertificateXmlOps.Discard(
                                                fileName, ref extractText,
                                                ref discardError) == ReturnCode.Ok)
                                        {
                                            //
                                            // HACK: In this case, it is likely that a script
                                            //       with an embedded certificate was renewed
                                            //       and now the external script certificate
                                            //       is being used; however, it must still be
                                            //       treated exactly as-if the embedded script
                                            //       certificate was extracted and used.
                                            //
                                            renewedCertificate.EntityValue = extractText;
                                            localCertificate = renewedCertificate;
                                            certificateHashFlags = CertificateHashFlags.Embedded;
                                            extracted = true;

#if DEBUG || FORCE_TRACE
                                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                                logClientData, String.Format(
                                                "Discard of embedded certificate success, " +
                                                "fileName = {0}, discardError = {1}",
                                                Utility.FormatWrapOrNull(fileName),
                                                Utility.FormatWrapOrNull(true, false, discardError)),
                                                typeof(CertificatePolicyOps).Name,
                                                TracePriority.MediumLow, 0);
#endif
                                        }
                                        else
                                        {
#if DEBUG || FORCE_TRACE
                                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                                logClientData, String.Format(
                                                "Discard of embedded certificate failure, " +
                                                "fileName = {0}, discardError = {1}",
                                                Utility.FormatWrapOrNull(fileName),
                                                Utility.FormatWrapOrNull(true, false, discardError)),
                                                typeof(CertificatePolicyOps).Name,
                                                TracePriority.MediumHigh, 0);
#endif
                                        }
                                    }

                                    Certificate.MaybeAdjustHashFlagsForAuthority(
                                        localCertificate, ref certificateHashFlags);

                                    ///////////////////////////////////////////////////////////////////////

                                    if (localCertificate != null)
                                    {
                                        //
                                        // NOTE: Only scripts included with Eagle Enterprise Edition itself
                                        //       can ever be signed with the assembly signing key.  This legacy
                                        //       usage for the assembly signing key being phased out.  In the
                                        //       future, it will only be used to sign license certificates as
                                        //       well as the assembly itself.
                                        //
                                        bool allowAssemblyPublicKey = Utility.HasFlags(
                                            policy, ExecutionPolicy.AllowAssemblyPublicKey, true);

                                        ///////////////////////////////////////////////////////////////////////

                                        bool allowEmbeddedPublicKey = Utility.HasFlags(
                                            policy, ExecutionPolicy.AllowEmbeddedPublicKey, true);

                                        bool allowRingPublicKey = Utility.HasFlags(
                                            policy, ExecutionPolicy.AllowRingPublicKey, true);

                                        bool allowAnyPublicKey = Utility.HasFlags(
                                            policy, ExecutionPolicy.AllowAnyPublicKey, true);

                                        bool enforceKeyUsage = Utility.HasFlags(
                                            policy, ExecutionPolicy.EnforceKeyUsage, true);

                                        AssemblyName assemblyName = (assembly != null) ?
                                            assembly.GetName() : null;

                                        IEnumerable<IKeyPair> keyPairs = null;

                                        localResult = null;

                                        if (CertificateKeyPairOps.GetPublicOnly( /* OK */
                                                keyRingName, PolicyType.Script, false,
                                                assembly, assemblyName, null, keyName, false,
                                                interpreter, localCertificate.EntityType,
                                                allowAssemblyPublicKey, allowEmbeddedPublicKey,
                                                allowRingPublicKey, !allowAnyPublicKey,
                                                enforceKeyUsage, ref keyPairs,
                                                ref localResult) == ReturnCode.Ok)
                                        {
                                            bool matchSubject = Utility.HasFlags(
                                                policy, ExecutionPolicy.MatchSubject, true);

                                            bool checkEntityType = Utility.HasFlags(
                                                policy, ExecutionPolicy.CheckEntityType, true);

                                            bool verifyString = Utility.HasFlags(
                                                policy, ExecutionPolicy.VerifyString, true);

                                            bool verifyFile = Utility.HasFlags(
                                                policy, ExecutionPolicy.VerifyFile, true);

                                            bool checkPublicKeyToken = Utility.HasFlags(
                                                policy, ExecutionPolicy.CheckPublicKeyToken, true);

                                            bool checkRevocation = Utility.HasFlags(
                                                policy, ExecutionPolicy.CheckRevocation, true);

                                            bool checkDomains = Utility.HasFlags(
                                                policy, ExecutionPolicy.CheckDomains, true);

                                            bool checkQuantity = Utility.HasFlags(
                                                policy, ExecutionPolicy.CheckQuantity, true);

                                            bool protectQuantity = Utility.HasFlags(
                                                policy, ExecutionPolicy.ProtectQuantity, true);

                                            bool perMachine = Utility.HasFlags(
                                                policy, ExecutionPolicy.PerMachine, true);

                                            bool checkExpiry = Utility.HasFlags(
                                                policy, ExecutionPolicy.CheckExpiry, true);

                                            bool saveApprovedData = Utility.HasFlags(
                                                policy, ExecutionPolicy.SaveApprovedData, true);

                                            //
                                            // NOTE: Initially, no renewal has been performed.
                                            //       This flag will be set upon renewal being
                                            //       completed successfully -AND- before the
                                            //       script certificate is checked again by
                                            //       jumping to the "retry" label.
                                            //
                                            bool wasRenewed = false;

                                            //
                                            // NOTE: Initially, there is no primary key pair
                                            //       used to verify the certificate and/or
                                            //       returned from the renewal processing.
                                            //
                                            IKeyPair keyPair = null;

                                        retry:

                                            Result localSubjectResult = null;
                                            Result subjectResult = null;

                                            if (!matchSubject ||
                                                (CertificateSharedOps.MatchSubject(
                                                    assembly, localCertificate, policy,
                                                    ref localSubjectResult) == ReturnCode.Ok) ||
                                                (CertificateSharedOps.MatchSubject(
                                                    assembly, certificate, policy,
                                                    ref subjectResult) == ReturnCode.Ok))
                                            {
                                                EntityType entityType = EntityType.None;
                                                Result entityTypeResult = null;

                                                if (!checkEntityType || (VerifyEntityType(
                                                        interpreter, localCertificate, ref entityType,
                                                        ref entityTypeResult) == ReturnCode.Ok))
                                                {
                                                    if (verifyString || verifyFile)
                                                    {
                                                        IEnumerable<IKeyPair> verifyKeyPairs = (keyPair != null) ?
                                                            new IKeyPair[] { keyPair } : keyPairs;

#if DEBUG || FORCE_TRACE
                                                        DebugOnlyOps.DumpKeyPairs(
                                                            interpreter, logClientData, "FileCallback", null,
                                                            verifyKeyPairs, typeof(CertificatePolicyOps).Name,
                                                            policyType, TracePriority.MediumLow);
#endif

                                                        string localHashAlgorithmName = GetHashAlgorithm(
                                                            verifyKeyPairs, localCertificate, HashAlgorithmType.Legacy);

                                                        //
                                                        // NOTE: If the certificate was extracted from the text,
                                                        //       then there probably is not an external file to
                                                        //       verify against at this point.  Further, even
                                                        //       if there was an external file, we already have
                                                        //       certificate metadata to verify against and we
                                                        //       do not (currently) attempt to verify more than
                                                        //       one certificate per policy operation.
                                                        //
                                                        IKeyPair verifyStringKeyPair = null;
                                                        IKeyPair verifyFileKeyPair = null;
                                                        ReturnCode verifyStringCode = ReturnCode.Ok;
                                                        ReturnCode verifyFileCode = ReturnCode.Ok;
                                                        Result verifyStringResult = null;
                                                        Result verifyFileResult = null;
                                                        bool didVerifyString = false;
                                                        bool didVerifyFile = false;

                                                        if (verifyString && (extracted || (text != null)))
                                                        {
                                                            IEnumerable<IKeyPair> verifyStringKeyPairs = null;

                                                            if (keyPair != null)
                                                            {
                                                                verifyStringKeyPairs = CertificateKeyPairOps.MergeAll(
                                                                    interpreter, keyPairs, null, keyPair, null, null,
                                                                    null, null, PolicyType.Script, null, false, false,
                                                                    false);
                                                            }
                                                            else
                                                            {
                                                                verifyStringKeyPairs = keyPairs;
                                                            }

                                                            verifyStringCode = VerifyStringAndBytes(
                                                                localHashAlgorithmName, null, localCertificate,
                                                                certificateHashFlags, encoding, verifyStringKeyPairs,
                                                                extracted ? null : text, bytes,
                                                                checkPublicKeyToken, checkRevocation,
                                                                ref verifyStringKeyPair, ref verifyStringResult);

                                                            didVerifyString = true;
                                                        }

                                                        if (verifyFile && !extracted)
                                                        {
                                                            IEnumerable<IKeyPair> verifyFileKeyPairs = null;

                                                            if (keyPair != null)
                                                            {
                                                                verifyFileKeyPairs = CertificateKeyPairOps.MergeAll(
                                                                    interpreter, keyPairs, null, keyPair, null, null,
                                                                    null, null, PolicyType.Script, null, false, false,
                                                                    false);
                                                            }
                                                            else
                                                            {
                                                                verifyFileKeyPairs = keyPairs;
                                                            }

                                                            verifyFileCode = VerifyFile(
                                                                localHashAlgorithmName, null, localCertificate,
                                                                null, encoding, verifyFileKeyPairs, fileName,
                                                                null, checkPublicKeyToken, checkRevocation,
                                                                ref verifyFileKeyPair, ref verifyFileResult);

                                                            didVerifyFile = true;
                                                        }

                                                        if ((didVerifyString || didVerifyFile) &&
                                                            (verifyStringCode == ReturnCode.Ok) &&
                                                            (verifyFileCode == ReturnCode.Ok))
                                                        {
                                                            Result requirementResult = null;
                                                            Result processResult = null;

                                                            if ((CertificateVerifyOps.CheckForRequirement(
                                                                    interpreter, localCertificate, null, cultureInfo,
                                                                    ref requirementResult) == ReturnCode.Ok) &&
                                                                (CertificateVerifyOps.CheckForProcess(
                                                                    interpreter, localCertificate, null, cultureInfo,
                                                                    ref processResult) == ReturnCode.Ok))
                                                            {
                                                                //
                                                                // HACK: When the global "force network" flag is set for
                                                                //       policy checking, all revocation and expiration
                                                                //       checks will require network access.
                                                                //
                                                                if (CertificatePolicyState.GetForceNetwork())
                                                                    networkFlags |= NetworkFlags.ForceMask;

                                                                //
                                                                // HACK: Maybe invoke the fail-safe checking, which will
                                                                //       perform an asynchronous forced remote check to
                                                                //       determine if the certificate -OR- its signing
                                                                //       key pair has been actively revoked.
                                                                //
                                                                CertificateRevocationOps.MaybePerformFailSafeChecks( /* OK */
                                                                    interpreter, assembly, plugin, localHashAlgorithmName,
                                                                    null, encoding, keyPairs, localCertificate,
                                                                    verifyStringKeyPair, cultureInfo, Utility.GetUtcNow(),
                                                                    timeout, networkFlags);

                                                                CertificateRevocationOps.MaybePerformFailSafeChecks( /* OK */
                                                                    interpreter, assembly, plugin, localHashAlgorithmName,
                                                                    null, encoding, keyPairs, localCertificate,
                                                                    verifyFileKeyPair, cultureInfo, Utility.GetUtcNow(),
                                                                    timeout, networkFlags);

                                                                Result revocationResult = null;

                                                                if (!checkRevocation ||
                                                                    (CertificateRevocationOps.IsRevoked( /* OK */
                                                                        interpreter, assembly, plugin,
                                                                        localHashAlgorithmName, null,
                                                                        encoding, keyPairs, localCertificate,
                                                                        cultureInfo, timeout, networkFlags,
                                                                        ref revocationResult) == ReturnCode.Ok))
                                                                {
                                                                    Result domainStringResult = null;
                                                                    Result domainFileResult = null;

                                                                    if (!checkDomains ||
                                                                        ((!didVerifyString || (CheckKeyDomains(
                                                                            verifyStringKeyPair, uri, cultureInfo,
                                                                            ref domainStringResult) == ReturnCode.Ok)) &&
                                                                        (!didVerifyFile || (CheckKeyDomains(
                                                                            verifyFileKeyPair, uri, cultureInfo,
                                                                            ref domainFileResult) == ReturnCode.Ok))))
                                                                    {
                                                                        Result quantityResult = null;

                                                                        if (wasRenewed || !checkQuantity ||
                                                                            (CertificateSharedOps.ProcessQuantity(
                                                                                interpreter, plugin, localHashAlgorithmName,
                                                                                null, localCertificate, cultureInfo, null,
                                                                                encoding, null, protectQuantity, perMachine,
                                                                                ref quantityResult) == ReturnCode.Ok))
                                                                        {
                                                                            //
                                                                            // HACK: Which of key pair are we most concerned with?
                                                                            //
                                                                            IKeyPair verifyKeyPair = SelectPrimaryKeyPair(
                                                                                verifyStringKeyPair, verifyFileKeyPair);

                                                                            //
                                                                            // NOTE: Attempt to deduce the "primary" key pair
                                                                            //       involved in verifying the script file in
                                                                            //       question.  The one used to verify a file
                                                                            //       on disk will take priority in this case.
                                                                            //       This is skipped if we got to this point
                                                                            //       via the renewal processing.
                                                                            //
                                                                            if (keyPair == null)
                                                                                keyPair = verifyKeyPair;

                                                                            bool canRenew = true;
                                                                            Result activationResult = null;
                                                                            Result expiredResult = null;

                                                                            if (!CertificateSharedOps.NeedsActivation(
                                                                                    localCertificate, ref activationResult) &&
                                                                                (!checkExpiry || (CertificateSharedOps.IsExpired(
                                                                                    interpreter, assembly, plugin, localCertificate,
                                                                                    keyPairs, keyPair, cultureInfo, null, timeout,
                                                                                    policyType, (wasRenewed ?
                                                                                        NetworkFlags.ViaRenewal :
                                                                                        NetworkFlags.None) | networkFlags,
                                                                                    ref canRenew,
                                                                                    ref expiredResult) == ReturnCode.Ok)))
                                                                            {
                                                                                result = null;

                                                                                Approved(interpreter, policyContext);

                                                                                if (saveApprovedData)
                                                                                {
                                                                                    //
                                                                                    // BUGFIX: Use the potentially new key pair from
                                                                                    //         the certificate renewal.
                                                                                    //
                                                                                    bool isKeyRing = CertificateSharedOps.HasFlags(
                                                                                        entityType, EntityType.KeyRing, true);

                                                                                    if ((hashValue != null) && (keyPair != null))
                                                                                    {
                                                                                        /* IGNORED */
                                                                                        CertificateKeyPairState.AddApproved(
                                                                                            interpreter, hashValue, keyPair,
                                                                                            !isKeyRing);
                                                                                    }

                                                                                    //
                                                                                    // HACK: Since the "approved key pair" data is only
                                                                                    //       used for key ring integration, only record
                                                                                    //       the file policy result for now (i.e. since
                                                                                    //       key rings always originate from a file).
                                                                                    //
                                                                                    // if ((text != null) &&
                                                                                    //     (verifyFileKeyPair != null))
                                                                                    // {
                                                                                    //     CertificateKeyPairState.AddApproved(
                                                                                    //         interpreter, text, verifyFileKeyPair,
                                                                                    //         !isKeyRing);
                                                                                    // }
                                                                                    //
                                                                                    // if ((text != null) &&
                                                                                    //     (verifyStringKeyPair != null))
                                                                                    // {
                                                                                    //     CertificateKeyPairState.AddApproved(
                                                                                    //         interpreter, text, verifyStringKeyPair,
                                                                                    //         !isKeyRing);
                                                                                    // }
                                                                                }

#if DEBUG || FORCE_TRACE
                                                                                if (verifyString && didVerifyString)
                                                                                {
                                                                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                                        logClientData, String.Format(
                                                                                        "Verification of string success, " +
                                                                                        "localCertificate = {0}, text = {1} ({2}), " +
                                                                                        "fileName = {3}, " +
                                                                                        "renewedCertificateFileName = {4}," +
                                                                                        "certificateFileName = {5}, " +
                                                                                        "hashCertificateFileName = {6}, " +
                                                                                        "renewedFileResult = {7}, hashFileResult = {8}, " +
                                                                                        "fileResult = {9}, renewedHostResult = {10}, " +
                                                                                        "hashHostResult = {11}, hashHostOnlyResult = {12}, " +
                                                                                        "hostResult = {13}, hostOnlyResult = {14}, " +
                                                                                        "extractError = {15}, verifyStringResult = {16}, " +
                                                                                        "verifyFileResult = {17}, didVerifyString = {18}, " +
                                                                                        "didVerifyFile = {19}, verifyStringCode = {20}, " +
                                                                                        "result = {21}",
                                                                                        DebugOnlyOps.FormatCertificate(localCertificate),
                                                                                        Utility.FormatWrapOrNull(true, true, text),
                                                                                        (text != null) ? text.Length : Length.Invalid,
                                                                                        Utility.FormatWrapOrNull(fileName),
                                                                                        Utility.FormatWrapOrNull(renewedCertificateFileName),
                                                                                        Utility.FormatWrapOrNull(certificateFileName),
                                                                                        Utility.FormatWrapOrNull(hashCertificateFileName),
                                                                                        Utility.FormatWrapOrNull(true, false, renewedFileResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hashFileResult),
                                                                                        Utility.FormatWrapOrNull(true, false, fileResult),
                                                                                        Utility.FormatWrapOrNull(true, false, renewedHostResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hashHostResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hashHostOnlyResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hostResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hostOnlyResult),
                                                                                        Utility.FormatWrapOrNull(true, false, extractError),
                                                                                        Utility.FormatWrapOrNull(true, false, verifyStringResult),
                                                                                        Utility.FormatWrapOrNull(true, false, verifyFileResult),
                                                                                        didVerifyString, didVerifyFile, verifyStringCode,
                                                                                        Utility.FormatWrapOrNull(true, false, result)),
                                                                                        typeof(CertificatePolicyOps).Name,
                                                                                        TracePriority.MediumLow, 0);
                                                                                }

                                                                                if (verifyFile && didVerifyFile)
                                                                                {
                                                                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                                        logClientData, String.Format(
                                                                                        "Verification of file success, " +
                                                                                        "localCertificate = {0}, text = {1} ({2}), " +
                                                                                        "fileName = {3}, " +
                                                                                        "renewedCertificateFileName = {4}, " +
                                                                                        "certificateFileName = {5}, " +
                                                                                        "hashCertificateFileName = {6}, " +
                                                                                        "renewedFileResult = {7}, hashFileResult = {8}, " +
                                                                                        "fileResult = {9}, renewedHostResult = {10}, " +
                                                                                        "hashHostResult = {11}, hashHostOnlyResult = {12}, " +
                                                                                        "hostResult = {13}, hostOnlyResult = {14}, " +
                                                                                        "extractError = {15}, verifyStringResult = {16}, " +
                                                                                        "verifyFileResult = {17}, didVerifyString = {18}, " +
                                                                                        "didVerifyFile = {19}, verifyFileCode = {20}, " +
                                                                                        "result = {21}",
                                                                                        DebugOnlyOps.FormatCertificate(localCertificate),
                                                                                        Utility.FormatWrapOrNull(true, true, text),
                                                                                        (text != null) ? text.Length : Length.Invalid,
                                                                                        Utility.FormatWrapOrNull(fileName),
                                                                                        Utility.FormatWrapOrNull(renewedCertificateFileName),
                                                                                        Utility.FormatWrapOrNull(certificateFileName),
                                                                                        Utility.FormatWrapOrNull(hashCertificateFileName),
                                                                                        Utility.FormatWrapOrNull(true, false, renewedFileResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hashFileResult),
                                                                                        Utility.FormatWrapOrNull(true, false, fileResult),
                                                                                        Utility.FormatWrapOrNull(true, false, renewedHostResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hashHostResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hashHostOnlyResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hostResult),
                                                                                        Utility.FormatWrapOrNull(true, false, hostOnlyResult),
                                                                                        Utility.FormatWrapOrNull(true, false, extractError),
                                                                                        Utility.FormatWrapOrNull(true, false, verifyStringResult),
                                                                                        Utility.FormatWrapOrNull(true, false, verifyFileResult),
                                                                                        didVerifyString, didVerifyFile, verifyFileCode,
                                                                                        Utility.FormatWrapOrNull(true, false, result)),
                                                                                        typeof(CertificatePolicyOps).Name,
                                                                                        TracePriority.MediumLow, 0);
                                                                                }
#endif

                                                                                return ReturnCode.Ok;
                                                                            }
                                                                            else if (canRenew && !wasRenewed && (renewCallback != null))
                                                                            {
                                                                                byte[] oldPublicKeyToken = Certificate.MaybeCopyKey(
                                                                                    localCertificate);

                                                                                string localFileName = fileName; /* IGNORED */
                                                                                Result renewResult = null;

                                                                                if (renewCallback(
                                                                                        interpreter, assembly, assemblyName,
                                                                                        plugin, localHashAlgorithmName, null,
                                                                                        hashValue, encoding, keyPairs,
                                                                                        new AnyClientData(clientData, false),
                                                                                        hasFlags, null, policy, policyType,
                                                                                        keyName, keyRingName, timeout,
                                                                                        allowEmbedded, validateXml,
                                                                                        ref localFileName, ref localCertificate,
                                                                                        ref renewResult) == ReturnCode.Ok)
                                                                                {
                                                                                    byte[] newPublicKeyToken = Certificate.MaybeCopyKey(
                                                                                        localCertificate);

                                                                                    if (CertificateDataOps.MatchPublicKeyToken(
                                                                                            newPublicKeyToken, oldPublicKeyToken))
                                                                                    {
                                                                                        wasRenewed = true;
                                                                                        goto retry;
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        //
                                                                                        // NOTE: If the new public key token does not match
                                                                                        //       the old one, first check if the new public
                                                                                        //       key token is present in the (filtered) list
                                                                                        //       of key pairs.
                                                                                        //
                                                                                        keyPair = CertificateSharedOps.GetKeyPairByPublicKeyToken(
                                                                                            keyPairs, newPublicKeyToken);

                                                                                        if (keyPair != null)
                                                                                        {
                                                                                            wasRenewed = true;
                                                                                            goto retry;
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                            //
                                                                                            // NOTE: Next, see if the trusted key ring for
                                                                                            //       the interpreter has been updated with
                                                                                            //       the new key pair.  This requires the
                                                                                            //       associated execution policy flag to be
                                                                                            //       enabled.
                                                                                            //
                                                                                            Result keyRingError = null;

                                                                                            if (allowRingPublicKey)
                                                                                            {
                                                                                                keyPair = CertificateKeyRingOps.GetKeyPair(
                                                                                                    interpreter, keyRingName, PolicyType.Script,
                                                                                                    newPublicKeyToken, ref keyRingError);

                                                                                                if (keyPair != null)
                                                                                                {
                                                                                                    wasRenewed = true;
                                                                                                    goto retry;
                                                                                                }
                                                                                            }

                                                                                            ResultList errors = new ResultList();

                                                                                            if (activationResult != null)
                                                                                                errors.Add(activationResult);

                                                                                            if (expiredResult != null)
                                                                                                errors.Add(expiredResult);

                                                                                            if (renewResult != null)
                                                                                                errors.Add(renewResult);

                                                                                            if (keyRingError != null)
                                                                                                errors.Add(keyRingError);

                                                                                            errors.Add("renewal public key token is not present");
                                                                                            result = errors;

#if DEBUG || FORCE_TRACE
                                                                                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                                                logClientData, String.Format(
                                                                                                "Certificate renewal public key token is not present, " +
                                                                                                "localCertificate = {0}, activationResult = {1}, " +
                                                                                                "expiredResult = {2}, renewResult = {3}, " +
                                                                                                "result = {4}",
                                                                                                DebugOnlyOps.FormatCertificate(localCertificate),
                                                                                                Utility.FormatWrapOrNull(true, false, activationResult),
                                                                                                Utility.FormatWrapOrNull(true, false, expiredResult),
                                                                                                Utility.FormatWrapOrNull(true, false, renewResult),
                                                                                                Utility.FormatWrapOrNull(true, false, result)),
                                                                                                typeof(CertificatePolicyOps).Name,
                                                                                                TracePriority.MediumHigh, 0);
#endif
                                                                                        }
                                                                                    }
                                                                                }
                                                                                else
                                                                                {
                                                                                    ResultList errors = new ResultList();

                                                                                    if (activationResult != null)
                                                                                        errors.Add(activationResult);

                                                                                    if (expiredResult != null)
                                                                                        errors.Add(expiredResult);

                                                                                    if (renewResult != null)
                                                                                        errors.Add(renewResult);

                                                                                    result = errors;

#if DEBUG || FORCE_TRACE
                                                                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                                        logClientData, String.Format(
                                                                                        "Certificate does not pass renewal check, " +
                                                                                        "localCertificate = {0}, activationResult = {1}, " +
                                                                                        "expiredResult = {2}, renewResult = {3}, " +
                                                                                        "result = {4}",
                                                                                        DebugOnlyOps.FormatCertificate(localCertificate),
                                                                                        Utility.FormatWrapOrNull(true, false, activationResult),
                                                                                        Utility.FormatWrapOrNull(true, false, expiredResult),
                                                                                        Utility.FormatWrapOrNull(true, false, renewResult),
                                                                                        Utility.FormatWrapOrNull(true, false, result)),
                                                                                        typeof(CertificatePolicyOps).Name,
                                                                                        TracePriority.MediumHigh, 0);
#endif
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                ResultList errors = new ResultList();

                                                                                if (activationResult != null)
                                                                                    errors.Add(activationResult);

                                                                                if (expiredResult != null)
                                                                                    errors.Add(expiredResult);

                                                                                result = errors;

#if DEBUG || FORCE_TRACE
                                                                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                                    logClientData, String.Format(
                                                                                    "Certificate does not pass expiration check, " +
                                                                                    "localCertificate = {0}, activationResult = {1}, " +
                                                                                    "expiredResult = {2}, result = {3}",
                                                                                    DebugOnlyOps.FormatCertificate(localCertificate),
                                                                                    Utility.FormatWrapOrNull(true, false, activationResult),
                                                                                    Utility.FormatWrapOrNull(true, false, expiredResult),
                                                                                    Utility.FormatWrapOrNull(true, false, result)),
                                                                                    typeof(CertificatePolicyOps).Name,
                                                                                    TracePriority.MediumHigh, 0);
#endif
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            ResultList errors = new ResultList();

                                                                            if (quantityResult != null)
                                                                                errors.Add(quantityResult);

                                                                            result = errors;

#if DEBUG || FORCE_TRACE
                                                                            CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                                logClientData, String.Format(
                                                                                "Certificate does not pass quantity check, " +
                                                                                "localCertificate = {0}, quantityResult = {1}, " +
                                                                                "result = {2}",
                                                                                DebugOnlyOps.FormatCertificate(localCertificate),
                                                                                Utility.FormatWrapOrNull(true, false, quantityResult),
                                                                                Utility.FormatWrapOrNull(true, false, result)),
                                                                                typeof(CertificatePolicyOps).Name,
                                                                                TracePriority.MediumHigh, 0);
#endif
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        ResultList errors = new ResultList();

                                                                        if (domainStringResult != null)
                                                                            errors.Add(domainStringResult);

                                                                        if (domainFileResult != null)
                                                                            errors.Add(domainFileResult);

                                                                        result = errors;

#if DEBUG || FORCE_TRACE
                                                                        CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                            logClientData, String.Format(
                                                                            "Certificate does not pass key domain check, " +
                                                                            "localCertificate = {0}, domainStringResult = {1}, " +
                                                                            "domainFileResult = {2}, result = {3}",
                                                                            DebugOnlyOps.FormatCertificate(localCertificate),
                                                                            Utility.FormatWrapOrNull(true, false, domainStringResult),
                                                                            Utility.FormatWrapOrNull(true, false, domainFileResult),
                                                                            Utility.FormatWrapOrNull(true, false, result)),
                                                                            typeof(CertificatePolicyOps).Name,
                                                                            TracePriority.MediumHigh, 0);
#endif
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    ResultList errors = new ResultList();

                                                                    if (revocationResult != null)
                                                                        errors.Add(revocationResult);

                                                                    result = errors;

#if DEBUG || FORCE_TRACE
                                                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                        logClientData, String.Format(
                                                                        "Certificate does not pass revocation check, " +
                                                                        "localCertificate = {0}, revocationResult = {1}, " +
                                                                        "result = {2}",
                                                                        DebugOnlyOps.FormatCertificate(localCertificate),
                                                                        Utility.FormatWrapOrNull(true, false, revocationResult),
                                                                        Utility.FormatWrapOrNull(true, false, result)),
                                                                        typeof(CertificatePolicyOps).Name,
                                                                        TracePriority.MediumHigh, 0);
#endif
                                                                }
                                                            }
                                                            else
                                                            {
                                                                ResultList errors = new ResultList();

                                                                if (requirementResult != null)
                                                                    errors.Add(requirementResult);

                                                                if (processResult != null)
                                                                    errors.Add(processResult);

                                                                result = errors;

#if DEBUG || FORCE_TRACE
                                                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                    logClientData, String.Format(
                                                                    "Certificate does not pass requirement check, " +
                                                                    "localCertificate = {0}, requirementResult = {1}, " +
                                                                    "processResult = {2}, result = {3}",
                                                                    DebugOnlyOps.FormatCertificate(localCertificate),
                                                                    Utility.FormatWrapOrNull(true, false, requirementResult),
                                                                    Utility.FormatWrapOrNull(true, false, processResult),
                                                                    Utility.FormatWrapOrNull(true, false, result)),
                                                                    typeof(CertificatePolicyOps).Name,
                                                                    TracePriority.MediumHigh, 0);
#endif
                                                            }
                                                        }
                                                        else
                                                        {
                                                            ResultList errors = new ResultList();

                                                            if (verifyStringResult != null)
                                                                errors.Add(verifyStringResult);

                                                            if (verifyFileResult != null)
                                                                errors.Add(verifyFileResult);

                                                            if (!didVerifyString && !didVerifyFile)
                                                                errors.Add("did not verify string and/or file");

                                                            result = errors;

#if DEBUG || FORCE_TRACE
                                                            if (verifyString && didVerifyString &
                                                                (verifyStringCode != ReturnCode.Ok))
                                                            {
                                                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                    logClientData, String.Format(
                                                                    "Verification of string failure, " +
                                                                    "localCertificate = {0}, text = {1} ({2}), " +
                                                                    "fileName = {3}, " +
                                                                    "renewedCertificateFileName = {4}, " +
                                                                    "certificateFileName = {5}, " +
                                                                    "hashCertificateFileName = {6}, " +
                                                                    "renewedFileResult = {7}, hashFileResult = {8}, " +
                                                                    "fileResult = {9}, renewedHostResult = {10}, " +
                                                                    "hashHostResult = {11}, hashHostOnlyResult = {12}, " +
                                                                    "hostResult = {13}, hostOnlyResult = {14}, " +
                                                                    "extractError = {15}, verifyStringResult = {16}, " +
                                                                    "verifyFileResult = {17}, didVerifyString = {18}, " +
                                                                    "didVerifyFile = {19}, verifyStringCode = {20}, " +
                                                                    "result = {21}",
                                                                    DebugOnlyOps.FormatCertificate(localCertificate),
                                                                    Utility.FormatWrapOrNull(true, true, text),
                                                                    (text != null) ? text.Length : Length.Invalid,
                                                                    Utility.FormatWrapOrNull(fileName),
                                                                    Utility.FormatWrapOrNull(renewedCertificateFileName),
                                                                    Utility.FormatWrapOrNull(certificateFileName),
                                                                    Utility.FormatWrapOrNull(hashCertificateFileName),
                                                                    Utility.FormatWrapOrNull(true, false, renewedFileResult),
                                                                    Utility.FormatWrapOrNull(true, false, hashFileResult),
                                                                    Utility.FormatWrapOrNull(true, false, fileResult),
                                                                    Utility.FormatWrapOrNull(true, false, renewedHostResult),
                                                                    Utility.FormatWrapOrNull(true, false, hashHostResult),
                                                                    Utility.FormatWrapOrNull(true, false, hashHostOnlyResult),
                                                                    Utility.FormatWrapOrNull(true, false, hostResult),
                                                                    Utility.FormatWrapOrNull(true, false, hostOnlyResult),
                                                                    Utility.FormatWrapOrNull(true, false, extractError),
                                                                    Utility.FormatWrapOrNull(true, false, verifyStringResult),
                                                                    Utility.FormatWrapOrNull(true, false, verifyFileResult),
                                                                    didVerifyString, didVerifyFile, verifyStringCode,
                                                                    Utility.FormatWrapOrNull(true, false, result)),
                                                                    typeof(CertificatePolicyOps).Name,
                                                                    TracePriority.MediumHigh, 0);
                                                            }

                                                            if (verifyFile && didVerifyFile &
                                                                (verifyFileCode != ReturnCode.Ok))
                                                            {
                                                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                                                    logClientData, String.Format(
                                                                    "Verification of file failure, " +
                                                                    "localCertificate = {0}, text = {1} ({2}), " +
                                                                    "fileName = {3}, " +
                                                                    "renewedCertificateFileName = {4}, " +
                                                                    "certificateFileName = {5}, " +
                                                                    "hashCertificateFileName = {6}, " +
                                                                    "renewedFileResult = {7}, hashFileResult = {8}, " +
                                                                    "fileResult = {9}, renewedHostResult = {10}, " +
                                                                    "hashHostResult = {11}, hashHostOnlyResult = {12}, " +
                                                                    "hostResult = {13}, hostOnlyResult = {14}, " +
                                                                    "extractError = {15}, verifyStringResult = {16}, " +
                                                                    "verifyFileResult = {17}, didVerifyString = {18}, " +
                                                                    "didVerifyFile = {19}, verifyFileCode = {20}, " +
                                                                    "result = {21}",
                                                                    DebugOnlyOps.FormatCertificate(localCertificate),
                                                                    Utility.FormatWrapOrNull(true, true, text),
                                                                    (text != null) ? text.Length : Length.Invalid,
                                                                    Utility.FormatWrapOrNull(fileName),
                                                                    Utility.FormatWrapOrNull(renewedCertificateFileName),
                                                                    Utility.FormatWrapOrNull(certificateFileName),
                                                                    Utility.FormatWrapOrNull(hashCertificateFileName),
                                                                    Utility.FormatWrapOrNull(true, false, renewedFileResult),
                                                                    Utility.FormatWrapOrNull(true, false, hashFileResult),
                                                                    Utility.FormatWrapOrNull(true, false, fileResult),
                                                                    Utility.FormatWrapOrNull(true, false, renewedHostResult),
                                                                    Utility.FormatWrapOrNull(true, false, hashHostResult),
                                                                    Utility.FormatWrapOrNull(true, false, hashHostOnlyResult),
                                                                    Utility.FormatWrapOrNull(true, false, hostResult),
                                                                    Utility.FormatWrapOrNull(true, false, hostOnlyResult),
                                                                    Utility.FormatWrapOrNull(true, false, extractError),
                                                                    Utility.FormatWrapOrNull(true, false, verifyStringResult),
                                                                    Utility.FormatWrapOrNull(true, false, verifyFileResult),
                                                                    didVerifyString, didVerifyFile, verifyFileCode,
                                                                    Utility.FormatWrapOrNull(true, false, result)),
                                                                    typeof(CertificatePolicyOps).Name,
                                                                    TracePriority.MediumHigh, 0);
                                                            }
#endif
                                                        }
                                                    }
                                                    else
                                                    {
                                                        result = "must select string and/or file verification";
                                                    }
                                                }
                                                else
                                                {
                                                    ResultList errors = new ResultList();

                                                    if (entityTypeResult != null)
                                                        errors.Add(entityTypeResult);

                                                    result = errors;

#if DEBUG || FORCE_TRACE
                                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                                        logClientData, String.Format(
                                                        "Certificate does not pass entity type check, " +
                                                        "localCertificate = {0}, entityTypeResult = {1}, " +
                                                        "result = {2}",
                                                        DebugOnlyOps.FormatCertificate(localCertificate),
                                                        Utility.FormatWrapOrNull(true, false, entityTypeResult),
                                                        Utility.FormatWrapOrNull(true, false, result)),
                                                        typeof(CertificatePolicyOps).Name,
                                                        TracePriority.MediumHigh, 0);
#endif
                                                }
                                            }
                                            else
                                            {
                                                ResultList errors = new ResultList();

                                                if (localSubjectResult != null)
                                                    errors.Add(localSubjectResult);

                                                if (subjectResult != null)
                                                    errors.Add(subjectResult);

                                                result = errors;

#if DEBUG || FORCE_TRACE
                                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                                    logClientData, String.Format(
                                                    "Certificate vendors do not match assembly subject, " +
                                                    "certificate = {0}, localCertificate = {1}, fileName = {2}, " +
                                                    "renewedCertificateFileName = {3}, certificateFileName = {4}, " +
                                                    "hashCertificateFileName = {5}, localSubjectResult = {6}, " +
                                                    "subjectResult = {7}, result = {8}",
                                                    DebugOnlyOps.FormatCertificate(certificate),
                                                    DebugOnlyOps.FormatCertificate(localCertificate),
                                                    Utility.FormatWrapOrNull(fileName),
                                                    Utility.FormatWrapOrNull(renewedCertificateFileName),
                                                    Utility.FormatWrapOrNull(certificateFileName),
                                                    Utility.FormatWrapOrNull(hashCertificateFileName),
                                                    Utility.FormatWrapOrNull(true, false, localSubjectResult),
                                                    Utility.FormatWrapOrNull(true, false, subjectResult),
                                                    Utility.FormatWrapOrNull(true, false, result)),
                                                    typeof(CertificatePolicyOps).Name,
                                                    TracePriority.MediumHigh, 0);
#endif
                                            }
                                        }
                                        else
                                        {
                                            result = localResult;
                                        }
                                    }
                                    else
                                    {
                                        ResultList errors = new ResultList();

                                        errors.Add("invalid certificate was provided");

                                        //
                                        // HACK: Just in case the import results actually
                                        //       contain something useful, include them in
                                        //       the overall result.
                                        //
                                        if (renewedFileResult != null)
                                            errors.Add(renewedFileResult);

                                        if (hashFileResult != null)
                                            errors.Add(hashFileResult);

                                        if (fileResult != null)
                                            errors.Add(fileResult);

                                        if (renewedHostResult != null)
                                            errors.Add(renewedHostResult);

                                        if (hashHostResult != null)
                                            errors.Add(hashHostResult);

                                        if (hashHostOnlyResult != null)
                                            errors.Add(hashHostOnlyResult);

                                        if (hostResult != null)
                                            errors.Add(hostResult);

                                        if (hostOnlyResult != null)
                                            errors.Add(hostOnlyResult);

                                        if (extractError != null)
                                            errors.Add(extractError);

                                        result = errors;

#if DEBUG || FORCE_TRACE
                                        CertificateTraceOps.MaybeLogAndDebugTrace(
                                            logClientData, String.Format(
                                            "Certificate invalid after being provided, " +
                                            "fileName = {0}, " +
                                            "renewedCertificateFileName = {1}, " +
                                            "certificateFileName = {2}, " +
                                            "hashCertificateFileName = {3}, " +
                                            "renewedFileResult = {4}, " +
                                            "hashFileResult = {5}, fileResult = {6}, " +
                                            "renewedHostResult = {7}, hashHostResult = {8}, " +
                                            "hashHostOnlyResult = {9}, hostResult = {10}, " +
                                            "hostOnlyResult = {11}, extractError = {12}, " +
                                            "result = {13}",
                                            Utility.FormatWrapOrNull(fileName),
                                            Utility.FormatWrapOrNull(renewedCertificateFileName),
                                            Utility.FormatWrapOrNull(certificateFileName),
                                            Utility.FormatWrapOrNull(hashCertificateFileName),
                                            Utility.FormatWrapOrNull(true, false, renewedFileResult),
                                            Utility.FormatWrapOrNull(true, false, hashFileResult),
                                            Utility.FormatWrapOrNull(true, false, fileResult),
                                            Utility.FormatWrapOrNull(true, false, renewedHostResult),
                                            Utility.FormatWrapOrNull(true, false, hashHostResult),
                                            Utility.FormatWrapOrNull(true, false, hashHostOnlyResult),
                                            Utility.FormatWrapOrNull(true, false, hostResult),
                                            Utility.FormatWrapOrNull(true, false, hostOnlyResult),
                                            Utility.FormatWrapOrNull(true, false, extractError),
                                            Utility.FormatWrapOrNull(true, false, result)),
                                            typeof(CertificatePolicyOps).Name,
                                            TracePriority.MediumHigh, 0);
#endif
                                    }
                                }
                                else
                                {
                                    ResultList errors = new ResultList();

                                    if (renewedFileResult != null)
                                        errors.Add(renewedFileResult);

                                    if (hashFileResult != null)
                                        errors.Add(hashFileResult);

                                    if (fileResult != null)
                                        errors.Add(fileResult);

                                    if (renewedHostResult != null)
                                        errors.Add(renewedHostResult);

                                    if (hashHostResult != null)
                                        errors.Add(hashHostResult);

                                    if (hashHostOnlyResult != null)
                                        errors.Add(hashHostOnlyResult);

                                    if (hostResult != null)
                                        errors.Add(hostResult);

                                    if (hostOnlyResult != null)
                                        errors.Add(hostOnlyResult);

                                    if (extractError != null)
                                        errors.Add(extractError);

                                    result = errors;

#if DEBUG || FORCE_TRACE
                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                        logClientData, String.Format(
                                        "Certificate file not found, " +
                                        "fileName = {0}, " +
                                        "renewedCertificateFileName = {1}, " +
                                        "certificateFileName = {2}, " +
                                        "hashCertificateFileName = {3}, " +
                                        "renewedFileResult = {4}, hashFileResult = {5}, " +
                                        "fileResult = {6}, renewedHostResult = {7}, " +
                                        "hashHostResult = {8}, hashHostOnlyResult = {9}, " +
                                        "hostResult = {10}, hostOnlyResult = {11}, " +
                                        "extractError = {12}, result = {13}",
                                        Utility.FormatWrapOrNull(fileName),
                                        Utility.FormatWrapOrNull(renewedCertificateFileName),
                                        Utility.FormatWrapOrNull(certificateFileName),
                                        Utility.FormatWrapOrNull(hashCertificateFileName),
                                        Utility.FormatWrapOrNull(true, false, renewedFileResult),
                                        Utility.FormatWrapOrNull(true, false, hashFileResult),
                                        Utility.FormatWrapOrNull(true, false, fileResult),
                                        Utility.FormatWrapOrNull(true, false, renewedHostResult),
                                        Utility.FormatWrapOrNull(true, false, hashHostResult),
                                        Utility.FormatWrapOrNull(true, false, hashHostOnlyResult),
                                        Utility.FormatWrapOrNull(true, false, hostResult),
                                        Utility.FormatWrapOrNull(true, false, hostOnlyResult),
                                        Utility.FormatWrapOrNull(true, false, extractError),
                                        Utility.FormatWrapOrNull(true, false, result)),
                                        typeof(CertificatePolicyOps).Name,
                                        TracePriority.MediumHigh, 0);
#endif
                                }
                            }
                            else
                            {
                                //
                                // TODO: Must strike a balance between security and
                                //       usability here.  We do not want to reveal the
                                //       fully qualified path to where we expected the
                                //       script certificate file to be; however, the
                                //       user does need to know which script file is
                                //       causing this error.
                                //
                                result = String.Format(
                                    "certificate file {0} does not exist",
                                    Utility.FormatWrapOrNull(Path.GetFileName(
                                        certificateFileName)));

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                    "Certificate file does not exist, " +
                                    "fileName = {0}, " +
                                    "renewedCertificateFileName = {1}, " +
                                    "certificateFileName = {2}, " +
                                    "hashCertificateFileName = {3}, " +
                                    "result = {4}", Utility.FormatWrapOrNull(fileName),
                                    Utility.FormatWrapOrNull(renewedCertificateFileName),
                                    Utility.FormatWrapOrNull(certificateFileName),
                                    Utility.FormatWrapOrNull(hashCertificateFileName),
                                    Utility.FormatWrapOrNull(true, false, result)),
                                    typeof(CertificatePolicyOps).Name,
                                    TracePriority.MediumHigh, 0);
#endif
                            }

                            Denied(interpreter, policyContext);

                            CertificateIsolatedOps.MaybeFixupResult(interpreter, plugin, result);
                            return ReturnCode.Ok;
                        }
                        finally
                        {
                            if (policyContext != null)
                            {
                                policyContext.Trace(
                                    "FilePolicyCallback", TracePriority.PolicyDebug,
                                    fullTracing);
                            }

                            CertificateTraceOps.MaybeEnableOrDisableTextWriter(
                                interpreter, cultureInfo, tracePolicy, false,
                                ref wasEnabled, ref savedBasePriority,
                                ref savedPriorities1, ref savedPriorities2);
                        }
                    }
                    finally
                    {
                        Utility.MaybePopActiveLogClientData(ref pushed);
                    }
                }
                finally
                {
                    if (logClientData != null)
                    {
                        logClientData.Dispose();
                        logClientData = null;
                    }
                }
            }
            finally
            {
                CertificatePolicyState.EndPending();
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Implements the stream execution policy callback by delegating to
        /// <see cref="FileCallback" /> to verify the streamed content.
        /// </summary>
        /// <param name="policy">
        /// The local or remote execution policy in effect for this callback.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the callback.
        /// </param>
        /// <param name="hasFlags">
        /// The string describing the flags that triggered the callback.
        /// </param>
        /// <param name="certificate">
        /// The certificate used to verify the stream.
        /// </param>
        /// <param name="assembly">
        /// The assembly associated with the policy.
        /// </param>
        /// <param name="keyName">
        /// The key name used to locate verification keys.
        /// </param>
        /// <param name="keyRingName">
        /// The key ring name used to locate verification keys.
        /// </param>
        /// <param name="scriptFlags">
        /// The script flags associated with the policy.
        /// </param>
        /// <param name="interpreter">
        /// The optional interpreter associated with the callback (may be
        /// null).
        /// </param>
        /// <param name="renewCallback">
        /// The optional renewal callback associated with the policy.
        /// </param>
        /// <param name="clientData">
        /// The client data carrying the policy context and stream content.
        /// </param>
        /// <param name="arguments">
        /// The arguments associated with the callback; not currently used.
        /// </param>
        /// <param name="ignoreBasePolicy">
        /// Non-zero to perform verification even when no base policy is set.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result or error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error
        /// return code.
        /// </returns>
        public static ReturnCode StreamCallback( /* POLICY IMPLEMENTATION */
            ExecutionPolicy policy,      /* in: LOCAL OR REMOTE */
            PolicyType policyType,       /* in */
            string hasFlags,             /* in */
            ICertificate certificate,    /* in */
            Assembly assembly,           /* in */
            string keyName,              /* in */
            string keyRingName,          /* in */
            ScriptFlags scriptFlags,     /* in */
            Interpreter interpreter,     /* in: OPTIONAL, MAY BE NULL. */
            RenewCallback renewCallback, /* in: OPTIONAL */
            IClientData clientData,      /* in */
            ArgumentList arguments,      /* in: NOT USED */
            bool ignoreBasePolicy,       /* in */
            ref Result result            /* out */
            )
        {
            return FileCallback(
                policy, policyType, hasFlags, certificate,
                assembly, keyName, keyRingName, scriptFlags,
                interpreter, renewCallback, clientData,
                arguments, ignoreBasePolicy, ref result);
        }
#endif
        #endregion
    }
}
