/*
 * CertificateKeyPairState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;
using This = Licensing.Components.Private.CertificateKeyPairState;

using ObjectKeyPairDictionary =
    System.Collections.Generic.Dictionary<object,
        Licensing.Interfaces.Private.IKeyPair>;

using KeyPairPair = System.Collections.Generic.KeyValuePair<
    Eagle._Interfaces.Public.IInterpreter, object>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides per-interpreter storage and lifecycle management for the RSA
    /// key pairs used when approving a script, file, or stream from within a
    /// licensing policy callback.
    /// </summary>
    [ObjectId("1808bab3-28bf-41d0-ae95-e3f4f37122ab")]
    internal static class CertificateKeyPairState
    {
        #region Private Data
        //
        // NOTE: This is used to synchronize access to the private key ring
        //       and key pair data in this class (i.e. which is used by the
        //       policy subsystem).
        //
        /// <summary>
        /// Used to synchronize access to the private key ring and key pair
        /// data in this class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this is non-zero, all expiration date and revocation
        //       checking for policies will require network access.
        //
        /// <summary>
        /// When non-zero, all expiration date and revocation checking for
        /// policies will require network access.
        /// </summary>
        private static bool forceNetwork = false;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the list of RSA key pairs, on a per-interpreter
        //       basis, that were used when approving a script, file, or
        //       stream from within a policy callback.
        //
        /// <summary>
        /// The list of RSA key pairs, on a per-interpreter basis, that were
        /// used when approving a script, file, or stream from within a policy
        /// callback.
        /// </summary>
        private static readonly InterpreterObjectDictionary keyPairs =
            new InterpreterObjectDictionary();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the list of locks, on a per-interpreter basis,
        //       that are used to prevent the RemoveAllApproved method
        //       from actually removing any key pairs.
        //
        /// <summary>
        /// The list of locks, on a per-interpreter basis, that are used to
        /// prevent the RemoveAllApproved method from actually removing any
        /// key pairs.
        /// </summary>
        private static readonly InterpreterObjectDictionary locks =
            new InterpreterObjectDictionary();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Gets the dictionary of approved key pairs associated with the
        /// specified interpreter, optionally creating it if it does not
        /// already exist.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being queried.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the dictionary if it does not already exist.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the
        /// dictionary could not be returned.
        /// </param>
        /// <returns>
        /// The dictionary of approved key pairs for the interpreter, or null
        /// if it could not be returned.
        /// </returns>
        private static ObjectKeyPairDictionary GetAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            bool create,             /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return null;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyPairs == null)
                {
                    error = "key pairs not available";
                    return null;
                }

                ObjectKeyPairDictionary dictionary = null;
                object value;

                if (keyPairs.TryGetValue(interpreter, out value))
                {
                    dictionary = value as ObjectKeyPairDictionary;
                }
                else if (create)
                {
                    dictionary = new ObjectKeyPairDictionary(
                        new Comparers.Object());

                    keyPairs.Add(interpreter, dictionary);
                }

                if (dictionary == null)
                    error = "no key pairs for interpreter";

                return dictionary;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Gets the approved key pair associated with the specified object for
        /// the given interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being queried.
        /// </param>
        /// <param name="object">
        /// The object whose associated key pair is being queried.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the key
        /// pair could not be returned.
        /// </param>
        /// <returns>
        /// The key pair associated with the object, or null if none was found.
        /// </returns>
        private static IKeyPair GetApproved(
            Interpreter interpreter, /* in */
            object @object,          /* in */
            ref Result error         /* out */
            )
        {
            if (@object == null)
            {
                error = "invalid object";
                return null;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                ObjectKeyPairDictionary dictionary = GetAllApproved(
                    interpreter, true, ref error);

                if (dictionary == null)
                    return null;

                IKeyPair keyPair;

                if (!dictionary.TryGetValue(@object, out keyPair))
                    return null;

                return keyPair;
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the number of approved key pairs for the specified interpreter
        /// to the supplied count.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being counted.
        /// </param>
        /// <param name="count">
        /// Receives the running total of approved key pairs, incremented by
        /// the number found for the interpreter.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the key
        /// pairs could not be counted.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        private static ReturnCode CountAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            ref int count,           /* in, out */
            ref Result error         /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                ObjectKeyPairDictionary dictionary = GetAllApproved(
                    interpreter, false, ref error);

                if (dictionary == null)
                    return ReturnCode.Error;

#if DEBUG || FORCE_TRACE
                DebugOnlyOps.DumpKeyPairs(interpreter,
                    "CountAllApproved", null, dictionary.Values,
                    typeof(CertificateKeyPairState).Name,
                    PolicyType.Unknown, TracePriority.High);
#endif

                count += dictionary.Count;
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the approved key pairs for the specified interpreter as
        /// locked, preventing them from being removed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being locked.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the key
        /// pairs could not be locked.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        private static ReturnCode LockAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (locks == null)
                {
                    error = "locks not available";
                    return ReturnCode.Error;
                }

                if (locks.ContainsKey(interpreter))
                {
                    error = "lock already present";
                    return ReturnCode.Error;
                }

                locks.Add(interpreter, null);
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the lock on the approved key pairs for the specified
        /// interpreter, allowing them to be removed again.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being unlocked.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the key
        /// pairs could not be unlocked.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        private static ReturnCode UnlockAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (locks == null)
                {
                    error = "locks not available";
                    return ReturnCode.Error;
                }

                if (!locks.ContainsKey(interpreter))
                {
                    error = "lock not present";
                    return ReturnCode.Error;
                }

                if (!locks.Remove(interpreter))
                {
                    error = "lock not removed";
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all approved key pairs for the specified interpreter,
        /// unless they are currently locked.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being removed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the key
        /// pairs could not be removed.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        private static ReturnCode RemoveAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((locks != null) &&
                    locks.ContainsKey(interpreter))
                {
                    error = "key pairs are locked";
                    return ReturnCode.Error;
                }

                if (keyPairs == null)
                {
                    error = "key pairs not available";
                    return ReturnCode.Error;
                }

                if (!keyPairs.Remove(interpreter))
                {
                    error = "key pair not removed";
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds an approved key pair, associated with the specified object, to
        /// the approved key pairs for the given interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to which the approved key pair is being added.
        /// </param>
        /// <param name="object">
        /// The object that the key pair is being associated with.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to associate with the object.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the key
        /// pair could not be added.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        private static ReturnCode AddApproved( /* CORE? */
            Interpreter interpreter, /* in */
            object @object,          /* in */
            IKeyPair keyPair,        /* in */
            ref Result error         /* out */
            )
        {
            if (@object == null)
            {
                error = "invalid object";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                ObjectKeyPairDictionary dictionary = GetAllApproved(
                    interpreter, true, ref error);

                if (dictionary == null)
                    return ReturnCode.Error;

                if (dictionary.ContainsKey(@object))
                {
                    error = "key pair already exists";
                    return ReturnCode.Error;
                }

                dictionary.Add(@object, keyPair);
                return ReturnCode.Ok;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Adds the number of approved key pairs for the specified interpreter
        /// to the supplied count, optionally complaining if the operation
        /// fails.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being counted.
        /// </param>
        /// <param name="noComplain">
        /// Non-zero to suppress complaining when the operation fails.
        /// </param>
        /// <param name="count">
        /// Receives the running total of approved key pairs, incremented by
        /// the number found for the interpreter.
        /// </param>
        /// <returns>
        /// Non-zero if the approved key pairs were counted successfully.
        /// </returns>
        public static bool CountAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            bool noComplain,         /* in */
            ref int count            /* in, out */
            )
        {
            ReturnCode code;
            Result error = null;

            code = CountAllApproved(interpreter, ref count, ref error);

            if ((code != ReturnCode.Ok) && !noComplain)
                Utility.Complain(interpreter, code, error);

            return (code == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the approved key pairs for the specified interpreter as
        /// locked, optionally complaining if the operation fails.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being locked.
        /// </param>
        /// <param name="noComplain">
        /// Non-zero to suppress complaining when the operation fails.
        /// </param>
        /// <returns>
        /// Non-zero if the approved key pairs were locked successfully.
        /// </returns>
        public static bool LockAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            bool noComplain          /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = LockAllApproved(interpreter, ref error);

            if ((code != ReturnCode.Ok) && !noComplain)
                Utility.Complain(interpreter, code, error);

            return (code == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the lock on the approved key pairs for the specified
        /// interpreter, optionally complaining if the operation fails.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being unlocked.
        /// </param>
        /// <param name="noComplain">
        /// Non-zero to suppress complaining when the operation fails.
        /// </param>
        /// <returns>
        /// Non-zero if the approved key pairs were unlocked successfully.
        /// </returns>
        public static bool UnlockAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            bool noComplain          /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = UnlockAllApproved(interpreter, ref error);

            if ((code != ReturnCode.Ok) && !noComplain)
                Utility.Complain(interpreter, code, error);

            return (code == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all approved key pairs for the specified interpreter,
        /// optionally complaining if the operation fails.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being removed.
        /// </param>
        /// <param name="noComplain">
        /// Non-zero to suppress complaining when the operation fails.
        /// </param>
        /// <returns>
        /// Non-zero if the approved key pairs were removed successfully.
        /// </returns>
        public static bool RemoveAllApproved( /* CORE? */
            Interpreter interpreter, /* in */
            bool noComplain          /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = RemoveAllApproved(interpreter, ref error);

            if ((code != ReturnCode.Ok) && !noComplain)
                Utility.Complain(interpreter, code, error);

            return (code == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds an approved key pair, associated with the specified object, to
        /// the approved key pairs for the given interpreter, optionally
        /// complaining if the operation fails.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to which the approved key pair is being added.
        /// </param>
        /// <param name="object">
        /// The object that the key pair is being associated with.
        /// </param>
        /// <param name="keyPair">
        /// The key pair to associate with the object.
        /// </param>
        /// <param name="noComplain">
        /// Non-zero to suppress complaining when the operation fails.
        /// </param>
        /// <returns>
        /// Non-zero if the approved key pair was added successfully.
        /// </returns>
        public static bool AddApproved( /* CORE? */
            Interpreter interpreter, /* in */
            object @object,          /* in */
            IKeyPair keyPair,        /* in */
            bool noComplain          /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = AddApproved(
                interpreter, @object, keyPair, ref error);

            if ((code != ReturnCode.Ok) && !noComplain)
                Utility.Complain(interpreter, code, error);

            return (code == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Retrieves and removes the approved key pair associated with the
        /// specified object for the given interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose approved key pairs are being queried.
        /// </param>
        /// <param name="object">
        /// The object whose associated key pair is being taken.
        /// </param>
        /// <param name="keyPair">
        /// Upon success, receives the key pair that was associated with the
        /// object.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the key
        /// pair could not be taken.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, an appropriate
        /// error code.
        /// </returns>
        public static ReturnCode TakeApproved( /* CORE? */
            Interpreter interpreter, /* in */
            object @object,          /* in */
            ref IKeyPair keyPair,    /* out */
            ref Result error         /* out */
            )
        {
            if (@object == null)
            {
                error = "invalid object";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                ObjectKeyPairDictionary dictionary = GetAllApproved(
                    interpreter, false, ref error);

                if (dictionary == null)
                    return ReturnCode.Error;

                IKeyPair localKeyPair;

                if (!dictionary.TryGetValue(@object, out localKeyPair))
                {
                    error = "key pair not found";
                    return ReturnCode.Error;
                }

                dictionary.Remove(@object);
                keyPair = localKeyPair;

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a value indicating whether expiration date and revocation
        /// checking for policies requires network access.
        /// </summary>
        /// <returns>
        /// Non-zero if network access is required for policy checking.
        /// </returns>
        public static bool GetForceNetwork() /* CORE? */
        {
            lock (syncRoot)
            {
                return forceNetwork;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets a value indicating whether expiration date and revocation
        /// checking for policies requires network access.
        /// </summary>
        /// <param name="forceNetwork">
        /// Non-zero if network access should be required for policy checking.
        /// </param>
        public static void SetForceNetwork( /* CORE? */
            bool forceNetwork /* in */
            )
        {
            lock (syncRoot)
            {
                This.forceNetwork = forceNetwork;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Cleanup Methods
        /// <summary>
        /// Counts the approved key pairs belonging to interpreters that have
        /// been disposed, appending a summary to the supplied builder.
        /// </summary>
        /// <param name="priority">
        /// The trace priority to use when emitting diagnostic information.
        /// </param>
        /// <param name="builder">
        /// Receives the appended summary text describing the counted key
        /// pairs; created if necessary.
        /// </param>
        /// <param name="totalCount">
        /// Receives the running total, incremented by the number of approved
        /// key pairs that were counted.
        /// </param>
        public static void MaybeCountAll(
            TracePriority priority,    /* in */
            ref StringBuilder builder, /* in, out */
            ref int totalCount         /* in, out */
            ) /* CORE? */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyPairs == null)
                    return;

                int count = 0;

                foreach (KeyPairPair pair in keyPairs)
                {
                    Interpreter interpreter =
                        pair.Key as Interpreter;

                    if (interpreter == null)
                        continue;

                    if (!interpreter.Disposed)
                        continue;

                    ObjectKeyPairDictionary dictionary =
                        pair.Value as ObjectKeyPairDictionary;

                    if (dictionary != null)
                    {
#if DEBUG || FORCE_TRACE
                        DebugOnlyOps.DumpKeyPairs(interpreter,
                            String.Format("MaybeCountAll({0})",
                            CertificateDataOps.FormatInterpreter(
                                interpreter, true, true)),
                            null, dictionary.Values,
                            typeof(CertificateKeyPairState).Name,
                            PolicyType.Unknown, priority);
#endif

                        count += dictionary.Count;
                    }
                }

                if (count > 0)
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.AppendFormat(
                        "approvedKeyPairs(interpreters, {0})", count);

                    totalCount += count;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the approved key pairs belonging to interpreters that have
        /// been disposed, appending a summary to the supplied builder.
        /// </summary>
        /// <param name="builder">
        /// Receives the appended summary text describing the removed key
        /// pairs; created if necessary.
        /// </param>
        /// <param name="totalCount">
        /// Receives the running total, incremented by the number of approved
        /// key pair entries that were removed.
        /// </param>
        public static void MaybeCleanupAll(
            ref StringBuilder builder, /* in, out */
            ref int totalCount         /* in, out */
            ) /* CORE? */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyPairs == null)
                    return;

                int count = 0;

                InterpreterList keys = new InterpreterList(
                    keyPairs.Keys);

                foreach (IInterpreter interpreter in keys)
                {
                    if (interpreter == null)
                        continue;

                    if (!interpreter.Disposed)
                        continue;

                    if (keyPairs.Remove(interpreter))
                        count++;
                }

                if (count > 0)
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.AppendFormat(
                        "approvedKeyPairs({0})", count);

                    totalCount += count;
                }
            }
        }
        #endregion
    }
}
