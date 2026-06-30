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
    /// Provides the primary commercial licensing plugin for the Harpy core
    /// feature set.  This plugin contributes its commands (and policies, when
    /// configured) to the interpreter and, when licensing is enabled,
    /// registers the associated license agreements.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("5feb7580-337f-4c6d-b1a0-f2948bf8b336")]
    [PluginFlags(
#if !CERTIFICATE_POLICY && !PLUGIN_COMMANDS
        PluginFlags.Primary |
#endif
#if !PLUGIN_COMMANDS && ISOLATED_PLUGINS
        PluginFlags.IsolatedOnly |
#if SHELL
        PluginFlags.UpdateCheck |
#endif
#endif
        PluginFlags.User | PluginFlags.Commercial |
        PluginFlags.Command |
#if CERTIFICATE_POLICY
        PluginFlags.Policy |
#endif
        PluginFlags.MergeCommands |
#if CERTIFICATE_POLICY
        PluginFlags.MergePolicies |
#endif
        PluginFlags.NoFunctions | PluginFlags.NoTraces |
        PluginFlags.NoGetString)]
    internal sealed class Core : Plugins.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of this plugin.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to initialize the base plugin.
        /// </param>
        public Core(
            IPluginData pluginData /* in */
            )
            : base(pluginData)
        {
            this.Flags |= Utility.GetPluginFlags(GetType().BaseType) |
                Utility.GetPluginFlags(this);

#if LICENSING
            AddAgreements(new Uri[] {
                Constants.CoreAgreement, Constants.CoreAgreement2,
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
        /// Gets the licensing feature name(s) provided by this plugin.
        /// </summary>
        public override string Features
        {
            get { return _Features.Plugins.CoreOrAll; }
        }
#endif
        #endregion
    }
}

///////////////////////////////////////////////////////////////////////////////

#if CERTIFICATE_POLICY
namespace Security
{
    /// <summary>
    /// Provides the commercial licensing plugin used to enforce the
    /// certificate policy.  This plugin contributes its commands and policies
    /// to the interpreter and, when licensing is enabled, registers the
    /// associated license agreements.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("3c0593e2-223d-4dc6-9fd2-a79afd3007d7")]
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
        PluginFlags.User | PluginFlags.Commercial |
        PluginFlags.Command | PluginFlags.Policy |
        PluginFlags.MergeCommands | PluginFlags.OverwriteCommands |
        PluginFlags.MergePolicies | PluginFlags.OverwritePolicies |
        PluginFlags.NoGetString)]
    internal sealed class Core : Licensing.Plugins.Default
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of this plugin.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to initialize the base plugin.
        /// </param>
        public Core(
            IPluginData pluginData /* in */
            )
            : base(pluginData)
        {
            this.Flags |= Utility.GetPluginFlags(GetType().BaseType) |
                Utility.GetPluginFlags(this);

#if LICENSING
            AddAgreements(new Uri[] {
                Constants.CoreAgreement, Constants.CoreAgreement2,
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
        /// Gets the licensing feature name(s) provided by this plugin.
        /// </summary>
        public override string Features
        {
            get { return _Features.Plugins.CoreOrAll; }
        }
#endif
        #endregion
    }
}
#endif
