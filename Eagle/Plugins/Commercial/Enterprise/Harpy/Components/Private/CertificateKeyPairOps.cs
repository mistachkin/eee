/*
 * CertificateKeyPairOps.cs --
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
using System.Security.Cryptography;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;

#if !NET_STANDARD_20
using RSAProvider = System.Security.Cryptography.RSACryptoServiceProvider;
using DSAProvider = System.Security.Cryptography.DSACryptoServiceProvider;
#else
using RSAProvider = System.Security.Cryptography.RSA;
using DSAProvider = System.Security.Cryptography.DSA;
#endif

#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
using BigCrypto;
#endif

using Utility = Eagle._Components.Public.Utility;

using KeyPairDictionary =
    System.Collections.Generic.Dictionary<string,
        Licensing.Interfaces.Private.IKeyPair>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods for locating, loading, merging, and filtering
    /// the key pairs used by the licensing certificate subsystem, including
    /// those embedded within assemblies and those stored on key rings.
    /// </summary>
    [ObjectId("49387e1c-d80a-4900-8a20-1acf13b94872")]
    internal static class CertificateKeyPairOps
    {
        #region Core Support Methods
        /// <summary>
        /// Determines whether the public key token in the specified key pair
        /// metadata matches the public key token of this assembly.
        /// </summary>
        /// <param name="keyPairMetadata">
        /// The key pair metadata whose public key token is compared.
        /// </param>
        /// <returns>
        /// Non-zero if the public key tokens match; otherwise, zero.
        /// </returns>
        private static bool MatchPublicKeyToken( /* CORE */
            IKeyPairMetadata keyPairMetadata /* in */
            )
        {
            return MatchPublicKeyToken(
                keyPairMetadata, CertificateAssemblyOps.GetName());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the public key token in the specified key pair
        /// metadata matches the public key token of the given assembly name.
        /// </summary>
        /// <param name="keyPairMetadata">
        /// The key pair metadata whose public key token is compared.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name supplying the public key token to compare.
        /// </param>
        /// <returns>
        /// Non-zero if the public key tokens match; otherwise, zero.
        /// </returns>
        private static bool MatchPublicKeyToken( /* CORE */
            IKeyPairMetadata keyPairMetadata, /* in */
            AssemblyName assemblyName         /* in: EXEMPT */
            )
        {
            //
            // NOTE: *SECURITY* For reasons of security-in-depth,
            //       the key pair is forbidden from being be null
            //       in this method.
            //
            if ((keyPairMetadata == null) || (assemblyName == null))
                return false;

            return MatchPublicKeyToken(
                keyPairMetadata, assemblyName.GetPublicKeyToken());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the public key token in the specified key pair
        /// metadata matches the given public key token bytes.
        /// </summary>
        /// <param name="keyPairMetadata">
        /// The key pair metadata whose public key token is compared.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token bytes to compare against.
        /// </param>
        /// <returns>
        /// Non-zero if the public key tokens match; otherwise, zero.
        /// </returns>
        public static bool MatchPublicKeyToken( /* CORE */
            IKeyPairMetadata keyPairMetadata, /* in */
            byte[] publicKeyToken             /* in */
            )
        {
            //
            // NOTE: *SECURITY* For reasons of security-in-depth,
            //       the public key token is forbidden from being
            //       null in this method.
            //
            if ((keyPairMetadata == null) || (publicKeyToken == null))
                return false;

            return CertificateDataOps.MatchPublicKeyToken(
                keyPairMetadata.PublicKeyToken, publicKeyToken);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines which assembly should be used as the source of key pair
        /// identifier metadata, substituting this assembly when appropriate.
        /// </summary>
        /// <param name="assembly">
        /// On input, the candidate assembly; on output, the assembly that
        /// should be used as the metadata source.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose signing assembly is being considered.
        /// </param>
        /// <returns>
        /// Non-zero if this assembly was substituted; otherwise, zero.
        /// </returns>
        private static bool MatchAssembly( /* CORE */
            ref Assembly assembly,     /* in, out: EXEMPT */
            IKeyPair keyPair           /* in */
            )
        {
            if ((assembly == null) ||
                CertificateAssemblyOps.MatchObject(assembly) ||
                MatchPublicKeyToken(keyPair))
            {
                //
                // NOTE: Either we do not have a valid assembly (i.e. because
                //       we are [AppDomain] isolated away from it, etc) -OR-
                //       the specified key pair happens to match the one used
                //       to sign this assembly (i.e. Harpy).  Always use this
                //       assembly and the default metadata resource name for
                //       both of these cases.
                //
                assembly = CertificateAssemblyOps.GetObject();
                return true;
            }
            else
            {
                //
                // NOTE: The specified assembly is valid -AND- is not this
                //       assembly (i.e. Harpy) -AND- the specified key pair
                //       does not match the one used to sign this assembly
                //       (i.e. Harpy).  Therefore, use the specified assembly
                //       as the basis for fetching the embedded resource
                //       containing the identifier metadata for the key pair.
                //
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds an enumerable that yields the two specified key pair
        /// collections, optionally swapping their order.
        /// </summary>
        /// <param name="keyPairs1">
        /// The first key pair collection.
        /// </param>
        /// <param name="keyPairs2">
        /// The second key pair collection.
        /// </param>
        /// <param name="swap">
        /// Non-zero to yield the second collection before the first.
        /// </param>
        /// <returns>
        /// An enumerable yielding the two collections in the chosen order.
        /// </returns>
        private static IEnumerable<IEnumerable<IKeyPair>> MakeEnumerable( /* CORE */
            IEnumerable<IKeyPair> keyPairs1, /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs2, /* in: OPTIONAL */
            bool swap                        /* in */
            )
        {
            if (swap)
            {
                return new IEnumerable<IKeyPair>[] {
                    keyPairs2, keyPairs1
                };
            }
            else
            {
                return new IEnumerable<IKeyPair>[] {
                    keyPairs1, keyPairs2
                };
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified collection contains a key pair
        /// with the given public key token.
        /// </summary>
        /// <param name="keyPairs">
        /// The collection of key pairs to search.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token to look for.
        /// </param>
        /// <returns>
        /// Non-zero if a matching key pair is present; otherwise, zero.
        /// </returns>
        private static bool HavePublicKeyToken( /* CORE */
            IEnumerable<IKeyPair> keyPairs, /* in */
            byte[] publicKeyToken           /* in */
            )
        {
            return SharedOps.GetKeyPairByPublicKeyToken(
                keyPairs, publicKeyToken) != null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Combines the specified key pair collections and individual key
        /// pairs into a single list, honoring the duplicate, ordering, and
        /// swap options.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used for tracing, if any.
        /// </param>
        /// <param name="keyPairs1">
        /// The first key pair collection to merge.
        /// </param>
        /// <param name="keyPairs2">
        /// The second key pair collection to merge.
        /// </param>
        /// <param name="keyPair1">
        /// An additional key pair to include, if any.
        /// </param>
        /// <param name="keyPair2">
        /// An additional key pair to include, if any.
        /// </param>
        /// <param name="keyPair3">
        /// An additional key pair to include, if any.
        /// </param>
        /// <param name="keyPair4">
        /// An additional key pair to include, if any.
        /// </param>
        /// <param name="keyPair5">
        /// An additional key pair to include, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the merge operation.
        /// </param>
        /// <param name="priority">
        /// The trace priority to use, if any.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow duplicate public key tokens.
        /// </param>
        /// <param name="swapMany">
        /// Non-zero to swap the order of the two collections.
        /// </param>
        /// <param name="oneFirst">
        /// Non-zero to insert the individual key pairs at the front.
        /// </param>
        /// <returns>
        /// The merged list of key pairs, or null if none were supplied.
        /// </returns>
        public static IEnumerable<IKeyPair> MergeAll( /* CORE */
            Interpreter interpreter,         /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs1, /* in: OPTIONAL */
            IEnumerable<IKeyPair> keyPairs2, /* in: OPTIONAL */
            IKeyPair keyPair1,               /* in: OPTIONAL */
            IKeyPair keyPair2,               /* in: OPTIONAL */
            IKeyPair keyPair3,               /* in: OPTIONAL */
            IKeyPair keyPair4,               /* in: OPTIONAL */
            IKeyPair keyPair5,               /* in: OPTIONAL */
            PolicyType policyType,           /* in */
            TracePriority? priority,         /* in: OPTIONAL */
            bool allowDuplicate,             /* in */
            bool swapMany,                   /* in */
            bool oneFirst                    /* in */
            )
        {
            IList<IKeyPair> localKeyPairs = null;

            foreach (IEnumerable<IKeyPair> keyPairs0 in MakeEnumerable(
                    keyPairs1, keyPairs2, swapMany))
            {
                if (keyPairs0 == null)
                    continue;

                foreach (IKeyPair localKeyPair in keyPairs0)
                {
                    if (localKeyPair == null)
                        continue;

                    if (!allowDuplicate && HavePublicKeyToken(
                            localKeyPairs, localKeyPair.PublicKeyToken))
                    {
                        continue;
                    }

                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    localKeyPairs.Add(localKeyPair);
                }
            }

            if (keyPair1 != null)
            {
                if (allowDuplicate || oneFirst || !HavePublicKeyToken(
                        localKeyPairs, keyPair1.PublicKeyToken))
                {
                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    if (!allowDuplicate)
                    {
                        /* IGNORED */
                        localKeyPairs.Remove(keyPair1); /* O(N) */
                    }

                    if (oneFirst)
                        localKeyPairs.Insert(0, keyPair1); /* O(N) */
                    else
                        localKeyPairs.Add(keyPair1);
                }
            }

            if (keyPair2 != null)
            {
                if (allowDuplicate || oneFirst || !HavePublicKeyToken(
                        localKeyPairs, keyPair2.PublicKeyToken))
                {
                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    if (!allowDuplicate)
                    {
                        /* IGNORED */
                        localKeyPairs.Remove(keyPair2); /* O(N) */
                    }

                    if (oneFirst)
                        localKeyPairs.Insert(0, keyPair2); /* O(N) */
                    else
                        localKeyPairs.Add(keyPair2);
                }
            }

            if (keyPair3 != null)
            {
                if (allowDuplicate || oneFirst || !HavePublicKeyToken(
                        localKeyPairs, keyPair3.PublicKeyToken))
                {
                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    if (!allowDuplicate)
                    {
                        /* IGNORED */
                        localKeyPairs.Remove(keyPair3); /* O(N) */
                    }

                    if (oneFirst)
                        localKeyPairs.Insert(0, keyPair3); /* O(N) */
                    else
                        localKeyPairs.Add(keyPair3);
                }
            }

            if (keyPair4 != null)
            {
                if (allowDuplicate || oneFirst || !HavePublicKeyToken(
                        localKeyPairs, keyPair4.PublicKeyToken))
                {
                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    if (!allowDuplicate)
                    {
                        /* IGNORED */
                        localKeyPairs.Remove(keyPair4); /* O(N) */
                    }

                    if (oneFirst)
                        localKeyPairs.Insert(0, keyPair4); /* O(N) */
                    else
                        localKeyPairs.Add(keyPair4);
                }
            }

            if (keyPair5 != null)
            {
                if (allowDuplicate || oneFirst || !HavePublicKeyToken(
                        localKeyPairs, keyPair5.PublicKeyToken))
                {
                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    if (!allowDuplicate)
                    {
                        /* IGNORED */
                        localKeyPairs.Remove(keyPair5); /* O(N) */
                    }

                    if (oneFirst)
                        localKeyPairs.Insert(0, keyPair5); /* O(N) */
                    else
                        localKeyPairs.Add(keyPair5);
                }
            }

#if DEBUG || FORCE_TRACE
            TracePriority localPriority;

            if (priority != null)
                localPriority = (TracePriority)priority;
            else
                localPriority = TracePriority.MediumLow;

            CertificateTraceOps.DebugTrace(String.Format(
                "MergeAll: allowDuplicate = {0}, swapMany = {1}, " +
                "oneFirst = {2}", allowDuplicate, swapMany, oneFirst),
                typeof(CertificateKeyPairOps).Name, localPriority);

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            DebugOnlyOps.DumpKeyPairs(
                interpreter, "MergeAll", null,
                localKeyPairs, typeof(CertificateKeyPairOps).Name,
                policyType, localPriority);
#endif
#endif

            return localKeyPairs;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Locates a single key pair within the specified collection,
        /// optionally matching a public key token.
        /// </summary>
        /// <param name="keyPairs">
        /// The collection of key pairs to search.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token to match, or null to return the first.
        /// </param>
        /// <param name="allowNull">
        /// Non-zero to permit a null key pair to be considered a match.
        /// </param>
        /// <param name="suffix">
        /// Optional text appended to any error message produced.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the matching key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetOne( /* CORE */
            IEnumerable<IKeyPair> keyPairs, /* in */
            byte[] publicKeyToken,          /* in: OPTIONAL */
            bool allowNull,                 /* in */
            string suffix,                  /* in: OPTIONAL */
            ref IKeyPair keyPair,           /* out */
            ref Result error                /* out */
            )
        {
            if (keyPairs == null)
            {
                error = "invalid key pair list";
                return ReturnCode.Error;
            }

            foreach (IKeyPair localKeyPair in keyPairs)
            {
                if (!allowNull && (localKeyPair == null))
                    continue;

                if ((publicKeyToken == null) ||
                    MatchPublicKeyToken(localKeyPair, publicKeyToken))
                {
                    keyPair = localKeyPair;
                    return ReturnCode.Ok;
                }
            }

            if (publicKeyToken != null)
            {
                error = String.Format(
                    "key pair {0} not found{1}", Utility.FormatWrapOrNull(
                    CertificateDataOps.FormatPublicKeyToken(publicKeyToken,
                    true, true)), suffix).Trim();
            }
            else
            {
                error = String.Format(
                    "no key pairs found{0}", suffix).Trim();
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the named assembly resource and parses its contents as a Tcl
        /// list of strings.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded resource.
        /// </param>
        /// <param name="resourceName">
        /// The name of the embedded resource to read.
        /// </param>
        /// <param name="list">
        /// Upon success, receives the parsed list of strings.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetAssemblyList( /* CORE */
            Assembly assembly,   /* in: EXEMPT */
            string resourceName, /* in */
            ref StringList list, /* out */
            ref Result error     /* out */
            )
        {
            Stream stream = SharedOps.GetStream(
                assembly, resourceName, ref error);

            if (stream == null)
                return ReturnCode.Error;

            try
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    StringList localList = null;

                    if (Parser.SplitList(
                            null, streamReader.ReadToEnd(), 0,
                            Length.Invalid, true, ref localList,
                            ref error) == ReturnCode.Ok)
                    {
                        list = new StringList(localList);
                        return ReturnCode.Ok;
                    }
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses an identifier and key pair metadata from the specified
        /// string, each formatted as a Tcl dictionary.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="identifier">
        /// Upon success, receives the parsed identifier.
        /// </param>
        /// <param name="keyPairMetadataBase">
        /// Upon success, receives the parsed key pair metadata.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetIdentifierAndMetadata( /* CORE */
            string text,                                  /* in */
            ref IIdentifier identifier,                   /* out */
            ref IKeyPairMetadataBase keyPairMetadataBase, /* out */
            ref Result error                              /* out */
            )
        {
            //
            // NOTE: Start by attempting to parse the IIdentifier
            //       data from the string, which is formatted as a
            //       Tcl dictionary.
            //
            IIdentifier localIdentifier = null;

            //
            // HACK: The "offset" parameter value is hard-coded here.
            //       We know the IIdentifier list elements start at
            //       the first index.
            //
            if (CertificateDataOps.ParseIdentifier(
                    text, ref localIdentifier,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            //
            // NOTE: Next, attempt to parse the IKeyPairMetadataBase
            //       data from the string, which is also formatted as
            //       a Tcl dictionary.
            //
            IKeyPairMetadataBase localKeyPairMetadataBase = null;

            //
            // HACK: The "offset" parameter value is hard-coded here.
            //       We know the IKeyPairMetadataBase list elements
            //       start at the eleventh index.
            //
            if (CertificateDataOps.ParseKeyPairMetadata(
                    text, ref localKeyPairMetadataBase,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            identifier = localIdentifier;
            keyPairMetadataBase = localKeyPairMetadataBase;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the named assembly resource and parses an identifier and key
        /// pair metadata from it.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded resource.
        /// </param>
        /// <param name="resourceName">
        /// The name of the embedded resource to read.
        /// </param>
        /// <param name="identifier">
        /// Upon success, receives the parsed identifier.
        /// </param>
        /// <param name="keyPairMetadataBase">
        /// Upon success, receives the parsed key pair metadata.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetIdentifierAndMetadata( /* CORE */
            Assembly assembly,                           /* in: EXEMPT */
            string resourceName,                         /* in */
            ref IIdentifier identifier,                  /* out */
            ref IKeyPairMetadataBase keyPairMetadataBase /* out */
            )
        {
            Result error = null;

            return GetIdentifierAndMetadata(
                assembly, resourceName, ref identifier,
                ref keyPairMetadataBase, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the named assembly resource and parses an identifier and key
        /// pair metadata from it, reporting any error.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded resource.
        /// </param>
        /// <param name="resourceName">
        /// The name of the embedded resource to read.
        /// </param>
        /// <param name="identifier">
        /// Upon success, receives the parsed identifier.
        /// </param>
        /// <param name="keyPairMetadataBase">
        /// Upon success, receives the parsed key pair metadata.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetIdentifierAndMetadata( /* CORE */
            Assembly assembly,                            /* in: EXEMPT */
            string resourceName,                          /* in */
            ref IIdentifier identifier,                   /* out */
            ref IKeyPairMetadataBase keyPairMetadataBase, /* out */
            ref Result error                              /* out */
            )
        {
            Stream stream = SharedOps.GetStream(
                assembly, resourceName, ref error);

            if (stream == null)
                return ReturnCode.Error;

            try
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    //
                    // NOTE: Read all available string data from the stream,
                    //       which originated from the assembly resources.
                    //
                    return GetIdentifierAndMetadata(
                        streamReader.ReadToEnd(), ref identifier,
                        ref keyPairMetadataBase, ref error);
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified assembly key type is of the given
        /// base key type.
        /// </summary>
        /// <param name="keyType">
        /// The assembly key type to examine.
        /// </param>
        /// <param name="baseKeyType">
        /// The base assembly key type to test against.
        /// </param>
        /// <returns>
        /// Non-zero if the key type has the indicated base; otherwise, zero.
        /// </returns>
        private static bool IsOfBaseKeyType( /* CORE */
            AssemblyKeyType keyType,    /* in */
            AssemblyKeyType baseKeyType /* in */
            )
        {
            keyType &= AssemblyKeyType.BaseMask;
            return keyType == AssemblyKeyType.Signature;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Maps the specified assembly key type to the name of the embedded
        /// resource that contains its key.
        /// </summary>
        /// <param name="keyType">
        /// The assembly key type to map.
        /// </param>
        /// <param name="resourceName">
        /// Upon success, receives the resource name for the key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetKeyResourceName( /* CORE */
            AssemblyKeyType keyType, /* in */
            out string resourceName, /* out */
            ref Result error         /* out */
            )
        {
            switch (keyType & AssemblyKeyType.BaseMask)
            {
                case AssemblyKeyType.Signature:
                    {
                        resourceName = Constants.SignatureKeyName;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Assembly:
                    {
                        resourceName = Constants.AssemblyKeyName;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.License:
                    {
                        resourceName = Constants.LicenseKeyName;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Time:
                    {
                        resourceName = Constants.TimeKeyName;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Auxiliary:
                    {
                        resourceName = Constants.AuxiliaryKeyName;
                        return ReturnCode.Ok;
                    }
                default:
                    {
                        resourceName = null;

                        error = String.Format(
                            "unsupported assembly key type {0}",
                            Utility.FormatWrapOrNull(keyType));

                        return ReturnCode.Error;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Maps the specified assembly key type to the name of the embedded
        /// resource that contains its identifier metadata.
        /// </summary>
        /// <param name="keyType">
        /// The assembly key type to map.
        /// </param>
        /// <param name="resourceName">
        /// Upon success, receives the resource name for the metadata.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetMetadataResourceName( /* CORE */
            AssemblyKeyType keyType, /* in */
            out string resourceName, /* out */
            ref Result error         /* out */
            )
        {
            switch (keyType & AssemblyKeyType.BaseMask)
            {
                case AssemblyKeyType.Signature:
                    {
                        resourceName = Constants.SignatureKeyMetadata;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Assembly:
                    {
                        resourceName = Constants.AssemblyKeyMetadata;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.License:
                    {
                        resourceName = Constants.LicenseKeyMetadata;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Time:
                    {
                        resourceName = Constants.TimeKeyMetadata;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Auxiliary:
                    {
                        resourceName = Constants.AuxiliaryKeyMetadata;
                        return ReturnCode.Ok;
                    }
                default:
                    {
                        resourceName = null;

                        error = String.Format(
                            "unsupported assembly key type {0}",
                            Utility.FormatWrapOrNull(keyType));

                        return ReturnCode.Error;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Maps the specified assembly key type to its base key usage value.
        /// </summary>
        /// <param name="keyType">
        /// The assembly key type to map.
        /// </param>
        /// <param name="keyUsage">
        /// Upon success, receives the base key usage value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetKeyUsage( /* CORE */
            AssemblyKeyType keyType, /* in */
            out string keyUsage,     /* out */
            ref Result error         /* out */
            )
        {
            switch (keyType & AssemblyKeyType.BaseMask)
            {
                case AssemblyKeyType.Signature:
                    {
                        keyUsage = KeyUsage.Signature;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Assembly:
                    {
                        keyUsage = KeyUsage.Assembly;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.License:
                    {
                        keyUsage = KeyUsage.License;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Time:
                    {
                        keyUsage = KeyUsage.Time;
                        return ReturnCode.Ok;
                    }
                case AssemblyKeyType.Auxiliary:
                    {
                        keyUsage = KeyUsage.Auxiliary;
                        return ReturnCode.Ok;
                    }
                default:
                    {
                        keyUsage = null;

                        error = String.Format(
                            "unsupported assembly key type {0}",
                            Utility.FormatWrapOrNull(keyType));

                        return ReturnCode.Error;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines the assembly and candidate resource names from which
        /// key pair identifier metadata should be loaded for the specified
        /// key type.
        /// </summary>
        /// <param name="assembly">
        /// The candidate assembly to consider, if any.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name used to construct standardized resource names.
        /// </param>
        /// <param name="keyPair">
        /// The key pair whose signing assembly is being considered.
        /// </param>
        /// <param name="keyType">
        /// The assembly key type being located.
        /// </param>
        /// <param name="resourceNames">
        /// Receives the candidate metadata resource names to try.
        /// </param>
        /// <param name="identifierAssembly">
        /// Receives the assembly that should supply the metadata.
        /// </param>
        private static void LocateIdentifierAssemblyResource( /* CORE */
            Assembly assembly,              /* in: OPTIONAL */
            AssemblyName assemblyName,      /* in: EXEMPT, OPTIONAL */
            IKeyPair keyPair,               /* in: OPTIONAL */
            AssemblyKeyType keyType,        /* in */
            ref StringList resourceNames,   /* in, out */
            ref Assembly identifierAssembly /* out: EXEMPT */
            )
        {
            identifierAssembly = assembly;

            if (!MatchAssembly(ref identifierAssembly, keyPair))
            {
                //
                // NOTE: If the specified assembly name is valid -AND- it is
                //       not the name of this assembly (i.e. Harpy), then use
                //       it to create the appropriate "standardized" metadata
                //       resource name using a pre-defined format string that
                //       includes the assembly name and the key type (e.g.
                //       "SdkExample3.Late.Signature.Public.txt"); otherwise,
                //       we can fallback to (just?) using a default metadata
                //       resource name.
                //
                if ((assemblyName != null) &&
                    !CertificateAssemblyOps.MatchName(assemblyName) &&
                    (IsOfBaseKeyType(keyType, AssemblyKeyType.Signature) ||
                    IsOfBaseKeyType(keyType, AssemblyKeyType.Assembly)))
                {
                    if (resourceNames == null)
                        resourceNames = new StringList();

                    resourceNames.Add(String.Format(
                        Constants.IdentifierMetadataResourceNameFormat,
                        assemblyName.Name, keyType, FileExtension.Text));

                    //
                    // HACK: Include the "AssemblyPublic.txt" resource
                    //       name as well so that projects do not have
                    //       to construct an overly elaborate resource
                    //       name if they have no need of one.
                    //
                    resourceNames.Add(Constants.AssemblyKeyMetadata);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens a stream over the named embedded resource of the specified
        /// assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded resource.
        /// </param>
        /// <param name="resourceName">
        /// The name of the embedded resource to open.
        /// </param>
        /// <returns>
        /// A stream over the resource, or null if it could not be opened.
        /// </returns>
        private static Stream GetAssemblyResourceStream( /* CORE */
            Assembly assembly,  /* in: EXEMPT */
            string resourceName /* in */
            )
        {
            Result error = null;

            return GetAssemblyResourceStream(
                assembly, resourceName, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens a stream over the named embedded resource of the specified
        /// assembly, reporting any error.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded resource.
        /// </param>
        /// <param name="resourceName">
        /// The name of the embedded resource to open.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// A stream over the resource, or null if it could not be opened.
        /// </returns>
        private static Stream GetAssemblyResourceStream( /* CORE */
            Assembly assembly,   /* in: EXEMPT */
            string resourceName, /* in */
            ref Result error     /* out */
            )
        {
            return SharedOps.GetStream(
                assembly, resourceName, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the key pair of the specified type from the assembly (or its
        /// embedded resources), populating its identifier metadata and key
        /// usage.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="pvk">
        /// Non-zero if the key data is in PVK format.
        /// </param>
        /// <param name="password">
        /// The password protecting the key data, if any.
        /// </param>
        /// <param name="keyType">
        /// The assembly key type to load.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to load the public key.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to load the private key.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetAssembly( /* CORE */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            bool pvk,                  /* in */
            string password,           /* in: OPTIONAL */
            AssemblyKeyType keyType,   /* in */
            bool publicKey,            /* in */
            bool privateKey,           /* in */
            ref IKeyPair keyPair,      /* out */
            ref Result error           /* out */
            )
        {
            string keyResourceName;

            if (GetKeyResourceName(keyType,
                    out keyResourceName, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            string metadataResourceName;

            if (GetMetadataResourceName(keyType,
                    out metadataResourceName, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            string baseKeyUsage;

            if (GetKeyUsage(keyType,
                    out baseKeyUsage, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IKeyPair localKeyPair = null;
            Result result; /* REUSED */

            if (IsOfBaseKeyType(keyType, AssemblyKeyType.Signature))
            {
                result = null;

                if (KeyFile.Open(
                        assemblyName, KeyFile.GetReadCallback(
                            localKeyPair, KeyPairType.Assembly),
                        KeyFileFormat.Assembly, publicKey,
                        privateKey, ref localKeyPair,
                        ref result) != ReturnCode.Ok)
                {
                    error = result;
                    return ReturnCode.Error;
                }
            }
            else
            {
                using (Stream stream = GetAssemblyResourceStream(
                        assembly, keyResourceName))
                {
                    //
                    // HACK: Other key types are optional.  This is
                    //       not an error.
                    //
                    if (stream == null)
                        return ReturnCode.Ok;

                    result = null;

                    if (KeyFile.Open(
                            stream, KeyFile.GetReadCallback(
                                localKeyPair, KeyPairType.Assembly),
                            KeyFileFormat.Assembly, pvk, password,
                            publicKey, privateKey, ref localKeyPair,
                            ref result) != ReturnCode.Ok)
                    {
                        error = result;
                        return ReturnCode.Error;
                    }
                }
            }

            if (localKeyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            StringList resourceNames = new StringList();
            Assembly identifierAssembly = null;

            /* NO RESULT */
            LocateIdentifierAssemblyResource(
                assembly, assemblyName, localKeyPair, keyType,
                ref resourceNames, ref identifierAssembly);

            //
            // HACK: Fallback to the "hard-coded" resource name
            //       after all the other ones have been tried.
            //
            resourceNames.Add(metadataResourceName);

            IIdentifier identifier = null;
            IKeyPairMetadataBase keyPairMetadataBase = null;

            foreach (string resourceName in resourceNames)
            {
                //
                // HACK: Calls to GetIdentifierAndMetadata are
                //       allowed to "fail" without causing the
                //       entire method to be aborted because
                //       [most?] (third-party) assemblies may
                //       not embed the necessary text resource
                //       that contains the metadata for their
                //       signing key (and that is allowed).
                //
                if (GetIdentifierAndMetadata(identifierAssembly,
                        resourceName, ref identifier,
                        ref keyPairMetadataBase) == ReturnCode.Ok)
                {
                    /* IGNORED */
                    CertificateDataOps.CopyIdentifier(
                        identifier, localKeyPair as IIdentifier);

                    break;
                }
            }

            /* IGNORED */
            CertificateDataOps.MaybeSetAsKeyPair(
                localKeyPair as IIdentifierBase, keyResourceName);

            /* IGNORED */
            CertificateDataOps.CopyKeyPairMetadataBase(
                keyPairMetadataBase, localKeyPair);

            string keyUsage = localKeyPair.KeyUsage;

            if (keyUsage != null)
            {
                if (!SharedOps.ChangeKeyUsage(
                        keyUsage, baseKeyUsage,
                        ref keyUsage, ref error))
                {
                    return ReturnCode.Error;
                }

                localKeyPair.KeyUsage = keyUsage;
            }
            else
            {
                localKeyPair.KeyUsage = baseKeyUsage;
            }

            keyPair = localKeyPair;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        /// <summary>
        /// Loads the public-only assembly signing key pair for the specified
        /// assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded public-only key pair.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetAssemblyPublicOnly(
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref IKeyPair keyPair       /* out */
            )
        {
            Result error = null;

            return GetAssembly( /* OK */
                assembly, assemblyName, false, null,
                AssemblyKeyType.Signature, true, false,
                ref keyPair, ref error);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public-only assembly signing key pair for the specified
        /// assembly, reporting any error.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded public-only key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetAssemblyPublicOnly( /* CORE */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref IKeyPair keyPair,      /* out */
            ref Result error           /* out */
            )
        {
            return GetAssembly( /* OK */
                assembly, assemblyName, false, null,
                AssemblyKeyType.Signature, true, false,
                ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN
        /// <summary>
        /// Loads the public-only license signing key pair for the specified
        /// assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded public-only key pair.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetLicensePublicOnly(
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref IKeyPair keyPair       /* out */
            )
        {
            Result error = null;

            return GetAssembly( /* OK */
                assembly, assemblyName, false, null,
                AssemblyKeyType.License, true, false,
                ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public-only time signing key pair for the specified
        /// assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded public-only key pair.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetTimePublicOnly(
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref IKeyPair keyPair       /* out */
            )
        {
            Result error = null;

            return GetAssembly( /* OK */
                assembly, assemblyName, false, null,
                AssemblyKeyType.Time, true, false,
                ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public-only auxiliary signing key pair for the specified
        /// assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded public-only key pair.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetAuxiliaryPublicOnly(
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref IKeyPair keyPair       /* out */
            )
        {
            Result error = null;

            return GetAssembly( /* OK */
                assembly, assemblyName, false, null,
                AssemblyKeyType.Auxiliary, true, false,
                ref keyPair, ref error);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public-only license signing key pair for the specified
        /// assembly, reporting any error.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded public-only key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetLicensePublicOnly( /* CORE */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref IKeyPair keyPair,      /* out */
            ref Result error           /* out */
            )
        {
            return GetAssembly( /* OK */
                assembly, assemblyName, false, null,
                AssemblyKeyType.License, true, false,
                ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public-only time signing key pair for the specified
        /// assembly, reporting any error.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded public-only key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetTimePublicOnly( /* CORE */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref IKeyPair keyPair,      /* out */
            ref Result error           /* out */
            )
        {
            return GetAssembly( /* OK */
                assembly, assemblyName, false, null,
                AssemblyKeyType.Time, true, false,
                ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public-only auxiliary signing key pair for the specified
        /// assembly, reporting any error.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded public-only key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetAuxiliaryPublicOnly( /* CORE */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            ref IKeyPair keyPair,      /* out */
            ref Result error           /* out */
            )
        {
            return GetAssembly( /* OK */
                assembly, assemblyName, false, null,
                AssemblyKeyType.Auxiliary, true, false,
                ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public-only assembly, license, time, and auxiliary key
        /// pairs for the specified assembly as a collection.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the loaded public-only key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetAssemblyPublicOnly( /* CORE */
            Assembly assembly,                  /* in: OK */
            AssemblyName assemblyName,          /* in: OK */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out */
            )
        {
            IKeyPair localKeyPair1 = null;

            if (GetAssemblyPublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPair1,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IKeyPair localKeyPair2 = null;

            if (GetLicensePublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPair2,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IKeyPair localKeyPair3 = null;

            if (GetTimePublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPair3,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IKeyPair localKeyPair4 = null;

            if (GetAuxiliaryPublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPair4,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IList<IKeyPair> localKeyPairs = null;

            if (localKeyPair1 != null)
            {
                if (localKeyPairs == null)
                    localKeyPairs = new List<IKeyPair>();

                localKeyPairs.Add(localKeyPair1);
            }

            if (localKeyPair2 != null)
            {
                if (localKeyPairs == null)
                    localKeyPairs = new List<IKeyPair>();

                localKeyPairs.Add(localKeyPair2);
            }

            if (localKeyPair3 != null)
            {
                if (localKeyPairs == null)
                    localKeyPairs = new List<IKeyPair>();

                localKeyPairs.Add(localKeyPair3);
            }

            if (localKeyPair4 != null)
            {
                if (localKeyPairs == null)
                    localKeyPairs = new List<IKeyPair>();

                localKeyPairs.Add(localKeyPair4);
            }

            keyPairs = localKeyPairs;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Merges any available public-only key pairs into the specified
        /// collection, tracing on failure.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used for tracing, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the merge operation.
        /// </param>
        /// <param name="keyPairs">
        /// The key pair collection to merge into.
        /// </param>
        public static void MergeAnyPublicOnlyOrTrace( /* CORE */
            Interpreter interpreter,           /* in: OPTIONAL */
            PolicyType policyType,             /* in */
            ref IEnumerable<IKeyPair> keyPairs /* in, out */
            )
        {
            Result error = null;

            if (MergeAnyPublicOnly(
                    interpreter, policyType, ref keyPairs,
                    ref error) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "MergeAnyPublicOnlyOrTrace: " +
                    "interpreter = {0}, policyType = {1}, " +
                    "error = {2}",
                    CertificateDataOps.FormatInterpreter(
                        interpreter, true, false),
                    Utility.FormatWrapOrNull(policyType),
                    Utility.FormatWrapOrNull(error)),
                    typeof(CertificateKeyPairOps).Name,
                    TracePriority.Highest);
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Merges any available public-only key pairs into the specified
        /// collection.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used for tracing, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the merge operation.
        /// </param>
        /// <param name="keyPairs">
        /// The key pair collection to merge into.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode MergeAnyPublicOnly( /* CORE */
            Interpreter interpreter,            /* in: OPTIONAL */
            PolicyType policyType,              /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* in, out */
            ref Result error                    /* out */
            )
        {
            Assembly assembly = CertificateAssemblyOps.GetObject();
            AssemblyName assemblyName = CertificateAssemblyOps.GetName();
            IEnumerable<IKeyPair> localKeyPairs = null;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            if (GetAnyPublicOnly( /* OK */
                    null, policyType, false, assembly, assemblyName,
                    null, false, interpreter, EntityType.None, true,
                    true, true, false, false, ref localKeyPairs,
                    ref error) == ReturnCode.Ok)
#else
            if (GetAssemblyPublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPairs,
                    ref error) == ReturnCode.Ok)
#endif
            {
                keyPairs = MergeAll(
                    null, keyPairs, localKeyPairs, null, null,
                    null, null, null, policyType, null, false,
                    false, false);

                return ReturnCode.Ok;
            }
            else
            {
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the first non-null key pair in the specified collection.
        /// </summary>
        /// <param name="name">
        /// The name described in any error message produced.
        /// </param>
        /// <param name="keyPairs">
        /// The collection of key pairs to search.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the first matching key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetFirst( /* CORE */
            string name,                    /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            ref IKeyPair keyPair,           /* out: may NOT be NULL if Ok. */
            ref Result error                /* out */
            )
        {
            int count = 0;

            return GetFirst(
                name, keyPairs, ref count, ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the first non-null key pair in the specified collection,
        /// also reporting the number of key pairs examined.
        /// </summary>
        /// <param name="name">
        /// The name described in any error message produced.
        /// </param>
        /// <param name="keyPairs">
        /// The collection of key pairs to search.
        /// </param>
        /// <param name="count">
        /// Incremented by the number of key pairs in the collection.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the first matching key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetFirst( /* CORE */
            string name,                    /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            ref int count,                  /* in, out */
            ref IKeyPair keyPair,           /* out: may NOT be NULL if Ok. */
            ref Result error                /* out */
            )
        {
            if (keyPairs != null)
            {
                //
                // HACK: Attempt to determine how many key pairs
                //       are present in the provided collection.
                //
                ICollection<IKeyPair> localKeyPairs =
                    keyPairs as ICollection<IKeyPair>;

                if (localKeyPairs != null)
                    count += localKeyPairs.Count;

                //
                // HACK: Always return the first match.  Normally,
                //       there should be only one.
                //
                foreach (IKeyPair localKeyPair in keyPairs)
                {
                    if (localKeyPair == null)
                        continue;

                    keyPair = localKeyPair;
                    return ReturnCode.Ok;
                }
            }

            error = String.Format(
                "no key pair matching {0} was found",
                Utility.FormatWrapOrNull(name));

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Policy Support Methods
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Determines whether the name of the specified key pair matches the
        /// given key name.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose name is compared.
        /// </param>
        /// <param name="keyName">
        /// The key name to compare against.
        /// </param>
        /// <returns>
        /// Non-zero if the names match; otherwise, zero.
        /// </returns>
        public static bool MatchName( /* CORE? */
            IKeyPair keyPair, /* in */
            string keyName    /* in */
            )
        {
            if ((keyPair == null) || (keyName == null))
                return false;

            IIdentifierName identifierName =
                keyPair as IIdentifierName;

            if (identifierName == null)
                return false;

            return CertificateDataOps.StringEquals(
                keyName, identifierName.Name);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes and returns the first key pair whose name matches the
        /// given key name from the specified list.
        /// </summary>
        /// <param name="keyPairs">
        /// The list of key pairs to modify.
        /// </param>
        /// <param name="keyName">
        /// The key name to match.
        /// </param>
        /// <param name="suffix">
        /// Optional text appended to any error message produced.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the removed key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode RemoveByName( /* CORE? */
            IEnumerable<IKeyPair> keyPairs, /* in */
            string keyName,                 /* in: OPTIONAL */
            string suffix,                  /* in: OPTIONAL */
            ref IKeyPair keyPair,           /* out */
            ref Result error                /* out */
            )
        {
            if (keyPairs == null)
            {
                error = "invalid key pair list (1)";
                return ReturnCode.Error;
            }

            IList<IKeyPair> localKeyPairs =
                keyPairs as IList<IKeyPair>;

            if (localKeyPairs == null)
            {
                error = "invalid key pair list (2)";
                return ReturnCode.Error;
            }

            int count = localKeyPairs.Count;

            for (int index = count - 1; index >= 0; index--)
            {
                IKeyPair localKeyPair = localKeyPairs[index];

                if (localKeyPair == null)
                    continue;

                if (MatchName(localKeyPair, keyName))
                {
                    localKeyPairs.RemoveAt(index);
                    keyPair = localKeyPair;

                    return ReturnCode.Ok;
                }
            }

            error = String.Format(
                "could not remove key pair {0}{1}",
                Utility.FormatWrapOrNull(keyName),
                suffix).Trim();

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the first key pair from the specified collection.
        /// </summary>
        /// <param name="keyPairs">
        /// The collection of key pairs to search.
        /// </param>
        /// <param name="allowNull">
        /// Non-zero to permit a null key pair to be returned.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the first key pair found.
        /// </param>
        /// <returns>
        /// Non-zero if a key pair was found; otherwise, zero.
        /// </returns>
        private static bool GetOne( /* CORE? */
            IEnumerable<IKeyPair> keyPairs, /* in */
            bool allowNull,                 /* in */
            ref IKeyPair keyPair            /* out */
            )
        {
            if (keyPairs != null)
            {
                foreach (IKeyPair localKeyPair in keyPairs)
                {
                    if (!allowNull && (localKeyPair == null))
                        continue;

                    keyPair = localKeyPair;
                    return true;
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a single key pair from the named key ring, optionally
        /// matching a public key token.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring to search, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the key ring.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token to match, or null for any key pair.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the matching key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetRing( /* CORE? */
            Interpreter interpreter, /* in */
            string keyRingName,      /* in: OPTIONAL */
            PolicyType policyType,   /* in */
            byte[] publicKeyToken,   /* in: OPTIONAL */
            ref IKeyPair keyPair,    /* out */
            ref Result error         /* out */
            )
        {
            IKeyRing keyRing = null;

            if (CertificateKeyRingOps.GetKeyRing(
                    interpreter, keyRingName, policyType,
                    ref keyRing, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IEnumerable<IKeyPair> localKeyPairs = null;

            if (keyRing.ListByToken(
                    publicKeyToken, ref localKeyPairs,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (GetOne(localKeyPairs, false, ref keyPair))
                return ReturnCode.Ok;

            if (publicKeyToken != null)
            {
                error = String.Format(
                    "key pair {0} not found on {1} key ring",
                    Utility.FormatWrapOrNull(
                        CertificateDataOps.FormatPublicKeyToken(
                            publicKeyToken, true, true)),
                    Utility.FormatWrapOrNull(policyType));
            }
            else
            {
                error = String.Format(
                    "no key pairs found on {0} key ring",
                    Utility.FormatWrapOrNull(policyType));
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the subset of the specified key pairs whose key usage is
        /// valid for the given entity type.
        /// </summary>
        /// <param name="keyPairs">
        /// The collection of key pairs to filter.
        /// </param>
        /// <param name="entityType">
        /// The entity type whose key usage requirements are applied.
        /// </param>
        /// <returns>
        /// The filtered list of key pairs, or null if none qualify.
        /// </returns>
        public static IEnumerable<IKeyPair> Filter( /* CORE? */
            IEnumerable<IKeyPair> keyPairs, /* in */
            EntityType entityType           /* in */
            )
        {
            if (keyPairs == null)
                return null;

            IList<IKeyPair> localKeyPairs = null;

            foreach (IKeyPair keyPair in keyPairs)
            {
                if (keyPair == null)
                    continue;

                if (SharedOps.CheckKeyUsage(
                        keyPair, entityType) == ReturnCode.Ok)
                {
                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    localKeyPairs.Add(keyPair);
                }
            }

            return localKeyPairs;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key ring name is the built-in key
        /// ring name for the given policy type.
        /// </summary>
        /// <param name="keyRingName">
        /// The key ring name to test, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type whose built-in key ring name is compared.
        /// </param>
        /// <returns>
        /// Non-zero if the name is the built-in one; otherwise, zero.
        /// </returns>
        private static bool IsBuiltInRingName( /* CORE? */
            string keyRingName,   /* in: OPTIONAL */
            PolicyType policyType /* in */
            )
        {
            string localKeyRingName = CertificateKeyRingOps.GetName(
                null, policyType); /* EXEMPT */

            return CertificateDataOps.StringEquals(keyRingName,
                localKeyRingName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjusts the merge ordering options to prefer built-in key pairs
        /// when the specified key ring is the built-in one.
        /// </summary>
        /// <param name="keyRingName">
        /// The key ring name being considered, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the key ring.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero if a specific key ring name must be matched.
        /// </param>
        /// <param name="entityType">
        /// The entity type associated with the operation.
        /// </param>
        /// <param name="swapMany">
        /// Set to non-zero to swap the order of the collections.
        /// </param>
        /// <param name="oneFirst">
        /// Set to non-zero to place individual key pairs first.
        /// </param>
        private static void MaybePreferBuiltIn( /* CORE? */
            string keyRingName,    /* in: OPTIONAL */
            PolicyType policyType, /* in */
            bool matchKeyRingName, /* in */
            EntityType entityType, /* in: NOT USED */
            ref bool swapMany,     /* out */
            ref bool oneFirst      /* out */
            )
        {
            //
            // TODO: This handling may need more fine-tuning and/or more
            //       parameters form its callers.
            //
            if ((!matchKeyRingName && (keyRingName == null)) ||
                IsBuiltInRingName(keyRingName, policyType))
            {
                swapMany = true;

                if (policyType == PolicyType.License)
                    oneFirst = true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads all embedded key pairs from the specified assembly whose
        /// resource names match the given pattern.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pairs.
        /// </param>
        /// <param name="pattern">
        /// The pattern matched against embedded resource names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively.
        /// </param>
        /// <param name="pvk">
        /// Non-zero if the key data is in PVK format.
        /// </param>
        /// <param name="password">
        /// The password protecting the key data, if any.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to load the public key.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to load the private key.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the loaded key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetEmbedded( /* CORE? */
            Assembly assembly,                  /* in: OK */
            string pattern,                     /* in */
            bool noCase,                        /* in */
            bool pvk,                           /* in */
            string password,                    /* in: OPTIONAL */
            bool publicKey,                     /* in */
            bool privateKey,                    /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out */
            )
        {
            IEnumerable<string> resourceNames = SharedOps.GetEmbeddedNames(
                    assembly, pattern, noCase, true, ref error);

            if (resourceNames == null)
                return ReturnCode.Error;

            IList<IKeyPair> localKeyPairs = new List<IKeyPair>();

            foreach (string resourceName in resourceNames)
            {
                if (String.IsNullOrEmpty(resourceName))
                    continue;

                IKeyPair keyPair = null;

                if (GetEmbedded( /* OK */
                        assembly, resourceName, pvk, password,
                        publicKey, privateKey, ref keyPair,
                        ref error) == ReturnCode.Ok)
                {
                    localKeyPairs.Add(keyPair);
                }
                else
                {
                    return ReturnCode.Error;
                }
            }

            keyPairs = localKeyPairs;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads all public-only embedded key pairs from the specified
        /// assembly matching the given pattern.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pairs.
        /// </param>
        /// <param name="pattern">
        /// The pattern matched against embedded resource names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the loaded public-only key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetEmbeddedPublicOnly( /* CORE? */
            Assembly assembly,                  /* in: OK */
            string pattern,                     /* in */
            bool noCase,                        /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out */
            )
        {
            return GetEmbedded( /* OK */
                assembly, pattern, noCase, false, null, true, false,
                ref keyPairs, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Collects the public-only key pairs from the key ring, embedded
        /// resources, and/or assembly according to the specified options, and
        /// merges them.
        /// </summary>
        /// <param name="keyRingName">
        /// The name of the key ring to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the operation.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero if a specific key ring name must be matched.
        /// </param>
        /// <param name="assembly">
        /// The assembly from which to load key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPair">
        /// An additional key pair to include in the merge.
        /// </param>
        /// <param name="pattern">
        /// The pattern used to select key pairs by name.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="entityType">
        /// The entity type whose key usage requirements are applied.
        /// </param>
        /// <param name="useAssembly">
        /// Non-zero to include the assembly key pairs.
        /// </param>
        /// <param name="useEmbedded">
        /// Non-zero to include the embedded key pairs.
        /// </param>
        /// <param name="useRing">
        /// Non-zero to include the key ring key pairs.
        /// </param>
        /// <param name="matchKeyName">
        /// Non-zero to match key pairs by name using the pattern.
        /// </param>
        /// <param name="enforceKeyUsage">
        /// Non-zero to filter key pairs by their key usage.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the merged key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetPublicOnly( /* CORE? */
            string keyRingName,                 /* in: OPTIONAL */
            PolicyType policyType,              /* in */
            bool matchKeyRingName,              /* in */
            Assembly assembly,                  /* in: OK */
            AssemblyName assemblyName,          /* in: OK */
            IKeyPair keyPair,                   /* in */
            string pattern,                     /* in */
            bool noCase,                        /* in */
            Interpreter interpreter,            /* in */
            EntityType entityType,              /* in */
            bool useAssembly,                   /* in */
            bool useEmbedded,                   /* in */
            bool useRing,                       /* in */
            bool matchKeyName,                  /* in */
            bool enforceKeyUsage,               /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out */
            )
        {
            ReturnCode code;
            IEnumerable<IKeyPair> localKeyPairs1 = null;

            if (useRing && ((pattern != null) || !matchKeyName))
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    "Attempting to load and use key ring key pairs...",
                    typeof(CertificateKeyPairOps).Name,
                    TracePriority.Lower, 0);
#endif

                IEnumerable<IKeyPair> localKeyPairs2 = null;

                if (CertificateKeyRingOps.GetKeyPairs(
                        interpreter, keyRingName, policyType,
                        matchKeyName ? pattern : null, noCase,
                        ref localKeyPairs2, ref error) == ReturnCode.Ok)
                {
                    if (enforceKeyUsage)
                    {
                        localKeyPairs1 = Filter(
                            localKeyPairs2, entityType);
                    }
                    else
                    {
                        localKeyPairs1 = localKeyPairs2;
                    }
                }
                else
                {
                    code = ReturnCode.Error;
                    goto done;
                }
            }

            IEnumerable<IKeyPair> localKeyPairs3 = null;

            if (useEmbedded && ((pattern != null) || !matchKeyName))
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    "Attempting to load and use embedded key pairs...",
                    typeof(CertificateKeyPairOps).Name,
                    TracePriority.Lower, 0);
#endif

                IEnumerable<IKeyPair> localKeyPairs4 = null;

                if (GetEmbeddedPublicOnly( /* OK */
                        assembly, matchKeyName ? pattern : null, noCase,
                        ref localKeyPairs4, ref error) == ReturnCode.Ok)
                {
                    if (enforceKeyUsage)
                    {
                        localKeyPairs3 = Filter(
                            localKeyPairs4, entityType);
                    }
                    else
                    {
                        localKeyPairs3 = localKeyPairs4;
                    }
                }
                else
                {
                    code = ReturnCode.Error;
                    goto done;
                }
            }

            IKeyPair localKeyPair1 = null;
            IKeyPair localKeyPair2 = null;
            IKeyPair localKeyPair3 = null;
            IKeyPair localKeyPair4 = null;

            if (useAssembly && ((pattern == null) || !matchKeyName))
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.MaybeLogAndDebugTrace(
                    "Attempting to load and use assembly key pair...",
                    typeof(CertificateKeyPairOps).Name,
                    TracePriority.Lower, 0);
#endif

                IKeyPair localKeyPair5 = null;

                if (GetAssemblyPublicOnly( /* OK */
                        assembly, assemblyName, ref localKeyPair5,
                        ref error) == ReturnCode.Ok)
                {
                    if (enforceKeyUsage)
                    {
                        if (SharedOps.CheckKeyUsage(
                                localKeyPair5,
                                entityType) == ReturnCode.Ok)
                        {
                            localKeyPair1 = localKeyPair5;
                        }
                    }
                    else
                    {
                        localKeyPair1 = localKeyPair5;
                    }
                }
                else
                {
                    code = ReturnCode.Error;
                    goto done;
                }

                IKeyPair localKeyPair6 = null;

                if (GetLicensePublicOnly( /* OK */
                        assembly, assemblyName,
                        ref localKeyPair6) == ReturnCode.Ok)
                {
                    if (enforceKeyUsage)
                    {
                        if (SharedOps.CheckKeyUsage(
                                localKeyPair6,
                                entityType) == ReturnCode.Ok)
                        {
                            localKeyPair2 = localKeyPair6;
                        }
                    }
                    else
                    {
                        localKeyPair2 = localKeyPair6;
                    }
                }

                IKeyPair localKeyPair7 = null;

                if (GetTimePublicOnly( /* OK */
                        assembly, assemblyName,
                        ref localKeyPair7) == ReturnCode.Ok)
                {
                    if (enforceKeyUsage)
                    {
                        if (SharedOps.CheckKeyUsage(
                                localKeyPair7,
                                entityType) == ReturnCode.Ok)
                        {
                            localKeyPair3 = localKeyPair7;
                        }
                    }
                    else
                    {
                        localKeyPair3 = localKeyPair7;
                    }
                }

                IKeyPair localKeyPair8 = null;

                if (GetAuxiliaryPublicOnly( /* OK */
                        assembly, assemblyName,
                        ref localKeyPair8) == ReturnCode.Ok)
                {
                    if (enforceKeyUsage)
                    {
                        if (SharedOps.CheckKeyUsage(
                                localKeyPair8,
                                entityType) == ReturnCode.Ok)
                        {
                            localKeyPair4 = localKeyPair8;
                        }
                    }
                    else
                    {
                        localKeyPair4 = localKeyPair8;
                    }
                }
            }

            bool swapMany = false;
            bool oneFirst = false;

            MaybePreferBuiltIn(
                keyRingName, policyType, matchKeyRingName,
                entityType, ref swapMany, ref oneFirst);

            keyPairs = MergeAll(
                interpreter, localKeyPairs1, localKeyPairs3,
                localKeyPair1, localKeyPair2, localKeyPair3,
                localKeyPair4, keyPair, policyType, null,
                false, swapMany, oneFirst);

            code = ReturnCode.Ok;
            goto done; /* REDUNDANT */

        done:

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetPublicOnly: keyRingName = {0}, policyType = {1}, " +
                "matchKeyRingName = {2}, assembly = {3}, assemblyName = {4}, " +
                "keyPair = {5}, pattern = {6}, noCase = {7}, entityType = {8}, " +
                "useAssembly = {9}, useEmbedded = {10}, useRing = {11}, " +
                "matchKeyName = {12}, enforceKeyUsage = {13}, code = {14}, " +
                "error = {15}", Utility.FormatWrapOrNull(keyRingName),
                Utility.FormatWrapOrNull(policyType), matchKeyRingName,
                Utility.FormatWrapOrNull(assembly),
                Utility.FormatWrapOrNull(assemblyName),
                Utility.FormatWrapOrNull(keyPair),
                Utility.FormatWrapOrNull(pattern), noCase,
                Utility.FormatWrapOrNull(entityType), useAssembly,
                useEmbedded, useRing, matchKeyName, enforceKeyUsage,
                code, Utility.FormatWrapOrNull(true, false, error)),
                typeof(CertificateKeyPairOps).Name, TracePriority.MediumLow);

            DebugOnlyOps.DumpKeyPairs(
                interpreter, "GetPublicOnly", null,
                keyPairs, typeof(CertificateKeyPairOps).Name,
                policyType, TracePriority.MediumLow);
#endif

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Collects public-only key pairs from all available sources and
        /// merges them according to the specified options.
        /// </summary>
        /// <param name="keyRingName">
        /// The name of the key ring to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the operation.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero if a specific key ring name must be matched.
        /// </param>
        /// <param name="assembly">
        /// The assembly from which to load key pairs, if any.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded, if any.
        /// </param>
        /// <param name="pattern">
        /// The pattern used to select key pairs by name.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="entityType">
        /// The entity type whose key usage requirements are applied.
        /// </param>
        /// <param name="useAssembly">
        /// Non-zero to include the assembly key pairs.
        /// </param>
        /// <param name="useEmbedded">
        /// Non-zero to include the embedded key pairs.
        /// </param>
        /// <param name="useRing">
        /// Non-zero to include the key ring key pairs.
        /// </param>
        /// <param name="matchKeyName">
        /// Non-zero to match key pairs by name using the pattern.
        /// </param>
        /// <param name="enforceKeyUsage">
        /// Non-zero to filter key pairs by their key usage.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the merged key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetAnyPublicOnly(
            string keyRingName,                 /* in: OPTIONAL */
            PolicyType policyType,              /* in */
            bool matchKeyRingName,              /* in */
            Assembly assembly,                  /* in: OK, OPTIONAL */
            AssemblyName assemblyName,          /* in: OK, OPTIONAL */
            string pattern,                     /* in: OPTIONAL */
            bool noCase,                        /* in */
            Interpreter interpreter,            /* in */
            EntityType entityType,              /* in */
            bool useAssembly,                   /* in */
            bool useEmbedded,                   /* in */
            bool useRing,                       /* in */
            bool matchKeyName,                  /* in */
            bool enforceKeyUsage,               /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* in */
            ref Result error                    /* out */
            )
        {
            IEnumerable<IKeyPair> localKeyPairs1 = null;
            Result localError = null;
            ResultList errors = null;

            if (GetPublicOnly( /* OK */
                    keyRingName, policyType, matchKeyRingName, assembly,
                    assemblyName, null, pattern, noCase, interpreter,
                    entityType, useAssembly, useEmbedded, useRing,
                    matchKeyName, enforceKeyUsage, ref localKeyPairs1,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            IEnumerable<IKeyPair> localKeyPairs2 = null;

#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
            localError = null;

            if (GetKeyPairs( /* OK */
                    keyRingName, policyType, matchKeyRingName, assembly,
                    assemblyName, interpreter, pattern, false, false,
                    ref localKeyPairs2, ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }
#endif

            if (localKeyPairs1 != null)
            {
                if (localKeyPairs2 != null)
                {
                    bool swapMany = false;
                    bool oneFirst = false;

                    MaybePreferBuiltIn(
                        keyRingName, policyType, matchKeyRingName,
                        entityType, ref swapMany, ref oneFirst);

                    keyPairs = MergeAll(interpreter,
                        localKeyPairs1, localKeyPairs2, null,
                        null, null, null, null, policyType,
                        null, false, swapMany, oneFirst);

                    return ReturnCode.Ok;
                }
                else
                {
                    keyPairs = localKeyPairs1;
                    return ReturnCode.Ok;
                }
            }
            else if (localKeyPairs2 != null)
            {
                keyPairs = localKeyPairs2;
                return ReturnCode.Ok;
            }

            if (errors == null)
                errors = new ResultList();

            errors.Add("key pair not found");

            error = errors;
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends a human-readable description of each flag set in the
        /// specified key usage to the given list.
        /// </summary>
        /// <param name="keyUsage">
        /// The key usage value to describe.
        /// </param>
        /// <param name="key">
        /// The flag value identifying the key usage flag set being matched.
        /// </param>
        /// <param name="list">
        /// The list to which the descriptions are appended.
        /// </param>
        public static void KeyUsageToList(
            string keyUsage,        /* in */
            long key,               /* in */
            ref StringPairList list /* in, out */
            )
        {
            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.None, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "NULL AUTHORITY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.Any, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "ALL AUTHORITY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.License, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "LICENSE SIGNING");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.Script, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "SCRIPT SIGNING");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.String, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "STRING SIGNING");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.File, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "FILE SIGNING");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.Contract, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "CONTRACT SIGNING");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.List, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "LIST SIGNING");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.Time, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "TIME SIGNING");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.Secret, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "SECRET SIGNING");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.Root, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "ROOT AUTHORITY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.Delegation, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "DELEGATION AUTHORITY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.Intermediate, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "INTERMEDIATE AUTHORITY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.LocalFile, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "LOCAL FILE ALLOWED");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.InsecureUri, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "INSECURE URI ALLOWED");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.ExpireSignature, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "EXPIRE AGAINST SIGNATURE");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    null, KeyUsage.KeyRingOnly, false, false,
                    true) != ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "FOR KEY RING USE ONLY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    null, KeyUsage.DeveloperOnly, false, false,
                    true) != ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "FOR DEVELOPER USE ONLY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    null, KeyUsage.TestOnly, false, false,
                    true) != ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "FOR TEST USE ONLY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    null, KeyUsage.LimitedTimeOnly, false, false,
                    true) != ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "FOR LIMITED TIME ONLY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.RelaxedLimitedTimeOnly, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "IGNORE LIMITED TIME FOR RENEW");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.ConvertToLimitedTime, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "AUTOMATIC LIMITED TIME");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    null, KeyUsage.OnlineOnly, false, false,
                    true) != ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "FOR ONLINE USE ONLY");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.RelaxedOnlineOnly, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "RELAXED NETWORK REVOCATION");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.InheritOnly, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "INHERIT \"ONLY\" USAGE");
            }

            ///////////////////////////////////////////////////////////////////

            if (SharedOps.MatchFlags(
                    keyUsage, FlagType.KeyUsage, key,
                    KeyUsage.LicenseeOnly, null, true, false,
                    true) == ReturnCode.Ok)
            {
                if (list == null)
                    list = new StringPairList();

                list.Add(Constants.WithUsage, "FOR LICENSEE USE ONLY");
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the formatted public key token of this assembly's license
        /// signing key pair.
        /// </summary>
        /// <returns>
        /// The formatted public key token, or null if it is unavailable.
        /// </returns>
        private static string GetLicensePublicKeyToken()
        {
            IKeyPair keyPair = null;

            if (GetLicensePublicOnly( /* OK */
                    CertificateAssemblyOps.GetObject(),
                    CertificateAssemblyOps.GetName(),
                    ref keyPair) != ReturnCode.Ok)
            {
                return null;
            }

            if (keyPair == null)
                return null;

            return CertificateDataOps.FormatPublicKeyToken(
                keyPair.PublicKeyToken, false, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the formatted public key token of this assembly's time
        /// signing key pair.
        /// </summary>
        /// <returns>
        /// The formatted public key token, or null if it is unavailable.
        /// </returns>
        private static string GetTimePublicKeyToken()
        {
            IKeyPair keyPair = null;

            if (GetTimePublicOnly( /* OK */
                    CertificateAssemblyOps.GetObject(),
                    CertificateAssemblyOps.GetName(),
                    ref keyPair) != ReturnCode.Ok)
            {
                return null;
            }

            if (keyPair == null)
                return null;

            return CertificateDataOps.FormatPublicKeyToken(
                keyPair.PublicKeyToken, false, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the formatted public key token of this assembly's
        /// auxiliary signing key pair.
        /// </summary>
        /// <returns>
        /// The formatted public key token, or null if it is unavailable.
        /// </returns>
        private static string GetAuxiliaryPublicKeyToken()
        {
            IKeyPair keyPair = null;

            if (GetAuxiliaryPublicOnly( /* OK */
                    CertificateAssemblyOps.GetObject(),
                    CertificateAssemblyOps.GetName(),
                    ref keyPair) != ReturnCode.Ok)
            {
                return null;
            }

            if (keyPair == null)
                return null;

            return CertificateDataOps.FormatPublicKeyToken(
                keyPair.PublicKeyToken, false, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a list of the assembly, license, time, and auxiliary public
        /// key tokens for the specified assembly name.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name supplying the signing public key token.
        /// </param>
        /// <param name="pattern">
        /// A pattern used to select tokens (currently not used).
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively (not used).
        /// </param>
        /// <param name="list">
        /// Upon success, receives the formatted public key tokens.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ListAssemblyPublicKeyTokens(
            AssemblyName assemblyName, /* in: EXEMPT */
            string pattern,            /* in: NOT USED */
            bool noCase,               /* in: NOT USED */
            ref StringList list,       /* out */
            ref Result error           /* out */
            )
        {
            if (assemblyName == null)
            {
                error = "invalid assembly name";
                return ReturnCode.Error;
            }

            IStringList localList = null;
            string value; /* REUSED */

            value = Utility.GetAssemblyPublicKeyToken(assemblyName);

            if (value != null)
            {
                CertificateDataOps.AddKeyPairToList(
                    Constants.SignatureKeyName, value, ref localList);
            }

            value = GetLicensePublicKeyToken();

            if (value != null)
            {
                CertificateDataOps.AddKeyPairToList(
                    Constants.LicenseKeyName, value, ref localList);
            }

            value = GetTimePublicKeyToken();

            if (value != null)
            {
                CertificateDataOps.AddKeyPairToList(
                    Constants.TimeKeyName, value, ref localList);
            }

            value = GetAuxiliaryPublicKeyToken();

            if (value != null)
            {
                CertificateDataOps.AddKeyPairToList(
                    Constants.AuxiliaryKeyName, value, ref localList);
            }

            list = localList as StringList;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Lists the public key tokens of the embedded key pairs matching the
        /// given pattern, as raw byte arrays.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded key pairs.
        /// </param>
        /// <param name="pattern">
        /// The pattern matched against embedded resource names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively.
        /// </param>
        /// <param name="list">
        /// Upon success, receives the public key tokens as byte arrays.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode ListEmbeddedPublicKeyTokens(
            Assembly assembly,     /* in: EXEMPT */
            string pattern,        /* in */
            bool noCase,           /* in */
            ref IList<byte[]> list /* out */
            )
        {
            Result error = null;

            return ListEmbeddedPublicKeyTokens(
                assembly, pattern, noCase, ref list, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Lists the public key tokens of the embedded key pairs matching the
        /// given pattern, as raw byte arrays, reporting any error.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded key pairs.
        /// </param>
        /// <param name="pattern">
        /// The pattern matched against embedded resource names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively.
        /// </param>
        /// <param name="list">
        /// Upon success, receives the public key tokens as byte arrays.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode ListEmbeddedPublicKeyTokens(
            Assembly assembly,      /* in: EXEMPT */
            string pattern,         /* in */
            bool noCase,            /* in */
            ref IList<byte[]> list, /* out */
            ref Result error        /* out */
            )
        {
            IEnumerable<string> resourceNames = SharedOps.GetEmbeddedNames(
                    assembly, pattern, noCase, true, ref error);

            if (resourceNames == null)
                return ReturnCode.Error;

            IList<byte[]> localList = null;

            foreach (string resourceName in resourceNames)
            {
                if (String.IsNullOrEmpty(resourceName))
                    continue;

                IKeyPair keyPair = null;

                if (GetEmbedded( /* OK */
                        assembly, resourceName, true, false,
                        ref keyPair, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (keyPair == null)
                    continue;

                keyPair.AddToList(ref localList);
            }

            list = localList;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Lists the public key tokens of the embedded key pairs matching the
        /// given pattern, as formatted strings.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded key pairs.
        /// </param>
        /// <param name="pattern">
        /// The pattern matched against embedded resource names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively.
        /// </param>
        /// <param name="list">
        /// Upon success, receives the formatted public key tokens.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode ListEmbeddedPublicKeyTokens(
            Assembly assembly,   /* in: EXEMPT */
            string pattern,      /* in */
            bool noCase,         /* in */
            ref StringList list, /* out */
            ref Result error     /* out */
            )
        {
            IEnumerable<string> resourceNames = SharedOps.GetEmbeddedNames(
                    assembly, pattern, noCase, true, ref error);

            if (resourceNames == null)
                return ReturnCode.Error;

            IStringList localList = null;

            foreach (string resourceName in resourceNames)
            {
                if (String.IsNullOrEmpty(resourceName))
                    continue;

                IKeyPair keyPair = null;

                if (GetEmbedded( /* OK */
                        assembly, resourceName, true, false,
                        ref keyPair, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (keyPair == null)
                    continue;

                keyPair.AddToList(ref localList);
            }

            list = localList as StringList;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified key pair metadata matches one of
        /// the embedded trust root public key tokens.
        /// </summary>
        /// <param name="keyPairMetadata">
        /// The key pair metadata whose public key token is compared.
        /// </param>
        /// <returns>
        /// Non-zero if a trust root token matches; otherwise, zero.
        /// </returns>
        public static bool HasTrustRootPublicKeyToken(
            IKeyPairMetadata keyPairMetadata /* in */
            )
        {
            //
            // NOTE: *SECURITY* For reasons of security-in-depth,
            //       the public key token is forbidden from being
            //       null in this method.
            //
            if (keyPairMetadata == null)
                return false;

            //
            // NOTE: *SECURITY* For reasons of security-in-depth,
            //       the assembly is forbidden from being null in
            //       this method.
            //
            Assembly assembly = CertificateAssemblyOps.GetObject();

            if (assembly == null)
                return false;

            IList<byte[]> list = null;

            if (ListEmbeddedPublicKeyTokens(
                    assembly, Constants.TrustRootKeyPattern, false,
                    ref list) != ReturnCode.Ok)
            {
                return false;
            }

            foreach (byte[] element in list)
            {
                if (element == null)
                    continue;

                if (MatchPublicKeyToken(keyPairMetadata, element))
                    return true;
            }

            return false;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Command Support Methods
#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Generates an RSA key pair of the given size and writes it to the
        /// specified file.
        /// </summary>
        /// <param name="fileName">
        /// The path of the file to which the key pair is written.
        /// </param>
        /// <param name="keyNumber">
        /// The key number identifying the key to generate.
        /// </param>
        /// <param name="keySize">
        /// The size, in bits, of the key to generate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GenerateRsa(
            string fileName,     /* in */
            KeyNumber keyNumber, /* in */
            int keySize,         /* in */
            ref Result error     /* out */
            )
        {
            try
            {
                byte[] bytes = null;

                if (GenerateRsa(
                        keyNumber, keySize, ref bytes,
                        ref error) == ReturnCode.Ok)
                {
                    File.WriteAllBytes(fileName, bytes); /* throw */
                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // TODO: Research into making this work on .NET Core.  Perhaps by
        //       using an external native library via P/Invoke?
        //
        /// <summary>
        /// Generates an RSA key pair of the given size and returns its
        /// exported CSP blob.
        /// </summary>
        /// <param name="keyNumber">
        /// The key number identifying the key to generate.
        /// </param>
        /// <param name="keySize">
        /// The size, in bits, of the key to generate.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the exported CSP blob.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GenerateRsa(
            KeyNumber keyNumber, /* in */
            int keySize,         /* in */
            ref byte[] bytes,    /* out */
            ref Result error     /* out */
            )
        {
            try
            {
                CspParameters parameters = new CspParameters();

                //
                // TODO: Why would this be needed and why is it not
                //       being used?
                //
                // parameters.ProviderType = Constants.PROV_RSA_FULL;

                parameters.KeyNumber = (int)keyNumber;

                Result localError = null;

                using (RSA rsa = SharedOps.CreateRsaProvider(
                        keySize, parameters, ref localError))
                {
                    if (rsa != null)
                    {
#if NET_20 || NET_30 || NET_35 || NET_40 || NET_STANDARD_20 || NET_STANDARD_21
                        BigRSACryptoServiceProvider bigRsa =
                            rsa as BigRSACryptoServiceProvider;

                        if (bigRsa != null)
                        {
                            bytes = bigRsa.ExportCspBlob(true);
                            return ReturnCode.Ok;
                        }
#endif

#if !NET_STANDARD_20
                        RSAProvider provider = rsa as RSAProvider;

                        if (provider != null)
                        {
                            bytes = provider.ExportCspBlob(true);
                            return ReturnCode.Ok;
                        }
#endif

                        error = String.Format(
                            "RSA provider is not based on " +
                            "{0} -OR- its use is not enabled",
                            typeof(RSAProvider));

                        return ReturnCode.Error;
                    }
                    else if (localError != null)
                    {
                        error = localError;
                    }
                    else
                    {
                        error = String.Format(
                            "RSA provider is not based on {0}",
                            typeof(RSA));
                    }
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Generates a DSA key pair of the given size and writes it to the
        /// specified file.
        /// </summary>
        /// <param name="fileName">
        /// The path of the file to which the key pair is written.
        /// </param>
        /// <param name="keyNumber">
        /// The key number identifying the key to generate.
        /// </param>
        /// <param name="keySize">
        /// The size, in bits, of the key to generate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GenerateDsa(
            string fileName,     /* in */
            KeyNumber keyNumber, /* in */
            int keySize,         /* in */
            ref Result error     /* out */
            )
        {
            try
            {
                byte[] bytes = null;

                if (GenerateDsa(
                        keyNumber, keySize, ref bytes,
                        ref error) == ReturnCode.Ok)
                {
                    File.WriteAllBytes(fileName, bytes); /* throw */
                    return ReturnCode.Ok;
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // TODO: Research into making this work on .NET Core.  Perhaps by
        //       using an external native library via P/Invoke?
        //
        /// <summary>
        /// Generates a DSA key pair of the given size and returns its
        /// exported CSP blob.
        /// </summary>
        /// <param name="keyNumber">
        /// The key number identifying the key to generate.
        /// </param>
        /// <param name="keySize">
        /// The size, in bits, of the key to generate.
        /// </param>
        /// <param name="bytes">
        /// Upon success, receives the exported CSP blob.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GenerateDsa(
            KeyNumber keyNumber, /* in */
            int keySize,         /* in */
            ref byte[] bytes,    /* out */
            ref Result error     /* out */
            )
        {
#if !NET_STANDARD_20
            try
            {
                CspParameters parameters = new CspParameters();

                parameters.ProviderType = Constants.PROV_DSS_DH;
                parameters.KeyNumber = (int)keyNumber;

                Result localError = null;

                using (DSA dsa = SharedOps.CreateDsaProvider(
                        keySize, parameters, ref localError))
                {
                    if (dsa != null)
                    {
                        DSAProvider provider = dsa as DSAProvider;

                        if (provider != null)
                        {
                            bytes = provider.ExportCspBlob(true);
                            return ReturnCode.Ok;
                        }

                        error = String.Format(
                            "DSA provider is not based on " +
                            "{0} -OR- its use is not enabled",
                            typeof(DSAProvider));

                        return ReturnCode.Error;
                    }
                    else if (localError != null)
                    {
                        error = localError;
                    }
                    else
                    {
                        error = String.Format(
                            "DSA provider is not based on {0}",
                            typeof(DSA));
                    }
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
#else
            error = "not implemented";
            return ReturnCode.Error;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Generates a key pair of the specified type and writes it to the
        /// given file.
        /// </summary>
        /// <param name="keyPairType">
        /// The type of key pair to generate.
        /// </param>
        /// <param name="fileName">
        /// The path of the file to which the key pair is written.
        /// </param>
        /// <param name="keyNumber">
        /// The key number identifying the key to generate.
        /// </param>
        /// <param name="keySize">
        /// The size, in bits, of the key to generate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode Generate(
            KeyPairType? keyPairType, /* in */
            string fileName,          /* in */
            KeyNumber keyNumber,      /* in */
            int keySize,              /* in */
            ref Result error          /* out */
            )
        {
            if (keyPairType != null)
            {
                //
                // TODO: Update this switch if additional key pair types
                //       are added.
                //
                switch (KeyFile.GetBasePairType(keyPairType))
                {
                    case KeyPairType.RSA:
                        {
                            return GenerateRsa(
                                fileName, keyNumber, keySize, ref error);
                        }
                    case KeyPairType.DSA:
                        {
                            return GenerateDsa(
                                fileName, keyNumber, keySize, ref error);
                        }
                }
            }

            error = String.Format(
                "unsupported key pair type {0}",
                Utility.FormatWrapOrNull(keyPairType));

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads a key pair either from a named embedded resource or, when no
        /// key name is given, from the assembly's key of the specified type.
        /// </summary>
        /// <param name="assembly">
        /// The assembly from which to load the key pair.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyName">
        /// The embedded resource name to load, or null to use the key type.
        /// </param>
        /// <param name="pvk">
        /// Non-zero if the key data is in PVK format.
        /// </param>
        /// <param name="password">
        /// The password protecting the key data, if any.
        /// </param>
        /// <param name="keyType">
        /// The assembly key type to load when no key name is given.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to load the public key.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to load the private key.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetAssemblyOrEmbedded(
            Assembly assembly,         /* in: OK, OPTIONAL Without keyName only. */
            AssemblyName assemblyName, /* in: OK, OPTIONAL With keyName only. */
            string keyName,            /* in: OPTIONAL */
            bool pvk,                  /* in */
            string password,           /* in: OPTIONAL */
            AssemblyKeyType keyType,   /* in */
            bool publicKey,            /* in */
            bool privateKey,           /* in */
            ref IKeyPair keyPair,      /* out */
            ref Result error           /* out */
            )
        {
            if (keyName != null)
            {
                return GetEmbedded( /* OK */
                    assembly, keyName, pvk, password, publicKey,
                    privateKey, ref keyPair, ref error);
            }
            else
            {
                return GetAssembly( /* OK */
                    assembly, assemblyName, pvk, password, keyType,
                    publicKey, privateKey, ref keyPair, ref error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Policy Command Support Methods
#if CERTIFICATE_POLICY
        /// <summary>
        /// Returns the formatted public key tokens of the embedded trust root
        /// key pairs in the specified assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded trust root key pairs.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the formatted public key tokens; upon
        /// failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetRootPublicKeyToken(
            Assembly assembly, /* in: OK */
            ref Result result  /* out */
            )
        {
            IEnumerable<IKeyPair> keyPairs = null;

            if (GetEmbeddedPublicOnly( /* OK */
                    assembly, Constants.TrustRootKeyPattern, false,
                    ref keyPairs, ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (keyPairs != null)
            {
                StringList list = null;

                foreach (IKeyPair keyPair in keyPairs)
                {
                    if (keyPair == null)
                        continue;

                    if (list == null)
                        list = new StringList();

                    string value = CertificateDataOps.FormatPublicKeyToken(
                        keyPair.PublicKeyToken, false, false);

                    if (value == null)
                        continue;

                    list.Add(value);
                }

                if (list != null)
                {
                    result = list;
                    return ReturnCode.Ok;
                }
            }

            result = "root key pair not found";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the formatted public key token of the script signing key
        /// pair from the default bootstrap key ring.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to load the bootstrap key ring.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data supplying the bootstrap key ring file name.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when loading the key pairs, if any.
        /// </param>
        /// <param name="policy">
        /// The execution policy to enforce, if any.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the formatted public key token; upon
        /// failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetScriptPublicKeyToken(
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            ExecutionPolicy? policy, /* in: OPTIONAL */
            ref Result result        /* out */
            )
        {
            string fileName = CertificateKeyRingOps.GetBootstrapFileName(
                pluginData, BootstrapType.Script);

            if (String.IsNullOrEmpty(fileName))
            {
                result = "default bootstrap key ring file name is invalid";
                return ReturnCode.Error;
            }

            //
            // NOTE: *EXEMPT* This call to IsRemoteUri is fine, even without
            //       checking the execution policy, because this conditional
            //       is simply an optimization.  Real checks will be present
            //       in the LoadKeyPairs method.
            //
            if (!Utility.IsRemoteUri(fileName) && !File.Exists(fileName))
            {
                result = "default bootstrap key ring file not found";
                return ReturnCode.Error;
            }

            KeyPairDictionary keyPairs = null;

            if (CertificateKeyRingOps.LoadKeyPairs(
                    interpreter, policy, fileName, cultureInfo, false,
                    null, true, false, false, true, ref keyPairs,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            IKeyPair keyPair;

            if ((keyPairs == null) ||
                !keyPairs.TryGetValue(Constants.ScriptKeyName, out keyPair))
            {
                result = "script key pair not found";
                return ReturnCode.Error;
            }

            if (keyPair == null)
            {
                result = "invalid script key pair";
                return ReturnCode.Error;
            }

            result = CertificateDataOps.FormatPublicKeyToken(
                keyPair.PublicKeyToken, false, false);

            return ReturnCode.Ok;
        }
#endif
        #endregion
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Policy and/or Command Support Methods
#if CERTIFICATE_PLUGIN && (CERTIFICATE_POLICY || PLUGIN_COMMANDS)
        /// <summary>
        /// Returns the subset of the specified key pairs whose names match
        /// the given pattern, discarding duplicate public key tokens.
        /// </summary>
        /// <param name="keyPairs">
        /// The collection of key pairs to filter.
        /// </param>
        /// <param name="pattern">
        /// The pattern matched against key pair names, if any.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to match the pattern case-insensitively.
        /// </param>
        /// <param name="emptyIfNull">
        /// Non-zero to return an empty list rather than null.
        /// </param>
        /// <param name="error">
        /// Reserved for error reporting (currently not used).
        /// </param>
        /// <returns>
        /// The filtered list of key pairs.
        /// </returns>
        public static IEnumerable<IKeyPair> FilterByName( /* CORE? */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            string pattern,                 /* in: OPTIONAL */
            bool noCase,                    /* in */
            bool emptyIfNull,               /* in */
            ref Result error                /* out: NOT USED */
            )
        {
            IList<IKeyPair> localKeyPairs = null;

            if (keyPairs != null)
            {
                foreach (IKeyPair keyPair in keyPairs)
                {
                    if (keyPair == null)
                        continue;

                    IIdentifierName identifierName =
                        keyPair as IIdentifierName;

                    if (identifierName == null)
                        continue;

                    if ((pattern != null) && !Parser.StringMatch(
                            null, identifierName.Name, 0, pattern,
                            0, noCase))
                    {
                        //
                        // NOTE: This means the caller only wants
                        //       to add only the key pair(s) that
                        //       match the key pattern specified.
                        //
                        continue;
                    }

                    if (HavePublicKeyToken(
                            localKeyPairs, keyPair.PublicKeyToken))
                    {
                        //
                        // NOTE: This condition could be hit if
                        //       this key ring was loaded while
                        //       allowing duplicate public key
                        //       tokens.
                        //
                        continue;
                    }

                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    localKeyPairs.Add(keyPair);
                }
            }

            if (emptyIfNull && (localKeyPairs == null))
                localKeyPairs = new List<IKeyPair>();

            return localKeyPairs;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the subset of the specified key pairs matching the given
        /// public key token, discarding duplicate public key tokens.
        /// </summary>
        /// <param name="keyPairs">
        /// The collection of key pairs to filter.
        /// </param>
        /// <param name="publicKeyToken">
        /// The public key token to match, if any.
        /// </param>
        /// <param name="emptyIfNull">
        /// Non-zero to return an empty list rather than null.
        /// </param>
        /// <param name="error">
        /// Reserved for error reporting (currently not used).
        /// </param>
        /// <returns>
        /// The filtered list of key pairs.
        /// </returns>
        public static IEnumerable<IKeyPair> FilterByToken( /* CORE? */
            IEnumerable<IKeyPair> keyPairs, /* in: OPTIONAL */
            byte[] publicKeyToken,          /* in: OPTIONAL */
            bool emptyIfNull,               /* in */
            ref Result error                /* out: NOT USED */
            )
        {
            IList<IKeyPair> localKeyPairs = null;

            if (keyPairs != null)
            {
                foreach (IKeyPair keyPair in keyPairs)
                {
                    if (keyPair == null)
                        continue;

                    byte[] localPublicKeyToken = keyPair.PublicKeyToken;

                    if ((publicKeyToken != null) && !Utility.ArrayEquals(
                            localPublicKeyToken, publicKeyToken))
                    {
                        //
                        // NOTE: This means the caller only wants to add
                        //       only the key pair(s) that match the key
                        //       token specified.
                        //
                        continue;
                    }

                    if (HavePublicKeyToken(
                            localKeyPairs, localPublicKeyToken))
                    {
                        //
                        // NOTE: This condition could be hit if this key
                        //       ring was loaded while allowing duplicate
                        //       public key tokens.
                        //
                        continue;
                    }

                    if (localKeyPairs == null)
                        localKeyPairs = new List<IKeyPair>();

                    localKeyPairs.Add(keyPair);
                }
            }

            if (emptyIfNull && (localKeyPairs == null))
                localKeyPairs = new List<IKeyPair>();

            return localKeyPairs;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads and merges the built-in key pairs (embedded, assembly,
        /// license, time, and auxiliary) into the specified collection.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter context used for tracing, if any.
        /// </param>
        /// <param name="assembly">
        /// The assembly from which to load key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="keyPairs">
        /// The collection to merge into; also receives the merged result.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while loading key pairs.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetBuiltIn(
            Interpreter interpreter,            /* in: OPTIONAL */
            Assembly assembly,                  /* in: OK */
            AssemblyName assemblyName,          /* in: OK */
            ref IEnumerable<IKeyPair> keyPairs, /* in, out */
            ref ResultList errors               /* in, out */
            )
        {
            Result error; /* REUSED */
            IEnumerable<IKeyPair> keyPairs1 = keyPairs; /* ORIGINAL */
            IEnumerable<IKeyPair> keyPairs2 = null;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
            error = null;

            if (GetEmbeddedPublicOnly( /* OK */
                    assembly, null, false, ref keyPairs2,
                    ref error) != ReturnCode.Ok)
            {
                if (error != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(error);
                }
            }
#endif

            IKeyPair keyPair1 = null;

            error = null;

            if (GetAssemblyPublicOnly( /* OK */
                    assembly, assemblyName, ref keyPair1,
                    ref error) != ReturnCode.Ok)
            {
                if (error != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(error);
                }
            }

            IKeyPair keyPair2 = null;

            error = null;

            if (GetLicensePublicOnly( /* OK */
                    assembly, assemblyName, ref keyPair2,
                    ref error) != ReturnCode.Ok)
            {
                if (error != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(error);
                }
            }

            IKeyPair keyPair3 = null;

            error = null;

            if (GetTimePublicOnly( /* OK */
                    assembly, assemblyName, ref keyPair3,
                    ref error) != ReturnCode.Ok)
            {
                if (error != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(error);
                }
            }

            IKeyPair keyPair4 = null;

            error = null;

            if (GetAuxiliaryPublicOnly( /* OK */
                    assembly, assemblyName, ref keyPair4,
                    ref error) != ReturnCode.Ok)
            {
                if (error != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(error);
                }
            }

            if ((keyPairs1 == null) && (keyPairs2 == null) &&
                (keyPair1 == null) && (keyPair2 == null) &&
                (keyPair3 == null) && (keyPair4 == null))
            {
                return ReturnCode.Error;
            }

            keyPairs = MergeAll(
                interpreter, keyPairs1, keyPairs2, keyPair1, keyPair2,
                keyPair3, keyPair4, null, PolicyType.Unknown, null,
                false, false, false);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads a single embedded key pair by resource name from the
        /// specified assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded key pair.
        /// </param>
        /// <param name="keyName">
        /// The name of the embedded resource to load.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to load the public key.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to load the private key.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetEmbedded(
            Assembly assembly,    /* in: OK */
            string keyName,       /* in */
            bool publicKey,       /* in */
            bool privateKey,      /* in */
            ref IKeyPair keyPair, /* out */
            ref Result error      /* out */
            )
        {
            return GetEmbedded( /* OK */
                assembly, keyName, false, null, publicKey, privateKey,
                ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads a single embedded key pair by resource name from the
        /// specified assembly, populating its identifier metadata and key
        /// usage.
        /// </summary>
        /// <param name="assembly">
        /// The assembly containing the embedded key pair.
        /// </param>
        /// <param name="keyName">
        /// The name of the embedded resource to load.
        /// </param>
        /// <param name="pvk">
        /// Non-zero if the key data is in PVK format.
        /// </param>
        /// <param name="password">
        /// The password protecting the key data, if any.
        /// </param>
        /// <param name="publicKey">
        /// Non-zero to load the public key.
        /// </param>
        /// <param name="privateKey">
        /// Non-zero to load the private key.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the loaded key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetEmbedded(
            Assembly assembly,    /* in: OK */
            string keyName,       /* in */
            bool pvk,             /* in */
            string password,      /* in: OPTIONAL */
            bool publicKey,       /* in */
            bool privateKey,      /* in */
            ref IKeyPair keyPair, /* out */
            ref Result error      /* out */
            )
        {
            using (Stream stream = GetAssemblyResourceStream(
                    assembly, keyName, ref error))
            {
                if (stream != null)
                {
                    IKeyPair localKeyPair = null;
                    Result result = null;

                    if (KeyFile.Open(stream, KeyFile.GetReadCallback(
                                localKeyPair, KeyPairType.Embedded),
                            KeyFileFormat.Embedded, pvk, password,
                            publicKey, privateKey, ref localKeyPair,
                            ref result) == ReturnCode.Ok)
                    {
                        if (localKeyPair == null)
                        {
                            error = "invalid key pair";
                            return ReturnCode.Error;
                        }

                        localKeyPair.FileName = KeyFile.GetFileName(
                            assembly);

                        string resourceName = String.Format("{0}{1}",
                            Path.GetFileNameWithoutExtension(keyName),
                            FileExtension.Text);

                        IIdentifier identifier = null;
                        IKeyPairMetadataBase keyPairMetadataBase = null;

                        if (GetIdentifierAndMetadata(
                                assembly, resourceName, ref identifier,
                                ref keyPairMetadataBase,
                                ref error) == ReturnCode.Ok)
                        {
                            /* IGNORED */
                            CertificateDataOps.CopyIdentifier(
                                identifier, localKeyPair as IIdentifier);
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }

                        /* IGNORED */
                        CertificateDataOps.MaybeSetAsKeyPair(
                            localKeyPair as IIdentifierBase, keyName);

                        /* IGNORED */
                        CertificateDataOps.CopyKeyPairMetadataBase(
                            keyPairMetadataBase, localKeyPair);

                        string keyUsage = localKeyPair.KeyUsage;

                        if (keyUsage != null)
                        {
                            if (!SharedOps.ChangeKeyUsage(
                                    keyUsage, KeyUsage.Embedded,
                                    ref keyUsage, ref error))
                            {
                                return ReturnCode.Error;
                            }

                            localKeyPair.KeyUsage = keyUsage;
                        }
                        else
                        {
                            localKeyPair.KeyUsage = KeyUsage.Embedded;
                        }

                        keyPair = localKeyPair;
                        return ReturnCode.Ok;
                    }
                    else
                    {
                        error = result;
                    }
                }
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves the key pair identified by the given object name as a
        /// single-element collection.
        /// </summary>
        /// <param name="keyRingName">
        /// The name of the key ring to search, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the operation.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero if a specific key ring name must be matched.
        /// </param>
        /// <param name="assembly">
        /// The assembly from which to load key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter used to resolve the key pair.
        /// </param>
        /// <param name="objectName">
        /// The name identifying the key pair to retrieve.
        /// </param>
        /// <param name="allowObject">
        /// Non-zero to permit resolving an interpreter object.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to fail when no key pair is found.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the resolved key pair collection.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetKeyPairs(
            string keyRingName,                 /* in: OPTIONAL */
            PolicyType policyType,              /* in */
            bool matchKeyRingName,              /* in */
            Assembly assembly,                  /* in: OK */
            AssemblyName assemblyName,          /* in: OK */
            Interpreter interpreter,            /* in */
            string objectName,                  /* in */
            bool allowObject,                   /* in: OK */
            bool errorOnNotFound,               /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out */
            )
        {
            IKeyPair keyPair = null;
            Result localError = null;

            if (GetOne( /* OK */
                    keyRingName, policyType, matchKeyRingName,
                    assembly, assemblyName, interpreter,
                    objectName, allowObject, errorOnNotFound,
                    ref keyPair, ref localError) == ReturnCode.Ok)
            {
                IList<IKeyPair> localKeyPairs = new List<IKeyPair>();

                localKeyPairs.Add(keyPair);

                keyPairs = localKeyPairs;
                return ReturnCode.Ok;
            }

            if (errorOnNotFound)
            {
                error = localError;
                return ReturnCode.Error;
            }
            else
            {
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Currently, this method never "fails", even if there are no
        //       key pairs in the returned key ring.
        //
        /// <summary>
        /// Collects the key pairs from the named key ring together with the
        /// built-in key pairs for the specified assembly.
        /// </summary>
        /// <param name="keyRingName">
        /// The name of the key ring to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the operation.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero if a specific key ring name must be matched.
        /// </param>
        /// <param name="assembly">
        /// The assembly from which to load key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the merged key pairs.
        /// </param>
        /// <param name="errors">
        /// Receives any errors encountered while loading key pairs.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        private static ReturnCode GetRing(
            string keyRingName,                 /* in: OPTIONAL */
            PolicyType policyType,              /* in */
            bool matchKeyRingName,              /* in */
            Assembly assembly,                  /* in: OK */
            AssemblyName assemblyName,          /* in: OK */
            Interpreter interpreter,            /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* in */
            ref ResultList errors               /* out */
            )
        {
            IEnumerable<IKeyPair> localKeyPairs1 = null;
            Result localError; /* REUSED */

#if CERTIFICATE_POLICY
            localError = null;

            if (CertificateKeyRingOps.GetKeyPairs(
                    interpreter, keyRingName, policyType,
                    null, false, ref localKeyPairs1,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }
#endif

            IEnumerable<IKeyPair> localKeyPairs2 = null;

#if CERTIFICATE_POLICY
            localError = null;

            if (GetEmbeddedPublicOnly( /* OK */
                    assembly, null, false, ref localKeyPairs2,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }
#endif

            IKeyPair localKeyPair1 = null;

            localError = null;

            if (GetAssemblyPublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPair1,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            IKeyPair localKeyPair2 = null;

            localError = null;

            if (GetLicensePublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPair2,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            IKeyPair localKeyPair3 = null;

            localError = null;

            if (GetTimePublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPair3,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            IKeyPair localKeyPair4 = null;

            localError = null;

            if (GetAuxiliaryPublicOnly( /* OK */
                    assembly, assemblyName, ref localKeyPair4,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            bool swapMany = false;
            bool oneFirst = false;

#if CERTIFICATE_POLICY
            MaybePreferBuiltIn(
                keyRingName, policyType, matchKeyRingName,
                EntityType.None, ref swapMany, ref oneFirst);
#endif

            IEnumerable<IKeyPair> localKeyPairs3 = MergeAll(
                interpreter, localKeyPairs1, localKeyPairs2,
                localKeyPair1, localKeyPair2, localKeyPair3,
                localKeyPair4, null, policyType, null, false,
                swapMany, oneFirst);

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetRing: keyRingName = {0}, policyType = {1}, " +
                "matchKeyRingName = {2}, assembly = {3}, assemblyName = {4}",
                Utility.FormatWrapOrNull(keyRingName),
                Utility.FormatWrapOrNull(policyType), matchKeyRingName,
                Utility.FormatWrapOrNull(assembly),
                Utility.FormatWrapOrNull(assemblyName)),
                typeof(CertificateKeyPairOps).Name, TracePriority.MediumLow);

#if CERTIFICATE_POLICY
            DebugOnlyOps.DumpKeyPairs(
                interpreter, "GetRing", null,
                localKeyPairs3, typeof(CertificateKeyPairOps).Name,
                policyType, TracePriority.MediumLow);
#endif
#endif

            keyPairs = localKeyPairs3;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a single key pair matching the given name or public key
        /// token from the named key ring and the built-in key pairs.
        /// </summary>
        /// <param name="keyRingName">
        /// The name of the key ring to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the operation.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero if a specific key ring name must be matched.
        /// </param>
        /// <param name="assembly">
        /// The assembly from which to load key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring.
        /// </param>
        /// <param name="name">
        /// The key pair name or public key token to match.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to fail when no key pair is found.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the matching key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetRing(
            string keyRingName,        /* in: OPTIONAL */
            PolicyType policyType,     /* in */
            bool matchKeyRingName,     /* in */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            Interpreter interpreter,   /* in */
            string name,               /* in */
            bool errorOnNotFound,      /* in */
            ref IKeyPair keyPair,      /* out */
            ref Result error           /* out */
            )
        {
            IEnumerable<IKeyPair> localKeyPairs1 = null;
            ResultList errors = null;

            if (GetRing( /* OK */
                    keyRingName, policyType, matchKeyRingName,
                    assembly, assemblyName, interpreter,
                    ref localKeyPairs1, ref errors) != ReturnCode.Ok)
            {
                error = errors;
                return ReturnCode.Error;
            }

            if (localKeyPairs1 == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid key pair list");

                error = errors;
                return ReturnCode.Error;
            }

            if (name != null)
            {
                IEnumerable<IKeyPair> localKeyPairs2;
                Result localError = null;

                localKeyPairs2 = FilterByName(
                    localKeyPairs1, name, false, true,
                    ref localError);

                if (localKeyPairs2 != null)
                {
                    localError = null;

                    if (GetFirst(
                            name, localKeyPairs2, ref keyPair,
                            ref localError) == ReturnCode.Ok)
                    {
                        return ReturnCode.Ok;
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
            }

            if (name != null) /* REDUNDANT */
            {
                byte[] publicKeyToken = null;
                Result localError = null;

                if (CertificateDataOps.ParsePublicKeyToken(
                        name, ref publicKeyToken,
                        ref localError) == ReturnCode.Ok)
                {
                    IEnumerable<IKeyPair> localKeyPairs3;

                    localError = null;

                    localKeyPairs3 = FilterByToken(
                        localKeyPairs1, publicKeyToken, true,
                        ref localError);

                    if (localKeyPairs3 != null)
                    {
                        localError = null;

                        if (GetFirst(
                                name, localKeyPairs3, ref keyPair,
                                ref localError) == ReturnCode.Ok)
                        {
                            return ReturnCode.Ok;
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
                }
                else if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }
            }

            if ((errors == null) || (errors.Count == 0))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("key pair not found");
            }

            if (errorOnNotFound)
            {
                error = errors;
                return ReturnCode.Error;
            }
            else
            {
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a single key pair by name from the key ring, built-in
        /// sources, or interpreter object.
        /// </summary>
        /// <param name="keyRingName">
        /// The name of the key ring to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the operation.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero if a specific key ring name must be matched.
        /// </param>
        /// <param name="assembly">
        /// The assembly from which to load key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter used to resolve the key pair.
        /// </param>
        /// <param name="objectName">
        /// The name identifying the key pair to retrieve.
        /// </param>
        /// <param name="allowObject">
        /// Non-zero to permit resolving an interpreter object.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to fail when no key pair is found.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the matching key pair.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetOne(
            string keyRingName,        /* in: OPTIONAL */
            PolicyType policyType,     /* in */
            bool matchKeyRingName,     /* in */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            Interpreter interpreter,   /* in */
            string objectName,         /* in */
            bool allowObject,          /* in: OK */
            bool errorOnNotFound,      /* in */
            ref IKeyPair keyPair       /* out: may NOT be NULL if Ok. */
            )
        {
            Result error = null;

            return GetOne( /* OK */
                keyRingName, policyType, matchKeyRingName, assembly,
                assemblyName, interpreter, objectName, allowObject,
                errorOnNotFound, ref keyPair, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves a single key pair by name from the key ring, built-in
        /// sources, or interpreter object, reporting any error.
        /// </summary>
        /// <param name="keyRingName">
        /// The name of the key ring to use, if any.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with the operation.
        /// </param>
        /// <param name="matchKeyRingName">
        /// Non-zero if a specific key ring name must be matched.
        /// </param>
        /// <param name="assembly">
        /// The assembly from which to load key pairs.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the assembly being loaded.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter used to resolve the key pair.
        /// </param>
        /// <param name="objectName">
        /// The name identifying the key pair to retrieve.
        /// </param>
        /// <param name="allowObject">
        /// Non-zero to permit resolving an interpreter object.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to fail when no key pair is found.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the matching key pair.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// ReturnCode.Ok on success; otherwise, ReturnCode.Error.
        /// </returns>
        public static ReturnCode GetOne(
            string keyRingName,        /* in: OPTIONAL */
            PolicyType policyType,     /* in */
            bool matchKeyRingName,     /* in */
            Assembly assembly,         /* in: OK */
            AssemblyName assemblyName, /* in: OK */
            Interpreter interpreter,   /* in */
            string objectName,         /* in */
            bool allowObject,          /* in: OK */
            bool errorOnNotFound,      /* in */
            ref IKeyPair keyPair,      /* out: may NOT be NULL if Ok. */
            ref Result error           /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            ResultList errors = null;
            IKeyPair localKeyPair;
            Result localError; /* REUSED */

#if CERTIFICATE_POLICY
            localKeyPair = null;
            localError = null;

            if (GetRing( /* OK */
                    keyRingName, policyType, matchKeyRingName, assembly,
                    assemblyName, interpreter, objectName, errorOnNotFound,
                    ref localKeyPair, ref localError) == ReturnCode.Ok)
            {
                if (localKeyPair != null)
                {
                    keyPair = localKeyPair;
                    return ReturnCode.Ok;
                }
            }
            else if (localError != null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(localError);
            }
#endif

            //
            // AUDIT: This has been audited to make sure that key pair
            //        objects returned from this method cannot be used
            //        in contexts that require only "full-trusted" key
            //        pairs.
            //
            if (!allowObject)
            {
                error = errors;
                return ReturnCode.Error;
            }

            IObject @object = null;

            localError = null;

            if (interpreter.GetObject( /* AUDIT */
                    objectName, LookupFlags.Default, ref @object,
                    ref localError) != ReturnCode.Ok)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                error = errors;
                return ReturnCode.Error;
            }

            localKeyPair = (@object != null) ?
                @object.Value as IKeyPair : null;

            if (localKeyPair == null)
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add("invalid key pair");

                error = errors;
                return ReturnCode.Error;
            }

            keyPair = localKeyPair;
            return ReturnCode.Ok;
        }
#endif
        #endregion
    }
}
