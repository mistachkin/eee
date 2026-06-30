/*
 * LicensePluginData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Containers.Public;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Represents the licensing data associated with a plugin, combining the
    /// license flags and license certificate information with the set of
    /// license agreements that apply to the plugin.
    /// </summary>
    [ObjectId("0b9f6bc2-074a-4332-abd4-dce031456c04")]
    public interface ILicensePluginData :
            ILicenseFlagsData, ILicenseCertificateData
    {
        /// <summary>
        /// Gets the collection of license agreements, keyed by their URI, that
        /// are associated with the plugin.
        /// </summary>
        UriDictionary<bool> Agreements { get; }
    }
}
