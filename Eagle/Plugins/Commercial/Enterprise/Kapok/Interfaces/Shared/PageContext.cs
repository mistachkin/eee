/*
 * PageContext.cs --
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

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

namespace Kapok.Interfaces.Shared
{
    /// <summary>
    /// This interface represents a logical page context for use during web
    /// server request processing.
    /// </summary>
#if KAPOK
    [ObjectId("f9fb91ec-05c2-4a07-a97d-485ff60c3b71")]
#else
    [Guid("f9fb91ec-05c2-4a07-a97d-485ff60c3b71")]
#endif
    public interface IPageContext : IDisposable
    {
        /// <summary>
        /// Returns the logical request object for this logical page context.
        /// </summary>
        /// <returns>
        /// The logical request object -OR- null if it cannot be determined.
        /// </returns>
        IRequest GetRequest();

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the logical response object for this logical page context.
        /// </summary>
        /// <returns>
        /// The logical response object -OR- null if it cannot be determined.
        /// </returns>
        IResponse GetResponse();

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the logical identity context for this logical page
        /// context.  The returned context is the only sanctioned way for
        /// page code to inspect the authenticated user; callers MUST NOT
        /// reach into the host runtime's principal type directly.
        /// </summary>
        /// <returns>
        /// The logical identity context -OR- null if it cannot be
        /// determined.  The returned object is never an authenticated
        /// identity for an anonymous request, but it is also not null in
        /// that case: callers should check
        /// <see cref="IIdentityContext.IsAuthenticated" />.
        /// </returns>
        IIdentityContext GetIdentity();
    }
}
