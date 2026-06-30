/*
 * DefineConstants.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using Eagle._Attributes;
using Eagle._Containers.Public;

namespace Badge.Components.Private
{
    /// <summary>
    /// Provides the set of conditional compilation symbols (define
    /// constants) that were active when this assembly was built.
    /// </summary>
    [ObjectId("b8f4a41b-89a3-40eb-968a-708ec21caaae")]
    internal static class DefineConstants
    {
        /// <summary>
        /// The list of conditional compilation symbols that were enabled
        /// at build time.  Each entry is included only when its
        /// corresponding preprocessor symbol was defined during
        /// compilation; the list is terminated with a null element.
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

#if CERTIFICATE_PLUGIN
            "CERTIFICATE_PLUGIN",
#endif

#if CERTIFICATE_POLICY
            "CERTIFICATE_POLICY",
#endif

#if CERTIFICATE_RENEWAL
            "CERTIFICATE_RENEWAL",
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

#if EMBED_CERTIFICATES
            "EMBED_CERTIFICATES",
#endif

#if ENTERPRISE_LOCKDOWN
            "ENTERPRISE_LOCKDOWN",
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

#if LICENSE_MANAGER
            "LICENSE_MANAGER",
#endif

#if LICENSING
            "LICENSING",
#endif

#if LIMITED_EDITION
            "LIMITED_EDITION",
#endif

#if MAYBE_ENTERPRISE_LOCKDOWN
            "MAYBE_ENTERPRISE_LOCKDOWN",
#endif

#if MONO
            "MONO",
#endif

#if MONO_BUILD
            "MONO_BUILD",
#endif

#if MONO_HACKS
            "MONO_HACKS",
#endif

#if MONO_LEGACY
            "MONO_LEGACY",
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

#if PLUGIN_COMMANDS
            "PLUGIN_COMMANDS",
#endif

#if SECURITY
            "SECURITY",
#endif

#if SHELL
            "SHELL",
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

#if TRACE
            "TRACE",
#endif

#if WINDOWS
            "WINDOWS",
#endif

            null
        });
    }
}
