/*
 * DefineConstants.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) \$Id: \$
 */

using Eagle._Attributes;
using Eagle._Containers.Public;

namespace ${projectName}.Components.Private
{
    /// <summary>
    /// Provides the list of conditional compilation options that were active
    /// when the plugin was built.
    /// </summary>
    \[ObjectId("[string tolower [guid new]]")\]
    internal static class DefineConstants
    {
        /// <summary>
        /// The list of conditional compilation options that were active when
        /// the plugin was built.
        /// </summary>
        public static readonly StringList OptionList = new StringList(new string\[\] {
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

#if DEBUG
            "DEBUG",
#endif

#if DEBUG_TRACE
            "DEBUG_TRACE",
#endif

#if DEBUG_WRITE
            "DEBUG_WRITE",
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

#if FOR_TEST_USE_ONLY
            "FOR_TEST_USE_ONLY",
#endif

#if FORCE_TRACE
            "FORCE_TRACE",
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

#if VERBOSE
            "VERBOSE",
#endif

            null
        });
    }
}
