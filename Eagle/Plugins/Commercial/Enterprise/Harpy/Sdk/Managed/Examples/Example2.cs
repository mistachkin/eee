/*
 * Example2.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Official Early-Bound Certificate Validation & Verification API Example
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Licensing.Components.Public;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;

namespace Example2
{
    /// <summary>
    /// This is a console-mode program that demonstrates how to use the Harpy
    /// "early-bound" licensing SDK in order to validate and verify a license
    /// certificate against a given assembly.
    /// </summary>
    class Program
    {
        /// <summary>
        /// This is the entry point for this program.  It performs the basic
        /// actions necessary to validate and verify the license certificate
        /// associated with this program.  The code is designed to be easily
        /// adapted for use with any program that requires license checking.
        /// </summary>
        /// <param name="args">
        /// The command line arguments to the program; currently, these are
        /// completely ignored.
        /// </param>
        /// <returns>
        /// Zero if the license certificate is successfully validated and
        /// verified; otherwise, non-zero.
        /// </returns>
        static int Main(
            string[] args /* in */
            )
        {
            ///////////////////////////////////////////////////////////
            //    OPTIONAL: SPECIFY LICENSE CERTIFICATE FILE NAME    //
            ///////////////////////////////////////////////////////////

            //
            // NOTE: Build the fully qualified file name for the license
            //       certificate associated with this program.  This may
            //       be set to null to enable automatic detection of the
            //       license certificate file name.
            //
            string fileName = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Example2.xml");

            ///////////////////////////////////////////////////////////
            //          REQUIRED: PERFORM THE LICENSE CHECK          //
            ///////////////////////////////////////////////////////////

            //
            // NOTE: Call the simplest of the early-bound licensing SDK
            //       entry points.  It will create the necessary Eagle
            //       interpreter context automatically.
            //
            ReturnCode code;
            ICertificate certificate = null;
            Result result = null;

            ILicenseManager licenseManager = new LicenseManager();

            code = licenseManager.VerifyCertificate(
                null, typeof(Program).Assembly, null, null, null, null,
                null, null, null, null, null, null, null, false, true,
                true, null, null, null, ref fileName, ref certificate,
                ref result);

            //
            // NOTE: *IMPORTANT* This is the critical check.  If the
            //       return code from the license checking operation
            //       above is "Ok", then this program is considered
            //       to be licensed fully and properly; otherwise,
            //       an error was encountered and the program cannot
            //       be considered to be "licensed" (i.e. it should
            //       emit an appropriate error message and abort).
            //
            if (code == ReturnCode.Ok)
            {
                ///////////////////////////////////////////////////////
                //  BEGIN OPTIONAL: HANDLE LICENSE CHECKING SUCCESS  //
                ///////////////////////////////////////////////////////

                //
                // NOTE: This (completely optional) block of code is
                //       used to construct some human-readable output
                //       from the detailed license checking results.
                //
                StringPairList list = new StringPairList();

                //
                // NOTE: First, add the resulting license certificate
                //       file name.  This parameter to the license SDK
                //       call was technically in/out; however, it will
                //       [almost] always have an output value that is
                //       functionally identical to the original input
                //       value upon if that input value was not null.
                //
                if (fileName != null)
                    list.Add("fileName", fileName);

                //
                // NOTE: Next, add the detailed license certificate
                //       information, if it is available.  The precise
                //       format of string returned by this method is
                //       officially "unspecified", except that it will
                //       always contain at least enough data to uniquely
                //       identify the license certificate.  Typically,
                //       it will contain a [dictionary formatted] list
                //       of name/value pairs with the detailed license
                //       certificate information.
                //
                if (certificate != null)
                    list.Add("certificate", certificate.ToString());

                //
                // NOTE: Next, add the overall textual result of the
                //       license SDK call.  This will almost always
                //       be the literal string "VerifiedOk".
                //
                if (result != null)
                    list.Add("result", result);

                //
                // NOTE: Finally, emit all the collected information
                //       to the console.
                //
                foreach (StringPair pair in list)
                {
                    Console.WriteLine("{0} = {1}{2}",
                        Utility.FormatWrapOrNull(pair.X),
                        Utility.FormatWrapOrNull(pair.Y),
                        Environment.NewLine);
                }

                ///////////////////////////////////////////////////////
                //   END OPTIONAL: HANDLE LICENSE CHECKING SUCCESS   //
                ///////////////////////////////////////////////////////
            }
            else
            {
                ///////////////////////////////////////////////////////
                //  BEGIN OPTIONAL: HANDLE LICENSE CHECKING FAILURE  //
                ///////////////////////////////////////////////////////

                //
                // NOTE: This (completely optional) block of code is
                //       used to perform error handling for a failed
                //       license check.  In this example, the error
                //       message is emitted to the console.
                //
                Console.WriteLine(Utility.FormatResult(code, result));

                ///////////////////////////////////////////////////////
                //   END OPTIONAL: HANDLE LICENSE CHECKING FAILURE   //
                ///////////////////////////////////////////////////////
            }

            ///////////////////////////////////////////////////////////
            //         OPTIONAL: POST-LICENSE CHECKING CODE          //
            ///////////////////////////////////////////////////////////

            //
            // NOTE: Finally, return exit code from this program that
            //       conveys the overall result (i.e. success/failure)
            //       of the license checking.
            //
            return (int)Utility.ReturnCodeToExitCode(code);
        }
    }
}
