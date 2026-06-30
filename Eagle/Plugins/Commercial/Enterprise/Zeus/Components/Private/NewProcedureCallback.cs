/*
 * NewProcedureCallback.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using Obfuscated = Zeus.Procedures.Obfuscated;

namespace Zeus.Components.Private
{
    /// <summary>
    /// Implements the callback that intercepts the creation of new
    /// procedures for the Zeus plugin.  Each newly created procedure is
    /// wrapped in an <see cref="Obfuscated" /> instance, transparently
    /// encrypting its body at rest.  When isolated interpreters or plugins
    /// are enabled, it derives from <c>ScriptMarshalByRefObject</c> so
    /// it can be invoked across application domain boundaries.
    /// </summary>
    [ObjectId("1174b8ad-129e-4623-83b1-3bd02c2699f4")]
    internal sealed class NewProcedureCallback
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        : ScriptMarshalByRefObject, INewProcedureCallback
#endif
    {
        #region Private Data
        /// <summary>
        /// The plugin used as the owner when constructing the obfuscated
        /// procedures produced by this callback.
        /// </summary>
        private IPlugin plugin;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new <see cref="NewProcedureCallback" /> instance for
        /// the specified plugin.
        /// </summary>
        /// <param name="plugin">
        /// The plugin used as the owner of the obfuscated procedures this
        /// callback creates.
        /// </param>
        public NewProcedureCallback(
            IPlugin plugin /* in */
            )
        {
            this.plugin = plugin;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region INewProcedureCallback Members
        /// <summary>
        /// Creates a new procedure from the supplied procedure data, wrapping
        /// it in an <see cref="Obfuscated" /> instance so its body is
        /// encrypted at rest.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter creating the procedure; not used by this
        /// implementation.
        /// </param>
        /// <param name="procedureData">
        /// The data describing the procedure to create.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The newly created obfuscated procedure.
        /// </returns>
        public IProcedure NewProcedure(
            Interpreter interpreter,      /* in: NOT USED */
            IProcedureData procedureData, /* in */
            ref Result error              /* out */
            )
        {
            CheckDisposed();

            return new Obfuscated(procedureData, plugin);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this instance has been disposed.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Throws an exception if this instance has already been disposed.
        /// </summary>
        /// <exception cref="InterpreterDisposedException">
        /// Thrown if this instance has been disposed and disposed-object
        /// checking is enabled.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, false))
            {
                throw new InterpreterDisposedException(
                    typeof(NewProcedureCallback));
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from
        /// <see cref="IDisposable.Dispose" />; zero if it is being called
        /// from the finalizer.
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

                    plugin = null; /* NOT OWNED: DO NOT DISPOSE */
                }

                //////////////////////////////////////
                // release unmanaged resources here...
                //////////////////////////////////////

                disposed = true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        /// <summary>
        /// Finalizes an instance of the <see cref="NewProcedureCallback" />
        /// class.
        /// </summary>
        ~NewProcedureCallback()
        {
            Dispose(false);
        }
        #endregion
    }
}
