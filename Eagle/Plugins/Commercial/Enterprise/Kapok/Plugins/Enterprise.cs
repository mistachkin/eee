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

#if LICENSING
using System;
#endif

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

using Kapok.Components.Private;
using Kapok.Components.Shared;
using _Plugins = Eagle._Plugins;

namespace Kapok
{
    /// <summary>
    /// Implements the primary Kapok Enterprise Edition plugin.  It registers
    /// the <c>kapok</c> command and, when licensing is enabled, verifies the
    /// plugin's license certificate during initialization.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("36155aaa-da19-4ee0-ab19-0ad145e166ec")]
    [PluginFlags(
        PluginFlags.Primary | PluginFlags.User |
        PluginFlags.Commercial | PluginFlags.NoFunctions |
        PluginFlags.NoPolicies | PluginFlags.NoTraces)]
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
            ReturnCode code = ReturnCode.Ok;

#if LICENSING
            LicenseOps.SetupWellKnownConfigurationData(this.AppDomain);

            code = LicenseOps.VerifyCertificate(
                interpreter, this.Assembly, null, this, null, null, null,
                null, null, null, null, null, null, null, null, true, false,
                false, true, LicenseOps.UseIsolated(typeof(Enterprise)),
                null, null, new AnyClientData(clientData, false),
                ref certificateFileName, ref certificate, ref result);

            if (code == ReturnCode.Ok)
                this.Flags |= PluginFlags.Licensed;
#endif

            if (code == ReturnCode.Ok)
                return base.Initialize(interpreter, clientData, ref result);

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Terminates the plugin, clearing the certificate state and the
        /// licensed flag before running the base termination.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the plugin is being terminated in.
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
        public override ReturnCode Terminate(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
            certificateFileName = null;
            certificate = null;

            this.Flags &= ~PluginFlags.Licensed;

            return base.Terminate(interpreter, clientData, ref result);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPlugin Members
#if LICENSING
        /// <summary>
        /// Gets the file name of a license certificate.  With a name, returns
        /// the plugin-relative file name for that certificate type; otherwise
        /// returns the file name of the certificate currently in use.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the certificate file name.
        /// </param>
        /// <param name="name">
        /// The certificate type name, or null/empty for the certificate
        /// currently in use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The certificate file name, or null on failure.
        /// </returns>
        public override string GetCertificateFileName(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            string fileName;

            if (!String.IsNullOrEmpty(name))
            {
                fileName = Utility.GetPluginRelativeFileName(
                    this, null, name);

                if (fileName == null)
                    error = "unsupported certificate type";
            }
            else
            {
                fileName = certificateFileName;

                if (fileName == null)
                    error = "invalid file name";
            }

            return fileName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the license certificate currently in use as an identifier.  A
        /// non-empty name is rejected as unsupported, and the certificate is
        /// unavailable when the plugin is isolated in a different application
        /// domain.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the certificate.
        /// </param>
        /// <param name="name">
        /// Must be null or empty; a non-empty value is unsupported.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The certificate identifier, or null on failure.
        /// </returns>
        public override IIdentifier GetCertificate(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            if (!String.IsNullOrEmpty(name))
            {
                error = "unsupported certificate type";
                return null;
            }

            if (Utility.IsCrossAppDomain(interpreter, this))
            {
                error = "unsupported when plugin is isolated";
                return null;
            }

            IIdentifier identifier = certificate as IIdentifier;

            if (identifier == null)
                error = "invalid certificate";

            return identifier;
        }
#endif

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
