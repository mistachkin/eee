/*
 * RegistryManager.cs --
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

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Provides access to the registry keys used to store and retrieve
    /// licensing information.
    /// </summary>
    [ObjectId("dc66fbe8-7713-462a-adc4-c31df73a9bce")]
    public interface IRegistryManager /* CORE */
    {
        /// <summary>
        /// Gets the root registry key under which licensing information is
        /// stored.
        /// </summary>
        /// <param name="perMachine">
        /// Non-zero to obtain the per-machine root key, zero to obtain the
        /// per-user root key, or null to use the default scope.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The root registry key, or null if it could not be obtained.
        /// </returns>
        object GetRootKey(
            bool? perMachine,
            ref Result error
        );

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the registry key used to store licensing
        /// information.
        /// </summary>
        /// <param name="perMachine">
        /// Non-zero to obtain the per-machine key name, zero to obtain the
        /// per-user key name, or null to use the default scope.
        /// </param>
        /// <param name="full">
        /// Non-zero to obtain the fully qualified key name, zero to obtain
        /// the relative key name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// The name of the registry key, or null if it could not be obtained.
        /// </returns>
        string GetKeyName(
            bool? perMachine,
            bool full,
            ref Result error
        );
    }
}
