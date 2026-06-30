/*
 * Example3.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Official Self-Contained Certificate Validation & Verification API Example
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
using System.Reflection;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Sdk.Private;
using StringPair = System.Collections.Generic.KeyValuePair<string, string>;

namespace Example3
{
    /// <summary>
    /// This is a public library that demonstrates how to use the Harpy
    /// "late-bound" licensing SDK in order to validate and verify a
    /// license certificate against a given assembly.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    public static class Library
    {
        #region Private Constants
#if !NET_STANDARD_20 && !NET_STANDARD_21
        /// <summary>
        /// This is the name of the environment variable that is used to
        /// forcibly disable extra plugin probing.
        /// </summary>
        private const string NoProbePluginsEnvVarName = "HarpyNoProbePlugins";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This is the name of the environment variable that is used to
        /// forcibly disable use of plugin isolation.
        /// </summary>
        private const string NoIsolatedEnvVarName = "HarpyNoIsolated";
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This is the name of the environment variable that is used to
        /// forcibly enable use of SDK mode for license verification.
        /// </summary>
        public const string ForceSdkModeEnvVarName = "HarpyForceSdkMode";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This is the name of the environment variable that is used to
        /// disable the loading of supplementary key ring files.
        /// </summary>
        private const string NoKeyRingsEnvVarName = "NoLoadLicenseKeyRings";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], this SDK will
        /// not attempt to disable (further) interpreter creation.
        /// </summary>
        private const string NoDisableCreationEnvVarName =
            "HarpyNoDisableCreation";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], this SDK will
        /// not attempt to contact network time servers.
        /// </summary>
        private const string NoNetworkTimeEnvVarName =
            "NoNetworkTime";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], this SDK may
        /// emit important diagnostic messages via the console and/or other
        /// means.
        /// </summary>
        private const string WriteWithoutFailEnvVarName =
            "WriteWithoutFail";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], this SDK may
        /// not emit important diagnostic messages via the console and/or
        /// other means.
        /// </summary>
        private const string NoWriteWithoutFailEnvVarName =
            "NoWriteWithoutFail";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This is the name of the resource that must contain the license
        /// certificate for the Harpy SDK itself.
        /// </summary>
        private const string SdkLicenseResourceName = "Harpy.certificate.exml";

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20 && !NET_STANDARD_21
        /// <summary>
        /// This is the hexadecimal prefix used for the "Key" property value
        /// in the certificate data returned by the Harpy SDK.
        /// </summary>
        private const string KeyHexadecimalPrefix = "0x";
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This prefix on a file name to be evaluated indicates that it is
        /// an actual file on the file system.
        /// </summary>
        private const string FilePrefix = "file:";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This prefix on a file name to be evaluated indicates that it is
        /// an embedded resource stream.
        /// </summary>
        private const string StreamPrefix = "stream:";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Data
        /// <summary>
        /// This is used to synchronize calls into the Harpy managed SDK,
        /// e.g. to keep the environment variable access thread-safe.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The list of file hashes to use when checking if a plugin file is
        /// considered to be "fully trusted".  This will only be used if the
        /// underlying platform does not support Authenticode.
        /// </summary>
        private static readonly StringList trustedHashes = new StringList(
            new string[] {
#if NET_STANDARD_20 || NET_STANDARD_21
            //
            // TODO: Add your static list of trusted plugin file hashes here,
            //       for use with the .NET Core runtime on Linux, macOS, etc.
            //
            // NOTE: The format of the hash entry strings must be as
            //       follows:
            //
            //       PolicyType <space> HashAlgorithmName <space> HashValue
            //
            //       The policy type MUST parse to a valid enumeration
            //       value.
            //
            //       The hash algorithm name SHOULD (almost always) be
            //       SHA512; however, other valid hash algorithm names
            //       MAY be accepted.
            //
            //       The hash value MUST be a string representation of
            //       a Base16 number with an optional "0x" prefix -OR-
            //       a Base64 encoded byte array for the computed hash
            //       over the entire file.
            //
#endif
        });
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
#if !NET_STANDARD_20 && !NET_STANDARD_21
        /// <summary>
        /// Attempt to extract the "Key" (a.k.a. public key token) from the
        /// specified dictionary and add it to the specified list.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary of strings obtained from the license certification
        /// verification call.
        /// </param>
        /// <param name="list">
        /// The list of strings, as pairs of names and values, to return to
        /// the caller of the <see cref="Verify" /> method.
        /// </param>
        private static void MaybeAddPublicKeyToken(
            StringDictionary dictionary, /* in */
            IList<string> list           /* in, out */
            )
        {
            string key;

            if (dictionary.TryGetValue("Key", out key))
            {
                //
                // HACK: Strip off the leading hexadecimal prefix because
                //       assembly public key tokens do not have it in the
                //       .NET Framework, et al.
                //
                if ((key != null) && key.StartsWith(
                        KeyHexadecimalPrefix, StringComparison.Ordinal))
                {
                    key = key.Substring(KeyHexadecimalPrefix.Length);
                }

                list.Add("publicKeyToken");
                list.Add(key);
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method examines the specified file name and determines if it
        /// actually refers to an embedded resource stream.  If necessary, it
        /// will modify the file name (in place) as well as update the passed
        /// <see cref="IAnyClientData" /> instance.
        /// </summary>
        /// <param name="anyClientData">
        /// The <see cref="IAnyClientData" /> instance that will be passed to
        /// the <see cref="LicenseOps.EvaluateFile" /> endpoint of the Harpy
        /// managed SDK.
        /// </param>
        /// <param name="fileName">
        /// The fully qualified file name of the configuration file or stream
        /// to be loaded.
        /// </param>
        private static void CheckFileOrStream(
            IAnyClientData anyClientData, /* in: OPTIONAL */
            ref string fileName           /* in, out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return;

            if (!String.IsNullOrEmpty(FilePrefix) &&
                fileName.StartsWith(FilePrefix, StringComparison.Ordinal))
            {
                fileName = fileName.Substring(FilePrefix.Length);
            }
            else if (!String.IsNullOrEmpty(StreamPrefix) &&
                fileName.StartsWith(StreamPrefix, StringComparison.Ordinal))
            {
                fileName = fileName.Substring(StreamPrefix.Length);

                if (anyClientData != null)
                    anyClientData.TrySetAny("stream", fileName);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// This is an entry point for this library.  It performs the basic
        /// actions necessary to validate and verify the license certificate
        /// associated with this library.  The code is designed to be easily
        /// adapted for use with any library that requires license checking.
        /// </summary>
        /// <param name="assembly">
        /// The optional assembly containing an embedded license certificate
        /// resource ("Harpy.certificate.exml") for Harpy itself.  If null,
        /// license certificate file may be assumed to exist in a directory
        /// associated with the current application domain.
        /// </param>
        /// <param name="fileName">
        /// The fully qualified file name of the license certificate that
        /// needs to be verified.
        /// </param>
        /// <param name="certificate">
        /// Upon success, this parameter will contain a list with an even
        /// number of elements, with each pair of elements representing a
        /// property name and value associated with the specified license
        /// certificate.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, it will contain a suitable error message.
        /// </param>
        /// <returns>
        /// Non-zero if the license certificate is successfully validated
        /// and verified; otherwise, zero.
        /// </returns>
        public static bool Verify(
            Assembly assembly,             /* in */
            string fileName,               /* in */
            ref IList<string> certificate, /* out */
            ref string error               /* out */
            )
        {
            ///////////////////////////////////////////////////////////
            //          REQUIRED: PERFORM THE LICENSE CHECK          //
            ///////////////////////////////////////////////////////////

            //
            //
            // NOTE: Call the simplest of the late-bound licensing SDK
            //       entry points.  It will create the necessary Eagle
            //       interpreter context automatically.
            //
            ReturnCode code;
            string localFileName = fileName;
            object localCertificate = null;
            Result result = null;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                //
                // HACK: Force all network access to be disabled,
                //       at least via standard WebClient wrapper.
                //
                // Utility.SetOfflineMode(true);

                //
                // HACK: Force the Eagle core library to be fully
                //       initialized for this AppDomain now.
                //
                Interpreter.MaybeStaticInitialize();

                //
                // NOTE: Attempt to use the assembly specified by
                //       the caller.  If null, fallback to using
                //       the assembly for this class.
                //
                Assembly localAssembly = assembly;

                if (localAssembly == null)
                    localAssembly = typeof(Library).Assembly;

                //
                // NOTE: Since there should be no need to load any
                //       extra key ring files to verify either of
                //       the license certificates (i.e. the Harpy
                //       SDK itself -AND- the target assembly),
                //       disable this feature as it takes quite a
                //       bit of extra time.
                //
                string[] options = {
#if !NET_STANDARD_20 && !NET_STANDARD_21
                    NoProbePluginsEnvVarName,
                    NoIsolatedEnvVarName,
#endif
                    ForceSdkModeEnvVarName,
                    NoKeyRingsEnvVarName,
                    // NoNetworkTimeEnvVarName,
                    NoDisableCreationEnvVarName
                };

                code = LicenseOps.ExtractAndVerifyCertificate(
                    localAssembly, trustedHashes, SdkLicenseResourceName,
                    options, ref localFileName, ref localCertificate,
                    ref result);
            }

            //
            // NOTE: *IMPORTANT* This is the critical check.  If the
            //       return code from the license checking operation
            //       above is "Ok", then this program is considered
            //       to be licensed fully and properly; otherwise,
            //       an error was encountered and the program cannot
            //       be considered to be "licensed" (i.e. it should
            //       emit an appropriate error message and abort).
            //
            if (code == ReturnCode.Ok)
            {
                ///////////////////////////////////////////////////////
                //  BEGIN OPTIONAL: HANDLE LICENSE CHECKING SUCCESS  //
                ///////////////////////////////////////////////////////

                //
                // NOTE: This (completely optional) block of code is
                //       used to construct some human-readable output
                //       from the detailed license checking results.
                //
                IList<string> list = new List<string>();

                //
                // NOTE: First, add the resulting license certificate
                //       file name.  This parameter to the license SDK
                //       call was technically in/out; however, it will
                //       [almost] always have an output value that is
                //       functionally identical to the original input
                //       value upon if that input value was not null.
                //
                if (localFileName != null)
                {
                    list.Add("fileName");
                    list.Add(localFileName);
                }

#if !NET_STANDARD_20 && !NET_STANDARD_21
                //
                // NOTE: Next, add the detailed license certificate
                //       information, if it is available.  The precise
                //       format of string returned by this method is
                //       officially "unspecified", except that it will
                //       always contain at least enough data to uniquely
                //       identify the license certificate.  Typically,
                //       it will contain a [dictionary formatted] list
                //       of name/value pairs with the detailed license
                //       certificate information.
                //
                if (localCertificate != null)
                {
                    StringDictionary dictionary =
                        localCertificate as StringDictionary;

                    Result localError = null;

                    if (dictionary == null)
                    {
                        dictionary = StringDictionary.FromString(
                            localCertificate.ToString(), false,
                            ref localError);
                    }

                    if (dictionary != null)
                    {
                        /* NO RESULT */
                        MaybeAddPublicKeyToken(dictionary, list);

                        foreach (StringPair pair in dictionary)
                        {
                            list.Add(pair.Key);
                            list.Add(pair.Value);
                        }
                    }
                    else if (localError != null)
                    {
                        list.Add("error");
                        list.Add(localError);
                    }
                }
#endif

                //
                // NOTE: Next, add the overall textual result of the
                //       license SDK call.  This will almost always
                //       be the literal string "VerifiedOk".
                //
                if (result != null)
                {
                    list.Add("result");
                    list.Add(result);
                }

                certificate = list;
                return true;

                ///////////////////////////////////////////////////////
                //   END OPTIONAL: HANDLE LICENSE CHECKING SUCCESS   //
                ///////////////////////////////////////////////////////
            }
            else
            {
                ///////////////////////////////////////////////////////
                //  BEGIN OPTIONAL: HANDLE LICENSE CHECKING FAILURE  //
                ///////////////////////////////////////////////////////

                error = result;
                return false;

                ///////////////////////////////////////////////////////
                //   END OPTIONAL: HANDLE LICENSE CHECKING FAILURE   //
                ///////////////////////////////////////////////////////
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This is an entry point for this library.  It performs the basic
        /// actions necessary to load a configuration file belonging to the
        /// Harpy SDK.
        /// </summary>
        /// <param name="fileName">
        /// The fully qualified file name of the configuration file or stream
        /// to be loaded.
        /// </param>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, it will contain a suitable error message.
        /// </param>
        /// <returns>
        /// Non-zero if the configuration file is successfully verified and
        /// loaded; otherwise, zero.
        /// </returns>
        public static bool Configure(
            string fileName, /* in */
            ref string error /* out */
            )
        {
            IAnyClientData anyClientData = new AnyClientData();

            CheckFileOrStream(anyClientData, ref fileName);

            anyClientData.TrySetAny("fileName", fileName);
            anyClientData.TrySetAny("allowRemoteUri", true);
            anyClientData.TrySetAny("useContext", true);
            anyClientData.TrySetAny("withCommands", true);

            lock (syncRoot) /* TRANSACTIONAL */
            {
                bool? savedWrite = null;

                if (!Utility.DoesEnvironmentVariableExist(
                        NoWriteWithoutFailEnvVarName))
                {
                    if (Utility.DoesEnvironmentVariableExist(
                            WriteWithoutFailEnvVarName))
                    {
                        savedWrite = true;
                    }
                    else
                    {
                        savedWrite = false;

                        Utility.SetEnvironmentVariable(
                            WriteWithoutFailEnvVarName, 1.ToString());
                    }
                }

                try
                {
                    Result result = null;

                    if (LicenseOps.EvaluateFile(
                            null, null, anyClientData, false,
                            ref result) == ReturnCode.Ok)
                    {
                        return true;
                    }
                    else
                    {
                        error = result;
                        return false;
                    }
                }
                finally
                {
                    if ((savedWrite != null) && !(bool)savedWrite)
                    {
                        Utility.UnsetEnvironmentVariable(
                            WriteWithoutFailEnvVarName);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to cleanup any internal state within this application
        /// domain related to this library.
        /// </summary>
        /// <param name="error">
        /// Upon success, the value of this parameter is undefined.  Upon
        /// failure, it will contain a suitable error message.
        /// </param>
        /// <returns>
        /// Non-zero if the cleanup was successful; otherwise, zero.
        /// </returns>
        public static bool Cleanup(
            ref string error /* out */
            )
        {
            Result localError = null;

            if (LicenseOps.Cleanup(
                    null, null, ref localError) == ReturnCode.Ok)
            {
                return true;
            }
            else
            {
                error = localError;
                return false;
            }
        }
        #endregion
    }
}
