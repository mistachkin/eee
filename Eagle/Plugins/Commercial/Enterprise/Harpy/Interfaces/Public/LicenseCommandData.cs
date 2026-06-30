/*
 * LicenseCommandData.cs --
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
    /// Represents the data associated with a licensing command, extending the
    /// licensing flags data with command-specific information.
    /// </summary>
    [ObjectId("76281ac1-15bf-404f-8c54-9d697410975e")]
    public interface ILicenseCommandData : ILicenseFlagsData
    {
        // nothing.
    }
}
