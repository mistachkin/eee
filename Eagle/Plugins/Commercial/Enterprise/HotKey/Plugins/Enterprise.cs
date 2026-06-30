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

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

#if LICENSING
using Licensing.Sdk.Private;
#endif

using HotKey.Components.Private;
using HotKey.Interfaces.Private;
using HotKey.Shell;
using _Plugins = Eagle._Plugins;

namespace HotKey
{
    /// <summary>
    /// Implements the primary HotKey Enterprise Edition plugin.  It starts and
    /// stops the dedicated hot-key manager thread, registers the hot-key
    /// template packages, sets up the shared complaint form, evaluates the
    /// hot-key startup script, registers the <c>hotkey</c> command, and (when
    /// licensed) verifies its certificate.  It requires an interpreter that
    /// supports Eagle threading.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("015a2932-70da-49c9-b37c-23dc6386a398")]
    [PluginFlags(
        PluginFlags.Primary | PluginFlags.User |
        PluginFlags.Commercial | PluginFlags.Command |
        PluginFlags.NativeCode | PluginFlags.MergeCommands |
        PluginFlags.UserInterface | PluginFlags.NoFunctions |
        PluginFlags.NoTraces)]
    internal sealed class Enterprise : _Plugins.Default, IStarted
    {
        #region Private Constants
        //
        // NOTE: This is the maximum number of milliseconds to wait when an
        //       attempt is being made to send a log request to the hot-key
        //       manager (i.e. from this plugin).
        //
        /// <summary>
        /// The maximum number of milliseconds to wait when sending a log
        /// request to the hot-key manager from this plugin.
        /// </summary>
        private static readonly int LogTimeout = 1000; /* milliseconds */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This will be non-zero if this plugin instance was directly
        //       responsible for starting the hot-key manager form thread.
        //
        /// <summary>
        /// Non-zero when this plugin instance was directly responsible for
        /// starting the hot-key manager form thread.
        /// </summary>
        private bool started;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This will be non-zero if this plugin instance was directly
        //       responsible for setting up the complaint form.
        //
        /// <summary>
        /// Non-zero when this plugin instance was directly responsible for
        /// setting up the complaint form.
        /// </summary>
        private bool complaintForm;

        ///////////////////////////////////////////////////////////////////////

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
        /// Initializes the plugin.  Verifies the license certificate (when
        /// licensed); requires interpreter threading support; starts the
        /// hot-key manager thread; adds the hot-key template packages; sets up
        /// the complaint form; evaluates the hot-key startup script; then runs
        /// the base initialization.
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
            CheckDisposed();

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

            if ((code == ReturnCode.Ok) &&
                !Utility.HaveEagleThreading(interpreter))
            {
                result = "interpreter does not support threading";
                code = ReturnCode.Error;
            }

            if (code == ReturnCode.Ok)
            {
                if ((interpreter == null) ||
                    interpreter.DoesVariableExist(VariableFlags.None,
                        HotKeyOps.NoThreadVariableName) != ReturnCode.Ok)
                {
                    code = Form.StartHotKeyManagerThread(
                        interpreter, this, clientData, false,
                        ref result);

                    if (code == ReturnCode.Ok)
                    {
                        LogOps.MaybeLogOrComplain(interpreter,
                            "StartHotKeyManagerThread OK", LogTimeout);
                    }
                }
            }

            if (code == ReturnCode.Ok)
            {
                Result localResult = null;

                code = TemplateOps.AddPackages(
                    interpreter, ref localResult);

                if (code == ReturnCode.Ok)
                {
                    LogOps.MaybeLogOrComplain(interpreter,
                        "AddHotKeyTemplatePackages OK", LogTimeout);
                }
                else
                {
                    result = localResult;
                }
            }

            if (code == ReturnCode.Ok)
            {
                if ((interpreter == null) ||
                    interpreter.DoesVariableExist(VariableFlags.None,
                        ComplaintFormOps.NoVariableName) != ReturnCode.Ok)
                {
                    Result localResult = null;

                    code = ComplaintFormOps.EvaluateSetupScript(
                        interpreter, ref complaintForm, ref localResult);

                    if (code == ReturnCode.Ok)
                    {
                        LogOps.MaybeLogOrComplain(interpreter,
                            "EvaluateComplaintFormSetupScript OK",
                            LogTimeout);
                    }
                    else
                    {
                        result = localResult;
                    }
                }
            }

            if (code == ReturnCode.Ok)
            {
                Result localResult = null;

                code = ScriptOps.EvaluateStartup(
                    interpreter, true, ref localResult);

                if (code == ReturnCode.Ok)
                {
                    LogOps.MaybeLogOrComplain(interpreter,
                        "EvaluateHotKeyStartup OK", LogTimeout);
                }
                else
                {
                    result = localResult;
                }
            }

            if (code == ReturnCode.Ok)
            {
                code = base.Initialize(interpreter, clientData, ref result);

                if (code == ReturnCode.Ok)
                {
                    LogOps.MaybeLogOrComplain(interpreter,
                        String.Format("{0}.Initialize OK",
                        typeof(Enterprise)), LogTimeout);
                }
            }

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Terminates the plugin.  Clears the certificate state (when
        /// licensed), cleans up the complaint form, stops the hot-key manager
        /// thread, and (when an interpreter is present) runs the base
        /// termination.  Also invoked from <see cref="Dispose()" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the plugin is being terminated in, or null when
        /// called from disposal.
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
            CheckDisposed();

#if LICENSING
            certificateFileName = null;
            certificate = null;
            this.Flags &= ~PluginFlags.Licensed;
#endif

            ReturnCode code;
            Result error = null;

            //
            // NOTE: Attempt to cleanup the dedicated complaint form.  If this
            //       fails, complain about it and then proceed.  This requires
            //       an interpreter context.
            //
            if (interpreter != null)
            {
                Result localResult = null;

                code = ComplaintFormOps.EvaluateCleanupScript(
                    interpreter, ref complaintForm, ref localResult);

                if (code != ReturnCode.Ok)
                    LogOps.Complain(interpreter, code, localResult);
            }

            //
            // NOTE: Attempt to stop the dedicated hot-key manager thread.  If
            //       this fails, complain about it and then proceed.
            //
            code = Form.StopHotKeyManagerThread(this, false, ref error);

            //
            // NOTE: Complain if we were unable to stop our thread.
            //
            if (code != ReturnCode.Ok)
                LogOps.Complain(interpreter, code, error);

            //
            // NOTE: If there is no interpreter, we are probably being called
            //       via Dispose, we must skip calling the base plugin in that
            //       case.
            //
            if (interpreter == null)
            {
                result = error;
                return code;
            }

            //
            // NOTE: Always terminate the base plugin, even if we "fail" at
            //       stopping our thread.
            //
            return base.Terminate(interpreter, clientData, ref result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPlugin Members
        /// <summary>
        /// Gets the named embedded resource string from the plugin assembly.
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
        /// Upon failure, receives an error message describing the problem.
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
            CheckDisposed();

            return Utility.GetAnyString(
                interpreter, this, ResourceManager, name, cultureInfo,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

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
            CheckDisposed();

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
            CheckDisposed();

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
        /// plus, when licensed, license certificate details).
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
            CheckDisposed();

            ReturnCode code = ReturnCode.Ok;
            Result localResult = HotKeyOps.FormatPluginAbout(this);

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode Options(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            CheckDisposed();

            return CommonOps.GetDefineConstants(ref result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IStarted Members
        /// <summary>
        /// Gets or sets a value indicating whether this plugin instance
        /// started the hot-key manager thread.
        /// </summary>
        public bool Started
        {
            get { CheckDisposed(); return started; }
            set { CheckDisposed(); started = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this instance has been disposed.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Throws an exception if this instance has already been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed and disposed-object
        /// checking is enabled.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, false))
                throw new ObjectDisposedException(typeof(Enterprise).Name);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance, terminating the
        /// plugin (and its hot-key manager thread) when disposing.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from
        /// <see cref="IDisposable.Dispose" />; zero if it is being called
        /// from the finalizer.
        /// </param>
        private /* protected virtual */ void Dispose(
            bool disposing /* in */
            )
        {
            if (!disposed)
            {
                if (disposing)
                {
                    ////////////////////////////////////
                    // dispose managed resources here...
                    ////////////////////////////////////

                    ReturnCode code;
                    Result result = null;

                    code = Terminate(null, null, ref result);

                    if (code != ReturnCode.Ok)
                        LogOps.Complain(code, result);
                }

                //////////////////////////////////////
                // release unmanaged resources here...
                //////////////////////////////////////

                disposed = true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        /// <summary>
        /// Finalizes an instance of the <see cref="Enterprise" /> class.
        /// </summary>
        ~Enterprise()
        {
            Dispose(false);
        }
        #endregion
    }
}
