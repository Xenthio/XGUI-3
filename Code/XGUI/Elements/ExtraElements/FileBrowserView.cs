using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XGUI;

/// <summary>
/// View modes available for FileBrowserView
/// </summary>
public enum FileBrowserViewMode
{
	Icons,
	List,
	Details
}

/// <summary>
/// A panel that displays files in a folder with different view modes (Icons, List, Details).
/// </summary>
public class FileBrowserView : Panel
{
	public BaseFileSystem CurrentFileSystem;
	public string CurrentPath;
	private List<FileItem> FileItems = new();
	private Panel ItemContainer;
	private Dictionary<string, FileItem> SelectedItems = new();

	// View mode properties
	private FileBrowserViewMode _viewMode = FileBrowserViewMode.Icons;
	public FileBrowserViewMode ViewMode
	{
		get => _viewMode;
		set
		{
			if ( _viewMode == value )
				return;

			_viewMode = value;
			UpdateViewMode();
			if ( CurrentFileSystem != null && !string.IsNullOrEmpty( CurrentPath ) )
				Refresh();
		}
	}

	// Sort properties
	private string _sortField = "Name";
	private bool _sortAscending = true;

	// Events
	public event Action<string> OnFileSelected;
	public event Action<string> OnDirectorySelected;
	public event Action<string> OnNavigateTo;
	public event Action<FileBrowserViewMode> OnViewModeChanged;

	public FileBrowserView()
	{
		AddClass( "file-browser-view" );

		// Create item container
		ItemContainer = new Panel();
		ItemContainer.AddClass( "item-container" );
		AddChild( ItemContainer );

		// Initialize with Icons view
		UpdateViewMode();
	}

	private void UpdateViewMode()
	{
		ItemContainer.SetClass( "icons-view", ViewMode == FileBrowserViewMode.Icons );
		ItemContainer.SetClass( "list-view", ViewMode == FileBrowserViewMode.List );
		ItemContainer.SetClass( "details-view", ViewMode == FileBrowserViewMode.Details );

		// Notify about view mode change
		OnViewModeChanged?.Invoke( ViewMode );
	}

	/// <summary>
	/// Switches between view modes (Icons, List, Details)
	/// </summary>
	public void CycleViewMode()
	{
		ViewMode = ViewMode switch
		{
			FileBrowserViewMode.Icons => FileBrowserViewMode.List,
			FileBrowserViewMode.List => FileBrowserViewMode.Details,
			FileBrowserViewMode.Details => FileBrowserViewMode.Icons,
			_ => FileBrowserViewMode.Icons
		};
	}

	public void NavigateTo( string path )
	{
		CurrentPath = path;
		OnNavigateTo?.Invoke( path );
		Refresh();
	}

	public void Refresh()
	{
		ItemContainer.DeleteChildren();
		FileItems.Clear();
		SelectedItems.Clear();

		if ( string.IsNullOrEmpty( CurrentPath ) || CurrentFileSystem == null )
			return;

		if ( !CurrentFileSystem.DirectoryExists( CurrentPath ) )
			return;

		// Create header row for details view
		if ( ViewMode == FileBrowserViewMode.Details )
		{
			var header = new Panel();
			header.AddClass( "details-header" );

			// Create column headers
			var nameHeader = new Panel();
			nameHeader.AddClass( "column-header" );
			nameHeader.AddClass( "name-column" );
			nameHeader.AddEventListener( "onclick", () => SortItems( "Name" ) );
			nameHeader.AddChild( new Label { Text = "Name" } );

			var typeHeader = new Panel();
			typeHeader.AddClass( "column-header" );
			typeHeader.AddClass( "type-column" );
			typeHeader.AddEventListener( "onclick", () => SortItems( "Type" ) );
			typeHeader.AddChild( new Label { Text = "Type" } );

			var sizeHeader = new Panel();
			sizeHeader.AddClass( "column-header" );
			sizeHeader.AddClass( "size-column" );
			sizeHeader.AddEventListener( "onclick", () => SortItems( "Size" ) );
			sizeHeader.AddChild( new Label { Text = "Size" } );

			// Add column headers to header row
			header.AddChild( nameHeader );
			header.AddChild( typeHeader );
			header.AddChild( sizeHeader );

			// Add header to container
			ItemContainer.AddChild( header );
		}

		try
		{
			// Get directories first
			List<string> directories = CurrentFileSystem.FindDirectory( CurrentPath ).ToList();

			// Get files
			List<string> files = CurrentFileSystem.FindFile( CurrentPath ).ToList();

			// Sort directories and files
			directories = SortPaths( directories, true );
			files = SortPaths( files, false );

			// Display directories first
			foreach ( var dir in directories )
			{
				var dirName = System.IO.Path.GetFileName( dir );
				var item = new FileItem( this, dir, dirName, true );
				FileItems.Add( item );
				ItemContainer.AddChild( item );
			}

			// Then display files
			foreach ( var file in files )
			{
				var fileName = System.IO.Path.GetFileName( file );
				var item = new FileItem( this, file, fileName, false );
				FileItems.Add( item );
				ItemContainer.AddChild( item );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error accessing directory: {ex.Message}" );
		}
	}

	private List<string> SortPaths( List<string> paths, bool isDirectories )
	{
		if ( _sortField == "Name" )
		{
			return _sortAscending
				? paths.OrderBy( p => System.IO.Path.GetFileName( p ) ).ToList()
				: paths.OrderByDescending( p => System.IO.Path.GetFileName( p ) ).ToList();
		}
		else if ( _sortField == "Type" && !isDirectories )
		{
			return _sortAscending
				? paths.OrderBy( p => System.IO.Path.GetExtension( p ) ).ToList()
				: paths.OrderByDescending( p => System.IO.Path.GetExtension( p ) ).ToList();
		}

		// Default sort by name
		return _sortAscending
			? paths.OrderBy( p => System.IO.Path.GetFileName( p ) ).ToList()
			: paths.OrderByDescending( p => System.IO.Path.GetFileName( p ) ).ToList();
	}

	private void SortItems( string field )
	{
		// If clicking the same field, toggle sort direction
		if ( _sortField == field )
		{
			_sortAscending = !_sortAscending;
		}
		else
		{
			_sortField = field;
			_sortAscending = true;
		}

		// Refresh to apply new sorting
		Refresh();
	}

	public void SelectItem( FileItem item, bool multiSelect = false )
	{
		// Clear previous selections if not multiselect
		if ( !multiSelect )
		{
			foreach ( var selected in SelectedItems.Values )
			{
				selected.SetSelected( false );
			}
			SelectedItems.Clear();
		}

		// Toggle selection state for this item
		bool isSelected = SelectedItems.ContainsKey( item.FullPath );
		if ( isSelected )
		{
			item.SetSelected( false );
			SelectedItems.Remove( item.FullPath );
		}
		else
		{
			item.SetSelected( true );
			SelectedItems[item.FullPath] = item;
		}
	}

	public void UnselectAll()
	{
		foreach ( var selected in SelectedItems.Values )
		{
			selected.SetSelected( false );
		}
		SelectedItems.Clear();
	}

	public class FileItem : Panel
	{
		public FileBrowserView Parent;
		public string Name;
		public string FullPath;
		public bool IsDirectory;
		private bool IsSelected;

		// UI Elements
		private XGUIIconPanel IconPanel;
		private Label NameLabel;
		private Label TypeLabel;
		private Label SizeLabel;

		public FileItem( FileBrowserView parent, string path, string name, bool isDirectory )
		{
			Parent = parent;
			Name = name;
			FullPath = path;
			IsDirectory = isDirectory;
			IsSelected = false;

			SetupUI();
			AddEventListeners();
		}

		private void SetupUI()
		{
			// Add classes based on view mode
			AddClass( "file-item" );
			AddClass( IsDirectory ? "directory-item" : "file-item" );

			// Create UI elements based on view mode
			if ( Parent.ViewMode == FileBrowserViewMode.Icons )
			{
				SetupIconsView();
			}
			else if ( Parent.ViewMode == FileBrowserViewMode.List )
			{
				SetupListView();
			}
			else if ( Parent.ViewMode == FileBrowserViewMode.Details )
			{
				SetupDetailsView();
			}
		}

		private void SetupIconsView()
		{
			AddClass( "icons-view-item" );

			// Create icon using XGUIIconSystem
			IconPanel = new XGUIIconPanel();
			IconPanel.AddClass( "item-icon" );

			if ( IsDirectory )
			{
				IconPanel.SetFolderIcon( "folder", 32 );
			}
			else
			{
				string extension = System.IO.Path.GetExtension( Name );
				IconPanel.SetFileIcon( extension, 32 );
			}

			AddChild( IconPanel );

			// Create label
			NameLabel = new Label();
			NameLabel.Text = Name;
			NameLabel.AddClass( "item-label" );
			AddChild( NameLabel );
		}


		private void SetupListView()
		{
			AddClass( "list-view-item" );

			// Create a horizontal layout
			Style.FlexDirection = FlexDirection.Row;
			Style.AlignItems = Align.Center;

			// Create small icon
			IconPanel = new XGUIIconPanel();
			IconPanel.AddClass( "item-icon" );
			IconPanel.AddClass( "small-icon" );

			if ( IsDirectory )
			{
				IconPanel.SetFolderIcon( "folder", 16 );
			}
			else
			{
				string extension = System.IO.Path.GetExtension( Name );
				IconPanel.SetFileIcon( extension, 16 );
			}

			AddChild( IconPanel );

			// Create label
			NameLabel = new Label();
			NameLabel.Text = Name;
			NameLabel.AddClass( "item-label" );
			AddChild( NameLabel );
		}

		private void SetupDetailsView()
		{
			AddClass( "details-view-item" );

			// Create a row with columns
			Style.FlexDirection = FlexDirection.Row;

			// Name column (includes icon and name)
			var nameColumn = new Panel();
			nameColumn.AddClass( "column" );
			nameColumn.AddClass( "name-column" );
			nameColumn.Style.FlexDirection = FlexDirection.Row;
			nameColumn.Style.AlignItems = Align.Center;

			// Create small icon
			IconPanel = new XGUIIconPanel();
			IconPanel.AddClass( "item-icon" );
			IconPanel.AddClass( "tiny-icon" );

			if ( IsDirectory )
			{
				IconPanel.SetFolderIcon( "folder", 16 );
			}
			else
			{
				string extension = System.IO.Path.GetExtension( Name );
				IconPanel.SetFileIcon( extension, 16 );
			}

			nameColumn.AddChild( IconPanel );

			// Create name label
			NameLabel = new Label();
			NameLabel.Text = Name;
			NameLabel.AddClass( "item-label" );
			nameColumn.AddChild( NameLabel );

			AddChild( nameColumn );

			// Type column
			var typeColumn = new Panel();
			typeColumn.AddClass( "column" );
			typeColumn.AddClass( "type-column" );

			// Create type label
			TypeLabel = new Label();
			TypeLabel.Text = GetFileType();
			TypeLabel.AddClass( "item-label" );
			typeColumn.AddChild( TypeLabel );

			AddChild( typeColumn );

			// Size column
			var sizeColumn = new Panel();
			sizeColumn.AddClass( "column" );
			sizeColumn.AddClass( "size-column" );

			// Create size label
			SizeLabel = new Label();
			SizeLabel.Text = GetFileSize();
			SizeLabel.AddClass( "item-label" );
			sizeColumn.AddChild( SizeLabel );

			AddChild( sizeColumn );
		}

		private string GetFileType()
		{
			if ( IsDirectory )
				return "File Folder";

			string extension = System.IO.Path.GetExtension( Name ).ToLower();
			return !string.IsNullOrEmpty( extension ) ? extension.Substring( 1 ).ToUpper() + " File" : "File";
		}

		private string GetFileSize()
		{
			if ( IsDirectory )
				return "";

			try
			{
				// TODO: Get actual file size if possible with BaseFileSystem
				return ""; // For now, return empty for files too
			}
			catch
			{
				return "";
			}
		}

		private void AddEventListeners()
		{
			// Handle single click
			AddEventListener( "onclick", ( e ) =>
			{
				// Check if ctrl is pressed for multi-select
				bool multiSelect = Input.Keyboard.Down( "control" );

				// Select this item
				Parent.SelectItem( this, multiSelect );

				// Trigger appropriate event
				if ( IsDirectory )
				{
					Parent.OnDirectorySelected?.Invoke( FullPath );
				}
				else
				{
					Parent.OnFileSelected?.Invoke( FullPath );
				}
			} );

			// Handle double click
			AddEventListener( "ondoubleclick", () =>
			{
				if ( IsDirectory )
				{
					Parent.NavigateTo( FullPath );
				}
				// Could add file opening logic here in the future
			} );
		}

		public void SetSelected( bool selected )
		{
			IsSelected = selected;
			SetClass( "selected", selected );
		}
	}
}
