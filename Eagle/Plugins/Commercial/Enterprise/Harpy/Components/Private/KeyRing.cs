/*
 * KeyRing.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;

using KeyPairDictionary =
    System.Collections.Generic.Dictionary<string,
        Licensing.Interfaces.Private.IKeyPair>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Represents an in-memory collection of named key pairs used for
    /// certificate licensing operations.
    /// </summary>
#if SERIALIZATION
    [Serializable()]
#endif
    [ObjectId("321da87d-f70e-41a3-8fd8-70823fbbd23a")]
    internal sealed class KeyRing : Identifier, IKeyRing
    {
        #region Private Data
        /// <summary>
        /// The object used to synchronize access to this instance.
        /// </summary>
        private readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The dictionary of key pairs contained in this key ring, keyed
        /// by name.
        /// </summary>
        private KeyPairDictionary ringKeyPairs = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new, empty instance of this class.
        /// </summary>
        public KeyRing() /* CORE? */
            : base(IdentifierKind.KeyRing)
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new instance of this class that contains a copy of
        /// the key pairs from the specified key ring.
        /// </summary>
        /// <param name="keyRing">
        /// The key ring whose key pairs should be copied into the new
        /// instance.
        /// </param>
        public KeyRing(
            IKeyRing keyRing /* in */
            ) /* CORE? */
            : this()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                ringKeyPairs = CopyKeyPairs(keyRing);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if PLUGIN_COMMANDS
        /// <summary>
        /// Constructs a new instance of this class that contains the
        /// specified key pairs.
        /// </summary>
        /// <param name="keyPairs">
        /// The key pairs to add to the new instance.
        /// </param>
        public KeyRing(
            IEnumerable<IKeyPair> keyPairs /* in */
            )
            : this()
        {
            ResetKeyPairs(keyPairs);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// Gets the underlying dictionary of key pairs from the specified
        /// key ring.
        /// </summary>
        /// <param name="keyRing">
        /// The key ring to obtain the key pairs from.
        /// </param>
        /// <returns>
        /// The dictionary of key pairs, or null if the key ring is not an
        /// instance of this class.
        /// </returns>
        private static KeyPairDictionary GetKeyPairs( /* CORE? */
            IKeyRing keyRing /* in */
            )
        {
            KeyRing localKeyRing = keyRing as KeyRing;

            if (localKeyRing == null)
                return null;

            return localKeyRing.ringKeyPairs;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a copy of the dictionary of key pairs from the
        /// specified key ring.
        /// </summary>
        /// <param name="keyRing">
        /// The key ring to copy the key pairs from.
        /// </param>
        /// <returns>
        /// A new dictionary containing the copied key pairs, or null if the
        /// key ring is not an instance of this class.
        /// </returns>
        private static KeyPairDictionary CopyKeyPairs( /* CORE? */
            IKeyRing keyRing /* in */
            )
        {
            KeyRing localKeyRing = keyRing as KeyRing;

            if (localKeyRing == null)
                return null;

            return localKeyRing.CopyKeyPairs();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
#if PLUGIN_COMMANDS
        /// <summary>
        /// Replaces the contents of this key ring with the specified key
        /// pairs.
        /// </summary>
        /// <param name="keyPairs">
        /// The key pairs to populate this key ring with.  Key pairs that
        /// are null or that lack a usable name are skipped.  This value may
        /// be null.
        /// </param>
        private void ResetKeyPairs( /* CORE? */
            IEnumerable<IKeyPair> keyPairs /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                //
                // HACK: This is semi-dangerous.  However, this method is
                //       currently only called from within the constructor
                //       when there cannot be any existing key pairs.
                //
                ringKeyPairs = new KeyPairDictionary();

                if (keyPairs != null)
                {
                    foreach (IKeyPair keyPair in keyPairs)
                    {
                        if (keyPair == null)
                            continue;

                        IIdentifierName identifierName =
                            keyPair as IIdentifierName;

                        if (identifierName == null)
                            continue;

                        string name = identifierName.Name;

                        if (name == null)
                            continue;

                        ringKeyPairs[name] = keyPair;
                    }
                }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a copy of the dictionary of key pairs contained in this
        /// key ring.
        /// </summary>
        /// <returns>
        /// A new dictionary containing the copied key pairs, or null if
        /// this key ring has no key pairs.
        /// </returns>
        private KeyPairDictionary CopyKeyPairs() /* CORE? */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (ringKeyPairs == null)
                    return null;

                KeyPairDictionary keyPairs = new KeyPairDictionary();

                foreach (KeyValuePair<string, IKeyPair> pair
                        in ringKeyPairs)
                {
                    keyPairs.Add(pair.Key, pair.Value);
                }

                return keyPairs;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether this key ring contains a key pair with the
        /// specified public key token.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token to search for.
        /// </param>
        /// <param name="name">
        /// Upon success, receives the name of the matching key pair.
        /// </param>
        /// <returns>
        /// Non-zero if a matching key pair was found; otherwise, zero.
        /// </returns>
        private bool HaveToken( /* CORE? */
            byte[] publicKeyToken, /* in */
            ref string name        /* out */
            )
        {
            if (publicKeyToken == null)
                return false;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (ringKeyPairs == null)
                    return false;

                foreach (KeyValuePair<string, IKeyPair> pair
                        in ringKeyPairs)
                {
                    IKeyPair keyPair = pair.Value;

                    if (keyPair == null)
                        continue;

                    byte[] localPublicKeyToken = keyPair.PublicKeyToken;

                    if (localPublicKeyToken == null)
                        continue;

                    if (CertificateDataOps.MatchPublicKeyToken(
                            localPublicKeyToken, publicKeyToken))
                    {
                        name = pair.Key;
                        return true;
                    }
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to add or update the key pair with the specified name
        /// in this key ring.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to add or update.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to associate with the specified name.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to allow an existing key pair with the same name to be
        /// overwritten.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow a key pair with a public key token that
        /// already exists in this key ring.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the key pair was added or updated; otherwise, zero.
        /// </returns>
        private bool MaybeAddOrUpdate( /* CORE? */
            string name,         /* in */
            IKeyPair keyPair,    /* in */
            bool overwrite,      /* in */
            bool allowDuplicate, /* in */
            ref Result error     /* out */
            )
        {
            if (name == null)
            {
                error = "invalid key pair name";
                return false;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (ringKeyPairs == null)
                    ringKeyPairs = new KeyPairDictionary();

                if (!overwrite &&
                    ringKeyPairs.ContainsKey(name)) /* EXEMPT */
                {
                    error = String.Format(
                        "can't add {0}: key pair already exists",
                        Utility.FormatWrapOrNull(name));

                    return false;
                }

                string tokenName = null;

                if (!allowDuplicate && (keyPair != null) &&
                    HaveToken(keyPair.PublicKeyToken, ref tokenName))
                {
                    error = String.Format(
                        "can't add {0}: public key token {1} " +
                        "already exists in {2}",
                        Utility.FormatWrapOrNull(name),
                        CertificateDataOps.FormatPublicKeyToken(
                            keyPair.PublicKeyToken, true, true),
                        Utility.FormatWrapOrNull(tokenName));

                    return false;
                }

                ringKeyPairs[name] = keyPair;
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to remove the key pair with the specified name from
        /// this key ring.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to remove.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to treat a missing key pair as an error.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was removed, if any.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero if the operation succeeded; otherwise, zero.
        /// </returns>
        private bool MaybeRemove( /* CORE? */
            string name,          /* in */
            bool errorOnNotFound, /* in */
            out IKeyPair keyPair, /* out */
            ref Result error      /* out */
            )
        {
            keyPair = null;

            if (name == null)
            {
                error = "invalid key pair name";
                return false;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (ringKeyPairs == null)
                    ringKeyPairs = new KeyPairDictionary();

                IKeyPair localKeyPair;

                if (!ringKeyPairs.TryGetValue(
                        name, out localKeyPair)) /* EXEMPT */
                {
                    if (errorOnNotFound)
                    {
                        error = String.Format(
                            "can't remove {0}: key pair does not exist",
                            Utility.FormatWrapOrNull(name));

                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }

                if (!ringKeyPairs.Remove(name))
                {
                    error = String.Format(
                        "can't remove {0}: operation failed",
                        Utility.FormatWrapOrNull(name));

                    return false;
                }

                keyPair = localKeyPair;
                return true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IKeyRing Members
        /// <summary>
        /// Determines whether this key ring contains any key pairs.
        /// </summary>
        /// <returns>
        /// Non-zero if this key ring contains at least one key pair;
        /// otherwise, zero.
        /// </returns>
        public bool IsNonEmpty() /* CORE? */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (ringKeyPairs == null)
                    return false;

                return ringKeyPairs.Count > 0;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a key pair with the specified name is present
        /// in this key ring.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to search for.
        /// </param>
        /// <returns>
        /// Non-zero if a matching key pair is present; otherwise, zero.
        /// </returns>
        public bool IsPresentByName( /* CORE? */
            string name /* in */
            )
        {
            IKeyPair keyPair = null;

            return IsPresentByName(name, ref keyPair);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a key pair with the specified name is present
        /// in this key ring.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to search for.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the matching key pair.
        /// </param>
        /// <returns>
        /// Non-zero if a matching key pair is present; otherwise, zero.
        /// </returns>
        public bool IsPresentByName( /* CORE? */
            string name,         /* in */
            ref IKeyPair keyPair /* out */
            )
        {
            if (name == null)
                return false;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (ringKeyPairs == null)
                    return false;

                return ringKeyPairs.TryGetValue(name, out keyPair);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a key pair with the specified public key
        /// token is present in this key ring.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token to search for.
        /// </param>
        /// <returns>
        /// Non-zero if a matching key pair is present; otherwise, zero.
        /// </returns>
        public bool IsPresentByToken( /* CORE? */
            byte[] publicKeyToken /* in */
            )
        {
            IKeyPair keyPair = null;

            return IsPresentByToken(publicKeyToken, ref keyPair);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a key pair with the specified public key
        /// token is present in this key ring.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token to search for.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the matching key pair.
        /// </param>
        /// <returns>
        /// Non-zero if a matching key pair is present; otherwise, zero.
        /// </returns>
        public bool IsPresentByToken( /* CORE? */
            byte[] publicKeyToken, /* in */
            ref IKeyPair keyPair   /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (ringKeyPairs == null)
                    return false;

                foreach (KeyValuePair<string, IKeyPair> pair
                        in ringKeyPairs)
                {
                    IKeyPair localKeyPair = pair.Value;

                    if (localKeyPair == null)
                        continue;

                    byte[] localPublicKeyToken = localKeyPair.PublicKeyToken;

                    if ((publicKeyToken == null) || Utility.ArrayEquals(
                            localPublicKeyToken, publicKeyToken))
                    {
                        keyPair = localKeyPair;
                        return true;
                    }
                }

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all key pairs from this key ring.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode Clear(
            ref Result error /* out: NOT USED */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (ringKeyPairs != null)
                    ringKeyPairs.Clear();

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a list of the string representations of the key pairs in
        /// this key ring.
        /// </summary>
        /// <param name="list">
        /// Upon success, receives the list of key pair representations, or
        /// null if this key ring has no key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode List(
            ref StringList list, /* out */
            ref Result error     /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                IStringList localList = null;

                if (ringKeyPairs == null)
                {
                    list = null;
                    return ReturnCode.Ok;
                }

                foreach (KeyValuePair<string, IKeyPair> pair
                        in ringKeyPairs)
                {
                    IKeyPair keyPair = pair.Value;

                    if (keyPair == null)
                        continue;

                    keyPair.AddToList(ref localList);
                }

                list = localList as StringList;
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified key pair to this key ring, incrementing the
        /// count of added key pairs on success.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to add.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to add.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to allow an existing key pair with the same name to be
        /// overwritten.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow a key pair with a public key token that
        /// already exists in this key ring.
        /// </param>
        /// <param name="added">
        /// The running count of added key pairs, incremented on success.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode Add( /* CORE? */
            string name,         /* in */
            IKeyPair keyPair,    /* in */
            bool overwrite,      /* in */
            bool allowDuplicate, /* in */
            ref int added,       /* in, out */
            ref Result error     /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (MaybeAddOrUpdate(
                        name, keyPair, overwrite,
                        allowDuplicate, ref error))
                {
                    added++;
                }
                else
                {
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the specified key pair from this key ring, incrementing
        /// the count of removed key pairs on success.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to remove.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to treat a missing key pair as an error.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was removed, if any.
        /// </param>
        /// <param name="removed">
        /// The running count of removed key pairs, incremented on success.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode Remove( /* CORE? */
            string name,          /* in */
            bool errorOnNotFound, /* in */
            ref IKeyPair keyPair, /* out */
            ref int removed,      /* in, out */
            ref Result error      /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (MaybeRemove(
                        name, errorOnNotFound,
                        out keyPair, ref error))
                {
                    removed++;
                }
                else
                {
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Merges the key pairs from the specified key ring into this key
        /// ring, incrementing the count of merged key pairs on success.
        /// </summary>
        /// <param name="keyRing">
        /// The key ring whose key pairs should be merged into this key
        /// ring.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to allow existing key pairs with the same name to be
        /// overwritten.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow key pairs with a public key token that already
        /// exists in this key ring.
        /// </param>
        /// <param name="merged">
        /// The running count of merged key pairs, incremented for each key
        /// pair that is merged.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode Merge( /* CORE? */
            IKeyRing keyRing,    /* in */
            bool overwrite,      /* in */
            bool allowDuplicate, /* in */
            ref int merged,      /* in, out */
            ref Result error     /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                KeyPairDictionary localKeyPairs = GetKeyPairs(
                    keyRing);

                if (localKeyPairs != null)
                {
                    foreach (KeyValuePair<string, IKeyPair> pair
                            in localKeyPairs)
                    {
                        IKeyPair keyPair = pair.Value;

                        if (keyPair == null)
                            continue;

                        if (MaybeAddOrUpdate(
                                pair.Key, keyPair, overwrite,
                                allowDuplicate, ref error))
                        {
                            merged++;
                        }
                        else
                        {
                            return ReturnCode.Error;
                        }
                    }
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the public-only key pairs from the specified file and adds
        /// them to this key ring.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to use when loading the key pairs.
        /// </param>
        /// <param name="policy">
        /// The execution policy to apply when loading the key pairs.  This
        /// value may be null.
        /// </param>
        /// <param name="fileName">
        /// The name of the file to load the key pairs from.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use for any culture-sensitive operations.  This
        /// value may be null.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to allow existing key pairs with the same name to be
        /// overwritten.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow key pairs with a public key token that already
        /// exists in this key ring.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode LoadPublicOnly( /* CORE? */
            Interpreter interpreter, /* in */
            ExecutionPolicy? policy, /* in: OPTIONAL */
            string fileName,         /* in */
            CultureInfo cultureInfo, /* in: OPTIONAL */
            bool overwrite,          /* in */
            bool allowDuplicate,     /* in */
            ref Result error         /* out */
            )
        {
            ReturnCode code;
            KeyPairDictionary localKeyPairs = null;

            code = CertificateKeyRingOps.LoadKeyPairs(
                interpreter, policy, fileName, cultureInfo, false,
                null, true, false, overwrite, true, ref localKeyPairs,
                ref error);

            if (code == ReturnCode.Ok)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    if (localKeyPairs != null)
                    {
                        foreach (KeyValuePair<string, IKeyPair> pair
                                in localKeyPairs)
                        {
                            IKeyPair keyPair = pair.Value;

                            if (keyPair == null)
                                continue;

                            if (!MaybeAddOrUpdate(
                                    pair.Key, keyPair, overwrite,
                                    allowDuplicate, ref error))
                            {
                                return ReturnCode.Error;
                            }
                        }
                    }
                }
            }

            return code;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IKeyRing Members
        /// <summary>
        /// Gets the key pairs contained in this key ring.
        /// </summary>
        /// <param name="keyPairs">
        /// Upon success, receives the key pairs contained in this key ring,
        /// or null if this key ring has no key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode List( /* CORE? */
            ref IEnumerable<IKeyPair> keyPairs,
            ref Result error
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                keyPairs = (ringKeyPairs != null) ?
                    new List<IKeyPair>(ringKeyPairs.Values) : null;

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key pairs in this key ring that match the specified
        /// public key token.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token to filter the key pairs by.  This value may
        /// be null.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the matching key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode ListByToken( /* CORE? */
            byte[] publicKeyToken,              /* in: OPTIONAL */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out: NOT USED */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                IEnumerable<IKeyPair> localKeyPairs = null;

                if (ringKeyPairs != null)
                {
                    Result localError = null;

                    localKeyPairs = CertificateKeyPairOps.FilterByToken(
                        ringKeyPairs.Values, publicKeyToken, false,
                        ref localError);

                    if ((localKeyPairs == null) && (localError != null))
                    {
                        error = localError;
                        return ReturnCode.Error;
                    }
                }

                keyPairs = localKeyPairs;
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key pairs in this key ring whose names match the
        /// specified pattern.
        /// </summary>
        /// <param name="pattern">
        /// The pattern to match key pair names against.  This value may be
        /// null.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to perform a case-insensitive match.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the matching key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Ok on success; otherwise, an error code.
        /// </returns>
        public ReturnCode ListByName( /* CORE? */
            string pattern,                     /* in: OPTIONAL */
            bool noCase,                        /* in */
            ref IEnumerable<IKeyPair> keyPairs, /* out */
            ref Result error                    /* out: NOT USED */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                IEnumerable<IKeyPair> localKeyPairs = null;

                if (ringKeyPairs != null)
                {
                    Result localError = null;

                    localKeyPairs = CertificateKeyPairOps.FilterByName(
                        ringKeyPairs.Values, pattern, noCase, false,
                        ref localError);

                    if ((localKeyPairs == null) && (localError != null))
                    {
                        error = localError;
                        return ReturnCode.Error;
                    }
                }

                keyPairs = localKeyPairs;
                return ReturnCode.Ok;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
        /// <summary>
        /// Returns a string representation of this key ring.
        /// </summary>
        /// <returns>
        /// The name of this key ring.
        /// </returns>
        public override string ToString()
        {
            return base.Name;
        }
        #endregion
    }
}
