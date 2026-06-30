/*
 * DotfuscatorHack.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Dotfuscator Community Edition Automation Tool
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;

////////////////////////////////////////////////////////////////////////////////

#region Compile / Build Commands
//
// csc.exe "/out:..\DotfuscatorHack.exe" /reference:UIAutomationClient.dll
//     /reference:UIAutomationTypes.dll /debug:pdbonly /optimize+ /delaysign+
//     "/keyfile:..\..\..\..\..\..\Keys\EagleEnterprisePluginRootPublic.snk"
//     "DotfuscatorHack.cs"
//
#endregion

////////////////////////////////////////////////////////////////////////////////

#region Assembly Metadata
[assembly: AssemblyTitle("DotfuscatorHack Tool")]
[assembly: AssemblyDescription("Helps to automate Dotfuscator Community Edition.")]
[assembly: AssemblyCompany("Eagle Development Team")]
[assembly: AssemblyProduct("Eagle")]
[assembly: AssemblyCopyright("Copyright © 2007-2012 by Joe Mistachkin.  All rights reserved.")]
[assembly: ComVisible(false)]
[assembly: Guid("b39ee6cf-1250-48e1-bef7-63f98e7c87e9")]
[assembly: AssemblyVersion("1.0.*")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
#endregion

////////////////////////////////////////////////////////////////////////////////

namespace Tools
{
    /// <summary>
    /// This class implements a command-line tool that drives the Dotfuscator
    /// Community Edition user interface, via UI Automation, in order to build
    /// (and optionally save) a project and then capture its output.
    /// </summary>
    public class DotfuscatorHack
    {
        #region Private Constants
        /// <summary>
        /// The file name (without directory) of the executable for this tool,
        /// used when displaying usage information.
        /// </summary>
        private static string ExecutableFileName =
            Path.GetFileName(Assembly.GetExecutingAssembly().Location);
        #endregion

        ////////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// This method invokes the save button in the specified Dotfuscator
        /// window and then waits for the specified amount of time.
        /// </summary>
        /// <param name="hWnd">
        /// The native window handle of the Dotfuscator window to automate.
        /// </param>
        /// <param name="milliseconds">
        /// The number of milliseconds to wait after invoking the save button.
        /// </param>
        /// <param name="error">
        /// Upon failure, this parameter will be set to an error message
        /// describing the problem that was encountered.
        /// </param>
        /// <returns>
        /// True if the save operation was invoked successfully; otherwise,
        /// false.
        /// </returns>
        private static bool SaveAndWait(
            IntPtr hWnd,      /* in */
            int milliseconds, /* in */
            ref string error  /* out */
            )
        {
            try
            {
                AutomationElement automationElement =
                    AutomationElement.FromHandle(hWnd);

                if (automationElement == null)
                {
                    error = String.Format(
                        "cannot find window {0} for save",
                        hWnd);

                    return false;
                }

                AutomationElement buttonElement =
                    automationElement.FindFirst(
                        TreeScope.Subtree, new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ControlTypeProperty,
                                ControlType.Button),
                            new PropertyCondition(
                                AutomationElement.AutomationIdProperty,
                                "Button_3")));

                if (buttonElement == null)
                {
                    error = "cannot find save button control";
                    return false;
                }

                InvokePattern invokePattern =
                    buttonElement.GetCurrentPattern(
                        InvokePattern.Pattern) as InvokePattern;

                if (invokePattern == null)
                {
                    error = "cannot get invoke pattern for save";
                    return false;
                }

                invokePattern.Invoke();      // NOTE: Save.

                Console.Write(String.Format( // NOTE: Show.
                    "Waiting for {0} milliseconds... ", milliseconds));

                Thread.Sleep(milliseconds);  // NOTE: Wait.
                Console.WriteLine("done.");  // NOTE: Done.

                //
                // NOTE: We should be all done now and everything succeeded.
                //
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
            }

            return false;
        }

        ////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method invokes the build button in the specified Dotfuscator
        /// window, waits for the specified amount of time, captures the build
        /// output text, and then closes the window.
        /// </summary>
        /// <param name="hWnd">
        /// The native window handle of the Dotfuscator window to automate.
        /// </param>
        /// <param name="milliseconds">
        /// The number of milliseconds to wait after invoking the build button.
        /// </param>
        /// <param name="output">
        /// Upon success, this parameter will be set to the text captured from
        /// the build output control.
        /// </param>
        /// <param name="error">
        /// Upon failure, this parameter will be set to an error message
        /// describing the problem that was encountered.
        /// </param>
        /// <returns>
        /// True if the build operation was invoked, its output captured, and
        /// the window closed successfully; otherwise, false.
        /// </returns>
        private static bool BuildWaitThenClose(
            IntPtr hWnd,       /* in */
            int milliseconds,  /* in */
            ref string output, /* in, out */
            ref string error   /* out */
            )
        {
            try
            {
                AutomationElement automationElement =
                    AutomationElement.FromHandle(hWnd);

                if (automationElement == null)
                {
                    error = String.Format(
                        "cannot find window {0} for build",
                        hWnd);

                    return false;
                }

                AutomationElement buttonElement =
                    automationElement.FindFirst(
                        TreeScope.Subtree, new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ControlTypeProperty,
                                ControlType.Button),
                            new PropertyCondition(
                                AutomationElement.AutomationIdProperty,
                                "btnTbBuild")));

                if (buttonElement == null)
                {
                    error = "cannot find build button control";
                    return false;
                }

                InvokePattern invokePattern =
                    buttonElement.GetCurrentPattern(
                        InvokePattern.Pattern) as InvokePattern;

                if (invokePattern == null)
                {
                    error = "cannot get invoke pattern for build";
                    return false;
                }

                AutomationElement tabControlElement =
                    automationElement.FindFirst(
                        TreeScope.Subtree, new AndCondition(
                            new PropertyCondition(
                                AutomationElement.ControlTypeProperty,
                                ControlType.Tab),
                            new PropertyCondition(
                                AutomationElement.AutomationIdProperty,
                                "tcOutput")));

                if (tabControlElement == null)
                {
                    error = "cannot find output tab control";
                    return false;
                }

                AutomationElement editElement =
                    tabControlElement.FindFirst(
                        TreeScope.Subtree, new PropertyCondition(
                            AutomationElement.ControlTypeProperty,
                            ControlType.Edit));

                if (editElement == null)
                {
                    error = "cannot find output edit control";
                    return false;
                }

                TextPattern textPattern =
                    editElement.GetCurrentPattern(
                        TextPattern.Pattern) as TextPattern;

                if (textPattern == null)
                {
                    error = "cannot get text pattern for output";
                    return false;
                }

                TextPatternRange textPatternRange = textPattern.DocumentRange;

                if (textPatternRange == null)
                {
                    error = "cannot get text pattern range for output";
                    return false;
                }

                WindowPattern windowPattern =
                    automationElement.GetCurrentPattern(
                        WindowPattern.Pattern) as WindowPattern;

                if (windowPattern == null)
                {
                    error = "cannot get window pattern for close";
                    return false;
                }

                invokePattern.Invoke();                // NOTE: Build.

                Console.Write(String.Format(           // NOTE: Show.
                    "Waiting for {0} milliseconds... ", milliseconds));

                Thread.Sleep(milliseconds);            // NOTE: Wait.
                Console.WriteLine("done.");            // NOTE: Done.

                output = textPatternRange.GetText(-1); // NOTE: Output.
                windowPattern.Close();                 // NOTE: Close.

                //
                // NOTE: We should be all done now and everything succeeded.
                //
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
            }

            return false;
        }

        ////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method attempts to parse the specified string as a native
        /// pointer value, using a width appropriate for the current process.
        /// </summary>
        /// <param name="text">
        /// The string to parse as a native pointer value.
        /// </param>
        /// <param name="value">
        /// Upon success, this parameter will be set to the parsed native
        /// pointer value; otherwise, it will be set to zero.
        /// </param>
        /// <returns>
        /// True if the string was parsed successfully; otherwise, false.
        /// </returns>
        private static bool TryParseIntPtr(
            string text,     /* in */
            out IntPtr value /* out */
            )
        {
            value = IntPtr.Zero;

            if (IntPtr.Size == sizeof(long)) /* 64-bit? */
            {
                long longValue;

                if (long.TryParse(text, out longValue))
                {
                    value = new IntPtr(longValue);
                    return true;
                }
            }
            else
            {
                int intValue;

                if (int.TryParse(text, out intValue))
                {
                    value = new IntPtr(intValue);
                    return true;
                }
            }

            return false;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// This method is the entry point for the tool.  It parses the
        /// command-line arguments (a window handle, a wait time in
        /// milliseconds, and a flag indicating whether to save first), then
        /// performs the requested save and build operations.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments passed to the tool.
        /// </param>
        /// <returns>
        /// Zero on success; non-zero on failure.
        /// </returns>
        public static int Main(string[] args)
        {
            int exitCode = 0; /* SUCCESS */

            if (args.Length == 3) /* <hWnd> <milliseconds> <save?> */
            {
                IntPtr hWnd;

                if (TryParseIntPtr(args[0], out hWnd))
                {
                    int milliseconds;

                    if (int.TryParse(args[1], out milliseconds))
                    {
                        bool save;

                        if (bool.TryParse(args[2], out save))
                        {
                            string output = null;
                            string error = null;

                            if (!save || SaveAndWait(
                                    hWnd, milliseconds, ref error))
                            {
                                if (BuildWaitThenClose(
                                        hWnd, milliseconds, ref output,
                                        ref error))
                                {
                                    Console.WriteLine(output);
                                }
                                else
                                {
                                    Console.WriteLine(error);
                                    exitCode = 1; /* FAILURE */
                                }
                            }
                            else
                            {
                                Console.WriteLine(error);
                                exitCode = 1; /* FAILURE */
                            }
                        }
                        else
                        {
                            Console.WriteLine(String.Format(
                                "cannot parse \"{0}\" as boolean save flag",
                                args[2]));

                            exitCode = 1; /* FAILURE */
                        }
                    }
                    else
                    {
                        Console.WriteLine(String.Format(
                            "cannot parse \"{0}\" as integer milliseconds",
                            args[1]));

                        exitCode = 1; /* FAILURE */
                    }
                }
                else
                {
                    Console.WriteLine(String.Format(
                        "cannot parse \"{0}\" as window handle",
                        args[0]));

                    exitCode = 1; /* FAILURE */
                }
            }
            else
            {
                Console.WriteLine(String.Format(
                    "usage: {0} <hWnd> <milliseconds> <save?>{1}",
                    ExecutableFileName, Environment.NewLine));

                exitCode = 1; /* FAILURE */
            }

            return exitCode;
        }
        #endregion
    }
}
