/*
 * Enterprise.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) \$Id: \$
 */

using System.Globalization;

#if OBFUSCATION
using System.Reflection;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

#if LICENSING
using Licensing.Sdk.Private;
#endif

using ${projectName}.Components.Private;
using _Plugins = Eagle._Plugins;

namespace ${projectName}
{
    /// <summary>
    /// Implements the example Eagle Enterprise Edition plugin.  It serves as
    /// a template for new plugins and, when licensing is enabled, verifies the
    /// plugin's license certificate during initialization.
    /// </summary>
#if OBFUSCATION
    \[Obfuscation(Feature = "renaming")\]
#endif
    \[ObjectId("[string tolower [guid new]]")\]
    \[PluginFlags(
        PluginFlags.Primary | PluginFlags.User |
        PluginFlags.Commercial | PluginFlags.NoCommands |
        PluginFlags.NoFunctions | PluginFlags.NoPolicies |
        PluginFlags.NoTraces)\]
    internal sealed class Enterprise : _Plugins.Default
    {
        #region Private Data
        #region Certificate Data
#if LICENSING
        //
        // NOTE: The certificate file name currently in use.
        //
        /// <summary>
        /// The file name of the license certificate currently in use.
        /// </summary>
        private string certificateFileName;

        //
        // NOTE: The certificate currently in use.
        //
        /// <summary>
        /// The license certificate currently in use.
        /// </summary>
        private object certificate;
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

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

        #region IState Members
        /// <summary>
        /// Initializes the plugin.  When licensing is enabled, the certificate
        /// is verified and the licensed flag is set; the base initialization
        /// then runs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the plugin is being initialized in.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Initialize(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
#if LICENSING
            LicenseOps.SetupWellKnownConfigurationData(this.AppDomain);

            ReturnCode code = ReturnCode.Ok;

            code = LicenseOps.VerifyCertificate(
                interpreter, this.Assembly, null, this, null, null,
                null, null, null, null, null, null, true, false, false,
                true, LicenseOps.UseIsolated(GetType()), null, null,
                clientData, ref certificateFileName, ref certificate,
                ref result);

            if (code == ReturnCode.Ok)
                this.Flags |= PluginFlags.Licensed;

            if (code != ReturnCode.Ok)
                return code;
#endif

            return base.Initialize(interpreter, clientData, ref result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPlugin Members
        /// <summary>
        /// Gets a localized string resource for the plugin.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the string.
        /// </param>
        /// <param name="name">
        /// The name of the string resource to retrieve.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use when retrieving the resource.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The requested string resource, or null upon failure.
        /// </returns>
        public override string GetString(
            Interpreter interpreter, /* in */
            string name,             /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
            return Utility.GetString(
                ResourceManager, name, cultureInfo, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Produces the plugin's "about" information (formatted plugin details
        /// plus, when licensing is enabled, license certificate details).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the about information.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the about information or an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode About(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            ReturnCode code = ReturnCode.Ok;
            Result localResult = Utility.FormatPluginAbout(this, false);

#if LICENSING
            code = LicenseOps.AboutCertificate(
                interpreter, this, certificate, LicenseOps.UseIsolated(
                typeof(Enterprise)), ref localResult);
#endif

            result = localResult;
            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Produces the list of conditional compilation options that were
        /// active when the plugin was built.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the options.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the list of build options.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public override ReturnCode Options(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            result = new StringList(DefineConstants.OptionList, false);
            return ReturnCode.Ok;
        }
        #endregion
    }
}
