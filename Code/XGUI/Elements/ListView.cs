using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XGUI;

/// <summary>
/// A Win32-inspired ListView control for XGUI, usable from C# and Razor.
/// </summary>
public class ListView : Panel
{
	public enum ListViewMode
	{
		List,
		Details,
		Icons
	}

	public class ListViewColumn
	{
		public string Header { get; set; }
		public string Field { get; set; }
		public int Width { get; set; } = 100;
	}

	public class ListViewItem : Panel
	{
		private ListView Parent { get; }
		public object Data { get; }
		public List<string> SubItems { get; }
		public bool IsSelected { get; private set; }
		private List<Panel> Cells { get; } = new();
		public XGUIIconPanel IconPanel;
		private TextEntry _renameEntry;

		public ListViewItem( ListView parent, object data, List<string> subItems )
		{
			Parent = parent;
			Data = data;
			SubItems = subItems;

			AddClass( "listview-item" );

			// Add event handlers
			AddEventListener( "onclick", OnItemClicked );
			AddEventListener( "ondoubleclick", OnItemDoubleClicked );

			// Initial render based on parent's current view mode
			UpdateViewMode( Parent.ViewMode );
		}

		public void UpdateViewMode( ListViewMode viewMode )
		{
			// Clear existing cells
			DeleteChildren();

			Cells.Clear();

			// Update classes for the view mode
			SetClass( "listview-row", viewMode == ListViewMode.Details || viewMode == ListViewMode.List );
			SetClass( "listview-icon-item", viewMode == ListViewMode.Icons );

			// Reuse existing icon panel or create a new one
			if ( IconPanel == null )
			{
				IconPanel = new XGUIIconPanel();
			}
			else
			{

				var originalIconName = IconPanel.IconName;
				var originalIconType = IconPanel.IconType;
				var originalIconSize = IconPanel.IconSize;
				IconPanel.Delete();
				IconPanel = new XGUIIconPanel
				{
					IconName = originalIconName,
					IconType = originalIconType,
					IconSize = originalIconSize
				};
			}
			IconPanel.SetClass( "listview-icon", true );

			if ( viewMode == ListViewMode.Details )
			{
				// Create a cell for each column
				for ( int i = 0; i < Parent.Columns.Count; i++ )
				{
					var cell = new Panel();
					if ( i == 0 )
					{
						cell.AddChild( IconPanel );
					}
					cell.AddClass( "listview-cell" );
					cell.Style.Width = Parent.Columns[i].Width;

					string text = (SubItems != null && i < SubItems.Count) ? SubItems[i] : "";
					cell.AddChild( new Label { Text = text } );

					Cells.Add( cell );
					AddChild( cell );
				}
			}
			else if ( viewMode == ListViewMode.List || viewMode == ListViewMode.Icons )
			{
				// Just show the first column
				AddChild( IconPanel );
				string text = (SubItems != null && SubItems.Count > 0) ? SubItems[0] : "";
				AddChild( new Label { Text = text } );
			}


			// Always update selected state
			SetSelected( IsSelected );
		}

		private void OnItemClicked()
		{
			Parent.SelectItem( this );
		}

		private void OnItemDoubleClicked()
		{
			Parent.OnItemActivated?.Invoke( this );
		}

		public void SetSelected( bool selected )
		{
			IsSelected = selected;
			SetClass( "selected", selected );
		}

		public void BeginRename( Action<string> onRenameComplete )
		{
			// Remove existing label(s)
			foreach ( var child in Children.ToList() )
			{
				if ( child is Label )
					child.Delete();
			}

			// Create and add TextEntry
			_renameEntry = new TextEntry
			{
				Text = (SubItems != null && SubItems.Count > 0) ? SubItems[0] : "",
				Style = { Width = Length.Percent( 100 ) }
			};
			AddChild( _renameEntry );
			_renameEntry.Focus();

			//_renameEntry.OnBlur += () => EndRename( onRenameComplete );
			//_renameEntry.OnEnterPressed += () => EndRename( onRenameComplete );
			_renameEntry.AddEventListener( "onblur", () => EndRename( onRenameComplete ) );
			_renameEntry.AddEventListener( "onsubmit", () => EndRename( onRenameComplete ) );
		}

		private void EndRename( Action<string> onRenameComplete )
		{
			if ( _renameEntry == null ) return;
			string newName = _renameEntry.Text;
			_renameEntry.Delete();
			_renameEntry = null;

			// Restore label
			string text = newName;
			AddChild( new Label { Text = text } );

			onRenameComplete?.Invoke( newName );
		}
	}

	private Panel ItemContainer { get; set; }

	public List<ListViewColumn> Columns { get; } = new();
	public List<ListViewItem> Items { get; } = new();
	private ListViewMode _viewMode = ListViewMode.Details;
	public ListViewMode ViewMode
	{
		get => _viewMode;
		set
		{
			if ( _viewMode != value )
			{
				_viewMode = value;
				UpdateItems();
			}
		}
	}

	public Action<ListViewItem> OnItemSelected { get; set; }
	public Action<ListViewItem> OnItemActivated { get; set; }

	// Sorting properties
	private int _sortColumnIndex = -1;
	private bool _sortAscending = true;

	public ListView()
	{
		ItemContainer = new Panel();
		ItemContainer.AddClass( "listview-container" );
		AddClass( "listview" );
		InitializeHeader();
	}

	public void AddColumn( string header, string field, int width = 100 )
	{
		Columns.Add( new ListViewColumn { Header = header, Field = field, Width = width } );
		UpdateHeader();
	}

	public void AddItem( object data, List<string> subItems )
	{
		var item = new ListViewItem( this, data, subItems );
		Items.Add( item );

		// If we have a sort column, resort the items
		if ( _sortColumnIndex >= 0 )
		{
			SortItems();
			UpdateItems();
		}
		else
		{
			// Add to the correct container based on view mode
			if ( ViewMode == ListViewMode.Icons && ItemContainer != null )
			{
				ItemContainer.AddChild( item );
			}
			else
			{
				ItemContainer.AddChild( item );
			}
		}
	}

	/// <summary>
	/// Selects an item and triggers the selection event.
	/// </summary>
	public void SelectItem( ListViewItem item )
	{
		foreach ( var i in Items )
		{
			i.SetSelected( false );
		}

		item.SetSelected( true );
		OnItemSelected?.Invoke( item );
	}

	/// <summary>
	/// Sorts the ListView items by the specified column index.
	/// </summary>
	public void SortByColumn( int columnIndex )
	{
		// If clicking the same column, toggle direction
		if ( _sortColumnIndex == columnIndex )
		{
			_sortAscending = !_sortAscending;
		}
		else
		{
			_sortColumnIndex = columnIndex;
			_sortAscending = true;
		}

		// Sort the items
		SortItems();

		// Update header to show sort indicators
		UpdateHeader();

		// Refresh the view
		UpdateItems();
	}

	/// <summary>
	/// Sorts the items based on current sort settings.
	/// </summary>
	private void SortItems()
	{
		if ( _sortColumnIndex < 0 || _sortColumnIndex >= Columns.Count )
			return;

		// Create a temporary sorted list
		var sortedItems = new List<ListViewItem>();

		if ( _sortAscending )
		{
			sortedItems = Items
				.OrderBy( item =>
					item.SubItems != null && _sortColumnIndex < item.SubItems.Count
					? item.SubItems[_sortColumnIndex]
					: "" )
				.ToList();
		}
		else
		{
			sortedItems = Items
				.OrderByDescending( item =>
					item.SubItems != null && _sortColumnIndex < item.SubItems.Count
					? item.SubItems[_sortColumnIndex]
					: "" )
				.ToList();
		}

		// Replace items with sorted list
		Items.Clear();
		Items.AddRange( sortedItems );
	}

	/// <summary>
	/// Updates the header based on current columns.
	/// </summary>
	private void UpdateHeader()
	{
		// Find and remove the existing header if it exists
		var existingHeader = ChildrenOfType<Panel>().FirstOrDefault( p => p.HasClass( "listview-header" ) );
		existingHeader?.Delete();

		// Create a new header
		InitializeHeader();
	}

	/// <summary>
	/// Initializes the header with current columns.
	/// </summary>
	public void InitializeHeader()
	{
		if ( ViewMode == ListViewMode.Details && Columns.Any() )
		{
			var header = new Panel();
			header.AddClass( "listview-header" );

			for ( int i = 0; i < Columns.Count; i++ )
			{
				var col = Columns[i];
				var colPanel = new Panel();
				colPanel.AddClass( "listview-header-column" );
				colPanel.Style.Width = col.Width;

				// Make a row for the header text and sort indicator
				var headerRow = new Panel();
				headerRow.Style.FlexDirection = FlexDirection.Row;
				headerRow.Style.AlignItems = Align.Center;

				// Add the header text
				headerRow.AddChild( new Label { Text = col.Header } );

				// Add sort indicator if this is the sort column
				if ( _sortColumnIndex == i )
				{
					var sortIndicator = new Label
					{
						Text = _sortAscending ? "▲" : "▼",
						Style =
						{
							MarginLeft = 4,
							FontSize = 8
						}
					};
					sortIndicator.AddClass( "sort-indicator" );
					sortIndicator.AddClass( _sortAscending ? "sort-up" : "sort-down" );
					headerRow.AddChild( sortIndicator );
				}

				colPanel.AddChild( headerRow );

				// Add click handler for sorting (capture the index)
				int columnIndex = i; // Capture for closure
				colPanel.AddEventListener( "onclick", () => SortByColumn( columnIndex ) );

				header.AddChild( colPanel );
			}

			AddChild( header );
		}
	}

	public void ClearList()
	{
		foreach ( var child in Children.ToList() )
		{
			if ( child.ElementName == "element" ) continue;
			child.Parent = null;
		}
	}

	/// <summary>
	/// Updates all items to match the current view mode.
	/// </summary>
	public void UpdateItems()
	{
		ClearList();
		// Clear all children first
		// Re-add the header for details mode
		InitializeHeader();

		// Special handling for Icons view - use a container for grid layout
		if ( ViewMode == ListViewMode.Icons )
		{
			ItemContainer = new Panel();
			ItemContainer.AddClass( "listview-container" );
			ItemContainer.AddClass( "listview-icon-container" );
			AddChild( ItemContainer );

			// Update all items and add them to the container
			foreach ( var item in Items )
			{
				item.UpdateViewMode( ViewMode );
				ItemContainer.AddChild( item );
			}
		}
		else
		{
			ItemContainer = new Panel();
			ItemContainer.AddClass( "listview-container" );
			AddChild( ItemContainer );
			// For other views, add items directly to the ListView
			foreach ( var item in Items )
			{
				item.UpdateViewMode( ViewMode );
				ItemContainer.AddChild( item );
			}
		}
	}
}
