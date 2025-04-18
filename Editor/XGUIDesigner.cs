using Editor;
using Sandbox.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace XGUI.XGUIEditor;
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
	private Menu _recentFilesMenu;
	private readonly List<string> _recentFiles = new();

	XGUIView _view;

	Widget _heirarchy;
	PanelInspector _inspector;

	Widget _componentPalette;
	Widget _codeView;
	string _currentFilePath;
	bool _isDirty;

	// Properties for design state
	private enum ViewMode { Design, Code, Split }
	private ViewMode _currentViewMode = ViewMode.Design;

	public XGUIDesigner()
	{
		DeleteOnClose = true;

		Title = "XGUI Razor Designer";
		Size = new Vector2( 1280, 720 );

		CreateUI();
		Show();

		New(); // Create a blank document to start with
	}

	protected override bool OnClose()
	{
		_view.CleanUp();
		return base.OnClose();
	}

	public void CreateUI()
	{
		BuildMenuBar();

		_view = new XGUIView();
		_view.SetSizeMode( SizeMode.Expand, SizeMode.Expand );
		_view.OnElementSelected = UpdateSelectedElement; // Add this line to handle selection

		// Create hierarchy panel for component structure
		_heirarchy = new Widget( null );
		_heirarchy.Layout = Layout.Column();

		// Create inspector panel using our custom PanelInspector
		_inspector = new PanelInspector();
		_inspector.WindowTitle = "Inspector";
		_inspector.SetWindowIcon( "info" );

		// Create component palette
		_componentPalette = new Widget( null );
		_componentPalette.Layout = Layout.Column();
		CreateComponentPalette();

		// Create code view
		_codeView = new Widget( null );
		_codeView.SetSizeMode( SizeMode.Expand, SizeMode.Expand );
		CreateCodeView();

		// Set up dock areas
		_view.WindowTitle = "Design View";
		_view.SetWindowIcon( "visibility" );
		this.DockManager.AddDock( null, _view );
		_heirarchy.WindowTitle = "Hierarchy";
		_heirarchy.SetWindowIcon( "view_list" );
		this.DockManager.AddDock( null, _heirarchy, dockArea: DockArea.Left, split: 0.20f );
		_inspector.WindowTitle = "Inspector";
		_inspector.SetWindowIcon( "info" );
		this.DockManager.AddDock( null, _inspector, dockArea: DockArea.Right, split: 0.20f );

		_componentPalette.WindowTitle = "Component Palette";
		_componentPalette.SetWindowIcon( "view_module" );
		_codeView.WindowTitle = "Code View";
		_codeView.SetWindowIcon( "code" );

		// old split vertically code
		//this.DockManager.AddDock( null, _componentPalette, dockArea: DockArea.Bottom, split: 0.50f );
		//this.DockManager.AddDock( null, _codeView, dockArea: DockArea.Bottom, split: 0.50f );

		// Add component palette and code view to the bottom, split side by side horizontally (not like above, help me copilot please ) 

		this.DockManager.AddDock( null, _componentPalette, dockArea: DockArea.Bottom, split: 0.30f );
		this.DockManager.AddDock( _componentPalette, _codeView, dockArea: DockArea.Right, split: 0.40f );
	}

	private void UpdateSelectedElement( Panel selectedElement )
	{
		if ( selectedElement != null )
		{
			// Pass this designer instance to the inspector
			_inspector.SetTarget( selectedElement );
		}
	}

	private void CreateComponentPalette()
	{
		var container = _componentPalette.Layout.Add( new Widget( null ) );
		container.SetSizeMode( SizeMode.Expand, SizeMode.Expand );
		container.Layout = Layout.Row();
		container.Layout.Spacing = 4;

		// Create the component categories (containers for component buttons)
		var layoutsCategory = CreateComponentCategory( container.Layout, "Layouts" );
		var controlsCategory = CreateComponentCategory( container.Layout, "Controls" );
		var containersCategory = CreateComponentCategory( container.Layout, "Containers" );

		// Add basic layout components
		AddComponentButton( layoutsCategory, "Div", "div" );
		AddComponentButton( layoutsCategory, "Row", "div", "class=\"self-layout self-layout-row\"" );
		AddComponentButton( layoutsCategory, "Column", "div", "class=\"self-layout self-layout-column\"" );

		// Add basic control components
		AddComponentButton( controlsCategory, "Button", "button" );
		AddComponentButton( controlsCategory, "Checkbox", "check" );
		AddComponentButton( controlsCategory, "Label", "label" );
		AddComponentButton( controlsCategory, "Text Entry", "textentry" );
		AddComponentButton( controlsCategory, "Slider", "sliderscale", "min=0 max=100 step=1" );

		// Add container components
		AddComponentButton( containersCategory, "Group Box", "groupbox", "title=\"Group\"" );
		AddComponentButton( containersCategory, "Tab Control", "tabcontrol" );
		AddComponentButton( containersCategory, "Combo Box", "combobox" );
	}

	private Layout CreateComponentCategory( Layout parentLayout, string categoryName )
	{
		var categoryWidget = parentLayout.Add( new Widget( null ) );
		categoryWidget.Layout = Layout.Column();
		categoryWidget.Layout.Spacing = 2;
		categoryWidget.Layout.Add( new Editor.Label( categoryName ) );

		return categoryWidget.Layout;
	}

	private void AddComponentButton( Layout layout, string displayName, string tagName, string attributes = "" )
	{
		var button = layout.Add( new Editor.Button( displayName ) );
		button.Clicked = () => AddComponentToDesignSurface( tagName, attributes );
	}

	private void AddComponentToDesignSurface( string tagName, string attributes = "" )
	{
		// Implementation to add the component to the current design
		Log.Info( $"Adding {tagName} component to design surface" );

		// Get current code
		var textEditor = FindTextEditInCodeView();
		if ( textEditor == null ) return;

		string currentCode = textEditor.PlainText;

		// Find the position where to insert the new component
		string componentCode = attributes.Length > 0 ? $"<{tagName} {attributes}></{tagName}>" : $"<{tagName}></{tagName}>";

		// Try to find the root tag where we should insert the component
		var rootTagMatch = Regex.Match( currentCode, @"<root[^>]*>([\s\S]*?)</root>" );
		if ( rootTagMatch.Success )
		{
			int insertPosition = rootTagMatch.Groups[1].Index;
			string updatedCode = currentCode.Substring( 0, insertPosition ) +
				"\n    " + componentCode +
				currentCode.Substring( insertPosition );

			UpdateCodeView( updatedCode );

			// Update the design view
			UpdateDesignView( updatedCode );
		}
		else
		{
			Log.Warning( "Could not find <root> tag to insert component" );
		}
	}

	private void UpdateDesignView( string razorCode )
	{
		try
		{
			// Reset the design view first
			if ( _view == null )
			{
				Log.Error( "_view is null in UpdateDesignView" );
				return;
			}

			// Create a blank window if none exists
			if ( _view.Window == null )
			{
				_view.CreateBlankWindow();
			}

			// Delete existing window and create a new one
			_view.Window?.Delete();
			_view.CreateBlankWindow();

			// Extract just the HTML part within <root> tags
			var match = Regex.Match( razorCode, @"<root[^>]*>([\s\S]*?)</root>" );
			if ( match.Success )
			{
				string htmlContent = match.Groups[1].Value;

				// Check for null WindowContent before trying to use it
				if ( _view.WindowContent == null )
				{
					Log.Error( "WindowContent is null after creating blank window" );
					return;
				}

				// Create elements based on the parsed HTML content
				ParseAndAddElements( htmlContent, _view.WindowContent );

				// Update the hierarchy panel
				UpdateHierarchyPanel( _view.WindowContent );
			}
		}
		catch ( System.Exception ex )
		{
			Log.Error( $"Error updating design view: {ex.Message}\n{ex.StackTrace}" );
		}
	}


	private void ParseAndAddElements( string htmlContent, Panel parentPanel )
	{
		// Simple parsing of HTML content and adding UI elements
		// Note: A full implementation would require a proper HTML parser

		// For now, let's handle some basic elements
		var tagMatches = Regex.Matches( htmlContent, @"<(\w+)([^>]*)>(.*?)</\1>|<(\w+)([^/]*)/>", RegexOptions.Singleline );

		foreach ( Match match in tagMatches )
		{
			string tagName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[4].Value;
			string attributes = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[5].Value;
			string innerContent = match.Groups[3].Value;

			Panel newElement = null;

			// Create the appropriate element based on tag name
			switch ( tagName.ToLower() )
			{
				case "div":
					newElement = new Panel();

					// Handle layout classes
					if ( attributes.Contains( "self-layout-row" ) )
					{
						newElement.AddClass( "self-layout" );
						newElement.AddClass( "self-layout-row" );
					}
					else if ( attributes.Contains( "self-layout-column" ) )
					{
						newElement.AddClass( "self-layout" );
						newElement.AddClass( "self-layout-column" );
					}
					break;

				case "button":
					var button = new Sandbox.UI.Button();
					button.Text = innerContent;
					newElement = button;
					break;

				case "check":
					var checkbox = new XGUI.CheckBox();
					checkbox.LabelText = innerContent;
					newElement = checkbox;
					break;

				case "label":
					var label = new Sandbox.UI.Label();
					label.Text = innerContent;
					newElement = label;
					break;

				case "textentry":
					newElement = new Sandbox.UI.TextEntry();
					break;

				case "sliderscale":
					var slider = new XGUI.SliderScale();
					// Parse attributes for min, max, step
					var minMatch = Regex.Match( attributes, @"min=(\d+)" );
					var maxMatch = Regex.Match( attributes, @"max=(\d+)" );
					if ( minMatch.Success && maxMatch.Success )
					{
						float min = float.Parse( minMatch.Groups[1].Value );
						float max = float.Parse( maxMatch.Groups[1].Value );
						slider.MinValue = min;
						slider.MaxValue = max;
					}
					newElement = slider;
					break;

				case "groupbox":
					var groupBox = new XGUI.GroupBox();
					// Parse title attribute
					var titleMatch = Regex.Match( attributes, @"title=""([^""]*)""|title='([^']*)'|title=(\S+)" );
					if ( titleMatch.Success )
					{
						string title = titleMatch.Groups[1].Success ? titleMatch.Groups[1].Value :
							titleMatch.Groups[2].Success ? titleMatch.Groups[2].Value : titleMatch.Groups[3].Value;
						groupBox.Title = title;
					}
					newElement = groupBox;
					break;

					// Add more element types as needed
			}

			// Add the new element to the parent if created
			if ( newElement != null )
			{
				parentPanel.AddChild( newElement );

				// Process inner content if it exists
				if ( !string.IsNullOrWhiteSpace( innerContent ) && newElement is Panel panel )
				{
					ParseAndAddElements( innerContent, panel );
				}
			}
		}
	}

	private void UpdateHierarchyPanel( Panel rootElement )
	{
		// Clear existing hierarchy
		foreach ( var widget in _heirarchy.Children.ToArray() )
		{
			if ( widget is not Editor.Label ) // Keep the label
			{
				widget.Destroy();
			}
		}

		// Create a tree view to display the element hierarchy
		var treeView = new TreeView( _heirarchy );
		_heirarchy.Layout.Add( treeView );

		// Enable selection functionality
		treeView.MultiSelect = false; // Only select one item at a time
		treeView.ItemSelected = ( object item ) =>
		{
			if ( item is Panel panel )
			{
				UpdateSelectedElement( panel );
			}
		};

		// Start with a root node for the window
		var rootNode = new TreeNode();
		rootNode.Name = "Window Root";
		treeView.AddItem( rootNode );

		// Build the tree starting from the window content
		BuildTreeForPanel( rootElement, rootNode );
	}

	private void BuildTreeForPanel( Panel panel, TreeNode parentNode )
	{
		string displayName = GetDisplayNameForPanel( panel );

		// Create a new tree node with the panel as its value
		var node = new TreeNode( panel );
		node.Name = displayName;

		// Add the node to the parent
		parentNode.AddItem( node );

		// Add children
		foreach ( var child in panel.Children )
		{
			if ( child is Panel childPanel )
			{
				BuildTreeForPanel( childPanel, node );
			}
		}
	}


	private string GetDisplayNameForPanel( Panel panel )
	{
		if ( panel is Sandbox.UI.Button button ) return $"Button: {button.Text}";
		if ( panel is Sandbox.UI.Label label ) return $"Label: {label.Text}";
		if ( panel is XGUI.CheckBox checkbox ) return $"CheckBox: {checkbox.LabelText}";
		if ( panel is XGUI.GroupBox groupBox ) return $"GroupBox: {groupBox.Title}";
		if ( panel is Sandbox.UI.TextEntry ) return "TextEntry";
		if ( panel is XGUI.SliderScale ) return "Slider";

		// For generic panels, check if it has layout classes
		if ( panel.HasClass( "self-layout-row" ) ) return "Row";
		if ( panel.HasClass( "self-layout-column" ) ) return "Column";

		return panel.GetType().Name;
	}

	private void CreateCodeView()
	{
		_codeView.Layout = Layout.Column();

		var textEditor = _codeView.Layout.Add( new TextEdit( null ), 1 );
		textEditor.TextChanged = ( string s ) =>
		{
			_isDirty = true;

			// Update the design view based on code changes
			UpdateDesignView( textEditor.PlainText );
		};
	}

	private void UpdateCodeView( string newContent )
	{
		// Update the code view with new content or append to existing content
		var textEditor = FindTextEditInCodeView();
		if ( textEditor != null )
		{
			textEditor.PlainText = newContent;
			_isDirty = true;
		}
	}

	public TextEdit FindTextEditInCodeView()
	{
		// Manual implementation to find the first TextEdit in _codeView
		if ( _codeView == null || _codeView.Layout == null )
			return null;

		foreach ( var widget in _codeView.Children )
		{
			if ( widget is TextEdit textEdit )
				return textEdit;
		}

		return null;
	}

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
		// Create a new blank Razor file with basic structure
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

		UpdateCodeView( template );
		_currentFilePath = null;
		_isDirty = false;
		Title = "XGUI Razor Designer - Untitled";

		// Reset the design view
		_view.Window?.Delete();
		_view.CreateBlankWindow();
	}

	void Open()
	{
		// Use FileDialog instead of OpenFileDialog
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
			UpdateCodeView( content );
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

			// Update the design view
			UpdateDesignView( content );
		}
		catch ( System.Exception ex )
		{
			Log.Error( $"Error opening file: {ex.Message}" );
		}
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
		var textEditor = FindTextEditInCodeView();
		if ( textEditor == null ) return;

		try
		{
			File.WriteAllText( path, textEditor.PlainText );
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

	void ShowDesignView()
	{
		_currentViewMode = ViewMode.Design;
		_codeView.Visible = false;
		_view.Visible = true;

		Log.Info( "Switched to design view" );
	}

	void ShowCodeView()
	{
		_currentViewMode = ViewMode.Code;
		_view.Visible = false;
		_codeView.Visible = true;

		Log.Info( "Switched to code view" );
	}

	void ShowSplitView()
	{
		_currentViewMode = ViewMode.Split;
		_view.Visible = true;
		_codeView.Visible = true;

		Log.Info( "Switched to split view" );
	}

	void CutSelection()
	{
		var textEditor = FindTextEditInCodeView();
		if ( textEditor == null ) return;

		//textEditor.Cut();
	}

	void CopySelection()
	{
		var textEditor = FindTextEditInCodeView();
		if ( textEditor == null ) return;

		//textEditor.Copy();
	}

	void PasteSelection()
	{
		var textEditor = FindTextEditInCodeView();
		if ( textEditor == null ) return;

		//textEditor.Paste();
	}

	void SelectAll()
	{
		var textEditor = FindTextEditInCodeView();
		if ( textEditor == null ) return;

		textEditor.SelectAll();
	}
}
