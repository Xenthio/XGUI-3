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
	/// Tracks a single editor in the inspector panel for incremental updates
	/// </summary>
	public class EditorReference
	{
		// The property/attribute name this editor controls
		public string PropertyName { get; set; }

		// CSS property name if applicable (for style properties)
		public string CssPropertyName { get; set; }

		// The UI control for this editor
		public Widget EditorControl { get; set; }

		// Function to update the editor's value without triggering change events
		public Action<string> UpdateValue { get; set; }

		// True if this editor handles a CSS style property
		public bool IsStyleProperty { get; set; }

		public EditorReference( string propertyName, Widget control, Action<string> updateFn, bool isStyle = false )
		{
			PropertyName = propertyName;
			EditorControl = control;
			UpdateValue = updateFn;
			IsStyleProperty = isStyle;

			// For style properties, store the CSS property name as well
			if ( isStyle )
			{
				CssPropertyName = propertyName;
			}
		}
	}

	/// <summary>
	/// Inspects properties of a MarkupNode and provides UI for editing,
	/// interacting with the corresponding live Panel for previews.
	/// </summary>
	public class PanelInspector : Widget
	{
		public XGUIDesigner OwnerDesigner;
		// The live panel (for previews, computed styles) - may be null
		private Panel _targetPanel;
		// The source node from the parsed markup - primary target for edits
		private MarkupNode _targetNode;

		// Sections
		private Widget _generalSection;
		private Widget _styleSection;
		// private Widget _eventsSection; // Add later if needed
		// private Widget _xguiSection; // Add later if needed

		// Tracks all created editors for efficient updates
		private Dictionary<string, EditorReference> _generalEditors = new Dictionary<string, EditorReference>();
		private Dictionary<string, EditorReference> _styleEditors = new Dictionary<string, EditorReference>( StringComparer.OrdinalIgnoreCase );

		/// <summary>
		/// Action triggered when a property potentially changes a source attribute or content.
		/// Passes the MarkupNode, the property/attribute name, and its new value.
		/// </summary>
		public Action<MarkupNode, string, object> OnPropertyChanged;

		public PanelInspector( Widget parent = null ) : base( parent )
		{
			Layout = Layout.Flow(); // Use Flow layout for automatic wrapping? Or Column? Let's stick with Flow.
			Layout.Spacing = 5;
			Layout.Margin = 10;
			CreateSections();
		}

		/// <summary>
		/// Sets the target node (from markup) and the corresponding live panel (if any).
		/// </summary>
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

		private void CreateSections()
		{
			_generalSection = AddSection( "General", "description", true );
			_styleSection = AddSection( "Style", "brush", true );
			// _eventsSection = AddSection( "Events", "event", false );
			// _xguiSection = AddSection( "XGUI", "extension", true );
		}

		private Widget AddSection( string title, string icon, bool initiallyExpanded )
		{
			var expandGroup = new ExpandGroup( this );
			expandGroup.StateCookieName = $"PanelInspector.{title}";
			expandGroup.Icon = icon;
			expandGroup.Title = title;
			expandGroup.SetOpenState( initiallyExpanded );
			var container = new Widget( null ) { Layout = Layout.Column() };
			container.Layout.Spacing = 4;
			container.Layout.Margin = new Margin( 8 );
			container.MinimumSize = new Vector2( 0, 10 ); // Ensure it doesn't collapse completely
			expandGroup.SetWidget( container );
			Layout.Add( expandGroup );
			return container;
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
				// RebuildEventsSection();
				// RebuildXguiSection();
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

		/// <summary>
		/// Updates all properties incrementally without rebuilding UI
		/// </summary>
		private void UpdatePropertiesIncremental()
		{
			if ( _targetNode == null ) return;

			// Update general properties (class, id, tag-specific)
			foreach ( var editor in _generalEditors )
			{
				string propertyName = editor.Key;
				string value = "";

				// Get current value from the node/panel
				switch ( propertyName )
				{
					case "class":
						value = _targetNode.Attributes.GetValueOrDefault( "class", "" );
						break;
					case "id":
						value = _targetNode.Attributes.GetValueOrDefault( "id", "" );
						break;
					case "innertext":
						value = _targetNode.Children.FirstOrDefault( c => c.Type == NodeType.Text )?.TextContent ?? "";
						break;
					default:
						// For other attributes (title, min, max, etc.)
						value = _targetNode.Attributes.GetValueOrDefault( propertyName, "" );
						break;
				}

				// Update the editor without triggering change events
				editor.Value.UpdateValue( value );
			}

			// Update style properties
			string styleAttributeValue = _targetNode.Attributes.GetValueOrDefault( "style", "" );
			var currentStyles = ParseStyleAttribute( styleAttributeValue );

			foreach ( var editor in _styleEditors )
			{
				string cssProperty = editor.Key;
				string value = currentStyles.GetValueOrDefault( cssProperty, "" );

				// Update the editor without triggering change events
				editor.Value.UpdateValue( value );
			}
		}

		/// <summary>
		/// Updates style properties without rebuilding UI
		/// </summary>
		private void UpdateStylePropertiesIncremental()
		{
			if ( _targetNode == null || _styleSection == null ) return;

			string styleAttributeValue = _targetNode.Attributes.GetValueOrDefault( "style", "" );
			var currentStyles = ParseStyleAttribute( styleAttributeValue );

			// Update each style property editor
			foreach ( var editor in _styleEditors )
			{
				string cssProperty = editor.Key;
				string value = currentStyles.GetValueOrDefault( cssProperty, "" );

				// Update the editor without triggering change events
				editor.Value.UpdateValue( value );
			}
		}

		private void ClearAllSections()
		{
			_generalSection?.Layout.Clear( true );
			_styleSection?.Layout.Clear( true );
			// _eventsSection?.Layout.Clear( true );
			// _xguiSection?.Layout.Clear( true );
		}

		private void RebuildGeneralSection()
		{
			var layout = _generalSection.Layout;
			layout.Clear( true );
			if ( _targetNode == null || _targetNode.Type != NodeType.Element ) return; // Guard

			// Type (read-only)
			AddPropertyLabel( layout, "Type", _targetNode.TagName );

			// Classes (Editable)
			string currentClasses = _targetNode.Attributes.GetValueOrDefault( "class", "" );
			AddPropertyEditor( layout, "Classes", "class", currentClasses, ( value ) =>
			{
				value = value.Trim(); // Clean up input
				_targetNode.Attributes["class"] = value; // Update source node data
														 // Update live panel preview
				if ( _targetPanel != null ) UpdatePanelClasses( _targetPanel, value );
				OnPropertyChanged?.Invoke( _targetNode, "class", value ); // Notify designer
			} );

			// ID Attribute (Editable) - Common HTML attribute
			string currentId = _targetNode.Attributes.GetValueOrDefault( "id", "" );
			AddPropertyEditor( layout, "ID", "id", currentId, ( value ) =>
			{
				_targetNode.Attributes["id"] = value.Trim();
				OnPropertyChanged?.Invoke( _targetNode, "id", value.Trim() );
			} );

			// Content / Text / Title Properties (specific to tag type)
			HandleContentProperties( layout, _targetNode, _targetPanel );
		}

		private void HandleContentProperties( Layout layout, MarkupNode node, Panel panel )
		{
			string tag = node.TagName.ToLowerInvariant();
			string innerText = node.Children.FirstOrDefault( c => c.Type == NodeType.Text && !string.IsNullOrWhiteSpace( c.TextContent ) )?.TextContent ?? "";

			switch ( tag )
			{
				case "button":
				case "label":
					AddPropertyEditor( layout, "Text", "innertext", innerText, ( value ) =>
					{
						UpdateOrCreateChildTextNode( node, value );
						if ( panel is Sandbox.UI.Button b ) b.Text = value;
						else if ( panel is Sandbox.UI.Label l ) l.Text = value;
						OnPropertyChanged?.Invoke( node, "innertext", value ); // Use "innertext" to signify content change
					} );
					break;

				case "check":
					AddPropertyEditor( layout, "Label", "innertext", innerText, ( value ) =>
					{
						UpdateOrCreateChildTextNode( node, value );
						if ( panel is XGUI.CheckBox cb ) cb.LabelText = value;
						OnPropertyChanged?.Invoke( node, "innertext", value );
					} );
					bool isChecked = node.Attributes.ContainsKey( "checked" );
					AddCheckboxProperty( layout, "Checked", "checked", isChecked, ( value ) =>
					{
						if ( value ) node.Attributes["checked"] = null; // Valueless
						else node.Attributes.Remove( "checked" );
						if ( panel is XGUI.CheckBox cb ) cb.Checked = value;
						OnPropertyChanged?.Invoke( node, "checked", value );
					} );
					break;

				case "groupbox":
					string titleAttr = node.Attributes.GetValueOrDefault( "title", "" );
					AddPropertyEditor( layout, "Title", "title", titleAttr, ( value ) =>
					{
						node.Attributes["title"] = value;
						if ( panel is XGUI.GroupBox gb ) gb.Title = value;
						OnPropertyChanged?.Invoke( node, "title", value );
					} );
					break;

				case "sliderscale":
					string minAttr = node.Attributes.GetValueOrDefault( "min", "0" );
					string maxAttr = node.Attributes.GetValueOrDefault( "max", "100" );
					AddPropertyEditor( layout, "Min", "min", minAttr, ( value ) =>
					{
						node.Attributes["min"] = value;
						if ( panel is XGUI.SliderScale sl && float.TryParse( value, CultureInfo.InvariantCulture, out var v ) ) sl.MinValue = v;
						OnPropertyChanged?.Invoke( node, "min", value );
					} );
					AddPropertyEditor( layout, "Max", "max", maxAttr, ( value ) =>
					{
						node.Attributes["max"] = value;
						if ( panel is XGUI.SliderScale sl && float.TryParse( value, CultureInfo.InvariantCulture, out var v ) ) sl.MaxValue = v;
						OnPropertyChanged?.Invoke( node, "max", value );
					} );
					// Add 'step', 'value' if needed
					break;
					// Add cases for other specific elements (TextEntry placeholder?, Combobox default?)
			}
		}

		private void RebuildStyleSection()
		{
			var layout = _styleSection.Layout;
			layout.Clear( true );
			if ( _targetNode == null || _targetNode.Type != NodeType.Element ) return; // Guard

			string styleAttributeValue = _targetNode.Attributes.GetValueOrDefault( "style", "" );
			var currentStyles = ParseStyleAttribute( styleAttributeValue ); // Parse into dictionary

			// Helper Action to create style editors consistently
			Action<Layout, string, string> AddStyleEditor = ( thislayout, propName, initialValue ) =>
			{
				string displayName = propName.Replace( "-", " " ).ToTitleCase();
				AddPropertyEditor( thislayout, displayName, propName, initialValue, ( value ) =>
				{
					value = value.Trim();
					_targetNode.TryModifyStyle( propName, value ); // Update source node data
					OwnerDesigner.ForceUpdate( false );
				}, true ); // true = isStyle
			};

			// Add editors for common styles
			// Use GetValueOrDefault to handle missing properties gracefully
			var sizeGroup = AddPropertyGroup( layout, "Size" );
			AddStyleEditor( sizeGroup, "width", currentStyles.GetValueOrDefault( "width", "" ) );
			AddStyleEditor( sizeGroup, "height", currentStyles.GetValueOrDefault( "height", "" ) );

			var positionGroup = AddPropertyGroup( layout, "Position" );
			AddStyleEditor( positionGroup, "left", currentStyles.GetValueOrDefault( "left", "" ) );
			AddStyleEditor( positionGroup, "top", currentStyles.GetValueOrDefault( "top", "" ) );
			AddStyleEditor( positionGroup, "position", currentStyles.GetValueOrDefault( "position", "" ) );

			var marginGroup = AddPropertyGroup( layout, "Margin" );
			AddStyleEditor( marginGroup, "margin-top", currentStyles.GetValueOrDefault( "margin-top", "" ) );
			AddStyleEditor( marginGroup, "margin-right", currentStyles.GetValueOrDefault( "margin-right", "" ) );
			AddStyleEditor( marginGroup, "margin-bottom", currentStyles.GetValueOrDefault( "margin-bottom", "" ) );
			AddStyleEditor( marginGroup, "margin-left", currentStyles.GetValueOrDefault( "margin-left", "" ) );

			var paddingGroup = AddPropertyGroup( layout, "Padding" );
			AddStyleEditor( paddingGroup, "padding-top", currentStyles.GetValueOrDefault( "padding-top", "" ) );
			AddStyleEditor( paddingGroup, "padding-right", currentStyles.GetValueOrDefault( "padding-right", "" ) );
			AddStyleEditor( paddingGroup, "padding-bottom", currentStyles.GetValueOrDefault( "padding-bottom", "" ) );
			AddStyleEditor( paddingGroup, "padding-left", currentStyles.GetValueOrDefault( "padding-left", "" ) );

			var colourGroup = AddPropertyGroup( layout, "Colour" );
			AddStyleEditor( colourGroup, "background-color", currentStyles.GetValueOrDefault( "background-color", "" ) );
			AddStyleEditor( colourGroup, "color", currentStyles.GetValueOrDefault( "color", "" ) );

			var textGroup = AddPropertyGroup( layout, "Text" );
			AddStyleEditor( textGroup, "font-size", currentStyles.GetValueOrDefault( "font-size", "" ) );
			// Add more styles as needed (flex-direction, etc.)

			// Pseudo class state (uses live panel, read-only for source)
			var pseudoGroup = AddPropertyGroup( layout, "Live State (Preview)" );
			if ( _targetPanel != null )
			{
				var pseudoStates = new[] { "hover", "active", "focus" };
				foreach ( var state in pseudoStates )
				{
					AddCheckboxProperty( pseudoGroup, state.ToTitleCase(), state, _targetPanel.HasClass( state ), ( value ) =>
					{
						if ( value ) _targetPanel.AddClass( state );
						else _targetPanel.RemoveClass( state );
					} );
				}
				pseudoGroup.Add( new Editor.Label( "Click to toggle states" ) );
			}
			else
			{
				pseudoGroup.Add( new Editor.Label( "No live panel for state preview" ) );
			}
		}

		//---------------------------------------------------------------------
		// Helpers for Inspector UI & Data Handling
		//---------------------------------------------------------------------

		/// <summary>
		/// Adds a read-only property label
		/// </summary>
		private void AddPropertyLabel( Layout layout, string name, string value )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( name ) { FixedWidth = 100 } );
			row.Add( new Editor.Label( value ) { }, 1 );
		}

		/// <summary>
		/// Adds a property editor with a text field and registers it for incremental updates
		/// </summary>
		/// <summary>
		/// Adds a property editor with a text field and registers it for incremental updates
		/// </summary>
		private void AddPropertyEditor( Layout layout, string displayName, string propertyName, string value, Action<string> onChange, bool isStyle = false )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( displayName ) { FixedWidth = 100 } );

			var editor = row.Add( new LineEdit() { Text = value }, 1 );

			// Create update function to modify text without triggering events
			Action<string> updateFn = ( newValue ) =>
			{
				// We need to use a different approach since we can't directly set TextChanged to null
				// Store current text and set it manually
				editor.Text = newValue;
			};

			// Register normal change events
			editor.TextChanged += ( s ) => onChange( editor.Text );

			// Register this editor for future incremental updates
			var editorRef = new EditorReference( propertyName, editor, updateFn, isStyle );

			if ( isStyle )
			{
				_styleEditors[propertyName] = editorRef;
			}
			else
			{
				_generalEditors[propertyName] = editorRef;
			}
		}

		/// <summary>
		/// Adds a checkbox property and registers it for incremental updates
		/// </summary>
		private void AddCheckboxProperty( Layout layout, string displayName, string propertyName, bool value, Action<bool> onChange, bool isStyle = false )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( displayName ) { FixedWidth = 100 } );

			var checkbox = row.Add( new Editor.Checkbox() { Value = value } );

			// Create update function that won't trigger events
			Action<string> updateFn = ( newValue ) =>
			{
				// Directly update the value without trying to modify the event
				checkbox.Value = !string.IsNullOrEmpty( newValue ); // Convert string to bool
			};

			// Add normal change handler
			checkbox.StateChanged = ( state ) => onChange( checkbox.Value );

			// Register for incremental updates
			var editorRef = new EditorReference( propertyName, checkbox, updateFn, isStyle );

			if ( isStyle )
			{
				_styleEditors[propertyName] = editorRef;
			}
			else
			{
				_generalEditors[propertyName] = editorRef;
			}
		}


		/// <summary>
		/// Adds a dropdown property
		/// </summary>
		private void AddDropdownProperty( Layout layout, string displayName, string propertyName, string[] options, string currentValue, Action<string> onChange, bool isStyle = false )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( displayName ) { FixedWidth = 100 } );

			var dropdown = row.Add( new Editor.ComboBox(), 1 );
			foreach ( var option in options )
			{
				dropdown.AddItem( option );
			}

			dropdown.CurrentText = currentValue;

			// Create update function that doesn't trigger events
			Action<string> updateFn = ( newValue ) =>
			{
				// Directly set the value without trying to manipulate the event
				dropdown.CurrentText = newValue;
			};

			// Add normal change handler
			dropdown.TextChanged += () => onChange( dropdown.CurrentText );

			// Register for updates
			var editorRef = new EditorReference( propertyName, dropdown, updateFn, isStyle );

			if ( isStyle )
			{
				_styleEditors[propertyName] = editorRef;
			}
			else
			{
				_generalEditors[propertyName] = editorRef;
			}
		}

		/// <summary>
		/// Adds a property group with its own layout
		/// </summary>
		private Layout AddPropertyGroup( Layout parentLayout, string groupName )
		{
			var groupWidget = new Widget( null );
			groupWidget.Layout = Layout.Column();
			groupWidget.Layout.Spacing = 2;

			var header = new Editor.Label( groupName );
			header.SetStyles( "font-weight: bold; margin-top: 5px;" );

			groupWidget.Layout.Add( header );
			parentLayout.Add( groupWidget );

			return groupWidget.Layout;
		}

		private void AddPropertyTitle( Layout layout, string groupName )
		{
			var groupWidget = new Widget( null );
			groupWidget.Layout = Layout.Column();
			groupWidget.Layout.Spacing = 2;

			var header = new Editor.Label( groupName );
			header.SetStyles( "font-weight: bold; margin-top: 5px;" );
			groupWidget.Layout.Add( header );
			layout.Add( groupWidget );
		}

		/// <summary>
		/// Parses a CSS style attribute string (e.g., "width: 100px; color: red;")
		/// into a dictionary of property-value pairs.
		/// Uses OrdinalIgnoreCase comparer for property names.
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
				sb.Length -= 1;
			}

			return sb.ToString();
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

				var node = OwnerDesigner.LookupNodeByPanel( panel );
				node.TryModifyStyle( propertyName, stringValue );
				OwnerDesigner.ForceUpdate( false );
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

		// Parses CSS length (px, %) string
		private Length? ParseLength( string value ) { return Length.Parse( value ); }

		// Parses CSS color string (#rgb, #rrggbb, rgb(), rgba())
		private Color ParseColor( string colorValue ) { return Color.Parse( colorValue ).Value; }
	}

	// Helper extension for Title Case display name
	public static class StringExtensions
	{
		public static string ToTitleCase( this string str )
		{
			return CultureInfo.CurrentCulture.TextInfo.ToTitleCase( str.ToLower() );
		}
	}
}
