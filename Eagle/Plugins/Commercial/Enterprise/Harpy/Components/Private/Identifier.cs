/*
 * Identifier.cs --
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

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides a concrete implementation of the <see cref="IIdentifier" />
    /// interface, holding the kind, unique identifier, name, group, and
    /// description that together identify an object.
    /// </summary>
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("9157cf40-c669-4856-aee4-21aaff96746d")]
    internal class Identifier
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        : ScriptMarshalByRefObject, IIdentifier
#else
        : IIdentifier
#endif
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Identifier" /> class
        /// of the specified kind, assigning a freshly generated unique
        /// identifier.
        /// </summary>
        /// <param name="kind">
        /// The kind of identifier being created.
        /// </param>
        public Identifier(
            IdentifierKind kind /* in */
            )
        {
            this.kind = kind;
            this.id = Utility.GetObjectId(this);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new instance of the <see cref="Identifier" /> class
        /// of the specified kind, using the supplied identifier, name, group,
        /// and description.  When <paramref name="id" /> is
        /// <see cref="Guid.Empty" />, the generated unique identifier is
        /// retained instead.
        /// </summary>
        /// <param name="kind">
        /// The kind of identifier being created.
        /// </param>
        /// <param name="id">
        /// The unique identifier to use; when this is
        /// <see cref="Guid.Empty" />, the automatically generated identifier
        /// is kept.
        /// </param>
        /// <param name="name">
        /// The name associated with this identifier.
        /// </param>
        /// <param name="group">
        /// The group associated with this identifier.
        /// </param>
        /// <param name="description">
        /// The description associated with this identifier.
        /// </param>
        public Identifier(
            IdentifierKind kind, /* in */
            Guid id,             /* in */
            string name,         /* in */
            string group,        /* in */
            string description   /* in */
            )
            : this(kind)
        {
            if (!id.Equals(Guid.Empty))
                this.id = id;

            this.name = name;
            this.group = group;
            this.description = description;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierName Members
        /// <summary>
        /// The name associated with this identifier.
        /// </summary>
        private string name;
        /// <summary>
        /// Gets or sets the name associated with this identifier.
        /// </summary>
        public virtual string Name
        {
            get { return name; }
            set { name = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierBase Members
        /// <summary>
        /// The kind of this identifier.
        /// </summary>
        private IdentifierKind kind;
        /// <summary>
        /// Gets or sets the kind of this identifier.
        /// </summary>
        public virtual IdentifierKind Kind
        {
            get { return kind; }
            set { kind = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The unique identifier for this object.
        /// </summary>
        private Guid id;
        /// <summary>
        /// Gets or sets the unique identifier for this object.
        /// </summary>
        public virtual Guid Id
        {
            get { return id; }
            set { id = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetClientData / ISetClientData Members
        /// <summary>
        /// The opaque, caller-supplied data associated with this identifier.
        /// </summary>
        private IClientData clientData;
        /// <summary>
        /// Gets or sets the opaque, caller-supplied data associated with this
        /// identifier.
        /// </summary>
        public virtual IClientData ClientData
        {
            get { return clientData; }
            set { clientData = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifier Members
        /// <summary>
        /// The group associated with this identifier.
        /// </summary>
        private string group;
        /// <summary>
        /// Gets or sets the group associated with this identifier.
        /// </summary>
        public virtual string Group
        {
            get { return group; }
            set { group = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The description associated with this identifier.
        /// </summary>
        private string description;
        /// <summary>
        /// Gets or sets the description associated with this identifier.
        /// </summary>
        public virtual string Description
        {
            get { return description; }
            set { description = value; }
        }
        #endregion
    }
}
