using Editor;
using Sandbox.UI;
using System;
using System.Linq;

namespace XGUI.XGUIEditor;

/// <summary>
/// A custom inspector for UI panels specifically for the XGUI designer
/// </summary>
public class PanelInspector : Widget
{
	// The panel being inspected
	private Panel _targetPanel;

	// Sections of the inspector
	private Widget _generalSection;
	private Widget _styleSection;
	private Widget _eventsSection;
	private Widget _xguiSection;


	public PanelInspector( Widget parent = null ) : base( parent )
	{
		Layout = Layout.Flow();
		Layout.Spacing = 5;
		Layout.Margin = 10;



		CreateSections();
	}

	/// <summary>
	/// Sets the target panel to inspect
	/// </summary>
	public void SetTarget( Panel panel )
	{
		_targetPanel = panel;
		Rebuild();
	}

	/// <summary>
	/// Create the different sections of the inspector
	/// </summary>
	private void CreateSections()
	{
		// Create expandable sections
		_generalSection = AddSection( "General", "description", true );
		_styleSection = AddSection( "Style", "brush", true );
		_eventsSection = AddSection( "Events", "event", false );
		_xguiSection = AddSection( "XGUI", "extension", true );
	}

	/// <summary>
	/// Creates a collapsible section with a header
	/// </summary>
	private Widget AddSection( string title, string icon, bool initiallyExpanded )
	{
		var expandGroup = new ExpandGroup( this );
		expandGroup.StateCookieName = $"PanelInspector.{title}";
		expandGroup.Icon = icon;
		expandGroup.Title = title;
		expandGroup.SetOpenState( initiallyExpanded );

		// Create content container
		var container = new Widget( null );
		container.Layout = Layout.Column();
		container.Layout.Spacing = 4;
		container.Layout.Margin = new Margin( 8, 8, 8, 8 );
		container.MinimumSize = new Vector2( 0, 10 );

		expandGroup.SetWidget( container );
		Layout.Add( expandGroup );

		return container;
	}

	/// <summary>
	/// Rebuilds the inspector with the current target panel
	/// </summary>
	public void Rebuild()
	{
		if ( _targetPanel == null )
		{
			ClearAllSections();
			return;
		}

		RebuildGeneralSection();
		RebuildStyleSection();
		RebuildEventsSection();
		RebuildXguiSection();
	}

	/// <summary>
	/// Clears all sections of the inspector
	/// </summary>
	private void ClearAllSections()
	{
		_generalSection.Layout.Clear( true );
		_styleSection.Layout.Clear( true );
		_eventsSection.Layout.Clear( true );
		_xguiSection.Layout.Clear( true );
	}

	/// <summary>
	/// Rebuilds the general section with basic panel properties
	/// </summary>
	private void RebuildGeneralSection()
	{
		var layout = _generalSection.Layout;
		layout.Clear( true );

		// Add panel type
		AddPropertyLabel( layout, "Type", _targetPanel.GetType().Name );

		// Add panel class list
		if ( _targetPanel.Classes != null && _targetPanel.Classes.Count() > 0 )
		{
			AddPropertyLabel( layout, "Classes", string.Join( " ", _targetPanel.Classes ) );
		}

		// Name property if available (might be via reflection)
		var nameProperty = _targetPanel.GetType().GetProperty( "Name" );
		if ( nameProperty != null )
		{
			string name = nameProperty.GetValue( _targetPanel ) as string;
			if ( !string.IsNullOrEmpty( name ) )
			{
				AddPropertyEditor( layout, "Name", name, ( value ) =>
				{
					nameProperty.SetValue( _targetPanel, value );
				} );
			}
		}

		// Text property for controls that have it
		if ( _targetPanel is Sandbox.UI.Button button )
		{
			AddPropertyEditor( layout, "Text", button.Text, ( value ) =>
			{
				button.Text = value;
			} );
		}
		else if ( _targetPanel is Sandbox.UI.Label label )
		{
			AddPropertyEditor( layout, "Text", label.Text, ( value ) =>
			{
				label.Text = value;
			} );
		}
		else if ( _targetPanel is XGUI.CheckBox checkbox )
		{
			AddPropertyEditor( layout, "Label", checkbox.LabelText, ( value ) =>
			{
				checkbox.LabelText = value;
			} );
			AddCheckboxProperty( layout, "Checked", checkbox.Checked, ( value ) =>
			{
				checkbox.Checked = value;
			} );
		}
		else if ( _targetPanel is XGUI.GroupBox groupBox )
		{
			AddPropertyEditor( layout, "Title", groupBox.Title, ( value ) =>
			{
				groupBox.Title = value;
			} );
		}
	}

	/// <summary>
	/// Rebuilds the style section with styling properties
	/// </summary>
	private void RebuildStyleSection()
	{
		var layout = _styleSection.Layout;
		layout.Clear( true );

		// Size properties
		var sizeGroup = AddPropertyGroup( layout, "Size" );

		AddFloatPropertyEditor( sizeGroup, "Width", _targetPanel.ComputedStyle.Width.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.Width = value;
		} );

		AddFloatPropertyEditor( sizeGroup, "Height", _targetPanel.ComputedStyle.Height.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.Height = value;
		} );

		// Margin properties
		var marginGroup = AddPropertyGroup( layout, "Margin" );

		AddFloatPropertyEditor( marginGroup, "Top", _targetPanel.ComputedStyle.MarginTop.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.MarginTop = value;
		} );

		AddFloatPropertyEditor( marginGroup, "Right", _targetPanel.ComputedStyle.MarginRight.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.MarginRight = value;
		} );

		AddFloatPropertyEditor( marginGroup, "Bottom", _targetPanel.ComputedStyle.MarginBottom.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.MarginBottom = value;
		} );

		AddFloatPropertyEditor( marginGroup, "Left", _targetPanel.ComputedStyle.MarginLeft.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.MarginLeft = value;
		} );

		// Padding properties
		var paddingGroup = AddPropertyGroup( layout, "Padding" );

		AddFloatPropertyEditor( paddingGroup, "Top", _targetPanel.ComputedStyle.PaddingTop.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.PaddingTop = value;
		} );

		AddFloatPropertyEditor( paddingGroup, "Right", _targetPanel.ComputedStyle.PaddingRight.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.PaddingRight = value;
		} );

		AddFloatPropertyEditor( paddingGroup, "Bottom", _targetPanel.ComputedStyle.PaddingBottom.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.PaddingBottom = value;
		} );

		AddFloatPropertyEditor( paddingGroup, "Left", _targetPanel.ComputedStyle.PaddingLeft.GetValueOrDefault().Value, ( value ) =>
		{
			_targetPanel.Style.PaddingLeft = value;
		} );

		// Color properties
		var colorGroup = AddPropertyGroup( layout, "Colors" );

		AddColorPropertyEditor( colorGroup, "Background", _targetPanel.ComputedStyle.BackgroundColor.GetValueOrDefault(), ( value ) =>
		{
			_targetPanel.Style.BackgroundColor = value;
		} );

		if ( _targetPanel.Style.FontColor.HasValue )
		{
			AddColorPropertyEditor( colorGroup, "Font", _targetPanel.Style.FontColor.Value, ( value ) =>
			{
				_targetPanel.Style.FontColor = value;
			} );
		}

		// Flex layout
		if ( _targetPanel.HasClass( "self-layout" ) )
		{
			var flexGroup = AddPropertyGroup( layout, "Layout" );

			string[] layoutOptions = { "row", "column" };
			string currentLayout = _targetPanel.HasClass( "self-layout-row" ) ? "row" :
								  _targetPanel.HasClass( "self-layout-column" ) ? "column" : "";

			AddDropdownProperty( flexGroup, "Direction", layoutOptions, currentLayout, ( value ) =>
			{
				_targetPanel.SetClass( "self-layout-row", value == "row" );
				_targetPanel.SetClass( "self-layout-column", value == "column" );
			} );
		}

		// Pseudo class state
		var pseudoGroup = AddPropertyGroup( layout, "State" );

		AddCheckboxProperty( pseudoGroup, ":hover", (_targetPanel.PseudoClass & PseudoClass.Hover) != 0, ( value ) =>
		{
			if ( value )
				_targetPanel.PseudoClass |= PseudoClass.Hover;
			else
				_targetPanel.PseudoClass &= ~PseudoClass.Hover;
		} );

		AddCheckboxProperty( pseudoGroup, ":active", (_targetPanel.PseudoClass & PseudoClass.Active) != 0, ( value ) =>
		{
			if ( value )
				_targetPanel.PseudoClass |= PseudoClass.Active;
			else
				_targetPanel.PseudoClass &= ~PseudoClass.Active;
		} );

		AddCheckboxProperty( pseudoGroup, ":focus", (_targetPanel.PseudoClass & PseudoClass.Focus) != 0, ( value ) =>
		{
			if ( value )
				_targetPanel.PseudoClass |= PseudoClass.Focus;
			else
				_targetPanel.PseudoClass &= ~PseudoClass.Focus;
		} );
	}

	/// <summary>
	/// Rebuilds the events section with event handlers
	/// </summary>
	private void RebuildEventsSection()
	{
		var layout = _eventsSection.Layout;
		layout.Clear( true );

		// For basic demonstration, just add a label
		layout.Add( new Editor.Label( "Events editing not implemented yet" ) );
	}

	/// <summary>
	/// Rebuilds the XGUI-specific section
	/// </summary>
	private void RebuildXguiSection()
	{
		var layout = _xguiSection.Layout;
		layout.Clear( true );

		// Add XGUI theme selection
		string[] themeOptions = { "Computer95", "Computer7", "ComputerXP", "Computer11" };
		string currentTheme = "Computer95"; // Get the current theme

		AddDropdownProperty( layout, "Theme", themeOptions, currentTheme, ( value ) =>
		{
			// Apply selected theme
			Log.Info( $"Setting theme to: {value}" );
			// Implementation to change theme would go here
		} );

		// Special XGUI-specific properties based on panel type
		/*		if ( _targetPanel is XGUI.Elements.IXGUIElement xguiElement )
				{
					layout.AddSeparator();
					layout.Add( new Editor.Label( "XGUI Element Properties" ) );

					// Add element-specific properties here
					// This would need to be extended for different XGUI elements
				}*/
	}

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
}
