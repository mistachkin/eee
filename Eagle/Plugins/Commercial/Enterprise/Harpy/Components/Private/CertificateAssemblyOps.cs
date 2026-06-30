/*
 * CertificateAssemblyOps.cs --
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
using System.IO;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using This = Licensing.Components.Private.CertificateAssemblyOps;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides static helper methods for querying and matching information
    /// about the executing Harpy assembly, including its name, version, file
    /// name, directory, public key token, and per-interpreter plugin
    /// reference counts.
    /// </summary>
    [ObjectId("6252a109-2c48-46e5-94f6-2f55741479a5")]
    internal static class CertificateAssemblyOps
    {
        #region Private Assembly Data Constants
        //
        // HACK: This fallback simple name will be used by certain methods in
        //       this class (e.g. "MustGetSimpleName") when the actual simple
        //       is invalid (for whatever reason).
        //
        /// <summary>
        /// Stores the fallback simple assembly name used when the actual
        /// simple name cannot be determined.
        /// </summary>
        private static readonly string FallbackSimpleName = "Harpy";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is always the assembly for Harpy -OR- null if it cannot
        //       be determined.  It is never anything else.  If this is null,
        //       various things may not work right.
        //
        /// <summary>
        /// Stores the executing Harpy assembly, or null if it cannot be
        /// determined.
        /// </summary>
        private static readonly Assembly @object =
            Assembly.GetExecutingAssembly();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is always the assembly name for Harpy -OR- null if it
        //       cannot be determined.  It is never anything else.  If this
        //       is null, various things may not work right.
        //
        /// <summary>
        /// Stores the assembly name of the Harpy assembly, or null if it
        /// cannot be determined.
        /// </summary>
        private static readonly AssemblyName name = (@object != null) ?
            @object.GetName() : null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is always the simple assembly name for Harpy -OR- null
        //       if it cannot be determined.  It is never anything else.  If
        //       this is null, various things may not work right.
        //
        /// <summary>
        /// Stores the simple assembly name of the Harpy assembly, or null if
        /// it cannot be determined.
        /// </summary>
        private static readonly string simpleName = (name != null) ?
            name.Name : null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is always the version of the Harpy assembly.
        //
        /// <summary>
        /// Stores the version of the Harpy assembly.
        /// </summary>
        private static readonly Version version = (name != null) ?
            name.Version : null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is always the fully qualified path and file name of
        //       the assembly.
        //
        /// <summary>
        /// Stores the fully qualified path and file name of the Harpy
        /// assembly.
        /// </summary>
        private static readonly string fileName = (@object != null) ?
            @object.Location : null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is always the directory containing the Harpy assembly
        //       -OR- null if it cannot be determined.  It is never anything
        //       else.  If this is null, various things may not work right.
        //
        /// <summary>
        /// Stores the directory containing the Harpy assembly, or null if it
        /// cannot be determined.
        /// </summary>
        private static readonly string directory = !String.IsNullOrEmpty(fileName) ?
            Path.GetDirectoryName(fileName) : null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is always the URI associated with the Harpy XML schema
        //       -OR- null if it cannot be determined.  It is never anything
        //       else.  If this is null, various things will not work right.
        //
        /// <summary>
        /// Stores the URI associated with the Harpy XML schema, or null if it
        /// cannot be determined.
        /// </summary>
        private static readonly Uri xmlSchemaUri = Utility.GetAssemblyXmlSchemaUri(
            @object);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is always the public key token of the assembly being
        //       executed (i.e. Harpy).
        //
        /// <summary>
        /// Stores the public key token of the executing Harpy assembly.
        /// </summary>
        private static readonly byte[] publicKeyToken = (name != null) ?
            name.GetPublicKeyToken() : null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
#if CERTIFICATE_PLUGIN
        //
        // NOTE: This field is used to synchronize access to the dictionary
        //       containing the per-interpreter plugin reference counts.
        //
        /// <summary>
        /// Stores the object used to synchronize access to the
        /// per-interpreter plugin reference count dictionary.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field keeps track of the number of outstanding plugin
        //       references to this assembly.  When a plugin is loaded from
        //       this assembly, this count is incremented.  When a plugin
        //       from this assembly is unloaded, this count is decremented.
        //       If/when this count is [eventually?] decremented to zero,
        //       various cleanup tasks may be performed.  Since this value
        //       is (by design) per-AppDomain, it must not use the process
        //       reference counting system used by other reference counts
        //       in this class (e.g. pending key ring count).
        //
        /// <summary>
        /// Stores the per-interpreter counts of outstanding plugin references
        /// to this assembly.
        /// </summary>
        private static InterpreterObjectDictionary referenceCounts;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Information Reading Methods
        /// <summary>
        /// Gets the executing Harpy assembly.
        /// </summary>
        /// <returns>
        /// The executing Harpy assembly, or null if it cannot be determined.
        /// </returns>
        public static Assembly GetObject() /* CORE */
        {
            return @object;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the build configuration of the Harpy assembly.
        /// </summary>
        /// <returns>
        /// The assembly configuration string, or null if it cannot be
        /// determined.
        /// </returns>
        public static string GetConfiguration() /* CORE */
        {
            return Utility.GetAssemblyConfiguration(@object);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the assembly name of the Harpy assembly.
        /// </summary>
        /// <returns>
        /// The assembly name of the Harpy assembly, or null if it cannot be
        /// determined.
        /// </returns>
        public static AssemblyName GetName() /* CORE */
        {
            return name;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the simple assembly name of the Harpy assembly.
        /// </summary>
        /// <returns>
        /// The simple assembly name of the Harpy assembly, or null if it
        /// cannot be determined.
        /// </returns>
        private static string GetSimpleName() /* CORE */
        {
            return simpleName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the fallback simple assembly name.
        /// </summary>
        /// <returns>
        /// The fallback simple assembly name.
        /// </returns>
        public static string GetFallbackSimpleName() /* CORE */
        {
            return FallbackSimpleName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the simple assembly name, falling back to the fallback simple
        /// name when the actual simple name is null or empty.
        /// </summary>
        /// <returns>
        /// The simple assembly name, or the fallback simple name if the
        /// simple name is null or empty.
        /// </returns>
        public static string MustGetSimpleName() /* CORE */
        {
            string simpleName = GetSimpleName();

            if (!String.IsNullOrEmpty(simpleName))
                return simpleName;

            return GetFallbackSimpleName();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the version of the Harpy assembly.
        /// </summary>
        /// <returns>
        /// The version of the Harpy assembly, or null if it cannot be
        /// determined.
        /// </returns>
        public static Version GetVersion() /* CORE */
        {
            return version;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the fully qualified path and file name of the Harpy assembly.
        /// </summary>
        /// <returns>
        /// The fully qualified path and file name of the Harpy assembly, or
        /// null if it cannot be determined.
        /// </returns>
        public static string GetFileName() /* CORE */
        {
            return fileName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the directory containing the Harpy assembly.
        /// </summary>
        /// <returns>
        /// The directory containing the Harpy assembly, or null if it cannot
        /// be determined.
        /// </returns>
        public static string GetDirectory() /* CORE */
        {
            return directory;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the URI associated with the Harpy XML schema.
        /// </summary>
        /// <returns>
        /// The URI associated with the Harpy XML schema, or null if it cannot
        /// be determined.
        /// </returns>
        public static Uri GetXmlSchemaUri() /* CORE */
        {
            return xmlSchemaUri;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the public key token of the Harpy assembly.
        /// </summary>
        /// <returns>
        /// The public key token of the Harpy assembly, or null if it cannot
        /// be determined.
        /// </returns>
        public static byte[] GetPublicKeyToken() /* CORE */
        {
            return publicKeyToken;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the public key token of the Harpy assembly formatted as a
        /// string.
        /// </summary>
        /// <returns>
        /// The formatted public key token of the Harpy assembly.
        /// </returns>
        public static string GetPublicKeyTokenString() /* CORE */
        {
            return CertificateDataOps.FormatPublicKeyToken(
                GetPublicKeyToken(), false, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the Harpy assembly to the specified list of assemblies if it
        /// is not already present.
        /// </summary>
        /// <param name="assemblies">
        /// The list of assemblies to which the Harpy assembly may be added.
        /// </param>
        /// <returns>
        /// True if the Harpy assembly was added to
        /// <paramref name="assemblies" />; otherwise, false.
        /// </returns>
        public static bool MaybeAddObject( /* CORE */
            IList<Assembly> assemblies
            )
        {
            if (assemblies == null)
                return false;

            if (@object == null)
                return false;

            if (assemblies.Contains(@object))
                return false;

            assemblies.Add(@object);
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the version of the specified assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly whose version is to be returned.
        /// </param>
        /// <returns>
        /// The version of <paramref name="assembly" />, or null if it cannot
        /// be determined.
        /// </returns>
        public static Version GetVersion( /* CORE */
            Assembly assembly /* in */
            )
        {
            if (assembly == null)
                return null;

            AssemblyName assemblyName = assembly.GetName();

            if (assemblyName == null)
                return null;

            return assemblyName.Version;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Reference Count Methods
#if CERTIFICATE_PLUGIN
        /// <summary>
        /// Gets the number of outstanding plugin references to this assembly
        /// for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose reference count is to be queried.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the reference, used for diagnostic
        /// purposes.
        /// </param>
        /// <param name="noComplain">
        /// When true, errors encountered while obtaining the reference count
        /// are not reported.
        /// </param>
        /// <returns>
        /// The current reference count, or zero if it could not be
        /// determined.
        /// </returns>
        public static int GetReferences(
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            bool noComplain          /* in */
            )
        {
            ReturnCode code;
            Result error = null;
            int referenceCount = 0;

            code = GetReferences(
                interpreter, pluginData, ref referenceCount, ref error);

            if (!noComplain && (code != ReturnCode.Ok))
                Utility.Complain(interpreter, code, error);

            return referenceCount;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the number of outstanding plugin references to this assembly
        /// for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose reference count is to be queried.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the reference, used for diagnostic
        /// purposes.
        /// </param>
        /// <param name="referenceCount">
        /// Upon return, receives the resulting reference count.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives a description of the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        private static ReturnCode GetReferences(
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            ref int referenceCount,  /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (referenceCounts == null)
                {
                    error = "reference counts unavailable";
                    return ReturnCode.Error;
                }

                object value;

                if (!referenceCounts.TryGetValue(interpreter, out value))
                {
                    error = "no reference count for interpreter";
                    return ReturnCode.Error;
                }

                if (!(value is int))
                {
                    error = "reference count is not an integer";
                    return ReturnCode.Error;
                }

                referenceCount = (int)value;
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Increments and returns the number of outstanding plugin references
        /// to this assembly for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose reference count is to be modified.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the reference, used for diagnostic
        /// purposes.
        /// </param>
        /// <returns>
        /// The resulting reference count, or zero if it could not be
        /// determined.
        /// </returns>
        public static int AddReference(
            Interpreter interpreter, /* in */
            IPluginData pluginData   /* in */
            )
        {
            ReturnCode code;
            Result error = null;
            int referenceCount = 0;

            code = AddReference(
                interpreter, pluginData, ref referenceCount, ref error);

            if (code != ReturnCode.Ok)
                Utility.Complain(interpreter, code, error);

            return referenceCount;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Increments the number of outstanding plugin references to this
        /// assembly for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose reference count is to be modified.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the reference, used for diagnostic
        /// purposes.
        /// </param>
        /// <param name="referenceCount">
        /// Upon return, receives the resulting reference count.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives a description of the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        private static ReturnCode AddReference(
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            ref int referenceCount,  /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (referenceCounts == null)
                    referenceCounts = new InterpreterObjectDictionary();

                object value;

                if (!referenceCounts.TryGetValue(interpreter, out value))
                {
                    referenceCount = 1;
                    referenceCounts.Add(interpreter, referenceCount);
                }
                else if (value is int)
                {
                    referenceCount = (int)value; referenceCount++;
                    referenceCounts[interpreter] = referenceCount;
                }
                else
                {
                    error = "reference count is not an integer";
                    return ReturnCode.Error;
                }

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "AddReference: {3}interpreter = {0}, " +
                    "pluginData = {1}, referenceCount = {2}",
                    CertificateDataOps.FormatInterpreter(
                        interpreter, true, false),
                    Utility.FormatWrapOrNull(pluginData),
                    referenceCount, referenceCount == 1 ? "INITIAL " :
                    String.Empty), typeof(CertificateAssemblyOps).Name,
                    (referenceCount == 1) ? TracePriority.MediumHigh :
                    TracePriority.MediumLow);
#endif

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Decrements and returns the number of outstanding plugin references
        /// to this assembly for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose reference count is to be modified.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the reference, used for diagnostic
        /// purposes.
        /// </param>
        /// <returns>
        /// The resulting reference count, or zero if it could not be
        /// determined.
        /// </returns>
        public static int RemoveReference(
            Interpreter interpreter, /* in */
            IPluginData pluginData   /* in */
            )
        {
            ReturnCode code;
            Result error = null;
            int referenceCount = 0;

            code = RemoveReference(
                interpreter, pluginData, ref referenceCount, ref error);

            if (code != ReturnCode.Ok)
                Utility.Complain(interpreter, code, error);

            return referenceCount;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Decrements the number of outstanding plugin references to this
        /// assembly for the specified interpreter, removing the entry when
        /// the count reaches zero.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose reference count is to be modified.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the reference, used for diagnostic
        /// purposes.
        /// </param>
        /// <param name="referenceCount">
        /// Upon return, receives the resulting reference count.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives a description of the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        private static ReturnCode RemoveReference(
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            ref int referenceCount,  /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (referenceCounts == null)
                {
                    error = "reference counts unavailable";
                    return ReturnCode.Error;
                }

                object value;

                if (!referenceCounts.TryGetValue(interpreter, out value))
                {
                    error = "no reference count";
                    return ReturnCode.Error;
                }

                if (!(value is int))
                {
                    error = "reference count is not an integer";
                    return ReturnCode.Error;
                }

                referenceCount = (int)value; referenceCount--;

#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "RemoveReference: {3}interpreter = {0}, " +
                    "pluginData = {1}, referenceCount = {2}",
                    CertificateDataOps.FormatInterpreter(
                        interpreter, true, false),
                    Utility.FormatWrapOrNull(pluginData),
                    referenceCount, referenceCount <= 0 ? "FINAL " :
                    String.Empty), typeof(CertificateAssemblyOps).Name,
                    (referenceCount <= 0) ? TracePriority.MediumHigh :
                    TracePriority.MediumLow);
#endif

                if (referenceCount <= 0)
                {
                    if (!referenceCounts.Remove(interpreter))
                    {
                        error = "could not remove reference count";
                        return ReturnCode.Error;
                    }

                    if (referenceCounts.Count == 0)
                        referenceCounts = null;
                }
                else
                {
                    referenceCounts[interpreter] = referenceCount;
                }

                return ReturnCode.Ok;
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Information Matching Methods
        /// <summary>
        /// Determines whether the assembly associated with the specified
        /// plugin data is the Harpy assembly.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose assembly is to be checked.
        /// </param>
        /// <returns>
        /// True if the plugin data refers to the Harpy assembly; otherwise,
        /// false.
        /// </returns>
        private static bool MatchObject( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            if (pluginData == null)
                return false;

            if (Utility.IsCrossAppDomain(pluginData))
                return false;

            return MatchObject(pluginData.Assembly);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified assembly is the Harpy assembly.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to be checked.
        /// </param>
        /// <returns>
        /// True if <paramref name="assembly" /> is the Harpy assembly;
        /// otherwise, false.
        /// </returns>
        public static bool MatchObject( /* CORE */
            Assembly assembly /* in */
            )
        {
            if (assembly == null)
                return false;

            return Object.ReferenceEquals(assembly, @object);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the assembly name associated with the specified
        /// plugin data matches the Harpy assembly name.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data whose assembly name is to be checked.
        /// </param>
        /// <returns>
        /// True if the plugin data assembly name matches the Harpy assembly
        /// name; otherwise, false.
        /// </returns>
        private static bool MatchName( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            if (pluginData == null)
                return false;

            return MatchName(pluginData.AssemblyName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified assembly name matches the Harpy
        /// assembly name and public key token.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name to be checked.
        /// </param>
        /// <returns>
        /// True if <paramref name="assemblyName" /> matches the Harpy
        /// assembly name and public key token; otherwise, false.
        /// </returns>
        public static bool MatchName( /* CORE */
            AssemblyName assemblyName /* in */
            )
        {
            if (assemblyName == null)
                return false;

            if (!Utility.IsSameAssemblyName(assemblyName, name))
                return false;

            if (!MatchPublicKeyToken(assemblyName))
                return false;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified plugin data matches the Harpy
        /// assembly by object reference or by assembly name.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin data to be checked.
        /// </param>
        /// <returns>
        /// True if the plugin data matches the Harpy assembly by object
        /// reference or by assembly name; otherwise, false.
        /// </returns>
        public static bool MatchObjectOrName( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            return MatchObject(pluginData) || MatchName(pluginData);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the public key token of the specified assembly
        /// name matches the public key token of the Harpy assembly.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name whose public key token is to be checked.
        /// </param>
        /// <returns>
        /// True if the public key token of <paramref name="assemblyName" />
        /// matches that of the Harpy assembly; otherwise, false.
        /// </returns>
        public static bool MatchPublicKeyToken( /* CORE */
            AssemblyName assemblyName /* in */
            )
        {
            if (assemblyName == null)
                return false;

            if (publicKeyToken == null)
                return false;

            if (CertificateDataOps.MatchPublicKeyToken(
                    assemblyName.GetPublicKeyToken(), publicKeyToken))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified file name refers to the same file
        /// as the Harpy assembly.
        /// </summary>
        /// <param name="fileName">
        /// The file name to be compared against the Harpy assembly file name.
        /// </param>
        /// <returns>
        /// True if <paramref name="fileName" /> refers to the same file as
        /// the Harpy assembly; otherwise, false.
        /// </returns>
        public static bool MatchFileName( /* CORE */
            string fileName /* in */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            if (String.IsNullOrEmpty(This.fileName))
                return false;

            return Utility.IsSameFile(fileName, This.fileName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified directory refers to the same
        /// directory as the Harpy assembly.
        /// </summary>
        /// <param name="directory">
        /// The directory to be compared against the Harpy assembly directory.
        /// </param>
        /// <returns>
        /// True if <paramref name="directory" /> refers to the same directory
        /// as the Harpy assembly; otherwise, false.
        /// </returns>
        public static bool MatchDirectory( /* CORE */
            string directory /* in */
            )
        {
            if (String.IsNullOrEmpty(directory))
                return false;

            if (String.IsNullOrEmpty(This.directory))
                return false;

            return Utility.IsSameFile(directory, This.directory);
        }
        #endregion
    }
}
