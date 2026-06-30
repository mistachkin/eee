/*
 * PageOps.cs --
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
using StringList = System.Collections.Generic.List<string>;
#endif

using Kapok.Interfaces.Shared;

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This class provides a basic abstraction for dealing with (cached?)
    /// input parameters supplied with the current request, either via the
    /// query string and/or the form data.  Differences between the "legacy"
    /// ASP.NET stack and ASP.NET Core should be (largely?) invisible to
    /// callers.
    /// </summary>
#if KAPOK
    [ObjectId("8b10768c-c3c2-46ef-95df-3e1f1efc5240")]
#else
    [Guid("8b10768c-c3c2-46ef-95df-3e1f1efc5240")]
#endif
    internal static class PageOps
    {
        #region Private Constants
#if DEBUG
        /// <summary>
        /// Used to separate the lines of the response.
        /// </summary>
        private static readonly string LineSeparator =
            Environment.NewLine + Environment.NewLine;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Naming Support Methods
        /// <summary>
        /// Attempts to determine and return the base name of the page being
        /// used to render the response.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type" /> instance associated with the calling class,
        /// which should be the page itself.
        /// </param>
        /// <param name="default">
        /// The value that should be returned from this method when the base
        /// name of the page cannot be determined.
        /// </param>
        /// <returns>
        /// The base name of the page associated with the specifie
        /// <see cref="Type" /> -OR- <paramref name="default" /> if it cannot
        /// be determined.
        /// </returns>
        public static string GetScriptName(
            Type type,      /* in */
            string @default /* in */
            )
        {
            //
            // HACK: For the .NET Framework, the name of this type will
            //       be something like "ASP.wrapper_script_cgi".  For
            //       .NET Core, the name of this type will be something
            //       like "Kapok.wrapper.script" (i.e. this will be the
            //       base type for the .NET Framework).
            //
            if (type == null)
                return @default;

            string name;

#if !NET_STANDARD_20
            Type baseType = type.BaseType; /* Kapok.wrapper.script */

            if (baseType == null)
                return @default;

            name = baseType.Namespace; /* Kapok.wrapper */
#else
            name = type.Namespace; /* Kapok.wrapper */
#endif

            if (String.IsNullOrEmpty(name))
                return @default;

            string[] parts = name.Split(Type.Delimiter);

            if ((parts == null) || (parts.Length == 0))
                return @default;

            string lastPart = parts[parts.Length - 1];

            if (String.IsNullOrEmpty(lastPart))
                return @default;

            return Char.ToUpper(lastPart[0]) + ((lastPart.Length > 1) ?
                lastPart.Substring(1).ToLower() : String.Empty);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Request Support Methods
        /// <summary>
        /// Attempts to determine and return the value of the specified input
        /// parameter to the current request.  This may involve looking at the
        /// query string and/or the form data.
        /// </summary>
        /// <param name="request">
        /// The logical request object associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <param name="name">
        /// The name of the input parameter being queried.  This parameter may
        /// not be null.
        /// </param>
        /// <returns>
        /// The value of the input parameter being queried -OR- null if it
        /// cannot be determined.
        /// </returns>
        public static string GetParameter(
            IRequest request, /* in */
            string name       /* in */
            )
        {
            return GetParameter(request, name, true, true);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine and return the value of the specified input
        /// parameter to the current request.  This may involve looking at the
        /// query string and/or the form data.
        /// </summary>
        /// <param name="request">
        /// The logical request object associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <param name="name">
        /// The name of the input parameter being queried.  This parameter may
        /// not be null.
        /// </param>
        /// <param name="useQuery">
        /// Non-zero if the query string should be queried.
        /// </param>
        /// <param name="useForm">
        /// Non-zero if the form data should be queried.
        /// </param>
        /// <returns>
        /// The value of the input parameter being queried -OR- null if it
        /// cannot be determined.
        /// </returns>
        private static string GetParameter(
            IRequest request, /* in */
            string name,      /* in */
            bool useQuery,    /* in */
            bool useForm      /* in */
            )
        {
            if (request == null)
                return null;

            string value = null;

            if (useQuery && (value == null))
                value = GetValue(request.Query, name);

            if (useForm && (value == null))
                value = GetValue(request.Form, name);

            return value;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine and return the value of an input parameter
        /// that may be cached in <paramref name="dictionary" />.
        /// </summary>
        /// <param name="dictionary">
        /// The logical request input parameter dictionary being used to cache
        /// the input parameters associated with the current request.
        /// </param>
        /// <param name="name">
        /// The name of the input parameter to query.
        /// </param>
        /// <returns>
        /// The value of the specified input parameter -OR- null if it cannot
        /// be determined.
        /// </returns>
        private static string GetValue(
            IRequestDictionary dictionary, /* in */
            string name                    /* in */
            )
        {
            if (dictionary == null)
                return null;

            if (String.IsNullOrEmpty(name))
                return null;

            return dictionary[name];
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to add all form data input parameters to the logical list
        /// of arguments for the page rendering the response for the current
        /// request.
        /// </summary>
        /// <param name="request">
        /// The logical request object associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <param name="arguments">
        /// The logical list of arguments to the page.  This parameter may be
        /// null.  If necessary, this list will be created by this method.
        /// </param>
        public static void AddForm(
            IRequest request,        /* in */
            ref StringList arguments /* in, out */
            )
        {
            if (request == null)
                return;

            AddArgument("form", request.Form, ref arguments);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to add all query string input parameters to the logical
        /// list of arguments for the page rendering the response for the
        /// current request.
        /// </summary>
        /// <param name="request">
        /// The logical request object associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <param name="arguments">
        /// The logical list of arguments to the page.  This parameter may be
        /// null.  If necessary, this list will be created by this method.
        /// </param>
        public static void AddQuery(
            IRequest request,        /* in */
            ref StringList arguments /* in, out */
            )
        {
            if (request == null)
                return;

            AddArgument("query", request.Query, ref arguments);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to add all cached input parameters to the logical list
        /// of arguments for the page rendering the response for the current
        /// request.
        /// </summary>
        /// <param name="name">
        /// The name of the input parameter to add.
        /// </param>
        /// <param name="value">
        /// The value of the input parameter to add.
        /// </param>
        /// <param name="arguments">
        /// The logical list of arguments to the page.  This parameter may be
        /// null.  If necessary, this list will be created by this method.
        /// </param>
        public static void AddArgument(
            string name,             /* in */
            string value,            /* in */
            ref StringList arguments /* out */
            )
        {
            if ((name == null) || (value == null))
                return;

            if (arguments == null)
                arguments = new StringList();

            arguments.Add(name);
            arguments.Add(value);
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to add all cached input parameters to the logical list
        /// of arguments for the page rendering the response for the current
        /// request.
        /// </summary>
        /// <param name="name">
        /// The name of the input parameter to add.
        /// </param>
        /// <param name="dictionary">
        /// The logical request input parameter dictionary being used to cache
        /// the input parameters associated with the current request.
        /// </param>
        /// <param name="arguments">
        /// The logical list of arguments to the page.  This parameter may be
        /// null.  If necessary, this list will be created by this method.
        /// </param>
        private static void AddArgument(
            string name,                   /* in */
            IRequestDictionary dictionary, /* in */
            ref StringList arguments       /* out */
            )
        {
            if (dictionary == null)
                return;

            if (arguments == null)
                arguments = new StringList();

            if (name != null)
                arguments.Add(name);

            IEnumerable<string> keys = dictionary.AllKeys;

            if (keys != null)
            {
                StringList list = new StringList();

                foreach (string key in keys)
                {
                    list.Add(key);
                    list.Add(dictionary[key]);
                }

                arguments.Add(list.ToString());
            }
            else
            {
                arguments.Add(dictionary.ToString());
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Response Support Methods
#if KAPOK && DEBUG
        /// <summary>
        /// Attempts to format and render extended error information about the
        /// current request via the specified logical response object.
        /// </summary>
        /// <param name="response">
        /// The logical response object associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <param name="code">
        /// The final <see cref="ReturnCode" /> value associated with rendering
        /// of the calling page for the current request.  If this value is not
        /// <see cref="ReturnCode.Error" />, no work will be performed.
        /// </param>
        /// <param name="errorCode">
        /// The optional short error code associated with some page rendering
        /// failure, if any.  This parameter may be null.
        /// </param>
        /// <param name="errorInfo">
        /// The optional error stacktrace associated with some page rendering
        /// failure, if any.  This parameter may be null.
        /// </param>
        public static void CheckWriteErrorInformation(
            IResponse response, /* in */
            ReturnCode code,    /* in */
            Result errorCode,   /* in: OPTIONAL */
            Result errorInfo    /* in: OPTIONAL */
            )
        {
            if (response == null)
                return;

            if (code != ReturnCode.Error)
                return;

            if (errorCode != null)
            {
                response.Write(LineSeparator);
                response.Write(errorCode);
            }

            if (errorInfo != null)
            {
                response.Write(LineSeparator);
                response.Write(errorInfo);
            }
        }
#endif
        #endregion
    }
}
