/*
 * ScintillaOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ScintillaNET;

#if !SCINTILLA_30
using ScintillaNET.Configuration;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using _Public = Eagle._Components.Public;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Provides the integration with the Scintilla source-editing control
    /// used by the editor forms: locating and pre-loading its native library,
    /// configuring the control (lexer, styles, margins, and properties) for
    /// the Eagle language, and getting, appending, and clicking text.
    /// </summary>
    [ObjectId("59f0864e-0ca9-40a0-9031-938513a580a7")]
    internal static class ScintillaOps
    {
        #region Scintilla Support Private Constants
        //
        // NOTE: This is the name of the script variable that contains the
        //       loaded native module handle, if any.
        //
        /// <summary>
        /// The script variable that holds the loaded Scintilla native module
        /// handle.
        /// </summary>
        private static readonly string NativeLibraryModuleVariableName =
            "::" + typeof(Enterprise).FullName + "_ScintillaModule";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of the script variable that contains the
        //       loaded Lexilla native module handle, if any.
        //
        /// <summary>
        /// The script variable that holds the loaded Lexilla native module
        /// handle.
        /// </summary>
        private static readonly string NativeLibraryLexerModuleVariableName =
            "::" + typeof(Enterprise).FullName + "_LexillaModule";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the native module name prefix, which is shared by all
        //       supported platforms.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The module name prefix of the Scintilla native library.
        /// </summary>
        private static string NativeLibraryModulePrefix = "Scintilla";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Scintilla 5.x split the lexers into a separate "Lexilla"
        //       native module; it must be pre-loaded alongside the core.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The module name prefix of the Lexilla native library.
        /// </summary>
        private static string NativeLibraryLexerModulePrefix = "Lexilla";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the directory name for the native Scintilla library
        //       needed by this process (i.e. 32-bit or 64-bit).
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The directory containing the Scintilla native library.
        /// </summary>
        private static string NativeLibraryDirectory = ManagerOps.GetDirectory();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the file name for the native Scintilla library needed
        //       by this process (i.e. 32-bit or 64-bit).
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The file name of the Scintilla native library module.
        /// </summary>
        private static string NativeLibraryFileName = GetModuleName(
            NativeLibraryModulePrefix);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the file name for the Lexilla native library needed
        //       by this process (i.e. 32-bit or 64-bit).
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The file name of the Lexilla native library module.
        /// </summary>
        private static string NativeLibraryLexerFileName = GetModuleName(
            NativeLibraryLexerModulePrefix);

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the script used to attempt to forcibly pre-load the
        //       native Scintilla library needed by this process (i.e. 32-bit
        //       or 64-bit).
        //
        /// <summary>
        /// The script that force-loads and locks the native Scintilla and
        /// Lexilla modules.
        /// </summary>
        private static readonly string NativeLibraryPreLoadScript =
            String.Format(
            "set {0} [library checkload -locked -- {1}]; " +
            "set {2} [library checkload -locked -- {3}];",
            Parser.Quote(NativeLibraryModuleVariableName), Parser.Quote(
            Path.Combine(NativeLibraryDirectory, NativeLibraryFileName)),
            Parser.Quote(NativeLibraryLexerModuleVariableName), Parser.Quote(
            Path.Combine(NativeLibraryDirectory, NativeLibraryLexerFileName)));

        //
        // NOTE: This is the script used to attempt to cleanup the native
        //       Scintilla library needed by this process (i.e. 32-bit or
        //       64-bit).  It does not actually unload the native library
        //       as that cannot be done safely.
        //
        /// <summary>
        /// The script that releases the pre-loaded native module handles
        /// (the locked libraries themselves remain loaded, as they cannot
        /// be unloaded safely).
        /// </summary>
        private static readonly string NativeLibraryCleanupScript =
            String.Format(
            "library unload [set {0}]; library unload [set {1}];",
            Parser.Quote(NativeLibraryModuleVariableName),
            Parser.Quote(NativeLibraryLexerModuleVariableName));

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of the array elements to use within each of
        //       array variable names specified below (i.e. which are used to
        //       load and configure ScintillaNET).
        //
#if SCINTILLA_30
        /// <summary>
        /// The version element name identifying the Scintilla 3.x resources.
        /// </summary>
        private static readonly string VersionElementName = "3.x";
#else
        /// <summary>
        /// The version element name identifying the Scintilla 2.x resources.
        /// </summary>
        private static readonly string VersionElementName = "2.x";
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of an optional script variable that contains
        //       the file name containing a ScintillaNET configuration file to
        //       use.
        //
        /// <summary>
        /// The optional script variable naming a ScintillaNET configuration
        /// file to use.
        /// </summary>
        private static readonly string ConfigureFileNameVariableName =
            "::" + typeof(Enterprise).FullName + "_ScintillaConfigureFileName(" +
            VersionElementName + ")";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of an optional script variable that contains
        //       the script that is used to pre-configure the Scintilla control
        //       via an opaque object handle provided by the hot-key plugin.
        //
        /// <summary>
        /// The optional script variable holding the script that pre-configures
        /// the Scintilla control.
        /// </summary>
        private static readonly string PreConfigureScriptVariableName =
            "::" + typeof(Enterprise).FullName + "_ScintillaPreConfigureScript(" +
            VersionElementName + ")";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of an optional script variable that contains
        //       the script that is used to configure the Scintilla control via
        //       an opaque object handle provided by the hot-key plugin.
        //
        /// <summary>
        /// The optional script variable holding the script that configures
        /// the Scintilla control.
        /// </summary>
        private static readonly string ConfigureScriptVariableName =
            "::" + typeof(Enterprise).FullName + "_ScintillaConfigureScript(" +
            VersionElementName + ")";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of an optional script variable that contains
        //       the script that is used to customize the Scintilla control via
        //       an opaque object handle provided by the hot-key plugin.
        //
        /// <summary>
        /// The optional script variable holding the script that customizes
        /// the Scintilla control.
        /// </summary>
        private static readonly string SetPropertyScriptVariableName =
            "::" + typeof(Enterprise).FullName + "_ScintillaSetPropertyScript(" +
            VersionElementName + ")";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of an optional script variable that contains
        //       the theme that is used to build the XML resource name that is
        //       used to configure the Scintilla control syntax highlighting.
        //
        /// <summary>
        /// The optional script variable holding the theme used to build the
        /// Scintilla syntax-highlighting resource name.
        /// </summary>
        private static readonly string ThemeVariableName =
            "::" + typeof(Enterprise).FullName + "_ScintillaTheme(" +
            VersionElementName + ")";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of the opaque object handle that will be used
        //       for the Scintilla control provided to the script engine.
        //
        /// <summary>
        /// The opaque object name used for the Scintilla control in scripts.
        /// </summary>
        private static readonly string ObjectName = "scintilla";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of the (embedded resource) script file that
        //       will contain (shared) resource data manipulation procedures
        //       used by ScintillaNET 2.x -AND- 3.x.
        //
        /// <summary>
        /// The name of the embedded managed resource containing the support
        /// script.
        /// </summary>
        private const string SupportManagedResourceName = "support.eagle";

        ///////////////////////////////////////////////////////////////////////

#if SCINTILLA_30
        /// <summary>
        /// The name of the default embedded managed resource for the language
        /// configuration.
        /// </summary>
        private const string DefaultManagedResourceName = "eagle.eagle";
        /// <summary>
        /// The format string for a variant-specific embedded managed resource
        /// name.
        /// </summary>
        private const string BaseManagedResourceName = "eagle.{0}.eagle";
#else
        /// <summary>
        /// The name of the default embedded managed resource for the language
        /// configuration.
        /// </summary>
        private const string DefaultManagedResourceName = "eagle.xml";
        /// <summary>
        /// The format string for a variant-specific embedded managed resource
        /// name.
        /// </summary>
        private const string BaseManagedResourceName = "eagle.{0}.xml";
#endif

        ///////////////////////////////////////////////////////////////////////

#if !SCINTILLA_30
        /// <summary>
        /// The Scintilla lexer language name registered for Eagle scripts.
        /// </summary>
        private const string LanguageName = "eagle";
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Scintilla Support Methods
        /// <summary>
        /// Builds the platform-specific file name of the Scintilla native
        /// library module.
        /// </summary>
        /// <returns>
        /// The native library module file name.
        /// </returns>
        private static string GetModuleName(string prefix)
        {
            switch (Utility.GetProcessorArchitecture())
            {
                case _Public.ProcessorArchitecture.Intel:
                case _Public.ProcessorArchitecture.IA32_on_Win64:
                    {
                        return prefix;
                    }
                case _Public.ProcessorArchitecture.ARM:
                    {
                        return prefix + "ARM";
                    }
                case _Public.ProcessorArchitecture.ARM64:
                    {
                        return prefix + "ARM64";
                    }
                case _Public.ProcessorArchitecture.IA64:
                    {
                        return prefix + "Itanium";
                    }
                case _Public.ProcessorArchitecture.AMD64:
                    {
                        return prefix + "64";
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the version of the Scintilla native library.
        /// </summary>
        /// <returns>
        /// The native library version, or null when unavailable.
        /// </returns>
        public static string GetNativeLibraryVersion()
        {
            try
            {
                string directory = NativeLibraryDirectory;

                if (String.IsNullOrEmpty(directory) ||
                    !Directory.Exists(directory))
                {
                    return null;
                }

                string fileNameOnly = NativeLibraryFileName;

                if (String.IsNullOrEmpty(fileNameOnly))
                    return null;

                fileNameOnly += FileExtension.Library; /* HACK: Win32 only. */

                string fileName;

                if (Path.IsPathRooted(fileNameOnly))
                {
                    fileName = fileNameOnly;
                }
                else
                {
                    fileName = Path.Combine(
                        directory, fileNameOnly);
                }

                if (String.IsNullOrEmpty(fileName) ||
                    !File.Exists(fileName))
                {
                    return null;
                }

                return FileVersionInfo.GetVersionInfo(
                    fileName).FileVersion; /* throw */
            }
            catch (Exception e)
            {
                LogOps.Complain(ReturnCode.Error, e);
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Pre-loads the Scintilla native library so the control can be
        /// created.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used while loading the library.
        /// </param>
        /// <param name="resetCancel">
        /// Non-zero to reset the script cancellation flag first.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode PreLoadNativeLibrary(
            Interpreter interpreter, /* in */
            bool resetCancel,        /* in */
            ref Result error         /* out */
            )
        {
            ReturnCode code;
            Result result = null;

            //
            // NOTE: Point ScintillaNET at the same directory we pre-load
            //       (and lock) the native Scintilla / Lexilla modules
            //       from, so both sides resolve the identical files.
            //
            if (!String.IsNullOrEmpty(NativeLibraryDirectory) &&
                Path.IsPathRooted(NativeLibraryDirectory))
            {
                Scintilla.SetModulePath(NativeLibraryDirectory);
            }

            code = ScriptOps.Evaluate(
                interpreter, NativeLibraryPreLoadScript, false, false,
                resetCancel, false, ref result);

            if (code != ReturnCode.Ok)
                error = result;

            code = ScriptOps.Evaluate(
                interpreter, NativeLibraryCleanupScript, false, false,
                resetCancel, false, ref result);

            if (code != ReturnCode.Ok)
                error = result;

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the embedded managed resource name to use for the current
        /// configuration (variant-specific when available, otherwise the
        /// default).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose variant selects the resource.
        /// </param>
        /// <returns>
        /// The managed resource name.
        /// </returns>
        private static string GetManagedResourceName(
            Interpreter interpreter /* in */
            )
        {
            Result value = null;
            Result error = null; /* NOT USED */

            if ((interpreter != null) && interpreter.GetVariableValue(
                    VariableFlags.None, ThemeVariableName,
                    ref value, ref error) == ReturnCode.Ok)
            {
                return String.Format(BaseManagedResourceName, value);
            }

            return DefaultManagedResourceName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs the initial (pre-)configuration of the Scintilla control
        /// before its main configuration.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate configuration scripts.
        /// </param>
        /// <param name="scintilla">
        /// The Scintilla control to configure.
        /// </param>
        /// <param name="isolated">
        /// Non-zero when configuring in an isolated context.
        /// </param>
        /// <param name="resetCancel">
        /// Non-zero to reset the script cancellation flag first.
        /// </param>
        public static void PreConfigure(
            Interpreter interpreter, /* in */
            Scintilla scintilla,     /* in */
            bool isolated,           /* in */
            bool resetCancel         /* in */
            )
        {
            #region Load Scintilla.NET 2.x / 3.x Pre-Configuration
            Result value = null;
            Result error = null; /* NOT USED */

            if ((interpreter != null) && interpreter.GetVariableValue(
                    VariableFlags.None, PreConfigureScriptVariableName,
                    ref value, ref error) == ReturnCode.Ok)
            {
                string text = value;
                ReturnCode code;
                Result result = null;

                code = ScriptOps.Evaluate(
                    interpreter, text, ObjectName, scintilla,
                    isolated, true, resetCancel, false,
                    ref result);

                if (code != ReturnCode.Ok)
                    LogOps.Complain(interpreter, code, result);
            }
            #endregion
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the Scintilla control for editing Eagle scripts
        /// (applying the lexer, styles, and resources).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate configuration scripts.
        /// </param>
        /// <param name="scintilla">
        /// The Scintilla control to configure.
        /// </param>
        /// <param name="isolated">
        /// Non-zero when configuring in an isolated context.
        /// </param>
        /// <param name="resetCancel">
        /// Non-zero to reset the script cancellation flag first.
        /// </param>
        public static void Configure(
            Interpreter interpreter, /* in */
            Scintilla scintilla,     /* in */
            bool isolated,           /* in */
            bool resetCancel         /* in */
            )
        {
            ReturnCode code; /* REUSED */
            Result result; /* REUSED */
            Result value; /* REUSED */
            Result error; /* REUSED */
            string resourceName; /* REUSED */

            ///////////////////////////////////////////////////////////////////

            #region Load Scintilla.NET 2.x / 3.x Shared Support Script File
            resourceName = SupportManagedResourceName;
            error = null;

            using (Stream stream = ManagerOps.GetResourceStream(
                    interpreter, resourceName, ref error))
            {
                if (stream != null)
                {
                    using (StreamReader streamReader = new StreamReader(
                            stream))
                    {
                        string text = null;

                        result = null;

                        code = Engine.ReadScriptStream(
                            interpreter, resourceName, streamReader,
                            0, Count.Invalid, ref text, ref result);

                        if (code == ReturnCode.Ok)
                        {
                            code = ScriptOps.Evaluate(
                                interpreter, text, ObjectName, scintilla,
                                isolated, true, resetCancel, false,
                                ref result);
                        }

                        if (code != ReturnCode.Ok)
                        {
                            LogOps.Complain(interpreter,
                                ReturnCode.Error, String.Format(
                                "could not configure Scintilla 2.x / 3.x " +
                                "using shared script resource {0}: {1}",
                                Utility.FormatWrapOrNull(resourceName),
                                Utility.FormatWrapOrNull(result)));
                        }
                    }
                }
                else
                {
                    LogOps.Complain(interpreter,
                        ReturnCode.Error, String.Format(
                        "could not configure Scintilla 2.x / 3.x " +
                        "using shared script resource {0}: {1}",
                        Utility.FormatWrapOrNull(resourceName),
                        Utility.FormatWrapOrNull(error)));
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Load Scintilla.NET 3.x Configuration From File
#if SCINTILLA_30
            value = null;
            error = null;

            if ((interpreter != null) && interpreter.GetVariableValue(
                    VariableFlags.None, ConfigureFileNameVariableName,
                    ref value, ref error) == ReturnCode.Ok)
            {
                string fileName = value;

                if (File.Exists(fileName))
                {
                    result = null;

                    code = ScriptOps.Evaluate(
                        interpreter, fileName, ObjectName, scintilla,
                        isolated, true, resetCancel, true, ref result);

                    if (code == ReturnCode.Ok)
                    {
                        goto configure;
                    }
                    else
                    {
                        LogOps.Complain(interpreter,
                            ReturnCode.Error, String.Format(
                            "could not configure Scintilla 3.x " +
                            "using script file name {0}: {1}",
                            Utility.FormatWrapOrNull(fileName),
                            Utility.FormatWrapOrNull(result)));
                    }
                }
                else
                {
                    LogOps.Complain(interpreter,
                        ReturnCode.Error, String.Format(
                        "cannot configure Scintilla 3.x using " +
                        "script file name {0}, it does not exist",
                        Utility.FormatWrapOrNull(fileName)));
                }
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Load Scintilla.NET 3.x Configuration From Resource
#if SCINTILLA_30
            resourceName = GetManagedResourceName(interpreter);
            error = null;

            using (Stream stream = ManagerOps.GetResourceStream(
                    interpreter, resourceName, ref error))
            {
                if (stream != null)
                {
                    using (StreamReader streamReader = new StreamReader(
                            stream))
                    {
                        string text = null;

                        result = null;

                        code = Engine.ReadScriptStream(
                            interpreter, resourceName, streamReader,
                            0, Count.Invalid, ref text, ref result);

                        if (code == ReturnCode.Ok)
                        {
                            code = ScriptOps.Evaluate(
                                interpreter, text, ObjectName, scintilla,
                                isolated, true, resetCancel, false,
                                ref result);
                        }

                        if (code == ReturnCode.Ok)
                        {
                            goto configure;
                        }
                        else
                        {
                            LogOps.Complain(interpreter,
                                ReturnCode.Error, String.Format(
                                "could not configure Scintilla 3.x " +
                                "using script resource {0}: {1}",
                                Utility.FormatWrapOrNull(resourceName),
                                Utility.FormatWrapOrNull(result)));
                        }
                    }
                }
                else
                {
                    LogOps.Complain(interpreter,
                        ReturnCode.Error, String.Format(
                        "could not configure Scintilla 3.x " +
                        "using script resource {0}: {1}",
                        Utility.FormatWrapOrNull(resourceName),
                        Utility.FormatWrapOrNull(error)));
                }
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Check Scintilla.NET Instance & Configuration Manager
#if !SCINTILLA_30
            if (scintilla == null)
                return;

            ConfigurationManager configurationManager =
                scintilla.ConfigurationManager;

            if (configurationManager == null)
                return;
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Load Scintilla.NET 2.x Configuration From File
#if !SCINTILLA_30
            value = null;
            error = null;

            if ((interpreter != null) && interpreter.GetVariableValue(
                    VariableFlags.None, ConfigureFileNameVariableName,
                    ref value, ref error) == ReturnCode.Ok)
            {
                string fileName = value;

                if (File.Exists(fileName))
                {
                    configurationManager.Configure(new Configuration(
                        fileName, LanguageName, true));

                    goto configure;
                }
                else
                {
                    LogOps.Complain(interpreter,
                        ReturnCode.Error, String.Format(
                        "cannot configure Scintilla 2.x using " +
                        "XML file name {0}, it does not exist",
                        Utility.FormatWrapOrNull(fileName)));
                }
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Load Scintilla.NET 2.x Configuration From Resource
#if !SCINTILLA_30
            resourceName = GetManagedResourceName(interpreter);
            error = null;

            using (Stream stream = ManagerOps.GetResourceStream(
                    interpreter, resourceName, ref error))
            {
                if (stream != null)
                {
                    configurationManager.Configure(new Configuration(
                        stream, LanguageName, true));

                    goto configure;
                }
                else
                {
                    LogOps.Complain(interpreter,
                        ReturnCode.Error, String.Format(
                        "could not configure Scintilla 2.x " +
                        "using script resource {0}: {1}",
                        Utility.FormatWrapOrNull(resourceName),
                        Utility.FormatWrapOrNull(error)));

                }
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

        configure:

            ///////////////////////////////////////////////////////////////////

            #region Setup Scintilla.NET 2.x / 3.x Properties
            SetProperties(interpreter, scintilla, isolated, resetCancel);
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Load Scintilla.NET 2.x / 3.x Configuration
            value = null;
            error = null;

            if ((interpreter != null) && interpreter.GetVariableValue(
                    VariableFlags.None, ConfigureScriptVariableName,
                    ref value, ref error) == ReturnCode.Ok)
            {
                string text = value;

                result = null;

                code = ScriptOps.Evaluate(
                    interpreter, text, ObjectName, scintilla,
                    isolated, true, resetCancel, false,
                    ref result);

                if (code != ReturnCode.Ok)
                    LogOps.Complain(interpreter, code, result);
            }
            #endregion
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the Scintilla control's editing properties (such as tabs,
        /// indentation, and folding).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate configuration scripts.
        /// </param>
        /// <param name="scintilla">
        /// The Scintilla control whose properties are set.
        /// </param>
        /// <param name="isolated">
        /// Non-zero when configuring in an isolated context.
        /// </param>
        /// <param name="resetCancel">
        /// Non-zero to reset the script cancellation flag first.
        /// </param>
        private static void SetProperties(
            Interpreter interpreter, /* in */
            Scintilla scintilla,     /* in */
            bool isolated,           /* in */
            bool resetCancel         /* in */
            )
        {
            #region Default Scintilla.NET 2.x / 3.x Property Values
#if SCINTILLA_30
            // if (scintilla == null)
            //     return;
            //
            // scintilla.ViewEol = true;
            // scintilla.ViewWhitespace = WhitespaceMode.VisibleAlways;
            // scintilla.WrapMode = WrapMode.None;
            // scintilla.WrapIndentMode = WrapIndentMode.Fixed;
#else
            // if (scintilla == null)
            //     return;
            //
            // scintilla.EndOfLine.IsVisible = true;
            // scintilla.Whitespace.Mode = WhitespaceMode.VisibleAlways;
            // scintilla.LineWrapping.Mode = LineWrappingMode.None;
            // scintilla.LineWrapping.IndentMode = LineWrappingIndentMode.Fixed;
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Load Scintilla.NET 2.x / 3.x Property Values
            Result value = null;
            Result error = null; /* NOT USED */

            if ((interpreter != null) && interpreter.GetVariableValue(
                    VariableFlags.None, SetPropertyScriptVariableName,
                    ref value, ref error) == ReturnCode.Ok)
            {
                string text = value;
                ReturnCode code;
                Result result = null;

                code = ScriptOps.Evaluate(
                    interpreter, text, ObjectName, scintilla,
                    isolated, true, resetCancel, false,
                    ref result);

                if (code != ReturnCode.Ok)
                    LogOps.Complain(interpreter, code, result);
            }
            #endregion
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a font derived from the supplied font at the specified
        /// size.
        /// </summary>
        /// <param name="oldFont">
        /// The font to derive the new font from.
        /// </param>
        /// <param name="emSize">
        /// The em-size, in points, of the new font.
        /// </param>
        /// <returns>
        /// The created font.
        /// </returns>
        public static Font MakeFont(
            Font oldFont, /* in */
            float emSize  /* in */
            )
        {
            if (oldFont == null)
                return null;

            return new Font(
                oldFont.FontFamily, emSize, oldFont.Style, oldFont.Unit,
                oldFont.GdiCharSet, oldFont.GdiVerticalFont);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the width of the line-number margin (margin zero) based on the
        /// line count and font.
        /// </summary>
        /// <param name="scintilla">
        /// The Scintilla control whose margin is sized.
        /// </param>
        /// <param name="lineCount">
        /// The number of lines used to size the margin.
        /// </param>
        /// <param name="font">
        /// The font used to measure the margin width.
        /// </param>
        public static void SetMargin0Width(
            Scintilla scintilla, /* in */
            int lineCount,       /* in */
            Font font            /* in */
            )
        {
            if (scintilla == null)
                return;

            scintilla.Margins[0].Width = TextRenderer.MeasureText(
                lineCount.ToString(), font).Width;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the text (or selected text) of the supplied text control.
        /// </summary>
        /// <param name="textBox">
        /// The text control to read.
        /// </param>
        /// <param name="selected">
        /// Non-zero to get only the selected text.
        /// </param>
        /// <param name="text">
        /// On output, receives the retrieved text.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool GetText(
            Scintilla textBox, /* in */
            bool selected,     /* in */
            ref string text    /* out */
            )
        {
            bool result;
            string localText = null;

            result = WinFormsOps.Invoke(
                    textBox, new DelegateWithNoArgs(delegate()
            {
                if (selected)
                {
#if SCINTILLA_30
                    string selectedText = textBox.SelectedText;

                    if (!String.IsNullOrEmpty(selectedText))
                        localText = selectedText;
                    else
                        localText = null;
#else
                    Selection selection = textBox.Selection;

                    if ((selection != null) && (selection.Length > 0))
                        localText = selection.Text;
                    else
                        localText = null;
#endif
                }
                else
                {
                    localText = textBox.Text;
                }
            }), true);

            if (result)
                text = localText;

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends text to the specified target control of the given type.
        /// </summary>
        /// <param name="type">
        /// The type of the target control.
        /// </param>
        /// <param name="target">
        /// The target control to append to.
        /// </param>
        /// <param name="text">
        /// The text to append.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool AppendText(
            Type type,     /* in */
            object target, /* in */
            string text    /* in */
            )
        {
            try
            {
                type.InvokeMember("AppendText",
                    BindingFlags.Public | BindingFlags.Instance |
                    BindingFlags.InvokeMethod, null, target,
                    new object[] { text });

                return true;
            }
            catch
            {
                // do nothing.
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends text to the supplied control, optionally asynchronously.
        /// </summary>
        /// <param name="control">
        /// The control to append to.
        /// </param>
        /// <param name="text">
        /// The text to append.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to append without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool AppendText(
            Control control,  /* in */
            string text,      /* in */
            bool asynchronous /* in */
            )
        {
            if (asynchronous)
            {
                return WinFormsOps.BeginInvoke(
                        control, new DelegateWithNoArgs(delegate()
                {
                    if (!AppendText(control.GetType(), control, text))
                    {
                        control.Text += text;
                    }
                }), true);
            }
            else
            {
                return WinFormsOps.Invoke(
                        control, new DelegateWithNoArgs(delegate()
                {
                    if (!AppendText(control.GetType(), control, text))
                    {
                        control.Text += text;
                    }
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Simulates a click in the supplied text control, optionally
        /// asynchronously.
        /// </summary>
        /// <param name="textBox">
        /// The text control to click.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to click without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool Click(
            Scintilla textBox, /* in */
            bool asynchronous  /* in */
            )
        {
            if (asynchronous)
            {
                return WinFormsOps.BeginInvoke(
                        textBox, new DelegateWithNoArgs(delegate()
                {
#if SCINTILLA_30
                    textBox.GotoPosition(textBox.TextLength);
#else
                    CaretInfo caretInfo = textBox.Caret;

                    if (caretInfo != null)
                        caretInfo.Goto(textBox.TextLength);
#endif
                }), true);
            }
            else
            {
                return WinFormsOps.Invoke(
                        textBox, new DelegateWithNoArgs(delegate()
                {
#if SCINTILLA_30
                    textBox.GotoPosition(textBox.TextLength);
#else
                    CaretInfo caretInfo = textBox.Caret;

                    if (caretInfo != null)
                        caretInfo.Goto(textBox.TextLength);
#endif
                }), true);
            }
        }
        #endregion
    }
}
