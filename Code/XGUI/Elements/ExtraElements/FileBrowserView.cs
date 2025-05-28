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
	public List<FileItem> FileItems = new();
	public ListView ListView;

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

	// Events
	public event Action<string> OnFileSelected;
	public event Action<string> OnDirectorySelected;
	public event Action<string> OnFileOpened;
	public event Action<string> OnDirectoryOpened;
	public event Action<string> OnNavigateTo;
	public event Action OnPreAddFiles;
	public event Action OnPostAddFiles;
	public event Action<FileBrowserViewMode> OnViewModeChanged;

	public FileBrowserView()
	{
		AddClass( "file-browser-view" );

		// Create the ListView component
		ListView = AddChild<ListView>();
		ListView.AddClass( "file-browser-listview" );

		// Add standard columns for details view
		ListView.AddColumn( "Name", "name", 300 );
		ListView.AddColumn( "Type", "type", 150 );
		ListView.AddColumn( "Size", "size", 100 );

		// Connect ListView events to FileBrowserView events
		ListView.OnItemSelected += OnListViewItemSelected;
		ListView.OnItemActivated += OnListViewItemActivated;

		// Initialize with Icons view
		UpdateViewMode();
	}

	protected void OnListViewItemSelected( ListView.ListViewItem item )
	{
		var fileItem = item.Data as FileItem;
		if ( fileItem != null )
		{
			if ( fileItem.IsDirectory )
			{
				OnDirectorySelected?.Invoke( fileItem.FullPath );
			}
			else
			{
				OnFileSelected?.Invoke( fileItem.FullPath );
			}
		}
	}

	protected void OnListViewItemActivated( ListView.ListViewItem item )
	{
		var fileItem = item.Data as FileItem;
		if ( fileItem != null )
		{
			if ( fileItem.IsDirectory )
			{
				OnDirectoryOpened?.Invoke( fileItem.FullPath );
			}
			else
			{
				OnFileOpened?.Invoke( fileItem.FullPath );
			}
		}
	}

	public void UpdateViewMode()
	{
		// Map FileBrowserViewMode to ListView.ListViewMode
		ListView.ViewMode = ViewMode switch
		{
			FileBrowserViewMode.Icons => ListView.ListViewMode.Icons,
			FileBrowserViewMode.List => ListView.ListViewMode.List,
			FileBrowserViewMode.Details => ListView.ListViewMode.Details,
			_ => ListView.ListViewMode.Icons
		};

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

	/// <summary>
	/// Properly raises the NavigateTo event
	/// </summary>
	public void RaiseNavigateToEvent( string path )
	{
		OnNavigateTo?.Invoke( path );
	}

	public void NavigateTo( string path )
	{
		CurrentPath = path;
		RaiseNavigateToEvent( path );
		Refresh();
	}

	public virtual void SetupHeader()
	{
		// Not needed anymore as ListView handles its own header
	}

	public virtual void Refresh()
	{
		FileItems.Clear();
		ListView.Items.Clear();

		// Important: Update the ListView UI
		ListView.DeleteChildren();
		ListView.InitializeHeader();

		if ( string.IsNullOrEmpty( CurrentPath ) || CurrentFileSystem == null )
			return;

		if ( !CurrentFileSystem.DirectoryExists( CurrentPath ) )
			return;

		try
		{
			// Get directories first
			List<string> directories = CurrentFileSystem.FindDirectory( CurrentPath ).ToList();

			// Get files
			List<string> files = CurrentFileSystem.FindFile( CurrentPath ).ToList();

			// Sort directories and files (default sort is by name)
			directories.Sort( ( a, b ) => string.Compare( System.IO.Path.GetFileName( a ), System.IO.Path.GetFileName( b ) ) );
			files.Sort( ( a, b ) => string.Compare( System.IO.Path.GetFileName( a ), System.IO.Path.GetFileName( b ) ) );

			OnPreAddFiles?.Invoke();

			// Display directories first
			foreach ( var dir in directories )
			{
				AddDirectoryToView( dir );
			}

			// Then display files
			foreach ( var file in files )
			{
				AddFileToView( file );
			}

			OnPostAddFiles?.Invoke();
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error accessing directory: {ex.Message}" );
		}
	}

	public virtual void AddFileToView( string file, bool isFullPath = false, string nameOverride = "" )
	{
		var fullPath = CurrentPath + "/" + file;
		if ( isFullPath )
		{
			fullPath = file;
		}
		var fileName = System.IO.Path.GetFileName( file );
		if ( !string.IsNullOrEmpty( nameOverride ) )
		{
			fileName = nameOverride;
		}

		// Create the file item data object
		var fileItem = new FileItem
		{
			Name = fileName,
			FullPath = fullPath,
			IsDirectory = false
		};
		FileItems.Add( fileItem );

		// Create subItems for ListView
		var subItems = new List<string>
		{
			fileName,
			GetFileType( fileName, false ),
			GetFileSize( false )
		};

		// Add to ListView
		ListView.AddItem( fileItem, subItems );

		// Configure icon for the newly added item
		var listViewItem = ListView.Items.LastOrDefault();
		if ( listViewItem?.IconPanel != null )
		{
			string extension = System.IO.Path.GetExtension( fileName );
			int iconSize = ViewMode == FileBrowserViewMode.Icons ? 32 : 16;
			listViewItem.IconPanel.SetFileIcon( extension, iconSize );
		}
	}

	public virtual void AddDirectoryToView( string dir, bool isFullPath = false, string nameOverride = "" )
	{
		var fullPath = CurrentPath + "/" + dir;
		if ( isFullPath )
		{
			fullPath = dir;
		}
		var dirName = System.IO.Path.GetFileName( dir );
		if ( !string.IsNullOrEmpty( nameOverride ) )
		{
			dirName = nameOverride;
		}

		// Create the directory item data object
		var dirItem = new FileItem
		{
			Name = dirName,
			FullPath = fullPath,
			IsDirectory = true
		};
		FileItems.Add( dirItem );

		// Create subItems for ListView
		var subItems = new List<string>
		{
			dirName,
			GetFileType( dirName, true ),
			GetFileSize( true )
		};

		// Add to ListView
		ListView.AddItem( dirItem, subItems );

		// Configure icon for the newly added item
		var listViewItem = ListView.Items.LastOrDefault();
		if ( listViewItem?.IconPanel != null )
		{
			int iconSize = ViewMode == FileBrowserViewMode.Icons ? 32 : 16;
			listViewItem.IconPanel.SetFolderIcon( "folder", iconSize );
		}
	}

	private string GetFileType( string name, bool isDirectory )
	{
		if ( isDirectory )
			return "File Folder";

		string extension = System.IO.Path.GetExtension( name ).ToLower();
		return !string.IsNullOrEmpty( extension ) ? extension.Substring( 1 ).ToUpper() + " File" : "File";
	}

	private string GetFileSize( bool isDirectory )
	{
		return isDirectory ? "" : "";  // Could implement file size calculation if needed
	}

	public void SelectItem( FileItem item )
	{
		// Find the ListView item that corresponds to this FileItem
		foreach ( var listViewItem in ListView.Items )
		{
			if ( listViewItem.Data == item )
			{
				ListView.SelectItem( listViewItem );
				break;
			}
		}
	}

	public void UnselectAll()
	{
		// ListView doesn't have explicit UnselectAll, so we'll just clear our tracking
		foreach ( var item in ListView.Items )
		{
			item.SetSelected( false );
		}
	}

	protected override void OnRightClick( MousePanelEvent e )
	{
		base.OnRightClick( e );
		// Right-click handling is implemented in derived classes
	}

	/// <summary>
	/// Data class representing a file or directory item
	/// </summary>
	public class FileItem
	{
		public string Name { get; set; }
		public string FullPath { get; set; }
		public bool IsDirectory { get; set; }
	}
}
