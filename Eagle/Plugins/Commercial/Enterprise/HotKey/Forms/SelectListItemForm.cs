/*
 * SelectListItemForm.cs --
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
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using HotKey.Components.Private;

using SelectedItemCollection =
    System.Windows.Forms.ListView.SelectedListViewItemCollection;

using CheckedItemCollection =
    System.Windows.Forms.ListView.CheckedListViewItemCollection;

using StringElementData = HotKey.Components.Private.ListElementData<
    string, string>;

using ElementPair = System.Collections.Generic.KeyValuePair<
    string, HotKey.Components.Private.ListElementData<string, string>>;

using ElementDictionary = System.Collections.Generic.Dictionary<
    string, HotKey.Components.Private.ListElementData<string, string>>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace HotKey.Forms
{
    /// <summary>
    /// Implements the list-selection form used to choose one or more items
    /// from a list view, supporting multiple view modes, icons, extra columns,
    /// and an executable-file listing mode.
    /// </summary>
    [ObjectId("d6f5ca0a-629d-4f68-8dd5-13d718e86582")]
    internal sealed partial class SelectListItemForm : BaseForm
    {
        #region Private Constants
        /// <summary>
        /// The default title of the select-list-item form.
        /// </summary>
        private const string DefaultTitle = "Select List Item";
        /// <summary>
        /// The name of the embedded resource providing the default item icon.
        /// </summary>
        private const string DefaultIconResourceName = "Item.ico";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The number of extra columns shown in the list view.
        /// </summary>
        private IEnumerable<string> extraColumns;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="SelectListItemForm" /> with the
        /// specified id, interpreter, result variable name, and extra column
        /// count.
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
        /// <param name="extraColumns">
        /// The number of extra columns to show.
        /// </param>
        private SelectListItemForm(
            int id,                          /* in */
            Interpreter interpreter,         /* in */
            string varName,                  /* in */
            IEnumerable<string> extraColumns /* in */
            )
            : base(id, interpreter, varName)
        {
            InitializeComponent();

            ///////////////////////////////////////////////////////////////////

            this.extraColumns = extraColumns;

            ///////////////////////////////////////////////////////////////////

            this.Disposed += new EventHandler(SelectListItemForm_Disposed);

            lstItem.DoubleClick += new EventHandler(lstItem_DoubleClick);

            cboView.SelectedIndexChanged += new EventHandler(
                cboView_SelectedIndexChanged);

            btnOk.Click += new EventHandler(btnOk_Click);
            btnCancel.Click += new EventHandler(btnCancel_Click);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Event Handlers
        /// <summary>
        /// Handles the disposed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void SelectListItemForm_Disposed(
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
        /// Handles the item double-click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void lstItem_DoubleClick(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            btnOk_Click(sender, e);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the view combo-box selected-index-changed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void cboView_SelectedIndexChanged(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            object enumValue = GetSelectedView();

            if (enumValue is View)
                SetView((View)enumValue);
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
        /// Gets the currently selected list view mode.
        /// </summary>
        /// <returns>
        /// The selected view.
        /// </returns>
        private object GetSelectedView()
        {
            int index = cboView.SelectedIndex;

            if ((index >= 0) && (index < cboView.Items.Count))
            {
                string value = cboView.Items[index] as string;

                if (value != null)
                {
                    value = value.Replace(
                        Characters.SpaceString, String.Empty);

                    return Utility.TryParseEnum(
                        typeof(View), value, true, true);
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the current list view mode.
        /// </summary>
        /// <param name="view">
        /// The view to select.
        /// </param>
        private void SetSelectedView(
            View view /* in */
            )
        {
            string value = view.ToString();

            //
            // HACK: Fix-up the view mode names that contain spaces.
            //
            value = value.Replace(View.LargeIcon.ToString(), "Large Icon");
            value = value.Replace(View.SmallIcon.ToString(), "Small Icon");

            cboView.SelectedIndex = cboView.FindStringExact(value);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Assigns the small and large image indexes to the list items.
        /// </summary>
        private void SetupItemImageIndexes()
        {
            foreach (ListViewItem item in lstItem.Items)
            {
                int smallIndex = Index.Invalid;
                int largeIndex = Index.Invalid;

                if ((imgSmall.Images.Count > 1) ||
                    (imgLarge.Images.Count > 1))
                {
                    foreach (string key
                            in new string[] { GetValue(item), GetKey(item) })
                    {
                        smallIndex = imgSmall.Images.IndexOfKey(key);
                        largeIndex = imgLarge.Images.IndexOfKey(key);

                        if ((smallIndex != Index.Invalid) ||
                            (largeIndex != Index.Invalid))
                        {
                            break;
                        }
                    }
                }
                else
                {
                    //
                    // NOTE: There appears to be only one icon available;
                    //       therefore, always use it.
                    //
                    smallIndex = 0;
                    largeIndex = 0;
                }

                if ((smallIndex != Index.Invalid) ||
                    (largeIndex != Index.Invalid))
                {
                    if ((smallIndex == Index.Invalid) ||
                        (largeIndex == Index.Invalid) ||
                        (smallIndex == largeIndex))
                    {
                        item.ImageIndex = (smallIndex != Index.Invalid) ?
                            smallIndex : largeIndex;

                        continue;
                    }
                }

                item.ImageIndex = Index.Invalid;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the list view's display mode.
        /// </summary>
        /// <param name="view">
        /// The view to apply.
        /// </param>
        private void SetView(
            View view /* in */
            )
        {
            lstItem.View = view;

            if (view == View.Details)
            {
                LoadColumns();

                lstItem.AutoResizeColumns(
                    ColumnHeaderAutoResizeStyle.ColumnContent);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets whether multiple selection is allowed and applies it to the
        /// list view.
        /// </summary>
        /// <param name="value">
        /// The current value, used to preselect items.
        /// </param>
        /// <param name="multiple">
        /// Non-zero to allow multiple selection.
        /// </param>
        private void SetMultiple(
            string value, /* in */
            bool multiple /* in */
            )
        {
            lstItem.MultiSelect = multiple;
            lstItem.CheckBoxes = multiple;

            if (multiple)
            {
                StringList list = null;
                Result error = null; /* NOT USED */

                if (Parser.SplitList(
                        null, value, 0, Length.Invalid, true,
                        ref list, ref error) != ReturnCode.Ok)
                {
                    return;
                }

                foreach (string element in list)
                {
                    ListViewItem[] items = lstItem.Items.Find(
                        element, false);

                    if ((items == null) || (items.Length == 0))
                        continue;

                    foreach (ListViewItem item in items)
                        item.Checked = true;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Extracts the key, text, and tag from the supplied list element.
        /// </summary>
        /// <param name="element">
        /// The element to read.
        /// </param>
        /// <param name="key">
        /// On output, receives the element key.
        /// </param>
        /// <param name="text">
        /// On output, receives the element text.
        /// </param>
        /// <param name="tag">
        /// On output, receives the element tag.
        /// </param>
        private void GetKeyAndTextAndTag(
            ElementPair element, /* in */
            out string key,      /* out */
            out string text,     /* out */
            out object tag       /* out */
            )
        {
            key = null;
            text = null;
            tag = null;

            StringElementData data = element.Value;

            if (data != null)
            {
                tag = data.Tag;
                text = data.Value;
                key = data.Key;

                if (key != null)
                    return;
            }
            else
            {
                text = element.Key;
            }

            key = element.Key;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the list view's columns (including any extra columns).
        /// </summary>
        private void LoadColumns()
        {
            lstItem.Columns.Clear();
            lstItem.Columns.Add("Item");

            if (extraColumns == null)
                return;

            foreach (string extraColumn in extraColumns)
                lstItem.Columns.Add(extraColumn);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the supplied elements into the list view.
        /// </summary>
        /// <param name="elements">
        /// The elements to load.
        /// </param>
        private void LoadElements(
            ElementDictionary elements /* in */
            )
        {
            lstItem.Items.Clear();

            if (elements == null)
                return;

            foreach (ElementPair element in elements)
            {
                string key;
                string text;
                object tag;

                GetKeyAndTextAndTag(
                    element, out key, out text, out tag);

                if ((key == null) || (text == null))
                    continue;

                ListViewItem item = lstItem.Items.Add(
                    key, text, Index.Invalid);

                if (item == null)
                    continue;

                if (tag != null)
                    item.Tag = tag;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the supplied list view items into the list view.
        /// </summary>
        /// <param name="items">
        /// The items to load.
        /// </param>
        private void LoadItems(
            ListViewItem[] items /* in */
            )
        {
            lstItem.Items.Clear();

            if (items == null)
                return;

            lstItem.Items.AddRange(items);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the small and large icon image lists into the list view.
        /// </summary>
        /// <param name="itemSmallIcons">
        /// The small icons keyed by item.
        /// </param>
        /// <param name="itemLargeIcons">
        /// The large icons keyed by item.
        /// </param>
        private void LoadIcons(
            IDictionary<string, Icon> itemSmallIcons, /* in */
            IDictionary<string, Icon> itemLargeIcons  /* in */
            )
        {
            imgSmall.Images.Clear();

            if (itemSmallIcons != null)
            {
                foreach (KeyValuePair<string, Icon> pair in itemSmallIcons)
                    imgSmall.Images.Add(pair.Key, pair.Value);
            }

            ///////////////////////////////////////////////////////////////////

            imgLarge.Images.Clear();

            if (itemLargeIcons != null)
            {
                foreach (KeyValuePair<string, Icon> pair in itemLargeIcons)
                    imgLarge.Images.Add(pair.Key, pair.Value);
            }

            ///////////////////////////////////////////////////////////////////

            SetupItemImageIndexes();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the single selected list view item.
        /// </summary>
        /// <returns>
        /// The selected item, or null when none.
        /// </returns>
        private ListViewItem GetItem()
        {
            SelectedItemCollection selectedItems = lstItem.SelectedItems;

            if ((selectedItems == null) || (selectedItems.Count == 0))
                return null;

            return selectedItems[0];
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the selected list view items.
        /// </summary>
        /// <returns>
        /// The selected items.
        /// </returns>
        private ListViewItem[] GetItems()
        {
            CheckedItemCollection checkedItems = lstItem.CheckedItems;

            if (checkedItems == null)
                return null;

            int count = checkedItems.Count;

            if (count == 0)
                return null;

            ListViewItem[] items = new ListViewItem[count];

            for (int index = 0; index < count; index++)
                items[index] = checkedItems[index];

            return items;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
        /// <summary>
        /// Loads the default item icon into the supplied icon collection.
        /// </summary>
        /// <param name="icons">
        /// The icon collection to add the default icon to.
        /// </param>
        private static void LoadDefaultIcon(
            ref IDictionary<string, Icon> icons /* in, out */
            )
        {
            if (icons == null)
                icons = new Dictionary<string, Icon>();

            Stream stream = ManagerOps.GetResourceStream(
                null, DefaultIconResourceName);

            if (stream != null)
                icons.Add(String.Empty, new Icon(stream)); /* throw */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key of the supplied list view item.
        /// </summary>
        /// <param name="item">
        /// The item whose key is requested.
        /// </param>
        /// <returns>
        /// The item key.
        /// </returns>
        private static string GetKey(
            ListViewItem item /* in */
            )
        {
            if (item == null)
                return null;

            return item.Name;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value (text) of the supplied list view item.
        /// </summary>
        /// <param name="item">
        /// The item whose value is requested.
        /// </param>
        /// <returns>
        /// The item value.
        /// </returns>
        private static string GetValue(
            ListViewItem item /* in */
            )
        {
            if (item == null)
                return null;

            if (item.Tag is string)
                return (string)item.Tag;

            return item.Text;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the values (text) of the supplied list view items as a list.
        /// </summary>
        /// <param name="items">
        /// The items whose values are requested.
        /// </param>
        /// <returns>
        /// The item values.
        /// </returns>
        private static string GetValues(
            ListViewItem[] items /* in */
            )
        {
            if (items == null)
                return null;

            StringList list = new StringList();

            for (int index = 0; index < items.Length; index++)
                list.Add(GetValue(items[index]));

            return list.ToString();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Creates and shows the form as an executable-file chooser, returning
        /// the selected file name.
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
        /// <param name="fileNames">
        /// The candidate files to list.
        /// </param>
        /// <param name="all">
        /// Non-zero to include files without icons or descriptions.
        /// </param>
        /// <param name="fileName">
        /// On output, receives the selected file name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowExecutableFileList(
            IWin32Window owner,          /* in */
            Interpreter interpreter,     /* in */
            string varName,              /* in */
            int id,                      /* in */
            ElementDictionary fileNames, /* in */
            bool all,                    /* in */
            ref string fileName,         /* in, out */
            ref Result error             /* out */
            )
        {
            if (fileNames == null)
            {
                error = "invalid file names";
                return ReturnCode.Error;
            }

            IEnumerable<string> extraColumns = new string[] {
                "File Name", "Company Name", "Product Name", "Version",
                "Description", "Comments", "Creation Time", "Source(s)"
            };

            ListViewItem[] fileItems = null;
            IDictionary<string, Icon> fileIcons = null;
            ListViewItem fileItem = null;

            if ((IconOps.GetExecutableFileListViewItems(
                    fileNames, all, ref fileItems, ref fileIcons,
                    ref error) == ReturnCode.Ok) &&
                (ShowItemList(
                    owner, interpreter, varName, id,
                    "Select Executable File", View.Details,
                    extraColumns, fileItems, fileIcons, fileIcons,
                    ref fileItem, ref error) == ReturnCode.Ok))
            {
                fileName = GetValue(fileItem);
                return ReturnCode.Ok;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and shows the form as an item chooser over the supplied
        /// elements, returning the selected value.
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
        /// <param name="title">
        /// The form title.
        /// </param>
        /// <param name="elements">
        /// The elements to list.
        /// </param>
        /// <param name="multiple">
        /// Non-zero to allow multiple selection.
        /// </param>
        /// <param name="value">
        /// On input, the initial selection; on output, the selected value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowItemList(
            IWin32Window owner,         /* in */
            Interpreter interpreter,    /* in */
            string varName,             /* in */
            int id,                     /* in */
            string title,               /* in */
            ElementDictionary elements, /* in */
            bool multiple,              /* in */
            ref string value,           /* in, out */
            ref Result error            /* out */
            )
        {
            IDictionary<string, Icon> valueIcons = null;

            LoadDefaultIcon(ref valueIcons);

            return ShowItemList(
                owner, interpreter, varName, id, title, View.List, null,
                elements, valueIcons, valueIcons, multiple, ref value,
                ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and shows the form as an item chooser with an explicit
        /// view, extra columns, and icons, returning the selected value.
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
        /// <param name="title">
        /// The form title.
        /// </param>
        /// <param name="view">
        /// The initial list view mode.
        /// </param>
        /// <param name="extraColumns">
        /// The number of extra columns to show.
        /// </param>
        /// <param name="elements">
        /// The elements to list.
        /// </param>
        /// <param name="itemSmallIcons">
        /// The small icons keyed by item.
        /// </param>
        /// <param name="itemLargeIcons">
        /// The large icons keyed by item.
        /// </param>
        /// <param name="multiple">
        /// Non-zero to allow multiple selection.
        /// </param>
        /// <param name="value">
        /// On input, the initial selection; on output, the selected value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowItemList(
            IWin32Window owner,                       /* in */
            Interpreter interpreter,                  /* in */
            string varName,                           /* in */
            int id,                                   /* in */
            string title,                             /* in */
            View view,                                /* in */
            IEnumerable<string> extraColumns,         /* in */
            ElementDictionary elements,               /* in */
            IDictionary<string, Icon> itemSmallIcons, /* in */
            IDictionary<string, Icon> itemLargeIcons, /* in */
            bool multiple,                            /* in */
            ref string value,                         /* in, out */
            ref Result error                          /* out */
            )
        {
            try
            {
                using (SelectListItemForm form = new SelectListItemForm(
                        id, interpreter, varName, extraColumns))
                {
                    form.Text = (title != null) ? title : DefaultTitle;
                    form.LoadElements(elements);
                    form.LoadIcons(itemSmallIcons, itemLargeIcons);
                    form.SetSelectedView(view);
                    form.SetMultiple(value, multiple);

                    if (form.ShowDialog(owner) == DialogResult.OK)
                    {
                        if (multiple)
                            value = GetValues(form.GetItems());
                        else
                            value = GetValue(form.GetItem());

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

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and shows the form as an item chooser over pre-built list
        /// view items, returning the selected item.
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
        /// <param name="title">
        /// The form title.
        /// </param>
        /// <param name="view">
        /// The initial list view mode.
        /// </param>
        /// <param name="extraColumns">
        /// The number of extra columns to show.
        /// </param>
        /// <param name="items">
        /// The pre-built list view items.
        /// </param>
        /// <param name="itemSmallIcons">
        /// The small icons keyed by item.
        /// </param>
        /// <param name="itemLargeIcons">
        /// The large icons keyed by item.
        /// </param>
        /// <param name="item">
        /// On output, receives the selected item.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowItemList(
            IWin32Window owner,                       /* in */
            Interpreter interpreter,                  /* in */
            string varName,                           /* in */
            int id,                                   /* in */
            string title,                             /* in */
            View view,                                /* in */
            IEnumerable<string> extraColumns,         /* in */
            ListViewItem[] items,                     /* in */
            IDictionary<string, Icon> itemSmallIcons, /* in */
            IDictionary<string, Icon> itemLargeIcons, /* in */
            ref ListViewItem item,                    /* in, out */
            ref Result error                          /* out */
            )
        {
            try
            {
                using (SelectListItemForm form = new SelectListItemForm(
                        id, interpreter, varName, extraColumns))
                {
                    form.Text = (title != null) ? title : DefaultTitle;
                    form.LoadItems(items);
                    form.LoadIcons(itemSmallIcons, itemLargeIcons);
                    form.SetSelectedView(view);

                    if (form.ShowDialog(owner) == DialogResult.OK)
                    {
                        item = form.GetItem();
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
                    typeof(SelectListItemForm).Name);
            }
#endif
        }
        #endregion
    }
}
