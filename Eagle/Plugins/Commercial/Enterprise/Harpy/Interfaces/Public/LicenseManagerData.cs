/*
 * LicenseManagerData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Components.Public.Delegates;
using Licensing.Components.Public.Delegates;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Provides access to the data used by a license manager, including the
    /// callbacks used to select a license file name and to handle license
    /// renewal.
    /// </summary>
    [ObjectId("13101765-d1c8-46dd-903a-fa40073f6b38")]
    public interface ILicenseManagerData
    {
        /// <summary>
        /// Gets or sets the callback used to select the license file name.
        /// </summary>
        ElementSelectionCallback FileNameCallback { get; set; }
        /// <summary>
        /// Gets or sets the callback used to handle license renewal.
        /// </summary>
        RenewCallback RenewCallback { get; set; }
    }
}
