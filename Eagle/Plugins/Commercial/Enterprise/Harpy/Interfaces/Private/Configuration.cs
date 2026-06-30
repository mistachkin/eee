/*
 * Configuration.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Collections.Generic;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;

namespace Licensing.Interfaces.Private
{
    /// <summary>
    /// Provides access to licensing configuration data, including sandbox
    /// tokens and the configuration file names used by the core licensing
    /// components.
    /// </summary>
    [ObjectId("76cfd059-2e12-4934-9701-0f18434cb69d")]
    internal interface IConfiguration /* CORE */
    {
        /// <summary>
        /// Gets the collection of sandbox tokens known to this
        /// configuration.
        /// </summary>
        IEnumerable<ulong> SandboxTokens { get; }

        /// <summary>
        /// Gets the primary sandbox token for this configuration.
        /// </summary>
        /// <returns>
        /// The primary sandbox token.
        /// </returns>
        ulong GetPrimarySandboxToken();

        /// <summary>
        /// Determines whether the specified token is the primary sandbox
        /// token.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to check.
        /// </param>
        /// <returns>
        /// Non-zero if <paramref name="token" /> is the primary sandbox
        /// token; otherwise, zero.
        /// </returns>
        bool IsPrimarySandboxToken(ulong token);

        /// <summary>
        /// Adds the specified sandbox token to this configuration.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to add.
        /// </param>
        /// <returns>
        /// Non-zero if the token was successfully added; otherwise, zero.
        /// </returns>
        bool AddSandboxToken(ulong token);

        /// <summary>
        /// Removes the specified sandbox token from this configuration.
        /// </summary>
        /// <param name="token">
        /// The sandbox token to remove.
        /// </param>
        /// <returns>
        /// Non-zero if the token was successfully removed; otherwise, zero.
        /// </returns>
        bool RemoveSandboxToken(ulong token);

        /// <summary>
        /// Gets the collection of configuration file names that were
        /// successfully loaded.
        /// </summary>
        IEnumerable<string> ConfigurationOkFileNames { get; }

        /// <summary>
        /// Gets the collection of configuration file names that resulted in
        /// an error.
        /// </summary>
        IEnumerable<string> ConfigurationErrorFileNames { get; }

        /// <summary>
        /// Clears the tracked collections of configuration file names.
        /// </summary>
        /// <returns>
        /// The number of configuration file names that were cleared.
        /// </returns>
        int ClearConfigurationFileNames();

        /// <summary>
        /// Adds the specified configuration file names to the collection of
        /// successfully loaded configuration file names.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context associated with the configuration file
        /// names.
        /// </param>
        /// <param name="fileNames">
        /// The configuration file names to add, keyed by file name.
        /// </param>
        /// <returns>
        /// Non-zero if the file names were successfully added; otherwise,
        /// zero.
        /// </returns>
        bool AddConfigurationOkFileNames(
            Interpreter interpreter,
            IDictionary<string, Result> fileNames
        );

        /// <summary>
        /// Adds the specified configuration file names to the collection of
        /// configuration file names that resulted in an error.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context associated with the configuration file
        /// names.
        /// </param>
        /// <param name="fileNames">
        /// The configuration file names to add, keyed by file name.
        /// </param>
        /// <returns>
        /// Non-zero if the file names were successfully added; otherwise,
        /// zero.
        /// </returns>
        bool AddConfigurationErrorFileNames(
            Interpreter interpreter,
            IDictionary<string, Result> fileNames
        );

        /// <summary>
        /// Sets up the well-known configuration data used by this
        /// configuration.
        /// </summary>
        void SetupWellKnownConfigurationData();

        /// <summary>
        /// Gets the directory that contains the configuration data.
        /// </summary>
        /// <returns>
        /// The configuration directory.
        /// </returns>
        string GetConfigurationDirectory();

        /// <summary>
        /// Gets the key pairs associated with this configuration.
        /// </summary>
        /// <param name="keyPairs">
        /// Upon return, receives the configuration key pairs.
        /// </param>
        /// <param name="keyUsage">
        /// Upon return, receives the key usage associated with the key
        /// pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the result of the
        /// operation.
        /// </returns>
        ReturnCode GetConfigurationKeyPairs(
            ref IEnumerable<IKeyPair> keyPairs,
            ref string keyUsage,
            ref Result error
        );

        /// <summary>
        /// Loads the licensing configurations for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which the configurations are being loaded.
        /// </param>
        /// <param name="anyClientData">
        /// The client data associated with the load operation.
        /// </param>
        /// <param name="configurationPhase">
        /// The phase of configuration loading being performed.
        /// </param>
        /// <param name="keyName">
        /// The name of the key to use when loading configurations.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to use when loading configurations.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout, in milliseconds, for the load operation.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the configurations to be reloaded.
        /// </param>
        /// <param name="doNotTrack">
        /// Non-zero to avoid tracking the loaded configurations.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation, including any
        /// error information.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating the result of the
        /// operation.
        /// </returns>
        ReturnCode LoadConfigurations(
            Interpreter interpreter,
            IAnyClientData anyClientData,
            ConfigurationPhase configurationPhase,
            string keyName,
            string keyRingName,
            int? timeout,
            bool force,
            bool doNotTrack,
            ref Result result
        );
    }
}
