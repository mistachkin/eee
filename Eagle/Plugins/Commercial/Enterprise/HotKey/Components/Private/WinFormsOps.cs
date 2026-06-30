/*
 * WinFormsOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

#if SCINTILLA
using ScintillaNET;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using HotKey.Forms;
using HotKey.Interfaces.Private;
using _Clipboard = System.Windows.Forms.Clipboard;

namespace HotKey.Components.Private
{
    #region Private Delegates
    /// <summary>
    /// Represents a parameterless, void-returning delegate used to marshal
    /// simple actions onto a control's thread.
    /// </summary>
    [ObjectId("e3e09dfd-aa7c-4698-8578-96a500a129e1")]
    internal delegate void DelegateWithNoArgs();
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Provides Windows Forms helper methods used by the plugin: common
    /// dialogs (file, directory, message box), nested control/menu/tool-strip
    /// lookup, thread-safe text get/set and clicking, key parsing and
    /// formatting, and event-to-list conversion.
    /// </summary>
    [ObjectId("142ba6f1-7c85-42a1-a210-d21c6cb2b43d")]
    internal static class WinFormsOps
    {
        #region Private Constants
        //
        // NOTE: This constant is used by the GetKeysToShow method in order
        //       to construct a formatted string compatible with Enum.Parse
        //       for the Keys enumerated type.
        //
        /// <summary>
        /// The separator used between enumeration values when formatting.
        /// </summary>
        private const string EnumSeperator = ", ";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This constant is used by the GetKeysToShow method in order
        //       to format a Keys enumerated type value with its associated
        //       modifiers, which is also a Keys enumerated type value.
        //
        /// <summary>
        /// The format used when rendering a key combination for display.
        /// </summary>
        private const string KeysFormat = "{0}{1}{2}";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the hash algorithm used within this class.
        //
        /// <summary>
        /// The name of the hash algorithm used by the hash-and-set-text
        /// helper.
        /// </summary>
        private const string HashAlgorithmName = "SHA512";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Common Dialog Helper Methods
        /// <summary>
        /// Shows a folder-selection dialog.
        /// </summary>
        /// <param name="description">
        /// The dialog description text.
        /// </param>
        /// <param name="rootFolder">
        /// The root folder for the dialog.
        /// </param>
        /// <param name="initialDirectory">
        /// The initially selected directory.
        /// </param>
        /// <returns>
        /// The selected directory, or null when cancelled.
        /// </returns>
        public static string SelectDirectory(
            string description,                   /* in */
            Environment.SpecialFolder rootFolder, /* in */
            string initialDirectory               /* in */
            )
        {
            string result = null;

            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.RootFolder = rootFolder;
                dialog.ShowNewFolderButton = true;

                if (initialDirectory != null)
                    dialog.SelectedPath = initialDirectory;

                if (dialog.ShowDialog() == DialogResult.OK)
                    result = dialog.SelectedPath;
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows an open-file dialog.
        /// </summary>
        /// <param name="title">
        /// The dialog title.
        /// </param>
        /// <param name="filter">
        /// The file filter.
        /// </param>
        /// <param name="initialDirectory">
        /// The initial directory.
        /// </param>
        /// <param name="fileName">
        /// The initial file name.
        /// </param>
        /// <returns>
        /// The selected file name, or null when cancelled.
        /// </returns>
        public static string SelectOpenFileName(
            string title,            /* in */
            string filter,           /* in */
            string initialDirectory, /* in */
            string fileName          /* in */
            )
        {
            string result = null;

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = filter;
                dialog.RestoreDirectory = true;
                dialog.Multiselect = false;

                if (initialDirectory != null)
                    dialog.InitialDirectory = initialDirectory;

                if (fileName != null)
                    dialog.FileName = fileName;

                if (dialog.ShowDialog() == DialogResult.OK)
                    result = dialog.FileName;
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows a save-file dialog.
        /// </summary>
        /// <param name="title">
        /// The dialog title.
        /// </param>
        /// <param name="filter">
        /// The file filter.
        /// </param>
        /// <param name="initialDirectory">
        /// The initial directory.
        /// </param>
        /// <param name="fileName">
        /// The initial file name.
        /// </param>
        /// <returns>
        /// The selected file name, or null when cancelled.
        /// </returns>
        public static string SelectSaveFileName(
            string title,            /* in */
            string filter,           /* in */
            string initialDirectory, /* in */
            string fileName          /* in */
            )
        {
            string result = null;

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = filter;
                dialog.RestoreDirectory = true;

                if (initialDirectory != null)
                    dialog.InitialDirectory = initialDirectory;

                if (fileName != null)
                    dialog.FileName = fileName;

                if (dialog.ShowDialog() == DialogResult.OK)
                    result = dialog.FileName;
            }

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Dialog Helper Methods
        /// <summary>
        /// Shows a yes/no message box.
        /// </summary>
        /// <param name="owner">
        /// The owner window, if any.
        /// </param>
        /// <param name="text">
        /// The prompt text.
        /// </param>
        /// <returns>
        /// The dialog result indicating the chosen button.
        /// </returns>
        public static DialogResult YesOrNo(
            IWin32Window owner, /* in */
            string text         /* in */
            )
        {
            return ShowMessage(
                owner, text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows a yes/no/cancel message box.
        /// </summary>
        /// <param name="owner">
        /// The owner window, if any.
        /// </param>
        /// <param name="text">
        /// The prompt text.
        /// </param>
        /// <returns>
        /// The dialog result indicating the chosen button.
        /// </returns>
        public static DialogResult YesNoOrCancel(
            IWin32Window owner, /* in */
            string text         /* in */
            )
        {
            return ShowMessage(owner, text,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows an error message box.
        /// </summary>
        /// <param name="owner">
        /// The owner window, if any.
        /// </param>
        /// <param name="text">
        /// The error text.
        /// </param>
        /// <returns>
        /// The dialog result.
        /// </returns>
        public static DialogResult ShowError(
            IWin32Window owner, /* in */
            string text         /* in */
            )
        {
            return MessageBox.Show(
                owner, text, Application.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows a message box with the specified buttons and icon.
        /// </summary>
        /// <param name="owner">
        /// The owner window, if any.
        /// </param>
        /// <param name="text">
        /// The message text.
        /// </param>
        /// <param name="buttons">
        /// The buttons to display.
        /// </param>
        /// <param name="icon">
        /// The icon to display.
        /// </param>
        /// <returns>
        /// The dialog result indicating the chosen button.
        /// </returns>
        private static DialogResult ShowMessage(
            IWin32Window owner,        /* in */
            string text,               /* in */
            MessageBoxButtons buttons, /* in */
            MessageBoxIcon icon        /* in */
            )
        {
            return MessageBox.Show(
                owner, text, Application.ProductName, buttons, icon);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows a message box describing a return code and result.
        /// </summary>
        /// <param name="owner">
        /// The owner window, if any.
        /// </param>
        /// <param name="code">
        /// The return code to display.
        /// </param>
        /// <param name="result">
        /// The result to display.
        /// </param>
        public static void ShowResult(
            IWin32Window owner, /* in */
            ReturnCode code,    /* in */
            Result result       /* in */
            )
        {
            ShowMessage(
                owner, Utility.FormatResult(code, result),
                MessageBoxButtons.OK, Utility.IsSuccess(code, false) ?
                    MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Window Control Methods
        /// <summary>
        /// Determines whether a form matches the supplied pattern (by name or
        /// text), honoring the raw-form-only and exact-only options.
        /// </summary>
        /// <param name="form">
        /// The form to test.
        /// </param>
        /// <param name="pattern">
        /// The pattern to match.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when matching.
        /// </param>
        /// <param name="rawFormOnly">
        /// Non-zero to match only non-managed forms.
        /// </param>
        /// <param name="exactOnly">
        /// Non-zero to require an exact match.
        /// </param>
        /// <param name="match">
        /// On output, non-zero when the form matched.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode DoesMatchForm(
            Form form,               /* in */
            string pattern,          /* in */
            CultureInfo cultureInfo, /* in */
            bool rawFormOnly,        /* in */
            bool exactOnly,          /* in */
            ref bool match,          /* out */
            ref Result error         /* out */
            )
        {
            IHotKeyForm hotKeyForm = rawFormOnly ? null : form as IHotKeyForm;

            if (hotKeyForm != null)
            {
                match = hotKeyForm.DoesMatch(pattern, cultureInfo, exactOnly);
                return ReturnCode.Ok;
            }
#if WINFORMS
            else
            {
                if (String.IsNullOrEmpty(pattern))
                {
                    error = "invalid pattern";
                    return ReturnCode.Error;
                }

                ReturnCode code;
                IntPtr hWnd = IntPtr.Zero;

                code = Utility.GetControlHandle(form, ref hWnd, ref error);

                if (code != ReturnCode.Ok)
                    return code;

                if (IntPtr.Size == sizeof(long))
                {
                    long id = 0;

                    if (Value.GetWideInteger2(
                            pattern, ValueFlags.AnyInteger, cultureInfo,
                            ref id) == ReturnCode.Ok)
                    {
                        match = (id == hWnd.ToInt64());
                        return ReturnCode.Ok;
                    }
                }
                else
                {
                    int id = 0;

                    if (Value.GetInteger2(
                            pattern, ValueFlags.AnyInteger, cultureInfo,
                            ref id) == ReturnCode.Ok)
                    {
                        match = (id == hWnd.ToInt32());
                        return ReturnCode.Ok;
                    }
                }

                string name = null;

                if (!GetName(form, ref name))
                {
                    error = "failed to get form name";
                    return ReturnCode.Error;
                }

                if ((name != null) &&
                    Utility.SystemStringEquals(pattern, name))
                {
                    match = true;
                    return ReturnCode.Ok;
                }

                string text = null;

                if (!GetText(form, ref text))
                {
                    error = "failed to get form text";
                    return ReturnCode.Error;
                }

                if ((text != null) &&
                    Utility.SystemStringEquals(pattern, text))
                {
                    match = true;
                    return ReturnCode.Ok;
                }

                if (!exactOnly)
                {
                    if ((name != null) &&
                        Parser.StringMatch(null, name, 0, pattern, 0, false))
                    {
                        match = true;
                        return ReturnCode.Ok;
                    }

                    if ((text != null) &&
                        Parser.StringMatch(null, text, 0, pattern, 0, false))
                    {
                        match = true;
                        return ReturnCode.Ok;
                    }
                }
            }

            match = false;
            return ReturnCode.Ok;
#else
            error = "not implemented";
            return ReturnCode.Error;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds a control by (possibly nested) name within a form.
        /// </summary>
        /// <param name="form">
        /// The form to search.
        /// </param>
        /// <param name="controlName">
        /// The name of the control to find.
        /// </param>
        /// <param name="wasNested">
        /// On output, non-zero when the control was found nested.
        /// </param>
        /// <returns>
        /// The matching control, or null when not found.
        /// </returns>
        private static Control GetNestedControl(
            Form form,          /* in */
            string controlName, /* in */
            ref bool? wasNested /* out */
            )
        {
            Control control;
            Result error = null;

            control = GetNestedControl(
                form, controlName, ref wasNested, ref error);

            if ((control == null) && (error != null))
                LogOps.Complain(ReturnCode.Error, error);

            return control;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds a control by (possibly nested) name within a form, reporting
        /// any error.
        /// </summary>
        /// <param name="form">
        /// The form to search.
        /// </param>
        /// <param name="controlName">
        /// The name of the control to find.
        /// </param>
        /// <param name="wasNested">
        /// On output, non-zero when the control was found nested.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The matching control, or null when not found.
        /// </returns>
        private static Control GetNestedControl(
            Form form,           /* in */
            string controlName,  /* in */
            ref bool? wasNested, /* out */
            ref Result error     /* out */
            )
        {
            try
            {
                if (form == null)
                {
                    error = "invalid form";
                    return null;
                }

                if (controlName == null)
                {
                    wasNested = false;
                    return form;
                }

                Control result = form.Controls[controlName];

                if (result != null)
                {
                    wasNested = false;
                    return result;
                }

                string[] parts = controlName.Split(Type.Delimiter);

                if ((parts == null) || (parts.Length < 2))
                    return null;

                for (int index = 0; index < parts.Length; index++)
                {
                    string part = parts[index];

                    if (index > 0)
                    {
                        if (result == null)
                            break;

                        result = result.Controls[part];
                    }
                    else
                    {
                        result = form.Controls[part];
                    }
                }

                if (result != null)
                    wasNested = true;

                return result;
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds a menu item by (possibly nested) name within a form.
        /// </summary>
        /// <param name="form">
        /// The form to search.
        /// </param>
        /// <param name="menuName">
        /// The name of the menu item to find.
        /// </param>
        /// <param name="merged">
        /// Non-zero to include merged menus in the search.
        /// </param>
        /// <param name="wasNested">
        /// On output, non-zero when the item was found nested.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The matching menu item, or null when not found.
        /// </returns>
        private static MenuItem GetNestedMenuItem(
            Form form,           /* in */
            string menuName,     /* in */
            bool merged,         /* in */
            ref bool? wasNested, /* out */
            ref Result error     /* out */
            )
        {
            try
            {
                if (form == null)
                {
                    error = "invalid form";
                    return null;
                }

                Menu menu = merged ? form.MergedMenu : form.Menu;

                if (menu == null)
                {
                    error = String.Format("invalid form {0}menu",
                        merged ? "merged " : String.Empty);

                    return null;
                }

                MenuItem result = null;

                if (menuName == null)
                {
                    result = menu as MenuItem;

                    if (result != null)
                        wasNested = false;

                    return result;
                }

                result = menu.MenuItems[menuName];

                if (result != null)
                {
                    wasNested = false;
                    return result;
                }

                string[] parts = menuName.Split(Type.Delimiter);

                if ((parts == null) || (parts.Length < 2))
                    return null;

                for (int index = 0; index < parts.Length; index++)
                {
                    string part = parts[index];

                    if (index > 0)
                    {
                        if (result == null)
                            break;

                        result = result.MenuItems[part];
                    }
                    else
                    {
                        result = menu.MenuItems[part];
                    }
                }

                if (result != null)
                    wasNested = true;

                return result;
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds a tool-strip item by (possibly nested) name within a form.
        /// </summary>
        /// <param name="form">
        /// The form to search.
        /// </param>
        /// <param name="itemName">
        /// The name of the tool-strip item to find.
        /// </param>
        /// <param name="wasNested">
        /// On output, non-zero when the item was found nested.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The matching tool-strip item, or null when not found.
        /// </returns>
        private static ToolStripItem GetNestedToolStripItem(
            Form form,           /* in */
            string itemName,     /* in */
            ref bool? wasNested, /* out */
            ref Result error     /* out */
            )
        {
            try
            {
                if (form == null)
                {
                    error = "invalid form";
                    return null;
                }

                if (itemName == null)
                {
                    error = "unsupported item name";
                    return null;
                }

                ToolStrip toolStrip = form.MainMenuStrip;

                if (toolStrip == null)
                {
                    error = "invalid form tool strip";
                    return null;
                }

                ToolStripItem result = toolStrip.Items[itemName];

                if (result != null)
                {
                    wasNested = false;
                    return result;
                }

                string[] parts = itemName.Split(Type.Delimiter);

                if ((parts == null) || (parts.Length < 2))
                    return null;

                for (int index = 0; index < parts.Length; index++)
                {
                    string part = parts[index];

                    if (index > 0)
                    {
                        if (result == null)
                            break;

                        ToolStripDropDownItem item =
                            result as ToolStripDropDownItem;

                        if ((item == null) || !item.HasDropDownItems)
                        {
                            result = null;
                            break;
                        }

                        result = item.DropDownItems[part];
                    }
                    else
                    {
                        result = toolStrip.Items[part];
                    }
                }

                if (result != null)
                    wasNested = true;

                return result;
            }
            catch (Exception e)
            {
                error = e;
            }

            return null;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Window Threading Methods
        /// <summary>
        /// Determines whether the supplied control (synchronize-invoke target)
        /// has been disposed.
        /// </summary>
        /// <param name="synchronizeInvoke">
        /// The control to test.
        /// </param>
        /// <returns>
        /// Non-zero when the control is disposed; otherwise, zero.
        /// </returns>
        private static bool IsDisposed(
            ISynchronizeInvoke synchronizeInvoke /* in */
            )
        {
            if (synchronizeInvoke != null)
            {
                Control control = synchronizeInvoke as Control;

                if ((control != null) && control.IsDisposed)
                    return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the supplied control.
        /// </summary>
        /// <param name="control">
        /// The control whose name is requested.
        /// </param>
        /// <param name="name">
        /// On output, receives the control name.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool GetName(
            Control control, /* in */
            ref string name  /* out */
            )
        {
            bool result;
            string localName = null;

            result = Invoke(control, new DelegateWithNoArgs(delegate()
            {
                localName = control.Name;
            }), true);

            if (result)
                name = localName;

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the text of the supplied control to the clipboard.
        /// </summary>
        /// <param name="control">
        /// The control whose text is copied.
        /// </param>
        /// <param name="clear">
        /// Non-zero to clear the clipboard first.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool CopyTextToClipboard(
            Control control, /* in */
            bool clear       /* in */
            )
        {
            string localText = null;

            if (!GetText(control, ref localText))
                return false;

            return CopyTextToClipboard(control, localText, clear);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the supplied text to the clipboard, associated with a
        /// control.
        /// </summary>
        /// <param name="control">
        /// The control associated with the operation.
        /// </param>
        /// <param name="text">
        /// The text to copy.
        /// </param>
        /// <param name="clear">
        /// Non-zero to clear the clipboard first.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool CopyTextToClipboard(
            Control control, /* in */
            string text,     /* in */
            bool clear       /* in */
            )
        {
            bool result;
            bool success = false;

            result = Invoke(control, new DelegateWithNoArgs(delegate()
            {
                if (clear)
                    _Clipboard.Clear();

                if (!String.IsNullOrEmpty(text))
                {
                    _Clipboard.SetText(text);
                    success = true;
                }
                else if (clear)
                {
                    //
                    // HACK: Only clear was requested.
                    //
                    success = true;
                }
            }), true);

            return result && success;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the text of the supplied control.
        /// </summary>
        /// <param name="control">
        /// The control whose text is requested.
        /// </param>
        /// <param name="text">
        /// On output, receives the control text.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool GetText(
            Control control, /* in */
            ref string text  /* out */
            )
        {
            bool result;
            string localText = null;

            result = Invoke(control, new DelegateWithNoArgs(delegate()
            {
                localText = control.Text;
            }), true);

            if (result)
                text = localText;

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

#if !SCINTILLA
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
            TextBoxBase textBox, /* in */
            bool selected,       /* in */
            ref string text      /* out */
            )
        {
            bool result;
            string localText = null;

            result = Invoke(textBox, new DelegateWithNoArgs(delegate()
            {
                if (selected)
                {
                    string selectedText = textBox.SelectedText;

                    if (!String.IsNullOrEmpty(selectedText))
                        localText = selectedText;
                    else
                        localText = null;
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
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the text of the supplied control, optionally asynchronously.
        /// </summary>
        /// <param name="control">
        /// The control whose text is set.
        /// </param>
        /// <param name="text">
        /// The text to set.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool SetText(
            Control control,  /* in */
            string text,      /* in */
            bool asynchronous /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(control, new DelegateWithNoArgs(delegate()
                {
                    control.Text = text;
                }), true);
            }
            else
            {
                return Invoke(control, new DelegateWithNoArgs(delegate()
                {
                    control.Text = text;
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends text to the supplied text control, optionally
        /// asynchronously.
        /// </summary>
        /// <param name="textBox">
        /// The text control to append to.
        /// </param>
        /// <param name="text">
        /// The text to append.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool AppendText(
            TextBoxBase textBox, /* in */
            string text,         /* in */
            bool asynchronous    /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(textBox, new DelegateWithNoArgs(delegate()
                {
                    textBox.AppendText(text);
                }), true);
            }
            else
            {
                return Invoke(textBox, new DelegateWithNoArgs(delegate()
                {
                    textBox.AppendText(text);
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Hashes the supplied text and sets the control's text to a masked
        /// representation, optionally asynchronously.
        /// </summary>
        /// <param name="control">
        /// The control whose text is set.
        /// </param>
        /// <param name="text">
        /// The text to hash and set.
        /// </param>
        /// <param name="character">
        /// The masking character to display.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool HashAndSetText(
            Control control,  /* in */
            string text,      /* in */
            char? character,  /* in */
            bool asynchronous /* in */
            )
        {
            byte[] charBytes = null;
            Result error; /* REUSED */

            if (character != null)
            {
                error = null;

                if (Utility.GetBytes(
                        null, ((char)character).ToString(),
                        EncodingType.Text, false, ref charBytes,
                        ref error) != ReturnCode.Ok)
                {
                    Utility.Complain(null, ReturnCode.Error, error);
                    return false;
                }
            }

            byte[] bytes = null;

            error = null;

            if (Utility.GetBytesFromString(text,
                    null, ref bytes, ref error) == ReturnCode.Ok)
            {
                ByteList builder = new ByteList(bytes);

                if (charBytes != null)
                    builder.AddRange(charBytes);

                error = null;

                bytes = Utility.HashBytes(
                    HashAlgorithmName, builder.ToArray(), ref error);
            }
            else
            {
                StringBuilder builder = new StringBuilder(text);

                if (character != null)
                    builder.Append((char)character);

                error = null;

                bytes = Utility.HashString(
                    null, HashAlgorithmName, builder.ToString(),
                    EncodingType.Text, ref error);
            }

            if (bytes == null)
            {
                Utility.Complain(null, ReturnCode.Error, error);
                return false;
            }

            text = Convert.ToBase64String(bytes,
                Base64FormattingOptions.InsertLineBreaks);

            return SetText(control, text, asynchronous);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Activates (clicks) the supplied form, optionally asynchronously.
        /// </summary>
        /// <param name="form">
        /// The form to activate.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool Click(
            Form form,        /* in */
            bool asynchronous /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(form, new DelegateWithNoArgs(delegate()
                {
                    form.Focus();
                }), true);
            }
            else
            {
                return Invoke(form, new DelegateWithNoArgs(delegate()
                {
                    form.Focus();
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clicks the supplied button, optionally asynchronously.
        /// </summary>
        /// <param name="button">
        /// The button to click.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool Click(
            Button button,    /* in */
            bool asynchronous /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(button, new DelegateWithNoArgs(delegate()
                {
                    button.PerformClick();
                }), true);
            }
            else
            {
                return Invoke(button, new DelegateWithNoArgs(delegate()
                {
                    button.PerformClick();
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clicks the supplied button, optionally asynchronously.
        /// </summary>
        /// <param name="button">
        /// The button to click.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool Click(
            RadioButton button, /* in */
            bool asynchronous   /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(button, new DelegateWithNoArgs(delegate()
                {
                    button.PerformClick();
                }), true);
            }
            else
            {
                return Invoke(button, new DelegateWithNoArgs(delegate()
                {
                    button.PerformClick();
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clicks the supplied text control, optionally asynchronously.
        /// </summary>
        /// <param name="textBox">
        /// The text control to click.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool Click(
            TextBoxBase textBox, /* in */
            bool asynchronous    /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(textBox, new DelegateWithNoArgs(delegate()
                {
                    int length = textBox.TextLength;

                    textBox.Focus();
                    textBox.Select((length > 0) ? length - 1 : 0, 0);
                    textBox.ScrollToCaret();
                }), true);
            }
            else
            {
                return Invoke(textBox, new DelegateWithNoArgs(delegate()
                {
                    int length = textBox.TextLength;

                    textBox.Focus();
                    textBox.Select((length > 0) ? length - 1 : 0, 0);
                    textBox.ScrollToCaret();
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clicks the supplied menu item, optionally asynchronously.
        /// </summary>
        /// <param name="menuItem">
        /// The menu item to click.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool Click(
            MenuItem menuItem, /* in */
            bool asynchronous  /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(GetInvoker(menuItem),
                        new DelegateWithNoArgs(delegate()
                {
                    menuItem.PerformClick();
                }), true);
            }
            else
            {
                return Invoke(GetInvoker(menuItem),
                        new DelegateWithNoArgs(delegate()
                {
                    menuItem.PerformClick();
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clicks the supplied tool-strip item, optionally asynchronously.
        /// </summary>
        /// <param name="toolStripItem">
        /// The tool-strip item to click.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool Click(
            ToolStripItem toolStripItem, /* in */
            bool asynchronous            /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(GetInvoker(toolStripItem),
                        new DelegateWithNoArgs(delegate()
                {
                    toolStripItem.PerformClick();
                }), true);
            }
            else
            {
                return Invoke(GetInvoker(toolStripItem),
                        new DelegateWithNoArgs(delegate()
                {
                    toolStripItem.PerformClick();
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Toggles the supplied check box, optionally asynchronously.
        /// </summary>
        /// <param name="checkBox">
        /// The check box to toggle.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool Toggle(
            CheckBox checkBox, /* in */
            bool asynchronous  /* in */
            )
        {
            if (asynchronous)
            {
                return BeginInvoke(checkBox, new DelegateWithNoArgs(delegate()
                {
                    checkBox.Checked = !checkBox.Checked;
                }), true);
            }
            else
            {
                return Invoke(checkBox, new DelegateWithNoArgs(delegate()
                {
                    checkBox.Checked = !checkBox.Checked;
                }), true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds the form matching the pattern and clicks the named component
        /// (or the form itself when the component name is empty).
        /// </summary>
        /// <param name="cultureInfo">
        /// The culture used when matching.
        /// </param>
        /// <param name="formPattern">
        /// The pattern matching the target form.
        /// </param>
        /// <param name="componentName">
        /// The name of the component to click, or empty for the form.
        /// </param>
        /// <param name="rawFormOnly">
        /// Non-zero to match only non-managed forms.
        /// </param>
        /// <param name="exactOnly">
        /// Non-zero to require an exact match.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero to perform the operation without waiting.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode PerformClick(
            CultureInfo cultureInfo, /* in */
            string formPattern,      /* in */
            string componentName,    /* in */
            bool rawFormOnly,        /* in */
            bool exactOnly,          /* in */
            bool asynchronous,       /* in */
            ref Result error         /* out */
            )
        {
            ReturnCode code = ReturnCode.Ok;
            bool? wasNested = null;
            bool matchedForm = false;
            bool done = false;

            foreach (Form form in BaseForm.CopyOpenForms())
            {
                bool match = false;

                code = DoesMatchForm(
                    form, formPattern, cultureInfo, rawFormOnly, exactOnly,
                    ref match, ref error);

                if (code != ReturnCode.Ok)
                    break;

                if (match)
                {
                    matchedForm = true;
                    wasNested = null;

                    Control control = GetNestedControl(
                        form, componentName, ref wasNested);

                    if (control != null)
                    {
                        if (control is Form)
                        {
                            done = Click(control as Form, asynchronous);
                            break;
                        }
                        else if (control is Button)
                        {
                            done = Click(control as Button, asynchronous);
                            break;
                        }
                        else if (control is RadioButton)
                        {
                            done = Click(control as RadioButton, asynchronous);
                            break;
                        }
                        else if (control is TextBoxBase)
                        {
                            done = Click(control as TextBoxBase, asynchronous);
                            break;
                        }
#if SCINTILLA
                        else if (control is Scintilla)
                        {
                            done = ScintillaOps.Click(
                                control as Scintilla, asynchronous);

                            break;
                        }
#endif
                        else if (control is CheckBox)
                        {
                            done = Toggle(control as CheckBox, asynchronous);
                            break;
                        }
                        else
                        {
                            //
                            // NOTE: This control type is not supported by
                            //       this method.
                            //
                            error = String.Format(
                                "unsupported control type: {0}",
                                control.GetType());

                            code = ReturnCode.Error;
                            break;
                        }
                    }

                    ///////////////////////////////////////////////////////////

                    wasNested = null;

                    MenuItem menuItem = GetNestedMenuItem(
                        form, componentName, /* merged? */ true,
                        ref wasNested, ref error);

                    if (menuItem == null)
                    {
                        menuItem = GetNestedMenuItem(
                            form, componentName, /* merged? */ false,
                            ref wasNested, ref error);
                    }

                    if (menuItem != null)
                    {
                        done = Click(menuItem, asynchronous);
                        break;
                    }

                    ///////////////////////////////////////////////////////////

                    wasNested = null;

                    ToolStripItem toolStripItem = GetNestedToolStripItem(
                        form, componentName, ref wasNested, ref error);

                    if (toolStripItem != null)
                    {
                        done = Click(toolStripItem, asynchronous);
                        break;
                    }
                }
            }

            if ((code == ReturnCode.Ok) && !done)
            {
                string controlType;

                if (wasNested == null)
                    controlType = "unknown control";
                else if ((bool)wasNested)
                    controlType = "nested control";
                else
                    controlType = "normal control";

                error = String.Format(
                    "could not click {3} {1} on {2} matching {0}{4}",
                    Utility.FormatWrapOrNull(formPattern),
                    Utility.FormatWrapOrNull(componentName),
                    rawFormOnly ? "form" : "hot-key form",
                    controlType, matchedForm ? String.Empty :
                    ": missing form");

                code = ReturnCode.Error;
            }

            return code;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the synchronize-invoke target for the supplied menu item.
        /// </summary>
        /// <param name="menuItem">
        /// The menu item whose invoker is requested.
        /// </param>
        /// <returns>
        /// The synchronize-invoke target.
        /// </returns>
        private static ISynchronizeInvoke GetInvoker(
            MenuItem menuItem /* in */
            )
        {
            if (menuItem == null)
                return null;

            Menu menu = menuItem.Parent;

            if (menu == null)
                return null;

            MainMenu mainMenu = menu.GetMainMenu();

            if (mainMenu == null)
                return null;

            return mainMenu.GetForm();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the synchronize-invoke target for the supplied tool-strip
        /// item.
        /// </summary>
        /// <param name="toolStripItem">
        /// The tool-strip item whose invoker is requested.
        /// </param>
        /// <returns>
        /// The synchronize-invoke target.
        /// </returns>
        private static ISynchronizeInvoke GetInvoker(
            ToolStripItem toolStripItem /* in */
            )
        {
            if (toolStripItem == null)
                return null;

            return toolStripItem.Owner;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Synchronously invokes the supplied method on the control's thread.
        /// </summary>
        /// <param name="synchronizeInvoke">
        /// The control whose thread the method runs on.
        /// </param>
        /// <param name="method">
        /// The method to invoke.
        /// </param>
        /// <param name="strict">
        /// Non-zero to fail when invocation is not possible.
        /// </param>
        /// <param name="args">
        /// The arguments passed to the method.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool Invoke(
            ISynchronizeInvoke synchronizeInvoke, /* in */
            Delegate method,                      /* in */
            bool strict,                          /* in */
            params object[] args                  /* in */
            )
        {
            object result = null;

            return Invoke(
                synchronizeInvoke, method, strict, ref result, args);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Synchronously invokes the supplied method on the control's thread,
        /// returning its result.
        /// </summary>
        /// <param name="synchronizeInvoke">
        /// The control whose thread the method runs on.
        /// </param>
        /// <param name="method">
        /// The method to invoke.
        /// </param>
        /// <param name="strict">
        /// Non-zero to fail when invocation is not possible.
        /// </param>
        /// <param name="result">
        /// On output, receives the method's return value.
        /// </param>
        /// <param name="args">
        /// The arguments passed to the method.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool Invoke(
            ISynchronizeInvoke synchronizeInvoke, /* in */
            Delegate method,                      /* in */
            bool strict,                          /* in */
            ref object result,                    /* out */
            params object[] args                  /* in */
            )
        {
            if (synchronizeInvoke != null)
            {
                if (strict && IsDisposed(synchronizeInvoke))
                    return false;

                if (synchronizeInvoke.InvokeRequired)
                    result = synchronizeInvoke.Invoke(method, args);
                else
                    result = method.DynamicInvoke(args);

                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Asynchronously invokes the supplied method on the control's thread.
        /// </summary>
        /// <param name="synchronizeInvoke">
        /// The control whose thread the method runs on.
        /// </param>
        /// <param name="method">
        /// The method to invoke.
        /// </param>
        /// <param name="strict">
        /// Non-zero to fail when invocation is not possible.
        /// </param>
        /// <param name="args">
        /// The arguments passed to the method.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool BeginInvoke(
            ISynchronizeInvoke synchronizeInvoke, /* in */
            Delegate method,                      /* in */
            bool strict,                          /* in */
            params object[] args                  /* in */
            )
        {
            IAsyncResult result = null;

            return BeginInvoke(
                synchronizeInvoke, method, strict, ref result, args);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Asynchronously invokes the supplied method on the control's thread,
        /// returning its result.
        /// </summary>
        /// <param name="synchronizeInvoke">
        /// The control whose thread the method runs on.
        /// </param>
        /// <param name="method">
        /// The method to invoke.
        /// </param>
        /// <param name="strict">
        /// Non-zero to fail when invocation is not possible.
        /// </param>
        /// <param name="result">
        /// On output, receives the method's return value.
        /// </param>
        /// <param name="args">
        /// The arguments passed to the method.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool BeginInvoke(
            ISynchronizeInvoke synchronizeInvoke, /* in */
            Delegate method,                      /* in */
            bool strict,                          /* in */
            ref IAsyncResult result,              /* out */
            params object[] args                  /* in */
            )
        {
            if (synchronizeInvoke != null)
            {
                if (strict && IsDisposed(synchronizeInvoke))
                    return false;

                result = synchronizeInvoke.BeginInvoke(method, args);
                return true;
            }

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Window Keyboard Methods
        /// <summary>
        /// Determines whether the supplied keys contain the given keys.
        /// </summary>
        /// <param name="keys">
        /// The keys to test.
        /// </param>
        /// <param name="hasKeys">
        /// The keys to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the keys; zero to require any.
        /// </param>
        /// <returns>
        /// Non-zero when the keys are present; otherwise, zero.
        /// </returns>
        public static bool HasKeys(
            Keys keys,    /* in */
            Keys hasKeys, /* in */
            bool all      /* in */
            )
        {
            if (all)
                return ((keys & hasKeys) == hasKeys);
            else
                return ((keys & hasKeys) != Keys.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the modifier-key portion of the supplied key combination.
        /// </summary>
        /// <param name="keys">
        /// The key combination.
        /// </param>
        /// <returns>
        /// The modifier keys.
        /// </returns>
        public static Keys GetModifiers(
            Keys keys /* in */
            )
        {
            return (keys & Keys.Modifiers);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the virtual-key (non-modifier) portion of the supplied key
        /// combination.
        /// </summary>
        /// <param name="keys">
        /// The key combination.
        /// </param>
        /// <returns>
        /// The virtual key.
        /// </returns>
        public static Keys GetVirtualKey(
            Keys keys /* in */
            )
        {
            return (keys & Keys.KeyCode);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses a key combination from its string representation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used when parsing.
        /// </param>
        /// <param name="value">
        /// The string to parse.
        /// </param>
        /// <param name="keys">
        /// On output, receives the parsed key combination.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ParseKeys(
            Interpreter interpreter, /* in */
            string value,            /* in */
            ref Keys keys            /* out */
            )
        {
            Result error = null;

            return ParseKeys(interpreter, value, ref keys, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses a key combination from its string representation, reporting
        /// any error.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used when parsing.
        /// </param>
        /// <param name="value">
        /// The string to parse.
        /// </param>
        /// <param name="keys">
        /// On output, receives the parsed key combination.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ParseKeys(
            Interpreter interpreter, /* in */
            string value,            /* in */
            ref Keys keys,           /* out */
            ref Result error         /* out */
            )
        {
            if (String.IsNullOrEmpty(value))
            {
                keys = Keys.None;
                return ReturnCode.Ok;
            }

            object enumValue = Utility.TryParseFlagsEnum(interpreter,
                typeof(Keys), null, value, (interpreter != null) ?
                interpreter.CultureInfo : null, true, true, true,
                ref error);

            if (!(enumValue is Keys))
                return ReturnCode.Error;

            keys = (Keys)enumValue;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses modifiers and a virtual key from the supplied string
        /// representation.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used when parsing.
        /// </param>
        /// <param name="value">
        /// The string to parse.
        /// </param>
        /// <param name="modifiers">
        /// On output, receives the parsed modifiers.
        /// </param>
        /// <param name="virtualKey">
        /// On output, receives the parsed virtual key.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ParseModifiersAndVirtualKey(
            Interpreter interpreter, /* in */
            string value,            /* in */
            ref Keys modifiers,      /* out */
            ref Keys virtualKey      /* out */
            )
        {
            Result error = null;

            return ParseModifiersAndVirtualKey(
                interpreter, value, ref modifiers, ref virtualKey,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses modifiers and a virtual key from the supplied string
        /// representation, reporting any error.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used when parsing.
        /// </param>
        /// <param name="value">
        /// The string to parse.
        /// </param>
        /// <param name="modifiers">
        /// On output, receives the parsed modifiers.
        /// </param>
        /// <param name="virtualKey">
        /// On output, receives the parsed virtual key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private static ReturnCode ParseModifiersAndVirtualKey(
            Interpreter interpreter, /* in */
            string value,            /* in */
            ref Keys modifiers,      /* out */
            ref Keys virtualKey,     /* out */
            ref Result error         /* out */
            )
        {
            ReturnCode code;
            Keys keys = Keys.None;

            code = ParseKeys(interpreter, value, ref keys, ref error);

            if (code != ReturnCode.Ok)
                return code;

            modifiers = GetModifiers(keys);
            virtualKey = GetVirtualKey(keys);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a list representation of an event from its name and
        /// arguments.
        /// </summary>
        /// <param name="eventName">
        /// The name of the event.
        /// </param>
        /// <param name="eventArgs">
        /// The event arguments.
        /// </param>
        /// <returns>
        /// A list describing the event.
        /// </returns>
        public static StringList ToList(
            string eventName,      /* in */
            KeyEventArgs eventArgs /* in */
            )
        {
            StringList list = new StringList();

            list.Add("EventName", eventName);

            if (eventArgs != null)
            {
                list.Add("KeyValue", eventArgs.KeyValue.ToString());
                list.Add("KeyData", eventArgs.KeyData.ToString());
                list.Add("Modifiers", eventArgs.Modifiers.ToString());
                list.Add("KeyCode", eventArgs.KeyCode.ToString());
                list.Add("Shift", eventArgs.Shift.ToString());
                list.Add("Control", eventArgs.Control.ToString());
                list.Add("Alt", eventArgs.Alt.ToString());

                list.Add("SuppressKeyPress",
                    eventArgs.SuppressKeyPress.ToString());

                list.Add("Handled", eventArgs.Handled.ToString());
            }

            return list;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a list representation of an event from its name and the
        /// associated hot-key.
        /// </summary>
        /// <param name="eventName">
        /// The name of the event.
        /// </param>
        /// <param name="hotKey">
        /// The hot-key associated with the event.
        /// </param>
        /// <returns>
        /// A list describing the event.
        /// </returns>
        public static StringList ToList(
            string eventName, /* in */
            IHotKey hotKey    /* in */
            )
        {
            StringList list = new StringList();

            list.Add("EventName", eventName);

            if (hotKey != null)
                list.AddRange(hotKey.ToList(true));

            return list;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a key combination for display.
        /// </summary>
        /// <param name="keys">
        /// The key combination to format.
        /// </param>
        /// <returns>
        /// The formatted key combination.
        /// </returns>
        public static string GetKeysToShow(
            Keys keys /* in */
            )
        {
            return GetKeysToShow(
                GetModifiers(keys), GetVirtualKey(keys));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a modifiers-and-virtual-key combination for display.
        /// </summary>
        /// <param name="modifiers">
        /// The modifier keys.
        /// </param>
        /// <param name="virtualKey">
        /// The virtual key.
        /// </param>
        /// <returns>
        /// The formatted key combination.
        /// </returns>
        public static string GetKeysToShow(
            Keys modifiers, /* in */
            Keys virtualKey /* in */
            )
        {
            if ((modifiers == Keys.None) || (virtualKey == Keys.None))
                return (modifiers | virtualKey).ToString();

            return String.Format(KeysFormat,
                modifiers, EnumSeperator, virtualKey);
        }
        #endregion
    }
}
