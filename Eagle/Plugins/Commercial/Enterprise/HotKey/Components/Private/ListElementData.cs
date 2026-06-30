/*
 * ListElementData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Holds a key, a value, and an arbitrary tag for a single list element,
    /// used by the plugin's list-selection support.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the element key.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of the element value.
    /// </typeparam>
    [ObjectId("566ed084-64a9-4bee-8773-e5c38fdeca0e")]
    internal sealed class ListElementData<TKey, TValue>
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new, empty
        /// <see cref="ListElementData{TKey, TValue}" /> instance with default
        /// key, value, and tag.
        /// </summary>
        public ListElementData()
            : this(default(TKey), default(TValue), null)
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new <see cref="ListElementData{TKey, TValue}" />
        /// instance with the specified key, value, and tag.
        /// </summary>
        /// <param name="key">
        /// The element key.
        /// </param>
        /// <param name="value">
        /// The element value.
        /// </param>
        /// <param name="tag">
        /// An arbitrary tag associated with the element.
        /// </param>
        public ListElementData(
            TKey key,     /* in: OPTIONAL */
            TValue value, /* in: OPTIONAL */
            object tag    /* in: OPTIONAL */
            )
        {
            this.key = key;
            this.value = value;
            this.tag = tag;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The backing field for the <see cref="Key" /> property.
        /// </summary>
        private TKey key;

        /// <summary>
        /// Gets or sets the element key.
        /// </summary>
        public TKey Key
        {
            get { return key; }
            set { key = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Value" /> property.
        /// </summary>
        private TValue value;

        /// <summary>
        /// Gets or sets the element value.
        /// </summary>
        public TValue Value
        {
            get { return value; }
            set { this.value = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Tag" /> property.
        /// </summary>
        private object tag;

        /// <summary>
        /// Gets or sets an arbitrary tag associated with the element.
        /// </summary>
        public object Tag
        {
            get { return tag; }
            set { tag = value; }
        }
        #endregion
    }
}
