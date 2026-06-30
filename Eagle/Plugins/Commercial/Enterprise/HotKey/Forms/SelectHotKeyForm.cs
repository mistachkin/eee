/*
 * SelectHotKeyForm.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Drawing;
using System.Windows.Forms;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using HotKey.Components.Private;

namespace HotKey.Forms
{
    /// <summary>
    /// Implements the on-screen keyboard form used to select a hot-key.  The
    /// user picks modifier check boxes and a virtual-key button (or, in
    /// unlimited mode, an arbitrary set of keys); the result is the chosen key
    /// combination.
    /// </summary>
    internal sealed partial class SelectHotKeyForm : BaseForm
    {
        #region Private Constants
        /// <summary>
        /// The default title of the select-hot-key form.
        /// </summary>
        private const string DefaultTitle = "Select Modifiers & Virtual Key";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prefix used for the virtual-key button names.
        /// </summary>
        private const string ButtonPrefix = "btn";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The font size used for the large keys.
        /// </summary>
        private const float BigFontSize = 12.0f;
        /// <summary>
        /// The font size used for the small keys.
        /// </summary>
        private const float SmallFontSize = 8.25f;
        /// <summary>
        /// The font size used for the buttons.
        /// </summary>
        private const float ButtonFontSize = 12.0f;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The normal foreground color for an unselected key.
        /// </summary>
        private Color normalForeColor = Color.White;
        /// <summary>
        /// The normal background color for an unselected key.
        /// </summary>
        private Color normalBackColor = Color.Black;

        /// <summary>
        /// The foreground color for a selected key.
        /// </summary>
        private Color selectForeColor = Color.Black;
        /// <summary>
        /// The background color for a selected key.
        /// </summary>
        private Color selectBackColor = Color.White;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// Non-zero when the form is in unlimited (arbitrary key list) mode.
        /// </summary>
        private bool unlimited;
        /// <summary>
        /// Non-zero when the selection has unsaved changes.
        /// </summary>
        private bool dirty;
        /// <summary>
        /// The collection of virtual-key buttons.
        /// </summary>
        private Button[] buttons;
        /// <summary>
        /// The collection of modifier check boxes.
        /// </summary>
        private CheckBox[] checkBoxes;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="SelectHotKeyForm" /> with the specified
        /// id, interpreter, and result variable name.
        /// </summary>
        /// <param name="id">
        /// The form id.
        /// </param>
        /// <param name="interpreter">
        /// The associated interpreter.
        /// </param>
        /// <param name="varName">
        /// The variable that receives the form id, if any.
        /// </param>
        private SelectHotKeyForm(
            int id,                  /* in */
            Interpreter interpreter, /* in */
            string varName           /* in */
            )
            : base(id, interpreter, varName)
        {
            InitializeComponent();

            checkBoxes = new CheckBox[] { chkShift, chkControl, chkAlt };

            buttons = new Button[] {
                btnEscape, btnF1, btnF2, btnF3, btnF4, btnF5, btnF6, btnF7,
                btnF8, btnF9, btnF10, btnF11, btnF12, btnPrintScreen,
                btnScroll, btnPause, btnF13, btnF14, btnF15, btnF16, btnF17,
                btnF18, btnF19, btnF20, btnF21, btnF22, btnF23, btnF24,
                btnOemTilde, btnD1, btnD2, btnD3, btnD4, btnD5, btnD6, btnD7,
                btnD8, btnD9, btnD0, btnOemMinus, btnOemPlus, btnBack,
                btnInsert, btnHome, btnPageUp, btnTab, btnQ, btnW, btnE, btnR,
                btnT, btnY, btnU, btnI, btnO, btnP, btnOemCloseBrackets,
                btnOemOpenBrackets, btnOemPipe, btnDelete, btnEnd, btnPageDown,
                btnCapsLock, btnA, btnS, btnD, btnF, btnG, btnH, btnJ, btnK,
                btnL, btnOemSemicolon, btnOemQuotes, btnEnter, btnZ, btnX,
                btnC, btnV, btnB, btnN, btnM, btnOemComma, btnOemPeriod,
                btnOemQuestion, btnUp, btnSpace, btnLeft, btnDown, btnRight
            };

            foreach (CheckBox checkBox in checkBoxes)
            {
                checkBox.CheckedChanged += new EventHandler(
                    Modifiers_CheckedChanged);
            }

            foreach (Button button in buttons)
                button.Click += new EventHandler(VirtualKey_Click);

            this.Shown += new EventHandler(SelectHotKeyForm_Shown);
            this.Disposed += new EventHandler(SelectHotKeyForm_Disposed);

            btnOk.Click += new EventHandler(btnOk_Click);
            btnCancel.Click += new EventHandler(btnCancel_Click);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Form Event Handlers
        /// <summary>
        /// Handles the form-shown event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void SelectHotKeyForm_Shown(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            this.BringToFront();
            this.Activate();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the disposed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void SelectHotKeyForm_Disposed(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            if (!disposed)
            {
                //
                // NOTE: This form is now disposed.
                //
                disposed = true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the modifier checked-changed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void Modifiers_CheckedChanged(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            foreach (CheckBox checkBox in checkBoxes)
                SelectModifierCheckBox(checkBox, checkBox.Checked);

            SetModifiers(Keys.None);
            UpdateTitleAndSetDirty(true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the virtual-key button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void VirtualKey_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            Button button = sender as Button;

            if (button == null)
                return;

            Keys? keys = GetKeysFor(button);

            if (keys == null)
                return;

            SetModifiers((Keys)keys);

            Keys newVirtualKey = WinFormsOps.GetVirtualKey((Keys)keys);
            bool unlimited = IsUnlimited();

            if (unlimited)
            {
                SelectVirtualKeyButton(button,
                    !IsSelectedVirtualKeyButton(button));
            }
            else
            {
                foreach (Button button2 in buttons)
                    SelectVirtualKeyButton(button2, false);

                if (newVirtualKey != virtualKey)
                {
                    //
                    // NOTE: The selected virtual key has changed;
                    //       therefore, update the stored virtual
                    //       key and then also visually select the
                    //       corresponding button.
                    //
                    virtualKey = newVirtualKey;
                    SelectVirtualKeyButton(button, true);
                }
                else
                {
                    //
                    // NOTE: The selected virtual key is unchanged;
                    //       therefore, "unselect" it.
                    //
                    virtualKey = Keys.None;
                }
            }

            UpdateTitleAndSetDirty(true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the OK-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnOk_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            this.DialogResult = DialogResult.OK;
            this.Hide();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the cancel-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnCancel_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            this.DialogResult = DialogResult.Cancel;
            this.Hide();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Determines whether the selection has unsaved changes.
        /// </summary>
        /// <returns>
        /// Non-zero when the form is dirty; otherwise, zero.
        /// </returns>
        private bool IsDirty()
        {
            return dirty;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates the form title to reflect the current selection and marks
        /// the form dirty.
        /// </summary>
        /// <param name="dirty">
        /// Non-zero to mark the form dirty.
        /// </param>
        private void UpdateTitleAndSetDirty(
            bool? dirty /* in */
            )
        {
            if (IsUnlimited())
            {
                StringList keyNames = GetKeysToShow();

                if ((modifiers != Keys.None) || (keyNames != null))
                {
                    if (keyNames != null)
                    {
                        keyNames.Insert(0, modifiers.ToString());
                    }
                    else
                    {
                        keyNames = new StringList();
                        keyNames.Add(modifiers.ToString());
                    }

                    this.Text = String.Format(
                        "{0} - Selected {1}", DefaultTitle, keyNames);

                    goto setDirty;
                }
            }
            else
            {
                if ((modifiers != Keys.None) || (virtualKey != Keys.None))
                {
                    this.Text = String.Format(
                        "{0} - Selected {1}", DefaultTitle,
                        WinFormsOps.GetKeysToShow(modifiers, virtualKey));

                    goto setDirty;
                }
            }

            this.Text = DefaultTitle;

        setDirty:

            if (dirty != null)
                this.dirty = (bool)dirty;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the form is in unlimited (arbitrary key list)
        /// mode.
        /// </summary>
        /// <returns>
        /// Non-zero when in unlimited mode; otherwise, zero.
        /// </returns>
        private bool IsUnlimited()
        {
            return unlimited;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets whether the form is in unlimited (arbitrary key list) mode.
        /// </summary>
        /// <param name="unlimited">
        /// Non-zero to enable unlimited mode.
        /// </param>
        private void SetUnlimited(
            bool unlimited /* in */
            )
        {
            this.unlimited = unlimited;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key value represented by the supplied virtual-key button.
        /// </summary>
        /// <param name="button">
        /// The button whose key is requested.
        /// </param>
        /// <returns>
        /// The button's key, or null when none.
        /// </returns>
        private static Keys? GetKeysFor(
            Button button /* in */
            )
        {
            string name = button.Name;

            if (String.IsNullOrEmpty(name))
                return null;

            string keyName = name.Substring(
                ButtonPrefix.Length);

            if (String.IsNullOrEmpty(keyName))
                return null;

            Keys keys = Keys.None;

            if (WinFormsOps.ParseKeys(
                    null, keyName, ref keys) != ReturnCode.Ok)
            {
                return null;
            }

            return keys;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the list of currently selected keys for display.
        /// </summary>
        /// <returns>
        /// The selected keys to show.
        /// </returns>
        private StringList GetKeysToShow()
        {
            StringList keyNames = null;

            foreach (Button button in buttons)
            {
                if (!IsSelectedVirtualKeyButton(button))
                    continue;

                Keys? keys = GetKeysFor(button);

                if (keys == null)
                    continue;

                if (keyNames == null)
                    keyNames = new StringList();

                keyNames.Add(keys.ToString());
            }

            return keyNames;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the virtual-key button corresponding to the supplied virtual
        /// key.
        /// </summary>
        /// <param name="virtualKey">
        /// The virtual key to map.
        /// </param>
        /// <returns>
        /// The matching button, or null when none.
        /// </returns>
        private Button VirtualKeyToButton(
            Keys virtualKey /* in */
            )
        {
            //
            // NOTE: Try for an exact enumeration value to name match first.
            //
            string buttonName = ButtonPrefix + virtualKey.ToString();
            Button button = this.Controls[buttonName] as Button;

            if (button != null)
                return button;

            //
            // NOTE: Failing an exact match, perform a brute-force search
            //       through all the "Keys" enumeration values for candidate
            //       names and then check if that named button exists.
            //
            try
            {
                foreach (string name in Enum.GetNames(typeof(Keys)))
                {
                    //
                    // NOTE: In theory, this call to the Enum.Parse method
                    //       should never fail because we just obtained the
                    //       name from the Enum.GetNames method.
                    //
                    Keys newVirtualKey = (Keys)Enum.Parse(typeof(Keys), name);

                    //
                    // NOTE: Does the enumerated value corresponding to the
                    //       current enumerated value name match the one
                    //       specified by the caller?
                    //
                    if (newVirtualKey == virtualKey)
                    {
                        //
                        // NOTE: Next, check if a button using the enumerated
                        //       value name exists on this form.
                        //
                        buttonName = ButtonPrefix + name;
                        button = this.Controls[buttonName] as Button;

                        if (button != null)
                            return button;
                    }
                }
            }
            catch
            {
                // do nothing.
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the selected state of a modifier check box.
        /// </summary>
        /// <param name="checkBox">
        /// The check box to update.
        /// </param>
        /// <param name="select">
        /// Non-zero to select it.
        /// </param>
        private void SelectModifierCheckBox(
            CheckBox checkBox, /* in */
            bool select        /* in */
            )
        {
            if (checkBox == null)
                return;

            if (select)
            {
                checkBox.ForeColor = selectForeColor;
                checkBox.BackColor = selectBackColor;
            }
            else
            {
                checkBox.ForeColor = normalForeColor;
                checkBox.BackColor = normalBackColor;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the selected state of a virtual-key button.
        /// </summary>
        /// <param name="button">
        /// The button to update.
        /// </param>
        /// <param name="select">
        /// Non-zero to select it.
        /// </param>
        private void SelectVirtualKeyButton(
            Button button, /* in */
            bool select    /* in */
            )
        {
            if (button == null)
                return;

            if (select)
            {
                button.ForeColor = selectForeColor;
                button.BackColor = selectBackColor;
            }
            else
            {
                button.ForeColor = normalForeColor;
                button.BackColor = normalBackColor;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied virtual-key button is selected.
        /// </summary>
        /// <param name="button">
        /// The button to test.
        /// </param>
        /// <returns>
        /// Non-zero when the button is selected; otherwise, zero.
        /// </returns>
        private bool IsSelectedVirtualKeyButton(
            Button button /* in */
            )
        {
            if (button == null)
                return false;

            if (button.ForeColor != selectForeColor)
                return false;

            if (button.BackColor != selectBackColor)
                return false;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the modifier check boxes from the supplied key combination.
        /// </summary>
        /// <param name="keys">
        /// The key combination whose modifiers are applied.
        /// </param>
        private void SetModifiers(Keys keys)
        {
            modifiers = WinFormsOps.GetModifiers(keys);

            if (chkAlt.Checked)
                modifiers |= Keys.Alt;

            if (chkControl.Checked)
                modifiers |= Keys.Control;

            if (chkShift.Checked)
                modifiers |= Keys.Shift;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Selects the modifier check boxes corresponding to the supplied
        /// modifiers.
        /// </summary>
        /// <param name="modifiers">
        /// The modifiers to select.
        /// </param>
        private void SelectModifiers(
            Keys modifiers /* in */
            )
        {
            chkAlt.Checked = WinFormsOps.HasKeys(
                modifiers, Keys.Alt, true);

            chkControl.Checked = WinFormsOps.HasKeys(
                modifiers, Keys.Control, true);

            chkShift.Checked = WinFormsOps.HasKeys(
                modifiers, Keys.Shift, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Selects the virtual-key button corresponding to the supplied
        /// virtual key.
        /// </summary>
        /// <param name="virtualKey">
        /// The virtual key to select.
        /// </param>
        private void SelectVirtualKey(
            Keys virtualKey /* in */
            )
        {
            Button button = VirtualKeyToButton(virtualKey);

            foreach (Button button2 in buttons)
                SelectVirtualKeyButton(button2, false);

            SelectVirtualKeyButton(button, true);

            this.virtualKey = virtualKey;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Selects the keys corresponding to the supplied key names (unlimited
        /// mode).
        /// </summary>
        /// <param name="keyNames">
        /// The names of the keys to select.
        /// </param>
        private void SelectKeys(
            StringList keyNames /* in */
            )
        {
            if (keyNames == null)
                return;

            foreach (Button button2 in buttons)
                SelectVirtualKeyButton(button2, false);

            foreach (string keyName in keyNames)
            {
                Keys virtualKey = Keys.None;

                if (WinFormsOps.ParseKeys(null, keyName,
                        ref virtualKey) != ReturnCode.Ok)
                {
                    continue;
                }

                Button button = VirtualKeyToButton(virtualKey);

                SelectVirtualKeyButton(button, true);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Designer Code Static Font Integration Methods
        /// <summary>
        /// Gets the font used for the buttons.
        /// </summary>
        /// <returns>
        /// The button font.
        /// </returns>
        private static Font GetButtonFont()
        {
            //
            // TODO: Do this in a somewhat nicer and more modular way?
            //
            return new Font(
                FontFamily.GenericSansSerif, ButtonFontSize,
                FontStyle.Regular, GraphicsUnit.Point, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the font used for the large keys.
        /// </summary>
        /// <returns>
        /// The large-key font.
        /// </returns>
        private static Font GetBigKeyFont()
        {
            //
            // TODO: Do this in a somewhat nicer and more modular way?
            //
            return new Font(
                FontFamily.GenericMonospace, BigFontSize,
                FontStyle.Regular, GraphicsUnit.Point, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the font used for the small keys.
        /// </summary>
        /// <returns>
        /// The small-key font.
        /// </returns>
        private static Font GetSmallKeyFont()
        {
            //
            // TODO: Do this in a somewhat nicer and more modular way?
            //
            return new Font(
                FontFamily.GenericMonospace, SmallFontSize,
                FontStyle.Regular, GraphicsUnit.Point, 0);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The backing field for the <see cref="Modifiers" /> property.
        /// </summary>
        private Keys modifiers;
        /// <summary>
        /// Gets the selected modifier keys.
        /// </summary>
        public Keys Modifiers
        {
            get { CheckDisposed(); return modifiers; }
            set { CheckDisposed(); modifiers = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="VirtualKey" /> property.
        /// </summary>
        private Keys virtualKey;
        /// <summary>
        /// Gets the selected virtual key.
        /// </summary>
        public Keys VirtualKey
        {
            get { CheckDisposed(); return virtualKey; }
            set { CheckDisposed(); virtualKey = value; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Creates and shows the select-hot-key form seeded with the supplied
        /// modifiers and virtual key.
        /// </summary>
        /// <param name="owner">
        /// The owner window, if any.
        /// </param>
        /// <param name="interpreter">
        /// The associated interpreter.
        /// </param>
        /// <param name="varName">
        /// The variable that receives the form id, if any.
        /// </param>
        /// <param name="id">
        /// The form id.
        /// </param>
        /// <param name="modifiers">
        /// On input, the initial modifiers; on output, the selected modifiers.
        /// </param>
        /// <param name="virtualKey">
        /// On input, the initial virtual key; on output, the selected virtual
        /// key.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowKeyboard(
            IWin32Window owner,      /* in */
            Interpreter interpreter, /* in */
            string varName,          /* in */
            int id,                  /* in */
            ref Keys modifiers,      /* in, out */
            ref Keys virtualKey      /* in, out */
            )
        {
            StringList keyNames = null;
            Result error = null;

            return ShowKeyboard(
                owner, interpreter, varName, id, false,
                ref modifiers, ref virtualKey, ref keyNames,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and shows the select-hot-key form, optionally in unlimited
        /// mode, seeded with the supplied modifiers, virtual key, and key
        /// names.
        /// </summary>
        /// <param name="owner">
        /// The owner window, if any.
        /// </param>
        /// <param name="interpreter">
        /// The associated interpreter.
        /// </param>
        /// <param name="varName">
        /// The variable that receives the form id, if any.
        /// </param>
        /// <param name="id">
        /// The form id.
        /// </param>
        /// <param name="unlimited">
        /// Non-zero for unlimited mode.
        /// </param>
        /// <param name="modifiers">
        /// On input, the initial modifiers; on output, the selected modifiers.
        /// </param>
        /// <param name="virtualKey">
        /// On input, the initial virtual key; on output, the selected virtual
        /// key.
        /// </param>
        /// <param name="keyNames">
        /// On input, the initial key names; on output, the selected key names
        /// (unlimited mode).
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowKeyboard(
            IWin32Window owner,      /* in */
            Interpreter interpreter, /* in */
            string varName,          /* in */
            int id,                  /* in */
            bool unlimited,          /* in */
            ref Keys modifiers,      /* in, out */
            ref Keys virtualKey,     /* in, out */
            ref StringList keyNames, /* in, out */
            ref Result error         /* out */
            )
        {
            try
            {
                using (SelectHotKeyForm form = new SelectHotKeyForm(
                        id, interpreter, varName))
                {
                    form.SetUnlimited(unlimited);
                    form.SelectModifiers(modifiers);

                    if (unlimited)
                        form.SelectKeys(keyNames);
                    else
                        form.SelectVirtualKey(virtualKey);

                    form.UpdateTitleAndSetDirty(false); /* NOTE: Just loaded. */

                    if (form.ShowDialog(owner) == DialogResult.OK)
                    {
                        //
                        // NOTE: Only modify the variables provided by
                        //       the caller if something was actually
                        //       changed.
                        //
                        if (form.IsDirty())
                        {
                            modifiers = form.Modifiers;
                            virtualKey = form.VirtualKey;
                            keyNames = form.GetKeysToShow();
                        }

                        return ReturnCode.Ok;
                    }
                    else
                    {
                        error = "selection canceled";
                    }
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this instance has been disposed.
        /// </summary>
        private bool disposed;
        /// <summary>
        /// Throws an exception if this instance has already been disposed.
        /// </summary>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(null, false))
            {
                throw new ObjectDisposedException(
                    typeof(SelectHotKeyForm).Name);
            }
#endif
        }
        #endregion
    }
}
