using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Unity.PerformanceTracking
{
    public class SnippetItem : TreeViewItem
    {
        public int index;
    }

    public class SnippetListView : TreeView
    {
        IList<ProfilingSnippet> m_Model;

        public event Action<ProfilingSnippet> doubleClicked;

        public SnippetListView(IList<ProfilingSnippet> model)
            : base(new TreeViewState(), new MultiColumnHeader(CreateDefaultMultiColumnHeaderState()))
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            m_Model = model;

            Reload();
        }

        protected override void DoubleClickedItem(int id)
        {
            var item = FindItem(id, this.rootItem);
            var snippetItem = item as SnippetItem;
            var snippet = m_Model[snippetItem.index];
            doubleClicked?.Invoke(snippet);
        }

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            var snippetItem = item as SnippetItem;
            var snippet = m_Model[snippetItem.index];

            if (snippet.category != null && snippet.category.IndexOf(search, StringComparison.OrdinalIgnoreCase) != -1)
                return true;

            if (snippet.label != null && snippet.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) != -1)
                return true;

            if (snippet.sampleName != null && snippet.sampleName.IndexOf(search, StringComparison.OrdinalIgnoreCase) != -1)
                return true;

            if (snippet.markerName != null && snippet.markerName.IndexOf(search, StringComparison.OrdinalIgnoreCase) != -1)
                return true;

            return false;
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new SnippetItem { id = 0, index = -1, depth = -1, displayName = "Root" };
            var index = 0;
            var allItems = m_Model.Select(snippet => new SnippetItem { id = snippet.id, index = index++, depth = 0, displayName = $"{snippet.label}" }).Cast<TreeViewItem>().ToList();

            // Utility method that initializes the TreeViewItem.children and .parent for all items.
            SetupParentsAndChildrenFromDepths(root, allItems);

            // Return root of the tree
            return root;
        }

        public bool IsFirstItemSelected()
        {
            var selection = GetSelection();
            if (selection.Count == 0)
                return false;

            var allRows = GetRows();
            if (allRows.Count == 0)
                return false;
            var selectedItems = FindRows(selection);
            return allRows[0] == selectedItems[0];
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item;
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, TreeViewItem item, int column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            var snippetItem = item as SnippetItem;
            var snippet = m_Model[snippetItem.index];
            switch (column)
            {
                case 0:
                    {
                        DefaultGUI.Label(cellRect, snippet.category, args.selected, args.focused);
                    }
                    break;
                case 1:
                    {
                        DefaultGUI.Label(cellRect, snippet.label, args.selected, args.focused);
                    }
                    break;
            }
        }

        public void ResizeColumn(float listViewWidth)
        {
            multiColumnHeader.state.columns[1].width = listViewWidth - 70 - 25;
        }

        static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Category"),
                    headerTextAlignment = TextAlignment.Left,
                    canSort = false,
                    width = 70,
                    minWidth = 60,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Name"),
                    headerTextAlignment = TextAlignment.Left,
                    canSort = false,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 400,
                    minWidth = 100,
                    autoResize = true
                }
            };

            var state = new MultiColumnHeaderState(columns);
            return state;
        }
    }
}