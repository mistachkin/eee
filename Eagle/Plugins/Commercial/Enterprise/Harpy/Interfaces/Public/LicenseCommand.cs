/*
 * LicenseCommand.cs --
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
    /// Represents a license-aware command that, in addition to the descriptive
    /// information provided by <see cref="ILicenseCommandData" />, can decide
    /// whether it is permitted to execute within a given
    /// <see cref="Interpreter" /> context based on the active licensing state.
    /// </summary>
    [ObjectId("4922681c-0967-4dd8-8f48-81fc9f05fcf6")]
    public interface ILicenseCommand : ILicenseCommandData
    {
        /// <summary>
        /// Determines whether this license command is currently allowed to
        /// execute within the specified interpreter, taking the prevailing
        /// licensing state into account.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> context in which the command would
        /// execute.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the <see cref="Result" /> of the check,
        /// including any error or diagnostic information when execution is not
        /// permitted.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the command is permitted to
        /// execute; otherwise, an appropriate error return code.
        /// </returns>
        ReturnCode CanExecute(
            Interpreter interpreter,
            ref Result result
            );
    }
}
