using Editor;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace XGUI.XGUIEditor
{
	/// <summary>
	/// Inspects properties of a MarkupNode and provides UI for editing
	/// </summary>
	public class PanelInspector : Widget
	{
		public XGUIDesigner OwnerDesigner;
		private Panel _targetPanel;
		private MarkupNode _targetNode;

		// Sections
		private Widget _generalSection;
		private Widget _styleSection;

		// Property groups
		private Dictionary<string, PropertyGroup> _generalGroups = new Dictionary<string, PropertyGroup>();
		private Dictionary<string, PropertyGroup> _styleGroups = new Dictionary<string, PropertyGroup>();

		// Tracks all created editors for efficient updates
		private Dictionary<string, PropertyEditor> _generalEditors = new Dictionary<string, PropertyEditor>();
		private Dictionary<string, PropertyEditor> _styleEditors = new Dictionary<string, PropertyEditor>( StringComparer.OrdinalIgnoreCase );

		// Scroll area container
		private ScrollArea _scrollArea;
		private Widget _contentContainer;

		/// <summary>
		/// Action triggered when a property potentially changes a source attribute or content.
		/// </summary>
		public Action<MarkupNode, string, object> OnPropertyChanged;

		public PanelInspector( Widget parent = null ) : base( parent )
		{
			// Set up the layout for PanelInspector to fill available space
			Layout = Layout.Column();

			// Create a ScrollArea to contain all inspector contents
			_scrollArea = Layout.Add( new ScrollArea( null ), 1 );
			_scrollArea.SetSizeMode( SizeMode.Expand, SizeMode.Expand );
			_scrollArea.MaximumSize = new Vector2( float.MaxValue, float.MaxValue );

			// Create a content container inside the scroll area
			_contentContainer = new Widget( null );
			_contentContainer.Layout = Layout.Flow();
			_contentContainer.Layout.Spacing = 5;
			_contentContainer.Layout.Margin = 10;

			// Add the content container to the scroll area
			_scrollArea.Canvas = _contentContainer;

			CreateSections();
		}

		private void CreateSections()
		{
			_generalSection = AddSection( "General", "description", true );
			_styleSection = AddSection( "Style", "brush", true );
		}

		private Widget AddSection( string title, string icon, bool initiallyExpanded )
		{
			var expandGroup = new ExpandGroup( _contentContainer );
			expandGroup.StateCookieName = $"PanelInspector.{title}";
			expandGroup.Icon = icon;
			expandGroup.Title = title;
			expandGroup.SetOpenState( initiallyExpanded );
			var container = new Widget( null ) { Layout = Layout.Column() };
			container.Layout.Spacing = 4;
			container.Layout.Margin = new Margin( 8 );
			container.MinimumSize = new Vector2( 0, 10 ); // Ensure it doesn't collapse completely
			expandGroup.SetWidget( container );
			_contentContainer.Layout.Add( expandGroup );
			return container;
		}

		public void SetTarget( Panel panel, MarkupNode node, bool rebuild = true )
		{
			bool sameTarget = (_targetPanel == panel && _targetNode == node);

			_targetPanel = panel;
			_targetNode = node;

			if ( rebuild )
			{
				Rebuild( true );
			}
			else
			{
				Rebuild( false );
			}
		}

		public void Rebuild( bool forceFullRebuild = true )
		{
			if ( _targetNode == null || _targetNode.Type != NodeType.Element )
			{
				ClearAllSections();
				ClearEditorReferences();
				return; // Nothing to inspect or not an element
			}

			// Only perform a full rebuild when necessary
			if ( forceFullRebuild )
			{
				ClearAllSections();
				ClearEditorReferences();
				RebuildGeneralSection();
				RebuildStyleSection();
			}
			else
			{
				// Update individual properties without rebuilding the entire UI
				UpdatePropertiesIncremental();
			}
		}

		private void ClearEditorReferences()
		{
			_generalEditors.Clear();
			_styleEditors.Clear();
		}

		private void ClearAllSections()
		{
			_generalSection?.Layout.Clear( true );
			_styleSection?.Layout.Clear( true );
		}

		private void RebuildGeneralSection()
		{
			var layout = _generalSection.Layout;
			layout.Clear( true );
			_generalGroups.Clear();
			_generalEditors.Clear();

			if ( _targetNode == null || _targetNode.Type != NodeType.Element ) return;

			// Create core properties group
			var coreGroup = CreatePropertyGroup( "Core", layout, _generalGroups );

			// Add type property (read-only)
			var typeEditor = coreGroup.AddEditor<TextPropertyEditor>( "type", "Type" );
			typeEditor.SetValueSilently( _targetNode.TagName );
			typeEditor.RootWidget.Enabled = false; // Make read-only

			// Add class property
			string currentClasses = _targetNode.Attributes.GetValueOrDefault( "class", "" );
			var classEditor = coreGroup.AddEditor<TextPropertyEditor>( "class", "Classes" );
			classEditor.SetValueSilently( currentClasses );
			classEditor.ValueChanged += ( value ) =>
			{
				string strValue = value.ToString().Trim();
				_targetNode.Attributes["class"] = strValue;
				if ( _targetPanel != null ) UpdatePanelClasses( _targetPanel, strValue );
				OnPropertyChanged?.Invoke( _targetNode, "class", strValue );
			};
			_generalEditors["class"] = classEditor;

			// Add ID property
			string currentId = _targetNode.Attributes.GetValueOrDefault( "id", "" );
			var idEditor = coreGroup.AddEditor<TextPropertyEditor>( "id", "ID" );
			idEditor.SetValueSilently( currentId );
			idEditor.ValueChanged += ( value ) =>
			{
				string strValue = value.ToString().Trim();
				_targetNode.Attributes["id"] = strValue;
				OnPropertyChanged?.Invoke( _targetNode, "id", strValue );
			};
			_generalEditors["id"] = idEditor;

			// Add element-specific properties
			AddElementSpecificProperties( _targetNode, _targetPanel );
		}

		private void AddElementSpecificProperties( MarkupNode node, Panel panel )
		{
			string tag = node.TagName.ToLowerInvariant();
			string innerText = node.Children.FirstOrDefault( c => c.Type == NodeType.Text && !string.IsNullOrWhiteSpace( c.TextContent ) )?.TextContent ?? "";

			var contentGroup = CreatePropertyGroup( "Content", _generalSection.Layout, _generalGroups );

			// Handle window root node (tag="root")
			if ( tag == "root" && panel is XGUI.Window window )
			{
				// Theme group
				var themeGroup = CreatePropertyGroup( "Theme", _generalSection.Layout, _generalGroups );

				// Theme dropdown
				var themeEditor = themeGroup.AddDropdownEditor( "theme", "Window Theme",
					FileSystem.Mounted.FindFile( "/XGUI/DefaultStyles/", "*.scss" )
					.Select( f => System.IO.Path.GetFileNameWithoutExtension( f ) )
					.ToArray() );

				// Get current theme path filename without extension for the dropdown
				string currentThemeFilename = System.IO.Path.GetFileNameWithoutExtension( OwnerDesigner.CurrentTheme );
				themeEditor.SetValueSilently( currentThemeFilename );

				themeEditor.ValueChanged += ( value ) =>
				{
					string themeName = value.ToString();
					string fullThemePath = $"/XGUI/DefaultStyles/{themeName}.scss";

					// Update the designer's current theme
					OwnerDesigner.CurrentTheme = fullThemePath;

					// Apply the theme to the window if it exists
					if ( window != null )
					{
						window.SetTheme( fullThemePath );
					}

					OnPropertyChanged?.Invoke( node, "theme", themeName );
				};
				_generalEditors["theme"] = themeEditor;

				// Window title property
				string titleAttr = node.Attributes.GetValueOrDefault( "title", "Window" );
				var titleEditor = contentGroup.AddEditor<TextPropertyEditor>( "title", "Window Title" );
				titleEditor.SetValueSilently( titleAttr );
				titleEditor.ValueChanged += ( value ) =>
				{
					string strValue = value.ToString();
					node.Attributes["title"] = strValue;
					if ( window != null ) window.Title = strValue;
					OnPropertyChanged?.Invoke( node, "title", strValue );
				};
				_generalEditors["title"] = titleEditor;

				// Window Dimensions Group
				var dimensionsGroup = CreatePropertyGroup( "Dimensions", _generalSection.Layout, _generalGroups );

				// Width property
				string widthAttr = node.Attributes.GetValueOrDefault( "width", "" );
				var widthEditor = dimensionsGroup.AddEditor<TextPropertyEditor>( "width", "Width" );
				widthEditor.SetValueSilently( widthAttr );
				widthEditor.ValueChanged += ( value ) =>
				{
					string strValue = value.ToString();
					node.Attributes["width"] = strValue;
					if ( window != null && int.TryParse( strValue, out var length ) )
						window.Style.Width = length;
					OnPropertyChanged?.Invoke( node, "width", strValue );
				};
				_generalEditors["width"] = widthEditor;

				// Height property
				string heightAttr = node.Attributes.GetValueOrDefault( "height", "" );
				var heightEditor = dimensionsGroup.AddEditor<TextPropertyEditor>( "height", "Height" );
				heightEditor.SetValueSilently( heightAttr );
				heightEditor.ValueChanged += ( value ) =>
				{
					string strValue = value.ToString();
					node.Attributes["height"] = strValue;
					if ( window != null && int.TryParse( strValue, out var length ) )
						window.Style.Height = length;
					OnPropertyChanged?.Invoke( node, "height", strValue );
				};
				_generalEditors["height"] = heightEditor;

				// Min width property
				string minWidthAttr = node.Attributes.GetValueOrDefault( "minwidth", "" );
				var minWidthEditor = dimensionsGroup.AddEditor<TextPropertyEditor>( "minwidth", "Min Width" );
				minWidthEditor.SetValueSilently( minWidthAttr );
				minWidthEditor.ValueChanged += ( value ) =>
				{
					string strValue = value.ToString();
					node.Attributes["minwidth"] = strValue;
					if ( window != null && int.TryParse( strValue, out var length ) )
						window.MinSize = new Vector2( length, window.MinSize.y );
					OnPropertyChanged?.Invoke( node, "minwidth", strValue );
				};
				_generalEditors["minwidth"] = minWidthEditor;

				// Min height property
				string minHeightAttr = node.Attributes.GetValueOrDefault( "minheight", "" );
				var minHeightEditor = dimensionsGroup.AddEditor<TextPropertyEditor>( "minheight", "Min Height" );
				minHeightEditor.SetValueSilently( minHeightAttr );
				minHeightEditor.ValueChanged += ( value ) =>
				{
					string strValue = value.ToString();
					node.Attributes["minheight"] = strValue;
					if ( window != null && int.TryParse( strValue, out var length ) )
						window.MinSize = new Vector2( window.MinSize.x, length );
					OnPropertyChanged?.Invoke( node, "minheight", strValue );
				};
				_generalEditors["minheight"] = minHeightEditor;

				// Position group
				var positionGroup = CreatePropertyGroup( "Position", _generalSection.Layout, _generalGroups );

				// X position
				string xPosAttr = node.Attributes.GetValueOrDefault( "x", "" );
				var xPosEditor = positionGroup.AddEditor<TextPropertyEditor>( "x", "X Position" );
				xPosEditor.SetValueSilently( xPosAttr );
				xPosEditor.ValueChanged += ( value ) =>
				{
					string strValue = value.ToString();
					node.Attributes["x"] = strValue;
					if ( window != null && float.TryParse( strValue, out var floatVal ) )
						window.Position = new Vector2( floatVal, window.Position.y );
					OnPropertyChanged?.Invoke( node, "x", strValue );
				};
				_generalEditors["x"] = xPosEditor;

				// Y position
				string yPosAttr = node.Attributes.GetValueOrDefault( "y", "" );
				var yPosEditor = positionGroup.AddEditor<TextPropertyEditor>( "y", "Y Position" );
				yPosEditor.SetValueSilently( yPosAttr );
				yPosEditor.ValueChanged += ( value ) =>
				{
					string strValue = value.ToString();
					node.Attributes["y"] = strValue;
					if ( window != null && float.TryParse( strValue, out var floatVal ) )
						window.Position = new Vector2( window.Position.x, floatVal );
					OnPropertyChanged?.Invoke( node, "y", strValue );
				};
				_generalEditors["y"] = yPosEditor;

				// Controls group
				var controlsGroup = CreatePropertyGroup( "Window Controls", _generalSection.Layout, _generalGroups );

				// Has Close button
				bool hasClose = node.Attributes.GetValueOrDefault( "hasclose", "true" ).Equals( "true", StringComparison.OrdinalIgnoreCase );
				var hasCloseEditor = controlsGroup.AddEditor<BoolPropertyEditor>( "hasclose", "Close Button" );
				hasCloseEditor.SetValueSilently( hasClose ? "true" : "" );
				hasCloseEditor.ValueChanged += ( value ) =>
				{
					bool boolValue = (bool)value;
					node.Attributes["hasclose"] = boolValue ? "true" : "false";
					if ( window != null ) window.HasClose = boolValue;
					OnPropertyChanged?.Invoke( node, "hasclose", boolValue );
				};
				_generalEditors["hasclose"] = hasCloseEditor;

				// Has Minimize button
				bool hasMinimize = node.Attributes.GetValueOrDefault( "hasminimise", "false" ).Equals( "true", StringComparison.OrdinalIgnoreCase );
				var hasMinimizeEditor = controlsGroup.AddEditor<BoolPropertyEditor>( "hasminimise", "Minimize Button" );
				hasMinimizeEditor.SetValueSilently( hasMinimize ? "true" : "" );
				hasMinimizeEditor.ValueChanged += ( value ) =>
				{
					bool boolValue = (bool)value;
					node.Attributes["hasminimise"] = boolValue ? "true" : "false";
					if ( window != null ) window.HasMinimise = boolValue;
					OnPropertyChanged?.Invoke( node, "hasminimise", boolValue );
				};
				_generalEditors["hasminimise"] = hasMinimizeEditor;

				// Has Maximize button
				bool hasMaximize = node.Attributes.GetValueOrDefault( "hasmaximise", "false" ).Equals( "true", StringComparison.OrdinalIgnoreCase );
				var hasMaximizeEditor = controlsGroup.AddEditor<BoolPropertyEditor>( "hasmaximise", "Maximize Button" );
				hasMaximizeEditor.SetValueSilently( hasMaximize ? "true" : "" );
				hasMaximizeEditor.ValueChanged += ( value ) =>
				{
					bool boolValue = (bool)value;
					node.Attributes["hasmaximise"] = boolValue ? "true" : "false";
					if ( window != null ) window.HasMaximise = boolValue;
					OnPropertyChanged?.Invoke( node, "hasmaximise", boolValue );
				};
				_generalEditors["hasmaximise"] = hasMaximizeEditor;

				// Behavior group
				var behaviorGroup = CreatePropertyGroup( "Behavior", _generalSection.Layout, _generalGroups );

				// Is Draggable
				bool isDraggable = node.Attributes.GetValueOrDefault( "isdraggable", "true" ).Equals( "true", StringComparison.OrdinalIgnoreCase );
				var isDraggableEditor = behaviorGroup.AddEditor<BoolPropertyEditor>( "isdraggable", "Draggable" );
				isDraggableEditor.SetValueSilently( isDraggable ? "true" : "" );
				isDraggableEditor.ValueChanged += ( value ) =>
				{
					bool boolValue = (bool)value;
					node.Attributes["isdraggable"] = boolValue ? "true" : "false";
					if ( window != null ) window.IsDraggable = boolValue;
					OnPropertyChanged?.Invoke( node, "isdraggable", boolValue );
				};
				_generalEditors["isdraggable"] = isDraggableEditor;

				// Is Resizable
				bool isResizable = node.Attributes.GetValueOrDefault( "isresizable", "true" ).Equals( "true", StringComparison.OrdinalIgnoreCase );
				var isResizableEditor = behaviorGroup.AddEditor<BoolPropertyEditor>( "isresizable", "Resizable" );
				isResizableEditor.SetValueSilently( isResizable ? "true" : "" );
				isResizableEditor.ValueChanged += ( value ) =>
				{
					bool boolValue = (bool)value;
					node.Attributes["isresizable"] = boolValue ? "true" : "false";
					if ( window != null ) window.IsResizable = boolValue;
					OnPropertyChanged?.Invoke( node, "isresizable", boolValue );
				};
				_generalEditors["isresizable"] = isResizableEditor;

				return; // We've handled the window properties, so exit the method
			}

			switch ( tag )
			{
				case "button":
				case "label":
					var textEditor = contentGroup.AddEditor<TextPropertyEditor>( "innertext", "Text" );
					textEditor.SetValueSilently( innerText );
					textEditor.ValueChanged += ( value ) =>
					{
						string strValue = value.ToString();
						UpdateOrCreateChildTextNode( node, strValue );
						if ( panel is Sandbox.UI.Button b ) b.Text = strValue;
						else if ( panel is Sandbox.UI.Label l ) l.Text = strValue;
						OnPropertyChanged?.Invoke( node, "innertext", strValue );
					};
					_generalEditors["innertext"] = textEditor;
					break;

				case "check":
					var labelEditor = contentGroup.AddEditor<TextPropertyEditor>( "innertext", "Label" );
					labelEditor.SetValueSilently( innerText );
					labelEditor.ValueChanged += ( value ) =>
					{
						string strValue = value.ToString();
						UpdateOrCreateChildTextNode( node, strValue );
						if ( panel is XGUI.CheckBox cb ) cb.LabelText = strValue;
						OnPropertyChanged?.Invoke( node, "innertext", strValue );
					};
					_generalEditors["innertext"] = labelEditor;

					bool isChecked = node.Attributes.ContainsKey( "checked" );
					var checkedEditor = contentGroup.AddEditor<BoolPropertyEditor>( "checked", "Checked" );
					checkedEditor.SetValueSilently( isChecked ? "true" : "" );
					checkedEditor.ValueChanged += ( value ) =>
					{
						bool boolValue = (bool)value;
						if ( boolValue ) node.Attributes["checked"] = null; // Valueless
						else node.Attributes.Remove( "checked" );
						if ( panel is XGUI.CheckBox cb ) cb.Checked = boolValue;
						OnPropertyChanged?.Invoke( node, "checked", boolValue );
					};
					_generalEditors["checked"] = checkedEditor;
					break;

				case "groupbox":
					string titleAttr = node.Attributes.GetValueOrDefault( "title", "" );
					var titleEditor = contentGroup.AddEditor<TextPropertyEditor>( "title", "Title" );
					titleEditor.SetValueSilently( titleAttr );
					titleEditor.ValueChanged += ( value ) =>
					{
						string strValue = value.ToString();
						node.Attributes["title"] = strValue;
						if ( panel is XGUI.GroupBox gb ) gb.Title = strValue;
						OnPropertyChanged?.Invoke( node, "title", strValue );
					};
					_generalEditors["title"] = titleEditor;
					break;

				case "sliderscale":
					string minAttr = node.Attributes.GetValueOrDefault( "min", "0" );
					var minEditor = contentGroup.AddEditor<TextPropertyEditor>( "min", "Min" );
					minEditor.SetValueSilently( minAttr );
					minEditor.ValueChanged += ( value ) =>
					{
						string strValue = value.ToString();
						node.Attributes["min"] = strValue;
						if ( panel is XGUI.SliderScale sl && float.TryParse( strValue, CultureInfo.InvariantCulture, out var v ) ) sl.MinValue = v;
						OnPropertyChanged?.Invoke( node, "min", strValue );
					};
					_generalEditors["min"] = minEditor;

					string maxAttr = node.Attributes.GetValueOrDefault( "max", "100" );
					var maxEditor = contentGroup.AddEditor<TextPropertyEditor>( "max", "Max" );
					maxEditor.SetValueSilently( maxAttr );
					maxEditor.ValueChanged += ( value ) =>
					{
						string strValue = value.ToString();
						node.Attributes["max"] = strValue;
						if ( panel is XGUI.SliderScale sl && float.TryParse( strValue, CultureInfo.InvariantCulture, out var v ) ) sl.MaxValue = v;
						OnPropertyChanged?.Invoke( node, "max", strValue );
					};
					_generalEditors["max"] = maxEditor;
					break;
			}
		}

		private void RebuildStyleSection()
		{
			var layout = _styleSection.Layout;
			layout.Clear( true );
			_styleGroups.Clear();
			_styleEditors.Clear();

			if ( _targetNode == null || _targetNode.Type != NodeType.Element ) return;

			string styleAttributeValue = _targetNode.Attributes.GetValueOrDefault( "style", "" );
			var currentStyles = ParseStyleAttribute( styleAttributeValue );

			// Add style property groups
			var sizeGroup = CreatePropertyGroup( "Size", layout, _styleGroups );
			var positionGroup = CreatePropertyGroup( "Position", layout, _styleGroups );
			var marginGroup = CreatePropertyGroup( "Margin", layout, _styleGroups );
			var paddingGroup = CreatePropertyGroup( "Padding", layout, _styleGroups );
			var colorGroup = CreatePropertyGroup( "Colors", layout, _styleGroups );

			// Helper to wire up style property editors
			Action<PropertyEditor> connectStyleEditor = ( editor ) =>
			{
				editor.ValueChanged += ( value ) =>
				{
					string cssValue;

					// Handle different editor types
					if ( editor is FloatPropertyEditor floatEditor )
					{
						cssValue = floatEditor.GetFormattedValue();
					}
					else if ( editor is ColorPropertyEditor colorEditor )
					{
						cssValue = colorEditor.GetHexString();
					}
					else
					{
						cssValue = value.ToString();
					}

					//Log.Info( cssValue );

					_targetNode.TryModifyStyle( editor.PropertyName, cssValue );
					if ( _targetPanel != null )
					{
						UpdatePanelSingleStyle( _targetPanel, editor.PropertyName, cssValue );
					}
					OnPropertyChanged?.Invoke( _targetNode, "style", _targetNode.Attributes["style"] );
				};

				_styleEditors[editor.PropertyName] = editor;
			};

			// Create size editors
			var widthEditor = sizeGroup.AddFloatEditor( "width", "Width", true );
			connectStyleEditor( widthEditor );

			var heightEditor = sizeGroup.AddFloatEditor( "height", "Height", true );
			connectStyleEditor( heightEditor );

			// Create position editors
			var leftEditor = positionGroup.AddFloatEditor( "left", "Left", true );
			connectStyleEditor( leftEditor );

			var topEditor = positionGroup.AddFloatEditor( "top", "Top", true );
			connectStyleEditor( topEditor );

			var rightEditor = positionGroup.AddFloatEditor( "right", "Right", true );
			connectStyleEditor( rightEditor );

			var bottomEditor = positionGroup.AddFloatEditor( "bottom", "Bottom", true );
			connectStyleEditor( bottomEditor );

			var positionEditor = positionGroup.AddDropdownEditor(
				"position", "Position", new[] { "relative", "absolute" }, true );
			connectStyleEditor( positionEditor );

			// Create margin editors
			var marginTopEditor = marginGroup.AddFloatEditor( "margin-top", "Top", true );
			connectStyleEditor( marginTopEditor );

			var marginRightEditor = marginGroup.AddFloatEditor( "margin-right", "Right", true );
			connectStyleEditor( marginRightEditor );

			var marginBottomEditor = marginGroup.AddFloatEditor( "margin-bottom", "Bottom", true );
			connectStyleEditor( marginBottomEditor );

			var marginLeftEditor = marginGroup.AddFloatEditor( "margin-left", "Left", true );
			connectStyleEditor( marginLeftEditor );

			// Create padding editors
			var paddingTopEditor = paddingGroup.AddFloatEditor( "padding-top", "Top", true );
			connectStyleEditor( paddingTopEditor );

			var paddingRightEditor = paddingGroup.AddFloatEditor( "padding-right", "Right", true );
			connectStyleEditor( paddingRightEditor );

			var paddingBottomEditor = paddingGroup.AddFloatEditor( "padding-bottom", "Bottom", true );
			connectStyleEditor( paddingBottomEditor );

			var paddingLeftEditor = paddingGroup.AddFloatEditor( "padding-left", "Left", true );
			connectStyleEditor( paddingLeftEditor );

			// Create color editors
			var bgColorEditor = colorGroup.AddColorEditor( "background-color", "Background", true );
			connectStyleEditor( bgColorEditor );

			var textColorEditor = colorGroup.AddColorEditor( "color", "Text", true );
			connectStyleEditor( textColorEditor );

			// Create font size editor
			var fontSizeEditor = colorGroup.AddFloatEditor( "font-size", "Font Size", true );
			connectStyleEditor( fontSizeEditor );

			// Set initial values for all style editors
			UpdateStyleEditors( currentStyles );

			// Add pseudo-class state editors if we have a live panel
			if ( _targetPanel != null )
			{
				var stateGroup = CreatePropertyGroup( "Live State", layout, _styleGroups );

				var hoverEditor = stateGroup.AddEditor<BoolPropertyEditor>( "hover", "Hover", false );
				hoverEditor.SetValueSilently( _targetPanel.HasClass( "hover" ) ? "true" : "" );
				hoverEditor.ValueChanged += ( value ) =>
				{
					bool isChecked = (bool)value;
					if ( isChecked ) _targetPanel.AddClass( "hover" );
					else _targetPanel.RemoveClass( "hover" );
				};

				var activeEditor = stateGroup.AddEditor<BoolPropertyEditor>( "active", "Active", false );
				activeEditor.SetValueSilently( _targetPanel.HasClass( "active" ) ? "true" : "" );
				activeEditor.ValueChanged += ( value ) =>
				{
					bool isChecked = (bool)value;
					if ( isChecked ) _targetPanel.AddClass( "active" );
					else _targetPanel.RemoveClass( "active" );
				};

				var focusEditor = stateGroup.AddEditor<BoolPropertyEditor>( "focus", "Focus", false );
				focusEditor.SetValueSilently( _targetPanel.HasClass( "focus" ) ? "true" : "" );
				focusEditor.ValueChanged += ( value ) =>
				{
					bool isChecked = (bool)value;
					if ( isChecked ) _targetPanel.AddClass( "focus" );
					else _targetPanel.RemoveClass( "focus" );
				};

				stateGroup.GroupLayout.Add( new Editor.Label( "Click to toggle states" ) );
			}
		}

		private PropertyGroup CreatePropertyGroup( string name, Layout parentLayout, Dictionary<string, PropertyGroup> groupDictionary )
		{
			var group = new PropertyGroup( name );
			group.CreateUI( parentLayout );
			groupDictionary[name] = group;
			return group;
		}

		private void UpdatePropertiesIncremental()
		{
			if ( _targetNode == null ) return;

			// Update general properties
			var generalValues = new Dictionary<string, string>();
			generalValues["class"] = _targetNode.Attributes.GetValueOrDefault( "class", "" );
			generalValues["id"] = _targetNode.Attributes.GetValueOrDefault( "id", "" );
			generalValues["innertext"] = _targetNode.Children.FirstOrDefault( c => c.Type == NodeType.Text )?.TextContent ?? "";

			// Add other attributes
			foreach ( var attr in _targetNode.Attributes )
			{
				if ( attr.Key != "style" && attr.Key != "class" && attr.Key != "id" )
				{
					generalValues[attr.Key] = attr.Value;
				}
			}

			foreach ( var editor in _generalEditors )
			{
				if ( generalValues.TryGetValue( editor.Key, out string value ) )
				{
					editor.Value.SetValueSilently( value );
				}
			}

			// Update style properties
			string styleAttributeValue = _targetNode.Attributes.GetValueOrDefault( "style", "" );
			var currentStyles = ParseStyleAttribute( styleAttributeValue );

			UpdateStyleEditors( currentStyles );
		}

		private void UpdateStyleEditors( Dictionary<string, string> styles )
		{
			foreach ( var editor in _styleEditors )
			{
				string cssProperty = editor.Key;
				string value = styles.GetValueOrDefault( cssProperty, "" );
				editor.Value.SetValueSilently( value );
			}
		}

		// Helper to update a single style property on the live panel
		private void UpdatePanelSingleStyle( Panel panel, string propertyName, string stringValue )
		{
			if ( panel == null ) return;
			try
			{
				switch ( propertyName.ToLowerInvariant() )
				{
					case "width": panel.Style.Width = ParseLength( stringValue ); break;
					case "height": panel.Style.Height = ParseLength( stringValue ); break;
					case "top": panel.Style.Top = ParseLength( stringValue ); break;
					case "left": panel.Style.Left = ParseLength( stringValue ); break;
					case "bottom": panel.Style.Bottom = ParseLength( stringValue ); break;
					case "right": panel.Style.Right = ParseLength( stringValue ); break;
					case "position": panel.Style.Position = stringValue == "absolute" ? PositionMode.Absolute : PositionMode.Relative; break;
					case "margin-top": panel.Style.MarginTop = ParseLength( stringValue ); break;
					case "margin-right": panel.Style.MarginRight = ParseLength( stringValue ); break;
					case "margin-bottom": panel.Style.MarginBottom = ParseLength( stringValue ); break;
					case "margin-left": panel.Style.MarginLeft = ParseLength( stringValue ); break;
					case "padding-top": panel.Style.PaddingTop = ParseLength( stringValue ); break;
					case "padding-right": panel.Style.PaddingRight = ParseLength( stringValue ); break;
					case "padding-bottom": panel.Style.PaddingBottom = ParseLength( stringValue ); break;
					case "padding-left": panel.Style.PaddingLeft = ParseLength( stringValue ); break;
					case "background-color": panel.Style.BackgroundColor = ParseColor( stringValue ); break;
					case "color": panel.Style.FontColor = ParseColor( stringValue ); break;
					case "font-size": panel.Style.FontSize = ParseLength( stringValue ); break;
				}

				panel.Style.Dirty();
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to apply live style preview for '{propertyName}:{stringValue}'. Error: {ex.Message}" );
			}
		}

		// Helper to update live panel classes
		private void UpdatePanelClasses( Panel panel, string classString )
		{
			if ( panel == null ) return;
			foreach ( var cls in panel.Class.ToList() ) panel.RemoveClass( cls );
			foreach ( var cls in classString.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) ) panel.AddClass( cls );
		}

		// Helper to update or create a direct child text node within a MarkupNode
		private void UpdateOrCreateChildTextNode( MarkupNode parentNode, string text )
		{
			if ( parentNode == null ) return;

			var textNode = parentNode.Children.FirstOrDefault( c => c.Type == NodeType.Text );
			if ( textNode != null )
			{
				textNode.TextContent = text;
			}
			else
			{
				parentNode.Children.Insert( 0, new MarkupNode
				{
					Type = NodeType.Text,
					TextContent = text,
					Parent = parentNode
				} );
			}
		}

		/// <summary>
		/// Parses a CSS style attribute string (e.g., "width: 100px; color: red;")
		/// into a dictionary of property-value pairs.
		/// </summary>
		private Dictionary<string, string> ParseStyleAttribute( string styleString )
		{
			var styles = new Dictionary<string, string>( System.StringComparer.OrdinalIgnoreCase );
			if ( string.IsNullOrWhiteSpace( styleString ) )
			{
				return styles;
			}

			var declarations = styleString.Split( ';', System.StringSplitOptions.RemoveEmptyEntries );

			foreach ( var declaration in declarations )
			{
				var parts = declaration.Split( ':', 2 );
				if ( parts.Length == 2 )
				{
					string property = parts[0].Trim();
					string value = parts[1].Trim();

					if ( !string.IsNullOrEmpty( property ) )
					{
						styles[property] = value;
					}
				}
			}
			return styles;
		}

		/// <summary>
		/// Generates a CSS style attribute string from a dictionary of property-value pairs.
		/// </summary>
		private string GenerateStyleAttributeValue( Dictionary<string, string> styles )
		{
			if ( styles == null || styles.Count == 0 )
			{
				return string.Empty;
			}

			var sb = new StringBuilder();

			foreach ( var kvp in styles )
			{
				if ( !string.IsNullOrWhiteSpace( kvp.Value ) )
				{
					sb.Append( kvp.Key.ToLowerInvariant() );
					sb.Append( ": " );
					sb.Append( kvp.Value.Trim() );
					sb.Append( "; " );
				}
			}

			if ( sb.Length > 0 )
			{
				sb.Length -= 1; // Remove trailing space
			}

			return sb.ToString();
		}

		// Parses CSS length (px, %) string
		private Length? ParseLength( string value )
		{
			if ( string.IsNullOrEmpty( value ) )
				return null;

			return Length.Parse( value );
		}

		// Parses CSS color string (#rgb, #rrggbb, rgb(), rgba())
		private Color ParseColor( string colorValue )
		{
			if ( string.IsNullOrEmpty( colorValue ) )
				return Color.White;

			if ( Color.TryParse( colorValue, out var color ) )
				return color;

			return Color.White;
		}

		/// <summary>
		/// Converts a color to CSS hex format
		/// </summary>
		private string ColorToHex( Color color )
		{
			if ( color.a < 1.0f )
			{
				return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}{(int)(color.a * 255):X2}";
			}
			else
			{
				return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
			}
		}
	}
}
