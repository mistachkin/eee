/*
 * IconOps.cs --
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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;

#if NATIVE && WINDOWS
using System.Runtime.InteropServices;
using System.Security;

#if !NET_40
using System.Security.Permissions;
#endif
#endif

using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;

using StringElementData = HotKey.Components.Private.ListElementData<
    string, string>;

using ElementPair = System.Collections.Generic.KeyValuePair<
    string, HotKey.Components.Private.ListElementData<string, string>>;

using ElementDictionary = System.Collections.Generic.Dictionary<
    string, HotKey.Components.Private.ListElementData<string, string>>;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Provides icon and executable-file metadata helpers used to populate the
    /// list-view controls.  It extracts icons (via the native Win32 API where
    /// available, otherwise the associated-icon API), reads executable version
    /// information, and builds list-view items for executable files.
    /// </summary>
#if NATIVE && WINDOWS
#if NET_40
    [SecurityCritical()]
#else
    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
#endif
#endif
    [ObjectId("3cab856f-e697-44a3-ba08-26682badbded")]
    internal static class IconOps
    {
        #region Unsafe Native Methods Class
#if NATIVE && WINDOWS
        /// <summary>
        /// Contains the native Win32 icon functions (Shell32 and User32) used
        /// to extract and destroy icons.
        /// </summary>
        [SuppressUnmanagedCodeSecurity()]
        [ObjectId("e08efbc5-d660-465a-bb7e-b4fbe13f90aa")]
        internal static class UnsafeNativeMethods
        {
            #region Windows Native Icon Methods
            /// <summary>
            /// Extracts an icon from a file (Shell32 <c>ExtractIcon</c>).
            /// </summary>
            /// <param name="hInstance">
            /// A handle to the calling module instance.
            /// </param>
            /// <param name="fileName">
            /// The file to extract the icon from.
            /// </param>
            /// <param name="iconIndex">
            /// The zero-based index of the icon to extract.
            /// </param>
            /// <returns>
            /// A handle to the extracted icon, or zero on failure.
            /// </returns>
            [DllImport(DllName.Shell32,
                CallingConvention = CallingConvention.Winapi,
                CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern IntPtr ExtractIcon(
                IntPtr hInstance, string fileName, uint iconIndex);

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Destroys an icon and frees its resources (User32
            /// <c>DestroyIcon</c>).
            /// </summary>
            /// <param name="hIcon">
            /// A handle to the icon to destroy.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            [DllImport(DllName.User32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DestroyIcon(IntPtr hIcon);
            #endregion
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
        /// <summary>
        /// The format string used to build the display text for an executable
        /// file (description and file name).
        /// </summary>
        private const string ExecutableTextFormat = "{0} ({1})";

        ///////////////////////////////////////////////////////////////////////

#if NATIVE && WINDOWS
        //
        // HACK: Get a reference to the private "ownHandle" field of the Icon
        //       class so that we can manually set the value to true when
        //       extracting icons from native executable files for use with
        //       the list view control.
        //
        /// <summary>
        /// A cached reference to the private <c>ownHandle</c> field of the
        /// <see cref="Icon" /> class, used to make extracted icons own (and
        /// thus properly dispose) their native handle.  Null on Mono, which
        /// disables native executable-icon extraction.
        /// </summary>
        private static readonly FieldInfo ownHandleField =
            !Utility.IsMono() ? typeof(Icon).GetField("ownHandle",
            BindingFlags.Instance | BindingFlags.NonPublic) : null;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Icon Extraction Methods
        #region Native Icon Extraction Methods
#if NATIVE && WINDOWS
        /// <summary>
        /// Gets the native module instance handle (HINSTANCE) of the plugin
        /// assembly, used when extracting icons.
        /// </summary>
        /// <returns>
        /// The module instance handle, or zero when unavailable.
        /// </returns>
        private static IntPtr GetHotKeyManagerHINSTANCE()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            if (assembly != null)
            {
                Module module = assembly.ManifestModule;

                if (module != null)
                    return Marshal.GetHINSTANCE(module);
            }

            return IntPtr.Zero;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the first icon from the specified file using the native
        /// Win32 API, complaining (rather than failing) on error.
        /// </summary>
        /// <param name="fileName">
        /// The file to extract the icon from.
        /// </param>
        /// <returns>
        /// The extracted icon, or null on failure.
        /// </returns>
        private static Icon ExtractIcon(
            string fileName /* in */
            )
        {
            Result error = null;

            return ExtractIcon(fileName, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the first icon from the specified file using the native
        /// Win32 API, ensuring the returned icon owns its native handle so it
        /// can be disposed.  Extraction is disabled when the
        /// <c>ownHandle</c> field is unavailable (for example, on Mono).
        /// </summary>
        /// <param name="fileName">
        /// The file to extract the icon from.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The extracted icon, or null on failure.
        /// </returns>
        private static Icon ExtractIcon(
            string fileName, /* in */
            ref Result error /* out */
            )
        {
            //
            // NOTE: Prior to extracting the icon, make sure we can fully
            //       dispose of it properly via the private "ownHandle" field
            //       of the Icon class.  If this field is not available (e.g.
            //       on Mono), then executable file icon extraction using the
            //       native Win32 API will effectively be disabled.
            //
            if (ownHandleField != null)
            {
                bool success = false;
                IntPtr hIcon = IntPtr.Zero;

                try
                {
                    hIcon = UnsafeNativeMethods.ExtractIcon(
                        GetHotKeyManagerHINSTANCE(), fileName, 0);

                    if (hIcon != IntPtr.Zero)
                    {
                        Icon icon = Icon.FromHandle(hIcon);

                        if (icon != null)
                        {
                            //
                            // HACK: We really want the native icon to be
                            //       destroyed via the managed object instance
                            //       because these icons are used by the user
                            //       interface.
                            //
                            ownHandleField.SetValue(icon, true);
                            success = true;
                        }

                        return icon;
                    }
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    if (!success && (hIcon != IntPtr.Zero))
                    {
                        if (!UnsafeNativeMethods.DestroyIcon(hIcon))
                        {
                            LogOps.Complain(
                                ReturnCode.Error, "failed to destroy icon");
                        }

                        hIcon = IntPtr.Zero;
                    }
                }
            }

            return null;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the icon associated with the specified file using the
        /// managed associated-icon API, returning null on any error.
        /// </summary>
        /// <param name="fileName">
        /// The file whose associated icon is extracted.
        /// </param>
        /// <returns>
        /// The associated icon, or null on failure.
        /// </returns>
        private static Icon ExtractAssociatedIcon(
            string fileName /* in */
            )
        {
            try
            {
                return Icon.ExtractAssociatedIcon(fileName); /* throw */
            }
            catch
            {
                // do nothing.
            }

            return null;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Executable File Metadata Methods
        /// <summary>
        /// Reads metadata for an executable file: its file info and version
        /// info, and computes the display text and tag.  When the file has a
        /// description, the text combines the description and file name and
        /// the tag is the file name; otherwise, in non-strict ("all") mode the
        /// file name is used as the text with a null tag.
        /// </summary>
        /// <param name="fileName">
        /// The executable file to read metadata from.
        /// </param>
        /// <param name="all">
        /// Non-zero to produce text even when the file has no description.
        /// </param>
        /// <param name="fileInfo">
        /// On output, receives the file info, or null on failure.
        /// </param>
        /// <param name="versionInfo">
        /// On output, receives the version info, or null on failure.
        /// </param>
        /// <param name="text">
        /// On output, receives the computed display text, if any.
        /// </param>
        /// <param name="tag">
        /// On output, receives the computed item tag, if any.
        /// </param>
        private static void GetExecutableFileData(
            string fileName,                 /* in */
            bool all,                        /* in */
            ref FileInfo fileInfo,           /* out */
            ref FileVersionInfo versionInfo, /* out */
            ref string text,                 /* out */
            ref object tag                   /* out */
            )
        {
            fileInfo = null;

            try
            {
                fileInfo = new FileInfo(fileName); /* throw */
            }
            catch
            {
                // do nothing.
            }

            versionInfo = null;

            try
            {
                versionInfo = FileVersionInfo.GetVersionInfo(
                    fileName); /* throw */
            }
            catch
            {
                // do nothing.
            }

            if (versionInfo != null)
            {
                string description = versionInfo.FileDescription;

                if (description != null)
                    description = description.Trim();

                if (!String.IsNullOrEmpty(description))
                {
                    text = String.Format(ExecutableTextFormat,
                        description, Path.GetFileName(fileName));

                    tag = fileName;
                }
                else if (all)
                {
                    text = fileName;
                    tag = null;
                }
            }
            else if (all)
            {
                text = fileName;
                tag = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the sources string stored in the tag of the supplied
        /// list-element pair, if any.
        /// </summary>
        /// <param name="pair">
        /// The element pair whose value tag is examined.
        /// </param>
        /// <returns>
        /// The sources string, or null when not present.
        /// </returns>
        private static string GetExecutableSources(
            ElementPair pair /* in */
            )
        {
            StringElementData data = pair.Value;

            if (data != null)
            {
                string sources = data.Tag as string;

                if (sources != null)
                    return sources;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the list-view items (and their icons) for a set of
        /// executable files.  For each file, an icon is extracted and metadata
        /// columns (company, product, version, description, comments, creation
        /// time, and sources) are populated; files without usable text are
        /// skipped unless "all" is set.
        /// </summary>
        /// <param name="fileNames">
        /// The executable files to build items for, keyed by file name.
        /// </param>
        /// <param name="all">
        /// Non-zero to include files even when they lack a description or
        /// icon.
        /// </param>
        /// <param name="items">
        /// On output, receives the constructed list-view items.
        /// </param>
        /// <param name="icons">
        /// On output, receives the extracted icons keyed by file name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode GetExecutableFileListViewItems(
            ElementDictionary fileNames,         /* in */
            bool all,                            /* in */
            ref ListViewItem[] items,            /* out */
            ref IDictionary<string, Icon> icons, /* out */
            ref Result error                     /* out */
            )
        {
            if (fileNames == null)
            {
                error = "invalid file name list";
                return ReturnCode.Error;
            }

            List<ListViewItem> localItems = new List<ListViewItem>();
            IDictionary<string, Icon> localIcons = new Dictionary<string, Icon>();

            foreach (ElementPair pair in fileNames)
            {
                string fileName = pair.Key;

                if (fileName == null) /* IMPOSSIBLE? */
                    continue;

                Icon icon = null;

#if NATIVE && WINDOWS
                icon = ExtractIcon(fileName);
#else
                icon = ExtractAssociatedIcon(fileName);
#endif

                if (all || (icon != null))
                {
#if NATIVE && WINDOWS
                    if (icon == null)
                        icon = ExtractAssociatedIcon(fileName);
#endif

                    FileInfo fileInfo = null;
                    FileVersionInfo versionInfo = null;
                    string text = null;
                    object tag = null;

                    /* NO RESULT */
                    GetExecutableFileData(
                        fileName, all, ref fileInfo, ref versionInfo, ref text,
                        ref tag);

                    if (text != null)
                    {
                        ListViewItem item = new ListViewItem(text);

                        item.Tag = tag;
                        item.ToolTipText = fileName;
                        item.SubItems.Add(fileName);

                        if (versionInfo != null)
                        {
                            item.SubItems.Add(versionInfo.CompanyName);
                            item.SubItems.Add(versionInfo.ProductName);

                            string version = versionInfo.ProductVersion;

                            if (String.IsNullOrEmpty(version))
                                version = versionInfo.FileVersion;

                            item.SubItems.Add(version);
                            item.SubItems.Add(versionInfo.FileDescription);
                            item.SubItems.Add(versionInfo.Comments);
                        }
                        else
                        {
                            item.SubItems.Add((string)null);
                            item.SubItems.Add((string)null);
                            item.SubItems.Add((string)null);
                            item.SubItems.Add((string)null);
                            item.SubItems.Add((string)null);
                        }

                        if (fileInfo != null)
                            item.SubItems.Add(fileInfo.CreationTime.ToString());
                        else
                            item.SubItems.Add((string)null);

                        string sources = GetExecutableSources(pair);

                        if (sources != null)
                            item.SubItems.Add(sources);
                        else
                            item.SubItems.Add((string)null);

                        localItems.Add(item);

                        if (icon != null)
                            localIcons[fileName] = icon;
                    }
                }
            }

            items = localItems.ToArray();
            icons = localIcons;

            return ReturnCode.Ok;
        }
        #endregion
    }
}
