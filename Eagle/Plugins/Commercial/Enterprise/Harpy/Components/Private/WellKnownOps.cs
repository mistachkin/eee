/*
 * WellKnownOps.cs --
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
using TraceOps = Licensing.Components.Private.CertificateTraceOps;

using SaltAndPasswordPair = Eagle._Components.Public.AnyPair<
    System.Guid, string>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper routines that publish well-known salt and password
    /// pairs into an application domain so that encrypted configuration
    /// script files can later be located and decrypted.
    /// </summary>
    [ObjectId("320bdde1-3aef-4eaf-bdc5-d5cd3b4ea273")]
    internal static class WellKnownOps
    {
        /// <summary>
        /// Populates the supplied application domain with the hard-coded set
        /// of salt and password pairs used to decrypt the "default"
        /// configuration script files that may be shipped in encrypted form.
        /// Each pair is stored as application domain data keyed by its salt
        /// value and the current process identifier.  If
        /// <paramref name="appDomain" /> is null, no action is taken.
        /// </summary>
        /// <param name="appDomain">
        /// The application domain to receive the salt and password pair data.
        /// </param>
        public static void SetupConfigurationData( /* CORE */
            AppDomain appDomain /* in */
            )
        {
            if (appDomain != null)
            {
                //
                // HACK: Hard-coded the password and salt for the
                //       encrypted NuGet configuration script file
                //       here.  This will be necessary if we want
                //       any of the "default" configuration script
                //       files to be shipped in encrypted form.
                //
                // WARNING: DO NOT REMOVE any of the salt / password
                //          pairs listed here without prior approval
                //          from one of the project owners.
                //
                // NOTE: All of these strings should end up being
                //       obfuscated when this assembly is compiled
                //       for release (via whatever configured code
                //       obfuscation tool is in use).
                //
                SaltAndPasswordPair[] anyPairs = {
                    /* Mistachkin Systems */
                    /**************************************************
                     * If somebody is actually reading this, they may *
                     * also want to know what these values are based  *
                     * on.  The salt value is the Z-card value within *
                     * the Fossil manifest associated with the first  *
                     * Fossil check-in for The Eagle Project.  The    *
                     * password value is the SHA1 hash value for the  *
                     * "lib/Eagle1.0/init.eagle" file from that same  *
                     * check-in. -- Joe Mistachkin, 2024-04-06        *
                     **************************************************/
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "38762f6e-1dba-c842-3fff-a7693e20c2c8"),
                        "43C7267045E56064EFE277306A243C8A3CBF8829"),
                    /* Mistachkin Solutions LLC */
                    /* System.Data.SQLite Enterprise Edition */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "a8507da0-3c21-4405-a5b7-6f8b0d06c4a5"),
                        "DE81481D7034048C67A062D602E19154A95C767F"),
                    /* Eyrie Solutions */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "40cd39ae-af74-7843-a16e-f39213790c1e"),
                        "BCF2E2477DDA6A144B057EB022D96893C5D5CC67"),
                    /* Mistachkin Solutions LLC */
                    /* System.Data.SQLite with SQLite Encryption Extension */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "21499d79-e39e-4c6e-8b8e-24794863f11b"),
                        "EB6C883120F3D5336F99C34DF2FB4863"),
                    /* Hipp, Wyrick, & Company, Inc */
                    /* System.Data.SQLite with SQLite Encryption Extension */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "6cbf2e98-2cdc-4c20-9f0a-d15327531a83"),
                        "8FFDCDCFF172FF3D1537F8E6F332455B"),
                    /* Eagle Development Team */
                    /* FOR ENGINEERING USE ONLY: "certificate.exml", */
                    /* is shared with the managed SDK "LicenseOps.cs" */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "c3e6f922-5b39-4a8b-b43c-18c74f00571b"),
                        "36EEAAFE585DCF682D06A3EC02C23589"),
                    /* NO NAME */
                    /* FOR CORE LIBRARY USE ONLY: "certificate.exml", */
                    /* is shared with the managed SDK "LicenseOps.cs" */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "a30a1ea5-33fc-499b-a615-93e273ff8abb"),
                        "1DEACD249D1C97CC9EB8B53E0012A827"),
                    /* "Harpy.v1.NuGet.eeagle" */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "c7925879-0606-442d-9989-b2e12f44d217"),
                        "5E4F6BCE074E78C0AEB0838F109950BE"),
                    /* Shared with LicenseOps SDK */
                    /* "*.v1.*.eeagle" */
                    /* PLEASE DO NOT MODIFY OR SOME THINGS MAY BREAK */
                    new SaltAndPasswordPair(new Guid(
                        "0d22343f-b7d4-4de4-b616-61d2c65fe50f"),
                        "81EF79920647EEE0134DA28BDAFEF107"),
                    /* RESERVED FOR FUTURE USE */
                    new SaltAndPasswordPair(new Guid(
                        "05580ddf-4d0f-477c-9118-4926c693de28"),
                        "AD67BDED1D15BBF0CABC29939D2183D5"),
                    /* RESERVED FOR FUTURE USE */
                    new SaltAndPasswordPair(new Guid(
                        "fccee498-1664-4195-8e84-c0375110be30"),
                        "E2AFC819525A0FA614706D693F9D76B5"),
                    /* RESERVED FOR FUTURE USE */
                    new SaltAndPasswordPair(new Guid(
                        "4bb03047-b21a-4937-804e-accff4e8fd0e"),
                        "217C214B2055E71F6BA7303E99870D24"),
                    /* RESERVED FOR FUTURE USE */
                    new SaltAndPasswordPair(new Guid(
                        "acbc9199-8301-4c27-ae27-e359ba0f5298"),
                        "7B9536EF23628FE4B55F3C7B2A1CB837"),
                    /* RESERVED FOR FUTURE USE */
                    new SaltAndPasswordPair(new Guid(
                        "e9fac691-16a8-44f0-a1d6-19e9fef5a3f4"),
                        "D7C582D09391F98FA844AED0ADA8BC15"),
                    /* RESERVED FOR FUTURE USE */
                    new SaltAndPasswordPair(new Guid(
                        "386c4971-7e6b-4edb-84e1-8e4254312029"),
                        "8DCCD79B227D519B71ABE77AED159B29"),
                    /* RESERVED FOR FUTURE USE */
                    new SaltAndPasswordPair(new Guid(
                        "9640de10-78b8-4e3c-b8e8-e9baaa5e3859"),
                        "83422050F7590FA0F702D0BB5761E22C"),
                    /* RESERVED FOR FUTURE USE */
                    new SaltAndPasswordPair(new Guid(
                        "187a7e93-2444-49b0-91d1-79193ebf3561"),
                        "213952C6A2D8B010083737D37535295B")
                };

                int count = 0;

                foreach (SaltAndPasswordPair anyPair in anyPairs)
                {
                    if (anyPair == null)
                        continue;

                    appDomain.SetData(String.Format(
                        Constants.GetDataFormat, anyPair.X.ToString(),
                        Utility.GetCurrentProcessId()), anyPair.Y);

                    count++;
                }

#if DEBUG || FORCE_TRACE
                TraceOps.DebugTrace(String.Format(
                    "SetupConfigurationData: count = {0}",
                    count), typeof(WellKnownOps).Name,
                    TracePriority.High);
#endif
            }
        }
    }
}
