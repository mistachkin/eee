/*
 * ScriptPageData.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using Eagle._Attributes;
using Eagle._Components.Public;
using Kapok.Components.Public;

using EnvironmentPair = Eagle._Interfaces.Public.IAnyPair<
    string, Kapok.Components.Shared.SettingDataType>;

namespace Kapok.Interfaces.Public
{
    /// <summary>
    /// Represents the configuration data for a script page, including its
    /// setup script, script file, interpreter caching and security settings,
    /// and per-page environment variables.
    /// </summary>
    [ObjectId("30f2ad87-3423-43c5-87b1-37f90c66f270")]
    public interface IScriptPageData : IDisposable
    {
        /// <summary>
        /// Gets or sets the setup script evaluated before the page script.
        /// </summary>
        string Setup { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the page uses mixed
        /// HTML/script blocks.
        /// </summary>
        bool Blocks { get; set; }
        /// <summary>
        /// Gets or sets the flags controlling script-block processing.
        /// </summary>
        ScriptBlockFlags BlockFlags { get; set; }
        /// <summary>
        /// Gets or sets the file name of the page script.
        /// </summary>
        string FileName { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the page is enabled.
        /// </summary>
        bool Enabled { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether licensing is enabled for
        /// the page.
        /// </summary>
        bool LicensingEnabled { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether a new interpreter should be
        /// created for the page.
        /// </summary>
        bool CreateInterpreter { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the page interpreter should
        /// be cached.
        /// </summary>
        bool CacheInterpreter { get; set; }
        /// <summary>
        /// Gets or sets the number of seconds a cached interpreter remains
        /// fresh.
        /// </summary>
        long CacheSeconds { get; set; }
        /// <summary>
        /// Gets or sets the security level applied to the page interpreter.
        /// </summary>
        int SecurityLevel { get; set; }
        /// <summary>
        /// Gets or sets the security flags applied to the page interpreter.
        /// </summary>
        SecurityFlags SecurityFlags { get; set; }
        /// <summary>
        /// Gets or sets the per-page environment variables (name/data-type
        /// pairs).
        /// </summary>
        IEnumerable<EnvironmentPair> Environment { get; set; }
    }
}
