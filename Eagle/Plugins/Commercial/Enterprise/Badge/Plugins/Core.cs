/*
 * Core.cs --
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

namespace Security
{
    /// <summary>
    /// Implements the <c>Security.Certificates</c> Badge plugin variant used
    /// to carry the bundled security certificate resources.  Depending on the
    /// build, it loads as a primary (optionally isolated) plugin or as a
    /// command plugin.  Most behavior is inherited from
    /// <see cref="Badge.Plugins.Default" />.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("b971e191-3aed-4965-be26-9c2f7220a01b")]
    [PluginFlags(
#if !PLUGIN_COMMANDS
        PluginFlags.Primary |
#endif
#if !PLUGIN_COMMANDS && ISOLATED_PLUGINS
        PluginFlags.IsolatedOnly |
#if SHELL
        PluginFlags.UpdateCheck |
#endif
#endif
        PluginFlags.User |
        PluginFlags.Commercial |
#if PLUGIN_COMMANDS
        PluginFlags.Command | PluginFlags.MergeCommands |
        PluginFlags.OverwriteCommands |
#endif
        PluginFlags.NoFunctions | PluginFlags.NoPolicies |
        PluginFlags.NoTraces)]
    internal sealed class Certificates : Badge.Plugins.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Certificates" /> plugin
        /// class.
        /// </summary>
        /// <param name="pluginData">
        /// The data used to create and configure the plugin.
        /// </param>
        public Certificates(
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
        /// Produces the plugin's "about" information (with certificate
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
            Result localResult = Utility.FormatPluginAbout(this, true);

            code = base.About(interpreter, ref localResult);

            result = localResult;
            return code;
        }
        #endregion
    }
}
