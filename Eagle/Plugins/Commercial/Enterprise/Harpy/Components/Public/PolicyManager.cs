/*
 * PolicyManager.cs --
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
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Private;
using Licensing.Interfaces.Public;
using Licensing.Components.Public.Delegates;
using _Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Public
{
    /// <summary>
    /// Provides the default <see cref="IPolicyManager" /> implementation,
    /// exposing the execution policies, certificates, and related settings
    /// used to control script, file, stream, license, key pair, trace, and
    /// other policy checks.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("2f53aab7-cf53-408a-98a2-e695d68bb71c")]
    public sealed class PolicyManager :
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        IPolicyManager
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="PolicyManager" />
        /// class, preparing the certificate subsystem and the key pair types
        /// for use.
        /// </summary>
        public PolicyManager()
        {
            /* NO RESULT */
            CertificateSharedOps.SetupForCoreLibraryState();

            /* NO RESULT */
            KeyFile.InitializeKeyPairTypes(false);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPolicyManagerData Members
        /// <summary>
        /// Gets or sets the execution policy applied to script policy checks.
        /// </summary>
        public ExecutionPolicy ScriptPolicy
        {
            get { return CertificatePolicyOps.GetPolicy(PolicyType.Script); }
            set { CertificatePolicyOps.SetPolicy(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the execution policy applied to file policy checks.
        /// </summary>
        public ExecutionPolicy FilePolicy
        {
            get { return CertificatePolicyOps.GetPolicy(PolicyType.File); }
            set { CertificatePolicyOps.SetPolicy(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the execution policy applied to stream policy checks.
        /// </summary>
        public ExecutionPolicy StreamPolicy
        {
            get { return CertificatePolicyOps.GetPolicy(PolicyType.Stream); }
            set { CertificatePolicyOps.SetPolicy(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the execution policy applied to license policy
        /// checks.
        /// </summary>
        public ExecutionPolicy LicensePolicy
        {
            get { return CertificatePolicyOps.GetPolicy(PolicyType.License); }
            set { CertificatePolicyOps.SetPolicy(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the execution policy applied to key pair policy
        /// checks.
        /// </summary>
        public ExecutionPolicy KeyPairPolicy
        {
            get { return CertificatePolicyOps.GetPolicy(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetPolicy(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the execution policy applied to trace policy checks.
        /// </summary>
        public ExecutionPolicy TracePolicy
        {
            get { return CertificatePolicyOps.GetPolicy(PolicyType.Trace); }
            set { CertificatePolicyOps.SetPolicy(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the execution policy applied to other policy checks.
        /// </summary>
        public ExecutionPolicy OtherPolicy
        {
            get { return CertificatePolicyOps.GetPolicy(PolicyType.Other); }
            set { CertificatePolicyOps.SetPolicy(PolicyType.Other, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate used for script policy checks.
        /// </summary>
        public ICertificate ScriptCertificate
        {
            get { return CertificatePolicyOps.GetCertificate(PolicyType.Script); }
            set { CertificatePolicyOps.SetCertificate(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate used for file policy checks.
        /// </summary>
        public ICertificate FileCertificate
        {
            get { return CertificatePolicyOps.GetCertificate(PolicyType.File); }
            set { CertificatePolicyOps.SetCertificate(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate used for stream policy checks.
        /// </summary>
        public ICertificate StreamCertificate
        {
            get { return CertificatePolicyOps.GetCertificate(PolicyType.Stream); }
            set { CertificatePolicyOps.SetCertificate(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate used for license policy checks.
        /// </summary>
        public ICertificate LicenseCertificate
        {
            get { return CertificatePolicyOps.GetCertificate(PolicyType.License); }
            set { CertificatePolicyOps.SetCertificate(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate used for key pair policy checks.
        /// </summary>
        public ICertificate KeyPairCertificate
        {
            get { return CertificatePolicyOps.GetCertificate(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetCertificate(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate used for trace policy checks.
        /// </summary>
        public ICertificate TraceCertificate
        {
            get { return CertificatePolicyOps.GetCertificate(PolicyType.Trace); }
            set { CertificatePolicyOps.SetCertificate(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the certificate used for other policy checks.
        /// </summary>
        public ICertificate OtherCertificate
        {
            get { return CertificatePolicyOps.GetCertificate(PolicyType.Other); }
            set { CertificatePolicyOps.SetCertificate(PolicyType.Other, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the assembly associated with script policy checks.
        /// </summary>
        public Assembly ScriptAssembly
        {
            get { return CertificatePolicyOps.GetAssembly(PolicyType.Script); }
            set { CertificatePolicyOps.SetAssembly(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the assembly associated with file policy checks.
        /// </summary>
        public Assembly FileAssembly
        {
            get { return CertificatePolicyOps.GetAssembly(PolicyType.File); }
            set { CertificatePolicyOps.SetAssembly(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the assembly associated with stream policy checks.
        /// </summary>
        public Assembly StreamAssembly
        {
            get { return CertificatePolicyOps.GetAssembly(PolicyType.Stream); }
            set { CertificatePolicyOps.SetAssembly(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the assembly associated with license policy checks.
        /// </summary>
        public Assembly LicenseAssembly
        {
            get { return CertificatePolicyOps.GetAssembly(PolicyType.License); }
            set { CertificatePolicyOps.SetAssembly(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the assembly associated with key pair policy checks.
        /// </summary>
        public Assembly KeyPairAssembly
        {
            get { return CertificatePolicyOps.GetAssembly(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetAssembly(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the assembly associated with trace policy checks.
        /// </summary>
        public Assembly TraceAssembly
        {
            get { return CertificatePolicyOps.GetAssembly(PolicyType.Trace); }
            set { CertificatePolicyOps.SetAssembly(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the assembly associated with other policy checks.
        /// </summary>
        public Assembly OtherAssembly
        {
            get { return CertificatePolicyOps.GetAssembly(PolicyType.Other); }
            set { CertificatePolicyOps.SetAssembly(PolicyType.Other, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key name used for script policy checks.
        /// </summary>
        public string ScriptKeyName
        {
            get { return CertificatePolicyOps.GetKeyName(PolicyType.Script); }
            set { CertificatePolicyOps.SetKeyName(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key name used for file policy checks.
        /// </summary>
        public string FileKeyName
        {
            get { return CertificatePolicyOps.GetKeyName(PolicyType.File); }
            set { CertificatePolicyOps.SetKeyName(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key name used for stream policy checks.
        /// </summary>
        public string StreamKeyName
        {
            get { return CertificatePolicyOps.GetKeyName(PolicyType.Stream); }
            set { CertificatePolicyOps.SetKeyName(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key name used for license policy checks.
        /// </summary>
        public string LicenseKeyName
        {
            get { return CertificatePolicyOps.GetKeyName(PolicyType.License); }
            set { CertificatePolicyOps.SetKeyName(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key name used for key pair policy checks.
        /// </summary>
        public string KeyPairKeyName
        {
            get { return CertificatePolicyOps.GetKeyName(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetKeyName(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key name used for trace policy checks.
        /// </summary>
        public string TraceKeyName
        {
            get { return CertificatePolicyOps.GetKeyName(PolicyType.Trace); }
            set { CertificatePolicyOps.SetKeyName(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key name used for other policy checks.
        /// </summary>
        public string OtherKeyName
        {
            get { return CertificatePolicyOps.GetKeyName(PolicyType.Other); }
            set { CertificatePolicyOps.SetKeyName(PolicyType.Other, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key ring name used for script policy checks.
        /// </summary>
        public string ScriptKeyRingName
        {
            get { return CertificatePolicyOps.GetKeyRingName(PolicyType.Script); }
            set { CertificatePolicyOps.SetKeyRingName(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key ring name used for file policy checks.
        /// </summary>
        public string FileKeyRingName
        {
            get { return CertificatePolicyOps.GetKeyRingName(PolicyType.File); }
            set { CertificatePolicyOps.SetKeyRingName(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key ring name used for stream policy checks.
        /// </summary>
        public string StreamKeyRingName
        {
            get { return CertificatePolicyOps.GetKeyRingName(PolicyType.Stream); }
            set { CertificatePolicyOps.SetKeyRingName(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key ring name used for license policy checks.
        /// </summary>
        public string LicenseKeyRingName
        {
            get { return CertificatePolicyOps.GetKeyRingName(PolicyType.License); }
            set { CertificatePolicyOps.SetKeyRingName(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key ring name used for key pair policy checks.
        /// </summary>
        public string KeyPairKeyRingName
        {
            get { return CertificatePolicyOps.GetKeyRingName(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetKeyRingName(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key ring name used for trace policy checks.
        /// </summary>
        public string TraceKeyRingName
        {
            get { return CertificatePolicyOps.GetKeyRingName(PolicyType.Trace); }
            set { CertificatePolicyOps.SetKeyRingName(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the key ring name used for other policy checks.
        /// </summary>
        public string OtherKeyRingName
        {
            get { return CertificatePolicyOps.GetKeyRingName(PolicyType.Other); }
            set { CertificatePolicyOps.SetKeyRingName(PolicyType.Other, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the script flags used for script policy checks.
        /// </summary>
        public ScriptFlags ScriptScriptFlags
        {
            get { return CertificatePolicyOps.GetScriptFlags(PolicyType.Script); }
            set { CertificatePolicyOps.SetScriptFlags(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the script flags used for file policy checks.
        /// </summary>
        public ScriptFlags FileScriptFlags
        {
            get { return CertificatePolicyOps.GetScriptFlags(PolicyType.File); }
            set { CertificatePolicyOps.SetScriptFlags(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the script flags used for stream policy checks.
        /// </summary>
        public ScriptFlags StreamScriptFlags
        {
            get { return CertificatePolicyOps.GetScriptFlags(PolicyType.Stream); }
            set { CertificatePolicyOps.SetScriptFlags(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the script flags used for license policy checks.
        /// </summary>
        public ScriptFlags LicenseScriptFlags
        {
            get { return CertificatePolicyOps.GetScriptFlags(PolicyType.License); }
            set { CertificatePolicyOps.SetScriptFlags(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the script flags used for key pair policy checks.
        /// </summary>
        public ScriptFlags KeyPairScriptFlags
        {
            get { return CertificatePolicyOps.GetScriptFlags(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetScriptFlags(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the script flags used for trace policy checks.
        /// </summary>
        public ScriptFlags TraceScriptFlags
        {
            get { return CertificatePolicyOps.GetScriptFlags(PolicyType.Trace); }
            set { CertificatePolicyOps.SetScriptFlags(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the script flags used for other policy checks.
        /// </summary>
        public ScriptFlags OtherScriptFlags
        {
            get { return CertificatePolicyOps.GetScriptFlags(PolicyType.Other); }
            set { CertificatePolicyOps.SetScriptFlags(PolicyType.Other, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the path flags used for script policy checks.
        /// </summary>
        public PathFlags ScriptPathFlags
        {
            get { return CertificatePolicyOps.GetPathFlags(PolicyType.Script); }
            set { CertificatePolicyOps.SetPathFlags(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the path flags used for file policy checks.
        /// </summary>
        public PathFlags FilePathFlags
        {
            get { return CertificatePolicyOps.GetPathFlags(PolicyType.File); }
            set { CertificatePolicyOps.SetPathFlags(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the path flags used for stream policy checks.
        /// </summary>
        public PathFlags StreamPathFlags
        {
            get { return CertificatePolicyOps.GetPathFlags(PolicyType.Stream); }
            set { CertificatePolicyOps.SetPathFlags(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the path flags used for license policy checks.
        /// </summary>
        public PathFlags LicensePathFlags
        {
            get { return CertificatePolicyOps.GetPathFlags(PolicyType.License); }
            set { CertificatePolicyOps.SetPathFlags(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the path flags used for key pair policy checks.
        /// </summary>
        public PathFlags KeyPairPathFlags
        {
            get { return CertificatePolicyOps.GetPathFlags(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetPathFlags(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the path flags used for trace policy checks.
        /// </summary>
        public PathFlags TracePathFlags
        {
            get { return CertificatePolicyOps.GetPathFlags(PolicyType.Trace); }
            set { CertificatePolicyOps.SetPathFlags(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the path flags used for other policy checks.
        /// </summary>
        public PathFlags OtherPathFlags
        {
            get { return CertificatePolicyOps.GetPathFlags(PolicyType.Other); }
            set { CertificatePolicyOps.SetPathFlags(PolicyType.Other, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the network flags used for script policy checks.
        /// </summary>
        public NetworkFlags ScriptNetworkFlags
        {
            get { return CertificatePolicyOps.GetNetworkFlags(PolicyType.Script); }
            set { CertificatePolicyOps.SetNetworkFlags(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the network flags used for file policy checks.
        /// </summary>
        public NetworkFlags FileNetworkFlags
        {
            get { return CertificatePolicyOps.GetNetworkFlags(PolicyType.File); }
            set { CertificatePolicyOps.SetNetworkFlags(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the network flags used for stream policy checks.
        /// </summary>
        public NetworkFlags StreamNetworkFlags
        {
            get { return CertificatePolicyOps.GetNetworkFlags(PolicyType.Stream); }
            set { CertificatePolicyOps.SetNetworkFlags(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the network flags used for license policy checks.
        /// </summary>
        public NetworkFlags LicenseNetworkFlags
        {
            get { return CertificatePolicyOps.GetNetworkFlags(PolicyType.License); }
            set { CertificatePolicyOps.SetNetworkFlags(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the network flags used for key pair policy checks.
        /// </summary>
        public NetworkFlags KeyPairNetworkFlags
        {
            get { return CertificatePolicyOps.GetNetworkFlags(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetNetworkFlags(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the network flags used for trace policy checks.
        /// </summary>
        public NetworkFlags TraceNetworkFlags
        {
            get { return CertificatePolicyOps.GetNetworkFlags(PolicyType.Trace); }
            set { CertificatePolicyOps.SetNetworkFlags(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the network flags used for other policy checks.
        /// </summary>
        public NetworkFlags OtherNetworkFlags
        {
            get { return CertificatePolicyOps.GetNetworkFlags(PolicyType.Other); }
            set { CertificatePolicyOps.SetNetworkFlags(PolicyType.Other, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the renew callback invoked for script policy checks.
        /// </summary>
        public RenewCallback ScriptRenewCallback
        {
            get { return CertificatePolicyOps.GetRenewCallback(PolicyType.Script); }
            set { CertificatePolicyOps.SetRenewCallback(PolicyType.Script, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the renew callback invoked for file policy checks.
        /// </summary>
        public RenewCallback FileRenewCallback
        {
            get { return CertificatePolicyOps.GetRenewCallback(PolicyType.File); }
            set { CertificatePolicyOps.SetRenewCallback(PolicyType.File, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the renew callback invoked for stream policy checks.
        /// </summary>
        public RenewCallback StreamRenewCallback
        {
            get { return CertificatePolicyOps.GetRenewCallback(PolicyType.Stream); }
            set { CertificatePolicyOps.SetRenewCallback(PolicyType.Stream, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the renew callback invoked for license policy checks.
        /// </summary>
        public RenewCallback LicenseRenewCallback
        {
            get { return CertificatePolicyOps.GetRenewCallback(PolicyType.License); }
            set { CertificatePolicyOps.SetRenewCallback(PolicyType.License, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the renew callback invoked for key pair policy
        /// checks.
        /// </summary>
        public RenewCallback KeyPairRenewCallback
        {
            get { return CertificatePolicyOps.GetRenewCallback(PolicyType.KeyPair); }
            set { CertificatePolicyOps.SetRenewCallback(PolicyType.KeyPair, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the renew callback invoked for trace policy checks.
        /// </summary>
        public RenewCallback TraceRenewCallback
        {
            get { return CertificatePolicyOps.GetRenewCallback(PolicyType.Trace); }
            set { CertificatePolicyOps.SetRenewCallback(PolicyType.Trace, value); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the renew callback invoked for other policy checks.
        /// </summary>
        public RenewCallback OtherRenewCallback
        {
            get { return CertificatePolicyOps.GetRenewCallback(PolicyType.Other); }
            set { CertificatePolicyOps.SetRenewCallback(PolicyType.Other, value); }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IPolicyManager Members
        /// <summary>
        /// Dispatches a policy check to the appropriate handler based on the
        /// specified <paramref name="policyType" />.
        /// </summary>
        /// <param name="policyType">
        /// The type of policy check to perform.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
        /// </returns>
        public ReturnCode Check(
            PolicyType policyType,   /* in */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            switch (policyType)
            {
                case PolicyType.Script:
                    {
                        return CheckScript(
                            interpreter, clientData, arguments,
                            ref result);
                    }
                case PolicyType.File:
                    {
                        return CheckFile(
                            interpreter, clientData, arguments,
                            ref result);
                    }
                case PolicyType.Stream:
                    {
                        return CheckStream(
                            interpreter, clientData, arguments,
                            ref result);
                    }
                case PolicyType.License:
                    {
                        return CheckLicense(
                            interpreter, clientData, arguments,
                            ref result);
                    }
                case PolicyType.KeyPair:
                    {
                        return CheckKeyPair(
                            interpreter, clientData, arguments,
                            ref result);
                    }
                case PolicyType.Trace:
                    {
                        return CheckTrace(
                            interpreter, clientData, arguments,
                            ref result);
                    }
                case PolicyType.Other:
                    {
                        return CheckOther(
                            interpreter, clientData, arguments,
                            ref result);
                    }
                default:
                    {
                        result = String.Format(
                            "unsupported policy type {0}",
                            _Utility.FormatWrapOrNull(policyType));

                        return ReturnCode.Error;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the script policy check by invoking the script policy
        /// callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
        /// </returns>
        public ReturnCode CheckScript(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return Licensing.Policies.Script.PolicyCallback(
                interpreter, clientData, arguments, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the file policy check by invoking the file policy
        /// callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
        /// </returns>
        public ReturnCode CheckFile(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return Licensing.Policies.File.PolicyCallback(
                interpreter, clientData, arguments, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the stream policy check by invoking the stream policy
        /// callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
        /// </returns>
        public ReturnCode CheckStream(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return Licensing.Policies.Stream.PolicyCallback(
                interpreter, clientData, arguments, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the license policy check by invoking the license policy
        /// callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
        /// </returns>
        public ReturnCode CheckLicense(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return Licensing.Policies.License.PolicyCallback(
                interpreter, clientData, arguments, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the key pair policy check by invoking the key pair policy
        /// callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
        /// </returns>
        public ReturnCode CheckKeyPair(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return Licensing.Policies.KeyPair.PolicyCallback(
                interpreter, clientData, arguments, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the trace policy check by invoking the trace policy
        /// callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
        /// </returns>
        public ReturnCode CheckTrace(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return Licensing.Policies.Trace.PolicyCallback(
                interpreter, clientData, arguments, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the other policy check by invoking the other policy
        /// callback.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context for which the policy is being evaluated.
        /// </param>
        /// <param name="clientData">
        /// Optional caller-supplied data associated with the policy request.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments being checked against the policy.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the policy decision or an error message.
        /// </param>
        /// <returns>
        /// A <see cref="ReturnCode" /> indicating whether the policy check
        /// succeeded.
        /// </returns>
        public ReturnCode CheckOther(
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in */
            ArgumentList arguments,  /* in */
            ref Result result        /* out */
            )
        {
            return Licensing.Policies.Other.PolicyCallback(
                interpreter, clientData, arguments, ref result);
        }
        #endregion
    }
}
