/*
 * FileAndOrStreamData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.IO;
using System.Reflection;
using Eagle._Attributes;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Holds the data associated with a file and/or stream, including the
    /// assembly it originated from, its file name, the stream containing its
    /// contents, its computed hash value, and its signature.
    /// </summary>
    [ObjectId("f9cdb5f5-8363-43e8-9841-c715c2fb8a5a")]
    internal sealed class FileAndOrStreamData
    {
        #region Public Constructors
        /// <summary>
        /// Constructs an instance of this class using the specified file
        /// and/or stream data.
        /// </summary>
        /// <param name="assembly">
        /// The assembly that the file and/or stream originated from.
        /// </param>
        /// <param name="fileName">
        /// The name of the file associated with the data.
        /// </param>
        /// <param name="stream">
        /// The stream containing the contents of the file and/or stream.
        /// </param>
        /// <param name="hashValue">
        /// The computed hash value of the file and/or stream contents.
        /// </param>
        /// <param name="signature">
        /// The signature associated with the file and/or stream.
        /// </param>
        public FileAndOrStreamData(
            Assembly assembly, /* in */
            string fileName,   /* in */
            Stream stream,     /* in */
            byte[] hashValue,  /* in */
            byte[] signature   /* in */
            )
        {
            this.assembly = assembly;
            this.fileName = fileName;
            this.stream = stream;
            this.hashValue = hashValue;
            this.signature = signature;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The assembly that the file and/or stream originated from.
        /// </summary>
        private Assembly assembly;
        /// <summary>
        /// Gets or sets the assembly that the file and/or stream originated
        /// from.
        /// </summary>
        public Assembly Assembly
        {
            get { return assembly; }
            set { assembly = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The name of the file associated with the data.
        /// </summary>
        private string fileName;
        /// <summary>
        /// Gets or sets the name of the file associated with the data.
        /// </summary>
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The stream containing the contents of the file and/or stream.
        /// </summary>
        private Stream stream;
        /// <summary>
        /// Gets or sets the stream containing the contents of the file and/or
        /// stream.
        /// </summary>
        public Stream Stream
        {
            get { return stream; }
            set { stream = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The computed hash value of the file and/or stream contents.
        /// </summary>
        private byte[] hashValue;
        /// <summary>
        /// Gets or sets the computed hash value of the file and/or stream
        /// contents.
        /// </summary>
        public byte[] HashValue
        {
            get { return hashValue; }
            set { hashValue = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The signature associated with the file and/or stream.
        /// </summary>
        private byte[] signature;
        /// <summary>
        /// Gets or sets the signature associated with the file and/or stream.
        /// </summary>
        public byte[] Signature
        {
            get { return signature; }
            set { signature = value; }
        }
        #endregion
    }
}
