/*
 * HookSelfTest.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Reflection;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using _Test = Eagle._Tests.Default;
using _Arch = Eagle._Components.Public.ProcessorArchitecture;

namespace Zeus.Components.Private
{
    /// <summary>
    /// Provides a built-in self-test of the CLR method hooking engine.  It
    /// hooks a known test method so it returns a fixed character, exercises
    /// it before, during, and after the hook, and compares the observed
    /// results against the expected sequence to confirm that hooking and
    /// unhooking work correctly on the current platform.
    /// </summary>
    [ObjectId("16e52c4b-ef85-4d25-8c37-70cb987925d0")]
    internal static class HookSelfTest
    {
        #region Private Constants
        /// <summary>
        /// The name of the original test method that is hooked.
        /// </summary>
        private const string OldMethodName = "TestCharacterMethod";

        /// <summary>
        /// The name of the replacement method that the original is hooked to
        /// call.
        /// </summary>
        private const string NewMethodName = "NewTestCharacterMethod";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The sequence of character results expected over the course of the
        /// self-test (before, during, and after the hook is in place).
        /// </summary>
        private static readonly StringList expectedList = new StringList(
            Characters.A.ToString(), Characters.Z.ToString(),
            Characters.M.ToString(), Characters.M.ToString(),
            Characters.A.ToString(), Characters.Z.ToString()
        );
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// The replacement method installed by the self-test; it ignores its
        /// argument and always returns a fixed character, so that the effect
        /// of the hook can be observed.
        /// </summary>
        /// <param name="x">
        /// The input character; not used by this implementation.
        /// </param>
        /// <returns>
        /// A fixed character used to detect that the hook is active.
        /// </returns>
        private static char NewTestCharacterMethod(
            char x /* in: NOT USED */
            )
        {
            //
            // NOTE: This replacement is intentionally a static method that
            //       ignores its argument and returns a constant.  The hooked
            //       target (TestCharacterMethod) is an instance method, so
            //       the native signatures differ by the implicit "this"
            //       parameter; ignoring all arguments and returning a
            //       constant keeps the self-test valid regardless of the
            //       calling convention, because it verifies only that
            //       control is redirected -- not that arguments marshal
            //       through the patched entry point.
            //
            return Characters.M;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Gets a copy of the sequence of character results expected over the
        /// course of the self-test.
        /// </summary>
        /// <returns>
        /// A new copy of the expected result list, or null when none is
        /// available.
        /// </returns>
        public static StringList GetExpectedList()
        {
            return (expectedList != null) ?
                new StringList(expectedList) : null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the hooking self-test.  A known test method is invoked,
        /// then hooked to a replacement method and invoked again, then
        /// unhooked and invoked once more; the accumulated results are
        /// compared against the expected list, if one is supplied.  The
        /// original method is always unhooked before returning.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to report any complaint raised while
        /// unhooking during cleanup.
        /// </param>
        /// <param name="wantList">
        /// The expected sequence of results, or null to simply return the
        /// observed results instead of comparing them.
        /// </param>
        /// <param name="maximumFollow">
        /// The maximum number of jump-stubs to follow when locating the
        /// method to hook.
        /// </param>
        /// <param name="patchFlags">
        /// The flags controlling how the method is patched.
        /// </param>
        /// <param name="result">
        /// Upon return, receives "PASSED" or the observed results on success,
        /// or an error message describing the mismatch or failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode PerformTest(
            Interpreter interpreter, /* in */
            StringList wantList,     /* in */
            int maximumFollow,       /* in */
            PatchFlags patchFlags,   /* in */
            ref Result result        /* out */
            )
        {
            HookOps.Initialize(false);

            //
            // NOTE: This self-test patches the shared TestCharacterMethod in
            //       place, so it must not be run concurrently with itself or
            //       with any other hook of that method; the caller is
            //       responsible for serializing it (see the note on
            //       HookOps.Write regarding concurrent patching).
            //
            using (_Test test = new _Test())
            {
                IClientData clientData = null;

                try
                {
                    MethodInfo oldMethod = null;
                    MethodInfo newMethod = null;

                    char one = Characters.A;
                    char two = Characters.Z;

                    StringList haveList = new StringList();

                    haveList.Add(test.TestCharacterMethod(one).ToString());
                    haveList.Add(test.TestCharacterMethod(two).ToString());

                    oldMethod = typeof(_Test).GetMethod(
                        OldMethodName, BindingFlags.Public |
                        BindingFlags.Instance);

                    if (oldMethod == null)
                    {
                        result = String.Format(
                            "old method \"{0}\" not found", OldMethodName);

                        return ReturnCode.Error;
                    }

                    newMethod = typeof(HookSelfTest).GetMethod(
                        NewMethodName, BindingFlags.NonPublic |
                        BindingFlags.Static);

                    if (newMethod == null)
                    {
                        result = String.Format(
                            "new method \"{0}\" not found", NewMethodName);

                        return ReturnCode.Error;
                    }

                    if (HookOps.Start(
                            oldMethod, newMethod, _Arch.Unknown,
                            maximumFollow, patchFlags, ref clientData,
                            ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    haveList.Add(test.TestCharacterMethod(one).ToString());
                    haveList.Add(test.TestCharacterMethod(two).ToString());

                    if (HookOps.Stop(
                            ref clientData, ref result) != ReturnCode.Ok)
                    {
                        return ReturnCode.Error;
                    }

                    haveList.Add(test.TestCharacterMethod(one).ToString());
                    haveList.Add(test.TestCharacterMethod(two).ToString());

                    if (wantList != null)
                    {
                        int haveLength = haveList.Count;
                        int wantLength = wantList.Count;

                        if (haveLength == wantLength)
                        {
                            for (int index = 0; index < haveLength; index++)
                            {
                                if (!Utility.SystemStringEquals(
                                        haveList[index], wantList[index]))
                                {
                                    result = String.Format(
                                        "bad list item at index {0}, " +
                                        "have {1}, want {2}", index,
                                        Utility.FormatWrapOrNull(
                                            haveList[index]),
                                        Utility.FormatWrapOrNull(
                                            wantList[index]));

                                    return ReturnCode.Error;
                                }
                            }

                            result = "PASSED";
                        }
                        else
                        {
                            result = String.Format(
                                "bad list count, have {0}, want {1}",
                                haveLength, wantLength);

                            return ReturnCode.Error;
                        }
                    }
                    else
                    {
                        result = haveList;
                    }

                    return ReturnCode.Ok;
                }
                finally
                {
                    if (clientData != null)
                    {
                        ReturnCode unhookCode;
                        Result unhookError = null;

                        unhookCode = HookOps.Stop(
                            ref clientData, ref unhookError);

                        if (unhookCode != ReturnCode.Ok)
                        {
                            Utility.Complain(
                                interpreter, unhookCode, unhookError);
                        }
                    }
                }
            }
        }
        #endregion
    }
}
