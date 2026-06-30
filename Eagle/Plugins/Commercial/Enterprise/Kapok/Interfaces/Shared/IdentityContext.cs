/*
 * IdentityContext.cs --
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
#else
using System.Runtime.InteropServices;
#endif

namespace Kapok.Interfaces.Shared
{
    /// <summary>
    /// This interface represents the authenticated identity associated with a
    /// logical page context, exposing only what page-level code needs in order
    /// to make authorization decisions.  Concrete implementations wrap either
    /// the .NET Framework <see cref="System.Security.Principal.IPrincipal" />
    /// or the ASP.NET Core
    /// <see cref="System.Security.Claims.ClaimsPrincipal" />; callers must
    /// never reach past this abstraction to those types directly.
    /// </summary>
#if KAPOK
    [ObjectId("c1d8e4a2-3f6b-4d7c-8e9f-a5b1c2d3e4f5")]
#else
    [Guid("c1d8e4a2-3f6b-4d7c-8e9f-a5b1c2d3e4f5")]
#endif
    public interface IIdentityContext : IDisposable
    {
        /// <summary>
        /// Non-zero when the underlying principal carries an authenticated
        /// identity.  Page code MUST check this before relying on any other
        /// member of this interface.
        /// </summary>
        bool IsAuthenticated { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The display name of the authenticated user, or null when the
        /// identity is anonymous or no name claim is available.
        /// </summary>
        string Name { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The stable, opaque subject identifier of the authenticated user
        /// (e.g. the value of the OpenID Connect "sub" claim or the Azure AD
        /// object identifier).  Null when no such identifier is available.
        /// </summary>
        string SubjectId { get; }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the set of role names the authenticated user belongs to.
        /// </summary>
        /// <returns>
        /// An enumerable of role names -OR- an empty enumerable when no
        /// roles are present.  This method MUST NOT return null.
        /// </returns>
        IEnumerable<string> GetRoles();

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the first claim value found for the specified claim type,
        /// or null when no matching claim is present on the underlying
        /// principal.
        /// </summary>
        /// <param name="claimType">
        /// The fully-qualified claim type to look up (e.g. one of the
        /// <see cref="System.Security.Claims.ClaimTypes" /> well-known
        /// values, or a custom claim URI).  This parameter may not be null.
        /// </param>
        /// <returns>
        /// The first matching claim value -OR- null when no matching claim
        /// is present.
        /// </returns>
        string GetClaim(string claimType);

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns non-zero when the authenticated user is a member of the
        /// specified role.  An anonymous identity is never a member of any
        /// role.
        /// </summary>
        /// <param name="roleName">
        /// The role name to test.  This parameter may not be null.
        /// </param>
        /// <returns>
        /// Non-zero when the user is a member of the role; zero otherwise.
        /// </returns>
        bool HasRole(string roleName);
    }
}
