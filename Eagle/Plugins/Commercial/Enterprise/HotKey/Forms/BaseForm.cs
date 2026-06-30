/*
 * BaseForm.cs --
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
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using HotKey.Components.Private;
using HotKey.Interfaces.Private;

using EventWaitHandleDictionary =
    System.Collections.Generic.Dictionary<
        int, System.Threading.EventWaitHandle>;

namespace HotKey.Forms
{
    /// <summary>
    /// Provides the base Windows Forms class for the plugin's forms.  It
    /// implements thread-safe identity accessors and safe-close support,
    /// tracks the open forms and a per-form shown event, and supports pattern
    /// matching and counting/closing forms by type.
    /// </summary>
    [ObjectId("dc2064e1-0d74-4949-9cd8-dea325baae4b")]
    internal class BaseForm : Form, IHotKeyForm
    {
        #region Private Static Data
        /// <summary>
        /// The object used to synchronize access to the shared static data.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The cached reflected method used to add a form to the application's
        /// open-forms collection.
        /// </summary>
        private static MethodInfo openFormsInternalAdd;
        /// <summary>
        /// The cached reflected method used to remove a form from the
        /// application's open-forms collection.
        /// </summary>
        private static MethodInfo openFormsInternalRemove;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The per-id wait handles signaled when a form is shown.
        /// </summary>
        private static readonly EventWaitHandleDictionary shownEvents =
            new EventWaitHandleDictionary();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The reentrancy count tracking in-progress safe-close operations.
        /// </summary>
        private int safeClose;
        /// <summary>
        /// The wait handle signaled when this form is shown.
        /// </summary>
        private EventWaitHandle shownEvent;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        //
        // WARNING: This constructor is only intended to be used by the VS
        //          WinForms designer IDE.
        //
        /// <summary>
        /// Constructs a new, default <see cref="BaseForm" /> instance.
        /// </summary>
        public BaseForm()
            : this(FormId.GetNext(), null, null)
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new <see cref="BaseForm" /> with the specified id,
        /// interpreter, and result variable name.
        /// </summary>
        /// <param name="id">
        /// The form id.
        /// </param>
        /// <param name="interpreter">
        /// The associated interpreter.
        /// </param>
        /// <param name="varName">
        /// The variable that receives the form id, if any.
        /// </param>
        public BaseForm(
            int id,                  /* in */
            Interpreter interpreter, /* in */
            string varName           /* in */
            )
            : base()
        {
            this.id = id;
            this.shownEvent = new AutoResetEvent(false);

            ///////////////////////////////////////////////////////////////////

            MaybeSetVariable(interpreter, varName);

            ///////////////////////////////////////////////////////////////////

            AddShownEvent();

            ///////////////////////////////////////////////////////////////////

            this.Disposed += new EventHandler(BaseForm_Disposed);
            this.Shown += new EventHandler(BaseForm_Shown);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Creates and registers this form's shown wait handle.
        /// </summary>
        private void AddShownEvent()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (shownEvents != null)
                    shownEvents.Add(id, shownEvent);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes and disposes this form's shown wait handle.
        /// </summary>
        private void RemoveShownEvent()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (shownEvents != null)
                {
                    /* IGNORED */
                    shownEvents.Remove(id);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signals this form's shown wait handle.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool SignalShownEvent()
        {
            EventWaitHandle @event = Interlocked.CompareExchange(
                ref shownEvent, null, null);

            if (@event == null)
                return false;

            return @event.Set();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the result variable (when supplied) to this form's id.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose variable is set.
        /// </param>
        /// <param name="varName">
        /// The variable to set, if any.
        /// </param>
        private void MaybeSetVariable(
            Interpreter interpreter, /* in */
            string varName           /* in */
            )
        {
            if ((interpreter != null) && (varName != null))
            {
                ReturnCode code;
                Result error = null;

                code = interpreter.SetVariableValue(
                    VariableFlags.None, varName, id.ToString(), null,
                    ref error);

                if (code != ReturnCode.Ok)
                    LogOps.Complain(interpreter, code, error);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
        /// <summary>
        /// Synchronizes this form with the application's open-forms collection
        /// (adding or removing it via the reflected methods).
        /// </summary>
        protected void SynchronizeWithOpenForms()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                //
                // HACK: Why must we do this?  These methods are marked
                //       as "internal"; however, we need to access them
                //       in order to keep things synchronized; therefore,
                //       just use Reflection.  We cache the MethodInfo
                //       objects so that we do not need to look them up
                //       more than once.
                //
                BindingFlags bindingFlags =
                    BindingFlags.Static | BindingFlags.NonPublic;

                if (openFormsInternalAdd == null)
                {
                    openFormsInternalAdd = typeof(Application).GetMethod(
                        "OpenFormsInternalAdd", bindingFlags);
                }

                if (openFormsInternalRemove == null)
                {
                    openFormsInternalRemove = typeof(Application).GetMethod(
                        "OpenFormsInternalRemove", bindingFlags);
                }

                //
                // NOTE: This call to the "Remove" method here is harmless
                //       (and may be necessary in some circumstances).  If
                //       the form is not actually present in the collection,
                //       nothing is done; however, if it is present, it will
                //       prevent us from attempting to add a duplicate form.
                //
                object[] args = { this };

                if (openFormsInternalRemove != null)
                    openFormsInternalRemove.Invoke(null, args);

                if (openFormsInternalAdd != null)
                    openFormsInternalAdd.Invoke(null, args);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Increments the safe-close reentrancy count.
        /// </summary>
        protected void EnterSafeClose()
        {
            Interlocked.Increment(ref safeClose);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Decrements the safe-close reentrancy count.
        /// </summary>
        protected void ExitSafeClose()
        {
            Interlocked.Decrement(ref safeClose);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Waits up to the specified timeout for the form with the given id to
        /// be shown.
        /// </summary>
        /// <param name="id">
        /// The id of the form to wait for.
        /// </param>
        /// <param name="timeout">
        /// The maximum number of milliseconds to wait.
        /// </param>
        /// <returns>
        /// Non-zero when the form was shown within the timeout; otherwise,
        /// zero.
        /// </returns>
        public static bool WaitForShown(
            int id,     /* in */
            int timeout /* in */
            )
        {
            EventWaitHandle shownEvent;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (shownEvents == null)
                    return false;

                if (!shownEvents.TryGetValue(id, out shownEvent))
                    return false;
            }

            if (shownEvent == null)
                return false;

            return shownEvent.WaitOne(timeout);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a snapshot copy of the currently open forms.
        /// </summary>
        /// <returns>
        /// A list of the open forms.
        /// </returns>
        public static IList<Form> CopyOpenForms() /* CANNOT RETURN NULL */
        {
            IList<Form> result = new List<Form>();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                foreach (Form form in Application.OpenForms)
                    result.Add(form);
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Counts the open forms of the specified type, optionally limited to
        /// a specific id.
        /// </summary>
        /// <param name="type">
        /// The form type to count.
        /// </param>
        /// <param name="id">
        /// The form id to match, or zero for all.
        /// </param>
        /// <returns>
        /// The matching count.
        /// </returns>
        public static int CountOneOrAll(
            Type type, /* in */
            int id     /* in */
            )
        {
            int result = 0;

            foreach (Form form in CopyOpenForms())
            {
                IHotKeyForm hotKeyForm = form as IHotKeyForm;

                if (hotKeyForm == null)
                    continue;

                if ((type != null) && !Object.ReferenceEquals(
                        hotKeyForm.GetType(), type))
                {
                    continue;
                }

                if ((id == 0) || (hotKeyForm.SafeId == id))
                    result++;
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the open forms of the specified type, optionally limited to
        /// a specific id.
        /// </summary>
        /// <param name="type">
        /// The form type to close.
        /// </param>
        /// <param name="id">
        /// The form id to match, or zero for all.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to close without waiting.
        /// </param>
        /// <returns>
        /// The number of forms closed.
        /// </returns>
        public static int CloseOneOrAll(
            Type type,        /* in */
            int id,           /* in */
            bool asynchronous /* in */
            )
        {
            try
            {
                IList<Form> forms = CopyOpenForms();
                int result = 0;

                foreach (Form form in forms)
                {
                    IHotKeyForm hotKeyForm = form as IHotKeyForm;

                    if (hotKeyForm == null)
                        continue;

                    //
                    // BUGFIX: If an attempt is made to close the "wrong"
                    //         form type here, a deadlock is possible.
                    //         Currently, all callers to this method want
                    //         to close the BusyForm type.
                    //
                    if ((type != null) && !Object.ReferenceEquals(
                            hotKeyForm.GetType(), type))
                    {
                        continue;
                    }

                    if ((id == 0) || (hotKeyForm.SafeId == id))
                    {
                        if (asynchronous)
                            hotKeyForm.SafeCloseAsynchronous();
                        else
                            hotKeyForm.SafeClose();

                        result++;
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                LogOps.Complain(ReturnCode.Error, e);
            }

            return 0;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Form Event Handlers
        /// <summary>
        /// Handles the disposed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void BaseForm_Disposed(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            if (!disposed)
            {
                //
                // NOTE: First, make sure the event handle is removed
                //       so it cannot be access from any other thread.
                //
                RemoveShownEvent();

                //
                // NOTE: Next, close the event handle itself.
                //
                EventWaitHandle @event = Interlocked.Exchange(
                    ref shownEvent, null);

                if (@event != null)
                {
                    @event.Close();
                    @event = null;
                }

                //
                // NOTE: This form is now disposed.
                //
                disposed = true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the form-shown event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void BaseForm_Shown(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            if (!SignalShownEvent())
            {
                LogOps.Complain(ReturnCode.Error, String.Format(
                    "failed to signal form {0} shown event", id));
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ISafeClose Members
        /// <summary>
        /// Determines whether a safe-close operation is currently in progress.
        /// Thread-safe.
        /// </summary>
        /// <returns>
        /// Non-zero when a safe close is in progress; otherwise, zero.
        /// </returns>
        public bool InSafeClose()
        {
            CheckDisposed();

            return Interlocked.CompareExchange(ref safeClose, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes this form safely, marshaling to its thread and waiting for
        /// completion.  Thread-safe.
        /// </summary>
        public void SafeClose()
        {
            CheckDisposed();

            WinFormsOps.Invoke(this, new DelegateWithNoArgs(delegate()
            {
                EnterSafeClose();

                try
                {
                    Close();
                }
                finally
                {
                    ExitSafeClose();
                }
            }), true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Begins closing this form safely without waiting for completion.
        /// Thread-safe.
        /// </summary>
        public void SafeCloseAsynchronous()
        {
            CheckDisposed();

            WinFormsOps.BeginInvoke(this, new DelegateWithNoArgs(delegate ()
            {
                EnterSafeClose();

                try
                {
                    Close();
                }
                finally
                {
                    ExitSafeClose();
                }
            }), true);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHotKeyForm Members
        /// <summary>
        /// The backing field for the <see cref="SafeId" /> property.
        /// </summary>
        private int id;
        /// <summary>
        /// Gets this form's id in a thread-safe manner.
        /// </summary>
        public int SafeId
        {
            get { CheckDisposed(); return id; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets this form's name in a thread-safe manner.
        /// </summary>
        public string SafeName
        {
            get
            {
                CheckDisposed();

                string name = null;

                if (WinFormsOps.GetName(this, ref name))
                    return name;

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets this form's text (title) in a thread-safe manner.
        /// </summary>
        public string SafeText
        {
            get
            {
                CheckDisposed();

                string text = null;

                if (WinFormsOps.GetText(this, ref text))
                    return text;

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether this form's name or text matches the supplied
        /// pattern.  Thread-safe.
        /// </summary>
        /// <param name="pattern">
        /// The pattern to match.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when matching.
        /// </param>
        /// <param name="exactOnly">
        /// Non-zero to require an exact match.
        /// </param>
        /// <returns>
        /// Non-zero when the form matches; otherwise, zero.
        /// </returns>
        public bool DoesMatch(
            string pattern,          /* in */
            CultureInfo cultureInfo, /* in */
            bool exactOnly           /* in */
            )
        {
            CheckDisposed();

            if (String.IsNullOrEmpty(pattern))
                return false;

            int id = 0;

            if (Value.GetInteger2(
                    pattern, ValueFlags.AnyInteger, cultureInfo,
                    ref id) == ReturnCode.Ok)
            {
                return (id == this.SafeId);
            }

            string name = this.SafeName;

            if ((name != null) &&
                Utility.SystemStringEquals(pattern, name))
            {
                return true;
            }

            string text = this.SafeText;

            if ((text != null) &&
                Utility.SystemStringEquals(pattern, text))
            {
                return true;
            }

            if (!exactOnly)
            {
                if ((name != null) &&
                    Parser.StringMatch(null, name, 0, pattern, 0, false))
                {
                    return true;
                }

                if ((text != null) &&
                    Parser.StringMatch(null, text, 0, pattern, 0, false))
                {
                    return true;
                }
            }

            return false;
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
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, false))
                throw new ObjectDisposedException(typeof(BaseForm).Name);
#endif
        }
        #endregion
    }
}
