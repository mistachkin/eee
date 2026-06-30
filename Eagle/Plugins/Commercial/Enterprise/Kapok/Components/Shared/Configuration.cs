/*
 * Configuration.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Eagle Enterprise Edition: Kapok SDK v1.0
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

namespace Harpy.Components.Shared
{
    /// <summary>
    /// This static class contains a compile-time Harpy configuration script
    /// that enables the Harpy plugin to be loaded and used within the Visual
    /// Studio debugger (or another debugger).
    /// </summary>
#if KAPOK
    [ObjectId("29a0a9e2-889c-4f50-8d5c-9b06e2622ce9")]
#else
    [Guid("29a0a9e2-889c-4f50-8d5c-9b06e2622ce9")]
#endif
    internal static class Configuration
    {
        #region Private Constants
        /// <summary>
        /// This is the Base-64 encoded, encrypted configuration script for the
        /// Harpy plugin.  It is only needed when the application is being run
        /// from inside the Visual Studio (or other) debugger.  It perform some
        /// basic sanity checks and then enables the the necessary Harpy flags
        /// to disable its anti-debugging code.
        /// </summary>
        private static readonly string text =
"! EncryptedData: v1.0\n" +
"! symmetricAlgorithmName: Rijndael\n" +
"! salt: eViSxwYGLUSZibLhL0TSFw==\n\n" +
"WLkDnN0apMrINFQ/IseXFOq2K6U/QJY72SjNs+j42VcwSwrgr15WY/uYgfDy4NKxs4VIijZKWmZS\n" +
"xS0muEiNmksUj4iJZnIPLgl0rn1srtMHVUAJRszV05It530V7NxfoNqfbnC+1gciy7bJVy6o1Qfd\n" +
"JuidU79Bz/WGR06XDWrBWXCukMu65a1Hozu4uJqF3bSuEVH7Dp1UXt9n3tYaC+24pW34a6glpKRm\n" +
"nmq1GGK1QTwG3WtKrcSxaxOQzEmNQDA6/Ou3CJcNE5HoYKCXpHSkd1muPcdoaMa/6ySi38tcZ0n1\n" +
"wC2Q3+mjcxlnFQX7ZJnWMW8ffC2mzFRmoqaMw8hz28sBkTgmM3BPHu7v9iLaUk9OFLcyhUooo/7e\n" +
"Ql/aOcoSulRuJQ7fPLJXzUBLqafn/oGsbGpxqjkVeIysOyzOmCuWcL21N67OZ/x4yUtQCoZWcclh\n" +
"drA90f1kHm64bdXCL2RSE1wMcW7qbFtFFEQVy6G/MmGliwDNBiw9tSPc9o8CTdA64gT0mfz+1m29\n" +
"a18W1vBsisqz+3EPILipiGtfNHXztT8LtIrcLy+7iG/6tqsrp3hs/rFZDrEBR5lSrMu4ID7kt+K1\n" +
"fm7+CtB1hrFXbJSIGeVweGgFvvPUz+EOt5kHIck+7eiX8yRISZAt9mgFusnZ4ffee01FDDc1IaCG\n" +
"J9klxQBMuzGKK8QDkcOpiSqnkLh6sXWbdCXxuMxrY9OkQUA7JM66tLAA6eeGvwHmmmQHhWjxrzsM\n" +
"sTPLbeQu1HsJHjudWTvDaiOMZk6sfFz1dZP5NDVrtnGJTD1Qm8iEXBgOhXCgesImAF931yAP41i2\n" +
"cEOCZSoau9F5KxpBOkVktbtpQyIOwtcZd2856mdq1zt6Hz+gZKGzTT39PHZQNFVImVGH1cYiC3UX\n" +
"Gl1RgKpbqpdG511nQx6ij8lVA6aGWLjmZU/TbMLjdQe1DK4kgnyirLqJ45yeJKojj55JsMf2K4SB\n" +
"/rMpw+XA7A2+8lvR0+vKAyAnFvq/9xj8F8bbm09MmggI0J6BnUOzHdh+l8lk7DmGkCpyxu99Wunn\n" +
"cpthdoIsl/+USuN9ODnPKj88vHRjqjEHDgpC73OWVVux4ucYJCOIdLBOXaDtn71UqrooY1PCwwow\n" +
"hxDspgU36vVyOtoRt8b4uZsSQRKTVIv5k5qBPbveeEH2hn1SVOe3H0xzAbMAii3PjroxURp15wRQ\n" +
"r6zsDwu2tKIfIFBftlIzdBzuzyxoQuQ2rBEeqWar2OPE8EalAsCKy7+uITQfz04IWzE2K2e9Uq11\n" +
"6dICwuXILoFAzslOFFcPnR4292Uhxzm472X4HLT79N8+deVvCS31Ol1wg+nx4EhxV0DZFiEwjHpG\n" +
"fUgNb1RnVFBOBpJYdUyWRps+q7HCY8leD5rvQnzsOpbWl4pqac2HoLMu66mb33e4h8TZ+asiTrZI\n" +
"11Q0RQlOmlgndir32i704RmoMMyIVz5Sj6rxHmryuH38m0ooRmu3ctk1FEOrx5M/UPP6RHkMxnTW\n" +
"e1mYGWqWCwdRvpQP4Rf+zqokkvruWC1ZGPpZtA7XXs5U7kp4EcsLDALE1aaL8s3x8m32QEf2rfY4\n" +
"pH0SnafwI2sKNfS9nCgm6711pncP0WdwLYzqKsdq3Z0K9o1YHHtlrwiK86XhqVnILF9E";

        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// This is the RSA signature block for the <see cref="text" /> field.
        /// </summary>
        private static readonly string signature =
"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";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Enables or disables the Harpy configuration script built into this
        /// class.
        /// </summary>
        /// <param name="appDomain">
        /// The application domain to use for the Harpy configuration.  This
        /// parameter may be null.  If this parameter is null, configuration
        /// for Harpy may be setup (or unsetup) on a process-wide basis.
        /// </param>
        /// <param name="enable">
        /// Non-zero to enable -OR- zero to disable the Harpy configuration
        /// script built into this class.
        /// </param>
        public static void Setup(
            AppDomain appDomain, /* in: OPTIONAL */
            bool enable          /* in */
            )
        {
            string[] varNames = {
                "ConfigurationFileName1",
                "ConfigurationScriptText1",
                "ConfigurationSignatureText1"
            };

            string[] varValues = {
                enable ? "Harpy.v1.eeagle" : null,
                enable ? text : null,
                enable ? signature : null
            };

            int length = varNames.Length;
            int index;

            if (appDomain != null)
            {
                for (index = 0; index < length; index++)
                {
                    appDomain.SetData(
                        varNames[index], varValues[index]);
                }
            }
            else
            {
                for (index = 0; index < length; index++)
                {
                    Environment.SetEnvironmentVariable(
                        varNames[index], varValues[index]);
                }
            }
        }
        #endregion
    }
}
