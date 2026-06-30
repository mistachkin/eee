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
using System.Globalization;

#if OBFUSCATION
using System.Reflection;
#endif

using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

#if LICENSING
using Licensing.Sdk.Private;
#endif

using Zeus.Components.Private;
using _Plugins = Eagle._Plugins;

namespace Zeus
{
    /// <summary>
    /// Implements the primary Zeus Enterprise Edition plugin.  It registers
    /// the <c>zeus</c> command, optionally verifies its license certificate
    /// during initialization, swaps in the enhanced <c>pi</c> math function,
    /// transparently decrypts its own encrypted resource strings, and holds
    /// the RFC 2898 data and provider references used by the plugin's
    /// procedure obfuscation feature.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("25f49db7-1032-48ed-a757-7d1a65900a23")]
    [PluginFlags(
        PluginFlags.Primary | PluginFlags.User |
        PluginFlags.Commercial | PluginFlags.NoFunctions |
        PluginFlags.NoPolicies | PluginFlags.NoTraces)]
    internal sealed class Enterprise : _Plugins.Default, IRfc2898DataManager
    {
        #region Private Constants
        //
        // TODO: Always change these when building a custom
        //       (vendor?) version of this plugin.
        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// Whether the plugin's own base64 resource strings are decrypted on
        /// retrieval.
        /// </summary>
        private static bool SelfDecrypt = true;

        /// <summary>
        /// The text encoding used to decrypt the plugin's own resource
        /// strings, or null for the default.
        /// </summary>
        private static Encoding SelfEncoding = null;

        /// <summary>
        /// The password used to decrypt the plugin's own resource strings.
        /// </summary>
        private static string SelfPassword =
            "55BBFEE56823F5CAC63D06614A9AFAEE7E7E8996";

        /// <summary>
        /// The salt used to decrypt the plugin's own resource strings.
        /// </summary>
        private static string SelfSalt =
            "79455596BBDBCDE17686E2C2A20A888CD8329BC9";

        /// <summary>
        /// The RFC 2898 iteration count used to decrypt the plugin's own
        /// resource strings.
        /// </summary>
        private static int SelfIterationCount = 100000;

        /// <summary>
        /// The hash algorithm name used to decrypt the plugin's own resource
        /// strings, or null for the default.
        /// </summary>
        private static string SelfHashAlgorithmName = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

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

        ///////////////////////////////////////////////////////////////////////

        #region Function Data
        //
        // NOTE: This field used to store the function token returned
        //       by the interpreter for our custom pi() math function.
        //
#if NET_40
        /// <summary>
        /// The interpreter token of the custom <c>pi</c> math function added
        /// by this plugin.
        /// </summary>
        private long savedToken;
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

        #region Private Methods
        /// <summary>
        /// Decrypts the supplied value in place when self-decryption is
        /// enabled and the value looks like base64 ciphertext, using the
        /// plugin's built-in key-derivation parameters.
        /// </summary>
        /// <param name="value">
        /// On input, the value to possibly decrypt; on output, the decrypted
        /// value when a transform occurred.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success (including when no transform was needed);
        /// otherwise, zero.
        /// </returns>
        private static bool MaybeTransform(
            ref string value, /* in, out */
            ref Result error  /* out */
            )
        {
            if (SelfDecrypt &&
                (value != null) && Utility.IsBase64(value))
            {
                if (CryptographyOps.Transform(
                        SelfEncoding, SelfPassword, SelfSalt,
                        SelfIterationCount, SelfHashAlgorithmName,
                        false, ref value, ref error) != ReturnCode.Ok)
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IState Members
        /// <summary>
        /// Initializes the plugin.  When licensing is enabled, the plugin's
        /// certificate is verified and the licensed flag is set; on supporting
        /// frameworks the original <c>pi</c> function is saved and the
        /// enhanced one installed; finally the base initialization runs.
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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
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

#if NET_40
            if (code == ReturnCode.Ok)
            {
                if (Pi.SaveOriginalFunction(
                        interpreter, ref result) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if ((savedToken == 0) && (Pi.CreateAndAddFunction(
                        interpreter, this, clientData, ref savedToken,
                        ref result) != ReturnCode.Ok))
                {
                    return ReturnCode.Error;
                }
            }
#endif

            if (code == ReturnCode.Ok)
                return base.Initialize(interpreter, clientData, ref result);

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Terminates the plugin.  On supporting frameworks the custom
        /// <c>pi</c> function is removed and the original restored; when
        /// licensing is enabled the certificate state is cleared and the
        /// licensed flag removed; finally the base termination runs.
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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Terminate(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
#if NET_40
            if ((savedToken != 0) && (Pi.RemoveFunction(
                    interpreter, clientData, ref savedToken,
                    ref result) != ReturnCode.Ok))
            {
                return ReturnCode.Error;
            }

            if (Pi.RestoreOriginalFunction(
                    interpreter, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }
#endif

#if LICENSING
            certificateFileName = null;
            certificate = null;

            this.Flags &= ~PluginFlags.Licensed;
#endif

            return base.Terminate(interpreter, clientData, ref result);
        }
#endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPlugin Members
        /// <summary>
        /// Gets a resource string by name, decrypting it when necessary.  The
        /// name is looked up verbatim and, failing that, by its
        /// package-relative form; a found value is transparently decrypted via
        /// <see cref="MaybeTransform" /> before being returned.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the string.
        /// </param>
        /// <param name="name">
        /// The name of the resource string to retrieve.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to select the resource string.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives the accumulated errors describing why the
        /// string could not be found.
        /// </param>
        /// <returns>
        /// The resource string, or null when it could not be found.
        /// </returns>
        public override string GetString(
            Interpreter interpreter, /* in */
            string name,             /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
            //
            // TODO: Firstly, figure out why this method now requires its
            //       own use of GetPackageRelativeFileName, which was not
            //       required in Beta 53.
            //
            // TODO: Secondly, consider changing this method to include a
            //       search of the ISnippetManager (within the specified
            //       interpreter).
            //
            ResultList errors = null;
            string value; /* REUSED */
            string localName = null; /* REUSED */
            Result localError; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            localError = null;

            value = Utility.GetAnyString(
                interpreter, this, ResourceManager, name,
                cultureInfo, ref localError);

            if (value != null)
            {
#if DEBUG || FORCE_TRACE
                Utility.DebugTrace(String.Format(
                    "GetString: verbatim resource string {0}",
                    Utility.FormatWrapOrNull(name)),
                    typeof(Enterprise).Name, TracePriority.Lower |
                        TracePriority.FromPlugin);
#endif

                localError = null;

                if (!MaybeTransform(ref value, ref localError))
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    goto error;
                }

                return value;
            }
            else if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }

            ///////////////////////////////////////////////////////////////////

            localError = null;

            localName = Utility.GetPackageRelativeFileName(
                name, true, false, ref localError);

            if (localName != null)
            {
                localName = Utility.TranslatePath(
                    localName, PathTranslationType.Unix);

                localError = null;

                value = Utility.GetAnyString(
                    interpreter, this, ResourceManager, localName,
                    cultureInfo, ref localError);

                if (value != null)
                {
#if DEBUG || FORCE_TRACE
                    Utility.DebugTrace(String.Format(
                        "GetString: relative resource string {0}",
                        Utility.FormatWrapOrNull(localName)),
                        typeof(Enterprise).Name, TracePriority.Lower |
                            TracePriority.FromPlugin);
#endif

                    localError = null;

                    if (!MaybeTransform(ref value, ref localError))
                    {
                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localError);
                        }

                        goto error;
                    }

                    return value;
                }
                else if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }
            else if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }

            ///////////////////////////////////////////////////////////////////

        error:

#if DEBUG || FORCE_TRACE
            Utility.DebugTrace(String.Format(
                "GetString: failed to find string {0} or {1}: {2}",
                Utility.FormatWrapOrNull(name),
                Utility.FormatWrapOrNull(localName),
                Utility.FormatWrapOrNull(true, false, errors)),
                typeof(Enterprise).Name, TracePriority.Low |
                    TracePriority.FromPlugin);
#endif

            error = errors;
            return null;
        }

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Gets the file name of a license certificate.  When a name is
        /// supplied, the plugin-relative file name for that certificate type
        /// is returned; otherwise the file name of the certificate currently
        /// in use is returned.
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
        /// non-empty name is rejected as an unsupported certificate type, and
        /// the certificate is unavailable when the plugin is isolated in a
        /// different application domain.
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
        /// Produces the plugin's "about" information, including its formatted
        /// plugin details and, when licensing is enabled, the license
        /// certificate information.  This backs the <c>zeus about</c>
        /// sub-command.
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
        /// active when the plugin was built.  This backs the
        /// <c>zeus options</c> sub-command.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the options.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the list of build options.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
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

        ///////////////////////////////////////////////////////////////////////

        #region IRfc2898DataManager Members
        /// <summary>
        /// The backing field for the <see cref="Rfc2898Data" /> property.
        /// </summary>
        private IRfc2898Data rfc2898Data;

        /// <summary>
        /// Gets or sets the RFC 2898 data held by this plugin and used for
        /// procedure obfuscation.
        /// </summary>
        public IRfc2898Data Rfc2898Data
        {
            get { return rfc2898Data; }
            set { rfc2898Data = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Rfc2898DataProvider" />
        /// property.
        /// </summary>
        private IRfc2898DataProvider rfc2898DataProvider;

        /// <summary>
        /// Gets or sets the RFC 2898 data provider held by this plugin and
        /// used for procedure obfuscation.
        /// </summary>
        public IRfc2898DataProvider Rfc2898DataProvider
        {
            get { return rfc2898DataProvider; }
            set { rfc2898DataProvider = value; }
        }
        #endregion
    }
}
