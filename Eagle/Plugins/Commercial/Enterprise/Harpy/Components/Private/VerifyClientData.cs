/*
 * VerifyClientData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Globalization;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides the client data used while verifying a Harpy license
    /// certificate, carrying the interpreter, plugin, encoding, logging,
    /// culture, timeout, and validation state for the operation.
    /// </summary>
    [ObjectId("491e9df1-8d23-4f41-a362-3a9d88f244e2")]
    internal class VerifyClientData :
            ClientData, IGetInterpreter, IHaveEncoding, IHaveCultureInfo
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the data in this
        /// instance.
        /// </summary>
        private readonly object syncRoot = new object();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs an instance of this class.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with this client data.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the license certificate being
        /// verified.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use when processing data.
        /// </param>
        /// <param name="logClientData">
        /// The client data used for logging during verification.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture information to use when processing data.
        /// </param>
        /// <param name="timeout">
        /// The timeout, in milliseconds, to use; may be null to indicate no
        /// specific timeout.
        /// </param>
        /// <param name="fileNameFlags">
        /// The flags used when processing file names.
        /// </param>
        /// <param name="wasValidated">
        /// Non-zero if the associated data has already been validated.
        /// </param>
        public VerifyClientData( /* CORE */
            Interpreter interpreter,      /* in */
            IPluginData pluginData,       /* in */
            Encoding encoding,            /* in */
            ILogClientData logClientData, /* in */
            CultureInfo cultureInfo,      /* in */
            int? timeout,                 /* in */
            FileNameFlags fileNameFlags,  /* in */
            bool wasValidated             /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                this.interpreter = interpreter;
                this.pluginData = pluginData;
                this.encoding = encoding;
                this.logClientData = logClientData;
                this.cultureInfo = cultureInfo;
                this.timeout = timeout;
                this.fileNameFlags = fileNameFlags;
                this.wasValidated = wasValidated;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetInterpreter Members
        /// <summary>
        /// The interpreter associated with this client data.
        /// </summary>
        private Interpreter interpreter;
        /// <summary>
        /// Gets the interpreter associated with this client data.
        /// </summary>
        public Interpreter Interpreter
        {
            get { lock (syncRoot) { return interpreter; } }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHaveEncoding Members
        /// <summary>
        /// The encoding to use when processing data.
        /// </summary>
        private Encoding encoding;
        /// <summary>
        /// Gets or sets the encoding to use when processing data.
        /// </summary>
        public Encoding Encoding
        {
            get { lock (syncRoot) { return encoding; } }
            set { throw new NotImplementedException(); }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHaveCultureInfo Members
        /// <summary>
        /// The culture information to use when processing data.
        /// </summary>
        private CultureInfo cultureInfo;
        /// <summary>
        /// Gets or sets the culture information to use when processing data.
        /// </summary>
        public CultureInfo CultureInfo
        {
            get { lock (syncRoot) { return cultureInfo; } }
            set { throw new NotImplementedException(); }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The plugin data associated with the license certificate being
        /// verified.
        /// </summary>
        private IPluginData pluginData;
        /// <summary>
        /// Gets or sets the plugin data associated with the license
        /// certificate being verified.
        /// </summary>
        public IPluginData PluginData
        {
            get { lock (syncRoot) { return pluginData; } }
            set { throw new NotImplementedException(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The client data used for logging during verification.
        /// </summary>
        private ILogClientData logClientData;
        /// <summary>
        /// Gets the client data used for logging during verification.
        /// </summary>
        public ILogClientData LogClientData
        {
            get { lock (syncRoot) { return logClientData; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The timeout, in milliseconds, to use; may be null to indicate no
        /// specific timeout.
        /// </summary>
        private int? timeout;
        /// <summary>
        /// Gets the timeout, in milliseconds, to use; may be null to
        /// indicate no specific timeout.
        /// </summary>
        public int? Timeout
        {
            get { lock (syncRoot) { return timeout; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The flags used when processing file names.
        /// </summary>
        private FileNameFlags fileNameFlags;
        /// <summary>
        /// Gets the flags used when processing file names.
        /// </summary>
        public FileNameFlags FileNameFlags
        {
            get { lock (syncRoot) { return fileNameFlags; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero if the associated data has already been validated.
        /// </summary>
        private bool wasValidated;
        /// <summary>
        /// Gets or sets a value indicating whether the associated data has
        /// already been validated.
        /// </summary>
        public bool WasValidated
        {
            get { lock (syncRoot) { return wasValidated; } }
            set { lock (syncRoot) { wasValidated = value; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The list of errors encountered during verification.
        /// </summary>
        private ResultList errors;
        /// <summary>
        /// Gets or sets the list of errors encountered during verification.
        /// </summary>
        public ResultList Errors
        {
            get { lock (syncRoot) { return errors; } }
            set { lock (syncRoot) { errors = value; } }
        }
        #endregion
    }
}
