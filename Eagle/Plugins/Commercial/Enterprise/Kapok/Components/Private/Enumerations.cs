/*
 * Enumerations.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Reflection;
using Eagle._Attributes;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Identifies the optional configuration actions that may be performed
    /// while initializing a Kapok interpreter or web server.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("d2f1fd58-7713-4266-84cd-49dd3c998b76")]
    internal enum ConfigurationAction
    {
        /// <summary>
        /// No configuration action.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// An invalid action; do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configure the interpreter settings, when applicable.
        /// </summary>
        MaybeConfigureSettings = 0x20,
        /// <summary>
        /// Set up the log file, when applicable.
        /// </summary>
        MaybeSetupLogFile = 0x40,
        /// <summary>
        /// Set up the trace listeners, when applicable.
        /// </summary>
        MaybeSetupListeners = 0x80,
        /// <summary>
        /// Disable use of the package root path.
        /// </summary>
        DisablePackageRootPath = 0x200,
        /// <summary>
        /// Configure the script library path.
        /// </summary>
        ConfigureLibrary = 0x400,
        /// <summary>
        /// Configure the package auto-path.
        /// </summary>
        ConfigureAutoPath = 0x800,
        /// <summary>
        /// Configure the SQLite base directory.
        /// </summary>
        ConfigureSQLiteBaseDirectory = 0x1000
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies the phase for which a Kapok interpreter is being obtained
    /// or created.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("a7d5d1f0-10fe-42bd-bbe3-4655fdaaf071")]
    internal enum InterpreterPhase
    {
        /// <summary>
        /// No phase.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// An invalid phase; do not use.
        /// </summary>
        Invalid = 0x1,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Obtaining an interpreter for request validation.
        /// </summary>
        Validate = 0x1000,
        /// <summary>
        /// Obtaining an interpreter for configuration.
        /// </summary>
        Configuration = 0x2000,
        /// <summary>
        /// Obtaining an interpreter for the running web server.
        /// </summary>
        Server = 0x4000
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies the kind of storage entity being processed.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("eb61ca9d-c89d-4970-8a36-4703b8cf58a7")]
    internal enum StorageType
    {
        /// <summary>
        /// No storage type.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// An invalid storage type; do not use.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// A storage command entity.
        /// </summary>
        Command = 0x2,
        /// <summary>
        /// A storage format entity.
        /// </summary>
        Format = 0x4
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies the variable storage operation requested by a variable
    /// storage client.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("84146305-8cf1-42d3-a84d-8023cd824b9f")]
    internal enum VariableMethod
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        /// <summary>
        /// Does the specified API key have access?
        /// </summary>
        Access = 0x10,

        /// <summary>
        /// Does the specified named variable exist?
        /// </summary>
        Exist = 0x100,
        /// <summary>
        /// Count the variable names matching the glob pattern.
        /// </summary>
        Count = 0x200,
        /// <summary>
        /// Return all variable names (only) matching the glob pattern.
        /// </summary>
        Names = 0x400,
        /// <summary>
        /// Return all variable values (only) matching the glob pattern.
        /// </summary>
        Values = 0x800,
        /// <summary>
        /// Return all variable names and values matching the glob pattern.
        /// </summary>
        All = 0x1000,
        /// <summary>
        /// Return the value of the specified named variable.
        /// </summary>
        Get = 0x2000,
        /// <summary>
        /// Change the value of the specified named variable.
        /// </summary>
        Set = 0x4000,
        /// <summary>
        /// Delete the value of the specified named variable.
        /// </summary>
        Unset = 0x8000,
        /// <summary>
        /// Purge the value of the specified named variable.  This has the
        /// same semantics as Unset; however, it also logically compacts the
        /// database after the specified named variable is deleted.
        /// </summary>
        Purge = 0x10000,

        /// <summary>
        /// The default method (same as None).
        /// </summary>
        Default = None
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies the kind of entity to provision in response to a
    /// provisioning request.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("3011b010-99ea-4b48-beb6-8d33eddc5438")]
    internal enum ProvisionType
    {
        /// <summary>
        /// No special handling.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid, do not use.
        /// </summary>
        Invalid = 0x1,

        /// <summary>
        /// Provision a license certificate.
        /// </summary>
        License = 0x2,
        /// <summary>
        /// Provision a script certificate.
        /// </summary>
        Script = 0x4,
        /// <summary>
        /// Provision a key ring.  Usage will be based on a parameter
        /// received in the request.
        /// </summary>
        KeyRing = 0x8,
        /// <summary>
        /// Provision a script repository.
        /// </summary>
        Repository = 0x10,
        /// <summary>
        /// Provision a strong name signature.
        /// </summary>
        StrongName = 0x20
    }
}
