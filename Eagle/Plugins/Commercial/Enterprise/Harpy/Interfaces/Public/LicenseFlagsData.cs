/*
 * LicenseFlagsData.cs --
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
    /// Represents the textual flag data associated with a license, namely
    /// the set of features the license enables and the restrictions it
    /// imposes.
    /// </summary>
    [ObjectId("eae04e60-97f7-4065-9828-8809b08a59a2")]
    public interface ILicenseFlagsData
    {
        /// <summary>
        /// Gets the textual description of the features enabled by the
        /// license.
        /// </summary>
        string Features { get; }

        /// <summary>
        /// Gets the textual description of the restrictions imposed by the
        /// license.
        /// </summary>
        string Restrictions { get; }
    }
}
