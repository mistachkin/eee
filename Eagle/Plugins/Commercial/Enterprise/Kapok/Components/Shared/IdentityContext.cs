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

#if NET_STANDARD_20
using System.Security.Claims;
#endif

using System.Security.Principal;

#if KAPOK
using Eagle._Attributes;
using Eagle._Components.Public;
#else
using System.Runtime.InteropServices;
#endif

using Kapok.Interfaces.Shared;

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This instanced class is the default implementation of
    /// <see cref="IIdentityContext" />.  It wraps an
    /// <see cref="IPrincipal" /> on the .NET Framework and the .NET Core
    /// <c>ClaimsPrincipal</c> derived type that the host
    /// authentication middleware places on
    /// <c>HttpContext.User</c>.
    /// </summary>
#if KAPOK
    [ObjectId("d2f9b5c3-4e8a-4f1b-9c2d-6e3f4a5b6c7d")]
#else
    [Guid("d2f9b5c3-4e8a-4f1b-9c2d-6e3f4a5b6c7d")]
#endif
    internal sealed class IdentityContext : IIdentityContext
    {
        #region Private Data
        /// <summary>
        /// The underlying principal exposed by the host runtime.  May be
        /// null when no principal is available (e.g. when running outside
        /// of a request that has passed through authentication
        /// middleware).
        /// </summary>
        private IPrincipal principal;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new identity context wrapping the specified
        /// underlying principal.
        /// </summary>
        /// <param name="principal">
        /// The host-supplied principal.  This parameter may be null;
        /// callers MUST treat the resulting context as anonymous.
        /// </param>
        public IdentityContext(
            IPrincipal principal /* in: OPTIONAL */
            )
        {
            this.principal = principal;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Helper Methods
        /// <summary>
        /// Returns the <see cref="IIdentity" /> instance associated with the
        /// underlying principal, or null when no such identity is
        /// available.
        /// </summary>
        /// <returns>
        /// The <see cref="IIdentity" /> for the wrapped principal -OR- null
        /// when the principal itself is null.
        /// </returns>
        private IIdentity GetIdentity()
        {
            if (principal == null)
                return null;

            return principal.Identity;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentityContext Members
        /// <summary>
        /// Non-zero when the underlying principal carries an authenticated
        /// identity.
        /// </summary>
        public bool IsAuthenticated
        {
            get
            {
                CheckDisposed();

                IIdentity identity = GetIdentity();

                if (identity == null)
                    return false;

                return identity.IsAuthenticated;
            }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The display name of the authenticated user, or null when the
        /// identity is anonymous or no name claim is available.
        /// </summary>
        public string Name
        {
            get
            {
                CheckDisposed();

                IIdentity identity = GetIdentity();

                if (identity == null)
                    return null;

                return identity.Name;
            }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The stable, opaque subject identifier of the authenticated user.
        /// </summary>
        public string SubjectId
        {
            get
            {
                CheckDisposed();

#if NET_STANDARD_20
                return GetClaim(ClaimTypes.NameIdentifier);
#else
                //
                // NOTE: The .NET Framework "legacy" path lacks claims
                //       pipeline; callers needing a stable subject id
                //       should retrieve it from a custom store.
                //
                return null;
#endif
            }
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the set of role names the authenticated user belongs to.
        /// </summary>
        /// <returns>
        /// An enumerable of role names; never null.
        /// </returns>
        public IEnumerable<string> GetRoles()
        {
            CheckDisposed();

            List<string> roles = new List<string>();

#if NET_STANDARD_20
            ClaimsPrincipal localPrincipal = principal as ClaimsPrincipal;

            if (localPrincipal != null)
            {
                foreach (Claim claim in localPrincipal.FindAll(ClaimTypes.Role))
                {
                    if (claim == null)
                        continue;

                    string value = claim.Value;

                    if (value != null)
                        roles.Add(value);
                }
            }
#endif

            return roles;
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns the first claim value found for the specified claim
        /// type.
        /// </summary>
        /// <param name="claimType">
        /// The fully-qualified claim type to look up.  May not be null.
        /// </param>
        /// <returns>
        /// The first matching claim value -OR- null when no matching claim
        /// is present.
        /// </returns>
        public string GetClaim(
            string claimType /* in */
            )
        {
            CheckDisposed();

            if (claimType == null)
                return null;

#if NET_STANDARD_20
            ClaimsPrincipal claimsPrincipal = principal as ClaimsPrincipal;

            if (claimsPrincipal == null)
                return null;

            Claim claim = claimsPrincipal.FindFirst(claimType);

            if (claim == null)
                return null;

            return claim.Value;
#else
            return null;
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Returns non-zero when the authenticated user is a member of the
        /// specified role.
        /// </summary>
        /// <param name="roleName">
        /// The role name to test.  May not be null.
        /// </param>
        /// <returns>
        /// Non-zero when the user is a member of the role; zero otherwise.
        /// </returns>
        public bool HasRole(
            string roleName /* in */
            )
        {
            CheckDisposed();

            if (roleName == null)
                return false;

            if (principal == null)
                return false;

            return principal.IsInRole(roleName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// Disposes this identity context.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Tracks whether this identity context has been disposed.
        /// </summary>
        private bool disposed;

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Verifies that this identity context has not been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed)
            {
#if KAPOK
                if (Engine.IsThrowOnDisposed(null, false))
#endif
                {
                    throw new ObjectDisposedException(
                        typeof(IdentityContext).Name);
                }
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Performs cleanup of any resources held by this identity context.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero when this identity context is being explicitly disposed
        /// via <see cref="Dispose()" />.
        /// </param>
        private /* protected virtual */ void Dispose(
            bool disposing /* in */
            )
        {
            if (!disposed)
            {
                if (disposing)
                {
                    ////////////////////////////////////
                    // dispose managed resources here...
                    ////////////////////////////////////

                    //
                    // NOTE: The principal is owned by the host runtime; we
                    //       only drop our reference.
                    //
                    principal = null;
                }

                //////////////////////////////////////
                // release unmanaged resources here...
                //////////////////////////////////////

                //
                // NOTE: This object is now disposed.
                //
                disposed = true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        /// <summary>
        /// Finalizer; called by the CLR runtime when this identity context
        /// is being collected.
        /// </summary>
        ~IdentityContext()
        {
            Dispose(false);
        }
        #endregion
    }
}
