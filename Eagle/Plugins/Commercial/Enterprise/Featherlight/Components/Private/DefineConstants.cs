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

namespace Featherlight.Components.Private
{
    /// <summary>
    /// Provides the list of conditional compilation options that were active
    /// when the plugin was built.
    /// </summary>
    [ObjectId("796f3b84-c185-4e43-87ba-bd1d2bc091f0")]
    internal static class DefineConstants
    {
        /// <summary>
        /// The list of conditional compilation options that were active when
        /// the plugin was built.
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

#if DEAD_CODE
            "DEAD_CODE",
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

#if INTERACTIVE_COMMANDS
            "INTERACTIVE_COMMANDS",
#endif

#if ISOLATED_INTERPRETERS
            "ISOLATED_INTERPRETERS",
#endif

#if ISOLATED_PLUGINS
            "ISOLATED_PLUGINS",
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

#if NET_20_ONLY
            "NET_20_ONLY",
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

#if SERIALIZATION
            "SERIALIZATION",
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

#if THROW_ON_DISPOSED
            "THROW_ON_DISPOSED",
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
