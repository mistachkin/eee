/*
 * PolicyManagerData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Licensing.Components.Public.Delegates;

namespace Licensing.Interfaces.Public
{
    /// <summary>
    /// Provides access to the per-category configuration data used by a
    /// policy manager to evaluate and enforce execution policies.  Each
    /// category (script, file, stream, license, key pair, trace, and other)
    /// exposes its own policy, certificate, assembly, key names, flags, and
    /// renewal callback.
    /// </summary>
    [ObjectId("bb784699-fea3-454f-88e3-50cfc2138a53")]
    public interface IPolicyManagerData
    {
        /// <summary>
        /// Gets or sets the execution policy applied to script operations.
        /// </summary>
        ExecutionPolicy ScriptPolicy { get; set; }
        /// <summary>
        /// Gets or sets the execution policy applied to file operations.
        /// </summary>
        ExecutionPolicy FilePolicy { get; set; }
        /// <summary>
        /// Gets or sets the execution policy applied to stream operations.
        /// </summary>
        ExecutionPolicy StreamPolicy { get; set; }
        /// <summary>
        /// Gets or sets the execution policy applied to license operations.
        /// </summary>
        ExecutionPolicy LicensePolicy { get; set; }
        /// <summary>
        /// Gets or sets the execution policy applied to key pair operations.
        /// </summary>
        ExecutionPolicy KeyPairPolicy { get; set; }
        /// <summary>
        /// Gets or sets the execution policy applied to trace operations.
        /// </summary>
        ExecutionPolicy TracePolicy { get; set; }
        /// <summary>
        /// Gets or sets the execution policy applied to other operations.
        /// </summary>
        ExecutionPolicy OtherPolicy { get; set; }

        /// <summary>
        /// Gets or sets the certificate used for script operations.
        /// </summary>
        ICertificate ScriptCertificate { get; set; }
        /// <summary>
        /// Gets or sets the certificate used for file operations.
        /// </summary>
        ICertificate FileCertificate { get; set; }
        /// <summary>
        /// Gets or sets the certificate used for stream operations.
        /// </summary>
        ICertificate StreamCertificate { get; set; }
        /// <summary>
        /// Gets or sets the certificate used for license operations.
        /// </summary>
        ICertificate LicenseCertificate { get; set; }
        /// <summary>
        /// Gets or sets the certificate used for key pair operations.
        /// </summary>
        ICertificate KeyPairCertificate { get; set; }
        /// <summary>
        /// Gets or sets the certificate used for trace operations.
        /// </summary>
        ICertificate TraceCertificate { get; set; }
        /// <summary>
        /// Gets or sets the certificate used for other operations.
        /// </summary>
        ICertificate OtherCertificate { get; set; }

        /// <summary>
        /// Gets or sets the assembly associated with script operations.
        /// </summary>
        Assembly ScriptAssembly { get; set; }
        /// <summary>
        /// Gets or sets the assembly associated with file operations.
        /// </summary>
        Assembly FileAssembly { get; set; }
        /// <summary>
        /// Gets or sets the assembly associated with stream operations.
        /// </summary>
        Assembly StreamAssembly { get; set; }
        /// <summary>
        /// Gets or sets the assembly associated with license operations.
        /// </summary>
        Assembly LicenseAssembly { get; set; }
        /// <summary>
        /// Gets or sets the assembly associated with key pair operations.
        /// </summary>
        Assembly KeyPairAssembly { get; set; }
        /// <summary>
        /// Gets or sets the assembly associated with trace operations.
        /// </summary>
        Assembly TraceAssembly { get; set; }
        /// <summary>
        /// Gets or sets the assembly associated with other operations.
        /// </summary>
        Assembly OtherAssembly { get; set; }

        /// <summary>
        /// Gets or sets the key name used for script operations.
        /// </summary>
        string ScriptKeyName { get; set; }
        /// <summary>
        /// Gets or sets the key name used for file operations.
        /// </summary>
        string FileKeyName { get; set; }
        /// <summary>
        /// Gets or sets the key name used for stream operations.
        /// </summary>
        string StreamKeyName { get; set; }
        /// <summary>
        /// Gets or sets the key name used for license operations.
        /// </summary>
        string LicenseKeyName { get; set; }
        /// <summary>
        /// Gets or sets the key name used for key pair operations.
        /// </summary>
        string KeyPairKeyName { get; set; }
        /// <summary>
        /// Gets or sets the key name used for trace operations.
        /// </summary>
        string TraceKeyName { get; set; }
        /// <summary>
        /// Gets or sets the key name used for other operations.
        /// </summary>
        string OtherKeyName { get; set; }

        /// <summary>
        /// Gets or sets the key ring name used for script operations.
        /// </summary>
        string ScriptKeyRingName { get; set; }
        /// <summary>
        /// Gets or sets the key ring name used for file operations.
        /// </summary>
        string FileKeyRingName { get; set; }
        /// <summary>
        /// Gets or sets the key ring name used for stream operations.
        /// </summary>
        string StreamKeyRingName { get; set; }
        /// <summary>
        /// Gets or sets the key ring name used for license operations.
        /// </summary>
        string LicenseKeyRingName { get; set; }
        /// <summary>
        /// Gets or sets the key ring name used for key pair operations.
        /// </summary>
        string KeyPairKeyRingName { get; set; }
        /// <summary>
        /// Gets or sets the key ring name used for trace operations.
        /// </summary>
        string TraceKeyRingName { get; set; }
        /// <summary>
        /// Gets or sets the key ring name used for other operations.
        /// </summary>
        string OtherKeyRingName { get; set; }

        /// <summary>
        /// Gets or sets the script flags used for script operations.
        /// </summary>
        ScriptFlags ScriptScriptFlags { get; set; }
        /// <summary>
        /// Gets or sets the script flags used for file operations.
        /// </summary>
        ScriptFlags FileScriptFlags { get; set; }
        /// <summary>
        /// Gets or sets the script flags used for stream operations.
        /// </summary>
        ScriptFlags StreamScriptFlags { get; set; }
        /// <summary>
        /// Gets or sets the script flags used for license operations.
        /// </summary>
        ScriptFlags LicenseScriptFlags { get; set; }
        /// <summary>
        /// Gets or sets the script flags used for key pair operations.
        /// </summary>
        ScriptFlags KeyPairScriptFlags { get; set; }
        /// <summary>
        /// Gets or sets the script flags used for trace operations.
        /// </summary>
        ScriptFlags TraceScriptFlags { get; set; }
        /// <summary>
        /// Gets or sets the script flags used for other operations.
        /// </summary>
        ScriptFlags OtherScriptFlags { get; set; }

        /// <summary>
        /// Gets or sets the path flags used for script operations.
        /// </summary>
        PathFlags ScriptPathFlags { get; set; }
        /// <summary>
        /// Gets or sets the path flags used for file operations.
        /// </summary>
        PathFlags FilePathFlags { get; set; }
        /// <summary>
        /// Gets or sets the path flags used for stream operations.
        /// </summary>
        PathFlags StreamPathFlags { get; set; }
        /// <summary>
        /// Gets or sets the path flags used for license operations.
        /// </summary>
        PathFlags LicensePathFlags { get; set; }
        /// <summary>
        /// Gets or sets the path flags used for key pair operations.
        /// </summary>
        PathFlags KeyPairPathFlags { get; set; }
        /// <summary>
        /// Gets or sets the path flags used for trace operations.
        /// </summary>
        PathFlags TracePathFlags { get; set; }
        /// <summary>
        /// Gets or sets the path flags used for other operations.
        /// </summary>
        PathFlags OtherPathFlags { get; set; }

        /// <summary>
        /// Gets or sets the network flags used for script operations.
        /// </summary>
        NetworkFlags ScriptNetworkFlags { get; set; }
        /// <summary>
        /// Gets or sets the network flags used for file operations.
        /// </summary>
        NetworkFlags FileNetworkFlags { get; set; }
        /// <summary>
        /// Gets or sets the network flags used for stream operations.
        /// </summary>
        NetworkFlags StreamNetworkFlags { get; set; }
        /// <summary>
        /// Gets or sets the network flags used for license operations.
        /// </summary>
        NetworkFlags LicenseNetworkFlags { get; set; }
        /// <summary>
        /// Gets or sets the network flags used for key pair operations.
        /// </summary>
        NetworkFlags KeyPairNetworkFlags { get; set; }
        /// <summary>
        /// Gets or sets the network flags used for trace operations.
        /// </summary>
        NetworkFlags TraceNetworkFlags { get; set; }
        /// <summary>
        /// Gets or sets the network flags used for other operations.
        /// </summary>
        NetworkFlags OtherNetworkFlags { get; set; }

        /// <summary>
        /// Gets or sets the renewal callback invoked for script operations.
        /// </summary>
        RenewCallback ScriptRenewCallback { get; set; }
        /// <summary>
        /// Gets or sets the renewal callback invoked for file operations.
        /// </summary>
        RenewCallback FileRenewCallback { get; set; }
        /// <summary>
        /// Gets or sets the renewal callback invoked for stream operations.
        /// </summary>
        RenewCallback StreamRenewCallback { get; set; }
        /// <summary>
        /// Gets or sets the renewal callback invoked for license operations.
        /// </summary>
        RenewCallback LicenseRenewCallback { get; set; }
        /// <summary>
        /// Gets or sets the renewal callback invoked for key pair operations.
        /// </summary>
        RenewCallback KeyPairRenewCallback { get; set; }
        /// <summary>
        /// Gets or sets the renewal callback invoked for trace operations.
        /// </summary>
        RenewCallback TraceRenewCallback { get; set; }
        /// <summary>
        /// Gets or sets the renewal callback invoked for other operations.
        /// </summary>
        RenewCallback OtherRenewCallback { get; set; }
    }
}
