/*
 * Environment.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

#if LICENSING
using Licensing.Sdk.Private;
#endif

using Featherlight.Components.Private;
using _Plugins = Eagle._Plugins;

namespace Featherlight
{
    /// <summary>
    /// Implements the Featherlight plugin, which provides a WPF-backed
    /// windowed interpreter host environment.  Rather than installing a host
    /// directly, on initialization it launches a dedicated interactive thread
    /// that runs the windowed shell (creating the WPF application and the
    /// primary interactive window) and registers a new-host callback so the
    /// interpreter host subsystem obtains windowed hosts; on termination it
    /// shuts that shell down.  When licensing is enabled, the plugin license
    /// certificate is verified during initialization.
    /// </summary>
    [ObjectId("95b30e18-9e1d-400b-a178-e7f32ecfff63")]
    [PluginFlags(
        PluginFlags.Primary | PluginFlags.User |
        PluginFlags.Commercial |  PluginFlags.Host |
        PluginFlags.UserInterface | PluginFlags.NoCommands |
        PluginFlags.NoFunctions | PluginFlags.NoPolicies |
        PluginFlags.NoTraces)]
    internal sealed class Environment : _Plugins.Default, IDisposable
    {
        #region Private Constants
        /// <summary>
        /// The timeout, in milliseconds, used when joining the interactive
        /// thread during shutdown (negative means wait indefinitely).
        /// </summary>
        private static readonly int ThreadJoinTimeout = -1;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: Used to synchronize access to the contained private data.
        //
        /// <summary>
        /// Used to synchronize access to the contained private data.
        /// </summary>
        private object syncRoot = new object();

        //
        // NOTE: The thread that we started [directly].
        //
        /// <summary>
        /// The interactive thread started by this plugin to run the windowed
        /// shell.
        /// </summary>
        private Thread thread;

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
        /// Constructs a new instance of the <see cref="Environment" /> plugin
        /// class.
        /// </summary>
        /// <param name="pluginData">
        /// The data used to create and configure the plugin.
        /// </param>
        public Environment(
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
        /// Initializes the plugin.  When licensing is enabled the certificate
        /// is verified; the plugin then records the interpreter, starts a
        /// dedicated interactive thread that runs the windowed shell, and
        /// calls the base initialization.
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
            CheckDisposed();

            ReturnCode code = ReturnCode.Ok;

            lock (syncRoot)
            {
                if (thread == null)
                {
                    try
                    {
#if LICENSING
                        //
                        // HACK: The license checking code here is basically a
                        //       proof-of-concept.  If it were intended to be
                        //       production code, it would also need to be done
                        //       in the Featherlight.Shell.Window.Main method
                        //       to prevent the application from starting up in
                        //       stand-alone mode if there is no valid license.
                        //
                        LicenseOps.SetupWellKnownConfigurationData(this.AppDomain);

                        code = LicenseOps.VerifyCertificate(
                            interpreter, this.Assembly, null, this, null, null, null,
                            null, null, null, null, null, null, null, null, true, false,
                            false, true, LicenseOps.UseIsolated(typeof(Environment)),
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
                            Featherlight.Shell.Window.SetPluginInterpreter(
                                interpreter);

                            thread = Engine.CreateThread(interpreter,
                                Featherlight.Shell.Window.MainThreadStart, 0,
                                true, false, true);

                            if (thread != null)
                            {
                                object obj = CommonOps.GetArguments(
                                    interpreter, clientData, true); /* string[] */

                                thread.Name = String.Format("{0}: {1}",
                                    typeof(Environment).FullName, interpreter);

                                thread.Start(obj); /* throw */

                                code = ReturnCode.Ok;
                            }
                            else
                            {
                                result = "could not create interactive thread";
                                code = ReturnCode.Error;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        result = e;
                        code = ReturnCode.Error;
                    }
                }
                else
                {
                    result = "interactive thread has already been started";
                    code = ReturnCode.Error;
                }
            }

            if (code == ReturnCode.Ok)
                return base.Initialize(interpreter, clientData, ref result);

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Terminates the plugin, shutting down the windowed shell and waiting
        /// for the interactive thread to exit before calling the base
        /// termination.
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
            CheckDisposed();

#if LICENSING
            certificateFileName = null;
            certificate = null;
            this.Flags &= ~PluginFlags.Licensed;
#endif

            ReturnCode code;
            Result error = null;

            lock (syncRoot)
            {
                if (thread != null)
                {
                    try
                    {
                        Featherlight.Shell.Window.Shutdown(); /* throw */

                        if (!thread.IsAlive ||
                            thread.Join(ThreadJoinTimeout)) /* throw */
                        {
                            code = ReturnCode.Ok;
                        }
                        else
                        {
                            error = "timeout waiting for interactive thread to exit";
                            code = ReturnCode.Error;
                        }
                    }
                    catch (Exception e)
                    {
                        error = e;
                        code = ReturnCode.Error;
                    }
                }
                else
                {
                    error = "interactive thread has not been started";
                    code = ReturnCode.Error;
                }
            }

            //
            // NOTE: If there is no interpreter, we are probably being called
            //       via Dispose, we must skip calling the base plugin in that
            //       case.
            //
            if (interpreter == null)
                return code;

            //
            // NOTE: Always terminate the plugin, even if we "fail" at stopping
            //       our thread; however, do complain if we were unable to stop
            //       our thread.
            //
            if (code != ReturnCode.Ok)
                Utility.Complain(interpreter, code, error);

            return base.Terminate(interpreter, clientData, ref result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPlugin Members
#if LICENSING
        /// <summary>
        /// Gets the file name of a license certificate for the plugin.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the certificate file name.
        /// </param>
        /// <param name="name">
        /// The certificate type name, or null for the one in use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The certificate file name, or null upon failure.
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
        /// Gets the license certificate for the plugin.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter requesting the certificate.
        /// </param>
        /// <param name="name">
        /// The certificate type name, or null for the one in use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The certificate, or null upon failure.
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
            CheckDisposed();

            ReturnCode code = ReturnCode.Ok;
            Result localResult = Utility.FormatPluginAbout(this, false);

#if LICENSING
            code = LicenseOps.AboutCertificate(
                interpreter, this, certificate, LicenseOps.UseIsolated(
                typeof(Environment)), ref localResult);
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
            CheckDisposed();

            return CommonOps.GetDefineConstants(ref result);
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
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, false))
                throw new ObjectDisposedException(typeof(Environment).Name);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from <see
        /// cref="IDisposable.Dispose" />; zero if it is being called from the
        /// finalizer.
        /// </param>
        private /* protected virtual */ void Dispose(bool disposing)
        {
            lock (syncRoot)
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
                            Utility.Complain(null, code, result);
                    }

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////

                    disposed = true;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        /// <summary>
        /// Finalizes this plugin, releasing any resources that were not
        /// released by an explicit call to <see cref="Dispose()" />.
        /// </summary>
        ~Environment()
        {
            Dispose(false);
        }
        #endregion
    }
}
