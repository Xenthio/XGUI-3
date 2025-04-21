using Editor;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace XGUI.XGUIEditor
{
	public static class XGUIMenu
	{
		[Menu( "Editor", "XGUI/Designer" )]
		public static void OpenMyMenu()
		{
			var b = new XGUIDesigner();
		}
	}
	public class XGUIDesigner : DockWindow
	{
		// UI Elements
		public XGUIOverlayWidget OverlayWidget; // do overlays here.
		private XGUIView _view;
		private Widget _heirarchy;
		private PanelInspector _inspector;
		private Widget _componentPalette;
		private Widget _codeView;
		private TextEdit _codeTextEditor;

		// State
		private string _currentFilePath;
		private bool _isDirty;
		private bool _isUpdatingUIFromCode = false; // Flag to prevent update loops
		private bool _isUpdatingCodeFromUI = false;
		private Menu _recentFilesMenu;
		private readonly List<string> _recentFiles = new();

		// Razor Content Cache
		private string _fullRazorContentCache = ""; // Cache of the full content for modification

		// Parsed Document State
		private List<MarkupNode> _rootMarkupNodes = new();
		private Dictionary<Panel, MarkupNode> _panelToMarkupNodeMap = new();
		private Dictionary<MarkupNode, Panel> _markupNodeToPanelMap = new();

		// Regex for extracting <root>...</root>
		private static readonly Regex _rootContentRegex = new( @"(<root[^>]*>)([\s\S]*?)(</root>)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline );

		public XGUIDesigner()
		{
			DeleteOnClose = true;
			Title = "XGUI Razor Designer";
			Size = new Vector2( 1280, 720 );
			CreateUI();
			Show();
			New();
		}

		public void CreateUI()
		{
			BuildMenuBar();

			// --- Views ---
			_view = new XGUIView { OnElementSelected = OnDesignViewElementSelected, OwnerDesigner = this };
			_view.SetSizeMode( SizeMode.Flexible, SizeMode.Flexible );

			_codeView = new Widget( null );
			_codeView.SetSizeMode( SizeMode.Expand, SizeMode.Expand );
			CreateCodeViewInternal(); // Creates _codeTextEditor

			// --- Panels ---
			_heirarchy = new Widget( null ) { Layout = Layout.Column() };
			_inspector = new PanelInspector() { OnPropertyChanged = OnInspectorPropertyChanged }; // Hook up delegate
			_componentPalette = new Widget( null ) { Layout = Layout.Column() };
			CreateComponentPaletteInternal();


			// --- Docking ---
			_view.WindowTitle = "Design View";
			_view.SetWindowIcon( "visibility" );
			DockManager.AddDock( null, _view );

			// Create and add the overlay widget
			OverlayWidget = new XGUIOverlayWidget( _view.Parent );
			OverlayWidget.ConnectToView( _view );

			_heirarchy.WindowTitle = "Hierarchy";
			_heirarchy.SetWindowIcon( "view_list" );
			DockManager.AddDock( null, _heirarchy, dockArea: DockArea.Left, split: 0.10f );

			_inspector.WindowTitle = "Inspector";
			_inspector.SetWindowIcon( "info" );
			DockManager.AddDock( null, _inspector, dockArea: DockArea.Right, split: 0.10f );

			_componentPalette.WindowTitle = "Component Palette";
			_componentPalette.SetWindowIcon( "view_module" );
			_codeView.WindowTitle = "Code View";
			_codeView.SetWindowIcon( "code" );

			DockManager.AddDock( null, _componentPalette, DockArea.TopOuter, split: 0.05f );
			DockManager.AddDock( null, _codeView, dockArea: DockArea.Bottom, split: 0.30f );

			_view.Setup();
		}

		public override void OnDestroyed()
		{

			base.OnDestroyed();
			OverlayWidget?.Destroy();
		}

		//---------------------------------------------------------------------
		// UI Construction & Updates (Internal Helpers)
		//---------------------------------------------------------------------

		private void CreateCodeViewInternal()
		{
			_codeView.Layout = Layout.Column();
			_codeTextEditor = _codeView.Layout.Add( new TextEdit( null ), 1 );
			_codeTextEditor.TextChanged = OnCodeTextChanged;
			// Configure TextEdit (e.g., font, line numbers) if needed
		}

		private void CreateComponentPaletteInternal()
		{
			var container = _componentPalette.Layout.Add( new Widget( null ) );
			container.SetSizeMode( SizeMode.Expand, SizeMode.Expand );
			container.Layout = Layout.Row();
			container.Layout.Spacing = 8;
			var layoutsCategory = CreateComponentCategory( container.Layout, "Layouts:" );
			var controlsCategory = CreateComponentCategory( container.Layout, "Controls:" );
			var containersCategory = CreateComponentCategory( container.Layout, "Containers:" );
			AddComponentButton( layoutsCategory, "Div", "div" );
			AddComponentButton( layoutsCategory, "Row", "div", "class=\"self-layout self-layout-row\"" );
			AddComponentButton( layoutsCategory, "Column", "div", "class=\"self-layout self-layout-column\"" );
			AddComponentButton( controlsCategory, "Button", "button" );
			AddComponentButton( controlsCategory, "Checkbox", "check" );
			AddComponentButton( controlsCategory, "Label", "label" );
			AddComponentButton( controlsCategory, "Text Entry", "textentry" );
			AddComponentButton( controlsCategory, "Slider", "sliderscale", "min=\"0\" max=\"100\" step=\"1\"" ); // Ensure quotes for parser
			AddComponentButton( containersCategory, "Group Box", "groupbox", "title=\"Group\"" );
			// AddComponentButton(containersCategory, "Tab Control", "tabcontrol");
			// AddComponentButton(containersCategory, "Combo Box", "combobox");
		}

		// Helper for Palette
		private Layout CreateComponentCategory( Layout parentLayout, string categoryName )
		{
			var categoryWidget = parentLayout.Add( new Widget( null ) );
			//border qt
			categoryWidget.ContentMargins = new Margin( 8, 8, 8, 8 );
			categoryWidget.SetStyles( "border: 1px solid #555; border-radius: 4px;" );
			categoryWidget.Layout = Layout.Row();
			categoryWidget.Layout.Spacing = 4;
			var label = new Editor.Label( categoryName );
			label.SetStyles( "font-size: 11px; font-weight: bold; color: #fff; border: none;" );
			categoryWidget.Layout.Add( label );
			return categoryWidget.Layout;
		}

		public MarkupNode LookupNodeByPanel( Panel panel )
		{
			if ( panel == null ) return null;
			if ( _panelToMarkupNodeMap.TryGetValue( panel, out var node ) ) return node;
			return null;
		}
		public Panel LookupPanelByNode( MarkupNode node )
		{
			if ( node == null ) return null;
			if ( _markupNodeToPanelMap.TryGetValue( node, out var panel ) ) return panel;
			return null;
		}

		private void OnDesignViewElementSelected( Panel selectedPanel )
		{
			if ( selectedPanel != null && _panelToMarkupNodeMap.TryGetValue( selectedPanel, out var node ) )
				SelectAndInspect( node, selectedPanel );
			else
				SelectAndInspect( null, null );
		}

		private void OnInspectorPropertyChanged( MarkupNode node, string propertyOrAttributeName, object newValue )
		{
			if ( node == null || node.Type != NodeType.Element ) return;

			// Update the MarkupNode tree directly
			if ( propertyOrAttributeName == "innertext" )
			{
				var textNode = node.Children.FirstOrDefault( c => c.Type == NodeType.Text );
				if ( textNode != null ) textNode.TextContent = newValue?.ToString() ?? "";
				else node.Children.Insert( 0, new MarkupNode { Type = NodeType.Text, TextContent = newValue?.ToString() ?? "" } );
			}
			else
			{
				if ( string.IsNullOrWhiteSpace( newValue?.ToString() ) )
					node.Attributes.Remove( propertyOrAttributeName );
				else
					node.Attributes[propertyOrAttributeName] = newValue.ToString();
			}

			// Serialize tree back to markup and update code view
			UpdateCodeFromTree();
		}

		private void OnCodeTextChanged( string newContent )
		{
			ParseAndUpdateUI( newContent );
		}

		private void ParseAndUpdateUI( string fullRazorContent, bool rebuildMarkupTree = true )
		{
			_panelToMarkupNodeMap.Clear();
			_markupNodeToPanelMap.Clear();
			if ( rebuildMarkupTree ) _rootMarkupNodes.Clear();
			if ( _view?.WindowContent == null )
			{
				_view?.CreateBlankWindow(); // Ensure base window exists
			}
			_view?.WindowContent?.DeleteChildren( true );

			// Extract <root>...</root>
			var rootContentMatch = _rootContentRegex.Match( fullRazorContent );
			string htmlContent = rootContentMatch.Success ? rootContentMatch.Groups[2].Value : "";

			if ( rebuildMarkupTree ) _rootMarkupNodes = SimpleMarkupParser.Parse( htmlContent );

			// Find the <div class="window-content"> node
			MarkupNode windowContentNode = null;
			foreach ( var node in _rootMarkupNodes )
			{
				if ( node.Type == NodeType.Element && node.TagName.Equals( "div", StringComparison.OrdinalIgnoreCase ) &&
					node.Attributes.TryGetValue( "class", out var cls ) && cls.Contains( "window-content" ) )
				{
					windowContentNode = node;
					_panelToMarkupNodeMap[_view.WindowContent] = windowContentNode;
					_markupNodeToPanelMap[windowContentNode] = _view.WindowContent;
					break;
				}
				// Optionally: search recursively if not always top-level
			}

			// Clear and rebuild the design surface
			_view?.WindowContent?.DeleteChildren( true );

			if ( windowContentNode != null && _view?.WindowContent != null )
			{
				foreach ( var child in windowContentNode.Children )
					CreatePanelsRecursive( child, _view.WindowContent );
			}

			UpdateHierarchyPanelInternal();
		}

		private void CreatePanelsRecursive( MarkupNode node, Panel parentPanel )
		{
			if ( node.Type == NodeType.Element )
			{
				Panel newElement = CreateElementFromTag( node.TagName );
				if ( newElement != null )
				{
					parentPanel.AddChild( newElement );
					ApplyAttributesToElement( newElement, node.Attributes );
					_panelToMarkupNodeMap[newElement] = node;
					_markupNodeToPanelMap[node] = newElement;

					foreach ( var childNode in node.Children )
						CreatePanelsRecursive( childNode, newElement );

					ApplyTextContent( newElement, node );
				}
			}
		}
		private Panel GetPanelAt( Vector2 screenPosition )
		{
			// Recursively search all panels in the design view for one containing the point
			Panel found = null;
			void Search( Panel panel )
			{
				if ( panel == null ) return;
				if ( panel.Box.Rect.IsInside( screenPosition ) )
					found = panel;
				foreach ( var child in panel.Children.OfType<Panel>() )
					Search( child );
			}
			Search( _view.WindowContent );
			return found;
		}
		public void ForceUpdate( bool rebuildMarkupTree = true )
		{
			var backup = LookupNodeByPanel( _view.SelectedPanel );
			UpdateCodeFromTree();
			ParseAndUpdateUI( _codeTextEditor.PlainText, rebuildMarkupTree );


			_view.SelectedPanel = LookupPanelByNode( backup );
			if ( _view.SelectedPanel == null )
			{
				Log.Warning( "_view.SelectedPanel is null after UI rebuild!" );
			}
		}
		private void UpdateCodeFromTree()
		{
			// Serialize the tree back to markup and update the code editor
			var rootMarkup = SimpleMarkupParser.Serialize( _rootMarkupNodes );
			// Re-wrap in <root>...</root> and preserve directives/code blocks as needed
			string newCode = $@"@using Sandbox;
@using Sandbox.UI;
@using XGUI;
@attribute [StyleSheet( ""/XGUI/DefaultStyles/OliveGreen.scss"" )]
@inherits Window

<root>
{rootMarkup}
</root>

@code {{
    // Add your code here
}}";
			SetCodeEditorText( newCode );
		}

		// Helper for Palette
		private void AddComponentButton( Layout layout, string displayName, string tagName, string attributes = "" )
		{
			var button = layout.Add( new Editor.Button( displayName ) );
			button.Clicked = () => AddComponentToSource( tagName, attributes );
			button.IsDraggable = true;

		}

		/// <summary>
		/// Adds a component by modifying the source code.
		/// </summary>
		private void AddComponentToSource( string tagName, string attributes = "" )
		{
			Log.Info( $"Adding component <{tagName}> to window-content" );
			if ( _isUpdatingUIFromCode ) return;

			// Find the window-content node in the markup tree
			MarkupNode windowContentNode = null;
			foreach ( var node in _rootMarkupNodes )
			{
				if ( node.Type == NodeType.Element && node.TagName.Equals( "div", StringComparison.OrdinalIgnoreCase ) &&
					node.Attributes.TryGetValue( "class", out var cls ) && cls.Contains( "window-content" ) )
				{
					windowContentNode = node;
					break;
				}
				// Optionally: search recursively if not always top-level
			}

			if ( windowContentNode == null )
			{
				Log.Warning( "Cannot add component: <div class=\"window-content\"> not found." );
				return;
			}

			// Create the new MarkupNode for the component
			var newNode = new MarkupNode
			{
				Type = NodeType.Element,
				TagName = tagName,
				Attributes = SimpleMarkupParser.ParseAttributes( attributes ),
				Children = new List<MarkupNode>()
			};
			// Default text for button
			if ( tagName == "button" )
			{
				newNode.Children.Add( new MarkupNode { Type = NodeType.Text, TextContent = "Button" } );
			}

			// Add to window-content node
			windowContentNode.Children.Add( newNode );

			// Serialize tree back to markup and update code view/UI
			UpdateCodeFromTree();
			ParseAndUpdateUI( _codeTextEditor.PlainText );
			_isDirty = true;
		}


		TreeView HierarchyTree;
		/// <summary>
		/// Updates the Hierarchy TreeView based on the _rootMarkupNodes.
		/// </summary>
		private void UpdateHierarchyPanelInternal()
		{
			// Find existing TreeView or create a new one 
			if ( HierarchyTree == null )
			{
				// Clear any old non-TreeView widgets if necessary before adding
				// _heirarchy.Layout.Clear(true); // Use if layout needs full reset
				HierarchyTree = new TreeView( _heirarchy );
				_heirarchy.Layout.Add( HierarchyTree );
			}
			else
			{
				HierarchyTree.Clear(); // Clear existing items efficiently
			}

			HierarchyTree.MultiSelect = false;
			HierarchyTree.ExpandForSelection = true;
			HierarchyTree.ItemSelected = OnHierarchyNodeSelected; // Use specific handler

			// Build tree from the root MarkupNodes
			foreach ( var rootNode in _rootMarkupNodes )
			{
				BuildTreeForMarkupNodeRecursive( rootNode, null, HierarchyTree ); // Pass treeview for root items
			}
			// Don't expand all by default? User can expand.
		}


		/// <summary>
		/// Recursively builds the TreeView structure from MarkupNodes.
		/// </summary>
		private void BuildTreeForMarkupNodeRecursive( MarkupNode node, TreeNode parentTreeNode, TreeView treeView )
		{
			// (Identical to previous implementation)
			if ( node.Type == NodeType.Element )
			{
				string displayName = $"{node.TagName}";
				if ( node.Attributes.TryGetValue( "class", out var cls ) && !string.IsNullOrWhiteSpace( cls ) ) displayName += $" .{cls.Split( ' ' )[0]}";
				if ( node.Attributes.TryGetValue( "id", out var id ) && !string.IsNullOrWhiteSpace( id ) ) displayName += $" #{id}";

				var treeNode = new TreeNode( node ) { Name = displayName, /*ToolTip = $"Pos: {node.SourceStart}-{node.SourceEnd}",*/ Value = node };

				if ( parentTreeNode != null ) parentTreeNode.AddItem( treeNode );
				else treeView.AddItem( treeNode );

				foreach ( var childNode in node.Children )
				{
					BuildTreeForMarkupNodeRecursive( childNode, treeNode, treeView );
				}
			}
			// Optionally add text nodes here if desired
		}


		/// <summary>
		/// Safely sets the text of the code editor without triggering its TextChanged event.
		/// </summary>
		private void SetCodeEditorText( string text )
		{
			if ( _codeTextEditor == null ) return;
			var originalHandler = _codeTextEditor.TextChanged;
			try
			{
				_codeTextEditor.TextChanged = null;
				_codeTextEditor.PlainText = text;
			}
			finally
			{
				_codeTextEditor.TextChanged = originalHandler;
			}
		}

		//---------------------------------------------------------------------
		// Menu Actions & File I/O
		//---------------------------------------------------------------------
		public void BuildMenuBar()
		{
			var file = MenuBar.AddMenu( "File" );
			file.AddOption( "New", "common/new.png", New, "Ctrl+N" ).StatusTip = "New Razor File";
			file.AddOption( "Open", "common/open.png", Open, "Ctrl+O" ).StatusTip = "Open Razor File";
			file.AddOption( "Save", "common/save.png", () => Save(), "Ctrl+S" ).StatusTip = "Save Razor File";
			file.AddOption( "Save As...", "common/save.png", () => Save( true ), "Ctrl+Shift+S" ).StatusTip = "Save Razor File As...";

			file.AddSeparator();

			_recentFilesMenu = file.AddMenu( "Recent Files" );

			file.AddSeparator();

			file.AddOption( "Quit", null, Close, "Ctrl+Q" ).StatusTip = "Quit";

			var edit = MenuBar.AddMenu( "Edit" );
			edit.AddSeparator();
			edit.AddOption( "Cut", "common/cut.png", CutSelection, "Ctrl+X" );
			edit.AddOption( "Copy", "common/copy.png", CopySelection, "Ctrl+C" );
			edit.AddOption( "Paste", "common/paste.png", PasteSelection, "Ctrl+V" );
			edit.AddOption( "Select All", "select_all", SelectAll, "Ctrl+A" );

			var view = MenuBar.AddMenu( "View" );
			view.AddOption( "Design View", null, ShowDesignView, "F7" );
			view.AddOption( "Code View", null, ShowCodeView, "F8" );
			view.AddOption( "Split View", null, ShowSplitView, "F9" );
		}
		void New()
		{
			string template = @"@using Sandbox;
@using Sandbox.UI;
@using XGUI;
@inherits Panel

<root>
    <div class=""window-content"">
        <!-- Design your UI here -->
    </div>
</root>

@code {
    // Add your code here
}";
			SetCodeEditorText( template ); // Set initial text
			_fullRazorContentCache = template; // Prime the cache
			ParseAndUpdateUI( template ); // Parse initial state
			_currentFilePath = null;
			_isDirty = false;
			Title = "XGUI Razor Designer - Untitled";
		}
		void Open()
		{
			var fd = new FileDialog( null );
			fd.Title = "Open Razor File";
			fd.SetNameFilter( "Razor Files (*.razor)" );
			fd.SetFindFile();
			fd.SetModeOpen();

			if ( !fd.Execute() ) return;

			OpenFile( fd.SelectedFile );
		}
		void OpenFile( string path )
		{
			if ( !File.Exists( path ) ) return;
			try
			{
				string content = File.ReadAllText( path );
				SetCodeEditorText( content ); // Update editor
				_fullRazorContentCache = content; // Update cache
				ParseAndUpdateUI( content ); // Parse new file
				_currentFilePath = path;
				_isDirty = false;
				Title = $"XGUI Razor Designer - {Path.GetFileName( path )}";
				AddRecentFile( path );
			}
			catch ( System.Exception ex ) { Log.Error( $"Error opening file: {ex.Message}" ); }
		}
		void AddRecentFile( string path )
		{
			_recentFiles.Remove( path ); // Remove if exists to move to top
			_recentFiles.Insert( 0, path );
			if ( _recentFiles.Count > 10 ) _recentFiles.RemoveAt( 10 );
			UpdateRecentFilesMenu();
		}
		void UpdateRecentFilesMenu()
		{
			// Clear menu items properly since Menu.Items doesn't exist
			_recentFilesMenu.Clear();

			foreach ( var path in _recentFiles )
			{
				string fileName = Path.GetFileName( path );
				_recentFilesMenu.AddOption( fileName, null, () => OpenFile( path ) );
			}
		}

		void Save( bool saveas = false )
		{
			if ( saveas || string.IsNullOrEmpty( _currentFilePath ) )
			{
				// Use FileDialog instead of SaveFileDialog
				var fd = new FileDialog( null );
				fd.Title = "Save Razor File";
				fd.SetNameFilter( "Razor Files (*.razor)" );
				fd.SetFindFile();
				fd.SetModeSave();
				fd.DefaultSuffix = ".razor";

				if ( !fd.Execute() ) return;

				SaveFile( fd.SelectedFile );
			}
			else
			{
				SaveFile( _currentFilePath );
			}
		}

		void SaveFile( string path )
		{
			// Find TextEdit directly instead of using ChildrenOfType
			if ( _codeTextEditor == null ) return;

			try
			{
				File.WriteAllText( path, _codeTextEditor.PlainText );
				_currentFilePath = path;
				_isDirty = false;

				Title = $"XGUI Razor Designer - {Path.GetFileName( path )}";

				// Add to recent files
				if ( !_recentFiles.Contains( path ) )
				{
					_recentFiles.Insert( 0, path );
					if ( _recentFiles.Count > 10 )
						_recentFiles.RemoveAt( 10 );

					UpdateRecentFilesMenu();
				}

				Log.Info( $"File saved: {path}" );
			}
			catch ( System.Exception ex )
			{
				Log.Error( $"Error saving file: {ex.Message}" );
			}
		}

		/// <summary>
		/// Central method to update inspector and potentially highlight selection.
		/// </summary>
		private void SelectAndInspect( MarkupNode node, Panel panel )
		{
			_inspector.SetTarget( panel, node );
			_view.SelectedPanel = panel; // Update selected panel in the view
										 // TODO: Add visual highlighting in TreeView and DesignView if needed
										 // FindTreeNodeAndSelect(node);
										 // _view?.HighlightPanel(panel);
		}

		/// <summary>
		/// Called when an item is selected in the Hierarchy TreeView.
		/// </summary>
		private void OnHierarchyNodeSelected( object item )
		{
			// *** LOG 1: Check if the event fires at all ***
			Log.Info( $"OnHierarchyNodeSelected Fired! Received item of type: {item?.GetType()?.FullName ?? "null"}" );
			HierarchyTree.UpdateIfDirty();

			if ( item is MarkupNode node && node.Type == NodeType.Element )
			{
				// *** LOG 2: Check if item is a valid MarkupNode Element ***
				Log.Info( $"  Item is MarkupNode Element: <{node.TagName}>" );

				_markupNodeToPanelMap.TryGetValue( node, out Panel correspondingPanel );
				SelectAndInspect( node, correspondingPanel );
			}
			else
			{
				// *** LOG 3: Check if item is something else (or null) ***
				Log.Info( $"  Item is NOT a MarkupNode Element (or is null). Clearing Inspector." );
				SelectAndInspect( null, null ); // Clear selection if text node or something else selected
			}
		}

		//---------------------------------------------------------------------
		// MarkupNode to Panel Creation Helpers (Refined from previous)
		//---------------------------------------------------------------------

		// Base element creation (no attributes/content applied here)
		private Panel CreateElementFromTag( string tagName )
		{/*
			switch ( tagName.ToLowerInvariant() )
			{
				case "div": return new Panel();
				case "button": return new Sandbox.UI.Button();
				case "label": return new Sandbox.UI.Label();
				case "check": return new XGUI.CheckBox();
				case "textentry": return new Sandbox.UI.TextEntry();
				case "sliderscale": return new XGUI.SliderScale();
				case "groupbox": return new XGUI.GroupBox();
				// Add other cases
				default:
					Log.Warning( $"Unsupported tag for Panel creation: {tagName}" );
					return new Panel(); // Create generic Panel for unknown tags?
			}*/

			// use typelibrary/reflection to create the element

			// If we get here, try the TypeLibrary approach with robust error handling
			try
			{
				// Log to help diagnose the issue
				Log.Info( $"Creating panel for tag '{tagName}' using TypeLibrary" );

				// Only attempt TypeLibrary approach if it's available
				if ( TypeLibrary == null )
				{
					Log.Warning( "TypeLibrary is null" );
					return new Panel();
				}

				// Try direct name match first (most likely to succeed)
				var types = TypeLibrary.GetTypes()
					.Where( t => t != null &&
						  t.TargetType != null &&
						  t.TargetType.IsSubclassOf( typeof( Panel ) ) &&
						  t.Name.Equals( tagName, StringComparison.OrdinalIgnoreCase ) )
					.ToList();

				if ( types.Count > 0 )
				{
					var type = types[0];
					Log.Info( $"Found panel type by name: {type.Name}" );
					return type.Create<Panel>();
				}

				// Try Library attribute second
				try
				{
					var libraryMatches = TypeLibrary.GetTypesWithAttribute<LibraryAttribute>()
						.Where( a => a.Type != null &&
							   a.Attribute != null &&
							   a.Type.TargetType.IsSubclassOf( typeof( Panel ) ) &&
							   a.Attribute.Name.Equals( tagName, StringComparison.OrdinalIgnoreCase ) )
						.ToList();

					if ( libraryMatches.Count > 0 )
					{
						var type = libraryMatches[0].Type;
						Log.Info( $"Found panel type by LibraryAttribute: {type.Name}" );
						return type.Create<Panel>();
					}
				}
				catch ( Exception ex )
				{
					Log.Warning( $"Error searching by LibraryAttribute: {ex.Message}" );
				}

				// Try Alias attribute last
				try
				{
					var aliasTypes = TypeLibrary.GetTypesWithAttribute<AliasAttribute>()
						.Where( a => a.Type != null &&
							   a.Attribute != null &&
							   a.Type.TargetType != null &&
							   a.Type.TargetType.IsSubclassOf( typeof( Panel ) ) &&
							   a.Attribute.Value != null &&
							   a.Attribute.Value.Any( x => x.Equals( tagName, StringComparison.OrdinalIgnoreCase ) ) )
						.ToList();

					if ( aliasTypes.Count > 0 )
					{
						var type = aliasTypes[0].Type;
						Log.Info( $"Found panel type by AliasAttribute: {type.Name}" );
						return type.Create<Panel>();
					}
				}
				catch ( Exception ex )
				{
					Log.Warning( $"Error searching by AliasAttribute: {ex.Message}" );
				}
			}
			catch ( Exception ex )
			{
				Log.Error( $"Error in TypeLibrary lookup for '{tagName}': {ex.Message}" );
			}

			// If we get here, nothing worked
			Log.Warning( $"Could not find panel type for tag '{tagName}', creating generic Panel" );
			return new Panel();
		}

		// Apply attributes from parsed dictionary
		private void ApplyAttributesToElement( Panel element, Dictionary<string, string> attributes )
		{
			// (Identical to previous implementation - uses switch statement)
			if ( element == null || attributes == null ) return;
			foreach ( var kvp in attributes )
			{
				string name = kvp.Key; // Already lowercased if dictionary uses OrdinalIgnoreCase
				string value = kvp.Value; // Already decoded by parser
				switch ( name )
				{
					case "class": if ( value != null ) foreach ( var cls in value.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) ) element.AddClass( cls ); break;
					case "style": if ( value != null ) ApplyInlineStyles( element, value ); break;
					case "title": if ( element is XGUI.GroupBox gb && value != null ) gb.Title = value; break;
					case "min": if ( element is XGUI.SliderScale sl && float.TryParse( value, CultureInfo.InvariantCulture, out var v ) ) sl.MinValue = v; break;
					case "max": if ( element is XGUI.SliderScale slm && float.TryParse( value, CultureInfo.InvariantCulture, out var v2 ) ) slm.MaxValue = v2; break;
					case "checked": if ( element is XGUI.CheckBox cb ) cb.Checked = true; break; // Valueless implies true
																								 // Add other attributes (id, src, disabled, etc.)
					default: /* Log unknown? Store in Tags? */ break;
				}
			}
		}

		// Apply text content after attributes/children are processed
		private void ApplyTextContent( Panel element, MarkupNode node )
		{
			if ( node == null || node.Type != NodeType.Element ) return;

			// Find first direct child Text node
			var textNode = node.Children.FirstOrDefault( c => c.Type == NodeType.Text && !string.IsNullOrWhiteSpace( c.TextContent ) );
			if ( textNode != null )
			{
				string text = textNode.TextContent; // Already decoded
				if ( element is Sandbox.UI.Button btn ) btn.Text = text;
				else if ( element is Sandbox.UI.Label lbl ) lbl.Text = text;
				else if ( element is XGUI.CheckBox chk ) chk.LabelText = text;
				// Add other elements that take direct text content
			}
		}
		private void ApplyInlineStyles( Panel panel, string styleText )
		{
			// (Implementation identical to previous response - uses ParseLength/ParseColor)
			if ( panel == null || string.IsNullOrWhiteSpace( styleText ) ) return;

			var styles = styleText.Split( ';', StringSplitOptions.RemoveEmptyEntries );
			foreach ( var style in styles )
			{
				var parts = style.Split( ':', 2 ); // Split only on the first colon
				if ( parts.Length != 2 ) continue;

				string property = parts[0].Trim().ToLowerInvariant();
				string value = parts[1].Trim();

				try
				{
					switch ( property )
					{
						case "width": panel.Style.Width = ParseLength( value ); break;
						case "height": panel.Style.Height = ParseLength( value ); break;
						case "min-width": panel.Style.MinWidth = ParseLength( value ); break;
						case "min-height": panel.Style.MinHeight = ParseLength( value ); break;
						case "max-width": panel.Style.MaxWidth = ParseLength( value ); break;
						case "max-height": panel.Style.MaxHeight = ParseLength( value ); break;

						case "margin": /* TODO: Handle shorthand */ panel.Style.Margin = ParseLength( value ); break;
						case "margin-top": panel.Style.MarginTop = ParseLength( value ); break;
						case "margin-right": panel.Style.MarginRight = ParseLength( value ); break;
						case "margin-bottom": panel.Style.MarginBottom = ParseLength( value ); break;
						case "margin-left": panel.Style.MarginLeft = ParseLength( value ); break;

						case "padding": /* TODO: Handle shorthand */ panel.Style.Padding = ParseLength( value ); break;
						case "padding-top": panel.Style.PaddingTop = ParseLength( value ); break;
						case "padding-right": panel.Style.PaddingRight = ParseLength( value ); break;
						case "padding-bottom": panel.Style.PaddingBottom = ParseLength( value ); break;
						case "padding-left": panel.Style.PaddingLeft = ParseLength( value ); break;

						case "background-color": panel.Style.BackgroundColor = ParseColor( value ); break;
						case "color": panel.Style.FontColor = ParseColor( value ); break;
						case "font-size": panel.Style.FontSize = ParseLength( value ); break;
						// Add other CSS properties you want to parse
						// e.g., flex-direction, align-items, justify-content, border, border-radius, etc.
						default: break; // Ignore unknown styles
					}
				}
				catch ( Exception ex )
				{
					Log.Warning( $"Failed to apply style '{property}:{value}'. Error: {ex.Message}" );
				}
			}
			panel.Style.Dirty(); // Ensure styles are applied visually
		}
		// Parses CSS length (px, %) string
		private Length? ParseLength( string value ) { return Length.Parse( value ); }
		// Parses CSS color string (#rgb, #rrggbb, rgb(), rgba())
		private Color ParseColor( string colorValue ) { return Color.Parse( colorValue ).Value; }
		// Optional helpers from ParseColor
		/// <summary>
		/// Helper for ParseColor: Parses a single R, G, or B component (number or percentage).
		/// </summary>
		private float ParseColorComponent( string component )
		{
			component = component.Trim();
			if ( component.EndsWith( '%' ) )
			{
				if ( float.TryParse( component.Substring( 0, component.Length - 1 ), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent ) )
				{
					return Math.Clamp( percent / 100.0f, 0f, 1f );
				}
			}
			else
			{
				if ( int.TryParse( component, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value ) )
				{
					return Math.Clamp( value / 255.0f, 0f, 1f );
				}
				// Handle float values 0-1 directly? CSS Color 4 allows this.
				else if ( float.TryParse( component, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal ) )
				{
					return Math.Clamp( floatVal, 0f, 1f );
				}
			}
			throw new FormatException( $"Invalid color component format: {component}" );
		}

		/// <summary>
		/// Helper for ParseColor: Parses the alpha component (number 0-1 or percentage).
		/// </summary>
		private float ParseAlphaComponent( string alpha )
		{
			alpha = alpha.Trim();
			if ( alpha.EndsWith( '%' ) )
			{
				if ( float.TryParse( alpha.Substring( 0, alpha.Length - 1 ), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent ) )
				{
					return Math.Clamp( percent / 100.0f, 0f, 1f );
				}
			}
			else
			{
				if ( float.TryParse( alpha, NumberStyles.Float, CultureInfo.InvariantCulture, out float value ) )
				{
					return Math.Clamp( value, 0f, 1f );
				}
			}
			throw new FormatException( $"Invalid alpha component format: {alpha}" );
		}

		//---------------------------------------------------------------------
		// Source Code Generation / Manipulation Helpers (Mostly Placeholders)
		//---------------------------------------------------------------------

		// Adjusts SourceStart/SourceEnd after parsing only a fragment
		private void AdjustSourcePositions( IEnumerable<MarkupNode> nodes, int offset ) { /* (Identical to previous) */ }
		/// <summary>
		/// Basic HTML entity decoder.
		/// </summary>
		private string DecodeHtml( string text )
		{
			if ( string.IsNullOrEmpty( text ) ) return "";
			// Manual basic version: Order matters! Ampersand must be last.
			return text.Replace( "&lt;", "<" )
					   .Replace( "&gt;", ">" )
					   .Replace( "&quot;", "\"" )
					   .Replace( "&#39;", "'" ) // Handle apostrophe
					   .Replace( "&apos;", "'" ) // Handle apostrophe XML entity
					   .Replace( "&amp;", "&" ); // Ampersand last
		}

		/// <summary>
		/// Basic HTML entity encoder.
		/// </summary>
		private string EncodeHtml( string text )
		{
			if ( string.IsNullOrEmpty( text ) ) return "";
			// Manual basic version: Order matters! Ampersand must be first.
			return text.Replace( "&", "&amp;" )
					   .Replace( "<", "&lt;" )
					   .Replace( ">", "&gt;" )
					   .Replace( "\"", "&quot;" )
					   .Replace( "'", "&#39;" ); // Use numeric entity for apostrophe for broader compatibility
		}        /// <summary>
				 /// Checks if a tag name typically represents a self-closing element in HTML/Razor context.
				 /// Customize this list based on the elements you commonly use.
				 /// </summary>
		private bool IsSelfClosingTag( string tagName )
		{
			if ( string.IsNullOrEmpty( tagName ) ) return false;
			return tagName.ToLowerInvariant() switch
			{
				"textentry" => true,
				"input" => true,
				"img" => true,
				"br" => true,
				"hr" => true,
				// Add others like <meta>, <link> if relevant, though unlikely in UI markup
				_ => false,
			};
		}/// <summary>
		 /// Converts a Sandbox.Color to a CSS hex string (#RRGGBB or #RRGGBBAA).
		 /// </summary>
		private string ColorToHex( Color color )
		{
			// Include Alpha only if it's not fully opaque (or nearly opaque due to float precision)
			if ( color.a < 0.999f )
				return $"#{color.r:X2}{color.g:X2}{color.b:X2}{color.a:X2}";
			else
				return $"#{color.r:X2}{color.g:X2}{color.b:X2}";
		}
		void ShowDesignView()
		{
			_codeView.Visible = false;
			_view.Visible = true;

			Log.Info( "Switched to design view" );
		}

		void ShowCodeView()
		{
			_view.Visible = false;
			_codeView.Visible = true;

			Log.Info( "Switched to code view" );
		}

		void ShowSplitView()
		{
			_view.Visible = true;
			_codeView.Visible = true;

			Log.Info( "Switched to split view" );
		}
		void CutSelection()
		{
			if ( _codeTextEditor == null ) return;

			//textEditor.Cut();
		}

		void CopySelection()
		{
			if ( _codeTextEditor == null ) return;

			//textEditor.Copy();
		}

		void PasteSelection()
		{
			;
			if ( _codeTextEditor == null ) return;

			//textEditor.Paste();
		}

		void SelectAll()
		{
			if ( _codeTextEditor == null ) return;

			_codeTextEditor.SelectAll();
		}

	}
}
