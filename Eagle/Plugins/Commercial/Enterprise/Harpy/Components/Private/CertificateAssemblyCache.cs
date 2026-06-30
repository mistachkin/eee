/*
 * CertificateAssemblyCache.cs --
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

using AssemblyDictionary = System.Collections.Generic.Dictionary<
    string, System.Reflection.Assembly>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Caches reflection-only assemblies that have been loaded from raw
    /// bytes, keyed by a cryptographic hash of those bytes.  This is used to
    /// avoid repeated attempts to load the same assembly bytes into the
    /// current application domain.
    /// </summary>
    [ObjectId("38f5f837-09e1-44ec-a933-f673940f5dd7")]
    internal static class CertificateAssemblyCache
    {
        #region Private Data
        //
        // NOTE: This is used to synchronize access to the dictionary of
        //       pre-loaded assemblies (below).
        //
        /// <summary>
        /// The object used to synchronize access to the cache of pre-loaded
        /// assemblies.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Apparently, the .NET Framework cannot deal with repeated
        //       calls to load the same (bytes?) assembly into the current
        //       AppDomain; therefore, cache it, based on a cryptographic
        //       hash of the bytes.
        //
        /// <summary>
        /// The cache of reflection-only assemblies, keyed by a hexadecimal
        /// string representation of a cryptographic hash of the assembly
        /// bytes.
        /// </summary>
        private static readonly AssemblyDictionary reflectionOnly =
            new AssemblyDictionary();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Formats the specified cryptographic hash bytes as a hexadecimal
        /// string.
        /// </summary>
        /// <param name="hashBytes">
        /// The cryptographic hash bytes to format.
        /// </param>
        /// <returns>
        /// The hexadecimal string representation of
        /// <paramref name="hashBytes" />.
        /// </returns>
        private static string FormatHashString( /* CORE */
            byte[] hashBytes /* in */
            )
        {
            return CertificateDataOps.FormatHexadecimal(hashBytes);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to retrieve a previously cached reflection-only assembly
        /// that matches the specified hash bytes.
        /// </summary>
        /// <param name="hashBytes">
        /// The cryptographic hash bytes identifying the assembly to look up.
        /// </param>
        /// <param name="assembly">
        /// Upon return, receives the cached assembly, or null if no matching
        /// assembly was found.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the lookup was performed successfully; otherwise, false.
        /// </returns>
        private static bool MaybeGetForReflection( /* CORE */
            byte[] hashBytes,      /* in */
            out Assembly assembly, /* out */
            ref Result error       /* out */
            )
        {
            assembly = null;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (reflectionOnly == null)
                {
                    error = "invalid reflection assemblies";
                    return false;
                }

                string hashString = FormatHashString(hashBytes);

                if (hashString == null)
                {
                    error = "invalid hash string";
                    return false;
                }

                /* IGNORED */
                reflectionOnly.TryGetValue(hashString, out assembly);

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to add the specified assembly to the reflection-only
        /// cache, keyed by the specified hash bytes.
        /// </summary>
        /// <param name="hashBytes">
        /// The cryptographic hash bytes identifying the assembly to add.
        /// </param>
        /// <param name="assembly">
        /// The reflection-only assembly to add to the cache.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// True if the assembly was added successfully; otherwise, false.
        /// </returns>
        private static bool MaybeAddForReflection( /* CORE */
            byte[] hashBytes,  /* in */
            Assembly assembly, /* in */
            ref Result error   /* out */
            )
        {
            if (hashBytes == null)
            {
                error = "invalid hash bytes";
                return false;
            }

            if (assembly == null)
            {
                error = "invalid assembly";
                return false;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (reflectionOnly == null)
                {
                    error = "invalid reflection assemblies";
                    return false;
                }

                string hashString = FormatHashString(hashBytes);

                if (hashString == null)
                {
                    error = "invalid hash string";
                    return false;
                }

                if (reflectionOnly.ContainsKey(hashString))
                {
                    error = "reflection assembly already added";
                    return false;
                }

                /* NO RESULT */
                reflectionOnly.Add(hashString, assembly);

                return true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Loads the specified assembly bytes into the current application
        /// domain for reflection only, using the cache to avoid loading the
        /// same bytes more than once.
        /// </summary>
        /// <param name="bytes">
        /// The raw bytes of the assembly to load for reflection only.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The loaded reflection-only assembly, or null if it could not be
        /// loaded.
        /// </returns>
        public static Assembly ForReflectionOnly( /* CORE */
            byte[] bytes,    /* in */
            ref Result error /* out */
            )
        {
            Assembly assembly;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                byte[] hashBytes = null;

                if (CertificateSharedOps.HashBytes(
                        Constants.AssemblyHashAlgorithmName,
                        null, bytes, ref hashBytes,
                        ref error) != ReturnCode.Ok)
                {
                    return null;
                }

                if (!MaybeGetForReflection(
                        hashBytes, out assembly, ref error))
                {
                    return null;
                }

                if (assembly != null)
                    return assembly;

                try
                {
                    assembly = Assembly.ReflectionOnlyLoad(
                        bytes); /* throw */
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificateAssemblyCache).Name,
                        TracePriority.SecurityError);
#endif
                }

                if ((assembly != null) && !MaybeAddForReflection(
                        hashBytes, assembly, ref error))
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "ForReflectionOnly: error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(CertificateAssemblyCache).Name,
                        TracePriority.SecurityError);
#endif
                }

                return assembly;
            }
        }
        #endregion
    }
}
