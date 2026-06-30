/*
 * StorageFormat.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Interfaces.Public;

namespace Kapok.Interfaces.Private
{
    /// <summary>
    /// Represents a storage format that encodes and decodes variable values
    /// for persistence.
    /// </summary>
    [ObjectId("81c9d64e-3fbd-4281-a3c6-a46db3d9942a")]
    internal interface IStorageFormat : IFormatDataValue
    {
        /// <summary>
        /// Produces a list representation of this storage format.
        /// </summary>
        /// <param name="full">
        /// Non-zero to include all fields; zero for the summary set.
        /// </param>
        /// <returns>
        /// A list describing this storage format.
        /// </returns>
        IStringList ToList(bool full);
    }
}
