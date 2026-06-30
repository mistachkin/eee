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
    /// Provides the commercial Enterprise edition licensing plugin, extending
    /// the default plugin implementation with the feature set and plugin
    /// flags appropriate for Enterprise licensing.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("17c9571b-0861-47af-9679-e4f4ae92adcc")]
    [PluginFlags(
#if PLUGIN_COMMANDS
        PluginFlags.Primary |
#endif
        PluginFlags.User |
        PluginFlags.Commercial |
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
        PluginFlags.NoFunctions |
        PluginFlags.NoTraces |
        PluginFlags.NoGetString)]
    internal sealed class Enterprise : Plugins.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an instance of the Enterprise licensing plugin, merging
        /// the plugin flags from its base type and this type and, when
        /// licensing is enabled, registering the Enterprise license
        /// agreements.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to initialize the base plugin.
        /// </param>
        public Enterprise(
            IPluginData pluginData /* in */
            )
            : base(pluginData)
        {
            this.Flags |= Utility.GetPluginFlags(GetType().BaseType) |
                Utility.GetPluginFlags(this);

#if LICENSING
            AddAgreements(new Uri[] {
                Constants.EnterpriseAgreement, Constants.EnterpriseAgreement2
            }, false);
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILicensePluginData Members
#if LICENSING
        /// <summary>
        /// Gets the licensed feature set associated with this plugin, which
        /// corresponds to the Enterprise edition (or all features).
        /// </summary>
        public override string Features
        {
            get { return _Features.Plugins.EnterpriseOrAll; }
        }
#endif
        #endregion
    }
}
