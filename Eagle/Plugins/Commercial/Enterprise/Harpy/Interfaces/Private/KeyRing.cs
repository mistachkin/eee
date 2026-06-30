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

using System.Collections.Generic;
using System.Globalization;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;

namespace Licensing.Interfaces.Private
{
    /// <summary>
    /// Represents a collection of named key pairs (a "key ring") that can be
    /// queried, modified, merged, and populated from external sources.
    /// </summary>
    [ObjectId("758322f9-65e3-484c-b71e-1c33eeb6a563")]
    internal interface IKeyRing /* CORE? */
    {
        /// <summary>
        /// Determines whether this key ring contains at least one key pair.
        /// </summary>
        /// <returns>
        /// Non-zero if the key ring contains one or more key pairs; otherwise,
        /// zero.
        /// </returns>
        bool IsNonEmpty();

        /// <summary>
        /// Determines whether a key pair with the specified name is present in
        /// this key ring.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to look for.
        /// </param>
        /// <returns>
        /// Non-zero if a key pair with the specified name is present;
        /// otherwise, zero.
        /// </returns>
        bool IsPresentByName(
            string name
            );

        /// <summary>
        /// Determines whether a key pair with the specified name is present in
        /// this key ring, returning the matching key pair if one is found.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to look for.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair matching
        /// <paramref name="name" />.
        /// </param>
        /// <returns>
        /// Non-zero if a key pair with the specified name is present;
        /// otherwise, zero.
        /// </returns>
        bool IsPresentByName(
            string name,
            ref IKeyPair keyPair
            );

        /// <summary>
        /// Determines whether a key pair with the specified public key
        /// token is present in this key ring.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token of the key pair to look for.
        /// </param>
        /// <returns>
        /// Non-zero if a key pair with the specified public key token is
        /// present; otherwise, zero.
        /// </returns>
        bool IsPresentByToken(
            byte[] publicKeyToken
            );

        /// <summary>
        /// Determines whether a key pair with the specified public key
        /// token is present in this key ring, returning the matching key
        /// pair if one is found.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token of the key pair to look for.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair matching
        /// <paramref name="publicKeyToken" />.
        /// </param>
        /// <returns>
        /// Non-zero if a key pair with the specified public key token is
        /// present; otherwise, zero.
        /// </returns>
        bool IsPresentByToken(
            byte[] publicKeyToken,
            ref IKeyPair keyPair
            );

        /// <summary>
        /// Removes all key pairs from this key ring.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode Clear(
            ref Result error
            );

        /// <summary>
        /// Lists the names of the key pairs contained in this key ring.
        /// </summary>
        /// <param name="list">
        /// Upon success, receives the list of key pair names.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode List(
            ref StringList list,
            ref Result error
            );

        /// <summary>
        /// Lists the key pairs contained in this key ring.
        /// </summary>
        /// <param name="keyPairs">
        /// Upon success, receives the contained key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode List(
            ref IEnumerable<IKeyPair> keyPairs,
            ref Result error
            );

        /// <summary>
        /// Lists the key pairs in this key ring that have the specified public
        /// key token.
        /// </summary>
        /// <param name="publicKeyToken">
        /// The public key token used to select matching key pairs.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the matching key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode ListByToken(
            byte[] publicKeyToken,
            ref IEnumerable<IKeyPair> keyPairs,
            ref Result error
            );

        /// <summary>
        /// Lists the key pairs in this key ring whose names match the
        /// specified pattern.
        /// </summary>
        /// <param name="pattern">
        /// The pattern used to match key pair names.
        /// </param>
        /// <param name="noCase">
        /// Non-zero to perform case-insensitive pattern matching.
        /// </param>
        /// <param name="keyPairs">
        /// Upon success, receives the matching key pairs.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode ListByName(
            string pattern,
            bool noCase,
            ref IEnumerable<IKeyPair> keyPairs,
            ref Result error
            );

        /// <summary>
        /// Adds a key pair with the specified name to this key ring.
        /// </summary>
        /// <param name="name">
        /// The name under which to add the key pair.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to add.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to overwrite an existing key pair with the same name.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow a duplicate key pair to be added.
        /// </param>
        /// <param name="added">
        /// Upon success, receives the number of key pairs that were added.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode Add(
            string name,
            IKeyPair keyPair,
            bool overwrite,
            bool allowDuplicate,
            ref int added,
            ref Result error
            );

        /// <summary>
        /// Removes the key pair with the specified name from this key ring.
        /// </summary>
        /// <param name="name">
        /// The name of the key pair to remove.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to treat the absence of a matching key pair as an error.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was removed.
        /// </param>
        /// <param name="removed">
        /// Upon success, receives the number of key pairs that were removed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode Remove(
            string name,
            bool errorOnNotFound,
            ref IKeyPair keyPair,
            ref int removed,
            ref Result error
            );

        /// <summary>
        /// Merges the key pairs from another key ring into this key ring.
        /// </summary>
        /// <param name="keyRing">
        /// The key ring whose key pairs should be merged into this one.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to overwrite existing key pairs with the same name.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow duplicate key pairs to be added.
        /// </param>
        /// <param name="merged">
        /// Upon success, receives the number of key pairs that were merged.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode Merge(
            IKeyRing keyRing,
            bool overwrite,
            bool allowDuplicate,
            ref int merged,
            ref Result error
            );

        /// <summary>
        /// Loads public-only key pairs from the specified file into this key
        /// ring.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to use when loading the key pairs.
        /// </param>
        /// <param name="policy">
        /// The execution policy to apply while loading, or null to use the
        /// default policy.
        /// </param>
        /// <param name="fileName">
        /// The name of the file from which to load the key pairs.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture to use when loading the key pairs.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to overwrite existing key pairs with the same name.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow duplicate key pairs to be added.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an error code.
        /// </returns>
        ReturnCode LoadPublicOnly(
            Interpreter interpreter,
            ExecutionPolicy? policy,
            string fileName,
            CultureInfo cultureInfo,
            bool overwrite,
            bool allowDuplicate,
            ref Result error
            );
    }
}
