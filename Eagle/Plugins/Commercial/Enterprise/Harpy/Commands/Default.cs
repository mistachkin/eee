/*
 * Default.cs --
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
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Components.Public;
using Licensing.Interfaces.Public;
using _Commands = Eagle._Commands;
using Utility = Eagle._Components.Public.Utility;

namespace Licensing.Commands
{
    /// <summary>
    /// Provides the default command implementation used by the licensing
    /// subsystem.  This command inherits the core default command behavior
    /// and adds support for license feature and restriction flags.
    /// </summary>
    [ObjectId("2db7f7ef-5d9f-4261-934d-3478e0805503")]
    [CommandFlags(CommandFlags.NoPopulate)]
    [ObjectGroup("default")]
    internal class Default : _Commands.Default, ILicenseCommand
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of this class, initializing the base
        /// command using the supplied command data and clearing the license
        /// feature and restriction flags.
        /// </summary>
        /// <param name="commandData">
        /// The command data used to initialize the base command.
        /// </param>
        public Default(
            ICommandData commandData /* in */
            )
            : base(commandData)
        {
            this.Flags |= Utility.GetCommandFlags(GetType().BaseType) |
                Utility.GetCommandFlags(this);

            ///////////////////////////////////////////////////////////////////

            this.features = null;
            this.restrictions = null;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicenseFlagsData Members
        /// <summary>
        /// The backing field for the <see cref="Features" /> property.
        /// </summary>
        private string features;

        /// <summary>
        /// Gets the license feature flags associated with this command.
        /// </summary>
        public virtual string Features
        {
            get { return features; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Restrictions" /> property.
        /// </summary>
        private string restrictions;

        /// <summary>
        /// Gets the license restriction flags associated with this command.
        /// </summary>
        public virtual string Restrictions
        {
            get { return restrictions; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicenseCommand Members
        /// <summary>
        /// Determines whether this command is permitted to execute based on
        /// the license certificate associated with its plugin, matching the
        /// configured feature and restriction flags.  When licensing is not
        /// enabled, execution is always permitted.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in whose context the command would execute.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives the error result describing why execution
        /// is not permitted.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the command is permitted to
        /// execute; otherwise, <see cref="ReturnCode.Error" />.
        /// </returns>
        public virtual ReturnCode CanExecute(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
#if LICENSING
            Result localResult = null;

            if (CertificateSharedOps.MatchFlags(
                    CertificateSharedOps.GetViaPlugin(this.Plugin),
                    FlagType.Feature, Utility.DefaultAttributeFlagsKey(),
                    this.Features, this.Restrictions, false, false, true,
                    ref localResult) == ReturnCode.Ok)
            {
                return ReturnCode.Ok;
            }

            result = localResult;

            CertificateIsolatedOps.MaybeFixupResult(
                interpreter, this.Plugin, result);

            return ReturnCode.Error;
#else
            return ReturnCode.Ok;
#endif
        }
        #endregion
    }
}
