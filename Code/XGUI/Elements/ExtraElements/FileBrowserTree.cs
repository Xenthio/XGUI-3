using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XGUI;

/// <summary>
/// A tree view that shows the directory structure for navigation, similar to Windows 95 Explorer.
/// </summary>
public class FileBrowserTree : Panel
{
	public BaseFileSystem CurrentFileSystem;
	public string RootPath = "/";
	public event Action<string> OnDirectorySelected;

	private TreeNode RootNode;
	private string SelectedPath;

	public FileBrowserTree()
	{
		AddClass( "file-browser-tree" );
	}

	public void Initialize( string rootPath )
	{
		RootPath = rootPath;
		SelectedPath = rootPath;
		Refresh();
	}

	public void Refresh()
	{
		DeleteChildren();

		if ( string.IsNullOrEmpty( RootPath ) || CurrentFileSystem == null )
			return;

		if ( !CurrentFileSystem.DirectoryExists( RootPath ) )
			return;

		try
		{
			string rootName = System.IO.Path.GetFileName( RootPath );
			// Use "Root" if at the file system's root
			if ( string.IsNullOrEmpty( rootName ) || rootName == "/" )
				rootName = "Root";

			RootNode = new TreeNode( this, rootName, RootPath, true );
			AddChild( RootNode );
			RootNode.Expand(); // Auto-expand root
			RootNode.SetSelected( true ); // Select root by default
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error creating file tree: {ex.Message}" );
		}
	}

	/// <summary>
	/// Select a directory in the tree and expand the path to it.
	/// </summary>
	public void SelectDirectory( string path )
	{
		if ( RootNode == null || string.IsNullOrEmpty( path ) )
			return;

		// Don't do anything if it's already selected
		if ( SelectedPath == path )
			return;

		SelectedPath = path;

		// If path is root, just select the root node
		if ( path == RootPath )
		{
			UnselectAll();
			RootNode.SetSelected( true );
			return;
		}

		// Expand the path to the directory
		ExpandPathTo( path );
	}

	/// <summary>
	/// Expand the tree path to the specified directory.
	/// </summary>
	private void ExpandPathTo( string targetPath )
	{
		// First unselect everything
		UnselectAll();

		// Start with the root
		string currentPath = RootPath;
		TreeNode currentNode = RootNode;

		// Get the relative path parts
		string relativePath = GetRelativePath( RootPath, targetPath );
		if ( string.IsNullOrEmpty( relativePath ) )
		{
			currentNode.SetSelected( true );
			return;
		}

		string[] pathParts = relativePath.Split( '/', '\\' ).Where( p => !string.IsNullOrEmpty( p ) ).ToArray();

		// Expand each part of the path
		foreach ( string part in pathParts )
		{
			// Expand current node to see its children
			currentNode.Expand();

			// Move to the next directory in the path
			currentPath = System.IO.Path.Combine( currentPath, part ).Replace( "\\", "/" );

			// Find the child node for this path part
			TreeNode nextNode = null;
			foreach ( var child in currentNode.ChildNodes )
			{
				if ( child.FullPath.Equals( currentPath, StringComparison.OrdinalIgnoreCase ) )
				{
					nextNode = child;
					break;
				}
			}

			// If we couldn't find a matching child, break out
			if ( nextNode == null )
				break;

			currentNode = nextNode;
		}

		// Select the final node if we reached it
		if ( currentNode.FullPath.Equals( targetPath, StringComparison.OrdinalIgnoreCase ) )
		{
			currentNode.SetSelected( true );
		}
	}

	private string GetRelativePath( string basePath, string fullPath )
	{
		// Normalize paths with forward slashes
		basePath = basePath.Replace( "\\", "/" ).TrimEnd( '/' ) + "/";
		fullPath = fullPath.Replace( "\\", "/" );

		if ( fullPath.StartsWith( basePath, StringComparison.OrdinalIgnoreCase ) )
		{
			return fullPath.Substring( basePath.Length );
		}

		return fullPath;
	}

	private void UnselectAll()
	{
		// Recursively unselect all nodes starting from root
		if ( RootNode != null )
			UnselectNode( RootNode );
	}

	private void UnselectNode( TreeNode node )
	{
		node.SetSelected( false );
		foreach ( var child in node.ChildNodes )
		{
			UnselectNode( child );
		}
	}

	private class TreeNode : Panel
	{
		public FileBrowserTree Parent;
		public string NodeName;
		public string FullPath;
		public bool IsDirectory;
		public bool IsExpanded;
		public List<TreeNode> ChildNodes = new();
		private Panel ExpandIcon;
		private Panel ContentPanel;
		private Panel ChildrenPanel;
		private bool IsSelected;

		public TreeNode( FileBrowserTree parent, string name, string path, bool isDirectory )
		{
			Parent = parent;
			NodeName = name;
			FullPath = path;
			IsDirectory = isDirectory;
			IsExpanded = false;
			IsSelected = false;

			AddClass( "tree-node" );

			// Create the row with icon and label
			ContentPanel = new Panel();
			ContentPanel.AddClass( "tree-node-content" );

			if ( isDirectory )
			{
				// Create expand/collapse icon
				ExpandIcon = new Panel();
				ExpandIcon.AddClass( "expand-icon" );
				ExpandIcon.AddEventListener( "onclick", ( e ) =>
				{
					e.StopPropagation();
					ToggleExpand();
				} );
				ContentPanel.AddChild( ExpandIcon );

				// Create folder icon
				var folderIcon = new Panel();
				folderIcon.AddClass( "folder-icon" );
				ContentPanel.AddChild( folderIcon );
			}
			else
			{
				// Create file icon
				var fileIcon = new Panel();
				fileIcon.AddClass( "file-icon" );
				ContentPanel.AddChild( fileIcon );
			}

			// Create label
			var label = new Label();
			label.Text = name;
			label.AddClass( "node-label" );
			ContentPanel.AddChild( label );

			AddChild( ContentPanel );

			// Create container for children
			ChildrenPanel = new Panel();
			ChildrenPanel.AddClass( "tree-node-children" );
			ChildrenPanel.Style.Display = DisplayMode.None;
			AddChild( ChildrenPanel );

			// Handle click event
			ContentPanel.AddEventListener( "onclick", () =>
			{
				if ( IsDirectory )
				{
					// Unselect all nodes first
					Parent.UnselectAll();

					// Select this node
					SetSelected( true );

					// Notify parent of directory selection
					Parent.OnDirectorySelected?.Invoke( FullPath );
					Parent.SelectedPath = FullPath;
				}
			} );
		}

		public void ToggleExpand()
		{
			if ( IsExpanded )
				Collapse();
			else
				Expand();
		}

		public void Expand()
		{
			if ( !IsDirectory || IsExpanded )
				return;

			IsExpanded = true;
			ExpandIcon.AddClass( "expanded" );
			ChildrenPanel.Style.Display = DisplayMode.Flex;

			// Only load children once
			if ( ChildNodes.Count == 0 )
				LoadChildren();
		}

		public void Collapse()
		{
			if ( !IsDirectory || !IsExpanded )
				return;

			IsExpanded = false;
			ExpandIcon.RemoveClass( "expanded" );
			ChildrenPanel.Style.Display = DisplayMode.None;
		}

		public void SetSelected( bool selected )
		{
			IsSelected = selected;
			ContentPanel.SetClass( "selected", selected );
		}

		private void LoadChildren()
		{
			try
			{
				// Get directories
				IEnumerable<string> directories = Parent.CurrentFileSystem.FindDirectory( FullPath );

				// Create a node for each subdirectory
				foreach ( var dir in directories )
				{
					var dirName = System.IO.Path.GetFileName( dir );
					var node = new TreeNode( Parent, dirName, dir, true );
					ChildNodes.Add( node );
					ChildrenPanel.AddChild( node );

					// Check if this directory has subdirectories (to show +/- icons)
					bool hasSubdirs = HasSubdirectories( dir );
					if ( !hasSubdirs )
					{
						node.ExpandIcon.Style.Opacity = 0; // Hide expand icon if no subdirs
					}
				}
			}
			catch ( Exception ex )
			{
				Log.Error( $"Error loading directory children: {ex.Message}" );
			}
		}

		private bool HasSubdirectories( string path )
		{
			try
			{
				var directories = Parent.CurrentFileSystem.FindDirectory( path );
				return directories.Any();
			}
			catch
			{
				return false;
			}
		}
	}
}
