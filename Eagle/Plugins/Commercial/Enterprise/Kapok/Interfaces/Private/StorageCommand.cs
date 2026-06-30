/*
 * StorageCommand.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Data;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using Kapok.Components.Private;

namespace Kapok.Interfaces.Private
{
    /// <summary>
    /// Represents a storage command that performs a variable-storage operation
    /// against a database connection using a given storage format.
    /// </summary>
    [ObjectId("efecb7c1-1b4d-416d-80bf-8255b562c8fd")]
    internal interface IStorageCommand
    {
        /// <summary>
        /// Executes the storage operation, binding the supplied parameter
        /// names and values according to the variable method.
        /// </summary>
        /// <param name="connection">
        /// The database connection to operate on.
        /// </param>
        /// <param name="format">
        /// The storage format describing how values are encoded.
        /// </param>
        /// <param name="parameterNames">
        /// The names of the parameters to bind.
        /// </param>
        /// <param name="parameterValues">
        /// The values of the parameters to bind.
        /// </param>
        /// <param name="method">
        /// The variable method (operation) to perform.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to perform case-insensitive name matching.
        /// </param>
        /// <param name="errorOnNop">
        /// Non-zero to treat a no-op as an error.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the operation, or an error
        /// message describing why it failed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        ReturnCode Execute(
            IDbConnection connection,
            IStorageFormat format,
            string[] parameterNames,
            string[] parameterValues,
            VariableMethod method,
            bool noCase,
            bool errorOnNop,
            ref Result result
        );

        /// <summary>
        /// Produces a list representation of this storage command.
        /// </summary>
        /// <param name="full">
        /// Non-zero to include all fields; zero for the summary set.
        /// </param>
        /// <returns>
        /// A list describing this storage command.
        /// </returns>
        IStringList ToList(bool full);
    }
}
