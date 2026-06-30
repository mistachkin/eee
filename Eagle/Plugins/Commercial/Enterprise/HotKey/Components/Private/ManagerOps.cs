/*
 * ManagerOps.cs --
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

#if !SCINTILLA_30
using System.Text;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;

#if !SCINTILLA_30
using Eagle._Constants;
using Eagle._Containers.Public;
#endif

namespace HotKey.Components.Private
{
    /// <summary>
    /// Provides helpers used by the hot-key manager to access the plugin's
    /// embedded resources (optionally repairing Scintilla markup data) and to
    /// resolve the plugin's title and working directories.
    /// </summary>
    [ObjectId("995324a4-0c12-43ff-a429-1c1a32b6815b")]
    internal static class ManagerOps
    {
        #region Private Constants
#if !SCINTILLA_30
        /// <summary>
        /// The name of the script procedure invoked to repair Scintilla.NET
        /// XML markup data.
        /// </summary>
        private static readonly string FixXmlDataProcedureName =
            "::fixScintillaNetXmlData";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The encoding used when re-encoding repaired XML markup data.
        /// </summary>
        private static readonly Encoding XmlEncoding = Encoding.UTF8;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hot-Key Manager Methods
#if !SCINTILLA_30
        /// <summary>
        /// When the named resource is a markup (XML) file, passes its content
        /// through the repair procedure and replaces the stream with the
        /// repaired data; non-markup resources are left unchanged.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the repair procedure.
        /// </param>
        /// <param name="name">
        /// The resource name, used to detect markup files by extension.
        /// </param>
        /// <param name="stream">
        /// On input, the resource stream; on output, the repaired stream (or
        /// null on failure).
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        private static void MaybeFixXmlStreamData(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Stream stream,       /* in, out */
            ref Result error         /* out */
            )
        {
            if ((interpreter == null) || (stream == null))
                return;

            try
            {
                if (Utility.ComparePathParts(
                        Path.GetExtension(name), FileExtension.Markup) != 0)
                {
                    return;
                }

                using (StreamReader streamReader = new StreamReader(stream))
                {
                    StringList command = new StringList();

                    command.Add(FixXmlDataProcedureName);
                    command.Add(streamReader.ReadToEnd()); /* throw */

                    Encoding encoding = XmlEncoding;
                    Result result = null;

                    if ((encoding != null) && (interpreter.EvaluateScript(
                            command.ToString(), ref result) == ReturnCode.Ok))
                    {
                        stream = new MemoryStream(
                            encoding.GetBytes(result)); /* throw */
                    }
                    else
                    {
                        stream = null;
                        error = null;
                    }
                }
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(ManagerOps).Name,
                    TracePriority.Highest);
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a stream for the named embedded resource, complaining (rather
        /// than failing) when it cannot be obtained.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used when post-processing the resource.
        /// </param>
        /// <param name="name">
        /// The name of the resource to open.
        /// </param>
        /// <returns>
        /// The resource stream, or null on failure.
        /// </returns>
        public static Stream GetResourceStream(
            Interpreter interpreter, /* in */
            string name              /* in */
            )
        {
            Stream stream;
            Result error = null;

            stream = GetResourceStream(interpreter, name, ref error);

            if (stream == null)
                LogOps.Complain(ReturnCode.Error, error);

            return stream;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a stream for the named embedded resource from the executing
        /// assembly, repairing markup data when applicable.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used when post-processing the resource.
        /// </param>
        /// <param name="name">
        /// The name of the resource to open.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The resource stream, or null on failure.
        /// </returns>
        public static Stream GetResourceStream(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            Stream stream = Utility.GetStream(
                Assembly.GetExecutingAssembly(), name, ref error);

#if !SCINTILLA_30
            MaybeFixXmlStreamData(
                interpreter, name, ref stream, ref error);
#endif

            return stream;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the title of the plugin assembly.
        /// </summary>
        /// <returns>
        /// The assembly title.
        /// </returns>
        public static string GetTitle()
        {
            return Utility.GetAssemblyTitle(
                Assembly.GetExecutingAssembly());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the directory containing the plugin assembly, falling back to
        /// the current directory when the location is unavailable.
        /// </summary>
        /// <returns>
        /// The assembly directory, or null on error.
        /// </returns>
        public static string GetDirectory()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();

                return (assembly != null) ?
                    Path.GetDirectoryName(assembly.Location) :
                    Directory.GetCurrentDirectory(); /* EXEMPT */
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(ManagerOps).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current user's documents directory.
        /// </summary>
        /// <returns>
        /// The user's documents directory path.
        /// </returns>
        public static string GetUserDirectory()
        {
            return Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);
        }
        #endregion
    }
}
