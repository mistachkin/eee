/*
 * Security.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Official Runtime Certificate Security & Anti-Hacking API
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !SECURITY
#error "This file cannot be compiled or used properly with security support disabled."
#endif

using System;
using System.Diagnostics;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;

namespace Licensing.Sdk.Private
{
    ///////////////////////////////////////////////////////////////////////////
    // *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*
    //
    // Please DO NOT modify this file.  Modifications to this file may cause
    // the functionality contained within it to malfunction, which may cause
    // the application itself to be completely non-functional.
    //
    // *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING*
    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Provides the managed SDK's security bootstrap: the embedded policy
    /// scripts plus the helpers that enable, configure, and optionally relax
    /// the security sandbox used when loading licensed plugins. As the
    /// warning above notes, this file is intentionally not meant to be
    /// modified.
    /// </summary>
    [ObjectId("3c25f2f6-fc08-4cb4-82c0-a8364a6a539a")]
    internal static class Security
    {
        #region Private Constants
        /// <summary>
        /// This script fragment is used to define the procedure that loads
        /// the necessary plugins, optionally in isolated mode.
        /// </summary>
        private const string Script1 = @"
bmFtZXNwYWNlIGV2YWwgOjpFYWdsZSB7cHJvYyBlbmFibGVTZWN1cml0eSB7e2lzb2xhdGUgdHJ1
ZX0ge3BhY2thZ2VQYXRoICIifSB7Y2VydGlmaWNhdGVGaWxlTmFtZSAiIn19IHtpZiB7JGlzb2xh
dGV9IHRoZW4ge2NhdGNoIHtvYmplY3QgaW52b2tlIC1mbGFncyArTm9uUHVibGljIEludGVycHJl
dGVyLkdldEFjdGl2ZSBFbmFibGVQbHVnaW5Jc29sYXRpb259fTsgaWYge1tzdHJpbmcgbGVuZ3Ro
ICRwYWNrYWdlUGF0aF0gPiAwfSB0aGVuIHtwYWNrYWdlIHNjYW4gLWhvc3QgLXBsdWdpbiAtbm9y
bWFsIC1yZWZyZXNoIC1yZWN1cnNpdmUgLS0gJHBhY2thZ2VQYXRofTsgaWYge1tzdHJpbmcgbGVu
Z3RoICRjZXJ0aWZpY2F0ZUZpbGVOYW1lXSA+IDB9IHRoZW4ge3NldCBwdWJsaWNLZXlUb2tlbiA4
YmY0M2I0NzQ5ZTQ2YTBiOyBvYmplY3QgaW52b2tlIEludGVycHJldGVyLkdldEFjdGl2ZSBBZGRQ
bHVnaW5Bcmd1bWVudHMgW2FwcGVuZEFyZ3MgIlNlY3VyaXR5LkNvcmUsIEhhcnB5KiwgKiwgUHVi
bGljS2V5VG9rZW49IiAkcHVibGljS2V5VG9rZW5dICRjZXJ0aWZpY2F0ZUZpbGVOYW1lOyBvYmpl
Y3QgaW52b2tlIEludGVycHJldGVyLkdldEFjdGl2ZSBBZGRQbHVnaW5Bcmd1bWVudHMgW2FwcGVu
ZEFyZ3MgIkxpY2Vuc2luZy5Db3JlLCBIYXJweSosICosIFB1YmxpY0tleVRva2VuPSIgJHB1Ymxp
Y0tleVRva2VuXSAkY2VydGlmaWNhdGVGaWxlTmFtZTsgb2JqZWN0IGludm9rZSBJbnRlcnByZXRl
ci5HZXRBY3RpdmUgQWRkUGx1Z2luQXJndW1lbnRzIFthcHBlbmRBcmdzICJMaWNlbnNpbmcuU3Rh
bmRhcmQsIEhhcnB5KiwgKiwgUHVibGljS2V5VG9rZW49IiAkcHVibGljS2V5VG9rZW5dICRjZXJ0
aWZpY2F0ZUZpbGVOYW1lOyBvYmplY3QgaW52b2tlIEludGVycHJldGVyLkdldEFjdGl2ZSBBZGRQ
bHVnaW5Bcmd1bWVudHMgW2FwcGVuZEFyZ3MgIkxpY2Vuc2luZy5FbnRlcnByaXNlLCBIYXJweSos
ICosIFB1YmxpY0tleVRva2VuPSIgJHB1YmxpY0tleVRva2VuXSAkY2VydGlmaWNhdGVGaWxlTmFt
ZTsgb2JqZWN0IGludm9rZSBJbnRlcnByZXRlci5HZXRBY3RpdmUgQWRkUGx1Z2luQXJndW1lbnRz
IFthcHBlbmRBcmdzICJTZWN1cml0eS5DZXJ0aWZpY2F0ZXMsIEJhZGdlKiwgKiwgUHVibGljS2V5
VG9rZW49IiAkcHVibGljS2V5VG9rZW5dICRjZXJ0aWZpY2F0ZUZpbGVOYW1lOyBvYmplY3QgaW52
b2tlIEludGVycHJldGVyLkdldEFjdGl2ZSBBZGRQbHVnaW5Bcmd1bWVudHMgW2FwcGVuZEFyZ3Mg
IkJhZGdlLkVudGVycHJpc2UsIEJhZGdlKiwgKiwgUHVibGljS2V5VG9rZW49IiAkcHVibGljS2V5
VG9rZW5dICRjZXJ0aWZpY2F0ZUZpbGVOYW1lfTsgcGFja2FnZSByZXF1aXJlIFNlY3VyaXR5LkNv
cmU7IHNlY3VyaXR5IHRydWU7IGtleXJpbmcgYm9vdHN0cmFwOyBjYXRjaCB7cmVuYW1lIGNlcnRp
ZmljYXRlICIifTsgY2F0Y2gge3JlbmFtZSBjcnlwdG9ncmFwaHkgIiJ9OyBjYXRjaCB7cmVuYW1l
IGZsYWdzICIifTsgY2F0Y2gge3JlbmFtZSBoYXJweSAiIn07IGNhdGNoIHtyZW5hbWUga2V2YWwg
IiJ9OyBjYXRjaCB7cmVuYW1lIGtleXBhaXIgIiJ9OyBjYXRjaCB7cmVuYW1lIGtleXJpbmcgIiJ9
OyBjYXRjaCB7cmVuYW1lIGtzb3VyY2UgIiJ9OyBjYXRjaCB7cmVuYW1lIHNlY3JldCAiIn07IGNh
dGNoIHtyZW5hbWUgc2VjdXJpdHkgIiJ9OyBjYXRjaCB7cmVuYW1lIHN0b3JhZ2UgIiJ9OyBjYXRj
aCB7cmVuYW1lIHN1cHBvcnQgIiJ9OyBwYWNrYWdlIHJlcXVpcmUgU2VjdXJpdHkuQ2VydGlmaWNh
dGVzOyByZXR1cm4gdHJ1ZX19
";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This script fragment is used to invoke the procedure that loads
        /// the necessary plugins, optionally in isolated mode.  The primary
        /// certificate file name (i.e. for Harpy itself) is always provided
        /// to this script fragment.  The boolean isolated flag is also
        /// provided to this script fragment. This script fragment also
        /// deletes the procedure defined by the script fragment "Script1".
        /// </summary>
        private static readonly string Script2 = @"
ZXZhbCBlbmFibGVTZWN1cml0eSB7MH07IHJlbmFtZSBlbmFibGVTZWN1cml0eSB7MX17Mn07
";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This script fragment adjusts the interpreter plugin flags to
        /// disable loading plugins in isolated mode.
        /// </summary>
        private static readonly string Script3 = @"
b2JqZWN0IGludm9rZSAtZmxhZ3MgK05vblB1YmxpYyBJbnRlcnByZXRlci5HZXRBY3RpdmUgRGlz
YWJsZVBsdWdpbklzb2xhdGlvbg==
";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// If this environment variable is set [to anything], the
        /// <see cref="CanLoad" /> method will always return true.  This can
        /// be used to force the runtime debugging support with Harpy to be
        /// used.
        /// </summary>
        private static readonly string Force_CanLoad = "Security_Force_CanLoad";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// This is used to synchronize access to the private static data in
        /// this module.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This is the encoding used to convert the byte arrays returned via
        /// the Base-64 decoding process into the actual script text.
        /// </summary>
        private static Encoding ScriptEncoding = Interpreter.DefaultEncoding;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// These are the trust flags used to evaluate the hard-coded scripts
        /// used by this class.
        /// </summary>
        private static readonly TrustFlags ScriptTrustFlags =
            TrustFlags.MaybeMarkTrusted;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// This method determines if the licensing plugin can be loaded into
        /// the current application domain.
        /// </summary>
        /// <returns>
        /// Non-zero if the licensing plugin can be loaded into the current
        /// application domain; otherwise, zero.  Since the behavior of this
        /// method can be overridden via an environment variable, its return
        /// value is not 100% reliable; however, it can provide a very useful
        /// guideline and should be used prior to attempting to make use of
        /// any functionality protected via the licensing plugin.
        /// </returns>
        public static bool CanLoad()
        {
            if (Utility.DoesEnvironmentVariableExist(Force_CanLoad))
                return true;

            return !Debugger.IsAttached;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Modifies the encoding used to convert the script fragments into
        /// strings from their decoded binary form.  If this is set to null,
        /// the <see cref="Enable" /> and <see cref="DisableIsolation" />
        /// methods will not operate correctly.
        /// </summary>
        /// <param name="encoding">
        /// The new encoding to use.  This parameter may be null.
        /// </param>
        public static void SetEncoding(
            Encoding encoding /* in */
            )
        {
            lock (syncRoot)
            {
                ScriptEncoding = encoding;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method loads the necessary plugins to integrate with the
        /// license manager in order to provide access to the functionality
        /// protected by it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter where the necessary script fragments should be
        /// evaluated.  Upon success, plugin isolation will be disabled.
        /// </param>
        /// <param name="isolation">
        /// Non-zero if the necessary plugins should be loaded into isolated
        /// application domains; otherwise, zero.
        /// </param>
        /// <param name="packagePath">
        /// An extra directory where the necessary plugins might be located,
        /// if any.
        /// </param>
        /// <param name="certificateFileName">
        /// The file name for the license certificate to be used for all the
        /// licensing plugins, if any.  It should be noted that other plugins
        /// may require different and/or additional license certificates in
        /// order to operate properly.
        /// </param>
        /// <param name="result">
        /// Upon success, this parameter will be modified to contain an
        /// informational message.  Upon failure, this parameter will be
        /// modified to contain an error message.
        /// </param>
        /// <returns>
        /// A standard Eagle return code, <see cref="ReturnCode.Ok" /> on
        /// success, <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode Enable(
            Interpreter interpreter,    /* in */
            bool isolation,             /* in */
            string packagePath,         /* in */
            string certificateFileName, /* in */
            ref Result result           /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            string text;

            lock (syncRoot)
            {
                if (ScriptEncoding == null)
                {
                    result = "script encoding not available";
                    return ReturnCode.Error;
                }

                text = ScriptEncoding.GetString(Convert.FromBase64String(
                    Script1));
            }

            ReturnCode code = interpreter.EvaluateTrustedScript(
                text, ScriptTrustFlags, ref result);

            if (code != ReturnCode.Ok)
                return code;

            lock (syncRoot)
            {
                text = String.Format(
                    ScriptEncoding.GetString(Convert.FromBase64String(
                    Script2)), StringList.MakeList(StringList.MakeList(
                    isolation, packagePath, certificateFileName)),
                    Characters.QuotationMark, Characters.QuotationMark);
            }

            code = interpreter.EvaluateTrustedScript(
                text, ScriptTrustFlags, ref result);

#if DEBUG || FORCE_TRACE
            try
            {
                if (code == ReturnCode.Ok)
                {
                    Console.WriteLine(Utility.FormatResult(
                        code, !String.IsNullOrEmpty(result) ? result :
                        (Result)String.Format("Security{0} is now enabled.",
                        isolation ? " with isolation" : String.Empty)));
                }
                else
                {
                    Console.WriteLine(Utility.FormatResult(code, result));
                }
            }
            catch
            {
                // do nothing.
            }
#endif

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disables plugin isolation for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter where the necessary script fragments should be
        /// evaluated.  Upon success, plugin isolation will be disabled.
        /// </param>
        /// <param name="result">
        /// Upon success, this parameter will be modified to contain an
        /// informational message.  Upon failure, this parameter will be
        /// modified to contain an error message.
        /// </param>
        /// <returns>
        /// A standard Eagle return code, <see cref="ReturnCode.Ok" /> on
        /// success, <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode DisableIsolation(
            Interpreter interpreter, /* in */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            string text;

            lock (syncRoot)
            {
                if (ScriptEncoding == null)
                {
                    result = "script encoding not available";
                    return ReturnCode.Error;
                }

                text = ScriptEncoding.GetString(Convert.FromBase64String(
                    Script3));
            }

            ReturnCode code = interpreter.EvaluateTrustedScript(
                text, ScriptTrustFlags, ref result);

#if DEBUG || FORCE_TRACE
            try
            {
                if (code == ReturnCode.Ok)
                {
                    Console.WriteLine(Utility.FormatResult(
                        code, !String.IsNullOrEmpty(result) ? result :
                        (Result)"Isolation is now disabled."));
                }
                else
                {
                    Console.WriteLine(Utility.FormatResult(code, result));
                }
            }
            catch
            {
                // do nothing.
            }
#endif

            return code;
        }
        #endregion
    }
}
