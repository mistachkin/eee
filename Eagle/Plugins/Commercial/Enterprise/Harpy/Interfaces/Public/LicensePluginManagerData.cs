/*
 * LicensePluginManagerData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Provides access to the manager objects used by the license plugin to
    /// perform its licensing, storage, and registry related operations.
    /// </summary>
    [ObjectId("8dc08361-8b79-4700-bd98-d64f00ba00f3")]
    public interface ILicensePluginManagerData
    {
        /// <summary>
        /// Gets the license manager used to perform licensing operations.
        /// </summary>
        ILicenseManager LicenseManager { get; }

        /// <summary>
        /// Gets the storage manager used to perform storage operations.
        /// </summary>
        IStorageManager StorageManager { get; }

#if !NET_STANDARD_20
        /// <summary>
        /// Gets the registry manager used to perform registry operations.
        /// </summary>
        IRegistryManager RegistryManager { get; }
#endif
    }
}
