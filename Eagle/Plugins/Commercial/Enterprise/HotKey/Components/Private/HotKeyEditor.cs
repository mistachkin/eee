/*
 * HotKeyEditor.cs --
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
using HotKey.Forms;
using HotKey.Interfaces.Private;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Bridges an asynchronous operation back to a hot-key editor result.
    /// When the asynchronous work completes, this callback applies the
    /// captured result to the associated editor and closes the busy form.
    /// </summary>
    [ObjectId("6507787c-fac4-4cb8-a426-0e00f47b2920")]
    internal sealed class HotKeyEditor :
        ScriptMarshalByRefObject, IAsynchronousCallback
    {
        #region Private Data
        /// <summary>
        /// The editor result that receives the outcome of the asynchronous
        /// operation.
        /// </summary>
        private IHotKeyEditorResult hotKeyEditorResult;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new <see cref="HotKeyEditor" /> callback bound to the
        /// specified editor result.
        /// </summary>
        /// <param name="hotKeyEditorResult">
        /// The editor result to update when the asynchronous operation
        /// completes.
        /// </param>
        public HotKeyEditor(
            IHotKeyEditorResult hotKeyEditorResult /* in */
            )
        {
            this.hotKeyEditorResult = hotKeyEditorResult;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IAsynchronousCallback Members
        /// <summary>
        /// Invoked when the asynchronous operation completes.  Applies the
        /// context's return code, result, and error line to the bound editor
        /// (appending or replacing per the context's caller data) and always
        /// closes the busy form afterward.
        /// </summary>
        /// <param name="context">
        /// The context describing the completed asynchronous operation.
        /// </param>
        public void Invoke(
            IAsynchronousContext context /* in */
            ) /* AsynchronousCallback */
        {
            try
            {
                if (context == null)
                    return;

                IClientData clientData = context.ClientData;

                if (clientData == null)
                    return;

                bool append = false;

                if (clientData.Data is bool)
                    append = (bool)clientData.Data;

                if (hotKeyEditorResult == null)
                    return;

                try
                {
                    hotKeyEditorResult.ModifyTextFromResult(
                        context.ReturnCode, context.Result,
                        context.ErrorLine, append); /* throw */
                }
                catch (Exception e)
                {
                    LogOps.Complain(ReturnCode.Error, e);
                }
            }
            finally
            {
                BaseForm.CloseOneOrAll(typeof(BusyForm), 0, false);
            }
        }
        #endregion
    }
}
