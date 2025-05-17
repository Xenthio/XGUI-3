using Editor;
using Sandbox;
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
		var treeNode = new MarkupTreeNode() { Name = displayName, /*ToolTip = $"Pos: {node.SourceStart}-{node.SourceEnd}",*/ Value = node };

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

// we can use custom draw
class MarkupTreeNode : TreeNode<MarkupNode>
{
	public override void OnPaint( VirtualWidget item )
	{
		PaintSelection( item );


		var hoveredInGame = false;//Value.PseudoClass.HasFlag( Sandbox.UI.PseudoClass.Hover );
		var a = 1.0f;//Value.IsVisible ? 1.0f : 0.5f;
		var r = item.Rect;

		void Write( Color color, string text, ref Rect r )
		{
			Paint.SetPen( color.WithAlphaMultiplied( a ) );
			var size = Paint.DrawText( r, text, TextFlag.LeftCenter );
			r.Left += size.Width;
		}

		{
			//	Paint.SetPen( Theme.Yellow.WithAlpha( alpha ) );
			//	Paint.DrawIcon( r, info.Icon ?? "window", 18, TextFlag.LeftCenter );
			//r.Left += Theme.RowHeight;
		}

		var brackets = Theme.Yellow.WithAlpha( 0.7f );
		var element = Color.White.WithAlpha( 0.9f );
		var keyword = Color.White.WithAlpha( 0.7f );
		var code = Theme.Pink.WithAlpha( 0.7f );

		if ( hoveredInGame )
		{
			element = Theme.Green.WithAlpha( 0.9f );
			keyword = Theme.Green.WithAlpha( 0.6f );
		}

		Paint.SetDefaultFont();

		if ( Value.Parent == null )
		{
			Paint.SetDefaultFont( 8, 500 );
			Write( element, "Window Content", ref r );
			Paint.SetDefaultFont();
		}
		else if ( Value.Type == NodeType.Element )
		{
			{
				Write( brackets, $"<", ref r );
				Paint.SetDefaultFont( 8, 500 );
				Write( element, $"{Value.TagName}", ref r );
				Paint.SetDefaultFont();
			}

			if ( Value.Attributes.ContainsKey( "id" ) )
			{
				Write( keyword, $" id=\"", ref r );
				Paint.SetDefaultFont( 8, 500 );
				Write( Theme.Blue, Value?.Attributes["id"], ref r );
				Paint.SetDefaultFont();
				Write( keyword, $"\"", ref r );
			}

			if ( Value.Attributes.ContainsKey( "class" ) )
			{
				Write( keyword, $" class=\"", ref r );
				Write( Theme.Blue, Value?.Attributes["class"], ref r );
				Write( keyword, $"\"", ref r );
			}

			foreach ( var attribute in Value.Attributes )
			{
				if ( attribute.Key == "id" || attribute.Key == "class" )
					continue;
				Write( keyword, $" {attribute.Key}=\"", ref r );
				Write( Theme.Blue, attribute.Value, ref r );
				Write( keyword, $"\"", ref r );
			}

			Write( brackets, $">", ref r );
		}
		else if ( Value.Type == NodeType.Text )
		{
			Write( brackets, $"\"{Value.TextContent}\"", ref r );
		}
		else if ( Value.Type == NodeType.RazorBlock )
		{
			// only show the first 32 characters of the Razor block
			var trimmedCode = Value.TextContent.Trim();
			Paint.SetDefaultFont( 8, 500 );
			Write( keyword, "Code Block ", ref r );
			Paint.SetDefaultFont();
			if ( trimmedCode.Length > 64 )
				trimmedCode = trimmedCode.Substring( 0, 64 ) + "...";
			Write( code, $"{trimmedCode}", ref r );
		}
		else
		{
			Write( element, Value.ToString(), ref r );
		}



	}
}
