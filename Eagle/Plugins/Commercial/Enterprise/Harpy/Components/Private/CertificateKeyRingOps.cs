/*
 * CertificateKeyRingOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;
using PolicyOps = Licensing.Components.Private.CertificatePolicyOps;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

using KeyPairDictionary =
    System.Collections.Generic.Dictionary<string,
        Licensing.Interfaces.Private.IKeyPair>;

using PolicyDictionary =
    System.Collections.Generic.Dictionary<
        Eagle._Components.Public.PolicyType,
        Eagle._Components.Public.ExecutionPolicy>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides static helper methods for locating, loading, and managing
    /// the trusted key ring (and key pair) files used by the Harpy
    /// licensing certificate subsystem.
    /// </summary>
    [ObjectId("a4e84389-8903-4ced-861d-a0e18226b2f1")]
    internal static class CertificateKeyRingOps
    {
        #region Private Constants
        //
        // HACK: This is purposely not read-only.
        //
        /* CORE? */
        /// <summary>
        /// Holds the set of bootstrap key ring types that are processed,
        /// in order, when discovering and loading bootstrap key ring files.
        /// </summary>
        private static BootstrapType[] allBootstrapTypes = {
            BootstrapType.License, BootstrapType.Script,
            BootstrapType.General
        };
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the collection of all bootstrap key ring types that should
        /// be considered when processing bootstrap key ring files.
        /// </summary>
        /// <returns>
        /// The collection of supported bootstrap key ring types.
        /// </returns>
        private static IEnumerable<BootstrapType> GetBootstrapTypes() /* CORE? */
        {
            return allBootstrapTypes;
        }

        ///////////////////////////////////////////////////////////////////////

        #region New Interpreter Callback Methods
        /// <summary>
        /// Obtains the active interpreter, to be used as the "other"
        /// interpreter, and verifies that it is available and distinct from
        /// the supplied interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that the resulting other interpreter must be
        /// distinct from.
        /// </param>
        /// <param name="otherInterpreter">
        /// Upon success, receives the active interpreter to be used as the
        /// other interpreter.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode GetOtherInterpreter( /* CORE? */
            Interpreter interpreter,          /* in */
            ref Interpreter otherInterpreter, /* out */
            ref Result error                  /* out */
            )
        {
            Interpreter localInterpreter = Interpreter.GetActive();

            if (localInterpreter == null)
            {
                error = "other interpreter not available";
                return ReturnCode.Error;
            }

            if (Object.ReferenceEquals(localInterpreter, interpreter))
            {
                error = "other interpreter not distinct";
                return ReturnCode.Error;
            }

            otherInterpreter = localInterpreter;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the environment variable used to hold the
        /// certificate key ring file name.
        /// </summary>
        /// <returns>
        /// The environment variable name.
        /// </returns>
        private static string GetFileEnvVarName() /* CORE? */
        {
            return CertificateSharedOps.GetEnvVarName(
                typeof(CertificateKeyRingOps).Name, typeof(Certificate).Name);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate key ring file name associated with the
        /// specified plugin data, if any.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to obtain the certificate file name from.
        /// </param>
        /// <returns>
        /// The certificate file name, or null if one is not available.
        /// </returns>
        private static string GetFileName( /* CORE? */
            IPluginData pluginData /* in */
            )
        {
            ILicenseCertificateData licenseCertificateData =
                CertificateSharedOps.GetLicenseCertificateData(
                    pluginData);

            if (licenseCertificateData != null)
            {
                string fileName =
                    licenseCertificateData.CertificateFileName;

                if (fileName != null)
                    return fileName;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate key ring file name from the configured
        /// environment variable.
        /// </summary>
        /// <returns>
        /// The configured certificate file name, or null if it is not set.
        /// </returns>
        public static string GetFileName() /* CORE? */
        {
            return Configuration.GetVariable(GetFileEnvVarName());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the current value of the certificate file name environment
        /// variable and sets it to the certificate file name obtained from
        /// the specified plugin, so it can be used while loading the plugin
        /// into a new interpreter.
        /// </summary>
        /// <param name="plugin">
        /// The plugin to obtain the certificate file name from.
        /// </param>
        /// <param name="envVarName">
        /// Receives the name of the environment variable that was set.
        /// </param>
        /// <param name="savedEnvVarValue">
        /// Receives the previously saved value of the environment variable.
        /// </param>
        private static void BeginFileEnvVarName( /* CORE? */
            IPlugin plugin,             /* in */
            out string envVarName,      /* out */
            out string savedEnvVarValue /* out */
            )
        {
            //
            // NOTE: Grab the certificate file name from the other plugin
            //       (from within the other interpreter) and store it in
            //       our private environment variable while loading the
            //       plugin into the new interpreter.
            //
            envVarName = GetFileEnvVarName();
            savedEnvVarValue = null;

            if (envVarName != null)
            {
                savedEnvVarValue = Configuration.GetVariable(
                    envVarName);

                string envVarValue = GetFileName(plugin);

                if (envVarValue != null)
                {
                    Utility.SetEnvironmentVariable(
                        envVarName, envVarValue, false);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores or removes the certificate file name environment
        /// variable that was previously set by
        /// <see cref="BeginFileEnvVarName" />.
        /// </summary>
        /// <param name="envVarName">
        /// The name of the environment variable to restore or remove; set
        /// to null on return.
        /// </param>
        /// <param name="savedEnvVarValue">
        /// The previously saved value to restore; set to null on return.
        /// </param>
        private static void EndFileEnvVarName( /* CORE? */
            ref string envVarName,      /* in, out */
            ref string savedEnvVarValue /* in, out */
            )
        {
            if (envVarName != null)
            {
                if (savedEnvVarValue != null)
                {
                    Utility.SetEnvironmentVariable(
                        envVarName, savedEnvVarValue, false);

                    savedEnvVarValue = null;
                }
                else
                {
                    Utility.UnsetEnvironmentVariable(
                        envVarName);
                }

                envVarName = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the common setup shared by the new and use interpreter
        /// callbacks, including locating the other interpreter, copying
        /// trusted hashes and trace writers, and loading the peer plugin.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter being created or used.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the callback.
        /// </param>
        /// <param name="newInterpreter">
        /// Non-zero if the interpreter is newly created.
        /// </param>
        /// <param name="otherInterpreter">
        /// Upon success, receives the other interpreter.
        /// </param>
        /// <param name="otherPlugin">
        /// Upon success, receives the peer plugin from the other
        /// interpreter.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode InterpreterCallbackPrologue( /* CORE? */
            Interpreter interpreter,          /* in */
            IClientData clientData,           /* in */
            bool newInterpreter,              /* in */
            ref Interpreter otherInterpreter, /* out */
            ref IPlugin otherPlugin,          /* out */
            ref Result result                 /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            Result localResult = null; /* REUSED */

            if (GetOtherInterpreter(
                    interpreter, ref otherInterpreter,
                    ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            //
            // BUGFIX: This method attempts to load the Security.Core
            //         plugin before the CreateInterpreterForSettings
            //         method has any chance to (subsequently) call the
            //         CopyTrustedHashes method (i.e. we are currently
            //         executing from within our NewInterpreterCallback
            //         callback method).  This means that if we do not
            //         call the CopyTrustedHashes method here, plugin
            //         loading can fail due to IsFileTrusted not having
            //         the list of trusted hashes (i.e. from inside the
            //         CertificatePluginOps.Check method).  This really
            //         only applies with the .NET Core runtime on Unix
            //         platforms.
            //
            /* NO RESULT */
            Utility.CopyTrustedHashes(otherInterpreter, interpreter);

            localResult = null;

            if (CertificateTraceOps.ShouldForceCloneForPolicy())
            {
                if (CertificateTraceOps.CloneTextWriter(
                        interpreter, otherInterpreter, clientData,
                        ref localResult) != ReturnCode.Ok)
                {
                    result = localResult;
                    return ReturnCode.Error;
                }
            }
            else
            {
                if (CertificateTraceOps.CopyTextWriter(
                        interpreter, otherInterpreter, clientData,
                        ref localResult) != ReturnCode.Ok)
                {
                    result = localResult;
                    return ReturnCode.Error;
                }
            }

            localResult = null;

            if (newInterpreter)
            {
                if (PolicyOps.GetOrLoadPlugin(
                        otherInterpreter, ref otherPlugin,
                        ref localResult) != ReturnCode.Ok)
                {
                    result = localResult;
                    return ReturnCode.Error;
                }
            }
            else
            {
                if (PolicyOps.GetPlugin(
                        otherInterpreter, ref otherPlugin,
                        ref localResult) != ReturnCode.Ok)
                {
                    result = localResult;
                    return ReturnCode.Error;
                }
            }

            //
            // NOTE: If the peer plugin in the other interpreter has the
            //       "Allow Signed Only" policy enabled, then keep track
            //       of the created interpreter for later use.  It will
            //       be used to get the key pair used when verifying the
            //       trusted key ring file was signed.  This key pair is
            //       saved by the file policy implementation itself when
            //       the SaveApprovedData execution policy is set.  It
            //       should be noted this data passing technique requires
            //       the peer plugin in the new interpreter to be loaded
            //       into this application domain, since the "approved
            //       key pair" data is stored in static data (i.e. which
            //       is per-application domain).  Previously, there was
            //       a call here to enable plugin isolation for the new
            //       interpreter; however, it was removed because it did
            //       not really serve a purpose and it prevented this
            //       data passing technique from working right.  Here is
            //       a more detailed explanation:
            //
            //       1. If the original plugin was loaded into the main
            //          application domain, then loading the new plugin
            //          into an isolated application domain did not make
            //          much sense as the new interpreter is "isolated
            //          enough" for script evaluation purposes and the
            //          plugin assembly itself cannot be unloaded since
            //          it is already loaded into the main application
            //          domain.
            //
            //       2. If the original plugin was loaded in an isolated
            //          application domain, then we are executing within
            //          that application domain right now.  Furthermore,
            //          the new interpreter belongs to this application
            //          domain as well which means that the new plugin,
            //          loaded as "non-isolated", will end up in this
            //          (isolated, non-main) application domain as well.
            //
            MaybeSetInterpreter(
                clientData, interpreter, otherInterpreter,
                otherPlugin);

            //
            // NOTE: At this point, we are all done.
            //
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the common teardown shared by the new and use
        /// interpreter callbacks, copying policy data and trusted key ring
        /// data from the other interpreter and clearing approved key pairs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter being created or used.
        /// </param>
        /// <param name="otherInterpreter">
        /// The other interpreter that data is copied from.
        /// </param>
        /// <param name="otherPlugin">
        /// The peer plugin from the other interpreter.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode InterpreterCallbackEpilogue( /* CORE? */
            Interpreter interpreter,      /* in */
            Interpreter otherInterpreter, /* in */
            IPlugin otherPlugin,          /* in */
            ref Result result             /* out */
            )
        {
            IPlugin plugin = null;
            Result localResult = null; /* REUSED */

            if (PolicyOps.GetPlugin(
                    interpreter, ref plugin,
                    ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            localResult = null;

            if (PolicyOps.CopyData(
                    otherPlugin, plugin, ExecutionPolicy.None,
                    false, ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            //
            // NOTE: At first glance, this appears to be wrong.  What about
            //       key rings loaded during license verification?  That is
            //       actually not relevant here.  All key rings (files) are
            //       always scripts.  Hence, the script key ring is used to
            //       copy into the data for the newly created interpreter,
            //       so that it can load any key ring that would have been
            //       trusted by the calling (parent) interpreter.  It is up
            //       to the higher level methods to deal with selecting the
            //       destination key ring for any loaded key pairs.
            //
            string keyRingName = GetName(null, PolicyType.Script); /* EXEMPT */

            localResult = null;

            if (CertificateKeyRingState.CopyTrusted(
                    otherInterpreter, interpreter,
                    keyRingName, keyRingName, false, false,
                    false, ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            //
            // HACK: Make sure that no approved key pairs are currently set
            //       for the interpreter being used, which may not actually
            //       be completely new.  This should eliminate errors while
            //       adding an approved key pair from policy implementation
            //       methods.
            //
            /* IGNORED */
            CertificateKeyPairState.UnlockAllApproved(interpreter, true);

            /* IGNORED */
            CertificateKeyPairState.RemoveAllApproved(interpreter, true);

            //
            // NOTE: At this point, we are all done.
            //
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Implements the new-interpreter callback used when loading a key
        /// ring file into a brand new interpreter, evaluating the package
        /// require script and transferring the resulting state back.
        /// </summary>
        /// <param name="interpreter">
        /// The newly created interpreter.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the callback.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode NewInterpreterCallback( /* CORE? */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
            Interpreter otherInterpreter = null;
            IPlugin otherPlugin = null;
            Result localResult = null; /* REUSED */

            if (InterpreterCallbackPrologue(
                    interpreter, clientData, true, ref otherInterpreter,
                    ref otherPlugin, ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            string text;

            localResult = null;

            text = PolicyOps.GetPackageRequireScript(
                otherInterpreter, ref localResult);

            if (text == null)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            string envVarName;
            string savedEnvVarValue;

            BeginFileEnvVarName(
                otherPlugin, out envVarName, out savedEnvVarValue);

            try
            {
                localResult = null;

                if (interpreter.EvaluateTrustedScript(text,
                        TrustFlags.MarkTrusted
#if ISOLATED_PLUGINS
                            | TrustFlags.NoIsolatedPlugins
#endif
                        , ref localResult) == ReturnCode.Ok)
                {
                    localResult = null; /* DISCARD */
                }
                else
                {
                    result = localResult;
                    return ReturnCode.Error;
                }
            }
            finally
            {
                EndFileEnvVarName(
                    ref envVarName, ref savedEnvVarValue);
            }

            localResult = null;

            if (InterpreterCallbackEpilogue(
                    interpreter, otherInterpreter, otherPlugin,
                    ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Implements the use-interpreter callback used when loading a key
        /// ring file into a previously cached interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter being reused.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the callback.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode UseInterpreterCallback( /* CORE? */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
            Interpreter otherInterpreter = null;
            IPlugin otherPlugin = null;
            Result localResult = null; /* REUSED */

            if (InterpreterCallbackPrologue(
                    interpreter, clientData, false, ref otherInterpreter,
                    ref otherPlugin, ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            localResult = null;

            if (InterpreterCallbackEpilogue(
                    interpreter, otherInterpreter, otherPlugin,
                    ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Implements the free-interpreter callback used to lock approved
        /// key pairs and reset policy data before an interpreter used to
        /// load a key ring file is disposed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter being freed.
        /// </param>
        /// <param name="clientData">
        /// The client data associated with the callback; null indicates the
        /// interpreter is being disposed.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode FreeInterpreterCallback( /* CORE? */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ref Result result        /* out */
            )
        {
            //
            // NOTE: Only handle this callback when the client data is not
            //       null; otherwise, the interpreter is being disposed.
            //
            if (clientData != null)
            {
                //
                // HACK: This is a somewhat sloppy solution to a somewhat
                //       dumb problem.  The problem (obviously?) involves
                //       handling of "approved key pairs" when loading an
                //       key ring file using a brand new interpreter or a
                //       previously cached interpreter.  In the event the
                //       interpreter was not cached, it will be disposed;
                //       however, this presents a problem for us because
                //       our static data for this plugin will be removed
                //       prior to the key ring loader in this class being
                //       able to make use of it.  In order to work around
                //       this, we lock out changes to "approved key pairs"
                //       for the interpreter being used until after the
                //       key ring loader in this class can obtain them.
                //
                /* IGNORED */
                CertificateKeyPairState.LockAllApproved(
                    interpreter, false);
            }

            ///////////////////////////////////////////////////////////////////

            if (interpreter != null)
            {
                IPlugin plugin = null;
                Result localError = null;

                if (PolicyOps.GetPlugin(
                        interpreter, ref plugin,
                        ref localError) != ReturnCode.Ok)
                {
                    result = localError;
                    return ReturnCode.Error;
                }

                ResultList localErrors = null;

                if (PolicyOps.ResetData(
                        plugin, true, false,
                        ref localErrors) != ReturnCode.Ok)
                {
                    result = localErrors;
                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the current interpreter creation callbacks and installs the
        /// callbacks used by this class while loading key ring files.
        /// </summary>
        /// <param name="savedNewInterpreterCallback">
        /// Receives the previously installed new-interpreter callback.
        /// </param>
        /// <param name="savedUseInterpreterCallback">
        /// Receives the previously installed use-interpreter callback.
        /// </param>
        /// <param name="savedFreeInterpreterCallback">
        /// Receives the previously installed free-interpreter callback.
        /// </param>
        private static void BeginInterpreterCallbacks( /* CORE? */
            ref EventCallback savedNewInterpreterCallback, /* out */
            ref EventCallback savedUseInterpreterCallback, /* out */
            ref EventCallback savedFreeInterpreterCallback /* out */
            )
        {
            savedNewInterpreterCallback = Interpreter.NewInterpreterCallback;
            savedUseInterpreterCallback = Interpreter.UseInterpreterCallback;
            savedFreeInterpreterCallback = Interpreter.FreeInterpreterCallback;

            Interpreter.NewInterpreterCallback = NewInterpreterCallback;
            Interpreter.UseInterpreterCallback = UseInterpreterCallback;
            Interpreter.FreeInterpreterCallback = FreeInterpreterCallback;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the interpreter creation callbacks that were previously
        /// saved by <see cref="BeginInterpreterCallbacks" />.
        /// </summary>
        /// <param name="savedNewInterpreterCallback">
        /// The saved new-interpreter callback to restore; set to null on
        /// return.
        /// </param>
        /// <param name="savedUseInterpreterCallback">
        /// The saved use-interpreter callback to restore; set to null on
        /// return.
        /// </param>
        /// <param name="savedFreeInterpreterCallback">
        /// The saved free-interpreter callback to restore; set to null on
        /// return.
        /// </param>
        private static void EndInterpreterCallbacks( /* CORE? */
            ref EventCallback savedNewInterpreterCallback, /* in, out */
            ref EventCallback savedUseInterpreterCallback, /* in, out */
            ref EventCallback savedFreeInterpreterCallback /* in, out */
            )
        {
            Interpreter.NewInterpreterCallback = savedNewInterpreterCallback;
            Interpreter.UseInterpreterCallback = savedUseInterpreterCallback;
            Interpreter.FreeInterpreterCallback = savedFreeInterpreterCallback;

            savedNewInterpreterCallback = null;
            savedUseInterpreterCallback = null;
            savedFreeInterpreterCallback = null;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        //
        // TODO: Move this to the Eagle core library?  Can anybody else really
        //       use this?
        //
        /// <summary>
        /// Extracts the array element name (the text between the parentheses)
        /// from the specified array variable name.
        /// </summary>
        /// <param name="name">
        /// The array variable name to parse.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The extracted element name, or null if the name could not be
        /// parsed.
        /// </returns>
        private static string GetArrayElementName( /* CORE? */
            string name,     /* in */
            ref Result error /* out */
            )
        {
            if (name == null)
            {
                error = "invalid name";
                return null;
            }

            int length = name.Length;

            if (length == 0)
            {
                error = "empty name";
                return null;
            }

            int openIndex = name.IndexOf(Characters.OpenParenthesis);

            if (openIndex == Index.Invalid)
            {
                error = "missing open parenthesis";
                return null;
            }

            if (openIndex >= (length - 1))
            {
                error = "nothing after open parenthesis";
                return null;
            }

            int closeIndex = name.LastIndexOf(Characters.CloseParenthesis);

            if (closeIndex == Index.Invalid)
            {
                error = "missing close parenthesis";
                return null;
            }

            if (closeIndex != (length - 1))
            {
                error = "extra after close parenthesis";
                return null;
            }

            if (closeIndex <= openIndex)
            {
                error = "close parenthesis must occur after open parenthesis";
                return null;
            }

            return name.Substring(openIndex + 1, closeIndex - openIndex - 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a dictionary of key pairs from the specified settings,
        /// parsing each key pair entry along with any associated metadata,
        /// key usage, expiration, key domains, and key groups.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file the settings were loaded from.
        /// </param>
        /// <param name="settings">
        /// The settings to parse the key pairs from.
        /// </param>
        /// <param name="keyPair">
        /// The signing (parent) key pair associated with the settings.
        /// </param>
        /// <param name="pvk">
        /// Non-zero if the key pair data is in the PVK format.
        /// </param>
        /// <param name="password">
        /// The optional password used to decrypt the key pair data.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to parse the public key portion.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to parse the private key portion.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the parsed key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode GetKeyPairsFromSettings( /* CORE? */
            string fileName,                /* in */
            StringDictionary settings,      /* in */
            IKeyPair keyPair,               /* in */
            bool pvk,                       /* in */
            string password,                /* in: OPTIONAL */
            bool publicKey,                 /* in */
            bool privateKey,                /* in */
            bool overwrite,                 /* in */
            ref KeyPairDictionary keyPairs, /* out */
            ref Result error                /* out */
            )
        {
            if (settings == null)
            {
                error = "invalid settings";
                return ReturnCode.Error;
            }

            KeyPairDictionary localKeyPairs = null;

            string pattern = String.Format(Constants.SettingsVariableFormat,
                Constants.KeyPairsVariableName, Characters.Asterisk);

            foreach (KeyValuePair<string, string> pair in settings)
            {
                string value = pair.Value;

                if (value == null)
                    continue;

                if (!Parser.StringMatch(
                        null, pair.Key, 0, pattern, 0, false))
                {
                    continue;
                }

                IKeyPair localKeyPair = CertificateDataOps.ParseKeyPairData(
                    fileName, value, pvk, password, publicKey, privateKey,
                    ref error);

                if (localKeyPair == null)
                    return ReturnCode.Error;

                localKeyPair.Parent = keyPair;

                if (localKeyPairs == null)
                    localKeyPairs = new KeyPairDictionary();

                string name = GetArrayElementName(pair.Key, ref error);

                if (name == null)
                    return ReturnCode.Error;

                if (!overwrite &&
                    localKeyPairs.ContainsKey(name)) /* EXEMPT */
                {
                    //
                    // HACK: In theory, this condition cannot be hit.
                    //
                    error = String.Format(
                        "can't add {0}: key pair already exists",
                        Utility.FormatWrapOrNull(name));

                    return ReturnCode.Error;
                }

                //
                // NOTE: Is there some identifier [metadata] about this key
                //       pair present in the settings?
                //
                if (settings.TryGetValue(String.Format(
                        Constants.SettingsVariableFormat,
                        Constants.KeyMetadataVariableName, name), out value))
                {
                    IIdentifier identifier = null;

                    if (CertificateDataOps.ParseIdentifier(value,
                            ref identifier, ref error) == ReturnCode.Ok)
                    {
                        /* IGNORED */
                        CertificateDataOps.CopyIdentifier(
                            identifier, localKeyPair as IIdentifier);
                    }
                    else
                    {
                        return ReturnCode.Error;
                    }
                }

                //
                // HACK: Make sure the new key pair usage contains all (?)
                //       restrictions that are present in signing (parent)
                //       key pair.
                //
                string keyUsage = null;

                if (!CertificateSharedOps.RestrictKeyUsage(
                        keyPair, ref keyUsage, ref error))
                {
                    return ReturnCode.Error;
                }

                //
                // NOTE: Is there some key usage [metadata] about this key
                //       pair present in the settings?
                //
                if (settings.TryGetValue(String.Format(
                        Constants.SettingsVariableFormat,
                        Constants.KeyUsageVariableName, name), out value))
                {
                    if (value != null)
                        value = value.Trim();

                    if (Utility.VerifyAttributeFlags(
                            value, true, true, ref error))
                    {
                        if ((keyUsage != null) &&
                            !CertificateSharedOps.ChangeKeyUsage(
                                value, keyUsage, ref value, ref error))
                        {
                            return ReturnCode.Error;
                        }

                        localKeyPair.KeyUsage = value;
                    }
                    else
                    {
                        return ReturnCode.Error;
                    }
                }
                else if (keyUsage != null)
                {
                    localKeyPair.KeyUsage = keyUsage;
                }

                //
                // NOTE: Is there an expiration date for this key pair
                //       present in the settings?
                //
                if (settings.TryGetValue(String.Format(
                        Constants.SettingsVariableFormat,
                        Constants.KeyExpirationVariableName, name),
                        out value))
                {
                    if (value != null)
                        value = value.Trim();

                    DateTime dateTime = DateTime.MinValue;

                    if (CertificateDataOps.TryParseUniversalTimeStamp(
                            value, ref dateTime, ref error))
                    {
                        localKeyPair.KeyExpiration = dateTime;
                    }
                    else
                    {
                        return ReturnCode.Error;
                    }
                }

                //
                // NOTE: Is there a list of associated key domains with this
                //       key pair present in the settings?
                //
                if (settings.TryGetValue(String.Format(
                        Constants.SettingsVariableFormat,
                        Constants.KeyDomainsVariableName, name), out value))
                {
                    StringList list = null;

                    if (Parser.SplitList(
                            null, value, 0, Length.Invalid, true, ref list,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    foreach (string element in list)
                    {
                        if (String.IsNullOrEmpty(element))
                            continue;

                        if (CertificateDataOps.CheckHostName(
                                element, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        /* NO RESULT */
                        localKeyPair.AddKeyDomain(element);
                    }
                }

                //
                // NOTE: Is there a list of associated key tokens with this
                //       key pair present in the settings?
                //
                if (settings.TryGetValue(String.Format(
                        Constants.SettingsVariableFormat,
                        Constants.KeyGroupsVariableName, name), out value))
                {
                    StringList list = null;

                    if (Parser.SplitList(
                            null, value, 0, Length.Invalid, true, ref list,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    foreach (string element in list)
                    {
                        if (String.IsNullOrEmpty(element))
                            continue;

                        byte[] publicKeyToken = null;

                        if (CertificateDataOps.ParsePublicKeyToken(
                                element, ref publicKeyToken,
                                ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        /* NO RESULT */
                        localKeyPair.AddKeyGroup(publicKeyToken);
                    }
                }

                /* IGNORED */
                CertificateDataOps.MaybeSetAsKeyPair(
                    localKeyPair as IIdentifierBase, name);

                localKeyPairs[name] = localKeyPair;
            }

            keyPairs = localKeyPairs;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks each key pair in the specified dictionary as approved or
        /// disapproved.
        /// </summary>
        /// <param name="keyPairs">
        /// The key pairs to mark.
        /// </param>
        /// <param name="approved">
        /// Non-zero to mark the key pairs as approved; zero to mark them as
        /// disapproved.
        /// </param>
        /// <returns>
        /// The number of key pairs whose approval state was changed.
        /// </returns>
        private static int MarkKeyPairsApproved( /* CORE? */
            KeyPairDictionary keyPairs, /* in */
            bool approved               /* in */
            )
        {
            int count = 0;

            if (keyPairs != null)
            {
                foreach (KeyValuePair<string, IKeyPair> pair in keyPairs)
                {
                    IKeyPair keyPair = pair.Value;

                    if (keyPair == null)
                        continue;

                    if (approved)
                    {
                        if (keyPair.MarkApproved())
                            count++;
                    }
                    else
                    {
                        if (keyPair.MarkDisapproved())
                            count++;
                    }
                }
            }

            return count;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the interpreter stored in the data of the specified client
        /// data, if any.
        /// </summary>
        /// <param name="clientData">
        /// The client data to obtain the interpreter from.
        /// </param>
        /// <returns>
        /// The interpreter, or null if one is not available.
        /// </returns>
        private static Interpreter GetInterpreter( /* CORE? */
            IClientData clientData /* in */
            )
        {
            if (clientData == null)
                return null;

            return clientData.Data as Interpreter;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stores the specified interpreter in the client data when the peer
        /// plugin in the other interpreter has the "allow signed only"
        /// policy enabled.
        /// </summary>
        /// <param name="clientData">
        /// The client data to store the interpreter in.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter to store.
        /// </param>
        /// <param name="otherInterpreter">
        /// The other interpreter whose policy is checked.
        /// </param>
        /// <param name="otherPluginData">
        /// The peer plugin data whose policy is checked.
        /// </param>
        private static void MaybeSetInterpreter( /* CORE? */
            IClientData clientData,       /* in */
            Interpreter interpreter,      /* in */
            Interpreter otherInterpreter, /* in */
            IPluginData otherPluginData   /* in */
            )
        {
            if (clientData == null)
                return;

            if (PolicyOps.IsAnyBasePolicyAllowSignedOnly(
                    otherInterpreter, otherPluginData))
            {
                clientData.Data = interpreter;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash value of the specified script file.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to use when hashing the file.
        /// </param>
        /// <param name="fileName">
        /// The name of the file to hash.
        /// </param>
        /// <param name="noRemote">
        /// Non-zero to disallow hashing of remote files.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The computed hash value, or null on failure.
        /// </returns>
        private static byte[] GetHashValue( /* CORE? */
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            bool noRemote,           /* in */
            ref Result error         /* out */
            )
        {
            return Utility.HashScriptFile(
                interpreter, fileName, noRemote, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the automatic loading of key rings is
        /// permitted, taking into account the relevant environment variable
        /// and execution policy flags.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose execution policy is considered.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose execution policy is considered.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy to consider.
        /// </param>
        /// <returns>
        /// Non-zero if the loading of key rings is permitted; otherwise,
        /// zero.
        /// </returns>
        public static bool CanLoadKeyPairs( /* CORE? */
            IPluginData pluginData, /* in */
            PolicyType policyType,  /* in */
            ExecutionPolicy? policy /* in: OPTIONAL */
            )
        {
            //
            // NOTE: See if the environment variable is present that forbids
            //       the [automatic] loading of key rings.
            //
            if (Configuration.DoesVariableExist(
                    Constants.NoLoadKeyRingsEnvVarName))
            {
                return false;
            }

            //
            // NOTE: If the specified execution policy has the NoLoadKeyRings
            //       flag, forbid the loading of key rings.
            //
            if (Utility.HasFlags(policy, ExecutionPolicy.NoLoadKeyRings, true))
                return false;

            //
            // NOTE: If the policy is not present, then loading of key rings is
            //       still permitted (i.e. as the NoLoadKeyRings flag cannot be
            //       present); however, the non-plugin execution policy must be
            //       checked as well.  This call will now check both.
            //
            if (Utility.HasFlags(PolicyOps.GetPolicy(pluginData,
                    policyType), ExecutionPolicy.NoLoadKeyRings, true))
            {
                return false;
            }

            //
            // NOTE: Otherwise, the NoLoadKeyRings flag is not present in any
            //       supported execution policy context; therefore, allow the
            //       loading of key rings.
            //
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the settings from the specified key ring (script) file by
        /// evaluating it within a suitably configured interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to use when loading the settings.
        /// </param>
        /// <param name="fileName">
        /// The name of the key ring file to load.
        /// </param>
        /// <param name="pushClientData">
        /// The client data pushed while loading the settings.
        /// </param>
        /// <param name="callbackClientData">
        /// The client data passed to the interpreter callbacks.
        /// </param>
        /// <param name="scriptDataFlags">
        /// The flags controlling how the script file is loaded.
        /// </param>
        /// <param name="settings">
        /// Upon success, receives the loaded settings.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode LoadKeyPairsSettings( /* CORE? */
            Interpreter interpreter,             /* in */
            string fileName,                     /* in */
            IClientData pushClientData,          /* in */
            IClientData callbackClientData,      /* in */
            ref ScriptDataFlags scriptDataFlags, /* in, out */
            ref StringDictionary settings,       /* out */
            ref Result error                     /* out */
            )
        {
            CertificateKeyRingState.BeginPending();

            try
            {
#if DEMO_KEY_PAIRS || DEMO_EDITION
                CertificateDemoState.BeginPendingFileName(
                    fileName);

                try
                {
#endif
                    return Utility.LoadSettingsViaScriptFile(
                        interpreter, pushClientData, callbackClientData,
                        fileName, ref scriptDataFlags, ref settings,
                        ref error);
#if DEMO_KEY_PAIRS || DEMO_EDITION
                }
                finally
                {
                    CertificateDemoState.EndPendingFileName(fileName);
                }
#endif
            }
            finally
            {
                CertificateKeyRingState.EndPending();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Filters the specified key pairs, returning only those whose
        /// approval state matches the requested value.
        /// </summary>
        /// <param name="keyPairs">
        /// The key pairs to filter.
        /// </param>
        /// <param name="approved">
        /// The approval state to match, or null to match any state.
        /// </param>
        /// <param name="errorOnEmpty">
        /// Non-zero to treat an empty result as an error.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The filtered key pairs, or null on failure.
        /// </returns>
        private static KeyPairDictionary FilterByApproved( /* CORE? */
            KeyPairDictionary keyPairs, /* in */
            bool? approved,             /* in */
            bool errorOnEmpty,          /* in */
            ref Result error            /* out */
            )
        {
            if (keyPairs == null)
            {
                error = "invalid key pair list";
                return null;
            }

            KeyPairDictionary localKeyPairs = new KeyPairDictionary();

            foreach (KeyValuePair<string, IKeyPair> pair in keyPairs)
            {
                IKeyPair keyPair = pair.Value;

                if (keyPair == null)
                    continue;

                if ((approved == null) ||
                    (keyPair.IsApproved() == (bool)approved))
                {
                    localKeyPairs.Add(pair.Key, keyPair);
                }
            }

            if (errorOnEmpty && (localKeyPairs.Count == 0))
            {
                error = String.Format(
                    "no {0}key pairs were loaded", (approved == null) ?
                    String.Empty : (bool)approved ? "approved " :
                    "unapproved ");

                return null;
            }

            return localKeyPairs;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the key pairs from the specified key ring file, using a
        /// dedicated interpreter, honoring the supplied execution policy and
        /// optionally tracking approved key pairs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to use when loading the key pairs.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy to honor while loading.
        /// </param>
        /// <param name="fileName">
        /// The name of the key ring file to load.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="pvk">
        /// Non-zero if the key pair data is in the PVK format.
        /// </param>
        /// <param name="password">
        /// The optional password used to decrypt the key pair data.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to load the public key portion.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to load the private key portion.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="errorOnEmpty">
        /// Non-zero to treat the absence of loaded key pairs as an error.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the loaded key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode LoadKeyPairs( /* CORE? */
            Interpreter interpreter,        /* in */
            ExecutionPolicy? policy,        /* in: OPTIONAL */
            string fileName,                /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            bool pvk,                       /* in */
            string password,                /* in: OPTIONAL */
            bool publicKey,                 /* in */
            bool privateKey,                /* in */
            bool overwrite,                 /* in */
            bool errorOnEmpty,              /* in */
            ref KeyPairDictionary keyPairs, /* out */
            ref Result error                /* out */
            )
        {
            bool locked = false;

            try
            {
                //
                // HACK: This is "dangerous" in terms of the potential for
                //       deadlocks; however, it is needed for security in
                //       order to prevent another thread from changing the
                //       static interpreter callbacks that we rely upon to
                //       fully setup the new interpreter state.  Given how
                //       the key ring (script) files are evaluated and the
                //       typical contents (i.e. static-data only scripts),
                //       this should be safe.  If key ring (script) files
                //       are changed at some point to allow more complex
                //       commands to be used (e.g. [object]), this will no
                //       longer be safe.
                //
                Interpreter.TryStaticLock(
                    Constants.InterpreterCreateLockTimeout,
                    ref locked); /* TRANSACTIONAL */

                if (locked)
                {
                    EventCallback savedNewInterpreterCallback = null;
                    EventCallback savedUseInterpreterCallback = null;
                    EventCallback savedFreeInterpreterCallback = null;

                    BeginInterpreterCallbacks(
                        ref savedNewInterpreterCallback,
                        ref savedUseInterpreterCallback,
                        ref savedFreeInterpreterCallback);

                    try
                    {
                        //
                        // NOTE: Use the maximum security settings while
                        //       loading the (key ring) settings file.
                        //       Also, make sure that the "Security" flag
                        //       is not set when creating the interpreter
                        //       that will be used to load (evaluate) the
                        //       settings file.
                        //
                        ScriptDataFlags scriptDataFlags =
                            ScriptDataFlags.ForKeyRingLoader;

                        //
                        // HACK: If requested using the execution policy,
                        //       enable the application domain isolation
                        //       when loading the settings file.
                        //
                        if (Utility.HasFlags(
                                policy, ExecutionPolicy.IsolateKeyRings,
                                true))
                        {
                            scriptDataFlags |=
                                ScriptDataFlags.UseIsolatedInterpreter;
                        }

                        //
                        // HACK: If requested using the execution policy,
                        //       enable the script data flags that force
                        //       the interpreter instance used to load
                        //       settings to be cached, thus saving quite
                        //       a bit of time.
                        //
                        if (Utility.HasFlags(
                                policy, ExecutionPolicy.CacheKeyRings,
                                true))
                        {
                            scriptDataFlags |=
                                ScriptDataFlags.FastStaticDataOnly |
                                ScriptDataFlags.NoCreateInterpreter |
                                ScriptDataFlags.CacheSafeInterpreter;
                        }

                        //
                        // HACK: Force the "software updates" public key
                        //       (associated with its SSL certificate) to
                        //       be "trusted"; this allows the [keyring
                        //       merge] sub-command to use HTTPS to point
                        //       at the official Eagle web site.
                        //
                        if (Utility.IsRemoteUri(fileName))
                        {
                            //
                            // NOTE: If remote URIs are not allowed, stop
                            //       attempts to load key rings from them.
                            //
                            if (!Utility.HasFlags(
                                    policy, ExecutionPolicy.AllowRemoteUri,
                                    true))
                            {
                                error = "file name cannot be a remote uri";
                                return ReturnCode.Error;
                            }

                            scriptDataFlags |= ScriptDataFlags.ForceTrustedUri;
                        }

                        ExecutionPolicy? tracePolicy = policy;
                        bool wasEnabled = false;
                        TracePriority? savedBasePriority = null;
                        TracePriority? savedPriorities1 = null;
                        TracePriority? savedPriorities2 = null;

                        IClientData pushClientData = new ClientData(null);
                        IClientData callbackClientData = new ClientData(null);
                        StringDictionary settings = null;

                        if (Utility.HasFlags(
                                tracePolicy, ExecutionPolicy.TraceKeyRings,
                                true))
                        {
                            /* NO RESULT */
                            CertificateTraceOps.MaybeChangeExecutionPolicy(
                                interpreter, Constants.ScriptExecutionPolicyEnvVarName,
                                Constants.EnablePolicyTracingLimitMask.ToString(),
                                cultureInfo, ref tracePolicy);

                            /* NO RESULT */
                            CertificateTraceOps.MaybeEnableOrDisableTextWriter(
                                interpreter, cultureInfo, tracePolicy, true,
                                ref wasEnabled, ref savedBasePriority,
                                ref savedPriorities1, ref savedPriorities2);
                        }

                        try
                        {
                            if (LoadKeyPairsSettings(
                                    interpreter, fileName, pushClientData,
                                    callbackClientData, ref scriptDataFlags,
                                    ref settings, ref error) != ReturnCode.Ok)
                            {
                                //
                                // TODO: It should be reasonably safe to exit
                                //       here (i.e. without removing approved
                                //       key pairs) because there should not
                                //       be any of those upon failure of this
                                //       method.  Why exactly is this true?
                                //
                                return ReturnCode.Error;
                            }
                        }
                        finally
                        {
                            if (Utility.HasFlags(
                                    tracePolicy, ExecutionPolicy.TraceKeyRings,
                                    true))
                            {
                                /* NO RESULT */
                                CertificateTraceOps.MaybeEnableOrDisableTextWriter(
                                    interpreter, cultureInfo, tracePolicy, false,
                                    ref wasEnabled, ref savedBasePriority,
                                    ref savedPriorities1, ref savedPriorities2);
                            }
                        }

                        byte[] hashValue = null;
                        bool skipApprovedKeyPair = false;
                        IKeyPair keyPair = null;

                        Interpreter otherInterpreter = GetInterpreter(
                            callbackClientData); /* DISPOSED */

                        if (otherInterpreter == null)
                        {
#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.DebugTrace(
                                "LoadKeyPairs: no other interpreter",
                                typeof(CertificateKeyRingOps).Name,
                                TracePriority.MediumLow);
#endif

                            skipApprovedKeyPair = true;
                            goto skipApprovedKeyPair;
                        }

                        if (!Utility.HasFlags(
                                policy, ExecutionPolicy.SaveApprovedData,
                                true))
                        {
#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.DebugTrace(
                                "LoadKeyPairs: no saved approved data",
                                typeof(CertificateKeyRingOps).Name,
                                TracePriority.MediumLow);
#endif

                            /* IGNORED */
                            CertificateKeyPairState.UnlockAllApproved(
                                otherInterpreter, false);

                            skipApprovedKeyPair = true;
                            goto skipApprovedKeyPair;
                        }

                        hashValue = GetHashValue(
                            interpreter, fileName, false, ref error);

                        if ((hashValue == null) ||
                            (CertificateKeyPairState.TakeApproved(
                                otherInterpreter, hashValue, ref keyPair,
                                ref error) != ReturnCode.Ok))
                        {
                            /* IGNORED */
                            CertificateKeyPairState.UnlockAllApproved(
                                otherInterpreter, false);

                            /* IGNORED */
                            CertificateKeyPairState.RemoveAllApproved(
                                otherInterpreter, false);

                            return ReturnCode.Error;
                        }

                        /* IGNORED */
                        CertificateKeyPairState.UnlockAllApproved(
                            otherInterpreter, false);

                        /* IGNORED */
                        CertificateKeyPairState.RemoveAllApproved(
                            otherInterpreter, false);

                    skipApprovedKeyPair:

                        KeyPairDictionary localKeyPairs = null;

                        if (GetKeyPairsFromSettings(
                                fileName, settings, keyPair, pvk,
                                password, publicKey, privateKey,
                                overwrite, ref localKeyPairs,
                                ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        if (!skipApprovedKeyPair &&
                            PolicyOps.AreApprovedContexts(
                                otherInterpreter, pushClientData))
                        {
                            /* IGNORED */
                            MarkKeyPairsApproved(localKeyPairs, true);
                        }
                        else
                        {
                            /* IGNORED */
                            MarkKeyPairsApproved(localKeyPairs, false);
                        }

#if DEBUG || FORCE_TRACE
                        if (localKeyPairs != null)
                        {
                            DebugOnlyOps.DumpKeyPairs(
                                interpreter, "LoadKeyPairs",
                                null, localKeyPairs.Values,
                                typeof(CertificateKeyRingOps).Name,
                                PolicyType.Unknown,
                                TracePriority.MediumLow);
                        }
#endif

                        if (Utility.HasFlags(
                                policy, ExecutionPolicy.UseApprovedData,
                                true))
                        {
                            KeyPairDictionary approvedKeyPairs =
                                FilterByApproved(localKeyPairs,
                                    true, errorOnEmpty, ref error);

                            if (approvedKeyPairs == null)
                                return ReturnCode.Error;

                            localKeyPairs = approvedKeyPairs;
                        }
                        else
                        {
#if DEBUG || FORCE_TRACE
                            CertificateTraceOps.DebugTrace(
                                "LoadKeyPairs: not using approved data",
                                typeof(CertificateKeyRingOps).Name,
                                TracePriority.MediumLow);
#endif
                        }

                        if (errorOnEmpty &&
                            ((localKeyPairs == null) ||
                            (localKeyPairs.Count == 0)))
                        {
                            error = "no key pairs were loaded";
                            return ReturnCode.Error;
                        }

                        /* IGNORED */
                        CertificateKeyRingState.AddFile(
                            interpreter, hashValue, fileName, true);

                        keyPairs = localKeyPairs;
                        return ReturnCode.Ok;
                    }
                    finally
                    {
                        EndInterpreterCallbacks(
                            ref savedNewInterpreterCallback,
                            ref savedUseInterpreterCallback,
                            ref savedFreeInterpreterCallback);
                    }
                }
                else
                {
                    error = "could not lock interpreters";
                }
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                Interpreter.ExitStaticLock(
                    ref locked); /* TRANSACTIONAL */
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /* CANNOT RETURN NULL */
        /// <summary>
        /// Gets the effective key ring name, returning the supplied name if
        /// present or otherwise the default name for the specified policy
        /// type.
        /// </summary>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <returns>
        /// The effective key ring name.
        /// </returns>
        public static string GetName( /* CORE? */
            string keyRingName,   /* in: OPTIONAL */
            PolicyType policyType /* in */
            )
        {
            if (keyRingName != null)
                return keyRingName;

            switch (policyType)
            {
                case PolicyType.Other:
                    return Constants.KeyRingName3;
                case PolicyType.License:
                    return Constants.KeyRingName2;
            }

            return Constants.KeyRingName1;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the list of key pairs in the trusted key ring identified by
        /// the specified name and policy type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="list">
        /// Upon success, receives the list of key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode KeyPairsToList(
            Interpreter interpreter, /* in */
            string keyRingName,      /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            ref StringList list,     /* out */
            ref Result error         /* out */
            )
        {
            IKeyRing keyRing = CertificateKeyRingState.GetTrusted(
                interpreter, GetName(keyRingName, policyType), ref error);

            if (keyRing == null)
                return ReturnCode.Error;

            return keyRing.List(ref list, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears all key pairs from the trusted key ring identified by the
        /// specified name and policy type, and clears the tracked key ring
        /// files.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode ClearKeyPairs(
            Interpreter interpreter, /* in */
            string keyRingName,      /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            ref Result error         /* out: NOT USED */
            )
        {
            IKeyRing keyRing = CertificateKeyRingState.GetTrusted(
                interpreter, GetName(keyRingName, policyType), ref error);

            if (keyRing == null)
                return ReturnCode.Error;

            /* IGNORED */
            CertificateKeyRingState.ClearFiles(interpreter);

            return keyRing.Clear(ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public key pairs from the specified file into the
        /// trusted key ring identified by the supplied name and policy type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="fileName">
        /// The name of the key ring file to load.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy to honor while loading.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to permit loading duplicate key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode LoadKeyPairsPublicOnly( /* CORE? */
            Interpreter interpreter, /* in */
            string keyRingName,      /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            string fileName,         /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ExecutionPolicy? policy, /* in: OPTIONAL */
            bool overwrite,          /* in */
            bool allowDuplicate,     /* in */
            ref Result error         /* out */
            )
        {
            IKeyRing keyRing = CertificateKeyRingState.GetTrusted(
                interpreter, GetName(keyRingName, policyType), ref error);

            if (keyRing == null)
                return ReturnCode.Error;

            ExecutionPolicy localPolicy;

            if (policy != null)
                localPolicy = (ExecutionPolicy)policy;
            else
                localPolicy = PolicyOps.GetPolicy(policyType);

            return keyRing.LoadPublicOnly(
                interpreter, localPolicy, fileName, cultureInfo, overwrite,
                allowDuplicate, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified bootstrap type flags contain the
        /// requested flags.
        /// </summary>
        /// <param name="flags">
        /// The bootstrap type flags to test.
        /// </param>
        /// <param name="hasFlags">
        /// The bootstrap type flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the requested flags; zero to require
        /// any of them.
        /// </param>
        /// <returns>
        /// Non-zero if the requested flags are present; otherwise, zero.
        /// </returns>
        private static bool HasBootstrapTypes( /* CORE? */
            BootstrapType flags,    /* in */
            BootstrapType hasFlags, /* in */
            bool all                /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != BootstrapType.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the names of the embedded bootstrap key ring resources that
        /// are present in the specified assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to search for embedded resources.
        /// </param>
        /// <returns>
        /// The collection of matching embedded resource names.
        /// </returns>
        private static IEnumerable<string> GetBootstrapResourceNames( /* CORE? */
            Assembly assembly /* in */
            )
        {
            IEnumerable<string>[] resourceNames = { null, null, null, null };

            resourceNames[0] = CertificateSharedOps.GetEmbeddedNames(
                assembly, Constants.KeyRingFileNamePattern5, false, false);

            resourceNames[1] = CertificateSharedOps.GetEmbeddedNames(
                assembly, Constants.KeyRingFileNamePattern6, false, false);

            resourceNames[2] = CertificateSharedOps.GetEmbeddedNames(
                assembly, Constants.KeyRingFileNamePattern7, false, false);

            resourceNames[3] = CertificateSharedOps.GetEmbeddedNames(
                assembly, Constants.KeyRingFileNamePattern8, false, false);

            StringList newResourceNames = new StringList();

            if (resourceNames[0] != null)
                newResourceNames.AddRange(resourceNames[0]);

            if (resourceNames[1] != null)
                newResourceNames.AddRange(resourceNames[1]);

            if (resourceNames[2] != null)
                newResourceNames.AddRange(resourceNames[2]);

            if (resourceNames[3] != null)
                newResourceNames.AddRange(resourceNames[3]);

            return newResourceNames;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the names of the embedded bootstrap key ring resources in the
        /// specified assembly whose file names match the requested bootstrap
        /// types.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter used during matching.
        /// </param>
        /// <param name="assembly">
        /// The assembly to search for embedded resources.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types whose file names should be matched.
        /// </param>
        /// <returns>
        /// The collection of matching embedded resource names, or null if
        /// none were found.
        /// </returns>
        private static IEnumerable<string> GetBootstrapResourceFileNames( /* CORE? */
            Interpreter interpreter,     /* in: OPTIONAL */
            Assembly assembly,           /* in */
            BootstrapType bootstrapTypes /* in */
            )
        {
            IEnumerable<string> resourceNames = GetBootstrapResourceNames(
                assembly);

            if (resourceNames == null)
                return null;

            StringList newResourceNames = null;

            foreach (string resourceName in resourceNames)
            {
                if (String.IsNullOrEmpty(resourceName))
                    continue;

                string resourceNameOnly = Path.GetFileName(
                    resourceName);

                if (MatchBootstrapFileName(
                        interpreter, bootstrapTypes,
                        resourceNameOnly, true, true))
                {
                    if (newResourceNames == null)
                        newResourceNames = new StringList();

                    newResourceNames.Add(resourceName);
                }
            }

            return newResourceNames;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the specified embedded bootstrap key ring resources from
        /// the assembly into a temporary directory on disk.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used when creating the temporary directory.
        /// </param>
        /// <param name="assembly">
        /// The assembly to read the embedded resources from.
        /// </param>
        /// <param name="resourceNames">
        /// The names of the embedded resources to extract.
        /// </param>
        /// <param name="temporaryDirectory">
        /// The temporary directory to extract into; created and assigned if
        /// not already set.
        /// </param>
        private static void ExtractBootstrapResourceFileNames( /* CORE? */
            Interpreter interpreter,           /* in */
            Assembly assembly,                 /* in */
            IEnumerable<string> resourceNames, /* in */
            ref string temporaryDirectory      /* in, out */
            )
        {
            if (resourceNames == null)
                return;

            Result error = null;

            if (temporaryDirectory == null)
            {
                temporaryDirectory = Utility.GetUniquePath(
                    interpreter, Utility.GetTempPath(interpreter),
                    null, null, ref error);
            }

            if (temporaryDirectory != null)
            {
                try
                {
                    Directory.CreateDirectory(
                        temporaryDirectory); /* throw */
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificateKeyRingOps).Name,
                        TracePriority.MediumHigh);
#endif

                    return;
                }

                foreach (string resourceName in resourceNames)
                {
                    if (String.IsNullOrEmpty(resourceName))
                        continue;

                    byte[] bytes = CertificateSharedOps.GetEmbeddedBytes(
                        assembly, resourceName, ref error);

                    if (bytes == null)
                    {
#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(String.Format(
                            "ExtractBootstrapResourceFileNames: " +
                            "assembly = {0}, resourceName = {1}, " +
                            "error = {2}",
                            Utility.FormatWrapOrNull(assembly),
                            Utility.FormatWrapOrNull(resourceName),
                            Utility.FormatWrapOrNull(error)),
                            typeof(CertificateKeyRingOps).Name,
                            TracePriority.MediumHigh);
#endif

                        continue;
                    }

                    try
                    {
                        string fileName = Path.Combine(
                            temporaryDirectory, resourceName); /* throw */

                        string directory = Path.GetDirectoryName(
                            fileName); /* throw */

                        if (!String.IsNullOrEmpty(directory) &&
                            !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory); /* throw */
                        }

                        File.WriteAllBytes(fileName, bytes); /* throw */
                    }
#if DEBUG || FORCE_TRACE
                    catch (Exception e)
#else
                    catch
#endif
                    {
#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(
                            e, typeof(CertificateKeyRingOps).Name,
                            TracePriority.MediumHigh);
#endif
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the key ring sub-directory, and any of its immediate
        /// sub-directories, of the specified directory to the supplied list
        /// when they exist.
        /// </summary>
        /// <param name="directory">
        /// The directory whose key ring sub-directory is considered.
        /// </param>
        /// <param name="directories">
        /// The list of directories to add to.
        /// </param>
        private static void MaybeAddBootstrapSubDirectories( /* CORE? */
            string directory,          /* in */
            ref StringList directories /* in, out */
            )
        {
            try
            {
                if (String.IsNullOrEmpty(directory))
                    return;

                if (!Directory.Exists(directory))
                    return;

                string subDirectory = Path.Combine(
                    directory, Constants.KeyRingDirectoryName);

                if (String.IsNullOrEmpty(subDirectory)) /* IMPOSSIBLE? */
                    return;

                if (!Directory.Exists(subDirectory))
                    return;

                if (directories == null)
                    directories = new StringList();

                directories.Add(subDirectory);

                string[] subSubDirectories = Directory.GetDirectories(
                    subDirectory, Characters.Asterisk.ToString(),
                    SearchOption.TopDirectoryOnly);

                if ((subSubDirectories == null) ||
                    (subSubDirectories.Length == 0))
                {
                    return;
                }

                Array.Sort(subSubDirectories); /* O(N) */

                foreach (string subSubDirectory in subSubDirectories)
                {
                    if (!Directory.Exists(subSubDirectory))
                        continue;

                    if (directories == null) /* REDUNDANT */
                        directories = new StringList();

                    directories.Add(subSubDirectory);
                }
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateKeyRingOps).Name,
                    TracePriority.MediumHigh);
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the list of environment variable names that may identify
        /// directories to search for bootstrap key ring files of the
        /// requested types.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to derive plugin-specific variable names.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types whose variable names should be included.
        /// </param>
        /// <param name="recursive">
        /// Non-zero if directories will be searched recursively.
        /// </param>
        /// <param name="envVarNames">
        /// The list of environment variable names to add to.
        /// </param>
        private static void GetBootstrapEnvVarNames( /* CORE? */
            IPluginData pluginData,       /* in */
            BootstrapType bootstrapTypes, /* in */
            bool recursive,               /* in */
            ref StringList envVarNames    /* in, out */
            )
        {
            StringList localEnvVarNames = new StringList();

            IEnumerable<BootstrapType> localBootstrapTypes =
                GetBootstrapTypes();

            ///////////////////////////////////////////////////////////////////

            if (localBootstrapTypes != null)
            {
                foreach (string pluginName in new string[] {
                    CertificatePathOps.GetPluginName(
                        pluginData, PluginNameFlags.Pass1 |
                            PluginNameFlags.ForEnvironment),
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                    CertificatePathOps.GetPluginName(
                        pluginData, PluginNameFlags.Pass2 |
                            PluginNameFlags.ForEnvironment),
#endif
                    })
                {
                    if (String.IsNullOrEmpty(pluginName))
                        continue;

                    foreach (BootstrapType bootstrapType
                            in localBootstrapTypes)
                    {
                        if (!HasBootstrapTypes(
                                bootstrapTypes, bootstrapType, true))
                        {
                            continue;
                        }

                        localEnvVarNames.Add(String.Format(
                            "{0}.{1}.{2}",
                            pluginName, bootstrapType,
                            Constants.BootstrapEnvVarSuffix));
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            string defaultEnvVarName =
                CertificatePathOps.GetDefaultEnvVarName();

            if (!String.IsNullOrEmpty(defaultEnvVarName) &&
                (localBootstrapTypes != null))
            {
                foreach (BootstrapType bootstrapType
                        in localBootstrapTypes)
                {
                    if (!HasBootstrapTypes(
                            bootstrapTypes, bootstrapType, true))
                    {
                        continue;
                    }

                    localEnvVarNames.Add(String.Format(
                        "{0}.{1}.{2}",
                        defaultEnvVarName, bootstrapType,
                        Constants.BootstrapEnvVarSuffix));
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (recursive)
            {
                //
                // HACK: Add the special home directory for key ring files.
                //       This will always be searched recursively.
                //
                localEnvVarNames.Add(EnvVars.XdgStateHome);
                localEnvVarNames.Add(EnvVars.XdgKeyRingHome);
            }
            else
            {
                //
                // NOTE: *HACK* Always prevent recursive directory searches
                //       of the user profile directories.
                //
                localEnvVarNames.Add(EnvVars.XdgStartupHome);
                localEnvVarNames.Add(EnvVars.UserProfile);
            }

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetBootstrapEnvVarNames: localEnvVarNames = {0}",
                Utility.FormatWrapOrNull(localEnvVarNames)),
                typeof(CertificateKeyRingOps).Name, TracePriority.Lower);
#endif

            ///////////////////////////////////////////////////////////////////

            if (envVarNames == null)
                envVarNames = new StringList();

            envVarNames.AddRange(localEnvVarNames);
        }

        ///////////////////////////////////////////////////////////////////////

        /* MAY RETURN NULL */
        /// <summary>
        /// Gets the fully qualified bootstrap key ring file name for the
        /// specified single bootstrap type.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to determine the bootstrap directory.
        /// </param>
        /// <param name="bootstrapType">
        /// The single bootstrap type whose file name is requested.
        /// </param>
        /// <returns>
        /// The bootstrap file name, or null if the type is not supported.
        /// </returns>
        public static string GetBootstrapFileName( /* CORE? */
            IPluginData pluginData,     /* in */
            BootstrapType bootstrapType /* in: SINGLE FLAG */
            )
        {
            string directory = GetBootstrapDirectory(pluginData);

            if (bootstrapType == BootstrapType.General) /* EXEMPT */
            {
                return Path.Combine(
                    directory, Constants.KeyRingGeneralFileName);
            }
            else if (bootstrapType == BootstrapType.License) /* EXEMPT */
            {
                return Path.Combine(
                    directory, Constants.KeyRingLicenseFileName);
            }
            else if (bootstrapType == BootstrapType.Script) /* EXEMPT */
            {
                return Path.Combine(
                    directory, Constants.KeyRingZeroFileName);
            }
            else if (bootstrapType == BootstrapType.Bundle) /* EXEMPT */
            {
                return Path.Combine(
                    directory, Constants.KeyRingOneFileName);
            }
            else
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "GetBootstrapFileName: unsupported type {0}",
                    Utility.FormatWrapOrNull(bootstrapType)),
                    typeof(CertificateKeyRingOps).Name,
                    TracePriority.MediumHigh);
#endif

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /* CANNOT RETURN NULL */
        /// <summary>
        /// Gets the bootstrap directory associated with the specified plugin
        /// data, falling back to the assembly directory.
        /// </summary>
        /// <param name="pluginData">
        /// The optional plugin data used to determine the directory.
        /// </param>
        /// <returns>
        /// The bootstrap directory, which is never null.
        /// </returns>
        public static string GetBootstrapDirectory( /* CORE? */
            IPluginData pluginData /* in: OPTIONAL */
            )
        {
            string directory = CertificateAssemblyOps.GetDirectory();

            /* IGNORED */
            CertificatePathOps.GetDirectory(pluginData, ref directory);

            if (directory == null)
                directory = String.Empty;

            return directory;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the complete list of directories to search for bootstrap
        /// key ring files of the requested types, including those derived
        /// from environment variables, plugin data, and extracted embedded
        /// resources.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during discovery and extraction.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to derive directories.
        /// </param>
        /// <param name="directories">
        /// The initial directories to include, if any.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types whose directories should be included.
        /// </param>
        /// <param name="temporaryDirectory">
        /// Receives the temporary directory used for extracted resources, if
        /// any.
        /// </param>
        /// <param name="recursive">
        /// Indicates whether directories should be searched recursively; may
        /// be adjusted on return.
        /// </param>
        /// <returns>
        /// The list of directories to search, or null if none were found.
        /// </returns>
        private static IEnumerable<string> GetBootstrapDirectories( /* CORE? */
            Interpreter interpreter,         /* in */
            IPluginData pluginData,          /* in */
            IEnumerable<string> directories, /* in */
            BootstrapType bootstrapTypes,    /* in */
            ref string temporaryDirectory,   /* out */
            ref bool recursive               /* in, out */
            )
        {
            //
            // NOTE: *HACK* Always disable recursive directory searches of
            //       the user profile directory.
            //
            if (recursive && HasBootstrapTypes(
                    bootstrapTypes, BootstrapType.Script, true))
            {
                recursive = false;
            }

            StringList localDirectories = null;

            if (directories != null)
            {
                if (localDirectories == null)
                    localDirectories = new StringList();

                localDirectories.AddRange(directories);
            }

            StringList envVarNames = null;

            GetBootstrapEnvVarNames(
                pluginData, bootstrapTypes, recursive, ref envVarNames);

            foreach (string envVarName in envVarNames)
            {
                if (String.IsNullOrEmpty(envVarName))
                    continue;

                string envVarValue = Configuration.GetVariable(
                    envVarName);

                if (String.IsNullOrEmpty(envVarValue))
                    continue;

                MaybeAddBootstrapSubDirectories(
                    envVarValue, ref localDirectories);

                if (localDirectories == null)
                    localDirectories = new StringList();

                localDirectories.Add(envVarValue);
            }

            string keyRingPath = Utility.GetEnvironmentVariable(
                EnvVars.XdgKeyRingDirs, true, true);

            if (keyRingPath != null)
            {
                string[] keyRingDirectories = keyRingPath.Split(
                    Path.PathSeparator);

                if (keyRingDirectories != null)
                {
                    foreach (string keyRingDirectory in keyRingDirectories)
                    {
                        if (String.IsNullOrEmpty(keyRingDirectory))
                            continue;

                        if (localDirectories == null)
                            localDirectories = new StringList();

                        localDirectories.Add(keyRingDirectory);
                    }
                }
            }

            string pluginDataDirectory = GetBootstrapDirectory(pluginData);

            if (pluginDataDirectory != null)
            {
                if (localDirectories == null)
                    localDirectories = new StringList();

                localDirectories.Add(pluginDataDirectory);
            }

            Assembly assembly = CertificateAssemblyOps.GetObject();

            if (assembly != null)
            {
                IEnumerable<string> resourceNames =
                    GetBootstrapResourceFileNames(
                        interpreter, assembly, bootstrapTypes);

                if (resourceNames != null)
                {
                    ExtractBootstrapResourceFileNames(
                        interpreter, assembly, resourceNames,
                        ref temporaryDirectory);

                    if (temporaryDirectory != null)
                    {
                        if (localDirectories == null)
                            localDirectories = new StringList();

                        localDirectories.Add(temporaryDirectory);
                    }
                }
            }

            return localDirectories;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name matches the supplied
        /// pattern, optionally also matching its detached signature file and
        /// excluding names that contain minus signs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during pattern matching.
        /// </param>
        /// <param name="fileNameOnly">
        /// The file name (without directory) to test.
        /// </param>
        /// <param name="pattern">
        /// The pattern to match against.
        /// </param>
        /// <param name="allowSignature">
        /// Non-zero to also match the corresponding signature file name.
        /// </param>
        /// <param name="excludeMinus">
        /// Non-zero to exclude file names that contain minus signs.
        /// </param>
        /// <returns>
        /// Non-zero if the file name matches; otherwise, zero.
        /// </returns>
        private static bool MatchFileName( /* CORE? */
            Interpreter interpreter, /* in */
            string fileNameOnly,     /* in */
            string pattern,          /* in */
            bool allowSignature,     /* in */
            bool excludeMinus        /* in */
            )
        {
            //
            // HACK: Exclude any (key ring?) file name that contains minus
            //       signs anywhere.  This permits end-user key rings that
            //       contain minus signs to be excluded from the bootstrap
            //       process.  They can still be (manually) loaded later.
            //
            if (excludeMinus && Parser.StringMatch(
                    interpreter, fileNameOnly, 0, String.Format(
                    "{0}{1}{2}", Characters.Asterisk, Characters.MinusSign,
                    Characters.Asterisk), 0, false))
            {
                return false;
            }

            if (Parser.StringMatch(
                    interpreter, fileNameOnly, 0, pattern, 0, false))
            {
                return true;
            }

            if (allowSignature && Parser.StringMatch(
                    interpreter, fileNameOnly, 0, String.Format("{0}{1}",
                    pattern, FileExtension.Signature), 0, false))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name matches any of the
        /// general key ring file name patterns.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during pattern matching.
        /// </param>
        /// <param name="fileNameOnly">
        /// The file name (without directory) to test.
        /// </param>
        /// <param name="allowSignature">
        /// Non-zero to also match the corresponding signature file name.
        /// </param>
        /// <param name="excludeMinus">
        /// Non-zero to exclude file names that contain minus signs.
        /// </param>
        /// <returns>
        /// Non-zero if the file name matches; otherwise, zero.
        /// </returns>
        private static bool MatchAnyFileName( /* CORE? */
            Interpreter interpreter, /* in */
            string fileNameOnly,     /* in */
            bool allowSignature,     /* in */
            bool excludeMinus        /* in */
            )
        {
            if (MatchFileName(
                    interpreter, fileNameOnly,
                    Constants.KeyRingFileNamePattern1,
                    allowSignature, excludeMinus) ||
                MatchFileName(
                    interpreter, fileNameOnly,
                    Constants.KeyRingFileNamePattern2,
                    allowSignature, excludeMinus))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name matches any of the key
        /// ring file name patterns for the given bootstrap type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during pattern matching.
        /// </param>
        /// <param name="fileNameOnly">
        /// The file name (without directory) to test.
        /// </param>
        /// <param name="bootstrapType">
        /// The bootstrap type whose patterns are matched.
        /// </param>
        /// <param name="allowSignature">
        /// Non-zero to also match the corresponding signature file name.
        /// </param>
        /// <param name="excludeMinus">
        /// Non-zero to exclude file names that contain minus signs.
        /// </param>
        /// <returns>
        /// Non-zero if the file name matches; otherwise, zero.
        /// </returns>
        private static bool MatchAnyFileName( /* CORE? */
            Interpreter interpreter,     /* in */
            string fileNameOnly,         /* in */
            BootstrapType bootstrapType, /* in */
            bool allowSignature,         /* in */
            bool excludeMinus            /* in */
            )
        {
            if (MatchFileName(
                    interpreter, fileNameOnly, String.Format(
                    Constants.KeyRingFileNamePattern3,
                    bootstrapType), allowSignature,
                    excludeMinus) ||
                MatchFileName(
                    interpreter, fileNameOnly, String.Format(
                    Constants.KeyRingFileNamePattern4,
                    bootstrapType), allowSignature,
                    excludeMinus))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name matches a bootstrap
        /// key ring file for any of the requested bootstrap types.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during pattern matching.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types to consider when matching.
        /// </param>
        /// <param name="fileNameOnly">
        /// The file name (without directory) to test.
        /// </param>
        /// <param name="allowSignature">
        /// Non-zero to also match the corresponding signature file name.
        /// </param>
        /// <param name="excludeMinus">
        /// Non-zero to exclude file names that contain minus signs.
        /// </param>
        /// <returns>
        /// Non-zero if the file name matches; otherwise, zero.
        /// </returns>
        private static bool MatchBootstrapFileName( /* CORE? */
            Interpreter interpreter,      /* in */
            BootstrapType bootstrapTypes, /* in */
            string fileNameOnly,          /* in */
            bool allowSignature,          /* in */
            bool excludeMinus             /* in */
            )
        {
            if (HasBootstrapTypes(
                    bootstrapTypes, BootstrapType.General, true) ||
                HasBootstrapTypes(
                    bootstrapTypes, BootstrapType.License, true))
            {
                if (HasBootstrapTypes(
                        bootstrapTypes, BootstrapType.PrimaryOnly, true))
                {
                    if (MatchFileName(
                            interpreter, fileNameOnly,
                            Constants.KeyRingLicenseFileName,
                            allowSignature, excludeMinus) ||
                        MatchFileName(
                            interpreter, fileNameOnly,
                            Constants.KeyRingGeneralFileName,
                            allowSignature, excludeMinus))
                    {
                        return true;
                    }
                }
                else
                {
                    if (MatchAnyFileName(
                            interpreter, fileNameOnly,
                            BootstrapType.General,
                            allowSignature, excludeMinus) ||
                        MatchAnyFileName(
                            interpreter, fileNameOnly,
                            BootstrapType.License,
                            allowSignature, excludeMinus))
                    {
                        return true;
                    }
                }
            }

            if (HasBootstrapTypes(
                    bootstrapTypes, BootstrapType.General, true) ||
                HasBootstrapTypes(
                    bootstrapTypes, BootstrapType.Script, true))
            {
                if (HasBootstrapTypes(
                        bootstrapTypes, BootstrapType.PrimaryOnly, true))
                {
                    if (MatchFileName(
                            interpreter, fileNameOnly,
                            Constants.KeyRingZeroFileName,
                            allowSignature, excludeMinus) ||
                        MatchFileName(
                            interpreter, fileNameOnly,
                            Constants.KeyRingGeneralFileName,
                            allowSignature, excludeMinus))
                    {
                        return true;
                    }
                }
                else
                {
                    if (MatchAnyFileName(
                            interpreter, fileNameOnly,
                            allowSignature, excludeMinus))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Moves the primary (script) bootstrap file name to the front of
        /// the specified list when it is present and the context permits
        /// reordering.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to determine the primary file name.
        /// </param>
        /// <param name="fileNames">
        /// The list of file names to reorder.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types describing the current loading context.
        /// </param>
        private static void MakeSureZeroFileNameIsFirst( /* CORE? */
            IPluginData pluginData,      /* in */
            StringList fileNames,        /* in */
            BootstrapType bootstrapTypes /* in */
            )
        {
            //
            // NOTE: Garbage in, garbage out.
            //
            if (fileNames == null)
                return;

            //
            // NOTE: If this method is being called in a license loading
            //       context, DO NOT change the order of the file names;
            //       Technically, we could; however, the "bootstrap" key
            //       ring file "keyRing.zero.eagle" is (primarily?) for
            //       use when verifying script certificates.
            //
            if (HasBootstrapTypes(
                    bootstrapTypes, BootstrapType.License, true))
            {
                return;
            }

            //
            // NOTE: Try to figure out the fully qualified path and file
            //       name to primary bootstrap file.
            //
            string fileName = GetBootstrapFileName(
                pluginData, BootstrapType.Script);

            //
            // NOTE: If the primary bootstrap file name is invalid, maybe
            //       the plugin data is invalid too?  Just skip doing any
            //       list modifications in this case.
            //
            if (String.IsNullOrEmpty(fileName))
                return;

            //
            // HACK: *NOCASE* File names are not case-sensitive on
            //       Windows.
            //
            int index = fileNames.IndexOf(
                fileName, 0, Utility.GetPathComparisonType());

            //
            // NOTE: If the primary bootstrap file name is not present in
            //       the list, do nothing.  Do not simply add it.
            //
            if (index == Index.Invalid)
                return;

            //
            // NOTE: This block has the net effect of moving the primary
            //       bootstrap file name to the front of the list.
            //
            fileNames.RemoveAt(index); /* O(N) */
            fileNames.Insert(0, fileName); /* O(N) */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the secondary (script bundle) bootstrap file name from the
        /// specified list when it is present.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data used to determine the secondary file name.
        /// </param>
        /// <param name="fileNames">
        /// The list of file names to modify.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types describing the current loading context.
        /// </param>
        private static void MakeSureOneFileNameIsAbsent( /* CORE? */
            IPluginData pluginData,      /* in */
            StringList fileNames,        /* in */
            BootstrapType bootstrapTypes /* in */
            )
        {
            //
            // NOTE: Garbage in, garbage out.
            //
            if (fileNames == null)
                return;

            //
            // NOTE: Try to figure out the fully qualified path and file
            //       name to secondary (script bundle) file.
            //
            string fileName = GetBootstrapFileName(
                pluginData, BootstrapType.Bundle);

            //
            // NOTE: If the secondary (script bundle) file name is invalid,
            //       maybe the plugin data is invalid too?  Just skip doing
            //       any list modifications in this case.
            //
            if (String.IsNullOrEmpty(fileName))
                return;

            //
            // HACK: *NOCASE* File names are not case-sensitive on Windows.
            //
            int index = fileNames.IndexOf(
                fileName, 0, Utility.GetPathComparisonType());

            //
            // NOTE: Make 100% sure that the secondary (script bundle) file
            //       name is not present in the list.  If needed, remove it.
            //
            if (index == Index.Invalid)
                return;

            fileNames.RemoveAt(index); /* O(N) */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Searches the specified directories for key ring files matching the
        /// requested bootstrap types and adds the matching file names to the
        /// supplied list.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during matching.
        /// </param>
        /// <param name="directories">
        /// The directories to search.
        /// </param>
        /// <param name="fileNames">
        /// The list of file names to add to.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The optional bootstrap types used to filter matches.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting errors.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search directories recursively.
        /// </param>
        private static void GetFileNames( /* CORE? */
            Interpreter interpreter,         /* in */
            IEnumerable<string> directories, /* in */
            StringList fileNames,            /* in */
            BootstrapType? bootstrapTypes,   /* in: OPTIONAL */
            TracePriority priority,          /* in */
            bool recursive                   /* in */
            )
        {
            if ((directories == null) || (fileNames == null))
                return;

            TracePriority localPriority; /* REUSED */

            try
            {
                SearchOption searchOption = GetSearchOption(
                    recursive);

                foreach (string directory in directories)
                {
                    if (String.IsNullOrEmpty(directory))
                        continue;

                    string[] localFileNames = Directory.GetFiles(
                        directory, Characters.Asterisk.ToString(),
                        searchOption);

                    if (localFileNames == null)
                        continue;

                    Array.Sort(localFileNames); /* O(N) */

                    foreach (string fileName in localFileNames)
                    {
                        if (String.IsNullOrEmpty(fileName))
                            continue;

                        string fileNameOnly = Path.GetFileName(
                            fileName);

                        if (bootstrapTypes != null)
                        {
                            if (!MatchBootstrapFileName(
                                    interpreter,
                                    (BootstrapType)bootstrapTypes,
                                    fileNameOnly, false, true))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            if (!MatchAnyFileName(
                                    interpreter, fileNameOnly,
                                    false, true))
                            {
                                continue;
                            }
                        }

                        if (!fileNames.Contains(fileName)) /* O(N) */
                            fileNames.Add(fileName);
                    }
                }
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                localPriority = priority;

                Utility.AdjustTracePriority(ref localPriority, 1);

                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateKeyRingOps).Name,
                    localPriority);
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /* CANNOT RETURN NULL */
        /// <summary>
        /// Builds the ordered list of bootstrap key ring file names for the
        /// requested types, discovered across all relevant directories.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during discovery.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to derive directories and file names.
        /// </param>
        /// <param name="directories">
        /// The initial directories to include, if any.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types whose file names should be discovered.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting progress.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search directories recursively.
        /// </param>
        /// <param name="temporaryDirectory">
        /// Receives the temporary directory used for extracted resources, if
        /// any.
        /// </param>
        /// <returns>
        /// The list of bootstrap file names, which is never null.
        /// </returns>
        private static StringList GetBootstrapFileNames( /* CORE? */
            Interpreter interpreter,         /* in */
            IPluginData pluginData,          /* in */
            IEnumerable<string> directories, /* in */
            BootstrapType bootstrapTypes,    /* in */
            TracePriority priority,          /* in */
            bool recursive,                  /* in */
            ref string temporaryDirectory    /* out */
            )
        {
            IEnumerable<string> localDirectories; /* TRACE */
            StringList list = new StringList();

            localDirectories = GetBootstrapDirectories(
                interpreter, pluginData, directories,
                bootstrapTypes, ref temporaryDirectory,
                ref recursive);

            /* NO RESULT */
            GetFileNames(
                interpreter, localDirectories, list,
                bootstrapTypes, priority, recursive);

            IEnumerable<BootstrapType> localBootstrapTypes =
                GetBootstrapTypes();

            if (localBootstrapTypes != null)
            {
                foreach (BootstrapType bootstrapType
                        in localBootstrapTypes)
                {
                    if (!HasBootstrapTypes(
                            bootstrapTypes, bootstrapType, true))
                    {
                        continue;
                    }

                    string fileName = GetBootstrapFileName(
                        pluginData, bootstrapType);

                    if (String.IsNullOrEmpty(fileName))
                        continue;

                    string fileNameOnly = Path.GetFileName(
                        fileName);

                    if (MatchBootstrapFileName(
                            interpreter, bootstrapType,
                            fileNameOnly, false, true))
                    {
                        if (!list.Contains(fileName)) /* O(N) */
                            list.Add(fileName);
                    }
                }
            }

            if (!HasBootstrapTypes(
                    bootstrapTypes, BootstrapType.DoNotReorder, true))
            {
                MakeSureZeroFileNameIsFirst(
                    pluginData, list, bootstrapTypes);

                MakeSureOneFileNameIsAbsent(
                    pluginData, list, bootstrapTypes);
            }

#if DEBUG || FORCE_TRACE
            int count = list.Count;
            TracePriority localPriority = priority;

            Utility.AdjustTracePriority(ref localPriority, -1);

            CertificateTraceOps.MaybeLogAndDebugTrace(
                String.Format(
                    "Bootstrap {0} directories were {1}, recursive " +
                    "search was {2}, matched {3} file {4} {5}...",
                    Utility.FormatWrapOrNull(bootstrapTypes),
                    Utility.FormatWrapOrNull(localDirectories),
                    recursive ? "enabled" : "disabled", count,
                    (count != 1) ? "names" : "name",
                    Utility.FormatWrapOrNull(list)),
                typeof(CertificateKeyRingOps).Name, localPriority, 0);
#endif

            return list;
        }

        ///////////////////////////////////////////////////////////////////////

        /* CANNOT RETURN NULL */
        /// <summary>
        /// Builds the combined, de-duplicated list of bootstrap key ring file
        /// names for both the plugin being loaded and the Harpy plugin
        /// itself.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during discovery.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data used to derive file names.
        /// </param>
        /// <param name="plugin">
        /// The optional Harpy plugin used to derive file names.
        /// </param>
        /// <param name="directories">
        /// The optional initial directories to include.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types whose file names should be discovered.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting progress.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search directories recursively.
        /// </param>
        /// <param name="temporaryDirectory">
        /// Receives the temporary directory used for extracted resources, if
        /// any.
        /// </param>
        /// <returns>
        /// The combined list of bootstrap file names, which is never null.
        /// </returns>
        private static StringList GetAllBootstrapFileNames( /* CORE? */
            Interpreter interpreter,         /* in */
            IPluginData pluginData,          /* in: OPTIONAL */
            IPlugin plugin,                  /* in: OPTIONAL */
            IEnumerable<string> directories, /* in: OPTIONAL */
            BootstrapType bootstrapTypes,    /* in */
            TracePriority priority,          /* in */
            bool recursive,                  /* in */
            ref string temporaryDirectory    /* out */
            )
        {
            //
            // BUGFIX: When building the final list of file names for the
            //         specified type, we must take into account both the
            //         plugin being loaded -AND- the Harpy plugin itself.
            //         Otherwise, e.g. Badge plugin could fail to load as
            //         it may not be able to access the trusted key rings
            //         from the Harpy directory.
            //
            StringList fileNames = new StringList();

            if ((pluginData != null) || (directories != null))
            {
                fileNames.AddRange(GetBootstrapFileNames(
                    interpreter, pluginData, directories,
                    bootstrapTypes, priority, recursive,
                    ref temporaryDirectory));
            }

            if (plugin != null)
            {
                fileNames.AddRange(GetBootstrapFileNames(
                    interpreter, plugin, null, bootstrapTypes,
                    priority, recursive, ref temporaryDirectory));
            }

            return Utility.GetUniqueElements(fileNames);
        }

        ///////////////////////////////////////////////////////////////////////

#if LIMITED_EDITION
        /// <summary>
        /// Loads the public script key pairs from the specified directories,
        /// complaining about any failure and treating the absence of loaded
        /// key rings as an error.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to derive directories and file names.
        /// </param>
        /// <param name="directories">
        /// The directories to search for key ring files.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="policy">
        /// The execution policy to honor while loading.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting progress.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search directories recursively.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to permit loading duplicate key pairs.
        /// </param>
        /// <param name="ignoreErrors">
        /// Non-zero to ignore errors loading individual key ring files.
        /// </param>
        public static void LoadScriptKeyPairsPublicOnly(
            Interpreter interpreter,         /* in */
            string keyRingName,              /* in: OPTIONAL */
            IPluginData pluginData,          /* in */
            IEnumerable<string> directories, /* in */
            CultureInfo cultureInfo,         /* in: OPTIONAL */
            ExecutionPolicy? policy,         /* in */
            TracePriority priority,          /* in */
            bool recursive,                  /* in */
            bool overwrite,                  /* in */
            bool allowDuplicate,             /* in */
            bool ignoreErrors                /* in */
            )
        {
            ReturnCode code;
            int loaded = 0;
            Result error = null;

            code = LoadScriptKeyPairsPublicOnly(
                interpreter, keyRingName, pluginData, directories,
                cultureInfo, policy, priority, recursive, overwrite,
                allowDuplicate, ignoreErrors, ref loaded, ref error);

            if ((code == ReturnCode.Ok) && (loaded == 0))
            {
                error = "no key rings were loaded";
                code = ReturnCode.Error;
            }

            if (code != ReturnCode.Ok)
                Utility.Complain(interpreter, code, error);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the directory search option corresponding to the requested
        /// recursion behavior.
        /// </summary>
        /// <param name="recursive">
        /// Non-zero to search all sub-directories; zero to search only the
        /// top directory.
        /// </param>
        /// <returns>
        /// The corresponding <see cref="SearchOption" /> value.
        /// </returns>
        private static SearchOption GetSearchOption( /* CORE? */
            bool recursive /* in */
            )
        {
            return recursive ?
                SearchOption.AllDirectories :
                SearchOption.TopDirectoryOnly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the file name patterns used to clean up extracted temporary
        /// bootstrap key ring files.
        /// </summary>
        /// <returns>
        /// The list of cleanup patterns.
        /// </returns>
        private static StringList GetCleanupPatterns() /* CORE? */
        {
            StringList patterns = new StringList();

            patterns.Add(Constants.KeyRingFileNamePattern5);
            patterns.Add(Constants.KeyRingFileNamePattern6);

            return patterns;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjusts the specified bootstrap types based on the relevant
        /// environment variables and execution policy flags, optionally
        /// restricting loading to specific or primary key rings.
        /// </summary>
        /// <param name="policy">
        /// The optional execution policy to consider.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types to adjust.
        /// </param>
        private static void MaybeMutateBootstrapTypes( /* CORE? */
            ExecutionPolicy? policy,         /* in */
            ref BootstrapType bootstrapTypes /* in, out */
            )
        {
            if (Configuration.DoesVariableExist(
                    Constants.SpecificKeyRingOnlyEnvVarName) ||
                Utility.HasFlags(
                    policy, ExecutionPolicy.SpecificKeyRingOnly, true))
            {
                bootstrapTypes &= ~BootstrapType.General;
            }

            if (Configuration.DoesVariableExist(
                    Constants.PrimaryKeyRingOnlyEnvVarName) ||
                Utility.HasFlags(
                    policy, ExecutionPolicy.PrimaryKeyRingOnly, true))
            {
                bootstrapTypes |= BootstrapType.PrimaryOnly;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Discovers all bootstrap key ring file names for the requested
        /// types and loads the public key pairs from them, cleaning up any
        /// extracted temporary files afterward.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to derive directories and file names.
        /// </param>
        /// <param name="plugin">
        /// The optional Harpy plugin used to derive file names.
        /// </param>
        /// <param name="directories">
        /// The directories to search for key ring files.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="policy">
        /// The execution policy to honor while loading.
        /// </param>
        /// <param name="bootstrapTypes">
        /// The bootstrap types whose files should be loaded.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting progress.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search directories recursively.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to permit loading duplicate key pairs.
        /// </param>
        /// <param name="ignoreErrors">
        /// Non-zero to ignore errors loading individual key ring files.
        /// </param>
        /// <param name="loaded">
        /// Receives the running count of key ring files that were loaded.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode LoadKeyPairsPublicOnly( /* CORE? */
            Interpreter interpreter,         /* in */
            string keyRingName,              /* in: OPTIONAL */
            IPluginData pluginData,          /* in */
            IPlugin plugin,                  /* in: OPTIONAL */
            IEnumerable<string> directories, /* in */
            CultureInfo cultureInfo,         /* in: OPTIONAL */
            ExecutionPolicy? policy,         /* in */
            BootstrapType bootstrapTypes,    /* in */
            PolicyType policyType,           /* in */
            TracePriority priority,          /* in */
            bool recursive,                  /* in */
            bool overwrite,                  /* in */
            bool allowDuplicate,             /* in */
            bool ignoreErrors,               /* in */
            ref int loaded,                  /* in, out */
            ref Result error                 /* out */
            )
        {
            string temporaryDirectory = null;

            try
            {
                return LoadKeyPairsPublicOnly(
                    interpreter, keyRingName, GetAllBootstrapFileNames(
                    interpreter, pluginData, plugin, directories,
                    bootstrapTypes, priority, recursive,
                    ref temporaryDirectory), cultureInfo, policy,
                    policyType, priority, overwrite, allowDuplicate,
                    ignoreErrors, ref loaded, ref error);
            }
            finally
            {
                /* IGNORED */
                Utility.CleanupDirectory(
                    temporaryDirectory, GetCleanupPatterns(), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public key pairs from each of the specified key ring
        /// files, skipping files that do not exist and optionally ignoring
        /// individual load failures.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="fileNames">
        /// The list of key ring file names to load.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy to honor while loading.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting progress.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to permit loading duplicate key pairs.
        /// </param>
        /// <param name="ignoreErrors">
        /// Non-zero to ignore errors loading individual key ring files.
        /// </param>
        /// <param name="loaded">
        /// Receives the running count of key ring files that were loaded.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode LoadKeyPairsPublicOnly( /* CORE? */
            Interpreter interpreter, /* in */
            string keyRingName,      /* in: OPTIONAL */
            StringList fileNames,    /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ExecutionPolicy? policy, /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            TracePriority priority,  /* in */
            bool overwrite,          /* in */
            bool allowDuplicate,     /* in */
            bool ignoreErrors,       /* in */
            ref int loaded,          /* in, out */
            ref Result error         /* out */
            )
        {
            if (fileNames == null)
            {
                error = "invalid file name list";
                return ReturnCode.Error;
            }

            foreach (string fileName in fileNames)
            {
                if (String.IsNullOrEmpty(fileName))
                    continue;

                TracePriority localPriority; /* REUSED */

                //
                // NOTE: *EXEMPT* This call to IsRemoteUri is fine, even
                //       without checking the execution policy, because
                //       this conditional is simply an optimization.
                //       Real checks will be present in the LoadKeyPairs
                //       method.
                //
                if (!Utility.IsRemoteUri(fileName) &&
                    !File.Exists(fileName))
                {
#if DEBUG || FORCE_TRACE
                    localPriority = priority;

                    Utility.AdjustTracePriority(ref localPriority, -4);

                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Skipped loading {0} key pair file {1} as " +
                            "it does not exist.", Utility.FormatWrapOrNull(
                            policyType), Utility.FormatWrapOrNull(fileName)),
                        typeof(CertificateKeyRingOps).Name,
                        localPriority, 0);
#endif

                    continue;
                }

                Result localError = null;

                if (LoadKeyPairsPublicOnly(
                        interpreter, keyRingName, policyType, fileName,
                        cultureInfo, policy, overwrite, allowDuplicate,
                        ref localError) == ReturnCode.Ok)
                {
#if DEBUG || FORCE_TRACE
                    localPriority = priority;

                    Utility.AdjustTracePriority(ref localPriority, -3);

                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Finished loading {0} key pair file {1}.",
                            Utility.FormatWrapOrNull(policyType),
                            Utility.FormatWrapOrNull(fileName)),
                        typeof(CertificateKeyRingOps).Name,
                        localPriority, 0);
#endif

                    loaded++;
                }
                else if (!ignoreErrors)
                {
#if DEBUG || FORCE_TRACE
                    localPriority = priority;

                    Utility.AdjustTracePriority(ref localPriority, -2);

                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Honored failure loading {0} key pair file " +
                            "{1}: {2}", Utility.FormatWrapOrNull(policyType),
                            Utility.FormatWrapOrNull(fileName),
                            Utility.FormatWrapOrNull(true, false, localError)),
                        typeof(CertificateKeyRingOps).Name,
                        localPriority, 0);
#endif

                    error = localError;
                    return ReturnCode.Error;
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    localPriority = priority;

                    Utility.AdjustTracePriority(ref localPriority, 1);

                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "Ignored failure loading {0} key pair file " +
                            "{1}: {2}", Utility.FormatWrapOrNull(policyType),
                            Utility.FormatWrapOrNull(fileName),
                            Utility.FormatWrapOrNull(true, false, localError)),
                        typeof(CertificateKeyRingOps).Name,
                        localPriority, 0);
#endif
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Searches the specified directories for key ring files and loads
        /// the public key pairs from each of them.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="directories">
        /// The directories to search for key ring files.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="policy">
        /// The optional execution policy to honor while loading.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting progress.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search directories recursively.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to permit loading duplicate key pairs.
        /// </param>
        /// <param name="ignoreErrors">
        /// Non-zero to ignore errors loading individual key ring files.
        /// </param>
        /// <param name="loaded">
        /// Receives the running count of key ring files that were loaded.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode LoadKeyPairsPublicOnlyFrom( /* CORE? */
            Interpreter interpreter,         /* in */
            string keyRingName,              /* in: OPTIONAL */
            IEnumerable<string> directories, /* in */
            CultureInfo cultureInfo,         /* in: OPTIONAL */
            ExecutionPolicy? policy,         /* in: OPTIONAL */
            PolicyType policyType,           /* in */
            TracePriority priority,          /* in */
            bool recursive,                  /* in */
            bool overwrite,                  /* in */
            bool allowDuplicate,             /* in */
            bool ignoreErrors,               /* in */
            ref int loaded,                  /* in, out */
            ref Result error                 /* out */
            )
        {
            if (directories == null)
            {
                error = "invalid directory list";
                return ReturnCode.Error;
            }

            StringList list = new StringList();

            /* NO RESULT */
            GetFileNames(
                interpreter, directories, list, null, priority,
                recursive);

            return LoadKeyPairsPublicOnly(
                interpreter, keyRingName, list, cultureInfo, policy,
                policyType, priority, overwrite, allowDuplicate,
                ignoreErrors, ref loaded, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether both the file and script base policies are set
        /// to allow signed content only.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter whose policies are checked.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data whose policies are checked.
        /// </param>
        /// <returns>
        /// Non-zero if both base policies allow signed content only;
        /// otherwise, zero.
        /// </returns>
        public static bool IsBaseScriptPolicyAllowSignedOnly( /* CORE? */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData   /* in: OPTIONAL */
            )
        {
            Result error = null;

            return IsBaseScriptPolicyAllowSignedOnly(
                interpreter, pluginData, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether both the file and script base policies are set
        /// to allow signed content only, reporting any error encountered.
        /// </summary>
        /// <param name="interpreter">
        /// The optional interpreter whose policies are checked.
        /// </param>
        /// <param name="pluginData">
        /// The optional plugin data whose policies are checked.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if both base policies allow signed content only;
        /// otherwise, zero.
        /// </returns>
        public static bool IsBaseScriptPolicyAllowSignedOnly( /* CORE? */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: OPTIONAL */
            ref Result error         /* out */
            )
        {
            if (PolicyOps.IsBasePolicyAllowSignedOnly(
                    PolicyType.File, interpreter, pluginData,
                    ref error) &&
                PolicyOps.IsBasePolicyAllowSignedOnly(
                    PolicyType.Script, interpreter, pluginData,
                    ref error))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This method always assumes the "script" key ring
        //       for the interpreter should be used (and modified).
        //
        /// <summary>
        /// Loads the public key pairs for the script key ring after ensuring
        /// the file and script base policies require signed content only.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the script key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to derive directories and file names.
        /// </param>
        /// <param name="directories">
        /// The directories to search for key ring files.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="policy">
        /// The execution policy to honor while loading.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting progress.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search directories recursively.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to permit loading duplicate key pairs.
        /// </param>
        /// <param name="ignoreErrors">
        /// Non-zero to ignore errors loading individual key ring files.
        /// </param>
        /// <param name="loaded">
        /// Receives the running count of key ring files that were loaded.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode LoadScriptKeyPairsPublicOnly(
            Interpreter interpreter,         /* in */
            string keyRingName,              /* in: OPTIONAL */
            IPluginData pluginData,          /* in */
            IEnumerable<string> directories, /* in */
            CultureInfo cultureInfo,         /* in: OPTIONAL */
            ExecutionPolicy? policy,         /* in */
            TracePriority priority,          /* in */
            bool recursive,                  /* in */
            bool overwrite,                  /* in */
            bool allowDuplicate,             /* in */
            bool ignoreErrors,               /* in */
            ref int loaded,                  /* in, out */
            ref Result error                 /* out */
            )
        {
            //
            // HACK: Enforce both "File" and "Script" base
            //       policies being set to "AllowSignedOnly"
            //       prior to loading any bootstrap key ring
            //       files.
            //
            if (!IsBaseScriptPolicyAllowSignedOnly(
                    interpreter, pluginData, ref error))
            {
                return ReturnCode.Error;
            }

            BootstrapType bootstrapTypes = BootstrapType.AnyScript;

            MaybeMutateBootstrapTypes(policy, ref bootstrapTypes);

            return LoadKeyPairsPublicOnly(
                interpreter, keyRingName, pluginData, null, directories,
                cultureInfo, policy, bootstrapTypes, PolicyType.Script,
                priority, recursive, overwrite, allowDuplicate, ignoreErrors,
                ref loaded, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: This method always assumes the "license" key ring
        //       for the interpreter should be used (and modified).
        //
        /// <summary>
        /// Loads the public key pairs for the license key ring, temporarily
        /// enabling the required script and file policies and restoring the
        /// previous policy configuration afterward.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the license key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to derive directories and file names.
        /// </param>
        /// <param name="directories">
        /// The directories to search for key ring files.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="policy">
        /// The execution policy to honor while loading.
        /// </param>
        /// <param name="priority">
        /// The trace priority used when reporting progress.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search directories recursively.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to permit overwriting existing key pairs.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to permit loading duplicate key pairs.
        /// </param>
        /// <param name="ignoreErrors">
        /// Non-zero to ignore errors loading individual key ring files.
        /// </param>
        /// <param name="loaded">
        /// Receives the running count of key ring files that were loaded.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode LoadLicenseKeyPairsPublicOnly( /* CORE? */
            Interpreter interpreter,         /* in */
            string keyRingName,              /* in: OPTIONAL */
            IPluginData pluginData,          /* in */
            IEnumerable<string> directories, /* in */
            CultureInfo cultureInfo,         /* in: OPTIONAL */
            ExecutionPolicy? policy,         /* in */
            TracePriority priority,          /* in */
            bool recursive,                  /* in */
            bool overwrite,                  /* in */
            bool allowDuplicate,             /* in */
            bool ignoreErrors,               /* in */
            ref int loaded,                  /* in, out */
            ref Result error                 /* out */
            )
        {
            //
            // BUGFIX: Make sure that the plugin used to forcibly enable
            //         the script and file policies is the one for *this*
            //         plugin (i.e. a Harpy one that owns policies), not
            //         another random plugin being loaded.
            //
            IPlugin plugin = null;

            if (PolicyOps.GetPlugin(
                    interpreter, ref plugin,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            //
            // NOTE: Save the current policy configuration for the plugin
            //       prior to changing it.  This saved configuration will
            //       be restored later, once all the key ring file names
            //       have been loaded.
            //
            PolicyDictionary policies = null;

            if (PolicyOps.SavePolicies(
                    plugin, false, ref policies,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            //
            // NOTE: This cannot succeed when the plugin data instance is
            //       null.  However, we do not want to ignore errors here
            //       and we need to make sure the script and file policies
            //       are fully enabled prior to loading the key ring file.
            //
            if (PolicyOps.EnableForCommand(
                    plugin, true, false, false, false,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            try
            {
                BootstrapType bootstrapTypes = BootstrapType.AnyLicense;

                MaybeMutateBootstrapTypes(policy, ref bootstrapTypes);

                return LoadKeyPairsPublicOnly(
                    interpreter, keyRingName, pluginData, plugin,
                    directories, cultureInfo, policy, bootstrapTypes,
                    PolicyType.License, priority, recursive, overwrite,
                    allowDuplicate, ignoreErrors, ref loaded, ref error);
            }
            finally
            {
                ReturnCode restoreCode;
                Result restoreError = null;

                restoreCode = PolicyOps.RestorePolicies(
                    plugin, policies, false, true, ref restoreError);

                if (restoreCode != ReturnCode.Ok)
                {
                    Utility.Complain(
                        interpreter, restoreCode, restoreError);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public key pairs for the script or license key ring as
        /// part of the bootstrap process, based on the specified policy type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to derive directories and file names.
        /// </param>
        /// <param name="cultureInfo">
        /// The optional culture used during loading.
        /// </param>
        /// <param name="policyType">
        /// The policy type that selects the script or license key ring.
        /// </param>
        /// <param name="loaded">
        /// Receives the running count of key ring files that were loaded.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode BootstrapKeyPairsPublicOnly( /* CORE? */
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            ref int loaded,          /* in, out */
            ref Result error         /* out */
            )
        {
            if (policyType == PolicyType.Script)
            {
                return LoadScriptKeyPairsPublicOnly(
                    interpreter, null, pluginData, null,
                    cultureInfo,
                    PolicyOps.GetPolicy(
                        pluginData, policyType),
                    TracePriority.Default, false,
                    true, true, false, ref loaded,
                    ref error); /* EXEMPT */
            }
            else if (policyType == PolicyType.License)
            {
                return LoadLicenseKeyPairsPublicOnly(
                    interpreter, null, pluginData, null,
                    cultureInfo,
                    PolicyOps.GetPolicy(
                        pluginData, policyType),
                    TracePriority.Default, false,
                    true, true, false, ref loaded,
                    ref error); /* EXEMPT */
            }
            else
            {
                error = String.Format(
                    "policy type {0} unsupported for bootstrap",
                    Utility.FormatWrapOrNull(policyType));

                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the first key pair matching the specified public key token
        /// from the trusted key ring identified by the supplied name and
        /// policy type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="publicKeyToken">
        /// The optional public key token to match.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The matching key pair, or null if none was found.
        /// </returns>
        public static IKeyPair GetKeyPair( /* CORE? */
            Interpreter interpreter, /* in */
            string keyRingName,      /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            byte[] publicKeyToken,   /* in: OPTIONAL */
            ref Result error
            )
        {
            IEnumerable<IKeyPair> keyPairs = null;

            if (GetKeyPairs(
                    interpreter, keyRingName, policyType,
                    publicKeyToken, ref keyPairs,
                    ref error) == ReturnCode.Ok)
            {
                IKeyPair keyPair = null;

                if (CertificateKeyPairOps.GetFirst(
                        null, keyPairs, ref keyPair,
                        ref error) == ReturnCode.Ok)
                {
                    return keyPair;
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key pairs matching the specified public key token from
        /// the trusted key ring identified by the supplied name and policy
        /// type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="publicKeyToken">
        /// The optional public key token to match.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the matching key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode GetKeyPairs( /* CORE? */
            Interpreter interpreter,            /* in */
            string keyRingName,                 /* in: OPTIONAL */
            PolicyType policyType,              /* in */
            byte[] publicKeyToken,              /* in: OPTIONAL */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out */
            )
        {
            IKeyRing keyRing = CertificateKeyRingState.GetTrusted(
                interpreter, GetName(keyRingName, policyType), ref error);

            if (keyRing == null)
                return ReturnCode.Error;

            return keyRing.ListByToken(
                publicKeyToken, ref keyPairs, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key pairs whose names match the specified pattern from
        /// the trusted key ring identified by the supplied name and policy
        /// type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="pattern">
        /// The optional name pattern to match.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to perform a case-insensitive match.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the matching key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetKeyPairs( /* CORE? */
            Interpreter interpreter,            /* in */
            string keyRingName,                 /* in: OPTIONAL */
            PolicyType policyType,              /* in */
            string pattern,                     /* in: OPTIONAL */
            bool noCase,                        /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out */
            )
        {
            IKeyRing keyRing = CertificateKeyRingState.GetTrusted(
                interpreter, GetName(keyRingName, policyType), ref error);

            if (keyRing == null)
                return ReturnCode.Error;

            return keyRing.ListByName(
                pattern, noCase, ref keyPairs, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the trusted key ring identified by the specified name and
        /// policy type.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The optional key ring name to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type used to select the default key ring name.
        /// </param>
        /// <param name="keyRing">
        /// Upon success, receives the trusted key ring.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetKeyRing( /* CORE? */
            Interpreter interpreter, /* in */
            string keyRingName,      /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            ref IKeyRing keyRing,    /* out */
            ref Result error         /* out */
            )
        {
            IKeyRing localKeyRing = CertificateKeyRingState.GetTrusted(
                interpreter, GetName(keyRingName, policyType), ref error);

            if (localKeyRing == null)
                return ReturnCode.Error;

            keyRing = localKeyRing;
            return ReturnCode.Ok;
        }
    }
}
