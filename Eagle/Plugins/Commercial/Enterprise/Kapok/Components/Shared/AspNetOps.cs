/*
 * AspNetOps.cs --
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
using System.Collections.Specialized;

#if NET_STANDARD_20
using System.Net;
#endif

#if !KAPOK
using System.Runtime.InteropServices;
#endif

using System.Security.Principal;
using System.Web;

#if NET_STANDARD_20 && NET_CORE_REFERENCES
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Http;
#endif

#if KAPOK
using Eagle._Attributes;
#endif

using Kapok.Interfaces.Shared;

#if NET_STANDARD_20
using NameAndValuesPair = System.Collections.Generic.KeyValuePair<
    string, Microsoft.Extensions.Primitives.StringValues>;
#endif

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This class provides a simple abstraction layer that hides differences
    /// between the "legacy" ASP.NET classes and their ASP.NET Core cousins.
    /// </summary>
#if KAPOK
    [ObjectId("19309a53-e7e6-42e7-adae-ca6ef8cfb71d")]
#else
    [Guid("19309a53-e7e6-42e7-adae-ca6ef8cfb71d")]
#endif
    internal static class AspNetOps
    {
        #region HttpContext Support Methods
#if !NET_STANDARD_20
        /// <summary>
        /// This method returns the <see cref="HttpContext" /> instance
        /// associated with the current request, if any.
        /// </summary>
        /// <returns>
        /// The <see cref="HttpContext" /> instance associated with the
        /// current request -OR- null if it cannot be determined.
        /// </returns>
        public static HttpContext GetHttpContext()
        {
            return HttpContext.Current;
        }
#endif

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method returns the <see cref="HttpRequest" /> associated with
        /// the current request, if any.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> instance associated with the
        /// current request.  This parameter may be null.
        /// </param>
        /// <returns>
        /// The <see cref="HttpRequest" /> instance associated with the
        /// current request -OR- null if it cannot be determined.
        /// </returns>
        public static HttpRequest GetHttpRequest(
            HttpContext context /* in */
            )
        {
            if (context == null)
                return null;

            return context.Request;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This method returns the <see cref="HttpResponse" /> associated with
        /// the current request, if any.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> instance associated with the
        /// current request.  This parameter may be null.
        /// </param>
        /// <returns>
        /// The <see cref="HttpResponse" /> instance associated with the
        /// current request -OR- null if it cannot be determined.
        /// </returns>
        public static HttpResponse GetHttpResponse(
            HttpContext context /* in */
            )
        {
            if (context == null)
                return null;

            return context.Response;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to create a logical page context that acts as a container
        /// for the associated <see cref="HttpContext" />,
        /// <see cref="HttpRequest" />, and <see cref="HttpResponse" />
        /// instances for the current request.
        /// </summary>
        /// <param name="context">
        /// This should be the <see cref="HttpContext" /> associated with the
        /// current request.  This parameter may be null; however, in certain
        /// cases this may cause the return value to be null as well.
        /// </param>
        /// <param name="contentType">
        /// The optional HTTP content type that will be used for the eventual
        /// response, if any.  This parameter may be null.
        /// </param>
        /// <returns>
        /// The new logical page context -OR- null if it cannot be created.
        /// </returns>
        public static IPageContext CreatePageContext(
            HttpContext context, /* in: OPTIONAL */
            string contentType   /* in: OPTIONAL */
            )
        {
            if (context != null)
            {
                return new PageContext(
                    Request.Create(context, false),
                    Response.Create(context, contentType),
                    new IdentityContext(GetPrincipal(context)));
            }
            else
            {
#if !NET_STANDARD_20
                return new PageContext(
                    Request.Create(false),
                    Response.Create(contentType),
                    new IdentityContext(GetPrincipal(null)));
#else
                return null;
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the underlying principal associated with the specified
        /// <see cref="HttpContext" />, if any.  On .NET Framework the host
        /// thread's principal is consulted when the context lacks an
        /// explicit user.
        /// </summary>
        /// <param name="context">
        /// The <see cref="HttpContext" /> associated with the current
        /// request.  This parameter may be null.
        /// </param>
        /// <returns>
        /// The <see cref="IPrincipal" />
        /// associated with the request -OR- null when none is available.
        /// </returns>
        public static IPrincipal GetPrincipal(
            HttpContext context /* in: OPTIONAL */
            )
        {
#if !NET_STANDARD_20
            if (context != null)
            {
                IPrincipal principal = context.User;

                if (principal != null)
                    return principal;
            }

            return System.Threading.Thread.CurrentPrincipal;
#else
            if (context == null)
                return null;

            return context.User;
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region HttpRequest Support Methods
        /// <summary>
        /// Attempts to determine and return the IP address for the current
        /// request.
        /// </summary>
        /// <param name="context">
        /// This should be the <see cref="HttpContext" /> associated with the
        /// current request.  This parameter may not be null.
        /// </param>
        /// <param name="request">
        /// The <see cref="HttpRequest" /> associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <returns>
        /// The IP address for the current request -OR- null if it cannot be
        /// determined.
        /// </returns>
        public static string GetAddress(
            HttpContext context, /* in */
            HttpRequest request  /* in */
            )
        {
#if !NET_STANDARD_20
            if (request == null)
                return null;

            return request.UserHostAddress;
#else
            if (context == null)
                return null;

            ConnectionInfo connectionInfo = context.Connection;

            if (connectionInfo == null)
                return null;

            IPAddress address = connectionInfo.RemoteIpAddress;

            if (address == null)
                return null;

            return address.ToString();
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine and return the virtual path for the current
        /// request.
        /// </summary>
        /// <param name="request">
        /// The <see cref="HttpRequest" /> associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <returns>
        /// The virtual path for the current request -OR- null if it cannot be
        /// determined.
        /// </returns>
        public static string GetPath(
            HttpRequest request /* in */
            )
        {
            if (request == null)
                return null;

#if !NET_STANDARD_20
            return request.Path;
#else
            PathString pathString = request.Path;

            return pathString.HasValue ? pathString.Value : null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine and return the HTTP method (e.g.
        /// <c>GET</c>, <c>POST</c>) for the current request.
        /// </summary>
        /// <param name="request">
        /// The <see cref="HttpRequest" /> associated with the current
        /// request.  This parameter may be null.
        /// </param>
        /// <returns>
        /// The upper-case HTTP method for the current request -OR-
        /// the empty string if it cannot be determined.
        /// </returns>
        public static string GetMethod(
            HttpRequest request /* in */
            )
        {
            if (request == null)
                return String.Empty;

#if !NET_STANDARD_20
            string method = request.HttpMethod;
#else
            string method = request.Method;
#endif

            if (method == null)
                return String.Empty;

            return method.ToUpperInvariant();
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to clone the specified collection of name / value pairs
        /// used with instances of the <see cref="HttpRequest" /> class.
        /// </summary>
        /// <param name="collection">
        /// The collection to be cloned.
        /// </param>
        /// <returns>
        /// The newly cloned collection of name / value pairs, which will be
        /// a deep copy that refers to the same underlying strings, which are
        /// also immutable.
        /// </returns>
        public static NameValueCollection CopyNamesAndValues(
            NameValueCollection collection /* in */
            )
        {
            NameValueCollection result = HttpUtility.ParseQueryString(
                String.Empty); /* HttpValueCollection */

            if (collection != null)
                result.Add(collection);

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

#if NET_STANDARD_20
        /// <summary>
        /// Attempts to create a <see cref="NameValueCollection" /> instance
        /// based on the specified collection of key / value pairs.  This is
        /// only done for the .NET (Core?) runtime.
        /// </summary>
        /// <param name="collection">
        /// The collection of key / value pairs to use when preparing the
        /// final result.  This parameter may not be null.
        /// </param>
        /// <returns>
        /// The newly created collection of name / value pairs, which will be
        /// a deep copy that refers to the same underlying strings, which are
        /// also immutable.
        /// </returns>
        private static NameValueCollection GetNamesAndValues(
            IEnumerable<NameAndValuesPair> collection /* in */
            )
        {
            if (collection == null)
                return null;

            NameValueCollection result = CopyNamesAndValues(null);

            foreach (NameAndValuesPair pair in collection)
                result.Add(pair.Key, pair.Value.ToString());

            return result;
        }
#endif

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine and return the collection of name / value
        /// pairs associated with the form data for the current request.
        /// </summary>
        /// <param name="request">
        /// The <see cref="HttpRequest" /> associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <returns>
        /// The collection of name / value pairs associated with form data for
        /// the current request. -OR- null if it cannot be determined.
        /// </returns>
        public static NameValueCollection GetForm(
            HttpRequest request /* in */
            )
        {
            if (request == null)
                return null;

#if !NET_STANDARD_20
            return request.Form;
#else
            try
            {
                return GetNamesAndValues(request.Form);
            }
            catch
            {
                return null;
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to determine and return the collection of name / value
        /// pairs associated with the query string for the current request.
        /// </summary>
        /// <param name="request">
        /// The <see cref="HttpRequest" /> associated with the current request.
        /// This parameter may not be null.
        /// </param>
        /// <returns>
        /// The collection of name / value pairs associated with query string
        /// for the current request. -OR- null if it cannot be determined.
        /// </returns>
        public static NameValueCollection GetQuery(
            HttpRequest request /* in */
            )
        {
            if (request == null)
                return null;

#if !NET_STANDARD_20
            return request.QueryString;
#else
            return GetNamesAndValues(request.Query);
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Attempts to collect every file uploaded as part of the
        /// current <c>multipart/form-data</c> request body, wrapping
        /// each in an <see cref="IUploadedFile" />.
        /// </summary>
        /// <param name="request">
        /// The <see cref="HttpRequest" /> associated with the current
        /// request.  This parameter may be null.
        /// </param>
        /// <returns>
        /// A non-null list of <see cref="IUploadedFile" /> wrappers;
        /// empty when the request carried no uploads or when the
        /// content type does not support file uploads.
        /// </returns>
        public static IList<IUploadedFile> GetFiles(
            HttpRequest request /* in: OPTIONAL */
            )
        {
            IList<IUploadedFile> result = new List<IUploadedFile>();

            if (request == null)
                return result;

#if !NET_STANDARD_20
            HttpFileCollection collection = request.Files;

            if (collection == null)
                return result;

            string[] keys = collection.AllKeys;

            if (keys == null)
                return result;

            for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
            {
                string key = keys[keyIndex];

                if (key == null)
                    continue;

                HttpPostedFile postedFile = collection[key];

                if (postedFile == null)
                    continue;

                result.Add(new UploadedFile(key, postedFile));
            }
#else
            //
            // NOTE: Accessing request.Form on a non-form-encoded
            //       request throws; suppress that and treat the
            //       request as carrying no files.
            //
            IFormCollection form;

            try
            {
                if (!request.HasFormContentType)
                    return result;

                form = request.Form;
            }
            catch
            {
                return result;
            }

            if (form == null)
                return result;

            IFormFileCollection collection = form.Files;

            if (collection == null)
                return result;

            foreach (IFormFile formFile in collection)
            {
                if (formFile == null)
                    continue;

                result.Add(new UploadedFile(formFile));
            }
#endif

            return result;
        }
        #endregion
    }
}
