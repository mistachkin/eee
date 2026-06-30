/*
 * Enumerations.cs --
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

namespace Licensing.Components.Public
{
    #region Certificate Property Enumerations
    ///////////////////////////////////////////////////////////////////////////
    // *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* //
    //                                                                       //
    //       Changing these values WILL break ALL existing certificates.     //
    //                                                                       //
    // *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* //
    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that specify the protocol handling to use when working with
    /// license certificates.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("b620d138-dc76-475a-a947-b1084a8d5383")]
    public enum ProtocolType /* CORE */
    {
        /// <summary>
        /// No special protocol handling.
        /// </summary>
        None = 0x0,    // No special handling.
        /// <summary>
        /// Invalid protocol type; do not use.
        /// </summary>
        Invalid = 0x1, // Invalid, do not use.
        /// <summary>
        /// Local-only handling; this is the typical default.
        /// </summary>
        Local = 0x2,   // Local only, this is the typical default.
        /// <summary>
        /// Remote handling; not yet implemented, do not use.
        /// </summary>
        Remote = 0x4,  // Not yet implemented, do not use.
        /// <summary>
        /// Secure handling; not yet implemented, do not use.
        /// </summary>
        Secure = 0x8   // Not yet implemented, do not use.
    }

    ///////////////////////////////////////////////////////////////////////////
    // *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* //
    //                                                                       //
    //       Changing these values WILL break ALL existing certificates.     //
    //                                                                       //
    // *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* //
    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that identify the kind of entity a license certificate (or other
    /// signed content) applies to.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("9e741d8f-e3c8-4729-8b45-706fe43cf0fa")]
    public enum EntityType /* CORE */
    {
        /// <summary>
        /// Any entity type is allowed.
        /// </summary>
        Any = -1,          // Any entity type is allowed.
        /// <summary>
        /// The entity type is unknown.
        /// </summary>
        None = 0x0,        // The entity type is unknown.
        /// <summary>
        /// Invalid entity type; do not use.
        /// </summary>
        Invalid = 0x1,     // Invalid, do not use.

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// A single person.
        /// </summary>
        Individual = 0x2,  // Single person.
        /// <summary>
        /// A team of people (around three to five).
        /// </summary>
        Team = 0x4,        // Team of people (around 3 to 5).
        /// <summary>
        /// Several teams of people.
        /// </summary>
        Section = 0x8,     // Several teams of people.
        /// <summary>
        /// An entire division of a company or agency.
        /// </summary>
        Division = 0x10,   // An entire division of a company / agency.
        /// <summary>
        /// An entire department of a company or agency.
        /// </summary>
        Department = 0x20, // An entire department of a company / agency.
        /// <summary>
        /// An entire site of a company or agency.
        /// </summary>
        Site = 0x40,       // An entire site of a company / agency.
        /// <summary>
        /// An entire company or agency.
        /// </summary>
        Company = 0x80,    // An entire company / agency.
        /// <summary>
        /// An entire company or agency (etc.) worldwide.
        /// </summary>
        Worldwide = 0x100, // An entire company / agency / etc, worldwide.

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// An <c>IScript</c> instance.
        /// </summary>
        Script = 0x200,    // An IScript instance.
        /// <summary>
        /// Script or data contained in a string.
        /// </summary>
        String = 0x400,    // Script or data contained in a string.
        /// <summary>
        /// Script or data contained in a file.
        /// </summary>
        File = 0x800,      // Script or data contained in a file.
        /// <summary>
        /// A key ring script contained in a string or file.
        /// </summary>
        KeyRing = 0x1000,  // Key ring script contained in a string / file.
        /// <summary>
        /// A list of key or certificate revocations, etc.
        /// </summary>
        List = 0x2000,     // List of key or certificate revocations, etc.
        /// <summary>
        /// A response from a time server.
        /// </summary>
        Time = 0x4000,     // Response from a time server.
        /// <summary>
        /// Script or data contained in a stream.
        /// </summary>
        Stream = 0x8000,   // Script or data contained in a stream.
        /// <summary>
        /// Something else.
        /// </summary>
        Special = 0x10000, // Something else.

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Something that does not require an external certificate; e.g. it
        /// was directly signed by an implicitly trusted key pair, without
        /// using a certificate.
        /// </summary>
        Trusted = 0x20000, // Something that does not require an external
                           // certificate, e.g. was directly signed by an
                           // implicitly trusted key pair, without using
                           // a certificate.
        /// <summary>
        /// The certificate is locked to a machine that is identified by the
        /// <c>EntityName</c> property.
        /// </summary>
        Machine = 0x40000, // The certificate is locked to a machine that
                           // is identified by the EntityName property.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This represents all entity types VALID for
        //       license certificates.
        //
        /// <summary>
        /// All entity types that are valid for license certificates.
        /// </summary>
        LicenseTypeMask = Individual | Team | Section | Division |
                          Department | Site | Company | Worldwide |
                          Machine,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This represents all entity types NOT VALID for
        //       license certificates.
        //
        /// <summary>
        /// All entity types that are not valid for license certificates.
        /// </summary>
        NonLicenseDataMask = Script | String | File | KeyRing |
                             List | Time | Stream | Special |
                             Trusted,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This represents all valid entity types for script
        //       certificates that may contain data.
        //
        /// <summary>
        /// All valid entity types for script certificates that may contain
        /// data.
        /// </summary>
        ScriptDataMask = Script | KeyRing,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This represents all valid entity types for non-script
        //       certificates that may contain data.
        //
        /// <summary>
        /// All valid entity types for non-script certificates that may
        /// contain data.
        /// </summary>
        NonScriptDataMask = String | File,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This represents all entity types that require the
        //       special handling for "embedded" content; i.e. the
        //       EntityValue must contain the data.
        //
        /// <summary>
        /// All entity types that require special handling for embedded
        /// content; i.e. the <c>EntityValue</c> must contain the data.
        /// </summary>
        EmbeddedDataMask = ScriptDataMask | String,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This represents all valid entity types for script
        //       and non-script certificates that may contain data.
        //
        /// <summary>
        /// All valid entity types for script and non-script certificates that
        /// may contain data.
        /// </summary>
        DataMask = ScriptDataMask | NonScriptDataMask
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Auxiliary & Support Enumerations
    ///////////////////////////////////////////////////////////////////////////
    // *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* //
    //                                                                       //
    //              Changing these values MAY break SDK users.               //
    //                                                                       //
    // *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* *WARNING* //
    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that select which set of flags (property) of a certificate or
    /// key pair a flag-based operation applies to.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("6310b63f-5925-4864-b684-b7426930fb52")]
    public enum FlagType /* CORE */
    {
        /// <summary>
        /// Nothing; do not use.
        /// </summary>
        None = 0x0,        // Nothing, do not use.
        /// <summary>
        /// Invalid; do not use.
        /// </summary>
        Invalid = 0x1,     // Invalid, do not use.
        /// <summary>
        /// Use the "Features" property / flags.
        /// </summary>
        Feature = 0x2,     // Use "Features" property / flags.
        /// <summary>
        /// Use the "Restrictions" property / flags.
        /// </summary>
        Restriction = 0x4, // Use "Restrictions" property / flags.
        /// <summary>
        /// Use the "KeyUsage" property / flags.
        /// </summary>
        KeyUsage = 0x8,    // Use "KeyUsage" property / flags.
        /// <summary>
        /// Use the default property / flags (i.e. "Features").
        /// </summary>
        Default = 0x10,    // Use default property / flags (i.e. "Features").

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This represents all valid flag types for both certificates
        //       and key pairs.
        //
        /// <summary>
        /// All valid flag types for both certificates and key pairs.
        /// </summary>
        All = Feature | Restriction | KeyUsage,
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Specifies the backing store used to persist licensing data.
    /// </summary>
    [ObjectId("d536101a-e0d0-4e97-a57b-57dad450072f")]
    public enum StorageType
    {
        /// <summary>
        /// Nothing; do not use.
        /// </summary>
        None = 0x0,       // Nothing, do not use.
        /// <summary>
        /// Invalid; do not use.
        /// </summary>
        Invalid = 0x1,    // Invalid, do not use.
        /// <summary>
        /// Use the Win32 registry.
        /// </summary>
        Registry = 0x2,   // Use the Win32 registry.
        /// <summary>
        /// Use the interpreter.
        /// </summary>
        Interpreter = 0x4 // Use the interpreter.
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Flags that select which certificate properties are included when
    /// computing a certificate hash.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("849c830f-652c-4f8a-9cb4-2a1a50e7079b")]
    public enum CertificateHashFlags /* CORE */
    {
        /// <summary>
        /// Include nothing; do not use.
        /// </summary>
        None = 0x0,               // Include nothing, do not use.
        /// <summary>
        /// Invalid; do not use.
        /// </summary>
        Invalid = 0x1,            // Invalid, do not use.

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Include the Protocol property.
        /// </summary>
        Protocol = 0x2,           // Include the Protocol property.
        /// <summary>
        /// Include the ProtocolVersion property.
        /// </summary>
        ProtocolVersion = 0x4,    // Include the ProtocolVersion property.
        /// <summary>
        /// Include the Vendor property.
        /// </summary>
        Vendor = 0x8,             // Include the Vendor property.
        /// <summary>
        /// Include the Origin property.
        /// </summary>
        Origin = 0x10,            // Include the Origin property.
        /// <summary>
        /// Include the Authority property.
        /// </summary>
        Authority = 0x20,         // Include the Authority property.
        /// <summary>
        /// Include the Agreement property.
        /// </summary>
        Agreement = 0x40,         // Include the Agreement property.
        /// <summary>
        /// Include the Support property.
        /// </summary>
        Support = 0x80,           // Include the Support property.
        /// <summary>
        /// Include the Id property.
        /// </summary>
        Id = 0x100,               // Include the Id property.
        /// <summary>
        /// Include the TimeStamp property.
        /// </summary>
        TimeStamp = 0x200,        // Include the TimeStamp property.
        /// <summary>
        /// Include the Duration property.
        /// </summary>
        Duration = 0x400,         // Include the Duration property.
        /// <summary>
        /// Include the Key property.
        /// </summary>
        Key = 0x800,              // Include the Key property.
        /// <summary>
        /// Include the Keys property.
        /// </summary>
        Keys = 0x1000,            // Include the Keys property.
        /// <summary>
        /// Include the Number property.
        /// </summary>
        Number = 0x2000,          // Include the Number property.
        /// <summary>
        /// Include the SerialNumber property.
        /// </summary>
        SerialNumber = 0x4000,    // Include the SerialNumber property.
        /// <summary>
        /// Include the HashAlgorithm property.
        /// </summary>
        HashAlgorithm = 0x8000,   // Include the HashAlgorithm property.
        /// <summary>
        /// Include the Type property.
        /// </summary>
        Type = 0x10000,           // Include the Type property.
        /// <summary>
        /// Include the EntityType property.
        /// </summary>
        EntityType = 0x20000,     // Include the EntityType property.
        /// <summary>
        /// Include the EntityName property.
        /// </summary>
        EntityName = 0x40000,     // Include the EntityName property.
        /// <summary>
        /// Include the EntityValue property.
        /// </summary>
        EntityValue = 0x80000,    // Include the EntityValue property.
        /// <summary>
        /// Include the ExtraData property.
        /// </summary>
        ExtraData = 0x100000,     // Include the ExtraData property.
        /// <summary>
        /// Include the Quantity property.
        /// </summary>
        Quantity = 0x200000,      // Include the Quantity property.
        /// <summary>
        /// Include the Product property.
        /// </summary>
        Product = 0x400000,       // Include the Product property.
        /// <summary>
        /// Include the Version property.
        /// </summary>
        Version = 0x800000,       // Include the Version property.
        /// <summary>
        /// Include the Features property.
        /// </summary>
        Features = 0x1000000,     // Include the Features property.
        /// <summary>
        /// Include the Restrictions property.
        /// </summary>
        Restrictions = 0x2000000, // Include the Restrictions property.
        /// <summary>
        /// Include the Notes property.  Do not use.
        /// </summary>
        Notes = 0x4000000,        // Include the Notes property.  Do not use.
        /// <summary>
        /// Include the ServerInfo property.  Do not use.
        /// </summary>
        ServerInfo = 0x8000000,   // Include the ServerInfo property.  Do not use.
        /// <summary>
        /// Not used.  Do not use.
        /// </summary>
        Signature = 0x10000000,   // Not used.  Do not use.
        /// <summary>
        /// Not used.  Do not use.
        /// </summary>
        Signatures = 0x20000000,  // Not used.  Do not use.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Include the properties that should be present in every
        //       valid certificate, including script certificates and
        //       license certificates.
        //
        /// <summary>
        /// Includes the properties that should be present in every valid
        /// certificate, including script certificates and license
        /// certificates.
        /// </summary>
        Basic = Id | TimeStamp | Duration | Key | Keys,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Embedded certificates are expected to contain at least
        //       these properties.
        //
        /// <summary>
        /// Includes the properties that embedded certificates are expected to
        /// contain at a minimum.
        /// </summary>
        Embedded = Vendor | Id | HashAlgorithm | EntityType |
                   EntityValue | TimeStamp | Duration | Key |
                   Keys,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Include all properties that have non-default values.  This is
        //       the value that should almost always be used by third-parties.
        //
        /// <summary>
        /// Includes all properties that have non-default values.  This is the
        /// value that should almost always be used by third parties.
        /// </summary>
        Full = Protocol | ProtocolVersion | Vendor | Origin |
               Authority | Agreement | Support | Id | TimeStamp |
               Duration | Key | Keys | Number | SerialNumber |
               HashAlgorithm | Type | EntityType | EntityName |
               EntityValue | ExtraData | Quantity | Product |
               Version | Features | Restrictions,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// For use by the Hash (certificate) method only.
        /// </summary>
        Certificate = Full, // For use by the Hash (certificate) method only.
        /// <summary>
        /// For use by the HashString method only.
        /// </summary>
        String = Basic,     // For use by the HashString method only.
        /// <summary>
        /// For use by the HashFile method only.
        /// </summary>
        File = Basic,       // For use by the HashFile method only.
        /// <summary>
        /// For use by the HashStream method only.
        /// </summary>
        Stream = Basic,     // For use by the HashStream method only.
        /// <summary>
        /// For use by the HashBytes method only.
        /// </summary>
        Bytes = Basic,      // For use by the HashBytes method only.

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: For use by the ScriptCallback method only.
        //
        /// <summary>
        /// For use by the ScriptCallback method only.
        /// </summary>
        Script = Id | TimeStamp | Duration | Key | Keys |
                 EntityType | EntityName | EntityValue |
                 ExtraData
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Status flags that describe the outcome of certificate and key-pair
    /// operations.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("3d0bf4f1-3bbc-44eb-91dc-4aa68da5a3e7")]
    public enum OperationStatus : ulong /* CORE */
    {
        /// <summary>
        /// Not currently used; this is a placeholder.
        /// </summary>
        None = 0x0,                       /* Not currently used, this is
                                           * a placeholder. */
        /// <summary>
        /// Not currently used; this is reserved for future use.
        /// </summary>
        Unknown = 0x1,                    /* Not currently used, this is
                                           * reserved for future use. */
        /// <summary>
        /// General failure; this is not currently used.
        /// </summary>
        Failed = 0x2,                     /* General failure, this is not
                                           * currently used. */
        /// <summary>
        /// The RSACryptoServiceProvider verified OK.
        /// </summary>
        VerifiedOk = 0x100,               /* RSACryptoServiceProvider
                                           * verified OK. */
        /// <summary>
        /// The RSACryptoServiceProvider was skipped OK.
        /// </summary>
        SkippedOk = 0x200,                /* RSACryptoServiceProvider
                                           * skipped OK. */
        /// <summary>
        /// The RenewCallback succeeded OK.
        /// </summary>
        RenewedOk = 0x400,                /* RenewCallback succeeded OK. */
        /// <summary>
        /// The RSACryptoServiceProvider signed OK.
        /// </summary>
        SignedOk = 0x800,                 /* RSACryptoServiceProvider
                                           * signed OK. */
        /// <summary>
        /// The certificate was imported OK.
        /// </summary>
        ImportedOk = 0x1000,              /* Certificate was imported OK. */
        /// <summary>
        /// The certificate was exported OK.
        /// </summary>
        ExportedOk = 0x2000,              /* Certificate was exported OK. */
        /// <summary>
        /// The RsaKeyFile was read OK.
        /// </summary>
        KeyPairOk = 0x4000,               /* RsaKeyFile was read OK. */
        /// <summary>
        /// A certificate warning was added OK.
        /// </summary>
        WarningOk = 0x8000,               /* Certificate warning was
                                           * added OK. */
        /// <summary>
        /// The X.509 assembly signature could not be queried.
        /// </summary>
        SignatureError = 0x10000,         /* X.509 assembly signature could
                                           * not be queried. */
        /// <summary>
        /// The X.509 assembly signature is missing.
        /// </summary>
        SignatureMissing = 0x20000,       /* X.509 assembly signature is
                                           * missing. */
        /// <summary>
        /// The X.509 assembly signature was skipped.
        /// </summary>
        SignatureSkipped = 0x40000,       /* X.509 assembly signature was
                                           * skipped. */
        /// <summary>
        /// The X.509 assembly signature mismatched.
        /// </summary>
        SignatureMismatch = 0x80000,      /* X.509 assembly signature
                                           * mismatched. */
        /// <summary>
        /// The X.509 assembly signature matched.
        /// </summary>
        SignatureOk = 0x100000,           /* X.509 assembly signature
                                           * matched. */
        /// <summary>
        /// The certificate is not expired; however, it is not yet valid,
        /// either.
        /// </summary>
        NotYetValid = 0x200000,           /* Certificate is not expired;
                                           * however, it is not yet valid,
                                           * either. */
        /// <summary>
        /// The certificate may be expired (NTP error).
        /// </summary>
        UnknownExpired = 0x400000,        /* Certificate may be expired
                                           * (NTP error). */
        /// <summary>
        /// The certificate may be expired (NTP drift).
        /// </summary>
        MaybeExpired = 0x800000,          /* Certificate may be expired
                                           * (NTP drift). */
        /// <summary>
        /// The certificate may be expired (cannot get install time).
        /// </summary>
        MaybeInstalled = 0x1000000,       /* Certificate may be expired
                                           * (cannot get install time). */
        /// <summary>
        /// The certificate is not expired.
        /// </summary>
        NotExpired = 0x2000000,           /* Certificate is not expired. */
        /// <summary>
        /// The certificate never expires.
        /// </summary>
        NeverExpires = 0x4000000,         /* Certificate never expires. */
        /// <summary>
        /// The certificate always expires (testing).
        /// </summary>
        AlwaysExpires = 0x8000000,        /* Certificate always expires
                                           * (testing). */
        /// <summary>
        /// The certificate agreement URI matches OK.
        /// </summary>
        AgreementOk = 0x10000000,         /* Certificate agreement URI
                                           * matches OK. */
        /// <summary>
        /// Flags were matched OK.
        /// </summary>
        FlagOk = 0x20000000,              /* Flags were matched OK. */
        /// <summary>
        /// The entity type was matched OK.
        /// </summary>
        TypeOk = 0x40000000,              /* Entity type was matched OK. */
        /// <summary>
        /// The certificate is for test use only.
        /// </summary>
        ForTestUseOnly = 0x80000000,      /* Certificate is for test use
                                           * only. */
        /// <summary>
        /// The certificate is not revoked.
        /// </summary>
        NotRevoked = 0x100000000,         /* Certificate is not revoked. */
        /// <summary>
        /// The certificate may be revoked (server error).
        /// </summary>
        UnknownRevoked = 0x200000000,     /* Certificate may be revoked
                                           * (server error). */
        /// <summary>
        /// The certificate is always revoked (testing).
        /// </summary>
        AlwaysRevoked = 0x400000000,      /* Certificate always revoked
                                           * (testing). */
        /// <summary>
        /// The certificate is expired; however, its public key is
        /// well-known.
        /// </summary>
        ExpiredWellKnown = 0x800000000,   /* Certificate is expired; however,
                                           * its public key is well-known. */
        /// <summary>
        /// The certificate is expired; however, the assembly is a version
        /// that is less-than-or-equal-to the (product?) version within the
        /// certificate.
        /// </summary>
        ExpiredOldVersion = 0x1000000000, /* Certificate is expired; however,
                                           * the assembly is a version that
                                           * is less-than-or-equal-to the
                                           * (product?) version within the
                                           * certificate. */
        /// <summary>
        /// The certificate is expired; however, the assembly is a version
        /// that falls within the version range that was configured for the
        /// plugin.
        /// </summary>
        ExpiredInRange = 0x2000000000,    /* Certificate is expired; however,
                                           * the assembly is a version that
                                           * falls within the version range
                                           * that was configured for the
                                           * plugin. */
        /// <summary>
        /// The plugin is either not installed -OR- is improperly installed.
        /// </summary>
        NotInstalled = 0x4000000000,      /* Plugin is either not installed
                                           * -OR- is improperly installed. */
        /// <summary>
        /// The certificate was found (i.e. in cache).
        /// </summary>
        FoundOk = 0x8000000000            /* Certificate was found (i.e. in
                                           * cache). */
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Identifies the phase (context) in which licensing configuration
    /// occurs.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [Flags()]
    [ObjectId("7bf91478-5906-4e28-a35a-01789bfd5b73")]
    public enum ConfigurationPhase
    {
        /// <summary>
        /// None; do not use.
        /// </summary>
        None = 0x0,       /* None, do not use. */
        /// <summary>
        /// Invalid; do not use.
        /// </summary>
        Invalid = 0x1,    /* Invalid, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Within the IState.Initialize method.
        /// </summary>
        Initialize = 0x4, /* Within IState.Initialize method. */
        /// <summary>
        /// Within the license verification subsystem.
        /// </summary>
        Verify = 0x8,     /* Within license verification subsystem. */
        /// <summary>
        /// Via a script call to a command, etc.
        /// </summary>
        Demand = 0x10,    /* Via script call to command, etc. */
        /// <summary>
        /// Within the IState.Terminate method.
        /// </summary>
        Terminate = 0x20, /* Within IState.Terminate method. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The manager configuration phase.
        /// </summary>
        Manager = 0x100,
        /// <summary>
        /// The isolated configuration phase.
        /// </summary>
        Isolated = 0x200,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The context (phase) is unknown.
        /// </summary>
        Unknown = 0x1000, /* Context (phase) is unknown. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Used when composing the default configuration phase.
        /// </summary>
        ForDefault = 0x10000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Mask combining the initialization-related phases (Initialize and
        /// Verify).
        /// </summary>
        InitializeMask = Initialize | Verify,
        /// <summary>
        /// Mask combining all configuration phases.
        /// </summary>
        AnyMask = InitializeMask | Demand | Terminate,
        /// <summary>
        /// Mask combining the configuration phase flags (Manager and
        /// Isolated).
        /// </summary>
        FlagsMask = Manager | Isolated,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default configuration phase.
        /// </summary>
        Default = Initialize | ForDefault
    }
    #endregion
}
