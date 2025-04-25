using Editor;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using XGUI.XGUIEditor;

class XGUIHierarchyWidget : Widget
{
	public XGUIDesigner OwnerDesigner { get; private set; }
	private TreeView _hierarchyTree;

	public XGUIHierarchyWidget( Widget parent, XGUIDesigner ownerDesigner ) : base( parent )
	{
		OwnerDesigner = ownerDesigner;
		this.Layout = Layout.Column();
		BuildUI();
	}

	private void BuildUI()
	{
		_hierarchyTree = new TreeView( this );
		_hierarchyTree.ItemSelected += OnHierarchyNodeSelected;
		this.Layout.Add( _hierarchyTree );
	}

	public void UpdateHierarchy( List<MarkupNode> rootNodes )
	{
		_hierarchyTree.Clear();
		// Rebuild the hierarchy tree
		foreach ( var rootNode in rootNodes )
		{
			BuildTreeForMarkupNodeRecursive( rootNode, null, _hierarchyTree );
		}
	}

	private void BuildTreeForMarkupNodeRecursive( MarkupNode node, TreeNode parentTreeNode, TreeView treeView )
	{

		string displayName = $"{node.TagName}";
		if ( node.Attributes.TryGetValue( "class", out var cls ) && !string.IsNullOrWhiteSpace( cls ) ) displayName += $" .{cls.Split( ' ' )[0]}";
		if ( node.Attributes.TryGetValue( "id", out var id ) && !string.IsNullOrWhiteSpace( id ) ) displayName += $" #{id}";

		// Create a tree node for this markup node
		var treeNode = new TreeNode( node ) { Name = displayName, /*ToolTip = $"Pos: {node.SourceStart}-{node.SourceEnd}",*/ Value = node };

		// If there's a parent node, add as child; otherwise add to the root
		if ( parentTreeNode != null )
		{
			parentTreeNode.AddItem( treeNode );
		}
		else
		{
			treeView.AddItem( treeNode );
		}

		// Recurse for children
		if ( node.Children != null )
		{
			foreach ( var child in node.Children )
			{
				BuildTreeForMarkupNodeRecursive( child, treeNode, treeView );
			}
		}
	}

	private void OnHierarchyNodeSelected( object item )
	{
		// Update the hierarchy tree if needed
		_hierarchyTree.UpdateIfDirty();

		if ( item is MarkupNode node && node.Type == NodeType.Element )
		{
			// Check if this is the window-content node (root node in hierarchy)
			bool isWindowContentNode = node.TagName.Equals( "div", StringComparison.OrdinalIgnoreCase ) &&
									  node.Attributes.TryGetValue( "class", out var cls ) &&
									  cls.Contains( "window-content" );

			if ( isWindowContentNode )
			{
				// Select the parent root node (which contains window properties)
				// This is the <root> node that wraps everything
				var rootNode = OwnerDesigner.GetOrCreateWindowNode();

				// Use the window itself as the panel
				OwnerDesigner.SelectAndInspect( rootNode, OwnerDesigner.Window );
				Log.Info( "Selected window root node" );
				return;
			}

			// Normal element selection  
			Panel correspondingPanel = OwnerDesigner.LookupPanelByNode( node );

			OwnerDesigner.SelectAndInspect( node, correspondingPanel );
		}
		// for code blocks, select the text in the code view
		else if ( item is MarkupNode codeBlock && codeBlock.Type == NodeType.RazorBlock )
		{
			// Select the text in the code view
			OwnerDesigner.SelectAndInspect( codeBlock, null );
		}
		else if ( item is MarkupNode textNode && textNode.Type == NodeType.Text )
		{
			// Do nothing for text nodes, or handle as needed
			Log.Info( "Text node selected" );
			// OwnerDesigner.SelectAndInspect( textNode, null ); // Optional: Handle text node selection if needed
		}
		else
		{
			OwnerDesigner.SelectAndInspect( null, null ); // Clear selection if text node or something else selected
		}
	}
}
