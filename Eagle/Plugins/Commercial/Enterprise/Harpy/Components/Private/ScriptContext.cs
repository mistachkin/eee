/*
 * ScriptContext.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;
using VariablePair = System.Collections.Generic.KeyValuePair<string, object>;
using VersionRange = Eagle._Components.Public.Pair<System.Version>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper routines that implement the licensing script
    /// context.  These routines manage the set of read-only and writable
    /// configuration variables exposed to scripts, as well as the logic
    /// used to gather, extract, apply, save, restore, and check for
    /// changes to that state.
    /// </summary>
    [ObjectId("3677c9ed-70f8-4442-8b8e-5948108a8427")]
    internal static class ScriptContext
    {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the creation of new interpreters should be
        /// disabled according to the applicable license execution policy.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin whose local policy may be consulted, which may be
        /// null.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to consult the policy that is local to
        /// <paramref name="pluginData" />; otherwise, the global policy
        /// is consulted.
        /// </param>
        /// <returns>
        /// Non-zero if interpreter creation should be disabled; otherwise,
        /// zero.
        /// </returns>
        public static bool ShouldDisableInterpreterCreation( /* CORE? */
            IPluginData pluginData, /* in: OPTIONAL */
            bool allowLocalPolicy   /* in */
            )
        {
            if (allowLocalPolicy && (pluginData != null))
            {
                if (Utility.HasFlags(CertificatePolicyOps.GetPolicy(
                        pluginData, PolicyType.License),
                        ExecutionPolicy.DisableCreation, true))
                {
                    return true;
                }
            }
            else
            {
                if (Utility.HasFlags(CertificatePolicyOps.GetPolicy(
                        PolicyType.License),
                        ExecutionPolicy.DisableCreation, true))
                {
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the creation of new interpreters is
        /// currently disabled in this process.
        /// </summary>
        /// <returns>
        /// Non-zero if interpreter creation is disabled; otherwise, zero.
        /// </returns>
        public static bool IsInterpreterCreationDisabled() /* CORE? */
        {
            Result error = null;

            if (Utility.IsInterpreterCreationDisabled(ref error))
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "IsInterpreterCreationDisabled: error = {0}",
                    Utility.FormatWrapOrNull(error)),
                    typeof(ScriptContext).Name,
                    TracePriority.Highest);
#endif

                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the creation of new interpreters is
        /// currently disabled and returns the result as a string.
        /// </summary>
        /// <returns>
        /// The string representation of true if interpreter creation is
        /// disabled; otherwise, null.
        /// </returns>
        public static string IsInterpreterCreationDisabledToString() /* CORE? */
        {
            return IsInterpreterCreationDisabled() ? true.ToString() : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disables the creation of new interpreters in this process.
        /// </summary>
        /// <param name="persistent">
        /// Non-zero to make the change persistent, which dynamically
        /// loads and invokes the stub assembly and cannot be undone.
        /// </param>
        public static void DisableInterpreterCreation( /* CORE? */
            bool persistent /* in */
            )
        {
            //
            // HACK: The persistent flag should be used with
            //       extreme caution.  It causes an assembly
            //       to be dynamically loaded and invoked,
            //       which could be slow.  Also, after it is
            //       loaded, it cannot be unloaded -AND- no
            //       further interpreters can be created in
            //       the process.
            //
            DisableFlags disableFlags = DisableFlags.Demand;

            if (persistent)
            {
                disableFlags |= DisableFlags.Persistent;

                /* NO RESULT */
                Utility.EnableStubAssembly(disableFlags); /* throw */
            }

            /* IGNORED */
            Utility.DisableInterpreterCreation(disableFlags); /* throw */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables the creation of new interpreters in this process.
        /// </summary>
        /// <param name="persistent">
        /// Non-zero to make the change persistent, which dynamically
        /// unloads the previously loaded stub assembly.
        /// </param>
        public static void EnableInterpreterCreation( /* CORE? */
            bool persistent /* in */
            )
        {
            DisableFlags disableFlags = DisableFlags.Demand;

            if (persistent)
            {
                disableFlags |= DisableFlags.Persistent;

                /* NO RESULT */
                Utility.DisableStubAssembly(disableFlags); /* throw */
            }

            /* IGNORED */
            Utility.EnableInterpreterCreation(disableFlags); /* throw */
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the name used to track whether the specified script
        /// context variable has been changed.
        /// </summary>
        /// <param name="name">
        /// The name of the script context variable.
        /// </param>
        /// <returns>
        /// The formatted changed-tracking variable name, or null if
        /// <paramref name="name" /> is null or empty.
        /// </returns>
        public static string FormatChangedVariableName( /* CORE */
            string name /* in */
            )
        {
            if (String.IsNullOrEmpty(name))
                return null;

            return String.Format(
                Constants.ChangedVariableFormat, name);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the name used to store the saved state snapshot
        /// associated with the specified name.
        /// </summary>
        /// <param name="name">
        /// The base name of the saved state.
        /// </param>
        /// <returns>
        /// The formatted saved state variable name, or null if
        /// <paramref name="name" /> is null or empty.
        /// </returns>
        private static string FormatSaveStateVariableName( /* CORE */
            string name /* in */
            )
        {
            if (String.IsNullOrEmpty(name))
                return null;

            return String.Format(
                Constants.SaveStateVariableFormat, name);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified script context variable has
        /// been marked as changed.
        /// </summary>
        /// <param name="clientData">
        /// The client data used to track per-context state, which may be
        /// null.
        /// </param>
        /// <param name="name">
        /// The name of the script context variable.
        /// </param>
        /// <returns>
        /// Non-zero if the variable has been marked as changed; otherwise,
        /// zero.
        /// </returns>
        private static bool HasChanged( /* CORE */
            IClientData clientData, /* in */
            string name             /* in */
            )
        {
            return CertificateSharedOps.TryHasDataValue(
                clientData, FormatChangedVariableName(name));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the specified script context variable as having been
        /// changed.
        /// </summary>
        /// <param name="clientData">
        /// The client data used to track per-context state, which may be
        /// null.
        /// </param>
        /// <param name="name">
        /// The name of the script context variable.
        /// </param>
        /// <returns>
        /// Non-zero if the changed flag was successfully set; otherwise,
        /// zero.
        /// </returns>
        public static bool SignalChanged( /* CORE */
            IClientData clientData, /* in */
            string name             /* in */
            )
        {
            Result error = null; /* NOT USED */

            return CertificateSharedOps.TrySetDataValue(
                clientData, FormatChangedVariableName(name),
                null, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the changed flag for the specified script context
        /// variable.
        /// </summary>
        /// <param name="clientData">
        /// The client data used to track per-context state, which may be
        /// null.
        /// </param>
        /// <param name="name">
        /// The name of the script context variable.
        /// </param>
        /// <returns>
        /// Non-zero if the changed flag was successfully cleared;
        /// otherwise, zero.
        /// </returns>
        public static bool SignalUnchanged( /* CORE */
            IClientData clientData, /* in */
            string name             /* in */
            )
        {
            Result error = null; /* NOT USED */

            return CertificateSharedOps.TryUnsetDataValue(
                clientData, FormatChangedVariableName(name),
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to retrieve the value of a variable from the specified
        /// dictionary, optionally enforcing a required type and null
        /// handling.
        /// </summary>
        /// <param name="variables">
        /// The dictionary of variables to query.
        /// </param>
        /// <param name="name">
        /// The name of the variable to retrieve.
        /// </param>
        /// <param name="type">
        /// The type the value is required to have, which may be null to
        /// skip type checking.
        /// </param>
        /// <param name="allowNull">
        /// Non-zero to permit a null value to be returned.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the value of the variable.
        /// </param>
        /// <returns>
        /// Non-zero if the variable was found and satisfied the type and
        /// null constraints; otherwise, zero.
        /// </returns>
        private static bool TryGetVariable( /* CORE */
            ObjectDictionary variables, /* in */
            string name,                /* in */
            Type type,                  /* in: OPTIONAL */
            bool allowNull,             /* in */
            ref object value            /* out */
            )
        {
            if (variables == null)
                return false;

            if (name == null)
                return false;

            if (!variables.TryGetValue(name, out value))
                return false;

            if (!allowNull && (value == null))
                return false;

            if (type != null)
            {
                if (type.IsValueType)
                {
                    if ((value == null) || /* valueType: disallow null */
                        !Object.ReferenceEquals(value.GetType(), type))
                    {
                        return false;
                    }
                }
                else
                {
                    if ((value != null) && /* referenceType: allow null */
                        !Object.ReferenceEquals(value.GetType(), type))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to store a value into the specified dictionary of
        /// variables.
        /// </summary>
        /// <param name="variables">
        /// The dictionary of variables to modify.
        /// </param>
        /// <param name="name">
        /// The name of the variable to set.
        /// </param>
        /// <param name="value">
        /// The value to store.
        /// </param>
        /// <param name="allowNull">
        /// Non-zero to permit a null value to be stored.
        /// </param>
        /// <returns>
        /// Non-zero if the value was stored; otherwise, zero.
        /// </returns>
        public static bool TrySetVariable( /* CORE */
            ObjectDictionary variables, /* in */
            string name,                /* in */
            object value,               /* in */
            bool allowNull              /* in */
            )
        {
            if (variables == null)
                return false;

            if (name == null)
                return false;

            if (!allowNull && (value == null))
                return false;

            variables[name] = value;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts a dictionary of script context variables into its
        /// string representation suitable for storage.
        /// </summary>
        /// <param name="variables">
        /// The dictionary of variables to convert.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The string representation of the variables, or null if the
        /// conversion fails.
        /// </returns>
        private static object FromVariables( /* CORE */
            ObjectDictionary variables, /* in */
            ref Result error            /* out */
            )
        {
            if (variables == null)
            {
                error = "invalid variables";
                return null;
            }

            return variables.KeysAndValuesToString(null, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts a previously stored value back into a dictionary of
        /// script context variables, optionally filtering out variables
        /// that have not been changed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during conversion, which may be null.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state, which may be
        /// null.
        /// </param>
        /// <param name="value">
        /// The stored value to convert.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to retain all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The dictionary of script context variables, or null if the
        /// conversion fails.
        /// </returns>
        private static ObjectDictionary ToVariables( /* CORE */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            object value,            /* in */
            bool ignoreChanged,      /* in */
            ref Result error         /* out */
            )
        {
            ObjectDictionary variables = ObjectDictionary.FromObject(
                interpreter, value, false, true, ref error);

            if (!ignoreChanged && (variables != null))
            {
                StringList keys = new StringList(variables.Keys);

                foreach (string key in keys)
                {
                    if (key == null) /* IMPOSSIBLE? */
                        continue;

                    if (!HasChanged(clientData, key))
                    {
                        /* IGNORED */
                        variables.Remove(key);
                    }
                }
            }

            return variables;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the dictionary of read-only script context variables
        /// that describe the current plugin, script, and licensing state.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, which may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context, which may be null.
        /// </param>
        /// <param name="pluginType">
        /// The type of the plugin, which may be null.
        /// </param>
        /// <param name="contextName">
        /// The name of the plugin context, which may be null.
        /// </param>
        /// <param name="variantName">
        /// The name of the plugin variant, which may be null.
        /// </param>
        /// <param name="type">
        /// The script type, which may be null.
        /// </param>
        /// <param name="subType">
        /// The script sub-type, which may be null.
        /// </param>
        /// <param name="hashValue">
        /// The hash that identifies the script file, which may be null.
        /// </param>
        /// <param name="fileName">
        /// The name of the script file, which may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The collection of key pairs associated with the script, which
        /// may be null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair associated with the script, which may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for value formatting, which may be null.
        /// </param>
        /// <param name="configurationPhase">
        /// The configuration phase that the context applies to.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="nameOnly">
        /// Non-zero to populate variable names only, using null values.
        /// </param>
        /// <returns>
        /// The dictionary of read-only script context variables.
        /// </returns>
        private static ObjectDictionary GetReadOnlyVariables( /* CORE */
            Interpreter interpreter,               /* in: OPTIONAL */
            IPluginData pluginData,                /* in: OPTIONAL */
            Type pluginType,                       /* in: OPTIONAL */
            string contextName,                    /* in: OPTIONAL */
            string variantName,                    /* in: OPTIONAL */
            string type,                           /* in: OPTIONAL */
            string subType,                        /* in: OPTIONAL */
            byte[] hashValue,                      /* in: OPTIONAL */
            string fileName,                       /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,        /* in: OPTIONAL */
            IKeyPair keyPair,                      /* in: OPTIONAL */
            CultureInfo cultureInfo,               /* in: OPTIONAL */
            ConfigurationPhase configurationPhase, /* in */
            bool noGlobalOnly,                     /* in: NOT USED */
            bool nameOnly                          /* in */
            )
        {
            ObjectDictionary variables = new ObjectDictionary();

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.PluginConfigurationPhaseVariableName,
                nameOnly ? null : configurationPhase.ToString());

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.PluginPublicKeyTokenVariableName, nameOnly ?
                null : CertificateAssemblyOps.GetPublicKeyTokenString());

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.PluginChangeCountVariableName, nameOnly ?
                null : (object)CertificateGlobalState.GetChangeCount());

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.ScriptTypeVariableName,
                nameOnly ? null : type);

            variables.Add(Constants.ScriptSubTypeVariableName,
                nameOnly ? null : subType);

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.ScriptDirectoryVariableName,
                !nameOnly && !String.IsNullOrEmpty(fileName) ?
                    Path.GetDirectoryName(fileName) : null);

            variables.Add(Constants.ScriptFileIdVariableName,
                nameOnly ? null : CertificateDataOps.FormatHexadecimal(
                hashValue, false));

            variables.Add(Constants.ScriptFileNameVariableName,
                nameOnly ? null : fileName);

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.PluginTypeVariableName,
                !nameOnly && (pluginType != null) ?
                    pluginType.ToString() : null);

            variables.Add(Constants.PluginContextVariableName,
                nameOnly ? null : contextName);

            variables.Add(Constants.PluginVariantVariableName,
                nameOnly ? null : variantName);

            variables.Add(
                Constants.PluginIsolatedVariableName, nameOnly ? null :
                    (object)CertificateSharedOps.IsCrossAppDomain(
                        interpreter, pluginData));

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.PluginMustHaveSecurityVariableName,
                nameOnly ? null :
                    CertificateGlobalState.GetMustHaveSecurityAsString());

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
            variables.Add(Constants.PluginCountVariableName,
                nameOnly ? null :
                    (object)CertificateAssemblyOps.GetReferences(interpreter,
                        pluginData, true));

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
            if (nameOnly)
            {
                variables.Add(
                    Constants.PluginMachineVariableName, null);
            }
            else
            {
                StringList list = null;

                if (CertificatePolicyOps.GetMachine(interpreter, cultureInfo,
                        CertificateLicenseState.GetPathFlagsOrDefault(),
                        ref list) == ReturnCode.Ok)
                {
                    variables.Add(
                        Constants.PluginMachineVariableName,
                        nameOnly ? null : list);
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (nameOnly)
            {
                variables.Add(
                    Constants.PluginPendingVariableName, null);
            }
            else
            {
                variables.Add(String.Format(
                    Constants.PluginPendingVariableFormat,
                    Constants.PluginPendingVariableName,
                    Constants.AnyKeyRingElementName),
                    CertificateKeyRingState.IsAnyPending());

                variables.Add(String.Format(
                    Constants.PluginPendingVariableFormat,
                    Constants.PluginPendingVariableName,
                    Constants.KeyRingElementName),
                    CertificateKeyRingState.IsPending());

                variables.Add(String.Format(
                    Constants.PluginPendingVariableFormat,
                    Constants.PluginPendingVariableName,
                    Constants.LicenseKeyRingElementName),
                    CertificateKeyRingState.IsLicensePending());

                variables.Add(String.Format(
                    Constants.PluginPendingVariableFormat,
                    Constants.PluginPendingVariableName,
                    Constants.LicenseElementName),
                    CertificateLicenseState.IsPending());
            }

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.ScriptPublicKeyTokenVariableName,
                !nameOnly && (keyPair != null) ?
                    CertificateDataOps.FormatPublicKeyToken(
                        keyPair.PublicKeyToken, false, false) : null);

            ///////////////////////////////////////////////////////////////////

            variables.Add(Constants.ScriptKeyPairsVariableName,
                nameOnly ? null : CertificateDataOps.FormatKeyPairs(
                    keyPairs, false));

            ///////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
            if (!nameOnly)
            {
                variables.Add(String.Format(
                    Constants.PluginPendingVariableFormat,
                    Constants.PluginPendingVariableName,
                    Constants.DemoLicenseElementName),
                    CertificateDemoState.IsLicensePending());
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if NETWORK && CERTIFICATE_RENEWAL
            if (!nameOnly)
            {
                variables.Add(String.Format(
                    Constants.PluginPendingVariableFormat,
                    Constants.PluginPendingVariableName,
                    Constants.RenewalElementName),
                    CertificateKeyRingState.IsRenewalPending());
            }
#endif
#endif
#endif

            ///////////////////////////////////////////////////////////////////

            return variables;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the complete dictionary of script context variables,
        /// including both the read-only variables and, unless requested
        /// otherwise, the writable configuration variables.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, which may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context, which may be null.
        /// </param>
        /// <param name="pluginType">
        /// The type of the plugin, which may be null.
        /// </param>
        /// <param name="contextName">
        /// The name of the plugin context, which may be null.
        /// </param>
        /// <param name="variantName">
        /// The name of the plugin variant, which may be null.
        /// </param>
        /// <param name="type">
        /// The script type, which may be null.
        /// </param>
        /// <param name="subType">
        /// The script sub-type, which may be null.
        /// </param>
        /// <param name="hashValue">
        /// The hash that identifies the script file, which may be null.
        /// </param>
        /// <param name="fileName">
        /// The name of the script file, which may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The collection of key pairs associated with the script, which
        /// may be null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair associated with the script, which may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for value formatting, which may be null.
        /// </param>
        /// <param name="configurationPhase">
        /// The configuration phase that the context applies to.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow the policy local to the plugin to be used.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero to return only the read-only variables.
        /// </param>
        /// <param name="nameOnly">
        /// Non-zero to populate variable names only, using null values.
        /// </param>
        /// <returns>
        /// The dictionary of script context variables.
        /// </returns>
        public static ObjectDictionary GetVariables( /* CORE */
            Interpreter interpreter,               /* in: OPTIONAL */
            IPluginData pluginData,                /* in: OPTIONAL */
            Type pluginType,                       /* in: OPTIONAL */
            string contextName,                    /* in: OPTIONAL */
            string variantName,                    /* in: OPTIONAL */
            string type,                           /* in: OPTIONAL */
            string subType,                        /* in: OPTIONAL */
            byte[] hashValue,                      /* in: OPTIONAL */
            string fileName,                       /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,        /* in: OPTIONAL */
            IKeyPair keyPair,                      /* in: OPTIONAL */
            CultureInfo cultureInfo,               /* in: OPTIONAL */
            ConfigurationPhase configurationPhase, /* in */
            bool noGlobalOnly,                     /* in */
            bool allowLocalPolicy,                 /* in */
            bool readOnly,                         /* in */
            bool nameOnly                          /* in */
            )
        {
            ObjectDictionary variables = GetReadOnlyVariables(
                interpreter, pluginData, pluginType, contextName,
                variantName, type, subType, hashValue, fileName,
                keyPairs, keyPair, cultureInfo, configurationPhase,
                noGlobalOnly, nameOnly);

            if (readOnly)
                goto done;

            ///////////////////////////////////////////////////////////////////

            if (!noGlobalOnly)
            {
                if (nameOnly)
                {
                    variables.Add(
                        Constants.PluginDurationVariableName, null);

                    variables.Add(
                        Constants.PluginTimeServersVariableName, null);

                    variables.Add(
                        Constants.PluginVersionRangeVariableName, null);

                    variables.Add(
                        Constants.PluginForceNetworkVariableName, null);

                    variables.Add(
                        Constants.PluginSkipLicenseVariableName, null);
                }
                else
                {
                    variables.Add(String.Format(
                        Constants.PluginDurationVariableFormat,
                        Constants.PluginDurationVariableName,
                        PolicyType.License), nameOnly ? null :
                        CertificateTimeState.GetDurationOrDefault(
                        PolicyType.License, false, true));

                    variables.Add(
                        Constants.PluginTimeServersVariableName,
                        nameOnly ? null : ((interpreter != null) ?
                        interpreter.TimeServers : null));

                    variables.Add(String.Format(
                        Constants.PluginVersionRangeVariableFormat,
                        Constants.PluginVersionRangeVariableName,
                        PolicyType.License), nameOnly ? null :
                        CertificateDataOps.FormatVersionRange(
                        CertificateVersionState.GetRange(
                        PolicyType.License, false)));

                    variables.Add(String.Format(
                        Constants.PluginForceNetworkVariableFormat,
                        Constants.PluginForceNetworkVariableName,
                        PolicyType.License), nameOnly ? null :
                        (object)CertificateLicenseState.GetForceNetwork());

                    variables.Add(String.Format(
                        Constants.PluginSkipLicenseVariableFormat,
                        Constants.PluginSkipLicenseVariableName,
                        Constants.EnabledElementName), nameOnly ? null :
                        (object)CertificateLicenseState.HaveSkip());

                    variables.Add(String.Format(
                        Constants.PluginSkipLicenseVariableFormat,
                        Constants.PluginSkipLicenseVariableName,
                        Constants.TypesElementName), nameOnly ? null :
                        CertificateLicenseState.GetSkipTypesToString());
                }

                variables.Add(Constants.PluginStorageTypeVariableName,
                    nameOnly ? null :
                        CertificateGlobalState.GetStorageTypeAsString());

                variables.Add(Constants.PluginSdkModeVariableName,
                    nameOnly ? null :
                        CertificateSdkMode.IsEnabledToString());

#if DEMO_KEY_PAIRS || DEMO_EDITION
                variables.Add(Constants.PluginDemoModeVariableName,
                    nameOnly ? null :
                        CertificateDemoMode.IsEnabledToString());
#endif

                variables.Add(Constants.PluginTestModeVariableName,
                    nameOnly ? null :
                        CertificateTestMode.IsEnabledToString());

                variables.Add(Constants.PluginFailSafeModeVariableName,
                    nameOnly ? null :
                        CertificateFailSafeMode.IsEnabledToString());

#if NETWORK
                variables.Add(Constants.PluginOfflineModeVariableName,
                    nameOnly ? null : Utility.InOfflineMode().ToString());
#endif
            }

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
#if !LIMITED_EDITION
            if (!noGlobalOnly)
            {
                variables.Add(Constants.PluginFeaturesVariableName,
                    nameOnly ? null :
                        CertificateGlobalState.GetExtraFeatures());
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
            PolicyConfiguration.GetVariables(
                pluginData, variables, allowLocalPolicy, nameOnly);

            ///////////////////////////////////////////////////////////////////

            if (!noGlobalOnly)
            {
                if (nameOnly)
                {
                    variables.Add(
                        Constants.PluginPathFlagsVariableName, null);

                    variables.Add(
                        Constants.PluginNetworkFlagsVariableName, null);
                }
                else
                {
                    variables.Add(String.Format(
                        Constants.PluginPathFlagsVariableFormat,
                        Constants.PluginPathFlagsVariableName,
                        PolicyType.License), nameOnly ? null :
                        CertificateLicenseState.GetPathFlagsToString());

                    variables.Add(String.Format(
                        Constants.PluginNetworkFlagsVariableFormat,
                        Constants.PluginNetworkFlagsVariableName,
                        PolicyType.License), nameOnly ? null :
                        CertificateLicenseState.GetNetworkFlagsToString());
                }
            }

            ///////////////////////////////////////////////////////////////////

#if SHELL && PLUGIN_COMMANDS
            if (!noGlobalOnly)
            {
                if (nameOnly)
                {
                    variables.Add(
                        Constants.PluginShellFlagsVariableName, null);
                }
                else
                {
                    variables.Add(Constants.PluginShellFlagsVariableName,
                        nameOnly ? null :
                        (object)CertificateShellState.GetFlagsToString(
                        interpreter));
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            if (!noGlobalOnly)
            {
                if (!nameOnly)
                {
                    variables.Add(String.Format(
                        Constants.PluginDurationVariableFormat,
                        Constants.PluginDurationVariableName,
                        PolicyType.Script), nameOnly ? null :
                        CertificateTimeState.GetDurationOrDefault(
                        PolicyType.Script, false, true));

                    variables.Add(String.Format(
                        Constants.PluginDurationVariableFormat,
                        Constants.PluginDurationVariableName,
                        PolicyType.File), nameOnly ? null :
                        CertificateTimeState.GetDurationOrDefault(
                        PolicyType.File, false, true));

                    variables.Add(String.Format(
                        Constants.PluginVersionRangeVariableFormat,
                        Constants.PluginVersionRangeVariableName,
                        PolicyType.Script), nameOnly ? null :
                        CertificateDataOps.FormatVersionRange(
                        CertificateVersionState.GetRange(
                        PolicyType.Script, false)));

                    variables.Add(String.Format(
                        Constants.PluginVersionRangeVariableFormat,
                        Constants.PluginVersionRangeVariableName,
                        PolicyType.File), nameOnly ? null :
                        CertificateDataOps.FormatVersionRange(
                        CertificateVersionState.GetRange(
                        PolicyType.File, false)));

                    variables.Add(String.Format(
                        Constants.PluginForceNetworkVariableFormat,
                        Constants.PluginForceNetworkVariableName,
                        PolicyType.Script), nameOnly ? null :
                        (object)CertificatePolicyState.GetForceNetwork());

                    variables.Add(String.Format(
                        Constants.PluginForceNetworkVariableFormat,
                        Constants.PluginForceNetworkVariableName,
                        PolicyType.KeyPair), nameOnly ? null :
                        (object)CertificateKeyPairState.GetForceNetwork());

                    variables.Add(String.Format(
                        Constants.PluginPathFlagsVariableFormat,
                        Constants.PluginPathFlagsVariableName,
                        PolicyType.Script), nameOnly ? null :
                        CertificatePolicyState.GetPathFlagsToString());

                    variables.Add(String.Format(
                        Constants.PluginNetworkFlagsVariableFormat,
                        Constants.PluginNetworkFlagsVariableName,
                        PolicyType.Script), nameOnly ? null :
                        CertificatePolicyState.GetNetworkFlagsToString());
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (!noGlobalOnly)
            {
                if (nameOnly)
                {
                    variables.Add(
                        Constants.InterpreterCreationVariableName, null);
                }
                else
                {
                    variables.Add(String.Format(
                        Constants.InterpreterCreationVariableFormat,
                        Constants.InterpreterCreationVariableName,
                        Constants.EnabledElementName), nameOnly ? null :
                        IsInterpreterCreationDisabledToString());

                    variables.Add(String.Format(
                        Constants.InterpreterCreationVariableFormat,
                        Constants.InterpreterCreationVariableName,
                        Constants.PersistentElementName), nameOnly ? null :
                        false.ToString()); // TODO: Good default?
                }
            }
#endif
#endif

            ///////////////////////////////////////////////////////////////////

        done:

            return variables;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: For use by ListContextVariables configuration command
        //          implementation only.
        //
        /// <summary>
        /// Returns the sorted list of all possible script context variable
        /// names, regardless of whether they are currently present.
        /// </summary>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <returns>
        /// The sorted list of script context variable names, or null if
        /// the names could not be determined.
        /// </returns>
        public static StringList GetVariableNames( /* CORE */
            bool noGlobalOnly /* in */
            )
        {
            //
            // HACK: This will return all possible variables used with
            //       the script context, not just those that happen to
            //       be present.
            //
            ObjectDictionary variables = GetVariables(
                null, null, null, null, null, null, null, null,
                null, null, null, null, ConfigurationPhase.Unknown,
                noGlobalOnly, false, false, true);

            if (variables == null)
                return null;

            Result error = null;

            if (RemoveElementNames(
                    ref variables, ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "GetVariableNames: error = {0}",
                    Utility.FormatWrapOrNull(error)),
                    typeof(ScriptContext).Name,
                    TracePriority.Highest);
#endif
            }

            StringList result = new StringList(variables.Keys);

            result.Sort(); /* O(N log N) */

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Rebuilds the script context variables and sets them within the
        /// specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the variables are set within.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context, which may be null.
        /// </param>
        /// <param name="pluginType">
        /// The type of the plugin, which may be null.
        /// </param>
        /// <param name="contextName">
        /// The name of the plugin context, which may be null.
        /// </param>
        /// <param name="variantName">
        /// The name of the plugin variant, which may be null.
        /// </param>
        /// <param name="type">
        /// The script type, which may be null.
        /// </param>
        /// <param name="subType">
        /// The script sub-type, which may be null.
        /// </param>
        /// <param name="hashValue">
        /// The hash that identifies the script file, which may be null.
        /// </param>
        /// <param name="fileName">
        /// The name of the script file, which may be null.
        /// </param>
        /// <param name="keyPairs">
        /// The collection of key pairs associated with the script, which
        /// may be null.
        /// </param>
        /// <param name="keyPair">
        /// The key pair associated with the script, which may be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for value formatting, which may be null.
        /// </param>
        /// <param name="configurationPhase">
        /// The configuration phase that the context applies to.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow the policy local to the plugin to be used.
        /// </param>
        /// <param name="globalFrame">
        /// Non-zero to set the variables in the global call frame;
        /// otherwise, the current call frame is used.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero to include only the read-only variables.
        /// </param>
        /// <param name="variables">
        /// Upon success, receives the dictionary of variables that were
        /// set.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode RefreshVariables( /* CORE */
            Interpreter interpreter,               /* in: OPTIONAL */
            IPluginData pluginData,                /* in: OPTIONAL */
            Type pluginType,                       /* in: OPTIONAL */
            string contextName,                    /* in: OPTIONAL */
            string variantName,                    /* in: OPTIONAL */
            string type,                           /* in: OPTIONAL */
            string subType,                        /* in: OPTIONAL */
            byte[] hashValue,                      /* in: OPTIONAL */
            string fileName,                       /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs,        /* in: OPTIONAL */
            IKeyPair keyPair,                      /* in: OPTIONAL */
            CultureInfo cultureInfo,               /* in: OPTIONAL */
            ConfigurationPhase configurationPhase, /* in */
            bool noGlobalOnly,                     /* in */
            bool allowLocalPolicy,                 /* in */
            bool globalFrame,                      /* in */
            bool readOnly,                         /* in */
            ref ObjectDictionary variables,        /* out */
            ref Result error                       /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            ObjectDictionary localVariables = GetVariables(
                interpreter, pluginData, pluginType, contextName,
                variantName, type, subType, hashValue, fileName,
                keyPairs, keyPair, cultureInfo, configurationPhase,
                noGlobalOnly, allowLocalPolicy, readOnly, false);

            if (SetVariables(
                    interpreter, localVariables, globalFrame,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            variables = localVariables;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the writable script context variables from the
        /// interpreter, converting each value to its native type, and
        /// merges them into the supplied dictionary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to read the variables from.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for value conversion, which may be null.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to process all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="variables">
        /// The dictionary that the extracted variables are merged into;
        /// when null, a new dictionary is created and returned.
        /// </param>
        /// <param name="count">
        /// The running total of processed variables, which is incremented
        /// by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ExtractVariables( /* CORE */
            Interpreter interpreter,        /* in */
            IClientData clientData,         /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            bool noGlobalOnly,              /* in */
            bool ignoreChanged,             /* in */
            ref ObjectDictionary variables, /* in, out */
            ref int count,                  /* in, out */
            ref Result error                /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            ObjectDictionary localVariables = new ObjectDictionary();
            string name; /* REUSED */
            Result value; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                TimeSpan? duration = null;

                if (Value.GetNullableTimeSpan2(
                        value, ValueFlags.AnyTimeSpan, cultureInfo,
                        ref duration, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (duration != null)
                {
                    localVariables[name] = (TimeSpan)duration;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginTimeServersVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                string servers = value;
                StringList list = null;

                if ((servers != null) && Parser.SplitList(
                        interpreter, servers, 0, Length.Invalid, true,
                        ref list, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                localVariables[name] = list;
                count++;
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                VersionRange versionRange = null;

                if (CertificateDataOps.GetVersionRange(
                        value, cultureInfo, ref versionRange,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (versionRange != null)
                {
                    localVariables[name] = versionRange;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
#if SHELL && PLUGIN_COMMANDS
            name = Constants.PluginShellFlagsVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(value))
                {
                    object enumValue = Utility.TryParseEnum(
                        typeof(ShellFlags), value, true, true,
                        ref error);

                    if (!(enumValue is ShellFlags))
                        return ReturnCode.Error;

                    localVariables[name] = (ShellFlags)enumValue;
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                TimeSpan? duration = null;

                if (Value.GetNullableTimeSpan2(
                        value, ValueFlags.AnyTimeSpan, cultureInfo,
                        ref duration, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (duration != null)
                {
                    localVariables[name] = (TimeSpan)duration;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.File);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                TimeSpan? duration = null;

                if (Value.GetNullableTimeSpan2(
                        value, ValueFlags.AnyTimeSpan, cultureInfo,
                        ref duration, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (duration != null)
                {
                    localVariables[name] = (TimeSpan)duration;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                VersionRange versionRange = null;

                if (CertificateDataOps.GetVersionRange(
                        value, cultureInfo, ref versionRange,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (versionRange != null)
                {
                    localVariables[name] = versionRange;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.File);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                VersionRange versionRange = null;

                if (CertificateDataOps.GetVersionRange(
                        value, cultureInfo, ref versionRange,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (versionRange != null)
                {
                    localVariables[name] = versionRange;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.KeyPair);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginPathFlagsVariableFormat,
                Constants.PluginPathFlagsVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(value))
                {
                    object enumValue = Utility.TryParseEnum(
                        typeof(PathFlags), value, true, true,
                        ref error);

                    if (!(enumValue is PathFlags))
                        return ReturnCode.Error;

                    localVariables[name] = (PathFlags)enumValue;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginPathFlagsVariableFormat,
                Constants.PluginPathFlagsVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(value))
                {
                    object enumValue = Utility.TryParseEnum(
                        typeof(PathFlags), value, true, true,
                        ref error);

                    if (!(enumValue is PathFlags))
                        return ReturnCode.Error;

                    localVariables[name] = (PathFlags)enumValue;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginNetworkFlagsVariableFormat,
                Constants.PluginNetworkFlagsVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(value))
                {
                    object enumValue = Utility.TryParseEnum(
                        typeof(NetworkFlags), value, true, true,
                        ref error);

                    if (!(enumValue is NetworkFlags))
                        return ReturnCode.Error;

                    localVariables[name] = (NetworkFlags)enumValue;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginNetworkFlagsVariableFormat,
                Constants.PluginNetworkFlagsVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(value))
                {
                    object enumValue = Utility.TryParseEnum(
                        typeof(NetworkFlags), value, true, true,
                        ref error);

                    if (!(enumValue is NetworkFlags))
                        return ReturnCode.Error;

                    localVariables[name] = (NetworkFlags)enumValue;
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginSkipLicenseVariableFormat,
                Constants.PluginSkipLicenseVariableName,
                Constants.EnabledElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginSkipLicenseVariableFormat,
                Constants.PluginSkipLicenseVariableName,
                Constants.TypesElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(value))
                {
                    object enumValue = Utility.TryParseFlagsEnum(
                        interpreter, typeof(LicenseType),
                        CertificateLicenseState.GetSkipTypesToString(),
                        value, cultureInfo, true, true, true, ref error);

                    if (!(enumValue is LicenseType))
                        return ReturnCode.Error;

                    localVariables[name] = (LicenseType)enumValue;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginStorageTypeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (!String.IsNullOrEmpty(value))
                {
                    object enumValue = Utility.TryParseEnum(
                        typeof(StorageType), value, true, true,
                        ref error);

                    if (!(enumValue is StorageType))
                        return ReturnCode.Error;

                    localVariables[name] = (StorageType)enumValue;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
            name = Constants.PluginDemoModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginTestModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginFailSafeModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

#if NETWORK
            name = Constants.PluginOfflineModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            name = Constants.PluginFeaturesVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                localVariables[name] = (string)value;
                count++;
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            name = String.Format(
                Constants.InterpreterCreationVariableFormat,
                Constants.InterpreterCreationVariableName,
                Constants.EnabledElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.InterpreterCreationVariableFormat,
                Constants.InterpreterCreationVariableName,
                Constants.PersistentElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (interpreter.GetVariableValue(
                        Constants.ContextGetVariableFlags, name,
                        ref value, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                bool? enable = null;

                if (Value.GetNullableBoolean2(
                        value, ValueFlags.AnyBoolean, cultureInfo,
                        ref enable, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (enable != null)
                {
                    localVariables[name] = (bool)enable;
                    count++;
                }
            }

            /////////////////////////////////////////////////////////////////////

            if (PolicyConfiguration.ExtractVariables(
                    interpreter, clientData, cultureInfo,
                    ignoreChanged, ref localVariables,
                    ref count, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }
#endif

            ///////////////////////////////////////////////////////////////////

            if (variables != null)
            {
                if (localVariables != null)
                {
                    //
                    // NOTE: This is a logical merge of variables
                    //       (i.e. names and values).  Old values
                    //       will be replaced with new values, if
                    //       present.
                    //
                    foreach (VariablePair pair in localVariables)
                        variables[pair.Key] = pair.Value;
                }
            }
            else
            {
                //
                // NOTE: There are no old variables (i.e. names
                //       and values); just return the new ones.
                //
                variables = localVariables;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Takes a complete snapshot of the current script context
        /// variables and stores it under the specified saved state name.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose variables are captured.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context, which may be null.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state.
        /// </param>
        /// <param name="saveStateName">
        /// The name under which the snapshot is stored.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for value conversion, which may be null.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to capture all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow the policy local to the plugin to be used.
        /// </param>
        /// <param name="count">
        /// The running total of processed variables, which is incremented
        /// by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode SaveVariables( /* CORE */
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            IClientData clientData,  /* in */
            string saveStateName,    /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            bool noGlobalOnly,       /* in */
            bool ignoreChanged,      /* in */
            bool allowLocalPolicy,   /* in */
            ref int count,           /* in, out */
            ref Result error         /* out */
            )
        {
            //
            // NOTE: The intent of this method is to take a complete
            //       "snapshot" of the (current) state of the script
            //       context variables (i.e. ones that can be changed
            //       indirectly via configuration commands).  There
            //       are several problems with naive a implementation
            //       here.  First, the caller must specify a non-zero
            //       value for the ignoreChanged parameter; otherwise,
            //       the snapshot will very likely be incomplete.
            //       Another problem lies with the use of the shared
            //       ExtractVariables method.  It is designed to skip
            //       any adding any variables that have a null -OR-
            //       empty value, with the sole exception of plugin
            //       feature flags.  This stronly implies that the
            //       variables within the interpreter cannot be the
            //       sole source for the created snapshot; therefore,
            //       the new GatherVariables methods were created to
            //       add "synthetic" script context variables based
            //       on the actual global state they correspond to.
            //       This should be harmless because any subsequent
            //       ExtractAndApplyVariables method calls will most
            //       likely consult the "changed" (i.e. "dirtiness")
            //       flags and associated with each script context
            //       variable before modifying its associated global
            //       state.
            //
            ObjectDictionary variables = null;

            if (GatherVariables(interpreter,
                    pluginData, clientData, noGlobalOnly,
                    allowLocalPolicy, ignoreChanged,
                    ref variables, ref count,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (ExtractVariables(
                    interpreter, clientData, cultureInfo,
                    noGlobalOnly, ignoreChanged, ref variables,
                    ref count, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            object value = FromVariables(variables, ref error);

            if (value == null)
                return ReturnCode.Error;

            if (!CertificateSharedOps.TrySetDataValue(
                    clientData, FormatSaveStateVariableName(
                    saveStateName), value, ref error))
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores a previously saved snapshot of the script context
        /// variables into the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the variables are restored into.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state.
        /// </param>
        /// <param name="saveStateName">
        /// The name of the snapshot to restore.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to restore all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="globalFrame">
        /// Non-zero to set the variables in the global call frame;
        /// otherwise, the current call frame is used.
        /// </param>
        /// <param name="setChanged">
        /// Non-zero to mark each restored variable as changed.
        /// </param>
        /// <param name="removeSaveState">
        /// Non-zero to remove the stored snapshot after restoring it.
        /// </param>
        /// <param name="count">
        /// The running total of processed variables, which is incremented
        /// by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode RestoreVariables( /* CORE */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            string saveStateName,    /* in */
            bool noGlobalOnly,       /* in */
            bool ignoreChanged,      /* in */
            bool globalFrame,        /* in */
            bool setChanged,         /* in */
            bool removeSaveState,    /* in */
            ref int count,           /* in, out */
            ref Result error         /* out */
            )
        {
            //
            // NOTE: The method restores a complete "snapshot" of the
            //       (previously saved) state of the script context
            //       variables (i.e. ones that can be changed indirectly
            //       via configuration commands).  This method should
            //       generally be called with a value of zero for the
            //       ignoreChanged parameter and zero for the setChanged
            //       parameter, which will filter the list of variables
            //       to be restored based on their associated "changed"
            //       (i.e. "dirtiness") flags and then skip re-setting
            //       their associated "changed" flags.
            //
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            object value = null;

            if (!CertificateSharedOps.TryGetDataValue(
                    clientData, FormatSaveStateVariableName(
                    saveStateName), ref value, ref error))
            {
                return ReturnCode.Error;
            }

            ObjectDictionary variables = ToVariables(
                interpreter, clientData, value, ignoreChanged,
                ref error);

            if (variables == null)
                return ReturnCode.Error;

            if (SetVariables(
                    interpreter, variables, globalFrame,
                    ref count, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (setChanged)
            {
                foreach (VariablePair pair in variables)
                {
                    /* IGNORED */
                    SignalChanged(clientData, pair.Key);
                }
            }

            if (removeSaveState)
            {
                if (!CertificateSharedOps.TryUnsetDataValue(
                        clientData, FormatSaveStateVariableName(
                        saveStateName), ref error))
                {
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gathers synthetic script context variables directly from the
        /// underlying global state and merges them into the supplied
        /// dictionary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, which may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context, which may be null.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state, which may be
        /// null.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow the policy local to the plugin to be used.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to gather all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="variables">
        /// The dictionary that the gathered variables are merged into;
        /// when null, a new dictionary is created and returned.
        /// </param>
        /// <param name="count">
        /// The running total of processed variables, which is incremented
        /// by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GatherVariables( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            IPluginData pluginData,         /* in: OPTIONAL */
            IClientData clientData,         /* in: OPTIONAL */
            bool noGlobalOnly,              /* in */
            bool allowLocalPolicy,          /* in */
            bool ignoreChanged,             /* in */
            ref ObjectDictionary variables, /* in, out */
            ref int count,                  /* in, out */
            ref Result error                /* out */
            )
        {
            ObjectDictionary localVariables = new ObjectDictionary();
            string name; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateTimeState.GetDurationOrDefault(
                        PolicyType.License, false, true), true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateVersionState.GetRange(
                        PolicyType.License, false), true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateLicenseState.GetForceNetwork(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
#if SHELL && PLUGIN_COMMANDS
            name = Constants.PluginShellFlagsVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateShellState.GetFlags(interpreter),
                        true))
                {
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateTimeState.GetDurationOrDefault(
                        PolicyType.Script, false, true), true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.File);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateTimeState.GetDurationOrDefault(
                        PolicyType.File, false, true), true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginTimeServersVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        (interpreter != null) ?
                        interpreter.TimeServers : null, true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateVersionState.GetRange(
                        PolicyType.Script, false), true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.File);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateVersionState.GetRange(
                        PolicyType.File, false), true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificatePolicyState.GetForceNetwork(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.KeyPair);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateKeyPairState.GetForceNetwork(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginPathFlagsVariableFormat,
                Constants.PluginPathFlagsVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateLicenseState.GetPathFlagsToString(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginPathFlagsVariableFormat,
                Constants.PluginPathFlagsVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificatePolicyState.GetPathFlagsToString(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginNetworkFlagsVariableFormat,
                Constants.PluginNetworkFlagsVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateLicenseState.GetNetworkFlagsToString(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginNetworkFlagsVariableFormat,
                Constants.PluginNetworkFlagsVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificatePolicyState.GetNetworkFlagsToString(),
                        true))
                {
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginSkipLicenseVariableFormat,
                Constants.PluginSkipLicenseVariableName,
                Constants.EnabledElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateLicenseState.HaveSkip(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginSkipLicenseVariableFormat,
                Constants.PluginSkipLicenseVariableName,
                Constants.TypesElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateLicenseState.GetSkipTypes(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginStorageTypeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateGlobalState.GetStorageType(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
            name = Constants.PluginDemoModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateDemoMode.IsEnabledToString(),
                        true))
                {
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginTestModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateTestMode.IsEnabledToString(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginFailSafeModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateFailSafeMode.IsEnabledToString(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

#if NETWORK
            name = Constants.PluginOfflineModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        Utility.InOfflineMode().ToString(),
                        true))
                {
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            name = Constants.PluginFeaturesVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        CertificateGlobalState.GetExtraFeatures(),
                        true))
                {
                    count++;
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            name = String.Format(
                Constants.InterpreterCreationVariableFormat,
                Constants.InterpreterCreationVariableName,
                Constants.EnabledElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TrySetVariable(localVariables, name,
                        IsInterpreterCreationDisabledToString(),
                        true))
                {
                    count++;
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (PolicyConfiguration.GatherVariables(
                    pluginData, clientData, allowLocalPolicy,
                    ignoreChanged, ref localVariables, ref count,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }
#endif

            ///////////////////////////////////////////////////////////////////

            if (variables != null)
            {
                if (localVariables != null)
                {
                    //
                    // NOTE: This is a logical merge of variables
                    //       (i.e. names and values).  Old values
                    //       will be replaced with new values, if
                    //       present.
                    //
                    foreach (VariablePair pair in localVariables)
                        variables[pair.Key] = pair.Value;
                }
            }
            else
            {
                //
                // NOTE: There are no old variables (i.e. names
                //       and values); just return the new ones.
                //
                variables = localVariables;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies the supplied script context variables to the underlying
        /// global state, updating the change counters as needed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, which may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context, which may be null.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state, which may be
        /// null.
        /// </param>
        /// <param name="variables">
        /// The dictionary of variables to apply.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow the policy local to the plugin to be used.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to apply all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="resetChanged">
        /// Non-zero to clear the changed flag for each applied variable.
        /// </param>
        /// <param name="count">
        /// The running total of processed variables, which is incremented
        /// by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ApplyVariables( /* CORE */
            Interpreter interpreter,    /* in: OPTIONAL */
            IPluginData pluginData,     /* in: OPTIONAL */
            IClientData clientData,     /* in: OPTIONAL */
            ObjectDictionary variables, /* in */
            bool noGlobalOnly,          /* in */
            bool allowLocalPolicy,      /* in */
            bool ignoreChanged,         /* in */
            bool resetChanged,          /* in */
            ref int count,              /* in, out */
            ref Result error            /* out */
            )
        {
            string name; /* REUSED */
            object value; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(TimeSpan), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateTimeState.SetDurationOrDefault(
                        PolicyType.License, (TimeSpan)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* IGNORED */
                    CertificateTimeState.UnsetDurationOrDefault(
                        PolicyType.License);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(VersionRange), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateVersionState.SetRange(
                        PolicyType.License, (VersionRange)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* IGNORED */
                    CertificateVersionState.UnsetRange(
                        PolicyType.License);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateLicenseState.SetForceNetwork(
                        (bool)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
#if SHELL && PLUGIN_COMMANDS
            name = Constants.PluginShellFlagsVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(ShellFlags), false,
                        ref value))
                {
                    //
                    // BUGBUG: Perhaps a failure to apply the flags
                    //         here should fail the entire method?
                    //         That would be tricky because some
                    //         state changes may have already been
                    //         applied.
                    //
                    ShellFlags shellFlags = (ShellFlags)value;

                    CertificateShellState.MaybeSetFlags(
                        ref shellFlags);

                    Result applyError = null;

                    if (CertificateShellState.ApplyFlags(
                            interpreter, pluginData, shellFlags,
                            ref applyError) != ReturnCode.Ok)
                    {
#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(String.Format(
                            "ApplyVariables: name = {0}, error = {1}",
                            Utility.FormatWrapOrNull(name),
                            Utility.FormatWrapOrNull(applyError)),
                            typeof(ScriptContext).Name,
                            TracePriority.Highest);
#endif
                    }

                    //
                    // BUGBUG: Just to be on the safe side, update
                    //         the change counters in case the shell
                    //         flags were partially applied.
                    //
                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* NO RESULT */
                    CertificateShellState.UnsetFlags();

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(TimeSpan), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateTimeState.SetDurationOrDefault(
                        PolicyType.Script, (TimeSpan)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* IGNORED */
                    CertificateTimeState.UnsetDurationOrDefault(
                        PolicyType.Script);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.File);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(TimeSpan), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateTimeState.SetDurationOrDefault(
                        PolicyType.File, (TimeSpan)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* IGNORED */
                    CertificateTimeState.UnsetDurationOrDefault(
                        PolicyType.File);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginTimeServersVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (interpreter != null)
                {
                    value = null;

                    if (TryGetVariable(
                            variables, name, typeof(StringList), false,
                            ref value))
                    {
                        interpreter.TimeServers = value as StringList;
                    }
                    else
                    {
                        /* IGNORED */
                        interpreter.TimeServers = null; /* UNSET */
                    }

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(VersionRange), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateVersionState.SetRange(
                        PolicyType.Script, (VersionRange)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* IGNORED */
                    CertificateVersionState.UnsetRange(
                        PolicyType.Script);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.File);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(VersionRange), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateVersionState.SetRange(
                        PolicyType.File, (VersionRange)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* IGNORED */
                    CertificateVersionState.UnsetRange(
                        PolicyType.File);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificatePolicyState.SetForceNetwork(
                        (bool)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.KeyPair);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateKeyPairState.SetForceNetwork(
                        (bool)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginPathFlagsVariableFormat,
                Constants.PluginPathFlagsVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(PathFlags), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateLicenseState.SetPathFlags(
                        (PathFlags)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* NO RESULT */
                    CertificateLicenseState.UnsetPathFlags();

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginPathFlagsVariableFormat,
                Constants.PluginPathFlagsVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(PathFlags), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificatePolicyState.SetPathFlags(
                        (PathFlags)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* NO RESULT */
                    CertificatePolicyState.UnsetPathFlags();

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginNetworkFlagsVariableFormat,
                Constants.PluginNetworkFlagsVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(NetworkFlags), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateLicenseState.SetNetworkFlags(
                        (NetworkFlags)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* NO RESULT */
                    CertificateLicenseState.UnsetNetworkFlags();

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginNetworkFlagsVariableFormat,
                Constants.PluginNetworkFlagsVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(NetworkFlags), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificatePolicyState.SetNetworkFlags(
                        (NetworkFlags)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* NO RESULT */
                    CertificatePolicyState.UnsetNetworkFlags();

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginSkipLicenseVariableFormat,
                Constants.PluginSkipLicenseVariableName,
                Constants.EnabledElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    if ((bool)value)
                    {
                        /* NO RESULT */
                        CertificateLicenseState.EnableSkip();
                    }
                    else
                    {
                        /* NO RESULT */
                        CertificateLicenseState.DisableSkip();
                    }

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginSkipLicenseVariableFormat,
                Constants.PluginSkipLicenseVariableName,
                Constants.TypesElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(LicenseType), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateLicenseState.SetSkipTypes(
                        (LicenseType)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginStorageTypeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(StorageType), false,
                        ref value))
                {
                    /* NO RESULT */
                    CertificateGlobalState.SetStorageType(
                        (StorageType)value);

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* NO RESULT */
                    CertificateGlobalState.UnsetStorageType();

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
            name = Constants.PluginDemoModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    if ((bool)value)
                    {
                        /* NO RESULT */
                        CertificateDemoMode.Enable();
                    }
                    else
                    {
                        /* NO RESULT */
                        CertificateDemoMode.Disable();
                    }

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginTestModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    if ((bool)value)
                    {
                        /* NO RESULT */
                        CertificateTestMode.Enable();
                    }
                    else
                    {
                        /* NO RESULT */
                        CertificateTestMode.Disable();
                    }

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginFailSafeModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    if ((bool)value)
                    {
                        /* NO RESULT */
                        CertificateFailSafeMode.Enable();
                    }
                    else
                    {
                        /* NO RESULT */
                        CertificateFailSafeMode.Disable();
                    }

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

#if NETWORK
            name = Constants.PluginOfflineModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    if ((bool)value)
                    {
                        /* NO RESULT */
                        Utility.SetOfflineMode(true);
                    }
                    else
                    {
                        /* NO RESULT */
                        Utility.SetOfflineMode(false);
                    }

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            name = Constants.PluginFeaturesVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(string), true,
                        ref value))
                {
                    if (!String.IsNullOrEmpty((string)value))
                    {
                        /* NO RESULT */
                        CertificateGlobalState.SetExtraFeatures(
                            (string)value);
                    }
                    else
                    {
                        /* NO RESULT */
                        CertificateGlobalState.UnsetExtraFeatures();
                    }

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }
                else
                {
                    /* NO RESULT */
                    CertificateGlobalState.UnsetExtraFeatures();

                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            name = String.Format(
                Constants.InterpreterCreationVariableFormat,
                Constants.InterpreterCreationVariableName,
                Constants.PersistentElementName);

            object persistent = null;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref persistent))
                {
                    /* IGNORED */
                    CertificateGlobalState.IncrementChangeCount();

                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.InterpreterCreationVariableFormat,
                Constants.InterpreterCreationVariableName,
                Constants.EnabledElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                value = null;

                if (TryGetVariable(
                        variables, name, typeof(bool), false,
                        ref value))
                {
                    try
                    {
                        if (persistent == null)
                            persistent = false; // TODO: Good default?

                        if ((bool)value)
                        {
                            /* NO RESULT */
                            EnableInterpreterCreation(
                                (bool)persistent); /* throw */
                        }
                        else
                        {
                            /* NO RESULT */
                            DisableInterpreterCreation(
                                (bool)persistent); /* throw */
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }
                    catch (Exception e)
                    {
                        error = e;
                        return ReturnCode.Error;
                    }
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (PolicyConfiguration.ApplyVariables(
                    pluginData, clientData, variables,
                    allowLocalPolicy, ignoreChanged,
                    resetChanged, ref count,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }
#endif

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the underlying global state for any script context
        /// variables that differ from their default values, optionally
        /// resetting them.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context, which may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context, which may be null.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state, which may be
        /// null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for value conversion, which may be null.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow the policy local to the plugin to be used,
        /// or null to skip the policy checks.
        /// </param>
        /// <param name="useDefaultPolicy">
        /// Non-zero to use the default policy, which may be null.
        /// </param>
        /// <param name="withReset">
        /// Non-zero to reset each changed variable to its default value.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to check all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="resetChanged">
        /// Non-zero to clear the changed flag for each checked variable.
        /// </param>
        /// <param name="count">
        /// The running total of changed variables, which is incremented
        /// by this method.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the list of changed variable names;
        /// upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode CheckForChanges( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: OPTIONAL */
            IClientData clientData,  /* in: OPTIONAL */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            bool noGlobalOnly,       /* in */
            bool? allowLocalPolicy,  /* in */
            bool? useDefaultPolicy,  /* in */
            bool withReset,          /* in */
            bool ignoreChanged,      /* in */
            bool resetChanged,       /* in */
            ref int count,           /* in, out */
            ref Result result        /* out */
            )
        {
            string name; /* REUSED */
            StringList list = null;

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateTimeState.HaveDurationOrDefault(
                        PolicyType.License, false))
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateTimeState.UnsetDurationOrDefault(
                            PolicyType.License);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginTimeServersVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if ((interpreter != null) &&
                    (interpreter.TimeServers != null)) /* DEFAULT? */
                {
                    if (withReset)
                        interpreter.TimeServers = null; /* RESET */

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateVersionState.HaveRange(
                        PolicyType.License, false))
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateVersionState.UnsetRange(
                            PolicyType.License);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateLicenseState.GetForceNetwork())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateLicenseState.SetForceNetwork(
                            false);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateTimeState.HaveDurationOrDefault(
                        PolicyType.Script, false))
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateTimeState.UnsetDurationOrDefault(
                            PolicyType.Script);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginDurationVariableFormat,
                Constants.PluginDurationVariableName,
                PolicyType.File);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateTimeState.HaveDurationOrDefault(
                        PolicyType.File, false))
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateTimeState.UnsetDurationOrDefault(
                            PolicyType.File);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateVersionState.HaveRange(
                        PolicyType.Script, false))
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateVersionState.UnsetRange(
                            PolicyType.Script);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginVersionRangeVariableFormat,
                Constants.PluginVersionRangeVariableName,
                PolicyType.File);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateVersionState.HaveRange(
                        PolicyType.File, false))
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateVersionState.UnsetRange(
                            PolicyType.File);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificatePolicyState.GetForceNetwork())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificatePolicyState.SetForceNetwork(
                            false);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginForceNetworkVariableFormat,
                Constants.PluginForceNetworkVariableName,
                PolicyType.KeyPair);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateKeyPairState.GetForceNetwork())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateKeyPairState.SetForceNetwork(
                            false);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginPathFlagsVariableFormat,
                Constants.PluginPathFlagsVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateLicenseState.HavePathFlags())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateLicenseState.UnsetPathFlags();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginPathFlagsVariableFormat,
                Constants.PluginPathFlagsVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificatePolicyState.HavePathFlags())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificatePolicyState.UnsetPathFlags();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginNetworkFlagsVariableFormat,
                Constants.PluginNetworkFlagsVariableName,
                PolicyType.License);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateLicenseState.HaveNetworkFlags())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateLicenseState.UnsetNetworkFlags();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginNetworkFlagsVariableFormat,
                Constants.PluginNetworkFlagsVariableName,
                PolicyType.Script);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificatePolicyState.HaveNetworkFlags())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificatePolicyState.UnsetNetworkFlags();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginSkipLicenseVariableFormat,
                Constants.PluginSkipLicenseVariableName,
                Constants.EnabledElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateLicenseState.HaveSkip())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateLicenseState.DisableSkip();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.PluginSkipLicenseVariableFormat,
                Constants.PluginSkipLicenseVariableName,
                Constants.TypesElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateLicenseState.HaveSkipTypes())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateLicenseState.SetSkipTypes(
                            LicenseType.None);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginStorageTypeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateGlobalState.HaveStorageType())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateGlobalState.UnsetStorageType();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginSdkModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (!CertificateSdkMode.IsDefault())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateSdkMode.ResetToDefault();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

#if DEMO_KEY_PAIRS || DEMO_EDITION
            name = Constants.PluginDemoModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (!CertificateDemoMode.IsDefault())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateDemoMode.ResetToDefault();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginTestModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (!CertificateTestMode.IsDefault())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateTestMode.ResetToDefault();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = Constants.PluginFailSafeModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (!CertificateFailSafeMode.IsDefault())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateFailSafeMode.ResetToDefault();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

#if NETWORK
            name = Constants.PluginOfflineModeVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (Utility.InOfflineMode())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        Utility.SetOfflineMode(false);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
            name = Constants.PluginFeaturesVariableName;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (CertificateGlobalState.HaveExtraFeatures())
                {
                    if (withReset)
                    {
                        /* NO RESULT */
                        CertificateGlobalState.UnsetExtraFeatures();
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }
#endif

            ///////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            name = String.Format(
                Constants.InterpreterCreationVariableFormat,
                Constants.InterpreterCreationVariableName,
                Constants.PersistentElementName);

            bool? persistent = null;

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                Result value = null;

                if (interpreter != null)
                {
                    if ((interpreter != null) &&
                        (interpreter.GetVariableValue(
                            Constants.ContextGetVariableFlags, name,
                            ref value, ref result) != ReturnCode.Ok))
                    {
                        return ReturnCode.Error;
                    }

                    if (Value.GetNullableBoolean2(
                            value, ValueFlags.AnyBoolean, cultureInfo,
                            ref persistent, ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    if ((persistent != null) && (bool)persistent)
                    {
                        if (list == null)
                            list = new StringList();

                        list.Add(name);
                        count++;
                    }
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            name = String.Format(
                Constants.InterpreterCreationVariableFormat,
                Constants.InterpreterCreationVariableName,
                Constants.EnabledElementName);

            if (!noGlobalOnly &&
                (ignoreChanged || HasChanged(clientData, name)))
            {
                if (IsInterpreterCreationDisabled())
                {
                    if (withReset)
                    {
                        if (persistent == null)
                            persistent = false; // TODO: Good default?

                        /* NO RESULT */
                        EnableInterpreterCreation(
                            (bool)persistent);
                    }

                    if (list == null)
                        list = new StringList();

                    list.Add(name);
                    count++;
                }

                if (resetChanged)
                {
                    /* IGNORED */
                    SignalUnchanged(clientData, name);
                }
            }

            ///////////////////////////////////////////////////////////////////

            if ((allowLocalPolicy != null) &&
                PolicyConfiguration.CheckForChanges(pluginData,
                    clientData, (bool)allowLocalPolicy, useDefaultPolicy,
                    withReset, ignoreChanged, resetChanged, ref count,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }
#endif

            ///////////////////////////////////////////////////////////////////

            result = list;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the writable script context variables from the
        /// interpreter and immediately applies them to the underlying
        /// global state.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to read the variables from.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for value conversion, which may be null.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow the policy local to the plugin to be used.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to process all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="resetChanged">
        /// Non-zero to clear the changed flag for each processed variable.
        /// </param>
        /// <param name="count">
        /// The running total of processed variables, which is incremented
        /// by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ExtractAndApplyVariables( /* CORE */
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            IClientData clientData,  /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            bool noGlobalOnly,       /* in */
            bool allowLocalPolicy,   /* in */
            bool ignoreChanged,      /* in */
            bool resetChanged,       /* in */
            ref int count,           /* in, out */
            ref Result error         /* out */
            )
        {
            ObjectDictionary variables = null;

            if (ExtractVariables(
                    interpreter, clientData, cultureInfo,
                    noGlobalOnly, ignoreChanged, ref variables,
                    ref count, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (ApplyVariables(interpreter,
                    pluginData, clientData, variables,
                    noGlobalOnly, allowLocalPolicy,
                    ignoreChanged, resetChanged, ref count,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies a previously saved snapshot of script context variables
        /// to the underlying global state.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context.
        /// </param>
        /// <param name="pluginData">
        /// The plugin associated with this context.
        /// </param>
        /// <param name="clientData">
        /// The client data used to track per-context state.
        /// </param>
        /// <param name="saveStateName">
        /// The name of the snapshot to apply.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to omit variables that apply to global state only.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow the policy local to the plugin to be used.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to apply all variables regardless of their changed
        /// state.
        /// </param>
        /// <param name="resetChanged">
        /// Non-zero to clear the changed flag for each applied variable.
        /// </param>
        /// <param name="count">
        /// The running total of processed variables, which is incremented
        /// by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ApplyVariables( /* CORE */
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            IClientData clientData,  /* in */
            string saveStateName,    /* in */
            bool noGlobalOnly,       /* in */
            bool allowLocalPolicy,   /* in */
            bool ignoreChanged,      /* in */
            bool resetChanged,       /* in */
            ref int count,           /* in, out */
            ref Result error         /* out */
            )
        {
            object value = null;

            if (!CertificateSharedOps.TryGetDataValue(
                    clientData, FormatSaveStateVariableName(
                    saveStateName), ref value, ref error))
            {
                return ReturnCode.Error;
            }

            ObjectDictionary variables = ToVariables(
                interpreter, clientData, value, ignoreChanged,
                ref error);

            if (variables == null)
                return ReturnCode.Error;

            if (ApplyVariables(interpreter,
                    pluginData, clientData, variables,
                    noGlobalOnly, allowLocalPolicy,
                    ignoreChanged, resetChanged,
                    ref count, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reduces the supplied dictionary so that it contains only the
        /// base variable names, with any array element indexes removed.
        /// </summary>
        /// <param name="variables">
        /// The dictionary of variables to reduce; upon success, receives
        /// the dictionary keyed by base variable name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode RemoveElementNames( /* CORE */
            ref ObjectDictionary variables, /* in, out */
            ref Result error                /* out */
            )
        {
            if (variables == null)
            {
                error = "invalid variables";
                return ReturnCode.Error;
            }

            ObjectDictionary localVariables = new ObjectDictionary();

            foreach (VariablePair pair in variables)
            {
                string varName = null;
                string varIndex = null; /* NOT USED */

                if (Parser.SplitVariableName(
                        pair.Key, ref varName, ref varIndex,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if ((varName == null) ||
                    localVariables.ContainsKey(varName))
                {
                    continue;
                }

                localVariables.Add(varName, null);
            }

            variables = localVariables;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves the value of the specified interpreter variable as a
        /// boolean.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to read the variable from.
        /// </param>
        /// <param name="name">
        /// The name of the variable to retrieve.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used for value conversion, which may be null.
        /// </param>
        /// <param name="globalFrame">
        /// Non-zero to read the variable from the global call frame;
        /// otherwise, the current call frame is used.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the boolean value of the variable.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetVariableAsBoolean(
            Interpreter interpreter, /* in */
            string name,             /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            bool globalFrame,        /* in */
            ref bool value,          /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            VariableFlags variableFlags = Constants.ContextGetVariableFlags;

            if (globalFrame)
                variableFlags |= VariableFlags.GlobalOnly;
            else
                variableFlags &= ~VariableFlags.GlobalOnly;

            if (CertificateDataOps.GetBoolean(
                    interpreter, cultureInfo, variableFlags,
                    name, ref value, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the supplied script context variables within the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the variables are set within.
        /// </param>
        /// <param name="variables">
        /// The dictionary of variables to set.
        /// </param>
        /// <param name="globalFrame">
        /// Non-zero to set the variables in the global call frame;
        /// otherwise, the current call frame is used.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode SetVariables(
            Interpreter interpreter,    /* in */
            ObjectDictionary variables, /* in */
            bool globalFrame,           /* in */
            ref Result error            /* out */
            )
        {
            int setOk = 0; /* NOT USED */

            return SetVariables(
                interpreter, variables, globalFrame, ref setOk, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the supplied script context variables within the specified
        /// interpreter, reporting how many were set successfully.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the variables are set within.
        /// </param>
        /// <param name="variables">
        /// The dictionary of variables to set.
        /// </param>
        /// <param name="globalFrame">
        /// Non-zero to set the variables in the global call frame;
        /// otherwise, the current call frame is used.
        /// </param>
        /// <param name="setOk">
        /// The running total of variables that were set successfully,
        /// which is updated by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode SetVariables(
            Interpreter interpreter,    /* in */
            ObjectDictionary variables, /* in */
            bool globalFrame,           /* in */
            ref int setOk,              /* in, out */
            ref Result error            /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            VariableFlags variableFlags = Constants.ContextSetVariableFlags;

            if (globalFrame)
                variableFlags |= VariableFlags.GlobalOnly;
            else
                variableFlags &= ~VariableFlags.GlobalOnly;

            if (interpreter.SetVariableValues(
                    variableFlags, null, variables, true,
                    ref setOk, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the supplied script context variables within the
        /// specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the variables are unset within.
        /// </param>
        /// <param name="variables">
        /// The dictionary of variables to unset.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode UnsetVariables( /* CORE */
            Interpreter interpreter,    /* in */
            ObjectDictionary variables, /* in */
            ref Result error            /* out */
            )
        {
            int unsetOk = 0; /* NOT USED */

            return UnsetVariables(
                interpreter, variables, ref unsetOk, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the supplied script context variables within the
        /// specified interpreter, reporting how many were unset
        /// successfully.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the variables are unset within.
        /// </param>
        /// <param name="variables">
        /// The dictionary of variables to unset.
        /// </param>
        /// <param name="unsetOk">
        /// The running total of variables that were unset successfully,
        /// which is updated by this method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode UnsetVariables( /* CORE */
            Interpreter interpreter,    /* in */
            ObjectDictionary variables, /* in */
            ref int unsetOk,            /* in, out */
            ref Result error            /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (variables == null)
            {
                error = "invalid variables";
                return ReturnCode.Error;
            }

            ObjectDictionary localVariables = new ObjectDictionary(
                (IDictionary<string, object>)variables);

            if (RemoveElementNames(
                    ref localVariables, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (interpreter.UnsetVariables(
                    Constants.ContextUnsetVariableFlags,
                    localVariables, true, ref unsetOk,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }
    }
}
