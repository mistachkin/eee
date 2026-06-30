/*
 * CertificateScriptOps.cs --
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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

#if !NET_STANDARD_20
using System.Security.AccessControl;
#endif

using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;
using _Directory = System.IO.Directory;

#if TEST
using _Test = Eagle._Tests.Default;
using _Helpers = Eagle._Tests.Default.Helpers;
#endif

using DataOps = Licensing.Components.Private.CertificateDataOps;
using Helpers = Licensing.Components.Private.Commands.Helpers;
using _KeyUsage = Licensing.Components.Private.KeyUsage;

using FileAndOrStreamDataList = System.Collections.Generic.List<
    Licensing.Components.Private.FileAndOrStreamData>;

using ScriptTriplet = Eagle._Components.Public.AnyTriplet<
    string, byte[], string>;

using ScriptList = System.Collections.Generic.List<
    Eagle._Components.Public.AnyTriplet<string, byte[], string>>;

using VariablePair = System.Collections.Generic.KeyValuePair<string, object>;

namespace Licensing.Components.Private
{
    #region Interpreter Settings File Name Helper Delegate
    /// <summary>
    /// Represents a callback used to obtain the interpreter settings file
    /// name associated with the specified plugin.
    /// </summary>
    /// <param name="pluginData">
    /// The plugin for which the settings file name is being obtained. This
    /// value may be null.
    /// </param>
    /// <returns>
    /// The settings file name, or null if one is not available.
    /// </returns>
    [ObjectId("df17f62c-260e-4b6e-a4cb-ea281075dad5")]
    internal delegate string GetFileNameCallback(
        IPluginData pluginData /* in */
    );
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region SharedEventWaitHandle Helper Class
    //
    // HACK: Work around a critical issue with LogicNP Software Crypto
    //       Obfuscator For .NET that causes a TypeLoadException during
    //       assembly load when running on the .NET Core runtime.  The
    //       exception messages thrown are of the form:
    //
    //       "Method 'Clone' in type 'X' from assembly 'Harpy' does not
    //       have an implementation."
    //
    /// <summary>
    /// Provides a <see cref="WaitHandle" /> wrapper around a named event that
    /// can be cloned and identified by name. This type works around an
    /// obfuscation-related <see cref="TypeLoadException" /> seen when running
    /// on the .NET Core runtime.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("ba945a59-cd76-4815-b633-7b3eee1d326c")]
    internal sealed class SharedEventWaitHandle :
            WaitHandle, ICloneable, IIdentifierName
    {
        #region Private Data
        /// <summary>
        /// The underlying named event wrapped by this instance.
        /// </summary>
        private EventWaitHandle @event;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance, creating an unnamed event with the
        /// specified initial state and reset mode.
        /// </summary>
        /// <param name="initialState">
        /// Non-zero if the event should be initially signaled.
        /// </param>
        /// <param name="mode">
        /// The reset mode that controls whether the event resets manually or
        /// automatically.
        /// </param>
        public SharedEventWaitHandle(
            bool initialState,  /* in */
            EventResetMode mode /* in */
            )
        {
            SetupName(null);

            bool createdNew; /* NOT USED */

#if NET_STANDARD_20
            CreateEvent(
                initialState, mode, out createdNew); /* throw */
#else
            CreateEvent(
                initialState, mode, out createdNew, null); /* throw */
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new instance, creating a named event with the
        /// specified initial state and reset mode.
        /// </summary>
        /// <param name="initialState">
        /// Non-zero if the event should be initially signaled.
        /// </param>
        /// <param name="mode">
        /// The reset mode that controls whether the event resets manually or
        /// automatically.
        /// </param>
        /// <param name="name">
        /// The name of the event to create.
        /// </param>
        public SharedEventWaitHandle(
            bool initialState,   /* in */
            EventResetMode mode, /* in */
            string name          /* in */
            )
        {
            SetupName(name);

            bool createdNew; /* NOT USED */

#if NET_STANDARD_20
            CreateEvent(
                initialState, mode, out createdNew); /* throw */
#else
            CreateEvent(
                initialState, mode, out createdNew, null); /* throw */
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new instance, creating a named event and reporting
        /// whether the event was newly created.
        /// </summary>
        /// <param name="initialState">
        /// Non-zero if the event should be initially signaled.
        /// </param>
        /// <param name="mode">
        /// The reset mode that controls whether the event resets manually or
        /// automatically.
        /// </param>
        /// <param name="name">
        /// The name of the event to create.
        /// </param>
        /// <param name="createdNew">
        /// Upon return, non-zero if the named event was created by this call
        /// instead of being opened from an existing one.
        /// </param>
        public SharedEventWaitHandle(
            bool initialState,   /* in */
            EventResetMode mode, /* in */
            string name,         /* in */
            out bool createdNew  /* out */
            )
        {
            SetupName(name);

#if NET_STANDARD_20
            CreateEvent(
                initialState, mode, out createdNew); /* throw */
#else
            CreateEvent(
                initialState, mode, out createdNew,
                null); /* throw */
#endif
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Constructs a new instance, creating a named event with the
        /// specified access control security and reporting whether the event
        /// was newly created.
        /// </summary>
        /// <param name="initialState">
        /// Non-zero if the event should be initially signaled.
        /// </param>
        /// <param name="mode">
        /// The reset mode that controls whether the event resets manually or
        /// automatically.
        /// </param>
        /// <param name="name">
        /// The name of the event to create.
        /// </param>
        /// <param name="createdNew">
        /// Upon return, non-zero if the named event was created by this call
        /// instead of being opened from an existing one.
        /// </param>
        /// <param name="eventSecurity">
        /// The access control security to apply to the named event.
        /// </param>
        public SharedEventWaitHandle(
            bool initialState,                    /* in */
            EventResetMode mode,                  /* in */
            string name,                          /* in */
            out bool createdNew,                  /* out */
            EventWaitHandleSecurity eventSecurity /* in */
            )
        {
            SetupName(name);

            CreateEvent(
                initialState, mode, out createdNew,
                eventSecurity); /* throw */
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new instance that opens an existing named event.
        /// </summary>
        /// <param name="name">
        /// The name of the existing event to open.
        /// </param>
        private SharedEventWaitHandle(
            string name /* in */
            )
            : base()
        {
            SetupName(name);

            OpenEvent(); /* throw */
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// Builds a unique event name based on the current process,
        /// application domain, thread, and tick count, optionally
        /// incorporating the type and hash code of the specified instance.
        /// </summary>
        /// <param name="event">
        /// The instance whose type and hash code are included in the name.
        /// This value may be null.
        /// </param>
        /// <param name="separator">
        /// The character used to separate the components of the name.
        /// </param>
        /// <returns>
        /// The formatted event name.
        /// </returns>
        private static string FormatName(
            SharedEventWaitHandle @event, /* in: OPTIONAL */
            char separator                /* in */
            )
        {
            Type type = null;
            int hashCode = 0;

            if (@event != null)
            {
                type = @event.GetType();
                hashCode = RuntimeHelpers.GetHashCode(@event);
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendFormat(
                "0x{1:X}{0}0x{2:X}{0}0x{3:X}{0}0x{4:X}",
                separator, Utility.GetCurrentProcessId(),
                Utility.GetCurrentAppDomainId(),
                Utility.GetCurrentThreadId(),
                Environment.TickCount);

            if (type != null)
            {
                builder.Append(separator);
                builder.AppendFormat("{0}", type);
            }

            if (hashCode != 0)
            {
                builder.Append(separator);
                builder.AppendFormat("0x{0:X}", hashCode);
            }

            return builder.ToString();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Throws an exception if the event name has not been set.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the event name is null.
        /// </exception>
        private void CheckName()
        {
            if (name == null)
            {
                throw new InvalidOperationException(
                    "event name cannot be null");
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Throws an exception if the underlying event has not been
        /// initialized.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the underlying event is null.
        /// </exception>
        private void CheckEvent()
        {
            if (@event == null)
            {
                throw new InvalidOperationException(
                    "event must be initialized");
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the event name, generating a unique name when one is not
        /// supplied.
        /// </summary>
        /// <param name="name">
        /// The name to use, or null to generate a unique name.
        /// </param>
        private void SetupName(
            string name /* in */
            )
        {
            this.name = (name != null) ?
                name : FormatName(this, Characters.Space);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens the existing named event identified by the current name.
        /// </summary>
        private void OpenEvent()
        {
            CheckName();

            @event = Utility.OpenNamedEvent(name);
        }

        ///////////////////////////////////////////////////////////////////////

#if NET_STANDARD_20
        /// <summary>
        /// Creates the named event identified by the current name.
        /// </summary>
        /// <param name="initialState">
        /// Non-zero if the event should be initially signaled.
        /// </param>
        /// <param name="mode">
        /// The reset mode that controls whether the event resets manually or
        /// automatically.
        /// </param>
        /// <param name="createdNew">
        /// Upon return, non-zero if the named event was created by this call
        /// instead of being opened from an existing one.
        /// </param>
        private void CreateEvent(
            bool initialState,   /* in */
            EventResetMode mode, /* in */
            out bool createdNew  /* out */
            )
        {
            CheckName();

            @event = Utility.CreateNamedEvent(
                initialState, mode, name, out createdNew);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Creates the named event identified by the current name, applying
        /// the specified access control security.
        /// </summary>
        /// <param name="initialState">
        /// Non-zero if the event should be initially signaled.
        /// </param>
        /// <param name="mode">
        /// The reset mode that controls whether the event resets manually or
        /// automatically.
        /// </param>
        /// <param name="createdNew">
        /// Upon return, non-zero if the named event was created by this call
        /// instead of being opened from an existing one.
        /// </param>
        /// <param name="eventSecurity">
        /// The access control security to apply to the named event.
        /// </param>
        private void CreateEvent(
            bool initialState,                    /* in */
            EventResetMode mode,                  /* in */
            out bool createdNew,                  /* out */
            EventWaitHandleSecurity eventSecurity /* in */
            )
        {
            CheckName();

            @event = Utility.CreateNamedEvent(
                initialState, mode, name, out createdNew,
                eventSecurity);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region WaitHandle Methods
        /// <summary>
        /// Closes the underlying named event.
        /// </summary>
        public override void Close()
        {
            CheckDisposed();
            CheckEvent();

            Utility.CloseNamedEvent(ref @event);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Blocks the current thread until the underlying event is signaled,
        /// using a timeout in milliseconds.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The number of milliseconds to wait, or
        /// <see cref="Timeout.Infinite" /> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Non-zero if the event was signaled before the timeout elapsed.
        /// </returns>
        public override bool WaitOne(
            int millisecondsTimeout /* in */
            )
        {
            CheckDisposed();
            CheckEvent();

            return @event.WaitOne(millisecondsTimeout);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Blocks the current thread until the underlying event is signaled,
        /// using a <see cref="TimeSpan" /> timeout.
        /// </summary>
        /// <param name="timeout">
        /// The interval to wait for the event to be signaled.
        /// </param>
        /// <returns>
        /// Non-zero if the event was signaled before the timeout elapsed.
        /// </returns>
        public override bool WaitOne(
            TimeSpan timeout /* in */
            )
        {
            CheckDisposed();
            CheckEvent();

            return @event.WaitOne(timeout);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Blocks the current thread until the underlying event is signaled,
        /// using a timeout in milliseconds and optionally exiting the
        /// synchronization context.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The number of milliseconds to wait, or
        /// <see cref="Timeout.Infinite" /> to wait indefinitely.
        /// </param>
        /// <param name="exitContext">
        /// Non-zero to exit the synchronization context before waiting and
        /// reacquire it afterward.
        /// </param>
        /// <returns>
        /// Non-zero if the event was signaled before the timeout elapsed.
        /// </returns>
        public override bool WaitOne(
            int millisecondsTimeout, /* in */
            bool exitContext         /* in */
            )
        {
            CheckDisposed();
            CheckEvent();

            return @event.WaitOne(millisecondsTimeout, exitContext);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Blocks the current thread until the underlying event is signaled,
        /// using a <see cref="TimeSpan" /> timeout and optionally exiting the
        /// synchronization context.
        /// </summary>
        /// <param name="timeout">
        /// The interval to wait for the event to be signaled.
        /// </param>
        /// <param name="exitContext">
        /// Non-zero to exit the synchronization context before waiting and
        /// reacquire it afterward.
        /// </param>
        /// <returns>
        /// Non-zero if the event was signaled before the timeout elapsed.
        /// </returns>
        public override bool WaitOne(
            TimeSpan timeout, /* in */
            bool exitContext  /* in */
            )
        {
            CheckDisposed();
            CheckEvent();

            return @event.WaitOne(timeout, exitContext);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region EventWaitHandle Methods
#if !NET_STANDARD_20
        /// <summary>
        /// Gets the access control security associated with the underlying
        /// named event.
        /// </summary>
        /// <returns>
        /// The access control security for the underlying event.
        /// </returns>
        public EventWaitHandleSecurity GetAccessControl()
        {
            CheckDisposed();
            CheckEvent();

            return @event.GetAccessControl();
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the underlying event to the non-signaled state.
        /// </summary>
        /// <returns>
        /// Non-zero if the operation succeeds.
        /// </returns>
        public bool Reset()
        {
            CheckDisposed();
            CheckEvent();

            return @event.Reset();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the underlying event to the signaled state.
        /// </summary>
        /// <returns>
        /// Non-zero if the operation succeeds.
        /// </returns>
        public bool Set()
        {
            CheckDisposed();
            CheckEvent();

            return @event.Set();
        }

        ///////////////////////////////////////////////////////////////////////

#if !NET_STANDARD_20
        /// <summary>
        /// Applies the specified access control security to the underlying
        /// named event.
        /// </summary>
        /// <param name="eventSecurity">
        /// The access control security to apply to the underlying event.
        /// </param>
        public void SetAccessControl(
            EventWaitHandleSecurity eventSecurity /* in */
            )
        {
            CheckDisposed();
            CheckEvent();

            @event.SetAccessControl(eventSecurity);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierName Members
        /// <summary>
        /// The name of the underlying event.
        /// </summary>
        private string name;
        /// <summary>
        /// Gets the name of the underlying event. The setter is not
        /// supported.
        /// </summary>
        public string Name
        {
            get { CheckDisposed(); return name; }
            set { CheckDisposed(); throw new NotSupportedException(); }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ICloneable Members
        /// <summary>
        /// Creates a new instance that opens the same named event as this
        /// instance.
        /// </summary>
        /// <returns>
        /// A new <see cref="SharedEventWaitHandle" /> referring to the same
        /// named event.
        /// </returns>
        public object Clone()
        {
            CheckDisposed();
            CheckName();

            return new SharedEventWaitHandle(name);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
        /// <summary>
        /// Returns a string representation of this instance, including the
        /// event name and underlying handle.
        /// </summary>
        /// <returns>
        /// A string describing this instance.
        /// </returns>
        public override string ToString()
        {
            CheckDisposed();
            CheckName();
            CheckEvent();

            return StringList.MakeList(
                base.ToString(), name, @event.Handle);
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
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed and disposed-object
        /// checking is enabled.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed &&
                Engine.IsThrowOnDisposed(null, false))
            {
                throw new ObjectDisposedException(
                    typeof(SharedEventWaitHandle).Name);
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
        protected override void Dispose(
            bool disposing /* in */
            )
        {
            try
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        ////////////////////////////////////
                        // dispose managed resources here...
                        ////////////////////////////////////

                        Utility.CloseNamedEvent(ref @event);

                        name = null;
                    }

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////
                }
            }
            finally
            {
                base.Dispose(disposing);

                disposed = true;
            }
        }
        #endregion
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region SandboxData Helper Class
    /// <summary>
    /// Holds the per-sandbox state used while evaluating signed scripts,
    /// including the associated variable name, next identifier, and event.
    /// </summary>
    [ObjectId("9f95f5db-c597-49d6-b1c2-79fb7422f0ce")]
    internal sealed class SandboxData
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance with the specified variable name, next
        /// identifier, and event.
        /// </summary>
        /// <param name="varName">
        /// The name of the variable associated with this sandbox.
        /// </param>
        /// <param name="nextId">
        /// The next identifier value associated with this sandbox.
        /// </param>
        /// <param name="event">
        /// The event used to coordinate this sandbox.
        /// </param>
        public SandboxData( /* CORE */
            string varName,        /* in */
            long nextId,           /* in */
            EventWaitHandle @event /* in */
            )
        {
            this.varName = varName;
            this.nextId = nextId;
            this.@event = @event;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The name of the variable associated with this sandbox.
        /// </summary>
        private string varName;
        /// <summary>
        /// Gets or sets the name of the variable associated with this
        /// sandbox.
        /// </summary>
        public string VarName
        {
            get { return varName; }
            set { varName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The next identifier value associated with this sandbox.
        /// </summary>
        private long nextId;
        /// <summary>
        /// Gets or sets the next identifier value associated with this
        /// sandbox.
        /// </summary>
        public long NextId
        {
            get { return nextId; }
            set { nextId = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The event used to coordinate this sandbox.
        /// </summary>
        private EventWaitHandle @event;
        /// <summary>
        /// Gets or sets the event used to coordinate this sandbox.
        /// </summary>
        public EventWaitHandle Event
        {
            get { return @event; }
            set { @event = value; }
        }
        #endregion
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region ScriptContextClientData Helper Class
    /// <summary>
    /// Carries the script evaluation context, including the associated
    /// interpreter, plugin, file name, and execution policy information.
    /// </summary>
    [ObjectId("417c9563-1a3f-4b7b-ab88-5a0c9cad84b9")]
    internal class ScriptContextClientData :
            ScriptClientData, IHavePlugin, IHaveFileName, IHaveExecutionPolicy
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to this instance.
        /// </summary>
        private readonly object syncRoot = new object();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Constructors
        /// <summary>
        /// Constructs a new instance with the specified context state.
        /// </summary>
        /// <param name="data">
        /// The opaque application-defined data to associate with this
        /// instance. This value may be null.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with this context. This value may be
        /// null.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with this context. This value may be null.
        /// </param>
        /// <param name="fileName">
        /// The current script file name associated with this context. This
        /// value may be null.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with this context. This value may be
        /// null.
        /// </param>
        /// <param name="policy">
        /// The execution policy associated with this context. This value may
        /// be null.
        /// </param>
        protected ScriptContextClientData( /* CORE */
            object data,             /* in: OPTIONAL */
            Interpreter interpreter, /* in: OPTIONAL */
            IPlugin plugin,          /* in: OPTIONAL */
            string fileName,         /* in: OPTIONAL */
            PolicyType? policyType,  /* in: OPTIONAL */
            ExecutionPolicy? policy  /* in: OPTIONAL */
            )
            : base(data)
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                this.interpreter = interpreter;
                this.plugin = plugin;
                this.fileName = fileName;
                this.policyType = policyType;
                this.policy = policy;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetInterpreter / ISetInterpreter Members
        /// <summary>
        /// The interpreter associated with this context.
        /// </summary>
        private Interpreter interpreter;
        /// <summary>
        /// Gets or sets the interpreter associated with this context.
        /// </summary>
        public new Interpreter Interpreter
        {
            get { CheckDisposed(); return GetInterpreter(); }
            set { CheckDisposed(); lock (syncRoot) { interpreter = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetPlugin / ISetPlugin Members
        /// <summary>
        /// The plugin associated with this context.
        /// </summary>
        private IPlugin plugin;
        /// <summary>
        /// Gets or sets the plugin associated with this context.
        /// </summary>
        public IPlugin Plugin
        {
            get { CheckDisposed(); return GetPlugin(); }
            set { CheckDisposed(); lock (syncRoot) { plugin = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHaveFileName Members
        /// <summary>
        /// The current script file name associated with this context.
        /// </summary>
        private string fileName;
        /// <summary>
        /// Gets or sets the current script file name associated with this
        /// context.
        /// </summary>
        public string FileName
        {
            get { CheckDisposed(); return GetFileName(); }
            set { CheckDisposed(); lock (syncRoot) { fileName = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHaveExecutionPolicy Members
        /// <summary>
        /// The policy type associated with this context.
        /// </summary>
        private PolicyType? policyType;
        /// <summary>
        /// Gets or sets the policy type associated with this context.
        /// </summary>
        public PolicyType? PolicyType
        {
            get { CheckDisposed(); return GetPolicyType(); }
            set { CheckDisposed(); lock (syncRoot) { policyType = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The execution policy associated with this context.
        /// </summary>
        private ExecutionPolicy? policy;
        /// <summary>
        /// Gets or sets the execution policy associated with this context.
        /// </summary>
        public ExecutionPolicy? ExecutionPolicy
        {
            get { CheckDisposed(); return GetExecutionPolicy(); }
            set { CheckDisposed(); lock (syncRoot) { policy = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
        /// <summary>
        /// Gets the interpreter associated with this context in a thread-safe
        /// manner.
        /// </summary>
        /// <returns>
        /// The interpreter associated with this context, or null.
        /// </returns>
        protected Interpreter GetInterpreter() /* CORE */
        {
            lock (syncRoot) { return interpreter; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the plugin associated with this context in a thread-safe
        /// manner.
        /// </summary>
        /// <returns>
        /// The plugin associated with this context, or null.
        /// </returns>
        protected IPlugin GetPlugin() /* CORE */
        {
            lock (syncRoot) { return plugin; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current script file name associated with this context in
        /// a thread-safe manner.
        /// </summary>
        /// <returns>
        /// The current script file name, or null.
        /// </returns>
        protected string GetFileName() /* CORE */
        {
            lock (syncRoot) { return fileName; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the policy type associated with this context in a thread-safe
        /// manner.
        /// </summary>
        /// <returns>
        /// The policy type associated with this context, or null.
        /// </returns>
        protected PolicyType? GetPolicyType() /* CORE */
        {
            lock (syncRoot) { return policyType; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the execution policy associated with this context in a
        /// thread-safe manner.
        /// </summary>
        /// <returns>
        /// The execution policy associated with this context, or null.
        /// </returns>
        protected ExecutionPolicy? GetExecutionPolicy() /* CORE */
        {
            lock (syncRoot) { return policy; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets all context state to its default (null) values.
        /// </summary>
        protected void Reset() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                interpreter = null;
                plugin = null;
                fileName = null;
                policyType = null;
                policy = null;
            }
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
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed and disposed-object
        /// checking is enabled.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed &&
                Engine.IsThrowOnDisposed(interpreter, false))
            {
                throw new ObjectDisposedException(
                    typeof(ScriptContextClientData).Name);
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
        protected override void Dispose( /* CORE */
            bool disposing /* in */
            )
        {
            try
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        ////////////////////////////////////
                        // dispose managed resources here...
                        ////////////////////////////////////

                        Reset();
                    }

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////
                }
            }
            finally
            {
                base.Dispose(disposing);

                disposed = true;
            }
        }
        #endregion
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region ScriptLogClientData Helper Class
    /// <summary>
    /// Extends <see cref="ScriptContextClientData" /> with the ability to
    /// append diagnostic messages to a dynamically determined trace log file.
    /// </summary>
    [ObjectId("45ca3c43-088c-48e3-8e79-d6d6b95d698d")]
    internal class ScriptLogClientData :
            ScriptContextClientData, ILogClientData
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to this instance.
        /// </summary>
        private readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is NOT the same as the "current script file name"
        //       that is tracked by our base class.  Instead, it is the
        //       trace log file name to be used.  It is never passed-in
        //       to the constructor.  Instead, it is dynamically built
        //       and saved by the GetFullFileName method.  To reset it,
        //       the ResetFullFileName method may be called.
        //
        /// <summary>
        /// The cached trace log file name to use, or null if it has not yet
        /// been determined.
        /// </summary>
        private string fileName;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Constructors
        /// <summary>
        /// Constructs a new instance with the specified context state.
        /// </summary>
        /// <param name="data">
        /// The opaque application-defined data to associate with this
        /// instance. This value may be null.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with this context. This value may be
        /// null.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with this context. This value may be null.
        /// </param>
        /// <param name="fileName">
        /// The current script file name associated with this context. This
        /// value may be null.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with this context. This value may be
        /// null.
        /// </param>
        /// <param name="policy">
        /// The execution policy associated with this context. This value may
        /// be null.
        /// </param>
        protected ScriptLogClientData( /* CORE */
            object data,             /* in: OPTIONAL */
            Interpreter interpreter, /* in: OPTIONAL */
            IPlugin plugin,          /* in: OPTIONAL */
            string fileName,         /* in: OPTIONAL */
            PolicyType? policyType,  /* in: OPTIONAL */
            ExecutionPolicy? policy  /* in: OPTIONAL */
            )
            : base(data, interpreter, plugin, fileName, policyType, policy)
        {
            lock (syncRoot)
            {
                this.noScript = false; // TODO: Good default?
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new instance with the specified context state and no
        /// associated application-defined data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with this context.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with this context.
        /// </param>
        /// <param name="fileName">
        /// The current script file name associated with this context.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with this context.
        /// </param>
        /// <param name="policy">
        /// The execution policy associated with this context.
        /// </param>
        public ScriptLogClientData( /* CORE */
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            string fileName,         /* in */
            PolicyType? policyType,  /* in */
            ExecutionPolicy? policy  /* in */
            )
            : this(null, interpreter, plugin, fileName, policyType, policy)
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// Gets the trace log file name saved for the current application
        /// domain.
        /// </summary>
        /// <returns>
        /// The saved trace log file name, or null if none has been saved.
        /// </returns>
        private static string GetSavedFullFileName()
        {
            return Utility.GetSavedLogFileName();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the trace log file name for the current application domain.
        /// </summary>
        /// <param name="fileName">
        /// The trace log file name to save. This value may be null.
        /// </param>
        /// <returns>
        /// Non-zero if the file name was saved successfully.
        /// </returns>
        private static bool SetSavedFullFileName(
            string fileName /* in: OPTIONAL */
            )
        {
            return Utility.SetSavedLogFileName(fileName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Builds the default trace log file name (without any directory),
        /// optionally incorporating the specified tag.
        /// </summary>
        /// <param name="tag">
        /// An optional tag to embed in the file name. This value may be null
        /// or empty.
        /// </param>
        /// <returns>
        /// The default trace log file name.
        /// </returns>
        /* CANNOT RETURN NULL */
        private string GetDefaultFileNameOnly( /* CORE */
            string tag /* in: OPTIONAL */
            )
        {
            string format = !String.IsNullOrEmpty(tag) ?
                Constants.TraceFileWithTagNameFormat :
                Constants.TraceFileWithoutTagNameFormat;

            long id = 0;

            if (Configuration.DoesVariableExist(
                    Constants.ForceLogPerProcessEnvVarName))
            {
                id = Utility.GetCurrentProcessId();
            }
            else
            {
                id = RuntimeHelpers.GetHashCode(this);
            }

            return String.Format(
                format, Constants.TraceFileNamePrefix,
                tag, id, FileExtension.Log);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a tag derived from the current script file name for use in
        /// building the trace log file name.
        /// </summary>
        /// <returns>
        /// The file name (without extension) of the current script file, or
        /// null if no script file name is available.
        /// </returns>
        private string GetTag() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                string fileName = GetFileName();

                if (!String.IsNullOrEmpty(fileName))
                {
                    return Path.GetFileNameWithoutExtension(
                        fileName); /* throw */
                }

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines the directory in which the trace log file should be
        /// placed, consulting configuration, the current script file name,
        /// the plugin, and finally a temporary path.
        /// </summary>
        /// <returns>
        /// The directory to use for the trace log file.
        /// </returns>
        /* CANNOT RETURN NULL */
        private string GetDirectory() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                string directory = Configuration.GetVariable(
                    Constants.ConfigurationTraceDirectoryEnvVarName);

                if (!String.IsNullOrEmpty(directory) &&
                    _Directory.Exists(directory))
                {
                    return directory;
                }

                if (noScript)
                    goto usePlugin;

                string fileName = GetFileName();

                if (!String.IsNullOrEmpty(fileName))
                {
                    directory = Path.GetDirectoryName(
                        fileName); /* throw */

                    if (!String.IsNullOrEmpty(directory) &&
                        _Directory.Exists(directory))
                    {
                        return directory;
                    }
                }

            usePlugin:

                IPlugin plugin = GetPlugin();

                if (plugin != null)
                {
                    directory = null;

                    if (CertificatePathOps.GetDirectory(
                            plugin, ref directory))
                    {
                        return directory;
                    }
                }
            }

            return Utility.GetTempPath(GetInterpreter());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the full trace log file name, building and caching it from
        /// the trace directory and default file name if it has not yet been
        /// determined.
        /// </summary>
        /// <returns>
        /// The full trace log file name, or null if a directory could not be
        /// determined.
        /// </returns>
        /* CANNOT RETURN NULL */
        private string GetFullFileName() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (Configuration.DoesVariableExist(
                        Constants.ForceLogPerAppDomainEnvVarName))
                {
                    fileName = GetSavedFullFileName();

                    if (fileName != null)
                        return fileName;
                }

                if (fileName == null)
                {
                    string directory = GetDirectory();

                    if (directory == null)
                        return null;

                    fileName = Path.Combine(
                        directory, GetDefaultFileNameOnly(GetTag()));

                    if (Configuration.DoesVariableExist(
                            Constants.ForceLogPerAppDomainEnvVarName))
                    {
                        /* IGNORED */
                        SetSavedFullFileName(fileName);
                    }
                }

                return fileName;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a <see cref="TextWriter" /> that appends to the specified
        /// file using the given encoding.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to append to.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use when writing, or null to use the default
        /// encoding.
        /// </param>
        /// <returns>
        /// A writer that appends to the specified file.
        /// </returns>
        /* CANNOT RETURN NULL */
        private TextWriter GetWriter( /* CORE */
            string fileName,  /* in */
            Encoding encoding /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return (encoding != null) ?
                    new StreamWriter(fileName, true, encoding) :
                    new StreamWriter(fileName, true);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
        /// <summary>
        /// Clears the cached trace log file name so that it will be rebuilt
        /// on the next request.
        /// </summary>
        protected void ResetFullFileName() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (fileName != null)
                    fileName = null;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Properties
        //
        // NOTE: When this flag is non-zero, skip attempting to use the
        //       "current script file name" as the basis for the trace
        //       log directory.  This flag can only be changed via the
        //       BeginNoScript() / EndNoScript() methods.
        //
        /// <summary>
        /// Non-zero to skip using the current script file name as the basis
        /// for the trace log directory.
        /// </summary>
        private bool noScript;
        /// <summary>
        /// Gets or sets a value indicating whether the current script file
        /// name should be skipped when determining the trace log directory.
        /// </summary>
        protected internal bool NoScript
        {
            get { lock (syncRoot) { return noScript; } }
            set { lock (syncRoot) { noScript = value; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Begins a region in which the current script file name is not used
        /// to determine the trace log directory, saving the previous setting.
        /// </summary>
        /// <param name="savedNoScript">
        /// Upon return, the previous value of the no-script setting, to be
        /// restored by <see cref="EndNoScript" />.
        /// </param>
        public void BeginNoScript( /* CORE */
            out bool? savedNoScript /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                savedNoScript = noScript;
                noScript = true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ends a region previously started by <see cref="BeginNoScript" />,
        /// restoring the saved no-script setting.
        /// </summary>
        /// <param name="savedNoScript">
        /// The previously saved no-script setting to restore. This value is
        /// reset to null on return.
        /// </param>
        public void EndNoScript( /* CORE */
            ref bool? savedNoScript /* in, out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (savedNoScript != null)
                {
                    noScript = (bool)savedNoScript;
                    savedNoScript = null;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ILogClientData Members
        /// <summary>
        /// Appends the specified message as a new line to the trace log file.
        /// </summary>
        /// <param name="message">
        /// The message to append. This value may be null.
        /// </param>
        /// <returns>
        /// Non-zero if the message was written successfully.
        /// </returns>
        public bool AppendToFile( /* CORE */
            string message /* in: OPTIONAL */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                try
                {
                    string fileName = GetFullFileName();

                    if (!String.IsNullOrEmpty(fileName))
                    {
                        using (TextWriter writer = GetWriter(
                                fileName, Constants.LogEncoding))
                        {
                            writer.WriteLine(message);
                            writer.Flush();

                            return true;
                        }
                    }
                }
#if DEBUG || FORCE_TRACE
                catch (Exception e)
#else
                catch
#endif
                {
#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(
                        e, typeof(ScriptLogClientData).Name,
                        TracePriority.Highest);
#endif
                }

                return false;
            }
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
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed and disposed-object
        /// checking is enabled.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed &&
                Engine.IsThrowOnDisposed(null, false))
            {
                throw new ObjectDisposedException(
                    typeof(ScriptLogClientData).Name);
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
        protected override void Dispose( /* CORE */
            bool disposing /* in */
            )
        {
            try
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        ////////////////////////////////////
                        // dispose managed resources here...
                        ////////////////////////////////////

                        /* NO RESULT */
                        ResetFullFileName();
                    }

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////
                }
            }
            finally
            {
                base.Dispose(disposing);

                disposed = true;
            }
        }
        #endregion
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region EvaluateClientData Helper Class
    /// <summary>
    /// Carries all of the state needed to verify and evaluate a signed
    /// script, including the key pairs, signature, encoding, version
    /// constraints, sandboxes, queued scripts, and registered variables.
    /// </summary>
    [ObjectId("8b4c2ba5-3a56-4b83-b335-dcdced10b3f8")]
    internal sealed class EvaluateClientData :
            ScriptLogClientData, IIdentifier
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to this instance.
        /// </summary>
        private readonly object syncRoot = new object();
        /// <summary>
        /// The active sandboxes, keyed by their next identifier value.
        /// </summary>
        private Dictionary<long, SandboxData> sandboxes;
        /// <summary>
        /// The list of scripts queued for later evaluation.
        /// </summary>
        private ScriptList scriptQueue;
        /// <summary>
        /// The names of objects registered with the interpreter during
        /// evaluation.
        /// </summary>
        private StringList registeredObjectNames;
        /// <summary>
        /// The variables registered with the interpreter during evaluation.
        /// </summary>
        private ObjectDictionary registeredVariables;
        /// <summary>
        /// The configuration variables associated with this instance.
        /// </summary>
        private StringDictionary configuration;
        /// <summary>
        /// The scope call frame associated with this instance, if any.
        /// </summary>
        private ICallFrame scopeFrame;
        /// <summary>
        /// The token of the swap command saved for later removal, or zero if
        /// none.
        /// </summary>
        private long swapToken;
        /// <summary>
        /// Non-zero if this instance was created as a copy of another
        /// instance.
        /// </summary>
        private bool wasCopied;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new instance with the specified base context state
        /// and default collection state.
        /// </summary>
        /// <param name="data">
        /// The opaque application-defined data to associate with this
        /// instance.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with this context.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with this context.
        /// </param>
        /// <param name="fileName">
        /// The current script file name associated with this context.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with this context.
        /// </param>
        /// <param name="policy">
        /// The execution policy associated with this context.
        /// </param>
        private EvaluateClientData( /* CORE */
            object data,             /* in */
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            string fileName,         /* in */
            PolicyType? policyType,  /* in */
            ExecutionPolicy? policy  /* in */
            )
            : base(data, interpreter, plugin, fileName, policyType, policy)
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                sandboxes = new Dictionary<long, SandboxData>();
                scriptQueue = null;
                registeredObjectNames = null;
                registeredVariables = null;
                configuration = null;
                scopeFrame = null;
                swapToken = 0;
                wasCopied = false;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a fully populated instance describing a script to be
        /// verified and evaluated.
        /// </summary>
        /// <param name="cultureInfo">
        /// The culture used when formatting and parsing values.
        /// </param>
        /// <param name="data">
        /// The opaque application-defined data to associate with this
        /// instance.
        /// </param>
        /// <param name="name">
        /// The identifier name of this instance.
        /// </param>
        /// <param name="id">
        /// The unique identifier of this instance.
        /// </param>
        /// <param name="group">
        /// The identifier group of this instance.
        /// </param>
        /// <param name="description">
        /// The human-readable description of this instance.
        /// </param>
        /// <param name="contextName">
        /// The name of the script context associated with this instance.
        /// </param>
        /// <param name="refreshEvent">
        /// The event signaled when the script context should be refreshed.
        /// </param>
        /// <param name="sandboxToken">
        /// The token identifying the sandbox associated with this instance.
        /// </param>
        /// <param name="commandTokens">
        /// The tokens of commands added on behalf of this instance.
        /// </param>
        /// <param name="settingsCallback">
        /// The callback used to obtain the interpreter settings file name.
        /// </param>
        /// <param name="ruleSet">
        /// The rule set applied to any interpreter created for this instance.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter associated with this context.
        /// </param>
        /// <param name="plugin">
        /// The plugin associated with this context.
        /// </param>
        /// <param name="pluginType">
        /// The type of the plugin associated with this context.
        /// </param>
        /// <param name="minimumVersion">
        /// The minimum plugin version required by the script.
        /// </param>
        /// <param name="maximumVersion">
        /// The maximum plugin version supported by the script.
        /// </param>
        /// <param name="variantName">
        /// The variant name associated with this instance.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to verify the script.
        /// </param>
        /// <param name="hashKey">
        /// The key used by the keyed hash algorithm, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the script and compute its hash.
        /// </param>
        /// <param name="type">
        /// The script type (e.g. signed, trusted, or unsigned).
        /// </param>
        /// <param name="subType">
        /// The script sub-type (e.g. file or resource).
        /// </param>
        /// <param name="directory">
        /// The directory associated with this instance.
        /// </param>
        /// <param name="fileName">
        /// The script file name associated with this instance.
        /// </param>
        /// <param name="stream">
        /// The stream containing the script, if any.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the script signature.
        /// </param>
        /// <param name="keyPair">
        /// The key pair that verified the script signature, if known.
        /// </param>
        /// <param name="keyName">
        /// The name of the key associated with this instance.
        /// </param>
        /// <param name="keyRingName">
        /// The name of the key ring associated with this instance.
        /// </param>
        /// <param name="hashValue">
        /// The computed hash value of the verified script.
        /// </param>
        /// <param name="signature">
        /// The signature used to verify the script.
        /// </param>
        /// <param name="keyUsage">
        /// The key usage required of the verifying key pair.
        /// </param>
        /// <param name="configurationPhase">
        /// The configuration phase associated with this instance.
        /// </param>
        /// <param name="trustFlags">
        /// The trust flags used when evaluating a trusted script.
        /// </param>
        /// <param name="policyType">
        /// The policy type associated with this context.
        /// </param>
        /// <param name="policy">
        /// The execution policy associated with this context.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data.
        /// </param>
        /// <param name="referenceCount">
        /// The initial reference count for this instance.
        /// </param>
        /// <param name="untrusted">
        /// Non-zero to evaluate the script without trust.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the script and signature to be read from remote
        /// locations.
        /// </param>
        /// <param name="useContext">
        /// Non-zero to refresh and use the script context variables.
        /// </param>
        /// <param name="withCommands">
        /// Non-zero to add the plugin commands before evaluation.
        /// </param>
        /// <param name="removeCommands">
        /// Non-zero to remove all existing commands before evaluation.
        /// </param>
        /// <param name="swapCommands">
        /// Non-zero to swap out the existing commands during evaluation.
        /// </param>
        /// <param name="noGlobalOnly">
        /// Non-zero to disable the global-only restriction on variables.
        /// </param>
        /// <param name="allowLocalPolicy">
        /// Non-zero to allow local policy to be applied.
        /// </param>
        /// <param name="extractAndApply">
        /// Non-zero to extract and apply context variables after evaluation.
        /// </param>
        /// <param name="failOnError">
        /// Non-zero to treat evaluation errors as failures.
        /// </param>
        /// <param name="fatalError">
        /// Non-zero to treat evaluation errors as fatal.
        /// </param>
        public EvaluateClientData( /* CORE */
            CultureInfo cultureInfo,               /* in: OPTIONAL */
            object data,                           /* in: OPTIONAL */
            string name,                           /* in: OPTIONAL */
            Guid id,                               /* in: OPTIONAL */
            string group,                          /* in: OPTIONAL */
            string description,                    /* in: OPTIONAL */
            string contextName,                    /* in: OPTIONAL */
            SharedEventWaitHandle refreshEvent,    /* in: OPTIONAL */
            ulong? sandboxToken,                   /* in: OPTIONAL */
            LongList commandTokens,                /* in: OPTIONAL */
            GetFileNameCallback settingsCallback,  /* in: OPTIONAL */
            IRuleSet ruleSet,                      /* in: OPTIONAL */
            Interpreter interpreter,               /* in */
            IPlugin plugin,                        /* in: OPTIONAL */
            Type pluginType,                       /* in: OPTIONAL */
            Version minimumVersion,                /* in: OPTIONAL */
            Version maximumVersion,                /* in: OPTIONAL */
            string variantName,                    /* in: OPTIONAL */
            string hashAlgorithmName,              /* in: OPTIONAL */
            byte[] hashKey,                        /* in: OPTIONAL */
            Encoding encoding,                     /* in */
            string type,                           /* in */
            string subType,                        /* in */
            string directory,                      /* in */
            string fileName,                       /* in */
            Stream stream,                         /* in */
            IEnumerable<IKeyPair> keyPairs,        /* in */
            IKeyPair keyPair,                      /* in */
            string keyName,                        /* in: OPTIONAL */
            string keyRingName,                    /* in: OPTIONAL */
            byte[] hashValue,                      /* in: OPTIONAL */
            byte[] signature,                      /* in */
            string keyUsage,                       /* in: OPTIONAL */
            ConfigurationPhase configurationPhase, /* in */
            TrustFlags trustFlags,                 /* in */
            PolicyType? policyType,                /* in: OPTIONAL */
            ExecutionPolicy? policy,               /* in: OPTIONAL */
            int? timeout,                          /* in: OPTIONAL */
            int referenceCount,                    /* in */
            bool untrusted,                        /* in */
            bool allowRemoteUri,                   /* in */
            bool useContext,                       /* in */
            bool withCommands,                     /* in */
            bool removeCommands,                   /* in */
            bool swapCommands,                     /* in */
            bool noGlobalOnly,                     /* in */
            bool allowLocalPolicy,                 /* in */
            bool extractAndApply,                  /* in */
            bool failOnError,                      /* in */
            bool fatalError                        /* in */
            )
            : this(data, interpreter, plugin, fileName, policyType, policy)
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                this.name = name;
                this.kind = IdentifierKind.ContextData;
                this.id = id;
                this.group = group;
                this.description = description;
                this.contextName = contextName;
                this.sandboxToken = sandboxToken;
                this.commandTokens = commandTokens;
                this.settingsCallback = settingsCallback;
                this.ruleSet = ruleSet;
                this.pluginType = pluginType;
                this.minimumVersion = minimumVersion;
                this.maximumVersion = maximumVersion;
                this.variantName = variantName;
                this.hashAlgorithmName = hashAlgorithmName;
                this.hashKey = hashKey;
                this.encoding = encoding;
                this.type = type;
                this.subType = subType;
                this.directory = directory;
                this.stream = stream;
                this.keyPairs = keyPairs;
                this.keyPair = keyPair;
                this.keyName = keyName;
                this.keyRingName = keyRingName;
                this.hashValue = hashValue;
                this.signature = signature;
                this.keyUsage = keyUsage;
                this.configurationPhase = configurationPhase;
                this.trustFlags = trustFlags;
                this.timeout = timeout;
                this.referenceCount = referenceCount;
                this.untrusted = untrusted;
                this.allowRemoteUri = allowRemoteUri;
                this.useContext = useContext;
                this.withCommands = withCommands;
                this.removeCommands = removeCommands;
                this.swapCommands = swapCommands;
                this.noGlobalOnly = noGlobalOnly;
                this.allowLocalPolicy = allowLocalPolicy;
                this.extractAndApply = extractAndApply;
                this.failOnError = failOnError;
                this.fatalError = fatalError;
                this.swapToken = 0;
                this.wasCopied = false;

                ///////////////////////////////////////////////////////////////

                CopyToBaseCultureInfo(cultureInfo);

                ///////////////////////////////////////////////////////////////

                SetupRefreshEvent(refreshEvent, false);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new instance by copying the state from the specified
        /// existing instance.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose state is copied. This value may be null, in
        /// which case the new instance is left in its default state.
        /// </param>
        public EvaluateClientData( /* CORE */
            EvaluateClientData clientData /* in: OPTIONAL */
            )
            : this(null, null, null, null, null, null)
        {
            if (clientData != null)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    CopyToBaseProperties(clientData);

                    ///////////////////////////////////////////////////////////

                    this.name = clientData.Name;
                    this.kind = clientData.Kind;
                    this.id = clientData.Id;
                    this.group = clientData.Group;
                    this.description = clientData.Description;
                    this.contextName = clientData.ContextName;
                    this.sandboxToken = clientData.SandboxToken;
                    this.commandTokens = clientData.CommandTokens;
                    this.settingsCallback = clientData.SettingsCallback;
                    this.ruleSet = clientData.RuleSet;
                    this.pluginType = clientData.PluginType;
                    this.minimumVersion = clientData.MinimumVersion;
                    this.maximumVersion = clientData.MaximumVersion;
                    this.variantName = clientData.VariantName;
                    this.hashAlgorithmName = clientData.HashAlgorithmName;
                    this.hashKey = clientData.HashKey;
                    this.encoding = clientData.Encoding;
                    this.type = clientData.Type;
                    this.subType = clientData.SubType;
                    this.directory = clientData.Directory;
                    this.stream = clientData.Stream;
                    this.keyPairs = clientData.KeyPairs;
                    this.keyPair = clientData.KeyPair;
                    this.keyName = clientData.KeyName;
                    this.keyRingName = clientData.KeyRingName;
                    this.hashValue = clientData.HashValue;
                    this.signature = clientData.Signature;
                    this.keyUsage = clientData.KeyUsage;
                    this.configurationPhase = clientData.ConfigurationPhase;
                    this.trustFlags = clientData.TrustFlags;
                    this.timeout = clientData.Timeout;
                    this.referenceCount = clientData.ReferenceCount;
                    this.untrusted = clientData.Untrusted;
                    this.allowRemoteUri = clientData.AllowRemoteUri;
                    this.useContext = clientData.UseContext;
                    this.withCommands = clientData.WithCommands;
                    this.removeCommands = clientData.RemoveCommands;
                    this.swapCommands = clientData.SwapCommands;
                    this.noGlobalOnly = clientData.NoGlobalOnly;
                    this.allowLocalPolicy = clientData.AllowLocalPolicy;
                    this.extractAndApply = clientData.ExtractAndApply;
                    this.failOnError = clientData.FailOnError;
                    this.fatalError = clientData.FatalError;
                    this.swapToken = 0;
                    this.wasCopied = true;

                    ///////////////////////////////////////////////////////////

                    SetupRefreshEvent(clientData.RefreshEvent, true);
                    ReplaceData(clientData);
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The name of the script context associated with this instance.
        /// </summary>
        private string contextName;
        /// <summary>
        /// Gets the name of the script context associated with this instance.
        /// </summary>
        public string ContextName
        {
            get { CheckDisposed(); lock (syncRoot) { return contextName; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The event signaled when the script context should be refreshed.
        /// </summary>
        private SharedEventWaitHandle refreshEvent;
        /// <summary>
        /// Gets the event signaled when the script context should be
        /// refreshed.
        /// </summary>
        public SharedEventWaitHandle RefreshEvent
        {
            get { CheckDisposed(); lock (syncRoot) { return refreshEvent; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The token identifying the sandbox associated with this instance.
        /// </summary>
        private ulong? sandboxToken;
        /// <summary>
        /// Gets the token identifying the sandbox associated with this
        /// instance.
        /// </summary>
        public ulong? SandboxToken
        {
            get { CheckDisposed(); lock (syncRoot) { return sandboxToken; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The tokens of commands added on behalf of this instance.
        /// </summary>
        private LongList commandTokens;
        /// <summary>
        /// Gets the tokens of commands added on behalf of this instance.
        /// </summary>
        public LongList CommandTokens
        {
            get { CheckDisposed(); lock (syncRoot) { return commandTokens; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The callback used to obtain the interpreter settings file name.
        /// </summary>
        private GetFileNameCallback settingsCallback;
        /// <summary>
        /// Gets the callback used to obtain the interpreter settings file
        /// name.
        /// </summary>
        public GetFileNameCallback SettingsCallback
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return settingsCallback;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The rule set applied to any interpreter created for this instance.
        /// </summary>
        private IRuleSet ruleSet;
        /// <summary>
        /// Gets the rule set applied to any interpreter created for this
        /// instance.
        /// </summary>
        public IRuleSet RuleSet
        {
            get { CheckDisposed(); lock (syncRoot) { return ruleSet; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The type of the plugin associated with this context.
        /// </summary>
        private Type pluginType;
        /// <summary>
        /// Gets the type of the plugin associated with this context.
        /// </summary>
        public Type PluginType
        {
            get { CheckDisposed(); lock (syncRoot) { return pluginType; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The minimum plugin version required by the script.
        /// </summary>
        private Version minimumVersion;
        /// <summary>
        /// Gets or sets the minimum plugin version required by the script.
        /// </summary>
        public Version MinimumVersion
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return minimumVersion;
                }
            }
            private set
            {
                // CheckDisposed();

                lock (syncRoot)
                {
                    minimumVersion = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The maximum plugin version supported by the script.
        /// </summary>
        private Version maximumVersion;
        /// <summary>
        /// Gets or sets the maximum plugin version supported by the script.
        /// </summary>
        public Version MaximumVersion
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return maximumVersion;
                }
            }
            private set
            {
                // CheckDisposed();

                lock (syncRoot)
                {
                    maximumVersion = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The variant name associated with this instance.
        /// </summary>
        private string variantName;
        /// <summary>
        /// Gets the variant name associated with this instance.
        /// </summary>
        public string VariantName
        {
            get { CheckDisposed(); lock (syncRoot) { return variantName; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the hash algorithm used to verify the script.
        /// </summary>
        private string hashAlgorithmName;
        /// <summary>
        /// Gets the name of the hash algorithm used to verify the script.
        /// </summary>
        public string HashAlgorithmName
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return hashAlgorithmName;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key used by the keyed hash algorithm, if any.
        /// </summary>
        private byte[] hashKey;
        /// <summary>
        /// Gets the key used by the keyed hash algorithm, if any.
        /// </summary>
        public byte[] HashKey
        {
            get { CheckDisposed(); lock (syncRoot) { return hashKey; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The encoding used to read the script and compute its hash.
        /// </summary>
        private Encoding encoding;
        /// <summary>
        /// Gets the encoding used to read the script and compute its hash.
        /// </summary>
        public Encoding Encoding
        {
            get { CheckDisposed(); lock (syncRoot) { return encoding; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script type (e.g. signed, trusted, or unsigned).
        /// </summary>
        private string type;
        /// <summary>
        /// Gets or sets the script type (e.g. signed, trusted, or unsigned).
        /// </summary>
        public string Type
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return type;
                }
            }
            private set
            {
                // CheckDisposed();

                lock (syncRoot)
                {
                    type = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script sub-type (e.g. file or resource).
        /// </summary>
        private string subType;
        /// <summary>
        /// Gets or sets the script sub-type (e.g. file or resource).
        /// </summary>
        public string SubType
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return subType;
                }
            }
            private set
            {
                // CheckDisposed();

                lock (syncRoot)
                {
                    subType = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The directory associated with this instance.
        /// </summary>
        private string directory;
        /// <summary>
        /// Gets or sets the directory associated with this instance.
        /// </summary>
        public string Directory
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return directory;
                }
            }
            private set
            {
                // CheckDisposed();

                lock (syncRoot)
                {
                    directory = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The stream containing the script, if any.
        /// </summary>
        private Stream stream;
        /// <summary>
        /// Gets or sets the stream containing the script, if any.
        /// </summary>
        public Stream Stream
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return stream;
                }
            }
            private set
            {
                // CheckDisposed();

                lock (syncRoot)
                {
                    stream = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key pairs used to verify the script signature.
        /// </summary>
        private IEnumerable<IKeyPair> keyPairs;
        /// <summary>
        /// Gets the key pairs used to verify the script signature.
        /// </summary>
        public IEnumerable<IKeyPair> KeyPairs
        {
            get { CheckDisposed(); lock (syncRoot) { return keyPairs; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key pair that verified the script signature, if known.
        /// </summary>
        private IKeyPair keyPair;
        /// <summary>
        /// Gets or sets the key pair that verified the script signature, if
        /// known.
        /// </summary>
        public IKeyPair KeyPair
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return keyPair;
                }
            }
            set /* EXEMPT */
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    keyPair = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key associated with this instance.
        /// </summary>
        private string keyName;
        /// <summary>
        /// Gets the name of the key associated with this instance.
        /// </summary>
        public string KeyName
        {
            get { CheckDisposed(); lock (syncRoot) { return keyName; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the key ring associated with this instance.
        /// </summary>
        private string keyRingName;
        /// <summary>
        /// Gets the name of the key ring associated with this instance.
        /// </summary>
        public string KeyRingName
        {
            get { CheckDisposed(); lock (syncRoot) { return keyRingName; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The computed hash value of the verified script.
        /// </summary>
        private byte[] hashValue;
        /// <summary>
        /// Gets or sets the computed hash value of the verified script.
        /// </summary>
        public byte[] HashValue
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return hashValue;
                }
            }
            set /* EXEMPT */
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    hashValue = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The signature used to verify the script.
        /// </summary>
        private byte[] signature;
        /// <summary>
        /// Gets or sets the signature used to verify the script.
        /// </summary>
        public byte[] Signature
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return signature;
                }
            }
            private set
            {
                // CheckDisposed();

                lock (syncRoot)
                {
                    signature = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key usage required of the verifying key pair.
        /// </summary>
        private string keyUsage;
        /// <summary>
        /// Gets the key usage required of the verifying key pair.
        /// </summary>
        public string KeyUsage
        {
            get { CheckDisposed(); lock (syncRoot) { return keyUsage; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The configuration phase associated with this instance.
        /// </summary>
        private ConfigurationPhase configurationPhase;
        /// <summary>
        /// Gets the configuration phase associated with this instance.
        /// </summary>
        public ConfigurationPhase ConfigurationPhase
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return configurationPhase;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The trust flags used when evaluating a trusted script.
        /// </summary>
        private TrustFlags trustFlags;
        /// <summary>
        /// Gets the trust flags used when evaluating a trusted script.
        /// </summary>
        public TrustFlags TrustFlags
        {
            get { CheckDisposed(); lock (syncRoot) { return trustFlags; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The timeout, in milliseconds, used when reading remote data.
        /// </summary>
        private int? timeout;
        /// <summary>
        /// Gets the timeout, in milliseconds, used when reading remote data.
        /// </summary>
        public int? Timeout
        {
            get { CheckDisposed(); lock (syncRoot) { return timeout; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The current reference count for this instance.
        /// </summary>
        private int referenceCount;
        /// <summary>
        /// Gets the current reference count for this instance.
        /// </summary>
        public int ReferenceCount
        {
            get
            {
                CheckDisposed();

                return Interlocked.CompareExchange(
                    ref referenceCount, 0, 0);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to evaluate the script without trust.
        /// </summary>
        private bool untrusted;
        /// <summary>
        /// Gets a value indicating whether the script is evaluated without
        /// trust.
        /// </summary>
        public bool Untrusted
        {
            get { CheckDisposed(); lock (syncRoot) { return untrusted; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to allow the script and signature to be read from remote
        /// locations.
        /// </summary>
        private bool allowRemoteUri;
        /// <summary>
        /// Gets a value indicating whether the script and signature may be
        /// read from remote locations.
        /// </summary>
        public bool AllowRemoteUri
        {
            get { CheckDisposed(); lock (syncRoot) { return allowRemoteUri; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to refresh and use the script context variables.
        /// </summary>
        private bool useContext;
        /// <summary>
        /// Gets a value indicating whether the script context variables are
        /// refreshed and used.
        /// </summary>
        public bool UseContext
        {
            get { CheckDisposed(); lock (syncRoot) { return useContext; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to add the plugin commands before evaluation.
        /// </summary>
        private bool withCommands;
        /// <summary>
        /// Gets a value indicating whether the plugin commands are added
        /// before evaluation.
        /// </summary>
        public bool WithCommands
        {
            get { CheckDisposed(); lock (syncRoot) { return withCommands; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to remove all existing commands before evaluation.
        /// </summary>
        private bool removeCommands;
        /// <summary>
        /// Gets a value indicating whether all existing commands are removed
        /// before evaluation.
        /// </summary>
        public bool RemoveCommands
        {
            get { CheckDisposed(); lock (syncRoot) { return removeCommands; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to swap out the existing commands during evaluation.
        /// </summary>
        private bool swapCommands;
        /// <summary>
        /// Gets a value indicating whether the existing commands are swapped
        /// out during evaluation.
        /// </summary>
        public bool SwapCommands
        {
            get { CheckDisposed(); lock (syncRoot) { return swapCommands; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to disable the global-only restriction on variables.
        /// </summary>
        private bool noGlobalOnly;
        /// <summary>
        /// Gets a value indicating whether the global-only restriction on
        /// variables is disabled.
        /// </summary>
        public bool NoGlobalOnly
        {
            get { CheckDisposed(); lock (syncRoot) { return noGlobalOnly; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to allow local policy to be applied.
        /// </summary>
        private bool allowLocalPolicy;
        /// <summary>
        /// Gets or sets a value indicating whether local policy may be
        /// applied.
        /// </summary>
        public bool AllowLocalPolicy
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return allowLocalPolicy;
                }
            }
            set /* EXEMPT */
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    allowLocalPolicy = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to extract and apply context variables after evaluation.
        /// </summary>
        private bool extractAndApply;
        /// <summary>
        /// Gets or sets a value indicating whether context variables are
        /// extracted and applied after evaluation.
        /// </summary>
        public bool ExtractAndApply
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return extractAndApply;
                }
            }
            set /* EXEMPT */
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    extractAndApply = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to treat evaluation errors as failures.
        /// </summary>
        private bool failOnError;
        /// <summary>
        /// Gets or sets a value indicating whether evaluation errors are
        /// treated as failures.
        /// </summary>
        public bool FailOnError
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return failOnError;
                }
            }
            set /* EXEMPT */
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    failOnError = value;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero to treat evaluation errors as fatal.
        /// </summary>
        private bool fatalError;
        /// <summary>
        /// Gets or sets a value indicating whether evaluation errors are
        /// treated as fatal.
        /// </summary>
        public bool FatalError
        {
            get
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    return fatalError;
                }
            }
            set /* EXEMPT */
            {
                CheckDisposed();

                lock (syncRoot)
                {
                    fatalError = value;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Copies the specified data into the corresponding base class
        /// property.
        /// </summary>
        /// <param name="data">
        /// The data to copy into the base class. This value may be null.
        /// </param>
        private void CopyToBaseData( /* CORE */
            object data /* in: OPTIONAL */
            )
        {
            lock (syncRoot)
            {
                base.Data = data;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the specified client data into the corresponding base class
        /// property.
        /// </summary>
        /// <param name="clientData">
        /// The client data to copy into the base class. This value may be
        /// null.
        /// </param>
        private void CopyToBaseClientData( /* CORE */
            IClientData clientData /* in: OPTIONAL */
            )
        {
            lock (syncRoot)
            {
                base.ClientData = clientData;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the specified culture into the corresponding base class
        /// property.
        /// </summary>
        /// <param name="cultureInfo">
        /// The culture to copy into the base class. This value may be null.
        /// </param>
        private void CopyToBaseCultureInfo( /* CORE */
            CultureInfo cultureInfo /* in: OPTIONAL */
            )
        {
            lock (syncRoot)
            {
                base.CultureInfo = cultureInfo;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the script context state from the specified instance into
        /// the corresponding base class properties.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose script context state is copied. This value may
        /// be null.
        /// </param>
        private void CopyToBaseScriptContext( /* CORE */
            ScriptContextClientData clientData /* in: OPTIONAL */
            )
        {
            if (clientData == null)
                return;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                base.Interpreter = clientData.Interpreter;
                base.Plugin = clientData.Plugin;
                base.FileName = clientData.FileName;
                base.PolicyType = clientData.PolicyType;
                base.ExecutionPolicy = clientData.ExecutionPolicy;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the script log state from the specified instance into the
        /// corresponding base class properties.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose script log state is copied. This value may be
        /// null.
        /// </param>
        private void CopyToBaseScriptLog( /* CORE */
            ScriptLogClientData clientData /* in: OPTIONAL */
            )
        {
            if (clientData == null)
                return;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                base.NoScript = clientData.NoScript;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the data, client data, culture, script context, and script
        /// log state from the specified instance into the corresponding base
        /// class properties.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose base class state is copied.
        /// </param>
        private void CopyToBaseProperties( /* CORE */
            EvaluateClientData clientData /* in */
            )
        {
            if (clientData == null)
                return;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                //
                // NOTE: From the ClientData base class.
                //
                /* NO RESULT */
                CopyToBaseData(clientData.Data);

                ///////////////////////////////////////////////////////////////

                //
                // NOTE: From the AnyClientData base class.
                //
                /* NO RESULT */
                CopyToBaseClientData(clientData.ClientData);

                /* NO RESULT */
                CopyToBaseCultureInfo(clientData.CultureInfo);

                ///////////////////////////////////////////////////////////////

                //
                // NOTE: From the ScriptContextClientData base class.
                //
                /* NO RESULT */
                CopyToBaseScriptContext(clientData);

                ///////////////////////////////////////////////////////////////

                //
                // NOTE: From the ScriptLogClientData base class.
                //
                /* NO RESULT */
                CopyToBaseScriptLog(clientData);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the refresh event for this instance, optionally cloning the
        /// supplied event.
        /// </summary>
        /// <param name="event">
        /// The refresh event to use. This value may be null.
        /// </param>
        /// <param name="clone">
        /// Non-zero to store a clone of <paramref name="event" /> instead of
        /// the supplied instance.
        /// </param>
        private void SetupRefreshEvent( /* CORE */
            SharedEventWaitHandle @event, /* in: OPTIONAL */
            bool clone                    /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (clone)
                {
                    refreshEvent = (@event != null) ?
                        @event.Clone() as SharedEventWaitHandle : null;
                }
                else
                {
                    refreshEvent = @event;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a stream containing the specified script text using the
        /// configured encoding.
        /// </summary>
        /// <param name="text">
        /// The script text to wrap in a stream.
        /// </param>
        /// <returns>
        /// A stream containing the encoded script text.
        /// </returns>
        private Stream GetScriptStream( /* CORE */
            string text /* in */
            )
        {
            Encoding localEncoding;

            lock (syncRoot)
            {
                localEncoding = encoding;
            }

            return DataOps.GetScriptStream(text, localEncoding);
        }

        ///////////////////////////////////////////////////////////////////////

#if TEST
        /// <summary>
        /// Registers the specified object names so that they can be removed
        /// later.
        /// </summary>
        /// <param name="names">
        /// The object names to register.
        /// </param>
        /// <returns>
        /// Non-zero if the names were registered successfully.
        /// </returns>
        private bool RegisterObjectNames( /* CORE */
            IEnumerable<string> names /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (names == null)
                    return false;

                if (registeredObjectNames == null)
                    registeredObjectNames = new StringList();

                registeredObjectNames.AddRange(names);
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified objects to the interpreter and registers their
        /// names for later removal.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to which the objects are added.
        /// </param>
        /// <param name="objects">
        /// The objects to add to the interpreter.
        /// </param>
        /// <param name="objectFlags">
        /// The flags applied to the added objects.
        /// </param>
        /// <param name="objectNames">
        /// Upon success, receives the names assigned to the added objects.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        private ReturnCode AddObjects( /* CORE */
            Interpreter interpreter,    /* in */
            ObjectDictionary objects,   /* in */
            ObjectFlags objectFlags,    /* in */
            ref StringList objectNames, /* in */
            ref Result error            /* out */
            )
        {
            ReturnCode code = ReturnCode.Ok;
            StringList localObjectNames = null;

            try
            {
                code = _Helpers.FixupReturnValues(
                    interpreter, objects, objectFlags,
                    0, ref localObjectNames, ref error);

                if (code == ReturnCode.Ok)
                {
                    if (RegisterObjectNames(localObjectNames))
                    {
                        objectNames = localObjectNames;
                    }
                    else
                    {
                        error = "could not register object names";
                        code = ReturnCode.Error;
                    }
                }

                return code;
            }
            finally
            {
                if ((code != ReturnCode.Ok) &&
                    (localObjectNames != null))
                {
                    /* NO RESULT */
                    _Helpers.RemoveObjects(
                        interpreter, localObjectNames, 0,
                        Utility.GetObjectDefaultSynchronous(),
                        Utility.GetObjectDefaultDispose());
                }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified variable name has been
        /// registered.
        /// </summary>
        /// <param name="name">
        /// The variable name to check.
        /// </param>
        /// <returns>
        /// Non-zero if the variable name has been registered.
        /// </returns>
        private bool IsRegisteredVariableName( /* CORE */
            string name /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (name == null)
                    return false;

                if (registeredVariables == null)
                    return false;

                return registeredVariables.ContainsKey(name);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Registers the specified variable name.
        /// </summary>
        /// <param name="name">
        /// The variable name to register.
        /// </param>
        /// <returns>
        /// Non-zero if the variable name was registered successfully.
        /// </returns>
        private bool RegisterVariableName( /* CORE */
            string name /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (name == null)
                    return false;

                if (registeredVariables == null)
                    registeredVariables = new ObjectDictionary();

                registeredVariables[name] = null;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unregisters the specified variable name.
        /// </summary>
        /// <param name="name">
        /// The variable name to unregister.
        /// </param>
        /// <returns>
        /// Non-zero if the variable name was unregistered successfully.
        /// </returns>
        private bool UnregisterVariableName( /* CORE */
            string name /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (name == null)
                    return false;

                if (registeredVariables == null)
                    return false;

                return registeredVariables.Remove(name);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the portion of the configuration phase that matches the "any"
        /// mask.
        /// </summary>
        /// <returns>
        /// The masked configuration phase.
        /// </returns>
        private ConfigurationPhase AnyConfigurationPhase()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return configurationPhase & ConfigurationPhase.AnyMask;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the script is allowed to omit its minimum and
        /// maximum version constraints.
        /// </summary>
        /// <returns>
        /// Non-zero if missing version constraints are allowed.
        /// </returns>
        private bool CanHaveMissingVersions() /* CORE */
        {
            return AnyConfigurationPhase() == ConfigurationPhase.Demand;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a meaningful configuration phase has been set.
        /// </summary>
        /// <returns>
        /// Non-zero if a configuration phase other than none or unknown has
        /// been set.
        /// </returns>
        private bool HasConfigurationPhase() /* CORE */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((configurationPhase == ConfigurationPhase.None) ||
                    (configurationPhase == ConfigurationPhase.Unknown) ||
                    (AnyConfigurationPhase() == ConfigurationPhase.None))
                {
                    return false;
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the configuration phase associated with this instance.
        /// </summary>
        /// <param name="configurationPhase">
        /// The configuration phase to set.
        /// </param>
        private void SetConfigurationPhase(
            ConfigurationPhase configurationPhase /* in */
            )
        {
            lock (syncRoot)
            {
                this.configurationPhase = configurationPhase;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes the rule set associated with this instance, if any.
        /// </summary>
        private void DisposeRuleSet()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                /* IGNORED */
                Utility.TryDisposeObjectOrTrace<IRuleSet>(ref ruleSet);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Ensures this instance has a unique identifier, generating a new
        /// one if the current identifier is empty.
        /// </summary>
        /// <param name="withForce">
        /// Non-zero to force generation of a new unique identifier.
        /// </param>
        public void NeedUniqueId( /* CORE */
            bool withForce /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (id.Equals(Guid.Empty))
                {
                    id = DataOps.GetNewId(withForce);

                    if (id.Equals(Guid.Empty))
                    {
#if DEBUG || FORCE_TRACE
                        CertificateTraceOps.DebugTrace(String.Format(
                            "NeedUniqueId: identifier not unique{0}",
                            withForce ? " with force" : String.Empty),
                            typeof(EvaluateClientData).Name,
                            TracePriority.Highest);
#endif
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the reference count of the specified instance to the
        /// reference count of this instance.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose reference count is added.
        /// </param>
        /// <returns>
        /// The updated reference count, or <see cref="Count.Invalid" /> if
        /// <paramref name="clientData" /> is null.
        /// </returns>
        public int AddToReferenceCount( /* CORE */
            EvaluateClientData clientData /* in */
            )
        {
            CheckDisposed();

            if (clientData == null)
                return Count.Invalid;

            return Interlocked.Add(
                ref referenceCount, clientData.ReferenceCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Forces the key usage to the default source value when the
        /// certificate SDK mode is enabled.
        /// </summary>
        public void MaybeForceDefaultKeyUsage() /* CORE */
        {
            CheckDisposed();

            if (CertificateSdkMode.IsEnabled())
            {
                string oldKeyUsage = keyUsage;
                string newKeyUsage = _KeyUsage.Source;

                if (!DataOps.StringEquals(oldKeyUsage, newKeyUsage))
                {
                    keyUsage = newKeyUsage;

#if DEBUG || FORCE_TRACE
                    CertificateTraceOps.DebugTrace(String.Format(
                        "MaybeForceDefaultKeyUsage: forcibly " +
                        "changed key usage from {0} to {1}",
                        Utility.FormatWrapOrNull(oldKeyUsage),
                        Utility.FormatWrapOrNull(keyUsage)),
                        typeof(EvaluateClientData).Name,
                        TracePriority.High);
#endif
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Increments the reference count of this instance.
        /// </summary>
        /// <returns>
        /// The incremented reference count.
        /// </returns>
        public int AddReference() /* CORE */
        {
            CheckDisposed();

            return Interlocked.Increment(ref referenceCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Decrements the reference count of this instance.
        /// </summary>
        /// <returns>
        /// The decremented reference count.
        /// </returns>
        public int RemoveReference() /* CORE */
        {
            CheckDisposed();

            return Interlocked.Decrement(ref referenceCount);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the reference count of this instance to zero.
        /// </summary>
        /// <returns>
        /// The reference count prior to being reset.
        /// </returns>
        public int ResetReferences() /* CORE */
        {
            CheckDisposed();

            return Interlocked.Exchange(ref referenceCount, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disables all flags that would cause this instance to add or modify
        /// commands, variables, or context within the interpreter.
        /// </summary>
        public void DoNotModifyInterpreter() /* CORE */
        {
            CheckDisposed();

            //
            // HACK: Disable all flags here that will attempt to add things
            //       (e.g. commands, variables, etc) within the interpreter
            //       that this object instance refers to.
            //
            withCommands = false;
            removeCommands = false;
            swapCommands = false;
            useContext = false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to retrieve the sandbox associated with the specified
        /// next identifier value.
        /// </summary>
        /// <param name="nextId">
        /// The next identifier value of the sandbox to retrieve.
        /// </param>
        /// <param name="sandboxData">
        /// Upon return, the sandbox associated with
        /// <paramref name="nextId" />, or null if it was not found.
        /// </param>
        /// <returns>
        /// Non-zero if the sandbox was found.
        /// </returns>
        public bool TryGetSandbox( /* CORE */
            long nextId,                /* in */
            out SandboxData sandboxData /* out */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (sandboxes == null)
                {
                    sandboxData = null;
                    return false;
                }

                return sandboxes.TryGetValue(nextId, out sandboxData);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified sandbox to this instance.
        /// </summary>
        /// <param name="sandboxData">
        /// The sandbox to add.
        /// </param>
        /// <returns>
        /// Non-zero if the sandbox was added successfully.
        /// </returns>
        public bool AddSandbox( /* CORE */
            SandboxData sandboxData /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (sandboxData == null)
                    return false;

                if (sandboxes == null)
                    return false;

                sandboxes[sandboxData.NextId] = sandboxData;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the specified sandbox from this instance.
        /// </summary>
        /// <param name="sandboxData">
        /// The sandbox to remove.
        /// </param>
        /// <returns>
        /// Non-zero if the sandbox was removed successfully.
        /// </returns>
        public bool RemoveSandbox( /* CORE */
            SandboxData sandboxData /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (sandboxData == null)
                    return false;

                if (sandboxes == null)
                    return false;

                return sandboxes.Remove(sandboxData.NextId);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Queues a script for later evaluation.
        /// </summary>
        /// <param name="text">
        /// The script text to queue.
        /// </param>
        /// <param name="signature">
        /// The signature associated with the script.
        /// </param>
        /// <param name="name">
        /// The name associated with the script.
        /// </param>
        /// <returns>
        /// The number of scripts now in the queue.
        /// </returns>
        public int QueueScript( /* CORE */
            string text,      /* in */
            byte[] signature, /* in */
            string name       /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (scriptQueue == null)
                    scriptQueue = new ScriptList();

                scriptQueue.Add(new ScriptTriplet(text, signature, name));
                return scriptQueue.Count;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all queued scripts and converts them into a list of
        /// file-and-or-stream data items.
        /// </summary>
        /// <returns>
        /// A list of file-and-or-stream data items for the queued scripts, or
        /// null if no scripts were queued.
        /// </returns>
        public FileAndOrStreamDataList DequeueScripts() /* CORE */
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (scriptQueue == null)
                    return null;

                FileAndOrStreamDataList result = null;

                foreach (ScriptTriplet anyTriplet in scriptQueue)
                {
                    if (anyTriplet == null)
                        continue;

                    Stream stream = GetScriptStream(anyTriplet.X);

                    if (stream == null)
                        continue;

                    if (result == null)
                        result = new FileAndOrStreamDataList();

                    result.Add(new FileAndOrStreamData(null,
                        anyTriplet.Z, stream, null, anyTriplet.Y));
                }

                scriptQueue.Clear();
                return result;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified command tokens to this instance.
        /// </summary>
        /// <param name="tokens">
        /// The command tokens to add.
        /// </param>
        /// <returns>
        /// Non-zero if the tokens were added successfully.
        /// </returns>
        public bool AddCommandTokens( /* CORE */
            IEnumerable<long> tokens /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (tokens == null)
                    return false;

                if (commandTokens == null)
                    return false;

                commandTokens.AddRange(tokens);
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the specified command tokens from this instance.
        /// </summary>
        /// <param name="tokens">
        /// The command tokens to remove.
        /// </param>
        /// <returns>
        /// Non-zero if the tokens were removed successfully.
        /// </returns>
        public bool RemoveCommandTokens( /* CORE */
            IEnumerable<long> tokens /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (tokens == null)
                    return false;

                if (commandTokens == null)
                    return false;

                commandTokens.RemoveRange(tokens);
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the token of a swap command so it can be removed later.
        /// </summary>
        /// <param name="token">
        /// The swap command token to save.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        //
        // WARNING: This method is for use by the Helpers.RestoreSwapCommand
        //          method only.  Please do not use it from anywhere else.
        //
        public ReturnCode SaveSwapToken( /* CORE */
            long token,      /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (token == 0)
                {
                    error = "invalid swap token";
                    return ReturnCode.Error;
                }

                if (swapToken != 0)
                {
                    error = "swap token already set";
                    return ReturnCode.Error;
                }

                swapToken = token;
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the previously saved swap command, if one was saved.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter from which the swap command is removed.
        /// </param>
        /// <param name="clientData">
        /// The client data passed when removing the swap command. This value
        /// may be null.
        /// </param>
        /// <param name="swapFlags">
        /// The flags controlling how the swap command is removed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public ReturnCode MaybeRemoveSwapCommand( /* CORE */
            Interpreter interpreter, /* in */
            IClientData clientData,  /* in: OPTIONAL */
            SwapFlags swapFlags,     /* in */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (interpreter == null)
                {
                    error = "invalid interpreter";
                    return ReturnCode.Error;
                }

                if (swapToken == 0)
                    return ReturnCode.Ok;

                Result result = null;

                if (interpreter.RemoveSwapCommand(
                        swapToken, clientData, swapFlags,
                        ref result) == ReturnCode.Ok)
                {
                    swapToken = 0;
                    return ReturnCode.Ok;
                }
                else
                {
                    error = result;
                    return ReturnCode.Error;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if TEST
        /// <summary>
        /// Adds a single object to the interpreter and returns its assigned
        /// name.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to which the object is added.
        /// </param>
        /// <param name="name">
        /// The desired name of the object, or null to use an empty name.
        /// </param>
        /// <param name="value">
        /// The object value to add.
        /// </param>
        /// <param name="objectFlags">
        /// The flags applied to the added object.
        /// </param>
        /// <param name="result">
        /// Upon success, receives the name assigned to the object; upon
        /// failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public ReturnCode AddObject( /* CORE */
            Interpreter interpreter, /* in */
            string name,             /* in */
            object value,            /* in */
            ObjectFlags objectFlags, /* in */
            ref Result result        /* out */
            )
        {
            CheckDisposed();

            ObjectDictionary objects = new ObjectDictionary();

            if (name == null)
                name = String.Empty;

            objects.Add(name, value);

            StringList objectNames = null;

            if (AddObjects(
                    interpreter, objects,
                    objectFlags, ref objectNames,
                    ref result) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            if ((objectNames != null) &&
                (objectNames.Count > 0))
            {
                result = objectNames[0];
            }
            else
            {
                result = null;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all previously registered objects from the interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter from which the objects are removed.
        /// </param>
        public void RemoveObjects( /* CORE */
            Interpreter interpreter /* in */
            )
        {
            CheckDisposed();

            StringList objectNames;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                objectNames = (registeredObjectNames != null) ?
                    new StringList(registeredObjectNames) : null;
            }

            if (objectNames == null)
                return;

            /* NO RESULT */
            _Helpers.RemoveObjects(
                interpreter, objectNames, 0,
                Utility.GetObjectDefaultSynchronous(),
                Utility.GetObjectDefaultDispose());
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value of the specified interpreter variable, enforcing
        /// that global variables be registered first.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter from which the variable value is read.
        /// </param>
        /// <param name="name">
        /// The name of the variable to read.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the value of the variable.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public ReturnCode GetVariableValue( /* CORE */
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result value,        /* in */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (interpreter.SyncRoot) /* TRANSACTIONAL */
            {
                bool isGlobal = CertificateScriptOps.IsCallFrameGlobal(
                    interpreter, null);

                if (isGlobal && !IsRegisteredVariableName(name))
                {
                    error = String.Format(
                        "variable name {0} not registered",
                        Utility.FormatWrapOrNull(name));

                    return ReturnCode.Error;
                }

                return interpreter.GetVariableValue(
                    Constants.CommandGetVariableFlags, name,
                    ref value, ref error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the value of the specified interpreter variable, registering
        /// global variable names as needed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the variable value is set.
        /// </param>
        /// <param name="name">
        /// The name of the variable to set.
        /// </param>
        /// <param name="value">
        /// The value to assign to the variable.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public ReturnCode SetVariableValue( /* CORE */
            Interpreter interpreter, /* in */
            string name,             /* in */
            string value,            /* in */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (interpreter.SyncRoot) /* TRANSACTIONAL */
            {
                bool isGlobal = CertificateScriptOps.IsCallFrameGlobal(
                    interpreter, null);

                if (isGlobal && !RegisterVariableName(name))
                {
                    error = String.Format(
                        "could not register variable name {0}",
                        Utility.FormatWrapOrNull(name));

                    return ReturnCode.Error;
                }

                return interpreter.SetVariableValue(
                    Constants.CommandSetVariableFlags, name,
                    value, null, ref error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unsets the specified interpreter variable, unregistering global
        /// variable names as needed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the variable is unset.
        /// </param>
        /// <param name="name">
        /// The name of the variable to unset.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public ReturnCode UnsetVariable( /* CORE */
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (interpreter.SyncRoot) /* TRANSACTIONAL */
            {
                bool isGlobal = CertificateScriptOps.IsCallFrameGlobal(
                    interpreter, null);

                if (isGlobal && !IsRegisteredVariableName(name))
                {
                    error = String.Format(
                        "variable name {0} not registered",
                        Utility.FormatWrapOrNull(name));

                    return ReturnCode.Error;
                }

                if (interpreter.UnsetVariable(
                        Constants.CommandUnsetVariableFlags,
                        name, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (isGlobal && !UnregisterVariableName(name))
                {
                    error = String.Format(
                        "could not unregister variable name {0}",
                        Utility.FormatWrapOrNull(name));

                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Merges the registered variables into the supplied dictionary and
        /// then clears the registered variables.
        /// </summary>
        /// <param name="variables">
        /// On input, an optional dictionary to merge into; on return, the
        /// merged dictionary containing the registered variables.
        /// </param>
        public void TakeRegisteredVariables( /* CORE */
            ref ObjectDictionary variables /* in, out */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (registeredVariables != null)
                {
                    ObjectDictionary localVariables;

                    if (variables != null)
                    {
                        localVariables = new ObjectDictionary(
                            (IDictionary<string, object>)variables);
                    }
                    else
                    {
                        localVariables = new ObjectDictionary();
                    }

                    foreach (VariablePair pair in registeredVariables)
                    {
                        if (localVariables.ContainsKey(pair.Key))
                            continue;

                        localVariables.Add(pair.Key, pair.Value);
                    }

                    variables = localVariables;

                    registeredVariables.Clear();
                    registeredVariables = null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a configuration variable with the specified
        /// name exists.
        /// </summary>
        /// <param name="name">
        /// The name of the configuration variable to check.
        /// </param>
        /// <returns>
        /// Non-zero if the configuration variable exists.
        /// </returns>
        public bool HaveConfiguration(
            string name /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (String.IsNullOrEmpty(name))
                    return false;

                if (configuration == null)
                    return false;

                return configuration.ContainsKey(name);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to retrieve the value of the specified configuration
        /// variable.
        /// </summary>
        /// <param name="name">
        /// The name of the configuration variable to retrieve.
        /// </param>
        /// <param name="value">
        /// Upon return, the value of the configuration variable, or null if
        /// it was not found.
        /// </param>
        /// <returns>
        /// Non-zero if the configuration variable was found.
        /// </returns>
        public bool TryGetConfiguration(
            string name,     /* in */
            out string value /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                value = null;

                if (String.IsNullOrEmpty(name))
                    return false;

                if (configuration == null)
                    return false;

                if (!configuration.TryGetValue(name, out value))
                    return false;

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the specified configuration variable, or unsets it when the
        /// value is null or empty.
        /// </summary>
        /// <param name="name">
        /// The name of the configuration variable to set or unset.
        /// </param>
        /// <param name="value">
        /// The value to assign, or null or empty to unset the variable.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public ReturnCode SetOrUnsetConfiguration(
            string name,     /* in */
            string value,    /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (String.IsNullOrEmpty(name))
                {
                    error = "invalid configuration variable name";
                    return ReturnCode.Error;
                }

                if (configuration == null)
                    configuration = new StringDictionary();

                if (String.IsNullOrEmpty(value)) /* NORMALIZE */
                    value = null;

                if (value != null)
                {
                    configuration[name] = value;
                    return ReturnCode.Ok;
                }
                else if (configuration.Remove(name))
                {
                    return ReturnCode.Ok;
                }
                else
                {
                    error = String.Format(
                        "could not remove configuration variable {0}",
                        Utility.FormatWrapOrNull(name));

                    return ReturnCode.Error;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the scope call frame associated with this instance.
        /// </summary>
        /// <returns>
        /// The scope call frame, or null if none has been set.
        /// </returns>
        public ICallFrame GetScopeCallFrame() /* CORE */
        {
            CheckDisposed();

            lock (syncRoot)
            {
                return scopeFrame;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the scope call frame associated with this instance.
        /// </summary>
        /// <param name="frame">
        /// The scope call frame to associate with this instance.
        /// </param>
        public void SetScopeCallFrame( /* CORE */
            ICallFrame frame /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot)
            {
                scopeFrame = frame;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Replaces the rule set associated with this instance, returning the
        /// previous one.
        /// </summary>
        /// <param name="ruleSet">
        /// The new rule set to associate with this instance.
        /// </param>
        /// <returns>
        /// The rule set that was previously associated with this instance.
        /// </returns>
        public IRuleSet ChangeRuleSet( /* CORE */
            IRuleSet ruleSet /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                IRuleSet oldRuleSet = this.ruleSet;

                this.ruleSet = ruleSet;

                return oldRuleSet;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the configuration phase if one has not already been
        /// established.
        /// </summary>
        /// <param name="configurationPhase">
        /// The configuration phase to set.
        /// </param>
        /// <returns>
        /// Non-zero if the configuration phase was set by this call.
        /// </returns>
        public bool MaybeSetConfigurationPhase( /* CORE */
            ConfigurationPhase configurationPhase /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (!HasConfigurationPhase())
                {
                    SetConfigurationPhase(configurationPhase);
                    return true;
                }

                return false;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierName Members
        /// <summary>
        /// The identifier name of this instance.
        /// </summary>
        private string name;
        /// <summary>
        /// Gets or sets the identifier name of this instance.
        /// </summary>
        public string Name
        {
            get { CheckDisposed(); return name; }
            set { CheckDisposed(); name = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierBase Members
        /// <summary>
        /// The identifier kind of this instance.
        /// </summary>
        private IdentifierKind kind;
        /// <summary>
        /// Gets or sets the identifier kind of this instance.
        /// </summary>
        public IdentifierKind Kind
        {
            get { CheckDisposed(); return kind; }
            set { CheckDisposed(); kind = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The unique identifier of this instance.
        /// </summary>
        private Guid id;
        /// <summary>
        /// Gets or sets the unique identifier of this instance.
        /// </summary>
        public Guid Id
        {
            get { CheckDisposed(); return id; }
            set { CheckDisposed(); id = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifier Members
        /// <summary>
        /// The identifier group of this instance.
        /// </summary>
        private string group;
        /// <summary>
        /// Gets or sets the identifier group of this instance.
        /// </summary>
        public string Group
        {
            get { CheckDisposed(); return group; }
            set { CheckDisposed(); group = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The human-readable description of this instance.
        /// </summary>
        private string description;
        /// <summary>
        /// Gets or sets the human-readable description of this instance.
        /// </summary>
        public string Description
        {
            get { CheckDisposed(); return description; }
            set { CheckDisposed(); description = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
        /// <summary>
        /// Returns a string representation of this instance, listing all of
        /// its non-default property values.
        /// </summary>
        /// <returns>
        /// A string describing this instance.
        /// </returns>
        public override string ToString() /* CORE */
        {
            CheckDisposed();

            StringPairList list = new StringPairList();
            object data = base.DataNoThrow;

            if (data != null)
                list.Add("data", data.ToString());

            if (name != null)
                list.Add("name", name);

            if (kind != IdentifierKind.None)
                list.Add("kind", kind.ToString());

            if (!id.Equals(Guid.Empty))
                list.Add("id", id.ToString());

            if (group != null)
                list.Add("group", group);

            if (description != null)
                list.Add("description", description);

            if (contextName != null)
                list.Add("contextName", contextName);

            if (refreshEvent != null)
                list.Add("refreshEvent", refreshEvent.ToString());

            if (sandboxToken != null)
                list.Add("sandboxToken", sandboxToken.ToString());

            if (commandTokens != null)
                list.Add("commandTokens", commandTokens.ToString());

            if (settingsCallback != null)
                list.Add("settingsCallback", settingsCallback.ToString());

            if (ruleSet != null)
                list.Add("ruleSet", ruleSet.ToString());

            Interpreter interpreter = GetInterpreter();

            if (interpreter != null)
                list.Add("interpreter", interpreter.ToString());

            IPlugin plugin = GetPlugin();

            if (plugin != null)
                list.Add("plugin", plugin.ToString());

            if (pluginType != null)
                list.Add("pluginType", pluginType.ToString());

            if (minimumVersion != null)
                list.Add("minimumVersion", minimumVersion.ToString());

            if (maximumVersion != null)
                list.Add("maximumVersion", maximumVersion.ToString());

            if (variantName != null)
                list.Add("variantName", variantName);

            if (hashAlgorithmName != null)
                list.Add("hashAlgorithmName", hashAlgorithmName);

            if (hashKey != null)
                list.Add("hashKey", DataOps.FormatHexadecimal(hashKey));

            if (encoding != null)
                list.Add("encoding", encoding.WebName);

            if (type != null)
                list.Add("type", type);

            if (subType != null)
                list.Add("subType", subType);

            if (directory != null)
                list.Add("directory", directory);

            string fileName = GetFileName();

            if (fileName != null)
                list.Add("fileName", fileName);

            if (stream != null)
                list.Add("stream", stream.ToString());

            if (keyPairs != null)
                list.Add("keyPairs", keyPairs.ToString());

            if (keyPair != null)
                list.Add("keyPair", keyPair.ToString());

            if (keyName != null)
                list.Add("keyName", keyName);

            if (keyRingName != null)
                list.Add("keyRingName", keyRingName);

            if (hashValue != null)
                list.Add("hashValue", DataOps.FormatHexadecimal(hashValue));

            if (signature != null)
                list.Add("signature", DataOps.FormatHexadecimal(signature));

            if (keyUsage != null)
                list.Add("keyUsage", keyUsage);

            if (configurationPhase != ConfigurationPhase.None)
                list.Add("configurationPhase", configurationPhase.ToString());

            if (trustFlags != TrustFlags.None)
                list.Add("trustFlags", trustFlags.ToString());

            PolicyType? policyType = base.PolicyType;

            if (policyType != null)
                list.Add("policyType", ((PolicyType)policyType).ToString());

            ExecutionPolicy? policy = base.ExecutionPolicy;

            if (policy != null)
                list.Add("policy", ((ExecutionPolicy)policy).ToString());

            if (timeout != null)
                list.Add("timeout", ((int)timeout).ToString());

            if (referenceCount != 0)
                list.Add("referenceCount", referenceCount.ToString());

            if (untrusted)
                list.Add("untrusted", untrusted.ToString());

            if (allowRemoteUri)
                list.Add("allowRemoteUri", allowRemoteUri.ToString());

            if (useContext)
                list.Add("useContext", useContext.ToString());

            if (withCommands)
                list.Add("withCommands", withCommands.ToString());

            if (removeCommands)
                list.Add("removeCommands", removeCommands.ToString());

            if (swapCommands)
                list.Add("swapCommands", swapCommands.ToString());

            if (noGlobalOnly)
                list.Add("noGlobalOnly", noGlobalOnly.ToString());

            if (allowLocalPolicy)
                list.Add("allowLocalPolicy", allowLocalPolicy.ToString());

            if (extractAndApply)
                list.Add("extractAndApply", extractAndApply.ToString());

            if (failOnError)
                list.Add("failOnError", failOnError.ToString());

            if (fatalError)
                list.Add("fatalError", fatalError.ToString());

            return list.ToString();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Creates a new instance by extracting its properties from the
        /// specified generic client data, filling in defaults as needed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to resolve referenced objects and values.
        /// </param>
        /// <param name="plugin">
        /// The plugin used as a fallback when none is specified in the client
        /// data.
        /// </param>
        /// <param name="variantName">
        /// The variant name used as a fallback when none is specified in the
        /// client data.
        /// </param>
        /// <param name="anyClientData">
        /// The generic client data from which the properties are extracted.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The new instance, or null if an error occurs.
        /// </returns>
        public static EvaluateClientData CreateFrom( /* CORE */
            Interpreter interpreter,      /* in: OPTIONAL */
            IPlugin plugin,               /* in: OPTIONAL */
            string variantName,           /* in: OPTIONAL */
            IAnyClientData anyClientData, /* in */
            ref Result error              /* out */
            )
        {
            if (anyClientData == null)
            {
                error = "invalid client data";
                return null;
            }

            ///////////////////////////////////////////////////////////////////

            #region Extract Properties from AnyClientData
            object data = null;

            if (anyClientData.HasAny("data"))
            {
                IObject @object;

                if (!anyClientData.TryGetObject(
                        interpreter, "data", true,
                        out @object, ref error))
                {
                    return null;
                }

                data = (@object != null) ? @object.Value : null;
            }

            ///////////////////////////////////////////////////////////////////

            string name = null;

            if (anyClientData.HasAny("name"))
            {
                if (!anyClientData.TryGetString(
                        "name", true,
                        out name, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            Guid id = Guid.Empty;

            if (anyClientData.HasAny("id"))
            {
                if (!anyClientData.TryGetGuid(
                        "id", true,
                        out id, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string group = null;

            if (anyClientData.HasAny("group"))
            {
                if (!anyClientData.TryGetString(
                        "group", true,
                        out group, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string description = null;

            if (anyClientData.HasAny("description"))
            {
                if (!anyClientData.TryGetString(
                        "description", true,
                        out description, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string contextName = null;

            if (anyClientData.HasAny("contextName"))
            {
                if (!anyClientData.TryGetString(
                        "contextName", true,
                        out contextName, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            SharedEventWaitHandle refreshEvent = null;

            if (anyClientData.HasAny("refreshEvent"))
            {
                IObject @object;

                if (!anyClientData.TryGetObject(
                        interpreter, "refreshEvent", true,
                        out @object, ref error))
                {
                    return null;
                }

                refreshEvent = (@object != null) ?
                    @object.Value as SharedEventWaitHandle : null;
            }

            ///////////////////////////////////////////////////////////////////

            ulong? sandboxToken = null;

            if (anyClientData.HasAny("sandboxToken"))
            {
                ulong ulongValue;

                if (!anyClientData.TryGetUnsignedWideInteger(
                        "sandboxToken", true,
                        out ulongValue, ref error))
                {
                    return null;
                }

                sandboxToken = ulongValue;
            }

            ///////////////////////////////////////////////////////////////////

            LongList commandTokens = null;

            if (anyClientData.HasAny("commandTokens"))
            {
                IObject @object;

                if (!anyClientData.TryGetObject(
                        interpreter, "commandTokens", true,
                        out @object, ref error))
                {
                    return null;
                }

                commandTokens = (@object != null) ?
                    @object.Value as LongList : null;
            }

            ///////////////////////////////////////////////////////////////////

            GetFileNameCallback settingsCallback = null;

            if (anyClientData.HasAny("useSettingsCallback"))
            {
                bool boolValue;

                if (!anyClientData.TryGetBoolean(
                        "useSettingsCallback", true,
                        out boolValue, ref error))
                {
                    return null;
                }

                settingsCallback = boolValue ?
                    (GetFileNameCallback)GetSettingsFileName : null;
            }

            ///////////////////////////////////////////////////////////////////

            IRuleSet ruleSet = null;

            if (anyClientData.HasAny("ruleSet"))
            {
                if (!anyClientData.TryGetRuleSet(
                        interpreter, "ruleSet", true,
                        out ruleSet, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            Interpreter localInterpreter = interpreter;

            if (anyClientData.HasAny("interpreter"))
            {
                if (!anyClientData.TryGetInterpreter(
                        interpreter, "interpreter", true,
                        out localInterpreter, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            IPlugin localPlugin = null;
            Type localPluginType = null;

            if (anyClientData.HasAny("plugin"))
            {
                if (!anyClientData.TryGetPlugin(
                        interpreter, "plugin", true,
                        out localPlugin, ref error))
                {
                    return null;
                }

                localPluginType = (localPlugin != null) ?
                    localPlugin.GetType() : null;
            }

            ///////////////////////////////////////////////////////////////////

            Version minimumVersion = null;

            if (anyClientData.HasAny("minimumVersion"))
            {
                if (!anyClientData.TryGetVersion(
                        "minimumVersion", true,
                        out minimumVersion, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            Version maximumVersion = null;

            if (anyClientData.HasAny("maximumVersion"))
            {
                if (!anyClientData.TryGetVersion(
                        "maximumVersion", true,
                        out maximumVersion, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string localVariantName = null;

            if (anyClientData.HasAny("variantName"))
            {
                if (!anyClientData.TryGetString(
                        "variantName", true,
                        out localVariantName, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string hashAlgorithmName = null;

            if (anyClientData.HasAny("hashAlgorithmName"))
            {
                if (!anyClientData.TryGetString(
                        "hashAlgorithmName", true,
                        out hashAlgorithmName, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            byte[] hashKey = null;

            if (anyClientData.HasAny("hashKey"))
            {
                if (!anyClientData.TryGetByteArray(
                        "hashKey", true,
                        out hashKey, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            Encoding encoding = null;

            if (anyClientData.HasAny("encoding"))
            {
                if (!anyClientData.TryGetEncoding(
                        interpreter, "encoding", true,
                        out encoding, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string type = null;

            if (anyClientData.HasAny("type"))
            {
                if (!anyClientData.TryGetString(
                        "type", true,
                        out type, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string subType = null;

            if (anyClientData.HasAny("subType"))
            {
                if (!anyClientData.TryGetString(
                        "subType", true,
                        out subType, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string directory = null;

            if (anyClientData.HasAny("directory"))
            {
                if (!anyClientData.TryGetString(
                        "directory", true,
                        out directory, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string fileName = null;

            if (anyClientData.HasAny("fileName"))
            {
                if (!anyClientData.TryGetString(
                        "fileName", true,
                        out fileName, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            Assembly assembly = null;
            Stream stream = null;

            if (anyClientData.HasAny("stream"))
            {
                ResultList errors = null;
                Result localError; /* REUSED */
                IObject @object;

                localError = null;

                if (anyClientData.TryGetObject(
                        interpreter, "stream", true,
                        out @object, ref localError))
                {
                    stream = (@object != null) ?
                        @object.Value as Stream : null;
                }
                else
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    string streamString;

                    localError = null;

                    if (anyClientData.TryGetString(
                            "stream", true, out streamString,
                            ref localError))
                    {
                        localError = null;

                        if (CertificateSharedOps.GetStream(
                                interpreter, streamString,
                                ref assembly, ref stream,
                                ref localError) != ReturnCode.Ok)
                        {
                            if (localError != null)
                            {
                                if (errors == null)
                                    errors = new ResultList();

                                errors.Add(localError);
                            }

                            if (errors != null)
                                error = errors;

                            return null;
                        }
                    }
                    else
                    {
                        if (localError != null)
                        {
                            if (errors == null)
                                errors = new ResultList();

                            errors.Add(localError);
                        }

                        if (errors != null)
                            error = errors;

                        return null;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            IEnumerable<IKeyPair> keyPairs = null;

            if (anyClientData.HasAny("keyPairs"))
            {
                IObject @object;

                if (!anyClientData.TryGetObject(
                        interpreter, "keyPairs", true,
                        out @object, ref error))
                {
                    return null;
                }

                keyPairs = (@object != null) ?
                    @object.Value as IEnumerable<IKeyPair> : null;
            }

            ///////////////////////////////////////////////////////////////////

            IKeyPair keyPair = null;

            if (anyClientData.HasAny("keyPair"))
            {
                IObject @object;

                if (!anyClientData.TryGetObject(
                        interpreter, "keyPair", true,
                        out @object, ref error))
                {
                    return null;
                }

                keyPair = (@object != null) ?
                    @object.Value as IKeyPair : null;
            }

            ///////////////////////////////////////////////////////////////////

            string keyName = null;

            if (anyClientData.HasAny("keyName"))
            {
                if (!anyClientData.TryGetString(
                        "keyName", true,
                        out keyName, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string keyRingName = null;

            if (anyClientData.HasAny("keyRingName"))
            {
                if (!anyClientData.TryGetString(
                        "keyRingName", true,
                        out keyRingName, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            byte[] hashValue = null;

            if (anyClientData.HasAny("hashValue"))
            {
                if (!anyClientData.TryGetByteArray(
                        "hashValue", true,
                        out hashValue, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            byte[] signature = null;

            if (anyClientData.HasAny("signature"))
            {
                if (!anyClientData.TryGetByteArray(
                        "signature", true,
                        out signature, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            string keyUsage = null;

            if (anyClientData.HasAny("keyUsage"))
            {
                if (!anyClientData.TryGetString(
                        "keyUsage", true,
                        out keyUsage, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            ConfigurationPhase configurationPhase = ConfigurationPhase.Unknown;

            if (anyClientData.HasAny("configurationPhase"))
            {
                Enum enumValue;

                if (!anyClientData.TryGetEnum(interpreter,
                        "configurationPhase", typeof(ConfigurationPhase),
                        true, out enumValue, ref error))
                {
                    return null;
                }

                configurationPhase = (ConfigurationPhase)enumValue;
            }

            ///////////////////////////////////////////////////////////////////

            TrustFlags trustFlags = Constants.DefaultTrustFlags;

            if (anyClientData.HasAny("trustFlags"))
            {
                Enum enumValue;

                if (!anyClientData.TryGetEnum(interpreter,
                        "trustFlags", typeof(TrustFlags),
                        true, out enumValue, ref error))
                {
                    return null;
                }

                trustFlags = (TrustFlags)enumValue;
            }

            ///////////////////////////////////////////////////////////////////

            PolicyType? policyType = null;

            if (anyClientData.HasAny("policyType"))
            {
                Enum enumValue;

                if (!anyClientData.TryGetEnum(interpreter,
                        "policyType", typeof(PolicyType),
                        true, out enumValue, ref error))
                {
                    return null;
                }

                policyType = (PolicyType)enumValue;
            }

            ///////////////////////////////////////////////////////////////////

            ExecutionPolicy? policy = null;

            if (anyClientData.HasAny("policy"))
            {
                Enum enumValue;

                if (!anyClientData.TryGetEnum(interpreter,
                        "policy", typeof(ExecutionPolicy),
                        true, out enumValue, ref error))
                {
                    return null;
                }

                policy = (ExecutionPolicy)enumValue;
            }

            ///////////////////////////////////////////////////////////////////

            int? timeout = null;

            if (anyClientData.HasAny("timeout"))
            {
                int intValue = 0;

                if (anyClientData.TryGetInteger(
                        "timeout", true,
                        out intValue, ref error))
                {
                    timeout = intValue;
                }
                else
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            int referenceCount = 0;

            if (anyClientData.HasAny("referenceCount"))
            {
                if (!anyClientData.TryGetInteger(
                        "referenceCount", true,
                        out referenceCount, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool untrusted = false;

            if (anyClientData.HasAny("untrusted"))
            {
                if (!anyClientData.TryGetBoolean(
                        "untrusted", true,
                        out untrusted, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool allowRemoteUri = false;

            if (anyClientData.HasAny("allowRemoteUri"))
            {
                if (!anyClientData.TryGetBoolean(
                        "allowRemoteUri", true,
                        out allowRemoteUri, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool useContext = false;

            if (anyClientData.HasAny("useContext"))
            {
                if (!anyClientData.TryGetBoolean(
                        "useContext", true,
                        out useContext, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool withCommands = false;

            if (anyClientData.HasAny("withCommands"))
            {
                if (!anyClientData.TryGetBoolean(
                        "withCommands", true,
                        out withCommands, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool removeCommands = false;

            if (anyClientData.HasAny("removeCommands"))
            {
                if (!anyClientData.TryGetBoolean(
                        "removeCommands", true,
                        out removeCommands, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool swapCommands = false;

            if (anyClientData.HasAny("swapCommands"))
            {
                if (!anyClientData.TryGetBoolean(
                        "swapCommands", true,
                        out swapCommands, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool noGlobalOnly = false;

            if (anyClientData.HasAny("noGlobalOnly"))
            {
                if (!anyClientData.TryGetBoolean(
                        "noGlobalOnly", true,
                        out noGlobalOnly, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool allowLocalPolicy = false;

            if (anyClientData.HasAny("allowLocalPolicy"))
            {
                if (!anyClientData.TryGetBoolean(
                        "allowLocalPolicy", true,
                        out allowLocalPolicy, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool extractAndApply = false;

            if (anyClientData.HasAny("extractAndApply"))
            {
                if (!anyClientData.TryGetBoolean(
                        "extractAndApply", true,
                        out extractAndApply, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool failOnError = false;

            if (anyClientData.HasAny("failOnError"))
            {
                if (!anyClientData.TryGetBoolean(
                        "failOnError", true,
                        out failOnError, ref error))
                {
                    return null;
                }
            }

            ///////////////////////////////////////////////////////////////////

            bool fatalError = false;

            if (anyClientData.HasAny("fatalError"))
            {
                if (!anyClientData.TryGetBoolean(
                        "fatalError", true,
                        out fatalError, ref error))
                {
                    return null;
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Load Property Defaults
            if (keyPairs == null)
            {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                if (CertificateKeyPairOps.GetEmbeddedPublicOnly( /* OK */
                        CertificateAssemblyOps.GetObject(),
                        null, false, ref keyPairs,
                        ref error) != ReturnCode.Ok)
                {
                    return null;
                }
#else
                if (CertificateKeyPairOps.GetAssemblyPublicOnly( /* OK */
                        CertificateAssemblyOps.GetObject(),
                        CertificateAssemblyOps.GetName(),
                        ref keyPairs, ref error) != ReturnCode.Ok)
                {
                    return null;
                }
#endif
            }

            ///////////////////////////////////////////////////////////////////

            if (signature == null)
            {
                string signatureFileName = DataOps.FormatSignatureFileName(
                    fileName);

                if (stream != null)
                {
                    if (!DataOps.TryReadSignatureStream(
                            assembly, encoding, signatureFileName,
                            ref signature, ref error))
                    {
                        return null;
                    }
                }
                else
                {
                    if (!DataOps.TryReadSignatureFile(
                            interpreter, encoding, signatureFileName,
                            timeout, allowRemoteUri, ref signature,
                            ref error))
                    {
                        return null;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////////

            if (encoding == null)
                encoding = DataOps.GetDefaultEncoding();

            ///////////////////////////////////////////////////////////////////

            if (keyUsage == null)
                keyUsage = _KeyUsage.Source;

            ///////////////////////////////////////////////////////////////////

            if (useContext)
            {
                if (localPlugin == null)
                {
                    localPlugin = plugin;

                    localPluginType = (localPlugin != null) ?
                        localPlugin.GetType() : null;
                }

                if (localVariantName == null)
                    localVariantName = GetVariantName(variantName);
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            return new EvaluateClientData(
                DataOps.GetCultureInfo(anyClientData), data,
                name, id, group, description, contextName,
                refreshEvent, sandboxToken, commandTokens,
                settingsCallback, ruleSet, localInterpreter,
                localPlugin, localPluginType, minimumVersion,
                maximumVersion, localVariantName,
                CertificateSharedOps.GetHashAlgorithm(
                    hashAlgorithmName, keyPairs, null,
                    HashAlgorithmType.CommandUse), hashKey,
                encoding, type, subType, directory, fileName,
                stream, keyPairs, keyPair, keyName,
                keyRingName, hashValue, signature, keyUsage,
                configurationPhase, trustFlags, policyType,
                policy, timeout, referenceCount, untrusted,
                allowRemoteUri, useContext, withCommands,
                removeCommands, swapCommands, noGlobalOnly,
                allowLocalPolicy, extractAndApply,
                failOnError, fatalError);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resets the per-script state of the specified instance in
        /// preparation for evaluating a new script, optionally adopting the
        /// supplied script data.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose per-script state is reset.
        /// </param>
        /// <param name="data">
        /// The script data to adopt, or null to leave the file, stream, hash,
        /// and signature cleared.
        /// </param>
        public static void ForNewScript( /* CORE */
            EvaluateClientData clientData, /* in */
            FileAndOrStreamData data       /* in */
            )
        {
            if (clientData != null)
            {
                clientData.ResetFullFileName();

                clientData.Type = null;
                clientData.SubType = null;

                if (data != null)
                {
                    clientData.FileName = data.FileName;
                    clientData.Stream = data.Stream;
                    clientData.HashValue = data.HashValue;
                    clientData.Signature = data.Signature;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a copy of the specified instance configured to evaluate a
        /// new script read from the given file name.
        /// </summary>
        /// <param name="clientData">
        /// The instance to copy.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter to associate with the new instance.
        /// </param>
        /// <param name="fileName">
        /// The file name of the script to read.
        /// </param>
        /// <param name="errors">
        /// On input, an optional list to which errors are appended; on
        /// return, the list of any errors that occurred while reading the
        /// script.
        /// </param>
        /// <returns>
        /// The new instance configured for the script.
        /// </returns>
        public static EvaluateClientData ForNewScript( /* CORE */
            EvaluateClientData clientData, /* in */
            Interpreter interpreter,       /* in */
            string fileName,               /* in */
            ref ResultList errors          /* in, out */
            )
        {
            EvaluateClientData newClientData =
                new EvaluateClientData(clientData);

            /* IGNORED */
            newClientData.AttachTo(clientData);

            newClientData.Interpreter = interpreter;
            newClientData.Type = null;
            newClientData.SubType = null;
            newClientData.FileName = null;
            newClientData.Stream = null;
            newClientData.HashValue = null;
            newClientData.Signature = null;

            FileAndOrStreamData fileData = null;
            FileAndOrStreamData streamData = null;

            /* NO RESULT */
            CertificateScriptOps.ReadFileAndOrStream(
                newClientData.Interpreter, null,
                newClientData.Encoding, fileName,
                newClientData.Timeout,
                newClientData.AllowRemoteUri,
                true, ref fileData, ref streamData,
                ref errors);

            if (fileData != null)
            {
                newClientData.FileName = fileData.FileName;
                newClientData.Stream = fileData.Stream;
                newClientData.HashValue = fileData.HashValue;
                newClientData.Signature = fileData.Signature;
            }

            return newClientData;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the script type and sub-type on the specified instance.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose type and sub-type are set.
        /// </param>
        /// <param name="type">
        /// The script type to set.
        /// </param>
        /// <param name="subType">
        /// The script sub-type to set.
        /// </param>
        public static void ForNewTypeAndSubType( /* CORE */
            EvaluateClientData clientData, /* in */
            string type,                   /* in */
            string subType                 /* in */
            )
        {
            if (clientData != null)
            {
                clientData.Type = type;
                clientData.SubType = subType;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the current plugin version satisfies the minimum and
        /// maximum version constraints recorded on the specified instance.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose version constraints are checked.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the version is acceptable;
        /// otherwise, a failure code.
        /// </returns>
        public static ReturnCode CheckRequiredVersion( /* CORE */
            EvaluateClientData clientData, /* in */
            ref Result error               /* out */
            )
        {
            if (clientData == null)
            {
                error = "invalid clientData";
                return ReturnCode.Error;
            }

            IPluginData pluginData = clientData.Plugin;
            Version pluginVersion = null;

            if (pluginData != null)
                pluginVersion = pluginData.Version;
            else
                pluginVersion = CertificateAssemblyOps.GetVersion();

            if (pluginVersion == null)
            {
                error = "invalid plugin version";
                return ReturnCode.Error;
            }

            Version emptyVersion = Constants.EmptyVersion;
            Version minimumVersion = clientData.MinimumVersion;

            if ((minimumVersion != null) &&
                ((emptyVersion == null) ||
                    (minimumVersion != emptyVersion)) &&
                (pluginVersion < minimumVersion))
            {
                error = String.Format(
                    "plugin version {0} is less than " +
                    "minimum allowed script version {1}",
                    Utility.FormatWrapOrNull(pluginVersion),
                    Utility.FormatWrapOrNull(minimumVersion));

                return ReturnCode.Error;
            }

            Version maximumVersion = clientData.MaximumVersion;

            if ((maximumVersion != null) &&
                ((emptyVersion == null) ||
                    (maximumVersion != emptyVersion)) &&
                (pluginVersion > maximumVersion))
            {
                error = String.Format(
                    "plugin version {0} is greater than " +
                    "maximum allowed script version {1}",
                    Utility.FormatWrapOrNull(pluginVersion),
                    Utility.FormatWrapOrNull(maximumVersion));

                return ReturnCode.Error;
            }

            if ((minimumVersion == null) && (maximumVersion == null))
            {
                if (clientData.CanHaveMissingVersions())
                    return ReturnCode.Ok;

                error = "allowed script versions missing";
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the minimum and maximum version constraints recorded on the
        /// specified instance.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose version constraints are cleared.
        /// </param>
        /// <param name="force">
        /// Non-zero to clear the constraints even if they are already
        /// invalid.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public static ReturnCode ResetRequiredVersion( /* CORE */
            EvaluateClientData clientData, /* in */
            bool force                     /* in */
            )
        {
            Result error = null;

            return ResetRequiredVersion(clientData, force, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the minimum and maximum version constraints recorded on the
        /// specified instance.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose version constraints are cleared.
        /// </param>
        /// <param name="force">
        /// Non-zero to clear the constraints even if they are already
        /// invalid.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public static ReturnCode ResetRequiredVersion( /* CORE */
            EvaluateClientData clientData, /* in */
            bool force,                    /* in */
            ref Result error               /* out */
            )
        {
            if (clientData == null)
            {
                error = "invalid clientData";
                return ReturnCode.Error;
            }

            Version oldMinimumVersion = clientData.MinimumVersion;

            if (!force && (oldMinimumVersion == null))
            {
                error = "minimum version is already invalid";
                return ReturnCode.Error;
            }

            Version oldMaximumVersion = clientData.MaximumVersion;

            if (!force && (oldMaximumVersion == null))
            {
                error = "maximum version is already invalid";
                return ReturnCode.Error;
            }

            clientData.MinimumVersion = null;
            clientData.MaximumVersion = null;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the minimum and maximum version constraints on the specified
        /// instance, refusing to change constraints that are already set.
        /// </summary>
        /// <param name="clientData">
        /// The instance whose version constraints are set.
        /// </param>
        /// <param name="newMinimumVersion">
        /// The new minimum version to set, or null to leave it unchanged.
        /// </param>
        /// <param name="newMaximumVersion">
        /// The new maximum version to set, or null to leave it unchanged.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public static ReturnCode SetRequiredVersion( /* CORE */
            EvaluateClientData clientData, /* in */
            Version newMinimumVersion,     /* in: OPTIONAL */
            Version newMaximumVersion,     /* in: OPTIONAL */
            ref Result error               /* out */
            )
        {
            if (clientData == null)
            {
                error = "invalid clientData";
                return ReturnCode.Error;
            }

            Version oldMinimumVersion = clientData.MinimumVersion;

            if ((oldMinimumVersion != null) &&
                (newMinimumVersion != null) &&
                !newMinimumVersion.Equals(oldMinimumVersion))
            {
                error = "minimum version cannot be changed once set";
                return ReturnCode.Error;
            }

            Version oldMaximumVersion = clientData.MaximumVersion;

            if ((oldMaximumVersion != null) &&
                (newMaximumVersion != null) &&
                !newMaximumVersion.Equals(oldMaximumVersion))
            {
                error = "maximum version cannot be changed once set";
                return ReturnCode.Error;
            }

            if ((newMinimumVersion != null) &&
                (newMaximumVersion != null) &&
                (newMinimumVersion > newMaximumVersion))
            {
                error = "minimum version cannot exceed maximum version";
                return ReturnCode.Error;
            }

            if ((oldMinimumVersion == null) && (newMinimumVersion != null))
                clientData.MinimumVersion = newMinimumVersion;

            if ((oldMaximumVersion == null) && (newMaximumVersion != null))
                clientData.MaximumVersion = newMaximumVersion;

            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// Gets the variant name to use, falling back to the assembly
        /// configuration when none is supplied.
        /// </summary>
        /// <param name="variantName">
        /// The supplied variant name, or null to use the default.
        /// </param>
        /// <returns>
        /// The variant name to use.
        /// </returns>
        private static string GetVariantName( /* CORE */
            string variantName /* in: OPTIONAL */
            )
        {
            if (variantName != null)
                return variantName;

            //
            // TODO: Perhaps be able to do something else here?
            //
            return CertificateAssemblyOps.GetConfiguration();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the interpreter settings file name for the specified plugin.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin for which the settings file name is obtained.
        /// </param>
        /// <returns>
        /// The settings file name, or null if one is not available.
        /// </returns>
        private static string GetSettingsFileName( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            //
            // TODO: Perhaps be able to do something else here?
            //
            return CertificateScriptOps.GetSettingsFileName(pluginData);
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
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed and disposed-object
        /// checking is enabled.
        /// </exception>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed &&
                Engine.IsThrowOnDisposed(null, false))
            {
                throw new ObjectDisposedException(
                    typeof(EvaluateClientData).Name);
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
        protected override void Dispose( /* CORE */
            bool disposing /* in */
            )
        {
            try
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        ////////////////////////////////////
                        // dispose managed resources here...
                        ////////////////////////////////////

                        lock (syncRoot) /* TRANSACTIONAL */
                        {
                            name = null;
                            kind = IdentifierKind.None;
                            id = Guid.Empty;
                            group = null;
                            description = null;
                            contextName = null;

                            if (refreshEvent != null)
                            {
                                refreshEvent.Close();
                                refreshEvent = null;
                            }

                            sandboxToken = null;

                            if (commandTokens != null)
                            {
                                if (!wasCopied) /* OWNED? */
                                    commandTokens.Clear();

                                commandTokens = null;
                            }

                            settingsCallback = null;

                            DisposeRuleSet();

                            pluginType = null;
                            minimumVersion = null;
                            maximumVersion = null;
                            variantName = null;
                            hashAlgorithmName = null;
                            hashKey = null;
                            encoding = null;
                            type = null;
                            subType = null;
                            directory = null;
                            stream = null; /* NOT OWNED */
                            keyPairs = null;
                            keyPair = null;
                            keyName = null;
                            keyRingName = null;
                            hashValue = null;
                            signature = null;
                            keyUsage = null;
                            configurationPhase = ConfigurationPhase.None;
                            trustFlags = TrustFlags.None;
                            timeout = null;

                            Interlocked.Exchange(
                                ref referenceCount, 0);

                            untrusted = false;
                            allowRemoteUri = false;
                            useContext = false;
                            withCommands = false;
                            removeCommands = false;
                            noGlobalOnly = false;
                            allowLocalPolicy = false;
                            extractAndApply = false;
                            failOnError = false;
                            fatalError = false;

                            ////////////////////////////////

                            if (sandboxes != null)
                            {
                                sandboxes.Clear();
                                sandboxes = null;
                            }

                            if (scriptQueue != null)
                            {
                                scriptQueue.Clear();
                                scriptQueue = null;
                            }

                            if (registeredObjectNames != null)
                            {
                                registeredObjectNames.Clear();
                                registeredObjectNames = null;
                            }

                            if (registeredVariables != null)
                            {
                                registeredVariables.Clear();
                                registeredVariables = null;
                            }

                            if (configuration != null)
                            {
                                configuration.Clear();
                                configuration = null;
                            }

                            scopeFrame = null; /* NOT OWNED */
                            swapToken = 0;
                            wasCopied = false;
                        }
                    }

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////
                }
            }
            finally
            {
                base.Dispose(disposing);

                disposed = true;
            }
        }
        #endregion
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Simple Signed Script Evaluation Helper Class (SSS)
    /// <summary>
    /// Provides the core operations for verifying and evaluating signed
    /// scripts, including signature verification, key usage checks, and
    /// interpreter creation.
    /// </summary>
    [ObjectId("3a4fd9f3-6b04-4754-994a-56f7ff4a8d69")]
    internal static class CertificateScriptOps
    {
        /// <summary>
        /// Determines whether the specified call frame is the global call
        /// frame of the interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose call frame is examined.
        /// </param>
        /// <param name="frame">
        /// The call frame to check, or null to use the current call frame.
        /// </param>
        /// <returns>
        /// Non-zero if the call frame is the global call frame.
        /// </returns>
        public static bool IsCallFrameGlobal( /* CORE */
            Interpreter interpreter, /* in */
            ICallFrame frame         /* in: OPTIONAL */
            )
        {
            if (interpreter == null)
                return false;

            bool? result = interpreter.HasCallFrameFlags(
                frame, CallFrameFlags.Global, true);

            return ((result != null) && (bool)result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the interpreter settings file name configured for the
        /// specified plugin, falling back to the plugin-independent setting.
        /// </summary>
        /// <param name="pluginData">
        /// The plugin for which the settings file name is obtained.
        /// </param>
        /// <returns>
        /// The configured settings file name, or null if none is configured.
        /// </returns>
        public static string GetSettingsFileName( /* CORE */
            IPluginData pluginData /* in */
            )
        {
            foreach (IPluginData localPluginData in new IPluginData[] {
                    pluginData, null
                })
            {
                string result = Configuration.GetVariable(
                    DataOps.FormatWithPluginData(localPluginData,
                    Constants.ScriptInterpreterSettingsEnvVarFormat,
                    Constants.ScriptInterpreterSettingsEnvVarName));

                if (!String.IsNullOrEmpty(result))
                    return result;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a file-and-or-stream data item from the supplied script and
        /// signature text, when all of the required inputs are present.
        /// </summary>
        /// <param name="fileName">
        /// The file name to associate with the resulting data item.
        /// </param>
        /// <param name="text">
        /// The script text to wrap in a stream.
        /// </param>
        /// <param name="signatureText">
        /// The textual signature to parse and associate with the script.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert the script text to a stream. This
        /// value may be null.
        /// </param>
        /// <param name="textData">
        /// Upon success, receives the constructed file-and-or-stream data
        /// item.
        /// </param>
        /// <param name="errors">
        /// On input, an optional list to which errors are appended; on
        /// return, the list of any errors that occurred.
        /// </param>
        public static void MaybeGetStreamFrom( /* CORE */
            string fileName,                  /* in */
            string text,                      /* in */
            string signatureText,             /* in */
            Encoding encoding,                /* in: OPTIONAL */
            ref FileAndOrStreamData textData, /* out */
            ref ResultList errors             /* out */
            )
        {
            Result localError; /* REUSED */

            if (String.IsNullOrEmpty(fileName))
                return;

            if (String.IsNullOrEmpty(text))
                return;

            if (String.IsNullOrEmpty(signatureText))
                return;

            Stream stream;

            localError = null;

            stream = DataOps.GetScriptStream(
                text, encoding, ref localError);

            if (stream == null)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                return;
            }

            byte[] signature = null;

            localError = null;

            if (!DataOps.TryParseSignature(
                    signatureText, true, ref signature,
                    ref localError))
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                return;
            }

            textData = new FileAndOrStreamData(
                null, fileName, stream, null, signature);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the script and its signature from the file system and,
        /// unless skipped, from embedded assembly resources, producing the
        /// corresponding file-and-or-stream data items.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used when reading the signature file. This value
        /// may be null.
        /// </param>
        /// <param name="assembly">
        /// The assembly used to resolve embedded resources. This value may be
        /// null.
        /// </param>
        /// <param name="encoding">
        /// The encoding used when reading the signature. This value may be
        /// null.
        /// </param>
        /// <param name="scriptFileName">
        /// The file name of the script to read.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data. This
        /// value may be null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the script and signature to be read from remote
        /// locations.
        /// </param>
        /// <param name="skipEmbedded">
        /// Non-zero to skip looking for the script in embedded assembly
        /// resources.
        /// </param>
        /// <param name="fileData">
        /// Upon success, receives the data item read from the file system.
        /// </param>
        /// <param name="streamData">
        /// Upon success, receives the data item read from embedded resources.
        /// </param>
        /// <param name="errors">
        /// On input, an optional list to which errors are appended; on
        /// return, the list of any errors that occurred.
        /// </param>
        public static void ReadFileAndOrStream( /* CORE */
            Interpreter interpreter,            /* in: OPTIONAL */
            Assembly assembly,                  /* in: OPTIONAL */
            Encoding encoding,                  /* in: OPTIONAL */
            string scriptFileName,              /* in */
            int? timeout,                       /* in: OPTIONAL */
            bool allowRemoteUri,                /* in */
            bool skipEmbedded,                  /* in */
            ref FileAndOrStreamData fileData,   /* out */
            ref FileAndOrStreamData streamData, /* out */
            ref ResultList errors               /* out */
            )
        {
            Result localError; /* REUSED */

            if (String.IsNullOrEmpty(scriptFileName))
                return;

            string signatureFileName = DataOps.FormatSignatureFileName(
                scriptFileName);

            if (String.IsNullOrEmpty(signatureFileName))
                return;

            if (Utility.IsRemoteUri(scriptFileName))
            {
                if (!allowRemoteUri)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "script file {0} cannot be a remote uri",
                        Utility.FormatWrapOrNull(scriptFileName)));

                    goto checkForEmbedded;
                }
            }
            else if (!File.Exists(scriptFileName))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "script file {0} does not exist",
                    Utility.FormatWrapOrNull(scriptFileName)));

                goto checkForEmbedded;
            }

            if (Utility.IsRemoteUri(signatureFileName))
            {
                if (!allowRemoteUri)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(String.Format(
                        "signature file {0} cannot be a remote uri",
                        Utility.FormatWrapOrNull(signatureFileName)));

                    goto checkForEmbedded;
                }
            }
            else if (!File.Exists(signatureFileName))
            {
                if (errors == null)
                    errors = new ResultList();

                errors.Add(String.Format(
                    "signature file {0} does not exist",
                    Utility.FormatWrapOrNull(signatureFileName)));

                goto checkForEmbedded;
            }

            byte[] fileSignature = null;

            localError = null; /* REUSED */

            if (!DataOps.TryReadSignatureFile(
                    interpreter, encoding, signatureFileName,
                    timeout, allowRemoteUri, ref fileSignature,
                    ref localError))
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                goto checkForEmbedded;
            }

            fileData = new FileAndOrStreamData(assembly,
                scriptFileName, null, null, fileSignature);

        checkForEmbedded:

            if (skipEmbedded)
                return;

            string scriptFileNameOnly = Path.GetFileName(
                scriptFileName);

            if (String.IsNullOrEmpty(scriptFileNameOnly))
                return;

            string signatureFileNameOnly = DataOps.FormatSignatureFileName(
                scriptFileNameOnly);

            if (String.IsNullOrEmpty(signatureFileNameOnly))
                return;

            Stream stream;

            localError = null;

            stream = CertificateSharedOps.GetStream(
                assembly, scriptFileNameOnly, ref localError);

            if (stream == null)
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                return;
            }

            byte[] streamSignature = null;

            localError = null;

            if (!DataOps.TryReadSignatureStream(
                    assembly, encoding, signatureFileNameOnly,
                    ref streamSignature, ref localError))
            {
                if (localError != null)
                {
                    if (errors == null)
                        errors = new ResultList();

                    errors.Add(localError);
                }

                return;
            }

            streamData = new FileAndOrStreamData(assembly,
                scriptFileNameOnly, stream, null, streamSignature);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines the script type based on the key pair that verified its
        /// signature.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair that verified the script, or null if the script is
        /// unsigned.
        /// </param>
        /// <returns>
        /// The trusted, signed, or unsigned script type.
        /// </returns>
        private static string GetScriptTypeFrom( /* CORE */
            IKeyPair keyPair /* in */
            )
        {
            if (keyPair != null)
            {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                if (CertificateKeyPairOps.HasTrustRootPublicKeyToken(
                        keyPair))
                {
                    return Constants.ScriptTypeTrusted;
                }
                else
#endif
                {
                    return Constants.ScriptTypeSigned;
                }
            }
            else
            {
                return Constants.ScriptTypeUnsigned;
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Decrypts the supplied encrypted script text, resolving the
        /// password from the application domain, environment, or a remote
        /// location as needed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while resolving the password. This value may
        /// be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin used while resolving the password. This value may be
        /// null.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to convert the decrypted bytes back to text.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while extracting cryptographic parameters. This
        /// value may be null.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when retrieving a remote
        /// password. This value may be null.
        /// </param>
        /// <param name="text">
        /// On input, the encrypted script text; on success, the decrypted
        /// script text.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public static ReturnCode Decrypt( /* CORE */
            Interpreter interpreter, /* in: OPTIONAL */
            IPluginData pluginData,  /* in: OPTIONAL */
            Encoding encoding,       /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            int? timeout,            /* in: OPTIONAL */
            ref string text,         /* in, out */
            ref Result error         /* out */
            )
        {
            if (encoding == null)
            {
                error = "invalid encoding";
                return ReturnCode.Error;
            }

            string symmetricAlgorithmName;
            string password;
            byte[] salt;
            int iterations;
            string hashAlgorithmName;
            CipherMode cipherMode;
            PaddingMode paddingMode;
            byte[] oldData;

            /* NO RESULT */
            CryptographyOps.InitializeParameters(
                out symmetricAlgorithmName, out password,
                out salt, out iterations, out hashAlgorithmName,
                out cipherMode, out paddingMode, out oldData);

            if (CryptographyOps.ExtractParameters(
                    interpreter, text, encoding, cultureInfo, true,
                    ref symmetricAlgorithmName, ref password,
                    ref salt, ref iterations, ref hashAlgorithmName,
                    ref cipherMode, ref paddingMode, ref oldData,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            Result result; /* REUSED */

            if (password == null)
            {
                result = null;

                if (CryptographyOps.GetPasswordViaAppDomainAndSalt(
                        pluginData, salt, ref password,
                        ref result) != ReturnCode.Ok)
                {
                    error = result;
                    return ReturnCode.Error;
                }
            }

            if (password == null)
            {
                result = null;

                if (CryptographyOps.GetPasswordViaEnvironmentAndSalt(
                        salt, ref password, ref result) != ReturnCode.Ok)
                {
                    error = result;
                    return ReturnCode.Error;
                }
            }

#if NETWORK && WEB
            if ((password == null) && Configuration.DoesVariableExist(
                    Constants.UseRemotePasswordsEnvVarName))
            {
                Uri uri = Utility.GetAssemblyUri(
                    CertificateAssemblyOps.GetObject(),
                    Constants.PasswordUriName);

                if (uri != null)
                {
                    result = null;

                    if (CryptographyOps.GetPasswordViaUriAndSalt(
                            interpreter, pluginData, uri, encoding,
                            salt, timeout, ref password,
                            ref result) != ReturnCode.Ok)
                    {
                        error = result;
                        return ReturnCode.Error;
                    }
                }
            }
#endif

            byte[] newData = null;

            if (CryptographyOps.EncryptOrDecrypt(
                    symmetricAlgorithmName, password, salt,
                    iterations, hashAlgorithmName, cipherMode,
                    paddingMode, oldData, false, ref newData,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            text = Utility.NormalizeLineEndings(
                encoding.GetString(newData));

            return ReturnCode.Ok;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the signature for the specified file and verifies the file
        /// against it, also checking the required key usage.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while reading and verifying the file.
        /// </param>
        /// <param name="clientData">
        /// The context describing how to read and verify the file.
        /// </param>
        /// <param name="fileName">
        /// The file name to read and verify.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the file is verified; otherwise, a
        /// failure code.
        /// </returns>
        public static ReturnCode ReadSignatureAndVerifyFile( /* CORE */
            Interpreter interpreter,       /* in */
            EvaluateClientData clientData, /* in */
            string fileName,               /* in */
            ref Result error               /* out */
            )
        {
            if (clientData == null)
            {
                error = "missing context clientData";
                return ReturnCode.Error;
            }

            byte[] signature = null;

            if (!DataOps.TryReadSignatureFile(
                    interpreter, clientData.Encoding,
                    DataOps.FormatSignatureFileName(
                    fileName), clientData.Timeout,
                    clientData.AllowRemoteUri,
                    ref signature, ref error))
            {
                return ReturnCode.Error;
            }

            string text = null; /* NOT USED */
            byte[] hashValue = null; /* NOT USED */
            IKeyPair keyPair = null;

            if (VerifyFile(
                    interpreter, clientData,
                    fileName, signature,
                    clientData.Timeout,
                    clientData.AllowRemoteUri,
                    true /* NO-XML */,
                    ref text, ref hashValue,
                    ref keyPair,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            string keyUsage = KeyUsage.ReadData;

            if ((keyUsage != null) &&
                (CheckKeyUsage(
                    keyPair, keyUsage, EntityType.File,
                    ref error) != ReturnCode.Ok))
            {
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves the boolean value of the specified annotation, if it is
        /// present.
        /// </summary>
        /// <param name="annotations">
        /// The annotations to search. This value may be null.
        /// </param>
        /// <param name="annotation">
        /// The name of the annotation to retrieve.
        /// </param>
        /// <param name="errorOnNull">
        /// Non-zero to treat a null annotation name as an error.
        /// </param>
        /// <param name="value">
        /// Upon return, the boolean value of the annotation, if present.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public static ReturnCode HasAnnotation( /* CORE */
            StringDictionary annotations, /* in */
            string annotation,            /* in */
            bool errorOnNull,             /* in */
            ref bool? value,              /* out */
            ref Result error              /* out */
            )
        {
            if (annotation == null)
            {
                if (errorOnNull)
                {
                    error = "invalid annotation";
                    return ReturnCode.Error;
                }
                else
                {
                    return ReturnCode.Ok;
                }
            }

            if ((annotations == null) || (annotations.Count == 0))
                return ReturnCode.Ok;

            string valueString;

            if (!annotations.TryGetValue(annotation, out valueString))
                return ReturnCode.Ok;

            if (valueString != null)
            {
                //
                // HACK: The annotation is present and has a value,
                //       which must be a boolean for this method to
                //       consider it valid.
                //
                bool? boolValue = null;

                if (Value.GetNullableBoolean2(
                        valueString, ValueFlags.AnyBoolean, null,
                        ref boolValue, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                value = boolValue;
            }
            else
            {
                //
                // HACK: The annotation is present, but has no value;
                //       therefore, give it a value of true, since it
                //       is present.
                //
                value = true;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves the timestamp value of the specified annotation, if it
        /// is present.
        /// </summary>
        /// <param name="annotations">
        /// The annotations to search. This value may be null.
        /// </param>
        /// <param name="annotation">
        /// The name of the annotation to retrieve.
        /// </param>
        /// <param name="errorOnNull">
        /// Non-zero to treat a null annotation name as an error.
        /// </param>
        /// <param name="value">
        /// Upon return, the timestamp value of the annotation, if present.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, a failure
        /// code.
        /// </returns>
        public static ReturnCode HasAnnotation( /* CORE */
            StringDictionary annotations, /* in */
            string annotation,            /* in */
            bool errorOnNull,             /* in */
            ref DateTime? value,          /* out */
            ref Result error              /* out */
            )
        {
            if (annotation == null)
            {
                if (errorOnNull)
                {
                    error = "invalid annotation";
                    return ReturnCode.Error;
                }
                else
                {
                    return ReturnCode.Ok;
                }
            }

            if ((annotations == null) || (annotations.Count == 0))
                return ReturnCode.Ok;

            string valueString;

            if (!annotations.TryGetValue(annotation, out valueString))
                return ReturnCode.Ok;

            if (valueString == null)
            {
                error = String.Format(
                    "invalid {0} annotation value", annotation);

                return ReturnCode.Error;
            }

            DateTime? dateTimeValue = DataOps.ParseAnnotationTimeStamp(
                valueString);

            if (dateTimeValue == null)
            {
                error = String.Format(
                    "could not parse {0} annotation value", annotation);

                return ReturnCode.Error;
            }

            value = dateTimeValue;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature of the specified script file, returning the
        /// verified text, hash value, and key pair.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while reading the file. This value may be
        /// null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin used while decrypting the file, if necessary. This
        /// value may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to verify the file. This value
        /// may be null.
        /// </param>
        /// <param name="hashKey">
        /// The key used by the keyed hash algorithm, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the file and compute its hash.
        /// </param>
        /// <param name="fileName">
        /// The file name of the script to verify.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the signature.
        /// </param>
        /// <param name="signature">
        /// The signature to verify against the file.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while verifying the file. This value may be null.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data. This
        /// value may be null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the file to be read from a remote location.
        /// </param>
        /// <param name="noXml">
        /// Non-zero to disable XML processing when reading the file.
        /// </param>
        /// <param name="text">
        /// Upon success, receives the verified script text.
        /// </param>
        /// <param name="hashValue">
        /// Upon success, receives the computed hash value of the file.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the file is verified; otherwise, a
        /// failure code.
        /// </returns>
        public static ReturnCode VerifyFile( /* CORE */
            Interpreter interpreter,        /* in: OPTIONAL */
            IPluginData pluginData,         /* in: OPTIONAL */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            Encoding encoding,              /* in */
            string fileName,                /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            byte[] signature,               /* in */
            CultureInfo cultureInfo,        /* in: OPTIONAL */
            int? timeout,                   /* in: OPTIONAL */
            bool allowRemoteUri,            /* in */
            bool noXml,                     /* in */
            ref string text,                /* out */
            ref byte[] hashValue,           /* out */
            ref IKeyPair keyPair,           /* out */
            ref Result error                /* out */
            )
        {
            string type = null; /* NOT USED */
            bool swapCommands = false; /* NOT USED */
            bool disableInterpreterCreation = false; /* NOT USED */

            return VerifyFile(
                interpreter, pluginData, hashAlgorithmName, hashKey,
                encoding, fileName, keyPairs, signature, cultureInfo,
                timeout, allowRemoteUri, noXml, ref text, ref hashValue,
                ref keyPair, ref type, ref swapCommands,
                ref disableInterpreterCreation, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature of the specified script file using the
        /// settings from the supplied context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while reading the file. This value may be
        /// null.
        /// </param>
        /// <param name="clientData">
        /// The context describing how to read and verify the file.
        /// </param>
        /// <param name="fileName">
        /// The file name of the script to verify.
        /// </param>
        /// <param name="signature">
        /// The signature to verify against the file.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data. This
        /// value may be null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the file to be read from a remote location.
        /// </param>
        /// <param name="noXml">
        /// Non-zero to disable XML processing when reading the file.
        /// </param>
        /// <param name="text">
        /// Upon success, receives the verified script text.
        /// </param>
        /// <param name="hashValue">
        /// Upon success, receives the computed hash value of the file.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the file is verified; otherwise, a
        /// failure code.
        /// </returns>
        public static ReturnCode VerifyFile( /* CORE */
            Interpreter interpreter,       /* in: OPTIONAL */
            EvaluateClientData clientData, /* in */
            string fileName,               /* in */
            byte[] signature,              /* in */
            int? timeout,                  /* in: OPTIONAL */
            bool allowRemoteUri,           /* in */
            bool noXml,                    /* in */
            ref string text,               /* out */
            ref byte[] hashValue,          /* out */
            ref IKeyPair keyPair,          /* out */
            ref Result error               /* out */
            )
        {
            if (clientData == null)
            {
                error = "missing context clientData";
                return ReturnCode.Error;
            }

            string type = null; /* NOT USED */
            bool swapCommands = false; /* NOT USED */
            bool disableInterpreterCreation = false; /* NOT USED */

            return VerifyFile(
                interpreter, clientData.Plugin,
                clientData.HashAlgorithmName, clientData.HashKey,
                clientData.Encoding, fileName, clientData.KeyPairs,
                signature, clientData.CultureInfo, timeout,
                allowRemoteUri, noXml, ref text, ref  hashValue,
                ref keyPair, ref type, ref swapCommands,
                ref disableInterpreterCreation, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature of the specified script file using the
        /// settings from the supplied context, also returning the script type
        /// and any swap and interpreter-creation annotations.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while reading the file. This value may be
        /// null.
        /// </param>
        /// <param name="clientData">
        /// The context describing how to read and verify the file.
        /// </param>
        /// <param name="fileName">
        /// The file name of the script to verify.
        /// </param>
        /// <param name="signature">
        /// The signature to verify against the file.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data. This
        /// value may be null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the file to be read from a remote location.
        /// </param>
        /// <param name="noXml">
        /// Non-zero to disable XML processing when reading the file.
        /// </param>
        /// <param name="text">
        /// Upon success, receives the verified script text.
        /// </param>
        /// <param name="hashValue">
        /// Upon success, receives the computed hash value of the file.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the signature.
        /// </param>
        /// <param name="type">
        /// Upon success, receives the script type determined from the key
        /// pair.
        /// </param>
        /// <param name="swapCommands">
        /// On input, the default swap-commands setting; on success, updated
        /// from the script annotations.
        /// </param>
        /// <param name="disableInterpreterCreation">
        /// On input, the default interpreter-creation setting; on success,
        /// updated from the script annotations.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the file is verified; otherwise, a
        /// failure code.
        /// </returns>
        public static ReturnCode VerifyFile( /* CORE */
            Interpreter interpreter,             /* in: OPTIONAL */
            EvaluateClientData clientData,       /* in */
            string fileName,                     /* in */
            byte[] signature,                    /* in */
            int? timeout,                        /* in: OPTIONAL */
            bool allowRemoteUri,                 /* in */
            bool noXml,                          /* in */
            ref string text,                     /* out */
            ref byte[] hashValue,                /* out */
            ref IKeyPair keyPair,                /* out */
            ref string type,                     /* out */
            ref bool swapCommands,               /* in, out */
            ref bool disableInterpreterCreation, /* in, out */
            ref Result error                     /* out */
            )
        {
            if (clientData == null)
            {
                error = "missing context clientData";
                return ReturnCode.Error;
            }

            return VerifyFile(
                interpreter, clientData.Plugin,
                clientData.HashAlgorithmName, clientData.HashKey,
                clientData.Encoding, fileName, clientData.KeyPairs,
                signature, clientData.CultureInfo, timeout,
                allowRemoteUri, noXml, ref text, ref hashValue,
                ref keyPair, ref type, ref swapCommands,
                ref disableInterpreterCreation, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature of the specified script file, reading the
        /// file, computing its hash, validating the signature and any time
        /// and annotation constraints, and decrypting it if necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while reading the file. This value may be
        /// null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin used while decrypting the file, if necessary. This
        /// value may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to verify the file. This value
        /// may be null.
        /// </param>
        /// <param name="hashKey">
        /// The key used by the keyed hash algorithm, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the file and compute its hash.
        /// </param>
        /// <param name="fileName">
        /// The file name of the script to verify.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the signature.
        /// </param>
        /// <param name="signature">
        /// The signature to verify against the file.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while decrypting the file, if necessary. This
        /// value may be null.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data. This
        /// value may be null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the file to be read from a remote location.
        /// </param>
        /// <param name="noXml">
        /// Non-zero to disable XML processing when reading the file.
        /// </param>
        /// <param name="text">
        /// Upon success, receives the verified script text.
        /// </param>
        /// <param name="hashValue">
        /// Upon success, receives the computed hash value of the file.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the signature.
        /// </param>
        /// <param name="type">
        /// Upon success, receives the script type determined from the key
        /// pair.
        /// </param>
        /// <param name="swapCommands">
        /// On input, the default swap-commands setting; on success, updated
        /// from the script annotations.
        /// </param>
        /// <param name="disableInterpreterCreation">
        /// On input, the default interpreter-creation setting; on success,
        /// updated from the script annotations.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the file is verified; otherwise, a
        /// failure code.
        /// </returns>
        private static ReturnCode VerifyFile( /* CORE */
            Interpreter interpreter,             /* in: OPTIONAL */
            IPluginData pluginData,              /* in: OPTIONAL */
            string hashAlgorithmName,            /* in: OPTIONAL */
            byte[] hashKey,                      /* in: OPTIONAL */
            Encoding encoding,                   /* in */
            string fileName,                     /* in */
            IEnumerable<IKeyPair> keyPairs,      /* in */
            byte[] signature,                    /* in */
            CultureInfo cultureInfo,             /* in: OPTIONAL */
            int? timeout,                        /* in: OPTIONAL */
            bool allowRemoteUri,                 /* in */
            bool noXml,                          /* in */
            ref string text,                     /* out */
            ref byte[] hashValue,                /* out */
            ref IKeyPair keyPair,                /* out */
            ref string type,                     /* out */
            ref bool swapCommands,               /* in, out */
            ref bool disableInterpreterCreation, /* in, out */
            ref Result error                     /* out */
            )
        {
            if (encoding == null)
            {
                error = "invalid encoding";
                return ReturnCode.Error;
            }

            EngineFlags engineFlags = Constants.VerifyEngineFlags;

            if (!allowRemoteUri)
                engineFlags |= EngineFlags.NoRemote;

#if XML
            if (noXml)
                engineFlags |= EngineFlags.NoXml;
#endif

            IClientData readScriptClientData = null;
            string localText = null;

            if (Engine.ReadScriptFile(
                    interpreter, fileName, engineFlags,
                    ref readScriptClientData, ref localText,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            string localOriginalText =
                Engine.GetReadScriptFileOriginalText(
                    readScriptClientData);

            if (localOriginalText == null)
            {
                error = "original script file text unavailable";
                return ReturnCode.Error;
            }

            byte[] bytes;

            try
            {
                bytes = encoding.GetBytes(localOriginalText); /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }

            Result verifyError = null;

            if (VerifyBytes(hashAlgorithmName,
                    hashKey, bytes, keyPairs, signature, ref hashValue,
                    ref keyPair, ref verifyError) != ReturnCode.Ok)
            {
                Result localError = String.Format(
                    "could not verify signature for script file {0}",
                    Utility.FormatWrapOrNull(fileName));

                if (verifyError != null)
                    error = new ResultList(localError, verifyError);
                else
                    error = localError;

                return ReturnCode.Error;
            }

            if (CertificateSharedOps.IsEncryptedFileName(fileName))
            {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                if (DataOps.HasEncryptedDataHeader(localText))
                {
                    if (Decrypt(
                            interpreter, pluginData, encoding,
                            cultureInfo, timeout, ref localText,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                }
                else
                {
                    error = "encrypted data header missing";
                    return ReturnCode.Error;
                }
#else
                error = "encrypted files unsupported";
                return ReturnCode.Error;
#endif
            }

            StringDictionary annotations = null;

            if (Value.ExtractAnnotations(localText,
                    ref annotations, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            DateTime? localDateTime; /* REUSED */
            DateTime now = DataOps.GetTimeStamp();

            localDateTime = null;

            if (HasAnnotation(
                    annotations, Annotations.NotBefore,
                    false, ref localDateTime,
                    ref error) == ReturnCode.Ok)
            {
                if ((localDateTime != null) &&
                    (now < (DateTime)localDateTime))
                {
                    error = String.Format(
                        "script file {0} not valid before {1}",
                        Utility.FormatWrapOrNull(fileName),
                        (localDateTime != null) ?
                            DataOps.FormatTimeStamp(
                                (DateTime)localDateTime) :
                            Constants.DisplayNull);

                    return ReturnCode.Error;
                }
            }
            else
            {
                return ReturnCode.Error;
            }

            localDateTime = null;

            if (HasAnnotation(
                    annotations, Annotations.NotAfter,
                    false, ref localDateTime,
                    ref error) == ReturnCode.Ok)
            {
                if ((localDateTime != null) &&
                    (now > (DateTime)localDateTime))
                {
                    error = String.Format(
                        "script file {0} not valid after {1}",
                        Utility.FormatWrapOrNull(fileName),
                        (localDateTime != null) ?
                            DataOps.FormatTimeStamp(
                                (DateTime)localDateTime) :
                            Constants.DisplayNull);

                    return ReturnCode.Error;
                }
            }
            else
            {
                return ReturnCode.Error;
            }

            bool? localSwapCommands = null;

            if (HasAnnotation(annotations,
                    Constants.SwapCommandsAnnotation,
                    false, ref localSwapCommands,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            bool? localDisableInterpreterCreation = null;

            if (HasAnnotation(annotations,
                    Constants.DisableInterpreterCreationAnnotation,
                    false, ref localDisableInterpreterCreation,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            text = localText;
            type = GetScriptTypeFrom(keyPair);

            if (localSwapCommands != null)
                swapCommands = (bool)localSwapCommands;

            if (localDisableInterpreterCreation != null)
            {
                disableInterpreterCreation =
                    (bool)localDisableInterpreterCreation;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature of the script contained in the specified
        /// stream using the settings from the supplied context.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while reading the stream. This value may be
        /// null.
        /// </param>
        /// <param name="clientData">
        /// The context describing how to read and verify the stream.
        /// </param>
        /// <param name="fileName">
        /// The file name associated with the stream, used in messages.
        /// </param>
        /// <param name="stream">
        /// The stream containing the script to verify.
        /// </param>
        /// <param name="signature">
        /// The signature to verify against the stream.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data. This
        /// value may be null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the stream to be read from a remote location.
        /// </param>
        /// <param name="text">
        /// Upon success, receives the verified script text.
        /// </param>
        /// <param name="hashValue">
        /// Upon success, receives the computed hash value of the stream.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the stream is verified; otherwise,
        /// a failure code.
        /// </returns>
        public static ReturnCode VerifyStream( /* CORE */
            Interpreter interpreter,       /* in: OPTIONAL */
            EvaluateClientData clientData, /* in */
            string fileName,               /* in */
            Stream stream,                 /* in */
            byte[] signature,              /* in */
            int? timeout,                  /* in: OPTIONAL */
            bool allowRemoteUri,           /* in */
            ref string text,               /* out */
            ref byte[] hashValue,          /* out */
            ref IKeyPair keyPair,          /* out */
            ref Result error               /* out */
            )
        {
            if (clientData == null)
            {
                error = "missing context clientData";
                return ReturnCode.Error;
            }

            string type = null; /* NOT USED */
            bool swapCommands = false; /* NOT USED */
            bool disableInterpreterCreation = false; /* NOT USED */

            return VerifyStream(
                interpreter, clientData.Plugin,
                clientData.HashAlgorithmName, clientData.HashKey,
                clientData.Encoding, fileName, stream,
                clientData.KeyPairs, signature,
                clientData.CultureInfo, timeout, allowRemoteUri,
                ref text, ref  hashValue, ref keyPair, ref type,
                ref swapCommands, ref disableInterpreterCreation,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the signature of the script contained in the specified
        /// stream, reading the stream, computing its hash, validating the
        /// signature and any time and annotation constraints, and decrypting
        /// it if necessary.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while reading the stream. This value may be
        /// null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin used while decrypting the stream, if necessary. This
        /// value may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to verify the stream. This
        /// value may be null.
        /// </param>
        /// <param name="hashKey">
        /// The key used by the keyed hash algorithm, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read the stream and compute its hash.
        /// </param>
        /// <param name="fileName">
        /// The file name associated with the stream, used in messages. This
        /// value may be null.
        /// </param>
        /// <param name="stream">
        /// The stream containing the script to verify.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the signature.
        /// </param>
        /// <param name="signature">
        /// The signature to verify against the stream.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while decrypting the stream, if necessary. This
        /// value may be null.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data. This
        /// value may be null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the stream to be read from a remote location.
        /// </param>
        /// <param name="text">
        /// Upon success, receives the verified script text.
        /// </param>
        /// <param name="hashValue">
        /// Upon success, receives the computed hash value of the stream.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the signature.
        /// </param>
        /// <param name="type">
        /// Upon success, receives the script type determined from the key
        /// pair.
        /// </param>
        /// <param name="swapCommands">
        /// On input, the default swap-commands setting; on success, updated
        /// from the script annotations.
        /// </param>
        /// <param name="disableInterpreterCreation">
        /// On input, the default interpreter-creation setting; on success,
        /// updated from the script annotations.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the stream is verified; otherwise,
        /// a failure code.
        /// </returns>
        private static ReturnCode VerifyStream( /* CORE */
            Interpreter interpreter,             /* in: OPTIONAL */
            IPluginData pluginData,              /* in: OPTIONAL */
            string hashAlgorithmName,            /* in: OPTIONAL */
            byte[] hashKey,                      /* in: OPTIONAL */
            Encoding encoding,                   /* in */
            string fileName,                     /* in: OPTIONAL */
            Stream stream,                       /* in */
            IEnumerable<IKeyPair> keyPairs,      /* in */
            byte[] signature,                    /* in */
            CultureInfo cultureInfo,             /* in: OPTIONAL */
            int? timeout,                        /* in: OPTIONAL */
            bool allowRemoteUri,                 /* in */
            ref string text,                     /* out */
            ref byte[] hashValue,                /* out */
            ref IKeyPair keyPair,                /* out */
            ref string type,                     /* out */
            ref bool swapCommands,               /* in, out */
            ref bool disableInterpreterCreation, /* in, out */
            ref Result error                     /* out */
            )
        {
            if (encoding == null)
            {
                error = "invalid encoding";
                return ReturnCode.Error;
            }

            if (stream == null)
            {
                error = "invalid stream";
                return ReturnCode.Error;
            }

            string localText = null;
            string localOriginalText = null;

            try
            {
                using (StreamReader streamReader = new StreamReader(
                        stream)) /* throw */
                {
                    EngineFlags engineFlags = Constants.VerifyEngineFlags;

                    if (!allowRemoteUri)
                        engineFlags |= EngineFlags.NoRemote;

                    IClientData readScriptClientData = null;

                    if (Engine.ReadScriptStream(
                            interpreter, fileName, streamReader, 0,
                            Count.Invalid, engineFlags,
                            ref readScriptClientData, ref localText,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    localOriginalText =
                        Engine.GetReadScriptFileOriginalText(
                            readScriptClientData);
                }
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }

            if (localOriginalText == null)
            {
                error = "original script stream text unavailable";
                return ReturnCode.Error;
            }

            byte[] bytes;

            try
            {
                bytes = encoding.GetBytes(localOriginalText); /* throw */
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }

            Result verifyError = null;

            if (VerifyBytes(hashAlgorithmName,
                    hashKey, bytes, keyPairs, signature, ref hashValue,
                    ref keyPair, ref verifyError) != ReturnCode.Ok)
            {
                Result localError = String.Format(
                    "could not verify signature for script stream {0}",
                    Utility.FormatWrapOrNull(fileName));

                if (verifyError != null)
                    error = new ResultList(localError, verifyError);
                else
                    error = localError;

                return ReturnCode.Error;
            }

            if (CertificateSharedOps.IsEncryptedFileName(fileName))
            {
#if XML && CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
                if (DataOps.HasEncryptedDataHeader(localText))
                {
                    if (Decrypt(
                            interpreter, pluginData, encoding,
                            cultureInfo, timeout, ref localText,
                            ref error) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }
                }
                else
                {
                    error = "encrypted data header missing";
                    return ReturnCode.Error;
                }
#else
                error = "encrypted streams unsupported";
                return ReturnCode.Error;
#endif
            }

            StringDictionary annotations = null;

            if (Value.ExtractAnnotations(localText,
                    ref annotations, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            DateTime? localDateTime; /* REUSED */
            DateTime now = DataOps.GetTimeStamp();

            localDateTime = null;

            if (HasAnnotation(
                    annotations, Annotations.NotBefore,
                    false, ref localDateTime,
                    ref error) == ReturnCode.Ok)
            {
                if ((localDateTime != null) &&
                    (now < (DateTime)localDateTime))
                {
                    error = String.Format(
                        "script stream {0} not valid before {1}",
                        Utility.FormatWrapOrNull(fileName),
                        (localDateTime != null) ?
                            DataOps.FormatTimeStamp(
                                (DateTime)localDateTime) :
                            Constants.DisplayNull);

                    return ReturnCode.Error;
                }
            }
            else
            {
                return ReturnCode.Error;
            }

            localDateTime = null;

            if (HasAnnotation(
                    annotations, Annotations.NotAfter,
                    false, ref localDateTime,
                    ref error) == ReturnCode.Ok)
            {
                if ((localDateTime != null) &&
                    (now > (DateTime)localDateTime))
                {
                    error = String.Format(
                        "script stream {0} not valid after {1}",
                        Utility.FormatWrapOrNull(fileName),
                        (localDateTime != null) ?
                            DataOps.FormatTimeStamp(
                                (DateTime)localDateTime) :
                            Constants.DisplayNull);

                    return ReturnCode.Error;
                }
            }
            else
            {
                return ReturnCode.Error;
            }

            bool? localSwapCommands = null;

            if (HasAnnotation(annotations,
                    Constants.SwapCommandsAnnotation,
                    false, ref localSwapCommands,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            bool? localDisableInterpreterCreation = null;

            if (HasAnnotation(annotations,
                    Constants.DisableInterpreterCreationAnnotation,
                    false, ref localDisableInterpreterCreation,
                    ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            text = localText;
            type = GetScriptTypeFrom(keyPair);

            if (localSwapCommands != null)
                swapCommands = (bool)localSwapCommands;

            if (localDisableInterpreterCreation != null)
            {
                disableInterpreterCreation =
                    (bool)localDisableInterpreterCreation;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the hash of the specified bytes and verifies it against
        /// the supplied signature and key pairs.
        /// </summary>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm to use. This value may be null.
        /// </param>
        /// <param name="hashKey">
        /// The key used by the keyed hash algorithm, if any.
        /// </param>
        /// <param name="bytes">
        /// The bytes whose hash is computed and verified.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the signature.
        /// </param>
        /// <param name="signature">
        /// The signature to verify against the computed hash.
        /// </param>
        /// <param name="hashValue">
        /// Upon success, receives the computed hash value.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that verified the signature.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the signature is verified;
        /// otherwise, a failure code.
        /// </returns>
        public static ReturnCode VerifyBytes( /* CORE */
            string hashAlgorithmName,       /* in: OPTIONAL */
            byte[] hashKey,                 /* in: OPTIONAL */
            byte[] bytes,                   /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            byte[] signature,               /* in */
            ref byte[] hashValue,           /* out */
            ref IKeyPair keyPair,           /* out */
            ref Result error                /* out */
            )
        {
            if (bytes == null)
            {
                error = "invalid bytes";
                return ReturnCode.Error;
            }

            byte[] hashBytes = null;

            if (CertificateSharedOps.HashBytes(
                    hashAlgorithmName, hashKey, bytes,
                    ref hashBytes, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            Result localResult = null;

            if (CertificateSharedOps.VerifyHash(
                    hashBytes, hashAlgorithmName, signature, keyPairs,
                    ref keyPair, ref localResult) == ReturnCode.Ok)
            {
                hashValue = hashBytes;
                return ReturnCode.Ok;
            }
            else
            {
                error = localResult;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that the specified key pair is permitted to be used for
        /// the given key usage and entity type.
        /// </summary>
        /// <param name="keyPair">
        /// The key pair whose usage is checked.
        /// </param>
        /// <param name="keyUsage">
        /// The key usage required of the key pair.
        /// </param>
        /// <param name="entityType">
        /// The type of entity being verified, used in trace messages.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> if the key usage is permitted;
        /// otherwise, a failure code.
        /// </returns>
        public static ReturnCode CheckKeyUsage( /* CORE */
            IKeyPair keyPair,      /* in */
            string keyUsage,       /* in */
            EntityType entityType, /* in */
            ref Result error       /* out */
            )
        {
            if (keyPair == null)
            {
                error = "invalid key pair";
                return ReturnCode.Error;
            }

            string keyPairKeyUsage = keyPair.KeyUsage;
            long flagsKey = Utility.DefaultAttributeFlagsKey();
            Result result; /* REUSED */

            result = null;

            if (CertificateSharedOps.MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, null, KeyUsage.LicenseeOnly,
                    false, false, true) != ReturnCode.Ok)
            {
                if (!CertificateSharedOps.MatchKeyIdentifier(
                        CertificateAssemblyOps.GetObject(), keyPair,
                        ref result))
                {
                    result = Utility.MaybeCombineResults(
                        "key pair is for licensee use only", result);

#if DEBUG || FORCE_TRACE
                    CertificateSharedOps.TraceKeyUsageError(
                        keyPair, entityType | EntityType.Trusted,
                        result);
#endif

                    error = result;
                    return ReturnCode.Error;
                }
            }

            result = null;

            if (CertificateSharedOps.MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, null, KeyUsage.DeveloperOnly,
                    false, false, true) != ReturnCode.Ok)
            {
                result = "key pair is for developer use only";

#if DEBUG || FORCE_TRACE
                CertificateSharedOps.TraceKeyUsageError(
                    keyPair, entityType | EntityType.Trusted,
                    result);
#endif

#if !DEBUG
                error = result;
                return ReturnCode.Error;
#endif
            }

            result = null;

            if (CertificateSharedOps.MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, null, KeyUsage.TestOnly,
                    false, false, true) != ReturnCode.Ok)
            {
                if (!CertificateTestMode.IsEnabled()
#if CERTIFICATE_PLUGIN && !LIMITED_EDITION
                        || !CertificateGlobalState.IsEnableTestModeOrAll()
#endif
                    )
                {
                    result = "key pair is for test use only";

#if DEBUG || FORCE_TRACE
                    CertificateSharedOps.TraceKeyUsageError(
                        keyPair, entityType | EntityType.Trusted,
                        result);
#endif

                    error = result;
                    return ReturnCode.Error;
                }
            }

            result = null;

            if (CertificateSharedOps.MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, null, KeyUsage.KeyRingOnly,
                    false, false, true) != ReturnCode.Ok)
            {
                result = "key pair is for key ring use only";

#if DEBUG || FORCE_TRACE
                CertificateSharedOps.TraceKeyUsageError(
                    keyPair, entityType | EntityType.Trusted,
                    result);
#endif

                error = result;
                return ReturnCode.Error;
            }

            result = null;

            if (CertificateSharedOps.MatchFlags(
                    keyPairKeyUsage, FlagType.KeyUsage,
                    flagsKey, keyUsage, null, true, false,
                    true, ref result) != ReturnCode.Ok)
            {
#if DEBUG || FORCE_TRACE
                CertificateSharedOps.TraceKeyUsageError(
                    keyPair, entityType | EntityType.Trusted,
                    result);
#endif

                error = result;
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies and evaluates a signed script file described by the
        /// specified context, managing command swapping, context variables,
        /// and references as configured.
        /// </summary>
        /// <param name="clientData">
        /// The context describing the script file to verify and evaluate.
        /// </param>
        /// <param name="result">
        /// On return, the result of evaluating the script, or information
        /// about any error that occurred.
        /// </param>
        /// <returns>
        /// The return code produced by evaluating the script.
        /// </returns>
        public static ReturnCode EvaluateFile( /* CORE */
            EvaluateClientData clientData, /* in */
            ref Result result              /* out */
            )
        {
            if (clientData == null)
            {
                result = "invalid file clientData";
                return ReturnCode.Error;
            }

            Interpreter interpreter = clientData.Interpreter;

            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (!Utility.HaveEagleThreading(interpreter) &&
                !interpreter.IsPrimaryThread())
            {
                result = "interpreter does not support threading";
                return ReturnCode.Error;
            }

            if (clientData.KeyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            Result localResult; /* REUSED */
            string text = null;
            byte[] hashValue = null;
            IKeyPair keyPair = null;
            string type; /* REUSED */
            bool swapCommands = clientData.SwapCommands;
            bool disableInterpreterCreation = false;

            type = Constants.ScriptTypeUnsigned;
            localResult = null;

            if (VerifyFile(
                    interpreter, clientData,
                    clientData.FileName,
                    clientData.Signature,
                    clientData.Timeout,
                    clientData.AllowRemoteUri,
                    false, ref text,
                    ref hashValue, ref keyPair,
                    ref type, ref swapCommands,
                    ref disableInterpreterCreation,
                    ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            if (hashValue == null)
            {
                result = String.Format(
                    "verified hash value for file {0} missing",
                    Utility.FormatWrapOrNull(clientData.FileName));

                return ReturnCode.Error;
            }

            clientData.HashValue = hashValue;

            EvaluateClientData.ForNewTypeAndSubType(
                clientData, type, Constants.ScriptSubTypeFile);

            IKeyPair savedKeyPair = clientData.KeyPair;

            try
            {
                clientData.KeyPair = keyPair;
                clientData.MaybeForceDefaultKeyUsage();

                if ((clientData.KeyUsage != null) && (CheckKeyUsage(
                        keyPair, clientData.KeyUsage, EntityType.File,
                        ref result) != ReturnCode.Ok))
                {
                    return ReturnCode.Error;
                }

                try
                {
                    bool topLevel = false;

                    if (clientData.AddReference() == 1)
                        topLevel = true;

                    ObjectDictionary variables = null;

                    if (clientData.UseContext)
                    {
                        localResult = null;

                        if (ScriptContext.RefreshVariables(
                                interpreter, clientData.Plugin,
                                clientData.PluginType,
                                clientData.ContextName,
                                clientData.VariantName,
                                clientData.Type,
                                clientData.SubType,
                                clientData.HashValue,
                                clientData.FileName,
                                clientData.KeyPairs,
                                clientData.KeyPair,
                                clientData.CultureInfo,
                                clientData.ConfigurationPhase,
                                clientData.NoGlobalOnly,
                                clientData.AllowLocalPolicy,
                                true, !topLevel, ref variables,
                                ref localResult) != ReturnCode.Ok)
                        {
                            result = localResult;
                            return ReturnCode.Error;
                        }
                    }

                    ILogClientData logClientData;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                    if (Configuration.DoesVariableExist(
                            Constants.ForceLogScriptEnvVarName))
                    {
                        logClientData = clientData;
                    }
                    else
#endif
                    {
                        logClientData = null;
                    }

                    ReturnCode code;
                    bool extractAndApply = clientData.ExtractAndApply;

                    try
                    {
                        StringList savedCommands = null;
                        long[] tokens = null;

                        try
                        {
                            if (disableInterpreterCreation)
                            {
                                try
                                {
                                    DisableFlags disableFlags =
                                        DisableFlags.FailSafe;

                                    /* NO RESULT */
                                    Utility.EnableStubAssembly(
                                        disableFlags); /* throw */

                                    /* IGNORED */
                                    Utility.DisableInterpreterCreation(
                                        disableFlags); /* throw */
                                }
                                catch (Exception e)
                                {
                                    result = e;
                                    return ReturnCode.Error;
                                }
                            }

                            if (topLevel && clientData.RemoveCommands)
                            {
                                localResult = null;

                                if (Helpers.RemoveAllCommands(
                                        interpreter, clientData,
                                        ref localResult) != ReturnCode.Ok)
                                {
                                    extractAndApply = false;

                                    result = localResult;
                                    return ReturnCode.Error;
                                }
                            }

                            if (topLevel && swapCommands)
                            {
                                StringList localCommands = new StringList();

                                localResult = null;

                                if (interpreter.SwapCommands(
                                        SwapFlags.Default, ref localCommands,
                                        ref localResult) == ReturnCode.Ok)
                                {
                                    savedCommands = localCommands;

#if DEBUG || FORCE_TRACE
                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                        logClientData, String.Format(
                                            "EvaluateFile: appDomain = {0}, " +
                                            "interpreter = {1}, swapCommands = {2}",
                                            Utility.GetCurrentAppDomainId(),
                                            DataOps.FormatInterpreter(
                                                interpreter, true, false),
                                            savedCommands),
                                        typeof(CertificateScriptOps).Name,
                                        TracePriority.Low, 0);
#endif
                                }
                                else
                                {
                                    extractAndApply = false;

                                    result = localResult;
                                    return ReturnCode.Error;
                                }
                            }

                            if (topLevel && clientData.WithCommands)
                            {
#if TEST && DEBUG
                                if (CertificateTestMode.IsEnabled())
                                {
                                    localResult = null;

                                    if (Helpers.AddAllCommandsViaReflection(
                                            interpreter, clientData.Plugin,
                                            clientData, ref tokens,
                                            ref localResult) != ReturnCode.Ok)
                                    {
                                        extractAndApply = false;

                                        result = localResult;
                                        return ReturnCode.Error;
                                    }
                                }
                                else
#endif
                                {
                                    localResult = null;

                                    if (Helpers.AddAllCommandsViaBuiltIns(
                                            interpreter, clientData.Plugin,
                                            clientData, ref tokens,
                                            ref localResult) != ReturnCode.Ok)
                                    {
                                        extractAndApply = false;

                                        result = localResult;
                                        return ReturnCode.Error;
                                    }
                                }

                                /* IGNORED */
                                clientData.AddCommandTokens(tokens);
                            }

#if TEST
                            try
                            {
#endif
                                try
                                {
                                    if (clientData.Untrusted)
                                    {
                                        localResult = null;

                                        code = interpreter.EvaluateScript(
                                            clientData.FileName, text,
                                            ref localResult);
                                    }
                                    else
                                    {
                                        localResult = null;

                                        code = interpreter.EvaluateTrustedScript(
                                            clientData.FileName, text,
                                            clientData.TrustFlags,
                                            ref localResult);
                                    }

                                    /* REFRESH */
                                    extractAndApply = clientData.ExtractAndApply;

                                    if (extractAndApply && (code != ReturnCode.Ok))
                                        extractAndApply = false;
                                }
                                finally
                                {
                                    /* NO RESULT */
                                    clientData.TakeRegisteredVariables(
                                        ref variables);
                                }
#if TEST
                            }
                            finally
                            {
                                /* NO RESULT */
                                clientData.RemoveObjects(interpreter);
                            }
#endif
                        }
                        finally
                        {
                            ReturnCode removeSwapCode;
                            Result removeSwapError = null;

                            removeSwapCode = clientData.MaybeRemoveSwapCommand(
                                interpreter, clientData, SwapFlags.Default,
                                ref removeSwapError);

                            if (removeSwapCode != ReturnCode.Ok)
                            {
                                Utility.Complain(
                                    interpreter, removeSwapCode,
                                    removeSwapError);
                            }

                            if (tokens != null)
                            {
                                ReturnCode removeCode;
                                Result removeResult = null;

                                removeCode = Helpers.RemoveTokens(
                                    interpreter, clientData.Plugin,
                                    null, ref tokens, ref removeResult);

                                if (removeCode == ReturnCode.Ok)
                                {
                                    /* IGNORED */
                                    clientData.RemoveCommandTokens(tokens);
                                }
                                else
                                {
                                    Utility.Complain(
                                        interpreter, removeCode,
                                        removeResult);
                                }
                            }

                            if (savedCommands != null)
                            {
                                ReturnCode swapCode;
                                Result swapError = null;

                                savedCommands = new StringList();

                                swapCode = interpreter.SwapCommands(
                                    SwapFlags.Default, ref savedCommands,
                                    ref swapError);

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                        "EvaluateFile: appDomain = {0}, " +
                                        "interpreter = {1}, savedCommands = {2}, " +
                                        "swapCode = {3}, swapError = {4}",
                                    Utility.GetCurrentAppDomainId(),
                                    DataOps.FormatInterpreter(
                                        interpreter, true, false),
                                    savedCommands, swapCode,
                                    Utility.FormatWrapOrNull(
                                        true, true, swapError)),
                                    typeof(CertificateScriptOps).Name,
                                    TracePriority.Low, 0);
#endif
                            }
                        }
                    }
                    finally
                    {
                        if (variables != null)
                        {
                            if (topLevel && extractAndApply) /* COMMIT? */
                            {
                                ReturnCode applyCode;
                                int applyCount = 0;
                                Result applyError = null;

                                applyCode =
                                    ScriptContext.ExtractAndApplyVariables(
                                        interpreter, clientData.Plugin,
                                        clientData, clientData.CultureInfo,
                                        clientData.NoGlobalOnly,
                                        clientData.AllowLocalPolicy, false,
                                        true, ref applyCount, ref applyError);

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                        "EvaluateFile: appDomain = {0}, " +
                                        "interpreter = {1}, clientData = {2}, " +
                                        "plugin = {3}, applyCode = {4}, " +
                                        "applyCount = {5}, applyError = {6}",
                                        Utility.GetCurrentAppDomainId(),
                                        DataOps.FormatInterpreter(
                                            interpreter, true, false),
                                        DataOps.FormatHexadecimal(
                                            RuntimeHelpers.GetHashCode(
                                                clientData)),
                                        Utility.FormatWrapOrNull(
                                            clientData.Plugin),
                                        applyCode, applyCount,
                                        Utility.FormatWrapOrNull(applyError)),
                                    typeof(CertificateScriptOps).Name,
                                    (applyCount > 0) ?
                                        TracePriority.Medium :
                                        TracePriority.MediumLow, 0);
#endif
                            }

                            ReturnCode unsetCode;
                            Result unsetError = null; /* REUSED */

                            unsetCode = ScriptContext.UnsetVariables(
                                interpreter, variables, ref unsetError);

                            if (unsetCode != ReturnCode.Ok)
                            {
                                Utility.Complain(
                                    interpreter, unsetCode, unsetError);
                            }
                        }
                    }

                    if (Configuration.DoesVariableExist(
                            Constants.ConfigurationTraceCommandsEnvVarName))
                    {
                        /* IGNORED */
                        clientData.AppendToFile(String.Format(
                            "EvaluateFile {0} code {1} result {2}",
                            DataOps.FormatTimeStamp(Utility.GetUtcNow()),
                            code, Utility.FormatWrapOrNull(
                                true, false, localResult)));
                    }

                    //
                    // HACK: Allow the [returnBackNow] command
                    //       to be used to quickly exit out of
                    //       a configuration script file.
                    //
                    if (code == ReturnCode.Return)
                        code = ReturnCode.Ok;

                    result = localResult;
                    return code;
                }
                finally
                {
                    /* IGNORED */
                    clientData.RemoveReference();
                }
            }
            finally
            {
                clientData.KeyPair = savedKeyPair;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies and evaluates a signed script contained in a stream
        /// described by the specified context, managing command swapping,
        /// context variables, and references as configured.
        /// </summary>
        /// <param name="clientData">
        /// The context describing the script stream to verify and evaluate.
        /// </param>
        /// <param name="result">
        /// On return, the result of evaluating the script, or information
        /// about any error that occurred.
        /// </param>
        /// <returns>
        /// The return code produced by evaluating the script.
        /// </returns>
        public static ReturnCode EvaluateStream( /* CORE */
            EvaluateClientData clientData, /* in */
            ref Result result              /* out */
            )
        {
            if (clientData == null)
            {
                result = "invalid stream clientData";
                return ReturnCode.Error;
            }

            Interpreter interpreter = clientData.Interpreter;

            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (!Utility.HaveEagleThreading(interpreter) &&
                !interpreter.IsPrimaryThread())
            {
                result = "interpreter does not support threading";
                return ReturnCode.Error;
            }

            if (clientData.KeyPairs == null)
            {
                result = "invalid key pair list";
                return ReturnCode.Error;
            }

            Result localResult; /* REUSED */
            string text = null;
            byte[] hashValue = null;
            IKeyPair keyPair = null;
            string type; /* REUSED */
            bool swapCommands = clientData.SwapCommands;
            bool disableInterpreterCreation = false;

            type = Constants.ScriptTypeUnsigned;
            localResult = null;

            if (VerifyStream(
                    interpreter, clientData.Plugin,
                    clientData.HashAlgorithmName,
                    clientData.HashKey,
                    clientData.Encoding,
                    clientData.FileName,
                    clientData.Stream,
                    clientData.KeyPairs,
                    clientData.Signature,
                    clientData.CultureInfo,
                    clientData.Timeout,
                    clientData.AllowRemoteUri,
                    ref text, ref hashValue,
                    ref keyPair, ref type,
                    ref swapCommands,
                    ref disableInterpreterCreation,
                    ref localResult) != ReturnCode.Ok)
            {
                result = localResult;
                return ReturnCode.Error;
            }

            if (hashValue == null)
            {
                result = String.Format(
                    "verified hash value for stream {0} missing",
                    Utility.FormatWrapOrNull(clientData.FileName));

                return ReturnCode.Error;
            }

            clientData.HashValue = hashValue;

            EvaluateClientData.ForNewTypeAndSubType(
                clientData, type, Constants.ScriptSubTypeResource);

            IKeyPair savedKeyPair = clientData.KeyPair;

            try
            {
                clientData.KeyPair = keyPair;
                clientData.MaybeForceDefaultKeyUsage();

                if ((clientData.KeyUsage != null) && (CheckKeyUsage(
                        keyPair, clientData.KeyUsage, EntityType.Stream,
                        ref result) != ReturnCode.Ok))
                {
                    return ReturnCode.Error;
                }

                try
                {
                    bool topLevel = false;

                    if (clientData.AddReference() == 1)
                        topLevel = true;

                    ObjectDictionary variables = null;

                    if (clientData.UseContext)
                    {
                        localResult = null;

                        if (ScriptContext.RefreshVariables(
                                interpreter, clientData.Plugin,
                                clientData.PluginType,
                                clientData.ContextName,
                                clientData.VariantName,
                                clientData.Type,
                                clientData.SubType,
                                clientData.HashValue,
                                clientData.FileName,
                                clientData.KeyPairs,
                                clientData.KeyPair,
                                clientData.CultureInfo,
                                clientData.ConfigurationPhase,
                                clientData.NoGlobalOnly,
                                clientData.AllowLocalPolicy,
                                true, !topLevel, ref variables,
                                ref localResult) != ReturnCode.Ok)
                        {
                            result = localResult;
                            return ReturnCode.Error;
                        }
                    }

                    ILogClientData logClientData;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                    if (Configuration.DoesVariableExist(
                            Constants.ForceLogScriptEnvVarName))
                    {
                        logClientData = clientData;
                    }
                    else
#endif
                    {
                        logClientData = null;
                    }

                    ReturnCode code;
                    bool extractAndApply = clientData.ExtractAndApply;

                    try
                    {
                        StringList savedCommands = null;
                        long[] tokens = null;

                        try
                        {
                            if (disableInterpreterCreation)
                            {
                                try
                                {
                                    DisableFlags disableFlags =
                                        DisableFlags.FailSafe;

                                    /* NO RESULT */
                                    Utility.EnableStubAssembly(
                                        disableFlags); /* throw */

                                    /* IGNORED */
                                    Utility.DisableInterpreterCreation(
                                        disableFlags); /* throw */
                                }
                                catch (Exception e)
                                {
                                    result = e;
                                    return ReturnCode.Error;
                                }
                            }

                            if (topLevel && clientData.RemoveCommands)
                            {
                                localResult = null;

                                if (Helpers.RemoveAllCommands(
                                        interpreter, clientData,
                                        ref localResult) != ReturnCode.Ok)
                                {
                                    extractAndApply = false;

                                    result = localResult;
                                    return ReturnCode.Error;
                                }
                            }

                            if (topLevel && swapCommands)
                            {
                                StringList localCommands = new StringList();

                                localResult = null;

                                if (interpreter.SwapCommands(
                                        SwapFlags.Default, ref localCommands,
                                        ref localResult) == ReturnCode.Ok)
                                {
                                    savedCommands = localCommands;

#if DEBUG || FORCE_TRACE
                                    CertificateTraceOps.MaybeLogAndDebugTrace(
                                        logClientData, String.Format(
                                            "EvaluateStream: appDomain = {0}, " +
                                            "interpreter = {1}, savedCommands = {2}",
                                            Utility.GetCurrentAppDomainId(),
                                            DataOps.FormatInterpreter(
                                                interpreter, true, false),
                                            savedCommands),
                                        typeof(CertificateScriptOps).Name,
                                        TracePriority.Low, 0);
#endif
                                }
                                else
                                {
                                    extractAndApply = false;

                                    result = localResult;
                                    return ReturnCode.Error;
                                }
                            }

                            if (topLevel && clientData.WithCommands)
                            {
#if TEST && DEBUG
                                if (CertificateTestMode.IsEnabled())
                                {
                                    localResult = null;

                                    if (Helpers.AddAllCommandsViaReflection(
                                            interpreter, clientData.Plugin,
                                            clientData, ref tokens,
                                            ref localResult) != ReturnCode.Ok)
                                    {
                                        extractAndApply = false;

                                        result = localResult;
                                        return ReturnCode.Error;
                                    }
                                }
                                else
#endif
                                {
                                    localResult = null;

                                    if (Helpers.AddAllCommandsViaBuiltIns(
                                            interpreter, clientData.Plugin,
                                            clientData, ref tokens,
                                            ref localResult) != ReturnCode.Ok)
                                    {
                                        extractAndApply = false;

                                        result = localResult;
                                        return ReturnCode.Error;
                                    }
                                }

                                /* IGNORED */
                                clientData.AddCommandTokens(tokens);
                            }

#if TEST
                            try
                            {
#endif
                                try
                                {
                                    if (clientData.Untrusted)
                                    {
                                        localResult = null;

                                        code = interpreter.EvaluateScript(
                                            clientData.FileName, text,
                                            ref localResult);
                                    }
                                    else
                                    {
                                        localResult = null;

                                        code = interpreter.EvaluateTrustedScript(
                                            clientData.FileName, text,
                                            clientData.TrustFlags,
                                            ref localResult);
                                    }

                                    /* REFRESH */
                                    extractAndApply = clientData.ExtractAndApply;

                                    if (extractAndApply && (code != ReturnCode.Ok))
                                        extractAndApply = false;
                                }
                                finally
                                {
                                    /* NO RESULT */
                                    clientData.TakeRegisteredVariables(
                                        ref variables);
                                }
#if TEST
                            }
                            finally
                            {
                                /* NO RESULT */
                                clientData.RemoveObjects(interpreter);
                            }
#endif
                        }
                        finally
                        {
                            ReturnCode removeSwapCode;
                            Result removeSwapError = null;

                            removeSwapCode = clientData.MaybeRemoveSwapCommand(
                                interpreter, clientData, SwapFlags.Default,
                                ref removeSwapError);

                            if (removeSwapCode != ReturnCode.Ok)
                            {
                                Utility.Complain(
                                    interpreter, removeSwapCode,
                                    removeSwapError);
                            }

                            if (tokens != null)
                            {
                                ReturnCode removeCode;
                                Result removeResult = null;

                                removeCode = Helpers.RemoveTokens(
                                    interpreter, clientData.Plugin,
                                    null, ref tokens, ref removeResult);

                                if (removeCode == ReturnCode.Ok)
                                {
                                    /* IGNORED */
                                    clientData.RemoveCommandTokens(tokens);
                                }
                                else
                                {
                                    Utility.Complain(
                                        interpreter, removeCode,
                                        removeResult);
                                }
                            }

                            if (savedCommands != null)
                            {
                                ReturnCode swapCode;
                                Result swapError = null;

                                savedCommands = new StringList();

                                swapCode = interpreter.SwapCommands(
                                    SwapFlags.Default, ref savedCommands,
                                    ref swapError);

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                        "EvaluateStream: appDomain = {0}, " +
                                        "interpreter = {1}, swapCommands = {2}, " +
                                        "swapCode = {3}, swapError = {4}",
                                        Utility.GetCurrentAppDomainId(),
                                        DataOps.FormatInterpreter(
                                            interpreter, true, false),
                                        savedCommands, swapCode,
                                        Utility.FormatWrapOrNull(
                                            true, true, swapError)),
                                    typeof(CertificateScriptOps).Name,
                                    TracePriority.Low, 0);
#endif
                            }
                        }
                    }
                    finally
                    {
                        if (variables != null)
                        {
                            if (topLevel && extractAndApply) /* COMMIT? */
                            {
                                ReturnCode applyCode;
                                int applyCount = 0;
                                Result applyError = null;

                                applyCode =
                                    ScriptContext.ExtractAndApplyVariables(
                                        interpreter, clientData.Plugin,
                                        clientData, clientData.CultureInfo,
                                        clientData.NoGlobalOnly,
                                        clientData.AllowLocalPolicy, false,
                                        true, ref applyCount, ref applyError);

#if DEBUG || FORCE_TRACE
                                CertificateTraceOps.MaybeLogAndDebugTrace(
                                    logClientData, String.Format(
                                        "EvaluateStream: appDomain = {0}, " +
                                        "interpreter = {1}, clientData = {2}, " +
                                        "plugin = {3}, applyCode = {4}, " +
                                        "applyCount = {5}, applyError = {6}",
                                        Utility.GetCurrentAppDomainId(),
                                        DataOps.FormatInterpreter(
                                            interpreter, true, false),
                                        DataOps.FormatHexadecimal(
                                            RuntimeHelpers.GetHashCode(
                                                clientData)),
                                        Utility.FormatWrapOrNull(
                                            clientData.Plugin),
                                        applyCode, applyCount,
                                        Utility.FormatWrapOrNull(applyError)),
                                    typeof(CertificateScriptOps).Name,
                                    (applyCount > 0) ?
                                        TracePriority.Medium :
                                        TracePriority.MediumLow, 0);
#endif
                            }

                            ReturnCode unsetCode;
                            Result unsetError = null; /* REUSED */

                            unsetCode = ScriptContext.UnsetVariables(
                                interpreter, variables, ref unsetError);

                            if (unsetCode != ReturnCode.Ok)
                            {
                                Utility.Complain(
                                    interpreter, unsetCode, unsetError);
                            }
                        }
                    }

                    if (Configuration.DoesVariableExist(
                            Constants.ConfigurationTraceCommandsEnvVarName))
                    {
                        /* IGNORED */
                        clientData.AppendToFile(String.Format(
                            "EvaluateStream {0} code {1} result {2}",
                            DataOps.FormatTimeStamp(Utility.GetUtcNow()),
                            code, Utility.FormatWrapOrNull(
                                true, false, localResult)));
                    }

                    //
                    // HACK: Allow the [returnBackNow] command
                    //       to be used to quickly exit out of
                    //       a configuration script file.
                    //
                    if (code == ReturnCode.Return)
                        code = ReturnCode.Ok;

                    result = localResult;
                    return code;
                }
                finally
                {
                    /* IGNORED */
                    clientData.RemoveReference();
                }
            }
            finally
            {
                clientData.KeyPair = savedKeyPair;
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if TEST
        /// <summary>
        /// Adds the reference count of the source client data to the target
        /// client data, when both are evaluation contexts.
        /// </summary>
        /// <param name="sourceClientData">
        /// The client data whose reference count is added.
        /// </param>
        /// <param name="targetClientData">
        /// The client data to which the reference count is added.
        /// </param>
        private static void AddToClientDataReferenceCount( /* CORE */
            IClientData sourceClientData, /* in */
            IClientData targetClientData  /* in */

            )
        {
            EvaluateClientData evaluateClientData =
                targetClientData as EvaluateClientData;

            if (evaluateClientData != null)
            {
                evaluateClientData.AddToReferenceCount(
                    sourceClientData as EvaluateClientData);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the evaluation context client data currently associated with
        /// the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose context client data is retrieved.
        /// </param>
        /// <returns>
        /// The associated evaluation context, or null if none is set.
        /// </returns>
        public static EvaluateClientData GetClientData( /* CORE */
            Interpreter interpreter /* in */
            )
        {
            return _Test.TestGetContextClientData(
                interpreter) as EvaluateClientData;
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Creates a copy of the evaluation context client data currently
        /// associated with the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose context client data is cloned.
        /// </param>
        /// <returns>
        /// A new evaluation context copied from the current one.
        /// </returns>
        public static EvaluateClientData CloneClientData( /* CORE */
            Interpreter interpreter /* in */
            )
        {
            EvaluateClientData oldClientData = GetClientData(interpreter);

            EvaluateClientData newClientData = new EvaluateClientData(
                oldClientData);

            /* IGNORED */
            newClientData.AttachTo(oldClientData);

            return newClientData;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Installs the specified client data as the interpreter's context
        /// client data, saving the previous one for later restoration.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose context client data is set.
        /// </param>
        /// <param name="clientData">
        /// The client data to install.
        /// </param>
        /// <param name="savedClientData">
        /// Upon return, the previously installed client data, to be restored
        /// by <see cref="EndClientData" />.
        /// </param>
        public static void BeginClientData( /* CORE */
            Interpreter interpreter,        /* in */
            IClientData clientData,         /* in */
            ref IClientData savedClientData /* out */
            )
        {
            savedClientData = _Test.TestGetContextClientData(
                interpreter);

            _Test.TestSetContextClientData(
                interpreter, clientData);

            AddToClientDataReferenceCount(
                savedClientData, clientData);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the previously saved context client data on the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose context client data is restored.
        /// </param>
        /// <param name="savedClientData">
        /// The previously saved client data to restore. This value is reset
        /// to null on return.
        /// </param>
        public static void EndClientData( /* CORE */
            Interpreter interpreter,        /* in */
            ref IClientData savedClientData /* in, out */
            )
        {
            _Test.TestSetContextClientData(
                interpreter, savedClientData);

            savedClientData = null;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Temporarily clears the interpreter creation, use, and free
        /// callbacks, saving their previous values for later restoration.
        /// </summary>
        /// <param name="savedNewInterpreterCallback">
        /// Upon return, the previous new-interpreter callback.
        /// </param>
        /// <param name="savedUseInterpreterCallback">
        /// Upon return, the previous use-interpreter callback.
        /// </param>
        /// <param name="savedFreeInterpreterCallback">
        /// Upon return, the previous free-interpreter callback.
        /// </param>
        private static void BeginNoInterpreterCallbacks( /* CORE */
            out EventCallback savedNewInterpreterCallback, /* out */
            out EventCallback savedUseInterpreterCallback, /* out */
            out EventCallback savedFreeInterpreterCallback /* out */
            )
        {
            savedNewInterpreterCallback = Interpreter.NewInterpreterCallback;
            savedUseInterpreterCallback = Interpreter.UseInterpreterCallback;
            savedFreeInterpreterCallback = Interpreter.FreeInterpreterCallback;

            Interpreter.NewInterpreterCallback = null;
            Interpreter.UseInterpreterCallback = null;
            Interpreter.FreeInterpreterCallback = null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the interpreter creation, use, and free callbacks
        /// previously saved by <see cref="BeginNoInterpreterCallbacks" />.
        /// </summary>
        /// <param name="savedNewInterpreterCallback">
        /// The previously saved new-interpreter callback to restore. This
        /// value is reset to null on return.
        /// </param>
        /// <param name="savedUseInterpreterCallback">
        /// The previously saved use-interpreter callback to restore. This
        /// value is reset to null on return.
        /// </param>
        /// <param name="savedFreeInterpreterCallback">
        /// The previously saved free-interpreter callback to restore. This
        /// value is reset to null on return.
        /// </param>
        private static void EndNoInterpreterCallbacks( /* CORE */
            ref EventCallback savedNewInterpreterCallback, /* in, out */
            ref EventCallback savedUseInterpreterCallback, /* in, out */
            ref EventCallback savedFreeInterpreterCallback /* in, out */
            )
        {
            Interpreter.NewInterpreterCallback = savedNewInterpreterCallback;
            Interpreter.UseInterpreterCallback = savedUseInterpreterCallback;
            Interpreter.FreeInterpreterCallback = savedFreeInterpreterCallback;

            savedNewInterpreterCallback = null;
            savedUseInterpreterCallback = null;
            savedFreeInterpreterCallback = null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new interpreter under the static interpreter lock, with
        /// the interpreter creation callbacks suppressed.
        /// </summary>
        /// <param name="token">
        /// The token to associate with the new interpreter. This value may be
        /// null.
        /// </param>
        /// <param name="interpreterSettings">
        /// The settings used to create the interpreter, or null to use the
        /// defaults.
        /// </param>
        /// <param name="created">
        /// Upon return, non-zero if a brand new interpreter was created.
        /// </param>
        /// <param name="result">
        /// On success, receives the created interpreter reference; upon
        /// failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The created interpreter, or null if an error occurs.
        /// </returns>
        private static Interpreter CreateInterpreter( /* CORE */
            ulong? token,                             /* in: OPTIONAL */
            IInterpreterSettings interpreterSettings, /* in: OPTIONAL */
            ref bool created,                         /* out */
            ref Result result                         /* out */
            )
        {
            bool locked = false;

            try
            {
                Interpreter.TryStaticLock(
                    Constants.InterpreterCreateLockTimeout,
                    ref locked); /* TRANSACTIONAL */

                if (locked)
                {
                    EventCallback savedNewInterpreterCallback;
                    EventCallback savedUseInterpreterCallback;
                    EventCallback savedFreeInterpreterCallback;

                    BeginNoInterpreterCallbacks(
                        out savedNewInterpreterCallback,
                        out savedUseInterpreterCallback,
                        out savedFreeInterpreterCallback);

                    try
                    {
                        //
                        // NOTE: *SECURITY* It is possible that
                        //       (all) interpreter creation is
                        //       disabled at this point, e.g.
                        //       via the license SDK, etc.  In
                        //       that case, this will fail.
                        //
                        Interpreter interpreter;

                        if (interpreterSettings != null)
                        {
                            interpreter = Interpreter.Create(
                                token, interpreterSettings, true,
                                ref result);
                        }
                        else
                        {
                            interpreter = Interpreter.Create(
                                token, ref result);
                        }

                        if ((interpreter != null) &&
                            (interpreter.CreateCount == 1))
                        {
                            created = true;

                            interpreter.EnableCaches(
                                Constants.DefaultCacheFlags, false);
                        }

                        return interpreter;
                    }
                    finally
                    {
                        EndNoInterpreterCallbacks(
                            ref savedNewInterpreterCallback,
                            ref savedUseInterpreterCallback,
                            ref savedFreeInterpreterCallback);
                    }
                }
                else
                {
                    result = "could not lock interpreters";
                    return null;
                }
            }
            finally
            {
                Interpreter.ExitStaticLock(
                    ref locked); /* TRANSACTIONAL */
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new interpreter, optionally loading and applying
        /// verified interpreter settings obtained via the supplied settings
        /// callback.
        /// </summary>
        /// <param name="token">
        /// The token to associate with the new interpreter. This value may be
        /// null.
        /// </param>
        /// <param name="settingsCallback">
        /// The callback used to obtain the interpreter settings file name, or
        /// null to create the interpreter with default settings.
        /// </param>
        /// <param name="ruleSet">
        /// The rule set applied to the loaded interpreter settings. This
        /// value may be null.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter used while reading and verifying the settings
        /// file. This value may be null.
        /// </param>
        /// <param name="pluginData">
        /// The plugin used while obtaining and verifying the settings file.
        /// This value may be null.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The name of the hash algorithm used to verify the settings file.
        /// This value may be null.
        /// </param>
        /// <param name="hashKey">
        /// The key used by the keyed hash algorithm, if any.
        /// </param>
        /// <param name="encoding">
        /// The encoding used to read and verify the settings file.
        /// </param>
        /// <param name="keyPairs">
        /// The key pairs used to verify the settings file signature.
        /// </param>
        /// <param name="keyUsage">
        /// The key usage required of the verifying key pair. This value may
        /// be null.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used while verifying and loading the settings. This
        /// value may be null.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, used when reading remote data. This
        /// value may be null.
        /// </param>
        /// <param name="allowRemoteUri">
        /// Non-zero to allow the settings and signature to be read from
        /// remote locations.
        /// </param>
        /// <param name="created">
        /// Upon return, non-zero if a brand new interpreter was created.
        /// </param>
        /// <param name="result">
        /// On success, receives the created interpreter reference; upon
        /// failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The created interpreter, or null if an error occurs.
        /// </returns>
        public static Interpreter CreateInterpreter( /* CORE */
            ulong? token,                         /* in: OPTIONAL */
            GetFileNameCallback settingsCallback, /* in: OPTIONAL */
            IRuleSet ruleSet,                     /* in: OPTIONAL */
            Interpreter interpreter,              /* in: OPTIONAL */
            IPluginData pluginData,               /* in: OPTIONAL */
            string hashAlgorithmName,             /* in: OPTIONAL */
            byte[] hashKey,                       /* in: OPTIONAL */
            Encoding encoding,                    /* in */
            IEnumerable<IKeyPair> keyPairs,       /* in */
            string keyUsage,                      /* in: OPTIONAL */
            CultureInfo cultureInfo,              /* in: OPTIONAL */
            int? timeout,                         /* in: OPTIONAL */
            bool allowRemoteUri,                  /* in */
            ref bool created,                     /* out */
            ref Result result                     /* out */
            )
        {
            if (Configuration.DoesVariableExist(
                    Constants.NoCreateInterpreterEnvVarName))
            {
                result = "plugin interpreter creation disabled";
                return null;
            }

            if (encoding == null)
            {
                result = "invalid encoding";
                return null;
            }

            string settingsFileName = null;

            if (settingsCallback != null)
            {
                try
                {
                    settingsFileName = settingsCallback(
                        pluginData); /* throw */
                }
                catch (Exception e)
                {
                    result = e;
                    return null;
                }
            }

            if (String.IsNullOrEmpty(settingsFileName))
            {
                return CreateInterpreter(
                    token, null, ref created, ref result);
            }

            if (!File.Exists(settingsFileName))
            {
                result = String.Format(
                    "interpreter settings file {0} does not exist",
                    Utility.FormatWrapOrNull(settingsFileName));

                return null;
            }

            string signatureFileName = DataOps.FormatSignatureFileName(
                settingsFileName);

            if (String.IsNullOrEmpty(signatureFileName))
            {
                result = "invalid interpreter signature file name";
                return null;
            }

            if (!File.Exists(signatureFileName))
            {
                result = String.Format(
                    "interpreter signature file {0} does not exist",
                    Utility.FormatWrapOrNull(signatureFileName));

                return null;
            }

            byte[] signature = null;

            if (!DataOps.TryReadSignatureFile(
                    interpreter, encoding, signatureFileName,
                    timeout, allowRemoteUri, ref signature,
                    ref result))
            {
                return null;
            }

            string text = null;
            byte[] hashValue = null; /* NOT USED */
            IKeyPair keyPair = null;

            if (VerifyFile(
                    interpreter, pluginData, hashAlgorithmName,
                    hashKey, encoding, settingsFileName, keyPairs,
                    signature, cultureInfo, timeout, allowRemoteUri,
                    true, ref text, ref hashValue, ref keyPair,
                    ref result) != ReturnCode.Ok)
            {
                return null;
            }

            if ((keyUsage != null) && (CheckKeyUsage(
                    keyPair, keyUsage, EntityType.File,
                    ref result) != ReturnCode.Ok))
            {
                return null;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(
                        encoding.GetBytes(text))) /* throw */
                {
                    IInterpreterSettings interpreterSettings = null;

                    if (InterpreterSettings.LoadFrom(
                            settingsFileName, stream, cultureInfo,
                            true, true, ref interpreterSettings,
                            ref result) != ReturnCode.Ok)
                    {
                        return null;
                    }

                    if ((ruleSet != null) &&
                        (interpreterSettings != null) &&
                        (interpreterSettings.MaybeSetRuleSet(
                            ruleSet, ref result) != ReturnCode.Ok))
                    {
                        return null;
                    }

                    return CreateInterpreter(
                        token, interpreterSettings, ref created,
                        ref result);
                }
            }
            catch (Exception e)
            {
                result = e;
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes the interpreter identified by the specified token.
        /// </summary>
        /// <param name="token">
        /// The token identifying the interpreter to dispose.
        /// </param>
        /// <returns>
        /// Non-zero if the interpreter was found and disposed successfully.
        /// </returns>
        public static bool CleanupInterpreter( /* CORE */
            ulong token /* in */
            )
        {
            Interpreter interpreter = null;
            Result error = null;

            if (Value.GetInterpreter(null, token.ToString(),
                    InterpreterType.Eagle | InterpreterType.Token,
                    ref interpreter, ref error) != ReturnCode.Ok)
            {
                return false;
            }

            if (interpreter == null)
                return false;

            try
            {
                interpreter.SetDisposalEnabled(false, true); /* throw */
                interpreter.Dispose(); /* throw */
                interpreter = null;

                return true;
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch
#endif
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateScriptOps).Name,
                    TracePriority.Highest);
#endif

                return false;
            }
        }
    }
    #endregion
}
