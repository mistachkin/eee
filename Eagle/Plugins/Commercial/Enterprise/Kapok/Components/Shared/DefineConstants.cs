/*
 * DefineConstants.cs --
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

#if KAPOK
using Eagle._Attributes;
using Eagle._Containers.Public;
#else
using System.Runtime.InteropServices;
using StringList = System.Collections.Generic.List<string>;
#endif

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This class contains a list of compile-time definitions that indicate
    /// which optional features are enabled -AND- which platform features may
    /// be available.
    /// </summary>
#if KAPOK
    [ObjectId("ec4fa299-e048-4440-ac32-4ccbfb06786c")]
#else
    [Guid("ec4fa299-e048-4440-ac32-4ccbfb06786c")]
#endif
    internal static class DefineConstants
    {
        /// <summary>
        /// This is the list of "well-known" compile-time definitions that are
        /// handled by this class.
        /// </summary>
        public static readonly StringList OptionList = new StringList(new string[] {
#if APPDOMAINS
            "APPDOMAINS",
#endif

#if ASSEMBLY_DATETIME
            "ASSEMBLY_DATETIME",
#endif

#if ASSEMBLY_RELEASE
            "ASSEMBLY_RELEASE",
#endif

#if ASSEMBLY_STRONG_NAME_TAG
            "ASSEMBLY_STRONG_NAME_TAG",
#endif

#if ASSEMBLY_TEXT
            "ASSEMBLY_TEXT",
#endif

#if CONSOLE
            "CONSOLE",
#endif

#if DEBUG
            "DEBUG",
#endif

#if DEMO_EDITION
            "DEMO_EDITION",
#endif

#if DEMO_KEY_PAIRS
            "DEMO_KEY_PAIRS",
#endif

#if EAGLE
            "EAGLE",
#endif

#if FOR_TEST_USE_ONLY
            "FOR_TEST_USE_ONLY",
#endif

#if FORCE_TRACE
            "FORCE_TRACE",
#endif

#if ISOLATED_INTERPRETERS
            "ISOLATED_INTERPRETERS",
#endif

#if ISOLATED_PLUGINS
            "ISOLATED_PLUGINS",
#endif

#if KAPOK
            "KAPOK",
#endif

#if KAPOK_PRIVATE
            "KAPOK_PRIVATE",
#endif

#if LICENSING
            "LICENSING",
#endif

#if LIMITED_EDITION
            "LIMITED_EDITION",
#endif

#if NATIVE
            "NATIVE",
#endif

#if NETWORK
            "NETWORK",
#endif

#if NET_20
            "NET_20",
#endif

#if NET_20_ONLY
            "NET_20_ONLY",
#endif

#if NET_20_SP1
            "NET_20_SP1",
#endif

#if NET_20_SP2
            "NET_20_SP2",
#endif

#if NET_30
            "NET_30",
#endif

#if NET_35
            "NET_35",
#endif

#if NET_40
            "NET_40",
#endif

#if NET_45
            "NET_45",
#endif

#if NET_451
            "NET_451",
#endif

#if NET_452
            "NET_452",
#endif

#if NET_46
            "NET_46",
#endif

#if NET_461
            "NET_461",
#endif

#if NET_462
            "NET_462",
#endif

#if NET_47
            "NET_47",
#endif

#if NET_471
            "NET_471",
#endif

#if NET_472
            "NET_472",
#endif

#if NET_48
            "NET_48",
#endif

#if NET_481
            "NET_481",
#endif

#if NET_CORE_REFERENCES
            "NET_CORE_REFERENCES",
#endif

#if NET_CORE_20
            "NET_CORE_20",
#endif

#if NET_CORE_30
            "NET_CORE_30",
#endif

#if NET_CORE_50
            "NET_CORE_50",
#endif

#if NET_STANDARD_20
            "NET_STANDARD_20",
#endif

#if NET_STANDARD_21
            "NET_STANDARD_21",
#endif

#if NO_MAIN
            "NO_MAIN",
#endif

#if OBFUSCATION
            "OBFUSCATION",
#endif

#if OFFICIAL
            "OFFICIAL",
#endif

#if OFFICIAL_BINARY
            "OFFICIAL_BINARY",
#endif

#if OPEN_SSL
            "OPEN_SSL",
#endif

#if PATCHLEVEL
            "PATCHLEVEL",
#endif

#if SECURITY
            "SECURITY",
#endif

#if SOURCE_ID
            "SOURCE_ID",
#endif

#if SOURCE_TIMESTAMP
            "SOURCE_TIMESTAMP",
#endif

#if STABLE
            "STABLE",
#endif

#if TEST
            "TEST",
#endif

#if THROW_ON_DISPOSED
            "THROW_ON_DISPOSED",
#endif

#if TRACE
            "TRACE",
#endif

#if WINDOWS
            "WINDOWS",
#endif

#if XML
            "XML",
#endif

            null
        });
    }
}
