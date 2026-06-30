/*
 * CertificatePathOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
using Helpers = Licensing.Components.Private.Commands.Helpers;
#endif

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods used to locate, name, and validate the file
    /// system paths and environment variables that may contain the license
    /// certificate files associated with an assembly or plugin.
    /// </summary>
    [ObjectId("168cffe4-afc7-4ac2-98d3-45b6bc823ebf")]
    internal static class CertificatePathOps
    {
        #region Environment Variable Checking Methods
        /// <summary>
        /// Checks the named environment variable for a certificate file name,
        /// adding any value found to <paramref name="fileNames" />.
        /// </summary>
        /// <param name="fileNames">
        /// The list to which any discovered file name is added.
        /// </param>
        /// <param name="envVarName">
        /// The name of the environment variable to check.
        /// </param>
        /// <param name="found">
        /// Set to true when a value was found and added to the list.
        /// </param>
        private static void CheckEnvVar( /* CORE */
            StringList fileNames, /* in */
            string envVarName,    /* in */
            ref bool found        /* out */
            )
        {
            CheckEnvVar(null, fileNames, envVarName, ref found);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the named environment variable, or its corresponding entry
        /// in <paramref name="dictionary" />, for a certificate file name and
        /// adds any value found to <paramref name="fileNames" />.
        /// </summary>
        /// <param name="dictionary">
        /// Optional dictionary consulted instead of the process environment;
        /// may be null.
        /// </param>
        /// <param name="fileNames">
        /// The list to which any discovered file name is added.
        /// </param>
        /// <param name="envVarName">
        /// The name of the environment variable to check.
        /// </param>
        private static void CheckEnvVar( /* CORE */
            StringDictionary dictionary, /* in, OPTIONAL: May be null. */
            StringList fileNames,        /* in */
            string envVarName            /* in */
            )
        {
            bool found = false;

            CheckEnvVar(dictionary, fileNames, envVarName, ref found);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the named environment variable, or its corresponding entry
        /// in <paramref name="dictionary" /> when one is supplied, for a
        /// certificate file name and adds any non-empty value found to
        /// <paramref name="fileNames" />.
        /// </summary>
        /// <param name="dictionary">
        /// Optional dictionary consulted instead of the process environment;
        /// may be null.
        /// </param>
        /// <param name="fileNames">
        /// The list to which any discovered file name is added.
        /// </param>
        /// <param name="envVarName">
        /// The name of the environment variable to check.
        /// </param>
        /// <param name="found">
        /// Set to true when a value was found and added to the list.
        /// </param>
        private static void CheckEnvVar( /* CORE */
            StringDictionary dictionary, /* in, OPTIONAL: May be null. */
            StringList fileNames,        /* in */
            string envVarName,           /* in */
            ref bool found               /* out */
            )
        {
            if ((fileNames == null) || String.IsNullOrEmpty(envVarName))
                return;

            string envVarValue;

            if (dictionary != null)
            {
                //
                // NOTE: This is "dictionary" mode, skip using the environment
                //       and try to grab the necessary value directly out of
                //       the dictionary itself.
                //
                if (!dictionary.TryGetValue(envVarName, out envVarValue))
                    return;
            }
            else
            {
                envVarValue = Configuration.GetVariable(envVarName);
            }

            if (String.IsNullOrEmpty(envVarValue))
                return;

            fileNames.Add(envVarValue);
            found = true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks an alternate form of the named environment variable, with
        /// any periods replaced by underscores, for a certificate file name.
        /// </summary>
        /// <param name="fileNames">
        /// The list to which any discovered file name is added.
        /// </param>
        /// <param name="envVarName">
        /// The original environment variable name from which the alternate
        /// name is derived.
        /// </param>
        /// <param name="found">
        /// Set to true when a value was found and added to the list.
        /// </param>
        private static void CheckAltEnvVar( /* CORE */
            StringList fileNames, /* in */
            string envVarName,    /* in */
            ref bool found        /* out */
            )
        {
            CheckAltEnvVar(null, fileNames, envVarName, ref found);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks an alternate form of the named environment variable, with
        /// any periods replaced by underscores, for a certificate file name,
        /// optionally consulting <paramref name="dictionary" /> instead of
        /// the process environment.
        /// </summary>
        /// <param name="dictionary">
        /// Optional dictionary consulted instead of the process environment;
        /// may be null.
        /// </param>
        /// <param name="fileNames">
        /// The list to which any discovered file name is added.
        /// </param>
        /// <param name="envVarName">
        /// The original environment variable name from which the alternate
        /// name is derived.
        /// </param>
        private static void CheckAltEnvVar( /* CORE */
            StringDictionary dictionary, /* in, OPTIONAL: May be null. */
            StringList fileNames,        /* in */
            string envVarName            /* in */
            )
        {
            bool found = false;

            CheckAltEnvVar(dictionary, fileNames, envVarName, ref found);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks an alternate form of the named environment variable, with
        /// any periods replaced by underscores, for a certificate file name,
        /// optionally consulting <paramref name="dictionary" /> instead of
        /// the process environment.
        /// </summary>
        /// <param name="dictionary">
        /// Optional dictionary consulted instead of the process environment;
        /// may be null.
        /// </param>
        /// <param name="fileNames">
        /// The list to which any discovered file name is added.
        /// </param>
        /// <param name="envVarName">
        /// The original environment variable name from which the alternate
        /// name is derived.
        /// </param>
        /// <param name="found">
        /// Set to true when a value was found and added to the list.
        /// </param>
        private static void CheckAltEnvVar( /* CORE */
            StringDictionary dictionary, /* in, OPTIONAL: May be null. */
            StringList fileNames,        /* in */
            string envVarName,           /* in */
            ref bool found               /* out */
            )
        {
            if (String.IsNullOrEmpty(envVarName))
                return;

            if (envVarName.IndexOf(Characters.Period) == Index.Invalid)
                return;

            string newEnvVarName = envVarName.Replace(
                Characters.Period, Characters.Underscore);

            CheckEnvVar(dictionary, fileNames, newEnvVarName, ref found);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Environment Variable Naming Methods
        /// <summary>
        /// Determines whether the specified assembly should be treated as the
        /// currently executing assembly, based on a matching public key
        /// token, returning the executing assembly name when it does.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name to test.
        /// </param>
        /// <param name="executingAssemblyName">
        /// Receives the name of the currently executing assembly when a match
        /// is found.
        /// </param>
        /// <returns>
        /// True if the assembly should be treated as the executing assembly;
        /// otherwise, false.
        /// </returns>
        private static bool ShouldTreatAsExecutingAssembly( /* CORE */
            AssemblyName assemblyName,             /* in */
            ref AssemblyName executingAssemblyName /* out */
            )
        {
            if (CertificateAssemblyOps.MatchPublicKeyToken(assemblyName))
            {
                executingAssemblyName = CertificateAssemblyOps.GetName();
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the name of an environment variable that may specify a
        /// certificate file, based on the supplied assembly name, plugin
        /// data, override preference, and naming flags.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly to incorporate into the name; may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin to incorporate into the name; may be null.
        /// </param>
        /// <param name="override">
        /// True to prepend the override prefix to the resulting name.
        /// </param>
        /// <param name="flags">
        /// Flags controlling how the plugin name portion is formatted.
        /// </param>
        /// <returns>
        /// The constructed environment variable name.
        /// </returns>
        private static string GetEnvVarName( /* CORE */
            AssemblyName assemblyName, /* in: OPTIONAL, May be null. */
            IPluginData pluginData,    /* in: OPTIONAL, May be null. */
            bool @override,            /* in: OPTIONAL, Use override prefix? */
            PluginNameFlags flags      /* in: OPTIONAL, Use full type name? */
            )
        {
            string result;

            if (assemblyName != null)
            {
                if (pluginData != null)
                {
                    result = String.Format(
                        Constants.AssemblyPackageEnvVarFormat, @override ?
                            Constants.OverrideEnvVarPrefix : String.Empty,
                        assemblyName.Name,
                        Characters.Underscore,
                        GetPluginName(pluginData,
                            flags | PluginNameFlags.ForEnvironment),
                        Characters.Underscore,
                        Constants.EnvVarSuffix);
                }
                else
                {
                    result = String.Format(
                        Constants.AssemblyEnvVarFormat, @override ?
                            Constants.OverrideEnvVarPrefix : String.Empty,
                        assemblyName.Name,
                        Characters.Underscore,
                        Constants.EnvVarSuffix);
                }
            }
            else if (pluginData != null)
            {
                result = String.Format(
                    Constants.PluginEnvVarFormat, @override ?
                        Constants.OverrideEnvVarPrefix : String.Empty,
                    GetPluginName(pluginData,
                        flags | PluginNameFlags.ForEnvironment),
                    Characters.Underscore,
                    Constants.EnvVarSuffix);
            }
            else if (HasFlags(flags, PluginNameFlags.Harpy, true))
            {
                result = String.Format(
                    Constants.AssemblyEnvVarFormat, @override ?
                        Constants.OverrideEnvVarPrefix : String.Empty,
                    Constants.HarpyAssemblySimpleName,
                    Characters.Underscore,
                    Constants.EnvVarSuffix);
            }
            else
            {
                result = String.Format(
                    Constants.EnvVarFormat, @override ?
                        Constants.OverrideEnvVarPrefix : String.Empty,
                    Constants.EnvVarSuffix);
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the complete set of override and non-override environment
        /// variable names that may refer to a certificate file for the given
        /// assembly and plugin, appending them to the supplied lists.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly used to construct the candidate names.
        /// </param>
        /// <param name="pluginData">
        /// The plugin used to construct the candidate names.
        /// </param>
        /// <param name="overrideEnvVarNames">
        /// The list to which the override environment variable names are
        /// added; created when null.
        /// </param>
        /// <param name="envVarNames">
        /// The list to which the normal environment variable names are added;
        /// created when null.
        /// </param>
        private static void GetEnvVarNames( /* CORE */
            AssemblyName assemblyName,          /* in */
            IPluginData pluginData,             /* in */
            ref StringList overrideEnvVarNames, /* in, out */
            ref StringList envVarNames          /* in, out */
            )
        {
            StringList localOverrideEnvVarNames = new StringList();
            StringList localEnvVarNames = new StringList();
            AssemblyName executingAssemblyName; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            foreach (PluginNameFlags flags in new PluginNameFlags[] {
                    PluginNameFlags.Pass1,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                    PluginNameFlags.Pass2,
#endif
                    PluginNameFlags.Pass3,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                    PluginNameFlags.Pass4,
#endif
                })
            {
                //
                // NOTE: Check all the associated override certificate
                //       environment variables *FIRST* because they override
                //       everything else.  These are checked in the same order
                //       as their non-override counterparts (below).
                //
                // NOTE: Check the override environment variable named
                //       "Override_<assembly>_<package>_Certificate" for the
                //       full path and file name of the certificate to use,
                //       where "<assembly>" is the base name of the assembly
                //       and "<package>" is the base name of the package
                //       within that assembly.
                //
                localOverrideEnvVarNames.Add(GetEnvVarName(
                    assemblyName, pluginData, true, flags));

                //
                // NOTE: If this assembly name shares the same public key
                //       token as the one currently executing, be sure to
                //       include those environment variables as well.
                //
                executingAssemblyName = null;

                if (ShouldTreatAsExecutingAssembly(
                        assemblyName, ref executingAssemblyName))
                {
                    localOverrideEnvVarNames.Add(GetEnvVarName(
                        executingAssemblyName, pluginData, true, flags));
                }

                //
                // NOTE: Also check the override environment variable named
                //       "<plugin>_Certificate" for the full path and file
                //       name of the certificate to use, where "<plugin>"
                //       is the base name of the plugin.
                //
                localOverrideEnvVarNames.Add(GetEnvVarName(
                    null, pluginData, true, flags));

                //
                // NOTE: Also check the environment variable named
                //       "<assembly>_<package>_Certificate" for the full
                //       path and file name of the certificate to use,
                //       where "<assembly>" is the base name of the
                //       assembly and "<package>" is the base name of the
                //       package within that assembly.
                //
                localEnvVarNames.Add(GetEnvVarName(
                    assemblyName, pluginData, false, flags));

                //
                // NOTE: If this assembly name shares the same public key
                //       token as the one currently executing, be sure to
                //       include those environment variables as well.
                //
                executingAssemblyName = null;

                if (ShouldTreatAsExecutingAssembly(
                        assemblyName, ref executingAssemblyName))
                {
                    localEnvVarNames.Add(GetEnvVarName(
                        executingAssemblyName, pluginData, false, flags));
                }

                //
                // NOTE: Also check the environment variable named
                //       "<plugin>_Certificate" for the full path and
                //       file name of the certificate to use, where
                //       "<plugin>" is the base name of the plugin.
                //
                localEnvVarNames.Add(GetEnvVarName(
                    null, pluginData, false, flags));
            }

            ///////////////////////////////////////////////////////////////////

            foreach (PluginNameFlags flags in new PluginNameFlags[] {
                    PluginNameFlags.Pass1,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                    PluginNameFlags.Pass2,
#endif
                    PluginNameFlags.Pass3,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                    PluginNameFlags.Pass4,
#endif
                })
            {
                //
                // NOTE: Also check the override environment variable named
                //       "<assembly>_Certificate" for the full path and
                //       file name of the certificate to use, where
                //       "<assembly>" is the base name of the assembly.
                //
                localOverrideEnvVarNames.Add(GetEnvVarName(
                    assemblyName, null, true, flags));

                //
                // NOTE: If this assembly name shares the same public key
                //       token as the one currently executing, be sure to
                //       include those environment variables as well.
                //
                executingAssemblyName = null;

                if (ShouldTreatAsExecutingAssembly(
                        assemblyName, ref executingAssemblyName))
                {
                    localOverrideEnvVarNames.Add(GetEnvVarName(
                        executingAssemblyName, null, true, flags));
                }

                //
                // NOTE: Also check the override environment variable named
                //       "<assembly>_Certificate" for the full path and
                //       file name of the certificate to use, where
                //       "<assembly>" is the base name of the Eagle core
                //       library assembly.
                //
                // TODO: 2022-12-17 Why was this put here?  Especially since
                //       any "Eagle" core library certificate specified here
                //       would likely be signed with a different public key,
                //       e.g. 29c6297630be05eb, etc.  This comes up because
                //       of work on creating a unified combination of Eagle,
                //       Harpy, and Badge in one managed assembly, signed by
                //       the Harpy public key (ironically).  It must use the
                //       Harpy public key due to limitations of its built-in
                //       plugin license verification implementation.
                //
                localOverrideEnvVarNames.Add(GetEnvVarName(
                    Utility.GetPackageAssemblyName(), null, true,
                    flags));

                //
                // NOTE: Also check the override environment variable named
                //       "Certificate" for the full path and file name of
                //       the certificate to use.
                //
                if (CertificateAssemblyOps.MatchName(assemblyName))
                {
                    localOverrideEnvVarNames.Add(GetEnvVarName(
                        null, null, true, flags | PluginNameFlags.Harpy));
                }

                localOverrideEnvVarNames.Add(GetEnvVarName(
                    null, null, true, flags));

                //
                // NOTE: Also check the environment variable named
                //       "<assembly>_Certificate" for the full path and
                //       file name of the certificate to use, where
                //       "<assembly>" is the base name of the assembly.
                //
                localEnvVarNames.Add(GetEnvVarName(
                    assemblyName, null, false, flags));

                //
                // NOTE: If this assembly name shares the same public key
                //       token as the one currently executing, be sure to
                //       include those environment variables as well.
                //
                executingAssemblyName = null;

                if (ShouldTreatAsExecutingAssembly(
                        assemblyName, ref executingAssemblyName))
                {
                    localEnvVarNames.Add(GetEnvVarName(
                        executingAssemblyName, null, false, flags));
                }

                //
                // NOTE: Also check the environment variable named
                //       "<assembly>_Certificate" for the full path and
                //       file name of the certificate to use, where
                //       "<assembly>" is the base name of the Eagle core
                //       library assembly.
                //
                localEnvVarNames.Add(GetEnvVarName(
                    Utility.GetPackageAssemblyName(), null, false,
                    flags));

                //
                // NOTE: Also check the environment variable named
                //       "Certificate" for the full path and file name
                //       of the certificate to use.
                //
                if (CertificateAssemblyOps.MatchName(assemblyName))
                {
                    localEnvVarNames.Add(GetEnvVarName(
                        null, null, false, flags | PluginNameFlags.Harpy));
                }

                localEnvVarNames.Add(GetEnvVarName(
                    null, null, false, flags));
            }

            ///////////////////////////////////////////////////////////////////

            localOverrideEnvVarNames = Utility.GetUniqueElements(
                localOverrideEnvVarNames);

            localEnvVarNames = Utility.GetUniqueElements(
                localEnvVarNames);

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetEnvVarNames: localOverrideEnvVarNames = {0}",
                Utility.FormatWrapOrNull(localOverrideEnvVarNames)),
                typeof(CertificatePathOps).Name, TracePriority.Lower);

            CertificateTraceOps.DebugTrace(String.Format(
                "GetEnvVarNames: localEnvVarNames = {0}",
                Utility.FormatWrapOrNull(localEnvVarNames)),
                typeof(CertificatePathOps).Name, TracePriority.Lower);
#endif

            ///////////////////////////////////////////////////////////////////

            if (overrideEnvVarNames == null)
                overrideEnvVarNames = new StringList();

            if (envVarNames == null)
                envVarNames = new StringList();

            overrideEnvVarNames.AddRange(localOverrideEnvVarNames);
            envVarNames.AddRange(localEnvVarNames);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Simple File Extension Methods
        /// <summary>
        /// Determines whether the encrypted certificate file extension should
        /// be used for the specified assembly name flags.
        /// </summary>
        /// <param name="flags">
        /// The assembly name flags to examine.
        /// </param>
        /// <returns>
        /// True if the encrypted file extension should be used; otherwise,
        /// false.
        /// </returns>
        private static bool UseEncryptedFileExtension( /* CORE */
            AssemblyNameFlags flags /* in */
            )
        {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            return HasFlags(flags, AssemblyNameFlags.Encrypted, true);
#else
            return false;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the encrypted certificate file extension should
        /// be used for the specified plugin name flags.
        /// </summary>
        /// <param name="flags">
        /// The plugin name flags to examine.
        /// </param>
        /// <returns>
        /// True if the encrypted file extension should be used; otherwise,
        /// false.
        /// </returns>
        private static bool UseEncryptedFileExtension( /* CORE */
            PluginNameFlags flags /* in */
            )
        {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            return HasFlags(flags, PluginNameFlags.Encrypted, true);
#else
            return false;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the certificate file extension to use, selecting the
        /// encrypted variant when requested.
        /// </summary>
        /// <param name="encrypted">
        /// True to return the encrypted markup file extension.
        /// </param>
        /// <returns>
        /// The certificate file extension to use.
        /// </returns>
        private static string GetFileExtension( /* CORE */
            bool encrypted /* in */
            )
        {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
            if (encrypted)
                return FileExtension.EncryptedMarkup;
#endif

            return FileExtension.Markup;
        }

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Replaces the extension of the specified file name with the
        /// encrypted markup extension when <paramref name="encrypted" /> is
        /// true.
        /// </summary>
        /// <param name="encrypted">
        /// True to rewrite the file name to use the encrypted extension.
        /// </param>
        /// <param name="fileName">
        /// The file name to mutate in place.
        /// </param>
        private static void MaybeMutateFileNameOnly( /* CORE */
            bool encrypted,     /* in */
            ref string fileName /* in, out */
            )
        {
            if (encrypted)
            {
                fileName = String.Format(
                    "{0}{1}", Path.GetFileNameWithoutExtension(
                    fileName), FileExtension.EncryptedMarkup);
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Simple File Name Methods
        /// <summary>
        /// Gets the default certificate sub-directory name.
        /// </summary>
        /// <returns>
        /// The default certificate sub-directory name.
        /// </returns>
        private static string GetDefaultDirectoryName() /* CORE */
        {
            return Constants.DefaultDirectoryName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default certificate file name, optionally using the
        /// encrypted file extension.
        /// </summary>
        /// <param name="encrypted">
        /// True to use the encrypted file extension.
        /// </param>
        /// <returns>
        /// The default certificate file name, in lower case.
        /// </returns>
        public static string GetDefaultFileName( /* CORE */
            bool encrypted /* in */
            )
        {
            string result = Constants.DefaultFileName;

            if (!String.IsNullOrEmpty(result))
            {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                MaybeMutateFileNameOnly(encrypted, ref result);
#endif

                result = result.ToLowerInvariant();
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the trial certificate file name, optionally using the
        /// encrypted file extension.
        /// </summary>
        /// <param name="encrypted">
        /// True to use the encrypted file extension.
        /// </param>
        /// <returns>
        /// The trial certificate file name, in lower case.
        /// </returns>
        private static string GetTrialFileName( /* CORE */
            bool encrypted /* in */
            )
        {
            string result = Constants.TrialFileName;

            if (!String.IsNullOrEmpty(result))
            {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                MaybeMutateFileNameOnly(encrypted, ref result);
#endif

                result = result.ToLowerInvariant();
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the internal-use certificate file name, optionally using the
        /// encrypted file extension.
        /// </summary>
        /// <param name="encrypted">
        /// True to use the encrypted file extension.
        /// </param>
        /// <returns>
        /// The internal-use certificate file name, in lower case.
        /// </returns>
        public static string GetInternalFileName( /* CORE */
            bool encrypted /* in */
            )
        {
            string result = Constants.InternalFileName;

            if (!String.IsNullOrEmpty(result))
            {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                MaybeMutateFileNameOnly(encrypted, ref result);
#endif

                result = result.ToLowerInvariant();
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Gets the name of the default certificate environment variable.
        /// </summary>
        /// <returns>
        /// The name of the default certificate environment variable.
        /// </returns>
        public static string GetDefaultEnvVarName() /* CORE? */
        {
            return Constants.DefaultEnvVarName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified path to the list, creating the list when
        /// necessary and ignoring any failure.
        /// </summary>
        /// <param name="value">
        /// The path to add; ignored when null.
        /// </param>
        /// <param name="paths">
        /// The list to which the path is added; created when null.
        /// </param>
        /// <returns>
        /// True if the path was added; otherwise, false.
        /// </returns>
        private static bool MaybeAddPath( /* CORE? */
            string value,        /* in */
            ref StringList paths /* in, out */
            )
        {
            if (value != null)
            {
                try
                {
                    if (paths == null)
                        paths = new StringList();

                    paths.Add(value);
                    return true;
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificatePathOps).Name,
                        TracePriority.High);
#endif
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified paths to the list, creating the list when
        /// necessary and ignoring any failure.
        /// </summary>
        /// <param name="list">
        /// The paths to add; ignored when null.
        /// </param>
        /// <param name="paths">
        /// The list to which the paths are added; created when null.
        /// </param>
        /// <returns>
        /// True if the paths were added; otherwise, false.
        /// </returns>
        private static bool MaybeAddPaths( /* CORE? */
            IEnumerable<string> list, /* in */
            ref StringList paths      /* in, out */
            )
        {
            if (list != null)
            {
                try
                {
                    if (paths == null)
                        paths = new StringList();

                    paths.AddRange(list);
                    return true;
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificatePathOps).Name,
                        TracePriority.High);
#endif
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the directory portion of the specified file name to the list,
        /// ignoring empty results and any failure.
        /// </summary>
        /// <param name="fileName">
        /// The file name whose directory portion is added; ignored when null.
        /// </param>
        /// <param name="paths">
        /// The list to which the directory is added; created when null.
        /// </param>
        /// <returns>
        /// True if a directory was added; otherwise, false.
        /// </returns>
        public static bool MaybeAddDirectoryName( /* CORE? */
            string fileName,     /* in */
            ref StringList paths /* in, out */
            )
        {
            if (fileName != null)
            {
                try
                {
                    //
                    // HACK: Since the Path.GetDirectoryName method
                    //       is documented to return null and/or an
                    //       empty string, the return value must be
                    //       checked prior to calling MaybeAddPath,
                    //       because it does not check for an empty
                    //       string -AND- there should not be empty
                    //       strings in the final resulting list.
                    //
                    string directory = Path.GetDirectoryName(
                        fileName); /* throw */

                    if (!String.IsNullOrEmpty(directory))
                        return MaybeAddPath(directory, ref paths);
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificatePathOps).Name,
                        TracePriority.High);
#endif
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the current and original directories associated with the
        /// specified assembly to the list, ignoring any failure.
        /// </summary>
        /// <param name="assembly">
        /// The assembly whose directories are added; ignored when null.
        /// </param>
        /// <param name="paths">
        /// The list to which the directories are added; created when null.
        /// </param>
        /// <returns>
        /// True if at least one directory was added; otherwise, false.
        /// </returns>
        public static bool MaybeAddDirectoryNames( /* CORE? */
            Assembly assembly,   /* in */
            ref StringList paths /* in, out */
            )
        {
            if (assembly != null)
            {
                try
                {
                    int count = 0;

                    if (MaybeAddPath(
                            Utility.GetCurrentPath(assembly),
                            ref paths))
                    {
                        count++;
                    }

                    if (MaybeAddDirectoryName(
                            Utility.GetOriginalLocalPath(assembly),
                            ref paths))
                    {
                        count++;
                    }

                    return (count > 0);
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificatePathOps).Name,
                        TracePriority.High);
#endif
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the directory containing the assembly, identified by the
        /// specified assembly name, to the list, ignoring any failure.
        /// </summary>
        /// <param name="assemblyName">
        /// The assembly name whose directory is added; ignored when null.
        /// </param>
        /// <param name="paths">
        /// The list to which the directory is added; created when null.
        /// </param>
        /// <returns>
        /// True if a directory was added; otherwise, false.
        /// </returns>
        public static bool MaybeAddDirectoryName( /* CORE? */
            AssemblyName assemblyName, /* in */
            ref StringList paths       /* in, out */
            )
        {
            if (assemblyName != null)
            {
                try
                {
                    return MaybeAddDirectoryName(
                        Utility.GetOriginalLocalPath(assemblyName),
                        ref paths);
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificatePathOps).Name,
                        TracePriority.High);
#endif
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the directory containing the file associated with the
        /// specified plugin to the list, ignoring any failure.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin whose file directory is added; ignored when null.
        /// </param>
        /// <param name="paths">
        /// The list to which the directory is added; created when null.
        /// </param>
        /// <returns>
        /// True if a directory was added; otherwise, false.
        /// </returns>
        public static bool MaybeAddDirectoryName( /* CORE? */
            IPluginData pluginData, /* in */
            ref StringList paths    /* in, out */
            )
        {
            if (pluginData != null)
            {
                try
                {
                    return MaybeAddDirectoryName(
                        pluginData.FileName, ref paths);
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(CertificatePathOps).Name,
                        TracePriority.High);
#endif
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the directories listed in the bootstrap directories
        /// environment variable to the list, ignoring any failure.
        /// </summary>
        /// <param name="paths">
        /// The list to which the directories are added; created when null.
        /// </param>
        /// <returns>
        /// True if at least one directory was added; otherwise, false.
        /// </returns>
        public static bool MaybeAddBootstrapDirectories( /* CORE? */
            ref StringList paths /* in, out */
            )
        {
            string value = Configuration.GetVariable(
                Constants.BootstrapDirectoriesEnvVarName);

            if (value != null)
            {
                StringList list = null;
                Result error = null;

                if (Parser.SplitList(
                        null, value, 0, Length.Invalid, true,
                        ref list, ref error) == ReturnCode.Ok)
                {
                    return MaybeAddPaths(list, ref paths);
                }
                else
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "MaybeAddBootstrapDirectories: error = {0}",
                        Utility.FormatWrapOrNull(error)),
                        typeof(CertificatePathOps).Name,
                        TracePriority.MediumHigh);
#endif
                }
            }

            return false;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates that each element of the specified list is a well-formed
        /// path name, treating the final element as a file name and all
        /// preceding elements as directory names.
        /// </summary>
        /// <param name="list">
        /// The list of path name components to validate.
        /// </param>
        /// <param name="error">
        /// Receives an error message when validation fails.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if every element is valid; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode HasValidPathNames( /* CORE */
            IList list,      /* in */
            ref Result error /* out */
            )
        {
            if (list == null)
            {
                error = "invalid path name list";
                return ReturnCode.Error;
            }

            Regex regEx1 = Constants.DirectoryNameRegEx;

            if (regEx1 == null) /* RARE */
            {
                error = "directory name validation unavailable";
                return ReturnCode.Error;
            }

            Regex regEx2 = Constants.FileNameRegEx;

            if (regEx2 == null) /* RARE */
            {
                error = "file name validation unavailable";
                return ReturnCode.Error;
            }

            int count = list.Count;

            if (count == 0)
            {
                error = "no path names";
                return ReturnCode.Error;
            }

            for (int index = 0; index < count; index++)
            {
                string path = Utility.GetStringFromObject(
                    list[index]);

                if (String.IsNullOrEmpty(path))
                {
                    error = String.Format(
                        "invalid path name {0}", index);

                    return ReturnCode.Error;
                }

                bool isFileName = (index == (count - 1));
                Regex regEx = isFileName ? regEx2 : regEx1;

                if (regEx == null) /* IMPOSSIBLE */
                {
                    error = "path name validation unavailable";
                    return ReturnCode.Error;
                }

                Match match = regEx.Match(path);

                if ((match == null) || !match.Success)
                {
                    error = String.Format(
                        "bad {0} name {1}", isFileName ?
                        "file" : "directory", index);

                    return ReturnCode.Error;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified assembly name flags contain the
        /// given flags, requiring either all or any of them to be present.
        /// </summary>
        /// <param name="flags">
        /// The flags to examine.
        /// </param>
        /// <param name="hasFlags">
        /// The flags to look for.
        /// </param>
        /// <param name="all">
        /// True to require all of <paramref name="hasFlags" /> to be present;
        /// false to require any of them.
        /// </param>
        /// <returns>
        /// True if the requested flags are present; otherwise, false.
        /// </returns>
        private static bool HasFlags( /* CORE */
            AssemblyNameFlags flags,    /* in */
            AssemblyNameFlags hasFlags, /* in */
            bool all
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != AssemblyNameFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified plugin name flags contain the
        /// given flags, requiring either all or any of them to be present.
        /// </summary>
        /// <param name="flags">
        /// The flags to examine.
        /// </param>
        /// <param name="hasFlags">
        /// The flags to look for.
        /// </param>
        /// <param name="all">
        /// True to require all of <paramref name="hasFlags" /> to be present;
        /// false to require any of them.
        /// </param>
        /// <returns>
        /// True if the requested flags are present; otherwise, false.
        /// </returns>
        private static bool HasFlags( /* CORE */
            PluginNameFlags flags,    /* in */
            PluginNameFlags hasFlags, /* in */
            bool all
            )
        {
            if (all)
            {
                return ((flags & hasFlags) == hasFlags);
            }
            else
            {
                return ((flags & hasFlags) != PluginNameFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Combines the supplied prefix, name, suffix, and extension into a
        /// single file name using the specified format and separator,
        /// avoiding a duplicated extension.
        /// </summary>
        /// <param name="format">
        /// The composite format string used to combine the parts.
        /// </param>
        /// <param name="separator">
        /// The optional separator character placed between parts.
        /// </param>
        /// <param name="prefix">
        /// The prefix portion of the file name.
        /// </param>
        /// <param name="name">
        /// The base name portion of the file name.
        /// </param>
        /// <param name="suffix">
        /// The suffix portion of the file name.
        /// </param>
        /// <param name="extension">
        /// The file extension to append; omitted when already present at the
        /// end of <paramref name="suffix" />.
        /// </param>
        /// <returns>
        /// The combined file name.
        /// </returns>
        private static string CombineFileNameParts( /* CORE */
            string format,   /* in */
            char? separator, /* in */
            string prefix,   /* in */
            string name,     /* in */
            string suffix,   /* in */
            string extension /* in */
            )
        {
            if ((suffix != null) && (extension != null) && suffix.EndsWith(
                    extension, Utility.GetPathComparisonType()))
            {
                extension = null;
            }

            return String.Format("{0}{1}", String.Format(
                format, prefix, name, separator, suffix).TrimEnd(
                Constants.TrimSeparators), extension);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the certificate file name for the specified assembly using
        /// the given prefix and assembly name flags.
        /// </summary>
        /// <param name="prefix">
        /// The prefix to incorporate into the file name.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly to incorporate into the file name; may be null.
        /// </param>
        /// <param name="flags">
        /// Flags controlling the file name format and extension.
        /// </param>
        /// <returns>
        /// The constructed assembly certificate file name, or null when none
        /// can be built.
        /// </returns>
        private static string GetAssemblyFileName( /* CORE */
            string prefix,             /* in */
            AssemblyName assemblyName, /* in: OPTIONAL, May be null. */
            AssemblyNameFlags flags    /* in */
            )
        {
            bool encrypted = UseEncryptedFileExtension(flags);

            string suffix = HasFlags(
                flags, AssemblyNameFlags.NoDefault, true) ?
                    null : GetDefaultFileName(encrypted);

            string fileExtension = GetFileExtension(encrypted);

            if (assemblyName != null)
            {
                if (HasFlags(flags, AssemblyNameFlags.Format3, true))
                {
                    return CombineFileNameParts(
                        Constants.AssemblyFileNameFormat3,
                        Characters.Underscore, prefix,
                        assemblyName.Name, suffix,
                        fileExtension);
                }
                else if (HasFlags(flags, AssemblyNameFlags.Format2, true))
                {
                    return CombineFileNameParts(
                        Constants.AssemblyFileNameFormat2,
                        Characters.MinusSign, prefix,
                        assemblyName.Name, suffix,
                        fileExtension);
                }
                else if (HasFlags(flags, AssemblyNameFlags.Format1, true))
                {
                    return CombineFileNameParts(
                        Constants.AssemblyFileNameFormat1,
                        Characters.Period, prefix,
                        assemblyName.Name, suffix,
                        fileExtension);
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the specified plugin, unwrapping any wrapper and
        /// honoring the supplied naming flags.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin whose name is returned; may be null.
        /// </param>
        /// <param name="flags">
        /// Flags controlling how the plugin name is determined.
        /// </param>
        /// <returns>
        /// The plugin name, or null when it cannot be determined.
        /// </returns>
        public static string GetPluginName( /* CORE */
            IPluginData pluginData, /* in: OPTIONAL, May be null. */
            PluginNameFlags flags   /* in: OPTIONAL, Use full type name? */
            )
        {
            if (pluginData == null)
                return null;

            if (HasFlags(flags, PluginNameFlags.UsePluginData, true) ||
                Utility.IsCrossAppDomain(pluginData))
            {
                return pluginData.TypeName;
            }

            //
            // BUGFIX: Make sure that we never use the plugin wrapper
            //         to obtain the type name.
            //
            IWrapper wrapper = pluginData as IWrapper;

            if (wrapper != null)
            {
                pluginData = wrapper.Object as IPluginData;

                if (pluginData == null)
                    return null;
            }

            Type type = pluginData.GetType();

            if (type == null)
                return null;

            return type.Name;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the certificate file name for the package associated with
        /// the specified plugin using the given prefix and naming flags.
        /// </summary>
        /// <param name="prefix">
        /// The prefix to incorporate into the file name.
        /// </param>
        /// <param name="pluginData">
        /// The plugin to incorporate into the file name; may be null.
        /// </param>
        /// <param name="flags">
        /// Flags controlling the file name format and extension.
        /// </param>
        /// <returns>
        /// The constructed package certificate file name, or null when none
        /// can be built.
        /// </returns>
        private static string GetPackageFileName( /* CORE */
            string prefix,          /* in */
            IPluginData pluginData, /* in: OPTIONAL, May be null. */
            PluginNameFlags flags   /* in: OPTIONAL, Use full type name? */
            )
        {
            bool encrypted = UseEncryptedFileExtension(flags);

            string suffix = HasFlags(
                flags, PluginNameFlags.NoDefault, true) ?
                    null : GetDefaultFileName(encrypted);

            string fileExtension = GetFileExtension(encrypted);

            if (pluginData != null)
            {
                if (HasFlags(flags, PluginNameFlags.Format3, true))
                {
                    return CombineFileNameParts(
                        Constants.PackageFileNameFormat3,
                        Characters.Underscore, prefix,
                        GetPluginName(pluginData,
                            flags | PluginNameFlags.ForFileName),
                        suffix, fileExtension);
                }
                else if (HasFlags(flags, PluginNameFlags.Format2, true))
                {
                    return CombineFileNameParts(
                        Constants.PackageFileNameFormat2,
                        Characters.MinusSign, prefix,
                        GetPluginName(pluginData,
                            flags | PluginNameFlags.ForFileName),
                        suffix, fileExtension);
                }
                else if (HasFlags(flags, PluginNameFlags.Format1, true))
                {
                    return CombineFileNameParts(
                        Constants.PackageFileNameFormat1,
                        Characters.Period, prefix,
                        GetPluginName(pluginData,
                            flags | PluginNameFlags.ForFileName),
                        suffix, fileExtension);
                }
            }

            return null;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Simple Directory Name Methods
        /// <summary>
        /// Gets the existing directory containing the file associated with
        /// the specified plugin.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin whose file directory is returned.
        /// </param>
        /// <returns>
        /// The directory containing the plugin file, or null when it cannot
        /// be determined.
        /// </returns>
        public static string GetDirectory( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            string directory = null;

            if (GetDirectory(pluginData, ref directory))
                return directory;

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to get the existing directory containing the file
        /// associated with the specified plugin.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin whose file directory is sought.
        /// </param>
        /// <param name="directory">
        /// Receives the directory containing the plugin file when successful.
        /// </param>
        /// <returns>
        /// True if an existing directory was found; otherwise, false.
        /// </returns>
        public static bool GetDirectory( /* CORE */
            IPluginData pluginData, /* in */
            ref string directory    /* in, out */
            )
        {
            if (pluginData == null)
                return false;

            string fileName = pluginData.FileName;

            if (String.IsNullOrEmpty(fileName))
                return false;

            string localDirectory;

            try
            {
                localDirectory = Path.GetDirectoryName(
                    fileName); /* throw */
            }
            catch
            {
                return false;
            }

            if (String.IsNullOrEmpty(localDirectory))
                return false;

            if (!Directory.Exists(localDirectory))
                return false;

            directory = localDirectory;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the base directory used to save certificate files.
        /// </summary>
        /// <returns>
        /// The base save directory, or null when it cannot be determined.
        /// </returns>
        private static string GetSaveDirectory() /* CORE */
        {
            return MaybeCreateSaveDirectory(
                BootstrapType.None, true, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the directory used to save certificate files, optionally
        /// including the bootstrap-specific sub-directory and creating it on
        /// disk when requested.
        /// </summary>
        /// <param name="bootstrapType">
        /// The bootstrap type whose sub-directory is appended when it is not
        /// <see cref="BootstrapType.None" /> and the base-only directory is
        /// not requested.
        /// </param>
        /// <param name="baseOnly">
        /// True to return only the base directory without the default
        /// sub-directory.
        /// </param>
        /// <param name="create">
        /// True to create the directory on disk when it does not exist.
        /// </param>
        /// <returns>
        /// The save directory, or null when it cannot be determined.
        /// </returns>
        public static string MaybeCreateSaveDirectory( /* CORE */
            BootstrapType bootstrapType, /* in */
            bool baseOnly,               /* in */
            bool create                  /* in */
            )
        {
            string directory = Utility.GetDocumentDirectory();

            if (String.IsNullOrEmpty(directory))
                return null;

            directory = Path.Combine(directory,
                CertificateAssemblyOps.MustGetSimpleName());

            if (!baseOnly)
            {
                directory = Path.Combine(
                    directory, GetDefaultDirectoryName());

                if (bootstrapType != BootstrapType.None)
                {
                    directory = Path.Combine(
                        directory, bootstrapType.ToString());
                }
            }

            if (create && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return directory;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Advanced Directory Name Methods
        /// <summary>
        /// Builds the ordered list of candidate directories to search for
        /// certificate files, generally from most specific to least specific.
        /// </summary>
        /// <param name="scriptPath">
        /// The directory of the script currently being evaluated, if any.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name used to derive package-specific directories.
        /// </param>
        /// <param name="assembly">
        /// The assembly used to derive package and binary directories.
        /// </param>
        /// <param name="environment">
        /// True to include directories obtained from environment variables.
        /// </param>
        /// <returns>
        /// The ordered array of candidate directories.
        /// </returns>
        private static string[] GetDirectories( /* CORE */
            string scriptPath,           /* in */
            AssemblyName assemblyName,   /* in */
            Assembly assembly,           /* in */
            bool environment             /* in */
            )
        {
            //
            // NOTE: Grab the package name and version, which are based
            //       on the assembly name.
            //
            string packageName = null;
            Version packageVersion = null;

            if (assemblyName != null)
            {
                packageName = assemblyName.Name;
                packageVersion = assemblyName.Version;
            }

            //
            // NOTE: Build the list of candidate paths to check for the
            //       various certificate file names, listed in order of
            //       priority (which is generally in the same ordering
            //       as "most specific" to "least specific").
            //
            PathFlags pathFlags = Constants.DefaultPathFlags;

            string[] paths = {
                environment ? Configuration.GetVariable(
                    Constants.PathEnvVarName) : null,
                GetSaveDirectory(),
                scriptPath,
                environment ? Configuration.GetVariable(
                    EnvVars.XdgStateHome) : null,
                environment ? Configuration.GetVariable(
                    EnvVars.XdgStartupHome) : null,
                environment ? Configuration.GetVariable(
                    EnvVars.UserProfile) : null,
                Utility.GetBinaryPath(),
                CertificateAssemblyOps.GetDirectory(),
                (assemblyName != null) ?
                    Utility.GetPackagePath(
                        assembly, packageName,
                        packageVersion, pathFlags |
                            PathFlags.None) : null,
                (assemblyName != null) ?
                    Utility.GetPackagePath(
                        assembly, packageName,
                        packageVersion, pathFlags |
                            PathFlags.NoShared) : null,
                (assemblyName != null) ?
                    Utility.GetPackagePath(
                        assembly, packageName,
                        packageVersion, pathFlags |
                            PathFlags.Root) : null,
                (assemblyName != null) ?
                    Utility.GetPackagePath(
                        assembly, packageName,
                        packageVersion, pathFlags |
                            PathFlags.RootOnly) : null,
                (assemblyName != null) ?
                    Utility.GetPackagePath(
                        assembly, packageName,
                        null, pathFlags |
                            PathFlags.None) : null,
                (assemblyName != null) ?
                    Utility.GetPackagePath(
                        assembly, packageName,
                        null, pathFlags |
                            PathFlags.NoShared) : null,
                (assemblyName != null) ?
                    Utility.GetPackagePath(
                        assembly, packageName,
                        null, pathFlags |
                            PathFlags.Root) : null,
                (assemblyName != null) ?
                    Utility.GetPackagePath(
                        assembly, packageName,
                        null, pathFlags |
                            PathFlags.RootOnly) : null,
                Utility.GetPackagePath(
                        assembly, null,
                        null, pathFlags |
                            PathFlags.None),
                Utility.GetPackagePath(
                        assembly, null,
                        null, pathFlags |
                            PathFlags.NoShared),
                Utility.GetPackagePath(
                        assembly, null,
                        null, pathFlags |
                            PathFlags.Root),
                Utility.GetPackagePath(
                        assembly, null,
                        null, pathFlags |
                            PathFlags.RootOnly)
            };

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetDirectories: paths = {0}", Utility.FormatWrapOrNull(paths)),
                typeof(CertificatePathOps).Name, TracePriority.MediumLow);
#endif

            ///////////////////////////////////////////////////////////////////

            return paths;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Advanced File Name Methods
        /// <summary>
        /// Gathers certificate file names supplied through the client data,
        /// including any dictionary of environment variable values and any
        /// Tcl-formatted list of file names, adding them to the list.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to parse list data; may be null.
        /// </param>
        /// <param name="assemblyName">
        /// The assembly name used to derive environment variable names.
        /// </param>
        /// <param name="pluginData">
        /// The plugin used to derive environment variable names.
        /// </param>
        /// <param name="clientData">
        /// The client data inspected for certificate file names.
        /// </param>
        /// <param name="fileNames">
        /// The list to which any discovered file names are added.
        /// </param>
        private static void GetFileNames( /* CORE */
            Interpreter interpreter,   /* in */
            AssemblyName assemblyName, /* in */
            IPluginData pluginData,    /* in */
            IClientData clientData,    /* in */
            StringList fileNames       /* in, out */
            )
        {
            if ((clientData == null) || (fileNames == null))
                return;

            //
            // NOTE: First, see if the client data contains a string
            //       dictionary that may contain certificate file names.
            //
            StringDictionary dictionary = null;

            if (CertificateSharedOps.TryGetDictionary(
                    clientData, ref dictionary) && (dictionary != null))
            {
                //
                // NOTE: Re-grab all the environment variable names that
                //       may refer to certificate file names.
                //
                StringList overrideEnvVarNames = null;
                StringList envVarNames = null;

                GetEnvVarNames(
                    assemblyName, pluginData, ref overrideEnvVarNames,
                    ref envVarNames);

                foreach (string overrideEnvVarName in overrideEnvVarNames)
                {
                    CheckEnvVar(
                        dictionary, fileNames, overrideEnvVarName);

                    CheckAltEnvVar(
                        dictionary, fileNames, overrideEnvVarName);
                }

                foreach (string envVarName in envVarNames)
                {
                    CheckEnvVar(
                        dictionary, fileNames, envVarName);

                    CheckAltEnvVar(
                        dictionary, fileNames, envVarName);
                }
            }

            object data = null;

            /* IGNORED */
            clientData = ClientData.UnwrapOrReturn(clientData, ref data);

            //
            // NOTE: *HACK* See if the data is really a string.  If so,
            //       assume that it must be a properly Tcl-formatted list
            //       containing certificate file names.
            //
            string dataString = data as string;

            if (dataString == null)
                return;

            ReturnCode code;
            StringList list = null;
            Result error = null;

            code = Parser.SplitList(
                interpreter, dataString, 0, Length.Invalid,
                true, ref list, ref error);

            if (code != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "GetFileNames: bad data string, error = {0}",
                    Utility.FormatWrapOrNull(true, false, error)),
                    typeof(CertificatePathOps).Name,
                    TracePriority.MediumHigh);
#endif

                return;
            }

            if (list == null)
                return;

            ///////////////////////////////////////////////////////////////////

            list = Utility.GetUniqueElements(list);

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetFileNames: list = {0}", Utility.FormatWrapOrNull(list)),
                typeof(CertificatePathOps).Name, TracePriority.MediumLow);
#endif

            ///////////////////////////////////////////////////////////////////

            fileNames.AddRange(list);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the complete, ordered list of candidate certificate file
        /// names to search for the specified assembly or plugin, consulting
        /// the client data, the override and normal environment variables,
        /// the candidate directories, and any plugin-supplied trial or
        /// internal certificate file names.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during the search; may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly being verified; may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin being verified; may be null.
        /// </param>
        /// <param name="clientData">
        /// The client data inspected for certificate file names; may be null.
        /// </param>
        /// <param name="policy">
        /// The execution policy controlling whether the file system search
        /// may be skipped when an environment variable supplies a file name.
        /// </param>
        /// <param name="bootstrapType">
        /// The bootstrap type used to form the bootstrap-specific
        /// sub-directories.
        /// </param>
        /// <param name="isForFileNamesOnly">
        /// True when only file names are being collected, which enables the
        /// internal-use certificate fallback.
        /// </param>
        /// <param name="extraDirectories">
        /// True to include extra directories obtained from environment
        /// variables.
        /// </param>
        /// <param name="useFileCache">
        /// True to also include file names from the certificate file cache.
        /// </param>
        /// <param name="isForThisPlugin">
        /// True when the search is for this plugin, which enables the
        /// internal-use certificate fallback.
        /// </param>
        /// <param name="fileNames">
        /// The list to which the resulting candidate file names are added;
        /// created when null.
        /// </param>
        /// <param name="error">
        /// Receives an error message when no candidate file names are found.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetFileNames( /* CORE */
            Interpreter interpreter,     /* in, OPTIONAL: May be null. */
            Assembly assembly,           /* in, OPTIONAL: May be null. */
            IPlugin plugin,              /* in, OPTIONAL: May be null. */
            IClientData clientData,      /* in, OPTIONAL: May be null. */
            ExecutionPolicy policy,      /* in */
            BootstrapType bootstrapType, /* in */
            bool isForFileNamesOnly,     /* in */
            bool extraDirectories,       /* in */
            bool useFileCache,           /* in */
            bool isForThisPlugin,        /* in */
            ref StringList fileNames,    /* in, out */
            ref Result error             /* out */
            )
        {
            //
            // NOTE: Get the name of the specified assembly.  We use this to
            //       construct the full path to the package certificate file.
            //
            AssemblyName assemblyName = null;

            if ((plugin != null) &&
                CertificateSharedOps.IsCrossAppDomain(interpreter, plugin))
            {
                assemblyName = plugin.AssemblyName;
            }

            if (assemblyName == null)
                assemblyName = (assembly != null) ? assembly.GetName() : null;

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Get the default certificate sub-directory name.
            //
            string defaultDirectoryName = GetDefaultDirectoryName();

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Create a local list of file names to temporarily hold the
            //       resulting list of file names to be searched.
            //
            StringList localFileNames = new StringList();

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: *NEW* First, inspect the IClientData provided when the
            //       plugin was loaded.  It may contain a string pointing to
            //       a certificate file name.
            //
            GetFileNames(
                interpreter, assemblyName, plugin, clientData,
                localFileNames);

            ///////////////////////////////////////////////////////////////////

            string namePrefix = Configuration.GetVariable(
                Constants.NamePrefixEnvVarName);

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Get the package specific certificate file names, if any.
            //
            string[] packageFileNames = {
                (namePrefix != null) ? GetPackageFileName(
                    namePrefix, plugin, PluginNameFlags.Pass1) : null,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                (namePrefix != null) ? GetPackageFileName(
                    namePrefix, plugin, PluginNameFlags.Pass2) : null,
#endif
                (namePrefix != null) ? GetPackageFileName(
                    namePrefix, plugin, PluginNameFlags.Pass3) : null,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                (namePrefix != null) ? GetPackageFileName(
                    namePrefix, plugin, PluginNameFlags.Pass4) : null,
#endif
                (namePrefix != null) ? GetPackageFileName(
                    namePrefix, plugin, PluginNameFlags.Pass5) : null,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                (namePrefix != null) ? GetPackageFileName(
                    namePrefix, plugin, PluginNameFlags.Pass6) : null,
                (namePrefix != null) ? GetPackageFileName(
                    namePrefix, plugin, PluginNameFlags.Pass7) : null,
                (namePrefix != null) ? GetPackageFileName(
                    namePrefix, plugin, PluginNameFlags.Pass8) : null,
#endif
                GetPackageFileName(
                    null, plugin, PluginNameFlags.Pass1),
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                GetPackageFileName(
                    null, plugin, PluginNameFlags.Pass2),
#endif
                GetPackageFileName(
                    null, plugin, PluginNameFlags.Pass3),
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                GetPackageFileName(
                    null, plugin, PluginNameFlags.Pass4),
#endif
                null
            };

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Get the assembly certificate file names.
            //
            string[] assemblyFileNames = {
                (namePrefix != null) ? GetAssemblyFileName(
                    namePrefix, assemblyName, AssemblyNameFlags.Pass1) : null,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                (namePrefix != null) ? GetAssemblyFileName(
                    namePrefix, assemblyName, AssemblyNameFlags.Pass2) : null,
#endif
                (namePrefix != null) ? GetAssemblyFileName(
                    namePrefix, assemblyName, AssemblyNameFlags.Pass3) : null,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                (namePrefix != null) ? GetAssemblyFileName(
                    namePrefix, assemblyName, AssemblyNameFlags.Pass4) : null,
#endif
                (namePrefix != null) ? GetAssemblyFileName(
                    namePrefix, assemblyName, AssemblyNameFlags.Pass5) : null,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                (namePrefix != null) ? GetAssemblyFileName(
                    namePrefix, assemblyName, AssemblyNameFlags.Pass6) : null,
                (namePrefix != null) ? GetAssemblyFileName(
                    namePrefix, assemblyName, AssemblyNameFlags.Pass7) : null,
                (namePrefix != null) ? GetAssemblyFileName(
                    namePrefix, assemblyName, AssemblyNameFlags.Pass8) : null,
#endif
                GetAssemblyFileName(
                    null, assemblyName, AssemblyNameFlags.Pass1),
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                GetAssemblyFileName(
                    null, assemblyName, AssemblyNameFlags.Pass2),
#endif
                GetAssemblyFileName(
                    null, assemblyName, AssemblyNameFlags.Pass3),
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                GetAssemblyFileName(
                    null, assemblyName, AssemblyNameFlags.Pass4),
#endif
                null
            };

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Get the default certificate file names.
            //
            string[] defaultFileNames = {
                (namePrefix != null) ? CombineFileNameParts(
                    Constants.DefaultFileNameFormat1, null, namePrefix,
                    null, GetDefaultFileName(false),
                    FileExtension.Markup) : null,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                (namePrefix != null) ? CombineFileNameParts(
                    Constants.DefaultFileNameFormat1, null, namePrefix,
                    null, GetDefaultFileName(true),
                    FileExtension.EncryptedMarkup) : null,
#endif
                (namePrefix != null) ? CombineFileNameParts(
                    Constants.DefaultFileNameFormat1, null, namePrefix,
                    null, null, FileExtension.Markup) : null,
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                (namePrefix != null) ? CombineFileNameParts(
                    Constants.DefaultFileNameFormat1, null, namePrefix,
                    null, null, FileExtension.EncryptedMarkup) : null,
#endif
                GetDefaultFileName(false),
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                GetDefaultFileName(true),
#endif
                null
            };

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Grab all the environment variable names that may refer
            //       to certificate file names.
            //
            StringList overrideEnvVarNames = null;
            StringList envVarNames = null;

            GetEnvVarNames(
                assemblyName, plugin, ref overrideEnvVarNames,
                ref envVarNames);

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Check "override" environment variables now.  These are
            //       always checked before the file system is searched.
            //
            if (overrideEnvVarNames != null)
            {
                bool foundOverrideEnvVar = false;

                foreach (string overrideEnvVarName in overrideEnvVarNames)
                {
                    if (!String.IsNullOrEmpty(overrideEnvVarName))
                    {
                        CheckEnvVar(
                            localFileNames, overrideEnvVarName,
                            ref foundOverrideEnvVar);

                        CheckAltEnvVar(
                            localFileNames, overrideEnvVarName,
                            ref foundOverrideEnvVar);
                    }
                }

                if (foundOverrideEnvVar && Utility.HasFlags(policy,
                        ExecutionPolicy.MaybeNoFileSearch, true))
                {
                    goto done;
                }
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Attempt to query the directory containing the innermost
            //       script file being evaluated right now, if any.
            //
            string scriptPath = null;
            Result localError = null;

            if (!Configuration.DoesVariableExist(
                    Constants.NoScriptPathEnvVarName) &&
                Utility.GetScriptPath(
                    interpreter, true, ref scriptPath,
                    ref localError) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(String.Format(
                    "GetFileNames: script path failure, localError = {0}",
                    Utility.FormatWrapOrNull(true, false, localError)),
                    typeof(CertificatePathOps).Name, TracePriority.MediumHigh);
#endif
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Query the list of candidate paths to check for the
            //       various certificate file names, listed in order of
            //       priority (which is generally in the same ordering
            //       as "most specific" to "least specific").
            //
            string[] paths = GetDirectories(
                scriptPath, assemblyName, assembly, extraDirectories);

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Search each candidate path in the list, in order.
            //
            foreach (string path in paths)
            {
                //
                // NOTE: Skip over any path that is null or empty as those
                //       cannot be considered to form the basis of a valid
                //       search.
                //
                if (String.IsNullOrEmpty(path))
                    continue;

                //
                // NOTE: Are there any package file names available?
                //
                if (packageFileNames != null)
                {
                    //
                    // NOTE: Check all the package file names, in order.
                    //
                    foreach (string packageFileName in packageFileNames)
                    {
                        //
                        // NOTE: Again, skip over any file name that is null
                        //       or empty as that cannot be searched for.
                        //
                        if (String.IsNullOrEmpty(packageFileName))
                            continue;

                        localFileNames.Add(Path.Combine(Path.Combine(
                            Path.Combine(path, defaultDirectoryName),
                            bootstrapType.ToString()), packageFileName));

                        localFileNames.Add(Path.Combine(Path.Combine(
                            path, defaultDirectoryName), packageFileName));

                        localFileNames.Add(Path.Combine(Path.Combine(
                            path, bootstrapType.ToString()), packageFileName));

                        localFileNames.Add(Path.Combine(
                            path, packageFileName));
                    }
                }

                ///////////////////////////////////////////////////////////////

                //
                // NOTE: Are there an assembly file names available?
                //
                if (assemblyFileNames != null)
                {
                    //
                    // NOTE: Check all the assembly file names, in order.
                    //
                    foreach (string assemblyFileName in assemblyFileNames)
                    {
                        //
                        // NOTE: Again, skip over any file name that is null
                        //       or empty as that cannot be searched for.
                        //
                        if (String.IsNullOrEmpty(assemblyFileName))
                            continue;

                        localFileNames.Add(Path.Combine(Path.Combine(
                            Path.Combine(path, defaultDirectoryName),
                            bootstrapType.ToString()), assemblyFileName));

                        localFileNames.Add(Path.Combine(Path.Combine(
                            path, defaultDirectoryName), assemblyFileName));

                        localFileNames.Add(Path.Combine(Path.Combine(
                            path, bootstrapType.ToString()), assemblyFileName));

                        localFileNames.Add(Path.Combine(
                            path, assemblyFileName));
                    }
                }

                ///////////////////////////////////////////////////////////////

                //
                // NOTE: Are there any default file names available?
                //
                if (defaultFileNames != null)
                {
                    foreach (string defaultFileName in defaultFileNames)
                    {
                        //
                        // NOTE: Again, skip over any file name that is null
                        //       or empty as that cannot be searched for.
                        //
                        if (String.IsNullOrEmpty(defaultFileName))
                            continue;

                        localFileNames.Add(Path.Combine(Path.Combine(
                            Path.Combine(path, defaultDirectoryName),
                            bootstrapType.ToString()), defaultFileName));

                        localFileNames.Add(Path.Combine(Path.Combine(
                            path, defaultDirectoryName), defaultFileName));

                        localFileNames.Add(Path.Combine(Path.Combine(
                            path, bootstrapType.ToString()), defaultFileName));

                        localFileNames.Add(Path.Combine(
                            path, defaultFileName));
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Check "normal" environment variables now.  These are
            //       always checked __after__ the file system is searched.
            //
            if (envVarNames != null)
            {
                bool foundEnvVar = false;

                foreach (string envVarName in envVarNames)
                {
                    if (!String.IsNullOrEmpty(envVarName))
                    {
                        CheckEnvVar(
                            localFileNames, envVarName,
                            ref foundEnvVar);

                        CheckAltEnvVar(
                            localFileNames, envVarName,
                            ref foundEnvVar);
                    }
                }

                if (foundEnvVar && Utility.HasFlags(policy,
                        ExecutionPolicy.MaybeNoFileSearch, true))
                {
                    goto done;
                }
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Attempt to query the plugin itself for a trial license
            //       certificate.  This is optional; however, it allows for
            //       a plugin to provide a temporary license certificate to
            //       be used when loading (i.e. it can be expired because a
            //       license certificate renewal can be processed while the
            //       loading and verification is pending).  Since this is a
            //       last resort (i.e. we should always prefer a permanent
            //       license certificate), these file names, if any, should
            //       always be last.
            //
            if (plugin != null)
            {
                string[] trialFileNames = {
                    Path.Combine(Path.Combine(defaultDirectoryName,
                        bootstrapType.ToString()), GetTrialFileName(false)),
                    Path.Combine(Path.Combine(defaultDirectoryName,
                        bootstrapType.ToString()), GetTrialFileName(true)),
                    Path.Combine(
                        defaultDirectoryName, GetTrialFileName(false)),
                    Path.Combine(
                        defaultDirectoryName, GetTrialFileName(true)),
                    Path.Combine(
                        bootstrapType.ToString(), GetTrialFileName(false)),
                    Path.Combine(
                        bootstrapType.ToString(), GetTrialFileName(true)),
                    GetTrialFileName(false),
                    GetTrialFileName(true)
                };

                foreach (string trialFileName in trialFileNames)
                {
                    if (String.IsNullOrEmpty(trialFileName))
                        continue;

                    string pluginFileName;
                    Result pluginError = null;

                    pluginFileName = plugin.GetCertificateFileName(
                        interpreter, trialFileName, ref pluginError);

                    if (!String.IsNullOrEmpty(pluginFileName))
                    {
                        localFileNames.Add(pluginFileName);
                    }
                    else
                    {
#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(String.Format(
                            "Plugin {0} has no file name for {1}: {2}",
                            Utility.FormatWrapOrNull(plugin),
                            Utility.FormatWrapOrNull(trialFileName),
                            Utility.FormatWrapOrNull(pluginError)),
                            typeof(CertificatePathOps).Name,
                            TracePriority.MediumHigh);
#endif
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Attempt to query only the Harpy plugin resources for
            //       an "FOR INTERNAL USE ONLY" license certificate.  This
            //       is optional; however, it allows for the Harpy plugin
            //       to provide a permanent license certificate to be used
            //       during development and/or experimenting with Harpy on
            //       one or more local machines; however, it cannot be
            //       legally shipped with external applications because it
            //       is licensed to ourselves (i.e. "Mistachkin Systems").
            //       Since this is an "ultimate fallback" certificate, it
            //       should always be last.
            //
            if (isForFileNamesOnly && isForThisPlugin &&
                !Configuration.DoesVariableExist(
                    Constants.NoInternalEnvVarName))
            {
                string[] internalFileNames = {
                    GetInternalFileName(false),
                    GetInternalFileName(true)
                };

                foreach (string internalFileName in internalFileNames)
                {
                    if (String.IsNullOrEmpty(internalFileName))
                        continue;

                    localFileNames.Add(internalFileName);
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (useFileCache)
            {
                //
                // HACK: Add (all?) the file names from the file cache to
                //       the list of file names returned by this method.
                //       Since this is technically cached data, only add
                //       it after all the other non-cached data has been
                //       added.  It is possible that these semantics may
                //       need to be changed later.
                //
                CertificateLicenseState.AddCachedFileNames(ref fileNames);
            }

            ///////////////////////////////////////////////////////////////////

        done:

            //
            // NOTE: Make sure there are some certificate file names to check.
            //
            if (localFileNames.Count == 0)
            {
                error = "no candidate package certificate file names";
                return ReturnCode.Error;
            }

            ///////////////////////////////////////////////////////////////////

            localFileNames = Utility.GetUniqueElements(localFileNames);

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetFileNames: localFileNames = {0}",
                Utility.FormatWrapOrNull(localFileNames)),
                typeof(CertificatePathOps).Name, TracePriority.MediumLow);
#endif

            ///////////////////////////////////////////////////////////////////

            if (fileNames == null)
                fileNames = new StringList();

            fileNames.AddRange(localFileNames);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the list of distinct certificate file names, without their
        /// directory portions, for the specified assembly or plugin.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used during the search; may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly being verified; may be null.
        /// </param>
        /// <param name="plugin">
        /// The plugin being verified; may be null.
        /// </param>
        /// <param name="clientData">
        /// The client data inspected for certificate file names; may be null.
        /// </param>
        /// <param name="bootstrapType">
        /// The bootstrap type used to form the bootstrap-specific
        /// sub-directories.
        /// </param>
        /// <param name="isForThisPlugin">
        /// True when the search is for this plugin.
        /// </param>
        /// <param name="fileNames">
        /// The list to which the resulting file names are added; created when
        /// null.
        /// </param>
        /// <param name="error">
        /// Receives an error message when the operation fails.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode GetFileNamesOnly( /* CORE */
            Interpreter interpreter,     /* in, OPTIONAL: May be null. */
            Assembly assembly,           /* in, OPTIONAL: May be null. */
            IPlugin plugin,              /* in, OPTIONAL: May be null. */
            IClientData clientData,      /* in, OPTIONAL: May be null. */
            BootstrapType bootstrapType, /* in */
            bool isForThisPlugin,        /* in */
            ref StringList fileNames,    /* in, out */
            ref Result error             /* out */
            )
        {
            StringList localFileNames = null;

            if (GetFileNames(
                    interpreter, assembly, plugin, clientData,
                    ExecutionPolicy.None, bootstrapType, true,
                    false, false, isForThisPlugin,
                    ref localFileNames, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if (localFileNames == null)
                return ReturnCode.Error;

            StringList localFileNamesOnly = new StringList();

            foreach (string fileName in localFileNames)
                localFileNamesOnly.Add(Path.GetFileName(fileName));

            localFileNamesOnly = Utility.GetUniqueElements(
                localFileNamesOnly);

            ///////////////////////////////////////////////////////////////////

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.DebugTrace(String.Format(
                "GetFileNamesOnly: localFileNamesOnly = {0}",
                Utility.FormatWrapOrNull(localFileNamesOnly)),
                typeof(CertificatePathOps).Name, TracePriority.MediumLow);
#endif

            ///////////////////////////////////////////////////////////////////

            if (fileNames == null)
                fileNames = new StringList();

            fileNames.AddRange(localFileNamesOnly);

            return ReturnCode.Ok;
        }
        #endregion
    }
}
