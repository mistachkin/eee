/*
 * Standard.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Utility = Eagle._Components.Public.Utility;
using _Features = Licensing.Components.Private.Features;

namespace Licensing
{
    /// <summary>
    /// Provides the standard licensing plugin used by Harpy, building on the
    /// default plugin behavior and advertising the standard set of licensed
    /// features.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("8847a1bd-78d1-4cf5-92b7-9e551922680c")]
    [PluginFlags(
        PluginFlags.User | PluginFlags.Commercial |
#if PLUGIN_COMMANDS
        PluginFlags.Command |
#endif
#if CERTIFICATE_POLICY
        PluginFlags.Policy |
#endif
#if PLUGIN_COMMANDS
        PluginFlags.MergeCommands |
#endif
#if CERTIFICATE_POLICY
        PluginFlags.MergePolicies |
#endif
        PluginFlags.NoFunctions | PluginFlags.NoTraces |
        PluginFlags.NoGetString)]
    internal sealed class Standard : Plugins.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of this plugin, combining the plugin
        /// flags from its base type and itself and, when licensing is
        /// enabled, registering the standard and enterprise license
        /// agreements.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to initialize the base plugin.
        /// </param>
        public Standard(
            IPluginData pluginData /* in */
            )
            : base(pluginData)
        {
            this.Flags |= Utility.GetPluginFlags(GetType().BaseType) |
                Utility.GetPluginFlags(this);

#if LICENSING
            AddAgreements(new Uri[] {
                Constants.StandardAgreement, Constants.StandardAgreement2,
                Constants.EnterpriseAgreement, Constants.EnterpriseAgreement2
            }, false);
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicensePluginData Members
#if LICENSING
        /// <summary>
        /// Gets the set of licensed features advertised by this plugin.
        /// </summary>
        public override string Features
        {
            get { return _Features.Plugins.StandardOrAll; }
        }
#endif
        #endregion
    }
}
