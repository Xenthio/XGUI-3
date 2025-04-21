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
		public void SetTarget( Panel panel, MarkupNode node )
		{
			_targetPanel = panel;
			_targetNode = node;
			Rebuild();
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
			// (Identical to previous implementation)
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

		public void Rebuild()
		{
			ClearAllSections();
			if ( _targetNode == null || _targetNode.Type != NodeType.Element )
			{
				return; // Nothing to inspect or not an element
			}

			RebuildGeneralSection();
			RebuildStyleSection();
			// RebuildEventsSection();
			// RebuildXguiSection();
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
			AddPropertyEditor( layout, "Classes", currentClasses, ( value ) =>
			{
				value = value.Trim(); // Clean up input
				_targetNode.Attributes["class"] = value; // Update source node data
														 // Update live panel preview
				if ( _targetPanel != null ) UpdatePanelClasses( _targetPanel, value );
				OnPropertyChanged?.Invoke( _targetNode, "class", value ); // Notify designer
			} );

			// ID Attribute (Editable) - Common HTML attribute
			string currentId = _targetNode.Attributes.GetValueOrDefault( "id", "" );
			AddPropertyEditor( layout, "ID", currentId, ( value ) =>
			{
				_targetNode.Attributes["id"] = value.Trim();
				// Apply to live panel? Sandbox Panels might not have a direct ID property. Store in Tags?
				// if (_targetPanel != null) _targetPanel.Tags.Set("id", value.Trim());
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
					AddPropertyEditor( layout, "Text", innerText, ( value ) =>
					{
						UpdateOrCreateChildTextNode( node, value );
						if ( panel is Sandbox.UI.Button b ) b.Text = value;
						else if ( panel is Sandbox.UI.Label l ) l.Text = value;
						OnPropertyChanged?.Invoke( node, "innertext", value ); // Use "innertext" to signify content change
					} );
					break;

				case "check":
					AddPropertyEditor( layout, "Label", innerText, ( value ) =>
					{
						UpdateOrCreateChildTextNode( node, value );
						if ( panel is XGUI.CheckBox cb ) cb.LabelText = value;
						OnPropertyChanged?.Invoke( node, "innertext", value );
					} );
					bool isChecked = node.Attributes.ContainsKey( "checked" );
					AddCheckboxProperty( layout, "Checked", isChecked, ( value ) =>
					{
						if ( value ) node.Attributes["checked"] = null; // Valueless
						else node.Attributes.Remove( "checked" );
						if ( panel is XGUI.CheckBox cb ) cb.Checked = value;
						OnPropertyChanged?.Invoke( node, "checked", value );
					} );
					break;

				case "groupbox":
					string titleAttr = node.Attributes.GetValueOrDefault( "title", "" );
					AddPropertyEditor( layout, "Title", titleAttr, ( value ) =>
					{
						node.Attributes["title"] = value;
						if ( panel is XGUI.GroupBox gb ) gb.Title = value;
						OnPropertyChanged?.Invoke( node, "title", value );
					} );
					break;

				case "sliderscale":
					string minAttr = node.Attributes.GetValueOrDefault( "min", "0" );
					string maxAttr = node.Attributes.GetValueOrDefault( "max", "100" );
					AddPropertyEditor( layout, "Min", minAttr, ( value ) =>
					{
						node.Attributes["min"] = value;
						if ( panel is XGUI.SliderScale sl && float.TryParse( value, CultureInfo.InvariantCulture, out var v ) ) sl.MinValue = v;
						OnPropertyChanged?.Invoke( node, "min", value );
					} );
					AddPropertyEditor( layout, "Max", maxAttr, ( value ) =>
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
				AddPropertyEditor( thislayout, propName.Replace( "-", " " ).ToTitleCase(), initialValue, ( value ) =>
				{ // Make name user-friendly
					value = value.Trim();
					// ... update currentStyles ...
					string newStyleAttr = GenerateStyleAttributeValue( currentStyles );

					// *** CRITICAL POINT: Ensure _targetNode is valid here ***
					if ( _targetNode == null )
					{
						Log.Error( $"Inspector: _targetNode is NULL when trying to update style '{propName}'!" );
						return;
					}
					Log.Info( $"  _targetNode appears valid: <{_targetNode.TagName}>" ); // Keep this check
																						 // *** Log the node being passed ***
					Log.Info( $"Inspector: Invoking OnPropertyChanged for node <{_targetNode?.TagName ?? "NULL"}>, prop 'style'" ); // Corrected log


					_targetNode.Attributes["style"] = newStyleAttr; // Update node attribute locally *before* notifying

					// Update live panel preview
					UpdatePanelSingleStyle( _targetPanel, propName, value );

					// *** Ensure correct node is passed ***
					OnPropertyChanged?.Invoke( _targetNode, "style", newStyleAttr ); // Notify designer
				} );
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
			AddStyleEditor( colourGroup, "color", currentStyles.GetValueOrDefault( "color", "" ) ); // Font color
			var textGroup = AddPropertyGroup( layout, "Text" );
			AddStyleEditor( textGroup, "font-size", currentStyles.GetValueOrDefault( "font-size", "" ) );
			// Add more styles as needed (flex-direction, etc.)


			// Pseudo class state (uses live panel, read-only for source)
			var pseudoGroup = AddPropertyGroup( layout, "Live State (Preview)" );
			if ( _targetPanel != null )
			{
				var pseudoStates = new[] { "hover", "active", "focus" }; // Add more if needed
				foreach ( var state in pseudoStates )
				{
					AddCheckboxProperty( pseudoGroup, state.ToTitleCase(), _targetPanel.HasClass( state ), ( value ) =>
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
		/// Adds a property editor with a text field
		/// </summary>
		private void AddPropertyEditor( Layout layout, string name, string value, Action<string> onChange )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( name ) { FixedWidth = 100 } );

			var editor = row.Add( new LineEdit() { Text = value }, 1 );
			editor.TextChanged += ( s ) => onChange( editor.Text );
		}

		/// <summary>
		/// Adds a numeric property editor
		/// </summary>
		private void AddFloatPropertyEditor( Layout layout, string name, float value, Action<float> onChange )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( name ) { FixedWidth = 100 } );

			var editor = row.Add( new Editor.LineEdit( null ) { Value = value.ToString() }, 1 );
			editor.TextChanged += ( val ) => onChange( float.Parse( val ) );
		}

		/// <summary>
		/// Adds a color property editor
		/// </summary>
		private void AddColorPropertyEditor( Layout layout, string name, Color value, Action<Color> onChange )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( name ) { FixedWidth = 100 } );

			var colorPicker = row.Add( new Editor.ColorPicker( null ) { Value = value, }, 1 );
			colorPicker.ValueChanged = ( val ) => onChange( val );
		}

		/// <summary>
		/// Adds a checkbox property
		/// </summary>
		private void AddCheckboxProperty( Layout layout, string name, bool value, Action<bool> onChange )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( name ) { FixedWidth = 100 } );

			var checkbox = row.Add( new Editor.Checkbox() { Value = value } );
			checkbox.StateChanged = ( state ) => onChange( checkbox.Value );
		}

		/// <summary>
		/// Adds a dropdown property
		/// </summary>
		private void AddDropdownProperty( Layout layout, string name, string[] options, string currentValue, Action<string> onChange )
		{
			var row = layout.AddRow();
			row.Add( new Editor.Label( name ) { FixedWidth = 100 } );

			var dropdown = row.Add( new Editor.ComboBox(), 1 );
			foreach ( var option in options )
			{
				dropdown.AddItem( option );
			}

			dropdown.CurrentText = currentValue;
			dropdown.TextChanged += () => onChange( dropdown.CurrentText );
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
			// Use OrdinalIgnoreCase so "color" and "Color" are treated the same
			var styles = new Dictionary<string, string>( System.StringComparer.OrdinalIgnoreCase );
			if ( string.IsNullOrWhiteSpace( styleString ) )
			{
				return styles;
			}

			// Split by semicolon, removing empty entries that might result from trailing semicolons
			var declarations = styleString.Split( ';', System.StringSplitOptions.RemoveEmptyEntries );

			foreach ( var declaration in declarations )
			{
				// Split only on the first colon to handle values like url("data:image...")
				var parts = declaration.Split( ':', 2 );
				if ( parts.Length == 2 )
				{
					string property = parts[0].Trim();
					string value = parts[1].Trim();

					// Only add if the property name is not empty
					if ( !string.IsNullOrEmpty( property ) )
					{
						styles[property] = value; // Add or overwrite using the dictionary's comparer
					}
				}
				// Ignore parts that don't contain a colon or are otherwise malformed
			}
			return styles;
		}

		/// <summary>
		/// Generates a CSS style attribute string from a dictionary of property-value pairs.
		/// Ensures properties are lowercase and followed by a semicolon and space.
		/// Filters out properties with null or whitespace values.
		/// </summary>
		private string GenerateStyleAttributeValue( Dictionary<string, string> styles )
		{
			if ( styles == null || styles.Count == 0 )
			{
				return string.Empty;
			}

			// Use StringBuilder for efficient string construction
			var sb = new StringBuilder();

			foreach ( var kvp in styles )
			{
				// Only include properties that have a non-whitespace value
				if ( !string.IsNullOrWhiteSpace( kvp.Value ) )
				{
					// Ensure property name is lowercase for consistency
					sb.Append( kvp.Key.ToLowerInvariant() );
					sb.Append( ": " );
					sb.Append( kvp.Value.Trim() ); // Trim value just in case
					sb.Append( "; " ); // Add semicolon and space separator
				}
			}

			// Remove the trailing space if any styles were added
			if ( sb.Length > 0 )
			{
				sb.Length -= 1; // Remove the last space
								// Optionally remove the last semicolon too? CSS usually tolerates it.
								// if (sb.Length > 0 && sb[sb.Length - 1] == ';') sb.Length--;
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
						// Add other style properties
				}

				var node = OwnerDesigner.LookupNodeByPanel( panel );
				node.TryModifyStyle( propertyName, stringValue ); // Update the source node
				OwnerDesigner.ForceUpdate( false );
				panel.Style.Dirty(); // Mark style as dirty if needed by UI framework

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
			// Simple clear and add
			foreach ( var cls in panel.Class.ToList() ) panel.RemoveClass( cls );
			foreach ( var cls in classString.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) ) panel.AddClass( cls );
		}

		// Helper to update or create a direct child text node within a MarkupNode
		private void UpdateOrCreateChildTextNode( MarkupNode parentNode, string text ) { /* (Identical to previous) */ }

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
