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
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

#if LICENSING
using Licensing.Sdk.Private;
#endif

using Demo.Components.Private;
using Demo.Interfaces.Public;
using _Hosts = Eagle._Hosts;
using _Plugins = Eagle._Plugins;

namespace Demo
{
    /// <summary>
    /// Implements the demo plugin.  It installs a demo host that replays a
    /// script as simulated interactive input and, when licensing is enabled,
    /// verifies the plugin's license certificate during initialization.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("1d54ed5b-9276-49d8-b4c4-4e44b86ed21f")]
    [PluginFlags(
        PluginFlags.Primary | PluginFlags.User |
        PluginFlags.Commercial | PluginFlags.Host |
        PluginFlags.NoFunctions | PluginFlags.NoPolicies |
        PluginFlags.NoTraces)]
    internal sealed class Enterprise : _Plugins.Default, IDemoPlugin, IDisposable
    {
        #region Private Constants
        //
        // HACK: Currently, the interpreter host must be this type or derive
        //       from one of these types; otherwise, the plugin will simply
        //       refuse to initialize.
        //
        /// <summary>
        /// The interpreter host types this plugin supports; the host must be
        /// one of these types or derive from one.
        /// </summary>
        private static readonly Type[] supportedHostTypes = {
            typeof(_Hosts.Console),
            typeof(_Hosts.Wrapper)
        };
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
        // NOTE: The "old" host, saved from the interpreter, prior to swapping
        //       in our demo host.
        //
        /// <summary>
        /// The original host saved from the interpreter before the demo host
        /// was swapped in.
        /// </summary>
        private IHost savedHost;

        //
        // NOTE: The "new" host (i.e. the demo host), set into the interpreter
        //       we were loaded into.
        //
        /// <summary>
        /// The demo host swapped into the interpreter this plugin was loaded
        /// into.
        /// </summary>
        private IDemoHost demoHost;

        //
        // NOTE: Did we create the demo host?  If so, we need to dispose it.
        //
        /// <summary>
        /// Non-zero if this plugin created the demo host and is therefore
        /// responsible for disposing it.
        /// </summary>
        private bool created;

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

        #region Private Helper Methods
        /// <summary>
        /// Determines whether the interpreter's current host is of a supported
        /// type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose host is checked.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero if the host type is supported; otherwise, zero.
        /// </returns>
        private bool HasSupportedHostType(
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            //
            // NOTE: If the interpreter is null, no match.
            //
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return false;
            }

            //
            // NOTE: Otherwise, if the host is null, no match.
            //
            IHost host = interpreter.Host;

            if (host == null)
            {
                error = "interpreter host not available";
                return false;
            }

            //
            // NOTE: Otherwise, if the host type is null, no match.
            //
            Type type = host.GetType();

            if (type == null)
            {
                error = "interpreter host has invalid type";
                return false;
            }

            //
            // NOTE: If there are no supported host types, no match.
            //
            if (supportedHostTypes == null)
            {
                error = "no supported host types";
                return false;
            }

            StringList list = null;

            foreach (Type supportedHostType in supportedHostTypes)
            {
                //
                // NOTE: If a supported host type is null, match.
                //
                if (supportedHostType == null)
                    return true;

                //
                // NOTE: Otherwise, if the host type exactly matches the
                //       supported host type, match.
                //
                if (type.Equals(supportedHostType))
                    return true;

                //
                // NOTE: Otherwise, if the host type derives from the
                //       supported host type, match.
                //
                if (type.IsSubclassOf(supportedHostType))
                    return true;

                //
                // NOTE: Create the list of unmatched supported host
                //       types on demand.
                //
                if (list == null)
                    list = new StringList();

                //
                // NOTE: Add this unmatched supported host type to the
                //       list.
                //
                list.Add(supportedHostType.FullName);
            }

            //
            // NOTE: Otherwise, no match.
            //
            if (list != null)
            {
                error = String.Format(
                    "must have type or subclass type matching: {0}",
                    Utility.ListToEnglish(list, ", ",
                        Characters.SpaceString, "or ", null,
                        null));
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Helper Methods
        /// <summary>
        /// Extracts a demo host (and the unwrapped client data) from the
        /// supplied client data, when present.
        /// </summary>
        /// <param name="clientData">
        /// On input, the client data possibly containing a demo host; on
        /// output, the unwrapped client data.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The demo host found in the client data, or null when none.
        /// </returns>
        private static IDemoHost GetDemoHost(
            ref IClientData clientData, /* in, out */
            ref Result error            /* out */
            )
        {
            if (clientData != null)
            {
                IAnyPair<IDemoHost, IClientData> anyPair = clientData.Data as
                    IAnyPair<IDemoHost, IClientData>;

                if (anyPair != null)
                {
                    IDemoHost host = anyPair.X;

                    if (host != null)
                        clientData = anyPair.Y;
                    else
                        error = "invalid demo host";

                    return host;
                }
                else
                {
                    error = "invalid object pair";
                }
            }
            else
            {
                error = "invalid clientData";
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the flags that suppress the console title and the script
        /// cancellation handler.
        /// </summary>
        /// <param name="hostCreateFlags">
        /// On input and output, the host creation flags to adjust.
        /// </param>
        private static void EnableHostTitleAndCancel(
            ref HostCreateFlags hostCreateFlags /* in, out */
            )
        {
            //
            // HACK: Mostly, we want the newly created demo interpreter
            //       host to skip messing with any global options (i.e.
            //       how a plugin should behave).  However, for certain
            //       tests, it is important to keep the console title
            //       updated (e.g. "host-1.2").  Also, the interactive
            //       loop does not work correctly without enabling the
            //       script cancellation event handler.
            //
            hostCreateFlags &= ~HostCreateFlags.NoTitle;
            hostCreateFlags &= ~HostCreateFlags.NoCancel;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the host creation flags for the demo host based on the
        /// interpreter's current host.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose host supplies the base flags.
        /// </param>
        /// <returns>
        /// The host creation flags for the new demo host.
        /// </returns>
        private static HostCreateFlags GetHostCreateFlags(
            Interpreter interpreter /* in */
            )
        {
            HostCreateFlags hostCreateFlags = HostCreateFlags.PluginUse;

            EnableHostTitleAndCancel(ref hostCreateFlags);

            IHost host = (interpreter != null) ? interpreter.Host : null;

            if (host != null)
            {
                hostCreateFlags = host.HostCreateFlags;

                EnableHostTitleAndCancel(ref hostCreateFlags);

                if (host.NoTitle)
                    hostCreateFlags |= HostCreateFlags.NoTitle;

                if (host.NoIcon)
                    hostCreateFlags |= HostCreateFlags.NoIcon;

                if (host.NoCancel)
                    hostCreateFlags |= HostCreateFlags.NoCancel;

                if (host.NoColor)
                    hostCreateFlags |= HostCreateFlags.NoColor;
            }

            return hostCreateFlags;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new demo host for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the demo host is created for.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied to the new host.
        /// </param>
        /// <param name="demoHost">
        /// Upon success, receives the new demo host.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode CreateDemoHost(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref IDemoHost demoHost,  /* out */
            ref Result error         /* out */
            )
        {
            try
            {
                HostCreateFlags hostCreateFlags = GetHostCreateFlags(
                    interpreter);

                demoHost = new Demo.Hosts.Demo(new HostData(null, null,
                    null, clientData, typeof(Demo.Hosts.Demo).Name,
                    interpreter, null, null, hostCreateFlags));

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the demo host from the client data, creating a new one when
        /// none is present.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter the demo host is for.
        /// </param>
        /// <param name="clientData">
        /// On input, the client data possibly containing a demo host; on
        /// output, the unwrapped client data.
        /// </param>
        /// <param name="demoHost">
        /// Upon success, receives the demo host.
        /// </param>
        /// <param name="created">
        /// Upon success, indicates whether a new demo host was created.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode GetOrCreateDemoHost(
            Interpreter interpreter,    /* in */
            ref IClientData clientData, /* in, out */
            ref IDemoHost demoHost,     /* out */
            ref bool created,           /* out */
            ref Result error            /* out */
            )
        {
            Result localError = null;

            demoHost = GetDemoHost(ref clientData, ref localError);

            if (demoHost != null)
            {
                created = false;
                return ReturnCode.Ok;
            }
            else
            {
                ReturnCode code = CreateDemoHost(interpreter, clientData,
                    ref demoHost, ref localError);

                if (code == ReturnCode.Ok)
                    created = true;
                else
                    error = localError;

                return code;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDemoPlugin Members
        /// <summary>
        /// Gets or sets the original interpreter host saved before the demo
        /// host was installed.
        /// </summary>
        public IHost SavedHost
        {
            get { CheckDisposed(); lock (syncRoot) { return savedHost; } }
            set { CheckDisposed(); lock (syncRoot) { savedHost = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the demo host installed into the interpreter.
        /// </summary>
        public IDemoHost DemoHost
        {
            get { CheckDisposed(); lock (syncRoot) { return demoHost; } }
            set { CheckDisposed(); lock (syncRoot) { demoHost = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IState Members
        /// <summary>
        /// Initializes the plugin.  When licensing is enabled the certificate
        /// is verified; the interpreter's host is then saved and replaced with
        /// the demo host before the base initialization runs.
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
            {
                bool locked = false;

                try
                {
                    interpreter.TryLockWithWait(
                        ref locked); /* TRANSACTIONAL */

                    if (locked)
                    {
                        lock (syncRoot)
                        {
                            if (savedHost == null)
                            {
                                Result error = null;

                                if (HasSupportedHostType(
                                        interpreter, ref error))
                                {
                                    code = GetOrCreateDemoHost(
                                        interpreter, ref clientData,
                                        ref demoHost, ref created,
                                        ref result);

                                    if (code == ReturnCode.Ok)
                                    {
                                        savedHost = interpreter.Host;
                                        interpreter.Host = demoHost;
                                    }
                                }
                                else
                                {
                                    if (error != null)
                                    {
                                        result = String.Format(
                                            "unsupported host type: {0}",
                                            error);
                                    }
                                    else
                                    {
                                        result = "unsupported host type";
                                    }

                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                result = "saved host already set";
                                code = ReturnCode.Error;
                            }
                        }
                    }
                    else
                    {
                        result = "interpreter is locked";
                        code = ReturnCode.Error;
                    }
                }
                catch (Exception e)
                {
                    result = e;
                    code = ReturnCode.Error;
                }
                finally
                {
                    interpreter.ExitLock(
                        ref locked); /* TRANSACTIONAL */
                }
            }

            if (code == ReturnCode.Ok)
            {
                //
                // NOTE: Initialize our base plugin using the original
                //       [unwrapped] clientData.  This is only done if
                //       we actually succeed at hooking the interpreter
                //       host.
                //
                return base.Initialize(interpreter, clientData, ref result);
            }

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Terminates the plugin, restoring the original interpreter host (and
        /// disposing the demo host when this plugin created it) before the
        /// base termination runs.
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
            bool locked = false;

            try
            {
                interpreter.TryLockWithWait(
                    ref locked); /* TRANSACTIONAL */

                if (locked)
                {
                    lock (syncRoot)
                    {
                        if (savedHost != null)
                        {
                            IDemoHost host = interpreter.Host as IDemoHost;

                            if (host != null)
                            {
                                if (Object.ReferenceEquals(host, demoHost))
                                {
                                    interpreter.Host = savedHost;
                                    savedHost = null;

                                    if (created)
                                    {
                                        ReturnCode disposeCode;
                                        Result disposeError = null;

                                        disposeCode = Utility.TryDisposeObject<IDemoHost>(
                                            ref demoHost, ref disposeError);

                                        if (disposeCode != ReturnCode.Ok)
                                            Utility.Complain(interpreter,
                                                disposeCode, disposeError);

                                        created = false;
                                    }

                                    demoHost = null;

                                    code = ReturnCode.Ok;
                                }
                                else
                                {
                                    error = "demo host mismatch";
                                    code = ReturnCode.Error;
                                }
                            }
                            else
                            {
                                error = "invalid demo host";
                                code = ReturnCode.Error;
                            }
                        }
                        else
                        {
                            error = "invalid saved host";
                            code = ReturnCode.Error;
                        }
                    }
                }
                else
                {
                    error = "interpreter is locked";
                    code = ReturnCode.Error;
                }
            }
            catch (Exception e)
            {
                result = e;
                code = ReturnCode.Error;
            }
            finally
            {
                interpreter.ExitLock(
                    ref locked); /* TRANSACTIONAL */
            }

            //
            // NOTE: Always terminate the plugin, even if we "fail" at
            //       restoring the original host; however, do complain
            //       if we were unable to restore the original host.
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
            return Utility.GetAnyString(
                interpreter, this, ResourceManager, name, cultureInfo,
                ref error);
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
            CheckDisposed();

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
                throw new ObjectDisposedException(typeof(Enterprise).Name);
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

                        if (demoHost != null)
                        {
                            if (created)
                            {
                                ReturnCode disposeCode;
                                Result disposeError = null;

                                disposeCode = Utility.TryDisposeObject<IDemoHost>(
                                    ref demoHost, ref disposeError);

                                if (disposeCode != ReturnCode.Ok)
                                    Utility.Complain(null,
                                        disposeCode, disposeError);

                                created = false;
                            }

                            demoHost = null;
                        }
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
        ~Enterprise()
        {
            Dispose(false);
        }
        #endregion
    }
}
