/*
 * ScriptPageData.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Kapok.Components.Public;
using Kapok.Interfaces.Public;

using EnvironmentPair = Eagle._Interfaces.Public.IAnyPair<
    string, Kapok.Components.Shared.SettingDataType>;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Implements the configuration data for a script page (<see
    /// cref="Kapok.Interfaces.Public.IScriptPageData" />), holding its setup
    /// script, script file, interpreter caching and security settings, and
    /// per-page environment variables.
    /// </summary>
    [ObjectId("2cc531d6-4e01-42e4-8191-a0120aa4eeb7")]
    internal sealed class ScriptPageData : IScriptPageData
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new, empty <see cref="ScriptPageData" /> instance.
        /// </summary>
        public ScriptPageData()
        {
            //
            // NOTE: Set the default Harpy SDK "security" subsystem flags.
            //
            this.securityFlags = SecurityFlags.Default;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IScriptPageData Members
        //
        // NOTE: The setup script to evaluate prior to evaluating the primary
        //       script file being wrapped.
        //
        /// <summary>
        /// The backing field for the <see cref="Setup" /> property.
        /// </summary>
        private string setup;
        /// <summary>
        /// Gets or sets the setup script evaluated before the page script.
        /// </summary>
        public string Setup
        {
            get { return setup; }
            set { setup = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Treat the primary script file (below) as though it contains
        //       mixed-mode (HTML and script) content to be processed via the
        //       Eagle._Components.Public.ScriptBlocks class.
        //
        /// <summary>
        /// The backing field for the <see cref="Blocks" /> property.
        /// </summary>
        private bool blocks;
        /// <summary>
        /// Gets or sets a value indicating whether the page uses mixed
        /// HTML/script blocks.
        /// </summary>
        public bool Blocks
        {
            get { return blocks; }
            set { blocks = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the flags to use when processing script blocks (see
        //       above).
        //
        /// <summary>
        /// The backing field for the <see cref="BlockFlags" /> property.
        /// </summary>
        private ScriptBlockFlags blockFlags;
        /// <summary>
        /// Gets or sets the flags controlling script-block processing.
        /// </summary>
        public ScriptBlockFlags BlockFlags
        {
            get { return blockFlags; }
            set { blockFlags = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The primary script file being wrapped.  This script file will
        //       be evaluated after the setup script.
        //
        /// <summary>
        /// The backing field for the <see cref="FileName" /> property.
        /// </summary>
        private string fileName;
        /// <summary>
        /// Gets or sets the file name of the page script.
        /// </summary>
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This must be non-zero if this page should perform the script
        //       evaluations (as configured); otherwise, an error response will
        //       be generated instead.
        //
        /// <summary>
        /// The backing field for the <see cref="Enabled" /> property.
        /// </summary>
        private bool enabled;
        /// <summary>
        /// Gets or sets a value indicating whether the page is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This must be non-zero if this page should check for a valid,
        //       non-expired license prior to processing the request.
        //
        /// <summary>
        /// The backing field for the <see cref="LicensingEnabled" /> property.
        /// </summary>
        private bool licensingEnabled;
        /// <summary>
        /// Gets or sets a value indicating whether licensing is enabled for
        /// the page.
        /// </summary>
        public bool LicensingEnabled
        {
            get { return licensingEnabled; }
            set { licensingEnabled = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This must be non-zero if this page requires an interpreter
        //       for any aspect of its request processing; otherwise, there
        //       may be NO interpreter available.
        //
        /// <summary>
        /// The backing field for the <see cref="CreateInterpreter" />
        /// property.
        /// </summary>
        private bool createInterpreter;
        /// <summary>
        /// Gets or sets a value indicating whether a new interpreter should be
        /// created for the page.
        /// </summary>
        public bool CreateInterpreter
        {
            get { return createInterpreter; }
            set { createInterpreter = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This must be non-zero if this page allows an interpreter to
        //       be cached for future use.
        //
        /// <summary>
        /// The backing field for the <see cref="CacheInterpreter" /> property.
        /// </summary>
        private bool cacheInterpreter;
        /// <summary>
        /// Gets or sets a value indicating whether the page interpreter should
        /// be cached.
        /// </summary>
        public bool CacheInterpreter
        {
            get { return cacheInterpreter; }
            set { cacheInterpreter = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This must be negative if this page allows an interpreter to
        //       live forever; otherwise, it is subject to disposal at *ANY*
        //       time that it is not actively in use.
        //
        /// <summary>
        /// The backing field for the <see cref="CacheSeconds" /> property.
        /// </summary>
        private long cacheSeconds;
        /// <summary>
        /// Gets or sets the number of seconds a cached interpreter remains
        /// fresh.
        /// </summary>
        public long CacheSeconds
        {
            get { return cacheSeconds; }
            set { cacheSeconds = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This will be non-zero if the Harpy SDK "security" is enabled.
        //       It is used to make sure that full script signing is enabled
        //       and working, among other things (i.e. for copy protection).
        //
        /// <summary>
        /// The backing field for the <see cref="SecurityLevel" /> property.
        /// </summary>
        private int securityLevel;
        /// <summary>
        /// Gets or sets the security level applied to the page interpreter.
        /// </summary>
        public int SecurityLevel
        {
            get { return securityLevel; }
            set { securityLevel = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This will contain the flags used when dealing with the Harpy
        //       SDK "security" subsystem.  However, they are NOT used by the
        //       Harpy SDK "security" subsystem itself.
        //
        /// <summary>
        /// The backing field for the <see cref="SecurityFlags" /> property.
        /// </summary>
        private SecurityFlags securityFlags;
        /// <summary>
        /// Gets or sets the security flags applied to the page interpreter.
        /// </summary>
        public SecurityFlags SecurityFlags
        {
            get { return securityFlags; }
            set { securityFlags = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Environment" /> property.
        /// </summary>
        private IEnumerable<EnvironmentPair> environment;
        /// <summary>
        /// Gets or sets the per-page environment variables (name/data-type
        /// pairs).
        /// </summary>
        public IEnumerable<EnvironmentPair> Environment
        {
            get { return environment; }
            set { environment = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
        /// <summary>
        /// Returns a string that represents this page data.
        /// </summary>
        /// <returns>
        /// A string that represents this page data.
        /// </returns>
        public override string ToString()
        {
            StringList list = new StringList();

            if (setup != null)
            {
                list.Add("setup");
                list.Add(setup);
            }

            list.Add("blocks");
            list.Add(blocks.ToString());

            list.Add("blockFlags");
            list.Add(blockFlags.ToString());

            if (fileName != null)
            {
                list.Add("fileName");
                list.Add(fileName);
            }

            list.Add("enabled");
            list.Add(enabled.ToString());

            list.Add("licensingEnabled");
            list.Add(licensingEnabled.ToString());

            list.Add("createInterpreter");
            list.Add(createInterpreter.ToString());

            list.Add("cacheInterpreter");
            list.Add(cacheInterpreter.ToString());

            list.Add("cacheSeconds");
            list.Add(cacheSeconds.ToString());

            list.Add("securityLevel");
            list.Add(securityLevel.ToString());

            list.Add("securityFlags");
            list.Add(securityFlags.ToString());

            if (environment != null)
            {
                StringList subList = new StringList();

                foreach (EnvironmentPair pair in environment)
                {
                    string name = pair.X;

                    if (name == null)
                        continue;

                    subList.Add(name);
                    subList.Add(pair.Y.ToString());
                }

                list.Add("environment");
                list.Add(subList.ToString());
            }

            return list.ToString();
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
            {
                throw new ObjectDisposedException(
                    typeof(ScriptPageData).Name);
            }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from <see
        /// cref="IDisposable.Dispose" />; zero if it is being called from the
        /// finalizer.
        /// </param>
        private /* protected virtual */ void Dispose(
            bool disposing /* in */
            )
        {
            if (!disposed)
            {
                // if (disposing)
                // {
                //     ////////////////////////////////////
                //     // dispose managed resources here...
                //     ////////////////////////////////////
                // }

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
        /// Finalizes this object, releasing any resources that were not
        /// released by an explicit call to <see cref="Dispose()" />.
        /// </summary>
        ~ScriptPageData()
        {
            Dispose(false);
        }
        #endregion
    }
}
