/*
 * WebGlobalState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Holds the process-wide global state for the Kapok server, including the
    /// application domain, assembly, assembly name, and a fake plugin used for
    /// license verification.
    /// </summary>
    [ObjectId("b933a9b4-33b4-43cb-90fd-ef030229032a")]
    internal static class WebGlobalState
    {
        #region Private Constants
        //
        // NOTE: Grab the current application domain.  This is possibly needed
        //       to setup access to the native SQLite core library when running
        //       in ASP.NET.
        //
        /// <summary>
        /// The application domain associated with the Kapok assembly.
        /// </summary>
        private static readonly AppDomain appDomain =
            AppDomain.CurrentDomain;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Grab the currently executing assembly.  This is needed to help
        //       setup access to the Eagle core library when running in ASP.NET
        //       as well as for the license certificate checking performed by
        //       this component.
        //
        /// <summary>
        /// The Kapok managed assembly.
        /// </summary>
        private static readonly Assembly assembly =
            Assembly.GetExecutingAssembly();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Grab the name of the currently executing assembly.  This is
        //       needed when verifying the server license certificate.
        //
        /// <summary>
        /// The name of the Kapok managed assembly.
        /// </summary>
        private static readonly AssemblyName assemblyName =
            (assembly != null) ? assembly.GetName() : null;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: Create a totally fake plugin without any commands.  This is
        //       only used when verifying the server license certificate.
        //
        /// <summary>
        /// The fake, command-less plugin used only when verifying the server
        /// license certificate.
        /// </summary>
        private static readonly IPlugin plugin = new Enterprise(
            Utility.CreatePluginData(appDomain, assembly, assemblyName,
            GetDateTime(), GetFileName(), typeof(Enterprise).Name,
            Utility.GetAssemblyUri(assembly), Utility.GetAssemblyUri(
            assembly, "update"), ClientData.Empty, PluginFlags.None)
        );
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Assembly Helper Methods
        /// <summary>
        /// Gets the file name of the Kapok assembly.
        /// </summary>
        /// <returns>
        /// The assembly file name.
        /// </returns>
        private static string GetFileName()
        {
            try
            {
                if (assembly == null)
                    return null;

                return assembly.Location; /* throw */
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(WebGlobalState).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the build date/time of the Kapok assembly.
        /// </summary>
        /// <returns>
        /// The assembly date/time, or null when unavailable.
        /// </returns>
        private static DateTime? GetDateTime()
        {
            try
            {
                if (assembly == null)
                    return null;

                string location = assembly.Location; /* throw */

                if (String.IsNullOrEmpty(location))
                    return null;

                return File.GetLastAccessTimeUtc(
                    Path.GetDirectoryName(location)); /* throw */
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(WebGlobalState).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);

                return null;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Global State Access Methods
        /// <summary>
        /// Gets the application domain associated with the Kapok assembly.
        /// </summary>
        /// <returns>
        /// The application domain.
        /// </returns>
        public static AppDomain GetAppDomain()
        {
            return appDomain;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the Kapok managed assembly.
        /// </summary>
        /// <returns>
        /// The assembly.
        /// </returns>
        public static Assembly GetAssembly()
        {
            return assembly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the Kapok managed assembly.
        /// </summary>
        /// <returns>
        /// The assembly name.
        /// </returns>
        public static AssemblyName GetAssemblyName()
        {
            return assemblyName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the fake plugin used for license verification.
        /// </summary>
        /// <returns>
        /// The plugin.
        /// </returns>
        public static IPlugin GetPlugin()
        {
            return plugin;
        }
        #endregion
    }
}
