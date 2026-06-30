/*
 * EnvironmentOps.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Eagle Enterprise Edition: Kapok SDK v1.0
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

#if KAPOK
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
#else
using System.Runtime.InteropServices;
#endif

#if KAPOK
using EnvironmentPair = Eagle._Interfaces.Public.IAnyPair<
    string, Kapok.Components.Shared.SettingDataType>;
#else
using StringList = System.Collections.Generic.List<string>;

using EnvironmentPair = System.Collections.Generic.KeyValuePair<
    string, Kapok.Components.Shared.SettingDataType>;
#endif

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This class provides an abstraction over environment variables and
    /// their associated functionality that are need by the web server.
    /// </summary>
#if KAPOK
    [ObjectId("10aa72ec-795b-4ad3-87c5-3e6542be07a5")]
#else
    [Guid("10aa72ec-795b-4ad3-87c5-3e6542be07a5")]
#endif
    internal static class EnvironmentOps
    {
        /// <summary>
        /// Attempts to determine and return the value associated with the
        /// specified named environment variable.
        /// </summary>
        /// <param name="name">
        /// The name of the environment variable to query.
        /// </param>
        /// <returns>
        /// The value of the environment variable -OR- null if it cannot be
        /// determined.
        /// </returns>
        public static string GetVariableValue(
            string name /* in */
            )
        {
#if KAPOK
            return Utility.GetEnvironmentVariable(name);
#else
            return Environment.GetEnvironmentVariable(name);
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine if the specified environment variable is
        /// currently available.
        /// </summary>
        /// <param name="name">
        /// The name of the environment variable to check.
        /// </param>
        /// <returns>
        /// Non-zero if the specified environment variable is available.
        /// </returns>
        public static bool HaveVariableValue(
            string name /* in */
            )
        {
#if KAPOK
            return Utility.GetEnvironmentVariable(name) != null;
#else
            return Environment.GetEnvironmentVariable(name) != null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to format (convert?) the specified environment variable
        /// name into the name of a setting that may (or may not) be present
        /// in the application configuration.
        /// </summary>
        /// <param name="name">
        /// The name of the environment variable.
        /// </param>
        /// <returns>
        /// The formatted application configuration name -OR- null if it
        /// cannot be determined.
        /// </returns>
        public static string FormatName(
            string name /* in */
            )
        {
            if (String.IsNullOrEmpty(name))
                return null;

            return String.Format(
                "Server{0}_{1}", typeof(Environment).Name, name);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to convert the specified collection of environment
        /// name / value pairs into a list of environment variable names
        /// (i.e. without their associated values).
        /// </summary>
        /// <param name="environment">
        /// The collection of environment variable name / value pairs to
        /// extract the names from.
        /// </param>
        /// <returns>
        /// The collection of environment variable names -OR- null if it
        /// cannot be determined.
        /// </returns>
        public static IEnumerable<string> GetVariableNames(
            IEnumerable<EnvironmentPair> environment /* in */
            )
        {
            if (environment == null)
                return null;

            StringList names = new StringList();

            foreach (EnvironmentPair anyPair in environment)
            {
#if KAPOK
                if (anyPair == null)
                    continue;

                names.Add(anyPair.X);
#else
                names.Add(anyPair.Key);
#endif
            }

            return names;
        }
    }
}
