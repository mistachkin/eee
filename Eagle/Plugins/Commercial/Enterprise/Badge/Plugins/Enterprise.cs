/*
 * Enterprise.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if OBFUSCATION
using System.Reflection;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

namespace Badge
{
    /// <summary>
    /// Implements the primary Badge Enterprise Edition plugin, a command-style
    /// variant that registers the <c>badge</c> command and merges it into the
    /// interpreter.  Most behavior is inherited from
    /// <see cref="Plugins.Default" />.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("c5f11bde-6e47-44db-9872-4ba8c28d5b0e")]
    [PluginFlags(
        PluginFlags.Primary | PluginFlags.User |
        PluginFlags.Commercial | PluginFlags.Command |
        PluginFlags.MergeCommands | PluginFlags.NoFunctions |
        PluginFlags.NoPolicies | PluginFlags.NoTraces)]
    internal sealed class Enterprise : Plugins.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Enterprise" /> plugin
        /// class.
        /// </summary>
        /// <param name="pluginData">
        /// The data used to create and configure the plugin.
        /// </param>
        public Enterprise(
            IPluginData pluginData /* in */
            )
            : base(pluginData)
        {
            this.Flags |= Utility.GetPluginFlags(GetType().BaseType) |
                Utility.GetPluginFlags(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPlugin Members
        /// <summary>
        /// Produces the plugin's "about" information (without certificate
        /// details) and delegates to the base implementation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the about information.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the about information or an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode About(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            ReturnCode code;
            Result localResult = Utility.FormatPluginAbout(this, false);

            code = base.About(interpreter, ref localResult);

            result = localResult;
            return code;
        }
        #endregion
    }
}
