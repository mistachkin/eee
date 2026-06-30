/*
 * PolicyConfiguration.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using VariablePair = System.Collections.Generic.KeyValuePair<string, object>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helpers for synchronizing certificate policy configuration
    /// with interpreter variables.  This includes formatting the variable
    /// names used to hold policy property values, reading and writing those
    /// values, gathering the current policy state into variables, applying
    /// variable values back onto the active policy, and detecting or
    /// resetting policy changes.
    /// </summary>
    [ObjectId("0f8c646c-4c58-42ba-913d-0629c4bb4b7a")]
    internal static class PolicyConfiguration
    {
        /// <summary>
        /// Builds the interpreter variable name used to hold the value of a
        /// single policy property for the specified policy type.
        /// </summary>
        /// <param name="policyType">
        /// The type of policy the property belongs to.
        /// </param>
        /// <param name="propertyName">
        /// The name of the policy property.
        /// </param>
        /// <returns>
        /// The formatted variable name.
        /// </returns>
        private static string GetPropertyVariableName( /* CORE? */
            PolicyType policyType, /* in */
            string propertyName    /* in */
            )
        {
            return String.Format("{0}({1},{2})",
                Constants.PluginPolicyPropertyVariableName,
                policyType, propertyName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of the specified policy property from the
        /// interpreter variable that holds it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose variable holds the property value.
        /// </param>
        /// <param name="policyType">
        /// The type of policy the property belongs to.
        /// </param>
        /// <param name="propertyName">
        /// The name of the policy property to retrieve.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the property value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetPropertyValue( /* CORE? */
            Interpreter interpreter, /* in */
            PolicyType policyType,   /* in */
            string propertyName,     /* in */
            ref Result value,        /* out */
            ref Result error         /* out */
            )
        {
            string varName = GetPropertyVariableName(
                policyType, propertyName);

            if (interpreter.GetVariableValue(
                    Constants.ContextGetVariableFlags, varName,
                    ref value, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the value of the specified policy property into the
        /// interpreter variable that holds it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose variable will hold the property value.
        /// </param>
        /// <param name="policyType">
        /// The type of policy the property belongs to.
        /// </param>
        /// <param name="propertyName">
        /// The name of the policy property to set.
        /// </param>
        /// <param name="propertyValue">
        /// The value to store; its string representation is used, or null if
        /// the value itself is null.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SetPropertyValue( /* CORE? */
            Interpreter interpreter, /* in */
            PolicyType policyType,   /* in */
            string propertyName,     /* in */
            object propertyValue,    /* in */
            ref Result error         /* out */
            )
        {
            string varName = GetPropertyVariableName(
                policyType, propertyName);

            string varValue = (propertyValue != null) ?
                propertyValue.ToString() : null;

            if (interpreter.SetVariableValue(
                    Constants.ContextSetVariableFlags, varName,
                    varValue, null, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the interpreter variable that holds the value of the
        /// specified policy property.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose variable will be unset.
        /// </param>
        /// <param name="policyType">
        /// The type of policy the property belongs to.
        /// </param>
        /// <param name="propertyName">
        /// The name of the policy property to unset.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode UnsetPropertyValue( /* CORE? */
            Interpreter interpreter, /* in */
            PolicyType policyType,   /* in */
            string propertyName,     /* in */
            ref Result error         /* out */
            )
        {
            string varName = GetPropertyVariableName(
                policyType, propertyName);

            if (interpreter.UnsetVariable(
                    Constants.ContextUnsetVariableFlags,
                    varName, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified policy property has been
        /// marked as changed in the supplied client data.
        /// </summary>
        /// <param name="clientData">
        /// The client data that tracks which policy properties have changed.
        /// </param>
        /// <param name="policyType">
        /// The type of policy the property belongs to.
        /// </param>
        /// <param name="name">
        /// The name of the policy property to check.
        /// </param>
        /// <returns>
        /// Non-zero if the property has been marked as changed; otherwise,
        /// zero.
        /// </returns>
        private static bool HasPropertyChanged( /* CORE? */
            IClientData clientData, /* in */
            PolicyType policyType,  /* in */
            string name             /* in */
            )
        {
            return CertificateSharedOps.TryHasDataValue(
                clientData, ScriptContext.FormatChangedVariableName(
                GetPropertyVariableName(policyType, name)));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the specified policy property as changed in the supplied
        /// client data.
        /// </summary>
        /// <param name="clientData">
        /// The client data that tracks which policy properties have changed.
        /// </param>
        /// <param name="policyType">
        /// The type of policy the property belongs to.
        /// </param>
        /// <param name="name">
        /// The name of the policy property to mark as changed.
        /// </param>
        /// <returns>
        /// Non-zero if the property was successfully marked as changed;
        /// otherwise, zero.
        /// </returns>
        public static bool SignalPropertyChanged( /* CORE? */
            IClientData clientData, /* in */
            PolicyType policyType,  /* in */
            string name             /* in */
            )
        {
            Result error = null; /* NOT USED */

            return CertificateSharedOps.TrySetDataValue(
                clientData, ScriptContext.FormatChangedVariableName(
                GetPropertyVariableName(policyType, name)), null,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the changed mark for the specified policy property in the
        /// supplied client data.
        /// </summary>
        /// <param name="clientData">
        /// The client data that tracks which policy properties have changed.
        /// </param>
        /// <param name="policyType">
        /// The type of policy the property belongs to.
        /// </param>
        /// <param name="name">
        /// The name of the policy property to mark as unchanged.
        /// </param>
        /// <returns>
        /// Non-zero if the changed mark was successfully cleared; otherwise,
        /// zero.
        /// </returns>
        public static bool SignalPropertyUnchanged( /* CORE? */
            IClientData clientData, /* in */
            PolicyType policyType,  /* in */
            string name             /* in */
            )
        {
            Result error = null; /* NOT USED */

            return CertificateSharedOps.TryUnsetDataValue(
                clientData, ScriptContext.FormatChangedVariableName(
                GetPropertyVariableName(policyType, name)), ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current state of the specified policy, returning its key
        /// name, key ring name, execution policy, and associated flags.  When
        /// <paramref name="allowLocalPolicy" /> is set and plugin data is
        /// supplied, the local (plugin-specific) policy state is used;
        /// otherwise, the global policy state is used.
        /// </summary>
        /// <param name="pluginData">
        /// The optional plugin data providing local policy state.
        /// </param>
        /// <param name="policyType">
        /// The type of policy whose state is retrieved.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow use of the local (plugin-specific) policy state
        /// when plugin data is available.
        /// </param>
        /// <param name="keyName">
        /// Receives the key name for the policy.
        /// </param>
        /// <param name="keyRingName">
        /// Receives the key ring name for the policy.
        /// </param>
        /// <param name="policy">
        /// Receives the execution policy.
        /// </param>
        /// <param name="scriptFlags">
        /// Receives the script flags for the policy.
        /// </param>
        /// <param name="pathFlags">
        /// Receives the path flags for the policy.
        /// </param>
        /// <param name="networkFlags">
        /// Receives the network flags for the policy.
        /// </param>
        public static void GetState( /* CORE? */
            IPluginData pluginData,        /* in: OPTIONAL */
            PolicyType policyType,         /* in */
            bool allowLocalPolicy,         /* in */
            out string keyName,            /* out */
            out string keyRingName,        /* out */
            out ExecutionPolicy? policy,   /* out */
            out ScriptFlags? scriptFlags,  /* out */
            out PathFlags? pathFlags,      /* out */
            out NetworkFlags? networkFlags /* out */
            )
        {
            if (allowLocalPolicy && (pluginData != null))
            {
                keyName = null;

                /* IGNORED */
                CertificatePolicyOps.GetKeyName(
                    pluginData, policyType, ref keyName);

                keyRingName = null;

                /* IGNORED */
                CertificatePolicyOps.GetKeyRingName(
                    pluginData, policyType, ref keyRingName);

                ExecutionPolicy localPolicy = ExecutionPolicy.Undefined;

                /* IGNORED */
                CertificatePolicyOps.GetPolicy(
                    pluginData, policyType, ref localPolicy);

                policy = localPolicy;

                ScriptFlags localScriptFlags = ScriptFlags.None;

                /* IGNORED */
                CertificatePolicyOps.GetScriptFlags(
                    pluginData, policyType, ref localScriptFlags);

                PathFlags localPathFlags = PathFlags.None;

                /* IGNORED */
                CertificatePolicyOps.GetPathFlags(
                    pluginData, policyType, ref localPathFlags);

                NetworkFlags localNetworkFlags = NetworkFlags.None;

                /* IGNORED */
                CertificatePolicyOps.GetNetworkFlags(
                    pluginData, policyType, ref localNetworkFlags);

                scriptFlags = localScriptFlags;
                pathFlags = localPathFlags;
                networkFlags = localNetworkFlags;
            }
            else
            {
                keyName = CertificatePolicyOps.GetKeyName(
                    policyType);

                keyRingName = CertificatePolicyOps.GetKeyRingName(
                    policyType);

                policy = CertificatePolicyOps.GetPolicy(
                    policyType);

                scriptFlags = CertificatePolicyOps.GetScriptFlags(
                    policyType);

                pathFlags = CertificatePolicyOps.GetPathFlags(
                    policyType);

                networkFlags = CertificatePolicyOps.GetNetworkFlags(
                    policyType);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds policy configuration variables to the supplied dictionary.
        /// When <paramref name="nameOnly" /> is set, only the policy variable
        /// name prefixes are added; otherwise, the current value of every
        /// policy property for every policy type is added.
        /// </summary>
        /// <param name="pluginData">
        /// The optional plugin data providing local policy state.
        /// </param>
        /// <param name="variables">
        /// The dictionary to which the policy variables are added.  No action
        /// is taken if this is null.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow use of the local (plugin-specific) policy state
        /// when plugin data is available.
        /// </param>
        /// <param name="nameOnly">
        /// Non-zero to add only the policy variable name prefixes instead of
        /// the full set of property values.
        /// </param>
        public static void GetVariables( /* CORE? */
            IPluginData pluginData,     /* in: OPTIONAL */
            ObjectDictionary variables, /* in */
            bool allowLocalPolicy,      /* in */
            bool nameOnly               /* in */
            )
        {
            if (variables == null)
                return;

            if (nameOnly)
            {
                variables.Add(
                    Constants.PluginPolicyPropertyVariableName, null);

                variables.Add(
                    Constants.PluginLicensePolicyVariableName, null);

                return;
            }

            IEnumerable<PolicyType> policyTypes =
                CertificatePolicyOps.GetPolicyTypes();

            if (policyTypes == null)
                return;

            foreach (PolicyType policyType in policyTypes)
            {
                string keyName; /* REUSED */
                string keyRingName; /* REUSED */
                ExecutionPolicy? policy; /* REUSED */
                ScriptFlags? scriptFlags; /* REUSED */
                PathFlags? pathFlags; /* REUSED */
                NetworkFlags? networkFlags; /* REUSED */

                ///////////////////////////////////////////////////////////////

                GetState(pluginData,
                    policyType, allowLocalPolicy, out keyName,
                    out keyRingName, out policy, out scriptFlags,
                    out pathFlags, out networkFlags);

                ///////////////////////////////////////////////////////////////

                variables.Add(GetPropertyVariableName(
                    policyType, Constants.KeyNamePropertyName),
                    keyName);

                variables.Add(GetPropertyVariableName(
                    policyType, Constants.KeyRingNamePropertyName),
                    keyRingName);

                variables.Add(GetPropertyVariableName(
                    policyType, Constants.CurrentPolicyPropertyName),
                    policy);

                variables.Add(GetPropertyVariableName(
                    policyType, Constants.ScriptFlagsPropertyName),
                    scriptFlags);

                variables.Add(GetPropertyVariableName(
                    policyType, Constants.PathFlagsPropertyName),
                    pathFlags);

                variables.Add(GetPropertyVariableName(
                    policyType, Constants.NetworkFlagsPropertyName),
                    networkFlags);

                ///////////////////////////////////////////////////////////////

                if (policyType == PolicyType.License)
                {
                    variables.Add(
                        Constants.PluginLicensePolicyVariableName,
                        policy.ToString());
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts policy property values from the interpreter variables
        /// into a dictionary, parsing flag and enumeration values as
        /// needed.  Only properties that have changed are extracted
        /// unless <paramref name="ignoreChanged" /> is set.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose variables are read.
        /// </param>
        /// <param name="clientData">
        /// The optional client data tracking which policy properties have
        /// changed.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture information used when parsing enumeration
        /// values.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to extract every property regardless of whether it has
        /// been marked as changed.
        /// </param>
        /// <param name="variables">
        /// Receives the extracted variables.  If a dictionary is supplied,
        /// the extracted values are merged into it; otherwise, a new
        /// dictionary is created.
        /// </param>
        /// <param name="count">
        /// Incremented by the number of variables that were extracted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ExtractVariables( /* CORE? */
            Interpreter interpreter,        /* in */
            IClientData clientData,         /* in: OPTIONAL */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            bool ignoreChanged,             /* in */
            ref ObjectDictionary variables, /* out */
            ref int count,                  /* in, out */
            ref Result error                /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            IEnumerable<PolicyType> policyTypes =
                CertificatePolicyOps.GetPolicyTypes();

            if (policyTypes == null)
            {
                error = "policy types are not available";
                return ReturnCode.Error;
            }

            ObjectDictionary localVariables = new ObjectDictionary();

            foreach (PolicyType policyType in policyTypes)
            {
                string propertyName; /* REUSED */
                string varName; /* REUSED */
                Result value; /* REUSED */
                object enumValue; /* REUSED */

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.KeyNamePropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (interpreter.DoesVariableExist(
                            Constants.ContextExistVariableFlags,
                            varName) == ReturnCode.Ok)
                    {
                        value = null;

                        if (interpreter.GetVariableValue(
                                Constants.ContextGetVariableFlags, varName,
                                ref value, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        if (!String.IsNullOrEmpty(value))
                        {
                            localVariables[varName] = (string)value;
                            count++;
                        }
                    }
                    else
                    {
                        /* IGNORED */
                        localVariables.Remove(varName);

                        count++;
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.KeyRingNamePropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (interpreter.DoesVariableExist(
                            Constants.ContextExistVariableFlags,
                            varName) == ReturnCode.Ok)
                    {
                        value = null;

                        if (interpreter.GetVariableValue(
                                Constants.ContextGetVariableFlags, varName,
                                ref value, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        if (!String.IsNullOrEmpty(value))
                        {
                            localVariables[varName] = (string)value;
                            count++;
                        }
                    }
                    else
                    {
                        /* IGNORED */
                        localVariables.Remove(varName);

                        count++;
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.CurrentPolicyPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (interpreter.DoesVariableExist(
                            Constants.ContextExistVariableFlags,
                            varName) == ReturnCode.Ok)
                    {
                        value = null;

                        if (interpreter.GetVariableValue(
                                Constants.ContextGetVariableFlags, varName,
                                ref value, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        if (!String.IsNullOrEmpty(value))
                        {
                            enumValue = Utility.TryParseFlagsEnum(
                                interpreter, typeof(ExecutionPolicy),
                                null, value, cultureInfo, true, true,
                                true, ref error);

                            if (!(enumValue is ExecutionPolicy))
                                return ReturnCode.Error;

                            localVariables[varName] = (ExecutionPolicy)enumValue;
                            count++;
                        }
                    }
                    else
                    {
                        /* IGNORED */
                        localVariables.Remove(varName);

                        count++;
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.ScriptFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (interpreter.DoesVariableExist(
                            Constants.ContextExistVariableFlags,
                            varName) == ReturnCode.Ok)
                    {
                        value = null;

                        if (interpreter.GetVariableValue(
                                Constants.ContextGetVariableFlags, varName,
                                ref value, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        if (!String.IsNullOrEmpty(value))
                        {
                            enumValue = Utility.TryParseFlagsEnum(
                                interpreter, typeof(ScriptFlags),
                                null, value, cultureInfo, true, true,
                                true, ref error);

                            if (!(enumValue is ScriptFlags))
                                return ReturnCode.Error;

                            localVariables[varName] = (ScriptFlags)enumValue;
                            count++;
                        }
                    }
                    else
                    {
                        /* IGNORED */
                        localVariables.Remove(varName);

                        count++;
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.PathFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (interpreter.DoesVariableExist(
                            Constants.ContextExistVariableFlags,
                            varName) == ReturnCode.Ok)
                    {
                        value = null;

                        if (interpreter.GetVariableValue(
                                Constants.ContextGetVariableFlags, varName,
                                ref value, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        if (!String.IsNullOrEmpty(value))
                        {
                            enumValue = Utility.TryParseFlagsEnum(
                                interpreter, typeof(PathFlags),
                                null, value, cultureInfo, true, true,
                                true, ref error);

                            if (!(enumValue is PathFlags))
                                return ReturnCode.Error;

                            localVariables[varName] = (PathFlags)enumValue;
                            count++;
                        }
                    }
                    else
                    {
                        /* IGNORED */
                        localVariables.Remove(varName);

                        count++;
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.NetworkFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (interpreter.DoesVariableExist(
                            Constants.ContextExistVariableFlags,
                            varName) == ReturnCode.Ok)
                    {
                        value = null;

                        if (interpreter.GetVariableValue(
                                Constants.ContextGetVariableFlags, varName,
                                ref value, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        if (!String.IsNullOrEmpty(value))
                        {
                            enumValue = Utility.TryParseFlagsEnum(
                                interpreter, typeof(NetworkFlags),
                                null, value, cultureInfo, true, true,
                                true, ref error);

                            if (!(enumValue is NetworkFlags))
                                return ReturnCode.Error;

                            localVariables[varName] = (NetworkFlags)enumValue;
                            count++;
                        }
                    }
                    else
                    {
                        /* IGNORED */
                        localVariables.Remove(varName);

                        count++;
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.PluginLicensePolicyVariableName;

                if ((policyType == PolicyType.License) && (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName)))
                {
                    varName = propertyName;

                    if (interpreter.DoesVariableExist(
                            Constants.ContextExistVariableFlags,
                            varName) == ReturnCode.Ok)
                    {
                        value = null;

                        if (interpreter.GetVariableValue(
                                Constants.ContextGetVariableFlags, varName,
                                ref value, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        if (!String.IsNullOrEmpty(value))
                        {
                            enumValue = Utility.TryParseFlagsEnum(
                                interpreter, typeof(ExecutionPolicy),
                                null, value, cultureInfo, true, true,
                                true, ref error);

                            if (!(enumValue is ExecutionPolicy))
                                return ReturnCode.Error;

                            localVariables[varName] = (ExecutionPolicy)enumValue;
                            count++;
                        }
                    }
                    else
                    {
                        /* IGNORED */
                        localVariables.Remove(varName);

                        count++;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (variables != null)
            {
                if (localVariables != null)
                {
                    foreach (VariablePair pair in localVariables)
                        variables[pair.Key] = pair.Value;
                }
            }
            else
            {
                variables = localVariables;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the value of a named variable from the supplied
        /// dictionary, optionally requiring it to be of a specific type.
        /// </summary>
        /// <param name="variables">
        /// The dictionary to look the variable up in.
        /// </param>
        /// <param name="name">
        /// The name of the variable to retrieve.
        /// </param>
        /// <param name="type">
        /// The optional type the value must be an exact instance of; when
        /// null, no type check is performed.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the variable value.
        /// </param>
        /// <returns>
        /// Non-zero if the variable was found, was non-null, and matched the
        /// required type (if any); otherwise, zero.
        /// </returns>
        private static bool TryGetVariable( /* CORE? */
            ObjectDictionary variables, /* in */
            string name,                /* in */
            Type type,                  /* in: OPTIONAL */
            ref object value            /* out */
            )
        {
            if (variables == null)
                return false;

            if (name == null)
                return false;

            if (!variables.TryGetValue(name, out value))
                return false;

            if (value == null)
                return false;

            if ((type != null) &&
                !Object.ReferenceEquals(value.GetType(), type))
            {
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the global policy of the specified type is set,
        /// optionally checking against the default policy.
        /// </summary>
        /// <param name="policyType">
        /// The type of policy to check.
        /// </param>
        /// <param name="useDefaultPolicy">
        /// Non-zero to check whether the default policy is present;
        /// otherwise, the current policy is checked.
        /// </param>
        /// <returns>
        /// Non-zero if the requested policy is present; otherwise, zero.
        /// </returns>
        private static bool HavePolicy( /* CORE? */
            PolicyType policyType, /* in */
            bool useDefaultPolicy  /* in */
            )
        {
            return useDefaultPolicy ?
                CertificatePolicyOps.HaveDefaultPolicy(policyType) :
                CertificatePolicyOps.HavePolicy(policyType);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the local (plugin-specific) policy of the
        /// specified type is set, optionally checking against the default
        /// policy.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data providing local policy state.
        /// </param>
        /// <param name="policyType">
        /// The type of policy to check.
        /// </param>
        /// <param name="useDefaultPolicy">
        /// Non-zero to check whether the default policy is present;
        /// otherwise, the current policy is checked.
        /// </param>
        /// <returns>
        /// Non-zero if the requested policy is present; otherwise, zero.
        /// </returns>
        private static bool HavePolicy( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            bool useDefaultPolicy   /* in */
            )
        {
            return useDefaultPolicy ?
                CertificatePolicyOps.HaveDefaultPolicy(
                    pluginData, policyType) :
                CertificatePolicyOps.HavePolicy(
                    pluginData, policyType);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gathers the current policy property values directly from the
        /// policy state into a dictionary of variables.  Only properties
        /// that have changed are gathered unless
        /// <paramref name="ignoreChanged" /> is set.
        /// </summary>
        /// <param name="pluginData">
        /// The optional plugin data providing local policy state.
        /// </param>
        /// <param name="clientData">
        /// The optional client data tracking which policy properties have
        /// changed.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow use of the local (plugin-specific) policy state
        /// when plugin data is available.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to gather every property regardless of whether it has
        /// been marked as changed.
        /// </param>
        /// <param name="variables">
        /// Receives the gathered variables.  If a dictionary is supplied, the
        /// gathered values are merged into it; otherwise, a new dictionary is
        /// created.
        /// </param>
        /// <param name="count">
        /// Incremented by the number of variables that were gathered.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GatherVariables( /* CORE? */
            IPluginData pluginData,         /* in: OPTIONAL */
            IClientData clientData,         /* in: OPTIONAL */
            bool allowLocalPolicy,          /* in */
            bool ignoreChanged,             /* in */
            ref ObjectDictionary variables, /* in, out */
            ref int count,                  /* in, out */
            ref Result error                /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes =
                CertificatePolicyOps.GetPolicyTypes();

            if (policyTypes == null)
            {
                error = "policy types are not available";
                return ReturnCode.Error;
            }

            ObjectDictionary localVariables = new ObjectDictionary();

            foreach (PolicyType policyType in policyTypes)
            {
                string propertyName; /* REUSED */
                string varName; /* REUSED */

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.KeyNamePropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        string keyName = null;

                        if (CertificatePolicyOps.GetKeyName(
                                pluginData, policyType, ref keyName) &&
                            ScriptContext.TrySetVariable(
                                localVariables, varName, keyName, true))
                        {
                            count++;
                        }
                    }
                    else
                    {
                        if (ScriptContext.TrySetVariable(
                                localVariables, varName,
                                CertificatePolicyOps.GetKeyName(policyType),
                                true))
                        {
                            count++;
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.KeyRingNamePropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        string keyRingName = null;

                        if (CertificatePolicyOps.GetKeyRingName(
                                pluginData, policyType, ref keyRingName) &&
                            ScriptContext.TrySetVariable(
                                localVariables, varName, keyRingName, true))
                        {
                            count++;
                        }
                    }
                    else
                    {
                        if (ScriptContext.TrySetVariable(
                                localVariables, varName,
                                CertificatePolicyOps.GetKeyRingName(policyType),
                                true))
                        {
                            count++;
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.CurrentPolicyPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        ExecutionPolicy policy = ExecutionPolicy.Undefined;

                        if (CertificatePolicyOps.GetPolicy(
                                pluginData, policyType, ref policy) &&
                            ScriptContext.TrySetVariable(
                                localVariables, varName, policy, true))
                        {
                            count++;
                        }
                    }
                    else
                    {
                        if (ScriptContext.TrySetVariable(
                                localVariables, varName,
                                CertificatePolicyOps.GetPolicy(policyType),
                                true))
                        {
                            count++;
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.ScriptFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        ScriptFlags scriptFlags = ScriptFlags.None;

                        if (CertificatePolicyOps.GetScriptFlags(
                                pluginData, policyType, ref scriptFlags) &&
                            ScriptContext.TrySetVariable(
                                localVariables, varName, scriptFlags, true))
                        {
                            count++;
                        }
                    }
                    else
                    {
                        if (ScriptContext.TrySetVariable(
                                localVariables, varName,
                                CertificatePolicyOps.GetScriptFlags(policyType),
                                true))
                        {
                            count++;
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.PathFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        PathFlags pathFlags = PathFlags.None;

                        if (CertificatePolicyOps.GetPathFlags(
                                pluginData, policyType, ref pathFlags) &&
                            ScriptContext.TrySetVariable(
                                localVariables, varName, pathFlags, true))
                        {
                            count++;
                        }
                    }
                    else
                    {
                        if (ScriptContext.TrySetVariable(
                                localVariables, varName,
                                CertificatePolicyOps.GetPathFlags(policyType),
                                true))
                        {
                            count++;
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.NetworkFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        NetworkFlags networkFlags = NetworkFlags.None;

                        if (CertificatePolicyOps.GetNetworkFlags(
                                pluginData, policyType, ref networkFlags) &&
                            ScriptContext.TrySetVariable(
                                localVariables, varName, networkFlags, true))
                        {
                            count++;
                        }
                    }
                    else
                    {
                        if (ScriptContext.TrySetVariable(
                                localVariables, varName,
                                CertificatePolicyOps.GetNetworkFlags(policyType),
                                true))
                        {
                            count++;
                        }
                    }
                }

                ///////////////////////////////////////////////////////////////

                //
                // HACK: This "if" block is only executed for one of the loop
                //       variable values (i.e. "License").
                //
                propertyName = Constants.PluginLicensePolicyVariableName;

                if ((policyType == PolicyType.License) && (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName)))
                {
                    varName = propertyName;

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        ExecutionPolicy policy = ExecutionPolicy.Undefined;

                        if (CertificatePolicyOps.GetPolicy(
                                pluginData, policyType, ref policy) &&
                            ScriptContext.TrySetVariable(
                                localVariables, varName, policy, true))
                        {
                            count++;
                        }
                    }
                    else
                    {
                        if (ScriptContext.TrySetVariable(
                                localVariables, varName,
                                CertificatePolicyOps.GetPolicy(policyType),
                                true))
                        {
                            count++;
                        }
                    }
                }
            }

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
        /// Applies policy property values from the supplied dictionary back
        /// onto the policy state, setting or unsetting each property as
        /// appropriate and incrementing the global change count.  Only
        /// properties that have changed are applied unless
        /// <paramref name="ignoreChanged" /> is set.
        /// </summary>
        /// <param name="pluginData">
        /// The optional plugin data providing local policy state.
        /// </param>
        /// <param name="clientData">
        /// The optional client data tracking which policy properties have
        /// changed.
        /// </param>
        /// <param name="variables">
        /// The dictionary of variable values to apply.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow use of the local (plugin-specific) policy state
        /// when plugin data is available.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to apply every property regardless of whether it has been
        /// marked as changed.
        /// </param>
        /// <param name="resetChanged">
        /// Non-zero to clear the changed mark for each property after it has
        /// been applied.
        /// </param>
        /// <param name="count">
        /// Incremented by the number of properties that were applied.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ApplyVariables( /* CORE? */
            IPluginData pluginData,     /* in: OPTIONAL */
            IClientData clientData,     /* in: OPTIONAL */
            ObjectDictionary variables, /* in */
            bool allowLocalPolicy,      /* in */
            bool ignoreChanged,         /* in */
            bool resetChanged,          /* in */
            ref int count,              /* in, out */
            ref Result error            /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes =
                CertificatePolicyOps.GetPolicyTypes();

            if (policyTypes == null)
            {
                error = "policy types are not available";
                return ReturnCode.Error;
            }

            foreach (PolicyType policyType in policyTypes)
            {
                string propertyName; /* REUSED */
                string varName; /* REUSED */
                object value; /* REUSED */

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.KeyNamePropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    value = null;

                    if (TryGetVariable(
                            variables, varName, typeof(string), ref value))
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetKeyName(
                                pluginData, policyType,
                                (string)value);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetKeyName(
                                policyType, (string)value);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }
                    else
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetKeyName(
                                pluginData, policyType);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetKeyName(
                                policyType);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.KeyRingNamePropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    value = null;

                    if (TryGetVariable(
                            variables, varName, typeof(string), ref value))
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetKeyRingName(
                                pluginData, policyType,
                                (string)value);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetKeyRingName(
                                policyType, (string)value);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }
                    else
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetKeyRingName(
                                pluginData, policyType);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetKeyRingName(
                                policyType);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.CurrentPolicyPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    value = null;

                    if (TryGetVariable(
                            variables, varName, typeof(ExecutionPolicy),
                            ref value))
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetPolicy(
                                pluginData, policyType,
                                (ExecutionPolicy)value);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetPolicy(
                                policyType, (ExecutionPolicy)value);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }
                    else
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetPolicy(
                                pluginData, policyType);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetPolicy(
                                policyType);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.ScriptFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    value = null;

                    if (TryGetVariable(
                            variables, varName, typeof(ScriptFlags),
                            ref value))
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetScriptFlags(
                                pluginData, policyType,
                                (ScriptFlags)value);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetScriptFlags(
                                policyType, (ScriptFlags)value);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }
                    else
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetScriptFlags(
                                pluginData, policyType);
                        }
                        else
                        {
                            //
                            // HACK: Do not call UnsetScriptFlags;
                            //       this must revert to default,
                            //       not none.
                            //
                            /* IGNORED */
                            CertificatePolicyOps.ResetScriptFlags(
                                policyType);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.PathFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    value = null;

                    if (TryGetVariable(
                            variables, varName, typeof(PathFlags),
                            ref value))
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetPathFlags(
                                pluginData, policyType,
                                (PathFlags)value);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetPathFlags(
                                policyType, (PathFlags)value);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }
                    else
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetPathFlags(
                                pluginData, policyType);
                        }
                        else
                        {
                            //
                            // HACK: Do not call UnsetPathFlags;
                            //       this must revert to default,
                            //       not none.
                            //
                            /* IGNORED */
                            CertificatePolicyOps.ResetPathFlags(
                                policyType);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.NetworkFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    value = null;

                    if (TryGetVariable(
                            variables, varName, typeof(NetworkFlags),
                            ref value))
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetNetworkFlags(
                                pluginData, policyType,
                                (NetworkFlags)value);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetNetworkFlags(
                                policyType, (NetworkFlags)value);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }
                    else
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetNetworkFlags(
                                pluginData, policyType);
                        }
                        else
                        {
                            //
                            // HACK: Do not call UnsetNetworkFlags;
                            //       this must revert to default,
                            //       not none.
                            //
                            /* IGNORED */
                            CertificatePolicyOps.ResetNetworkFlags(
                                policyType);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                //
                // HACK: This "if" block is only executed for one of the loop
                //       variable values (i.e. "License").
                //
                propertyName = Constants.PluginLicensePolicyVariableName;

                if ((policyType == PolicyType.License) && (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName)))
                {
                    varName = propertyName;

                    value = null;

                    if (TryGetVariable(
                            variables, varName, typeof(ExecutionPolicy),
                            ref value))
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetPolicy(
                                pluginData, policyType,
                                (ExecutionPolicy)value);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.SetPolicy(
                                policyType, (ExecutionPolicy)value);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }
                    else
                    {
                        if (allowLocalPolicy && (pluginData != null))
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetPolicy(
                                pluginData, policyType);
                        }
                        else
                        {
                            /* IGNORED */
                            CertificatePolicyOps.UnsetPolicy(
                                policyType);
                        }

                        /* IGNORED */
                        CertificateGlobalState.IncrementChangeCount();

                        count++;
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (ScriptContext.ShouldDisableInterpreterCreation(
                    pluginData, allowLocalPolicy))
            {
                /* NO RESULT */
                ScriptContext.DisableInterpreterCreation(true); /* throw */
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the policy state for properties that are currently set,
        /// optionally unsetting (or resetting to default) each one that is
        /// found.  Only properties that have changed are checked unless
        /// <paramref name="ignoreChanged" /> is set.
        /// </summary>
        /// <param name="pluginData">
        /// The optional plugin data providing local policy state.
        /// </param>
        /// <param name="clientData">
        /// The optional client data tracking which policy properties have
        /// changed.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow use of the local (plugin-specific) policy state
        /// when plugin data is available.
        /// </param>
        /// <param name="useDefaultPolicy">
        /// When non-null, controls whether the default policy (rather
        /// than the current policy) is used when checking for the
        /// presence of a policy.
        /// </param>
        /// <param name="withReset">
        /// Non-zero to unset (or reset to default) each property that
        /// is found to be set.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to check every property regardless of whether it has been
        /// marked as changed.
        /// </param>
        /// <param name="resetChanged">
        /// Non-zero to clear the changed mark for each property after it has
        /// been checked.
        /// </param>
        /// <param name="count">
        /// Incremented by the number of set properties that were found.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode CheckForChanges( /* CORE? */
            IPluginData pluginData, /* in: OPTIONAL */
            IClientData clientData, /* in: OPTIONAL */
            bool allowLocalPolicy,  /* in */
            bool? useDefaultPolicy, /* in */
            bool withReset,         /* in */
            bool ignoreChanged,     /* in */
            bool resetChanged,      /* in */
            ref int count,          /* in, out */
            ref Result error        /* out */
            )
        {
            IEnumerable<PolicyType> policyTypes =
                CertificatePolicyOps.GetPolicyTypes();

            if (policyTypes == null)
            {
                error = "policy types are not available";
                return ReturnCode.Error;
            }

            bool useGlobalDefaultPolicy = true;
            bool useLocalDefaultPolicy = false;

            if (useDefaultPolicy != null)
            {
                useGlobalDefaultPolicy = (bool)useDefaultPolicy;
                useLocalDefaultPolicy = (bool)useDefaultPolicy;
            }

            StringList list = null;

            foreach (PolicyType policyType in policyTypes)
            {
                string propertyName; /* REUSED */
                string varName; /* REUSED */

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.KeyNamePropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        if (CertificatePolicyOps.HaveKeyName(
                                pluginData, policyType))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetKeyName(
                                    pluginData, policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }
                    else
                    {
                        if (CertificatePolicyOps.HaveKeyName(
                                policyType))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetKeyName(
                                    policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.KeyRingNamePropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        if (CertificatePolicyOps.HaveKeyRingName(
                                pluginData, policyType))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetKeyRingName(
                                    pluginData, policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }
                    else
                    {
                        if (CertificatePolicyOps.HaveKeyRingName(
                                policyType))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetKeyRingName(
                                    policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.CurrentPolicyPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        if (HavePolicy(
                                pluginData, policyType,
                                useLocalDefaultPolicy))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetPolicy(
                                    pluginData, policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }
                    else
                    {
                        if (HavePolicy(
                                policyType, useGlobalDefaultPolicy))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetPolicy(
                                    policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.ScriptFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        if (CertificatePolicyOps.HaveScriptFlags(
                                pluginData, policyType))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetScriptFlags(
                                    pluginData, policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }
                    else
                    {
                        if (CertificatePolicyOps.HaveScriptFlags(
                                policyType))
                        {
                            if (withReset)
                            {
                                //
                                // HACK: Do not call UnsetScriptFlags;
                                //       this must revert to default,
                                //       not none.
                                //
                                /* IGNORED */
                                CertificatePolicyOps.ResetScriptFlags(
                                    policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.PathFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        if (CertificatePolicyOps.HavePathFlags(
                                pluginData, policyType))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetPathFlags(
                                    pluginData, policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }
                    else
                    {
                        if (CertificatePolicyOps.HavePathFlags(
                                policyType))
                        {
                            if (withReset)
                            {
                                //
                                // HACK: Do not call UnsetPathFlags;
                                //       this must revert to default,
                                //       not none.
                                //
                                /* IGNORED */
                                CertificatePolicyOps.ResetPathFlags(
                                    policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                propertyName = Constants.NetworkFlagsPropertyName;

                if (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName))
                {
                    varName = GetPropertyVariableName(
                        policyType, propertyName);

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        if (CertificatePolicyOps.HaveNetworkFlags(
                                pluginData, policyType))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetNetworkFlags(
                                    pluginData, policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }
                    else
                    {
                        if (CertificatePolicyOps.HaveNetworkFlags(
                                policyType))
                        {
                            if (withReset)
                            {
                                //
                                // HACK: Do not call UnsetNetworkFlags;
                                //       this must revert to default,
                                //       not none.
                                //
                                /* IGNORED */
                                CertificatePolicyOps.ResetNetworkFlags(
                                    policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }

                ///////////////////////////////////////////////////////////////

                //
                // HACK: This "if" block is only executed for one of the loop
                //       variable values (i.e. "License").
                //
                propertyName = Constants.PluginLicensePolicyVariableName;

                if ((policyType == PolicyType.License) && (ignoreChanged ||
                    HasPropertyChanged(clientData, policyType, propertyName)))
                {
                    varName = propertyName;

                    if (allowLocalPolicy && (pluginData != null))
                    {
                        if (HavePolicy(
                                pluginData, policyType,
                                useLocalDefaultPolicy))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetPolicy(
                                    pluginData, policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }
                    else
                    {
                        if (HavePolicy(
                                policyType, useGlobalDefaultPolicy))
                        {
                            if (withReset)
                            {
                                /* IGNORED */
                                CertificatePolicyOps.UnsetPolicy(
                                    policyType);
                            }

                            if (list == null)
                                list = new StringList();

                            list.Add(varName);
                            count++;
                        }
                    }

                    if (resetChanged)
                    {
                        /* IGNORED */
                        SignalPropertyUnchanged(
                            clientData, policyType, propertyName);
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts policy property values from the interpreter variables and
        /// then applies them back onto the policy state, combining
        /// <see cref="ExtractVariables" /> and <see cref="ApplyVariables" />
        /// into a single operation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose variables are read.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data providing local policy state.
        /// </param>
        /// <param name="clientData">
        /// The client data tracking which policy properties have changed.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture information used when parsing enumeration
        /// values.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Reserved for controlling whether global-only policy is excluded.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow use of the local (plugin-specific) policy state
        /// when plugin data is available.
        /// </param>
        /// <param name="ignoreChanged">
        /// Non-zero to process every property regardless of whether it has
        /// been marked as changed.
        /// </param>
        /// <param name="resetChanged">
        /// Non-zero to clear the changed mark for each property after it has
        /// been applied.
        /// </param>
        /// <param name="count">
        /// Incremented by the number of variables that were extracted and
        /// applied.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the error information.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ExtractAndApplyVariables( /* CORE? */
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
                    ignoreChanged, ref variables, ref count,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (ApplyVariables(
                    pluginData, clientData, variables,
                    allowLocalPolicy, ignoreChanged,
                    resetChanged, ref count,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }
    }
}
