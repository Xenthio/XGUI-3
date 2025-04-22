using Editor;
using System;
using System.Globalization;
using System.Linq;

namespace XGUI.XGUIEditor
{
	/// <summary>
	/// Text editor with a simple line edit control
	/// </summary>
	public class TextPropertyEditor : PropertyEditor
	{
		private LineEdit _editor;
		private Widget _rootWidget;

		public TextPropertyEditor( string propertyName, string displayName, bool isStyle = false )
			: base( propertyName, displayName, isStyle )
		{
		}

		public override Widget CreateUI( Layout layout )
		{
			// Create a container widget for the editor
			_rootWidget = new Widget( null );
			_rootWidget.Layout = Layout.Row();
			_rootWidget.Layout.Margin = 0;
			_rootWidget.Layout.Spacing = 2;

			_rootWidget.Layout.Add( new Editor.Label( DisplayName ) { FixedWidth = 100 } );
			_editor = _rootWidget.Layout.Add( new LineEdit(), 1 );
			_editor.TextChanged += ( text ) => NotifyValueChanged( text );

			// Add the root widget to the parent layout
			layout.Add( _rootWidget );

			// Set the RootWidget property
			RootWidget = _rootWidget;
			return _rootWidget;
		}

		public override void SetValueSilently( string value )
		{
			if ( _editor != null )
			{
				_editor.Text = value ?? "";
			}
		}
	}

	/// <summary>
	/// Numeric editor for float values with unit handling
	/// </summary>
	public class FloatPropertyEditor : PropertyEditor
	{
		private LineEdit _editor;
		private string _unit = "px";
		private Widget _rootWidget;

		public FloatPropertyEditor( string propertyName, string displayName, bool isStyle = false, string unit = "px" )
			: base( propertyName, displayName, isStyle )
		{
			_unit = unit;
		}

		public override Widget CreateUI( Layout layout )
		{
			// Create a container widget for the editor
			_rootWidget = new Widget( null );
			_rootWidget.Layout = Layout.Row();
			_rootWidget.Layout.Margin = 0;
			_rootWidget.Layout.Spacing = 2;

			_rootWidget.Layout.Add( new Editor.Label( DisplayName ) { FixedWidth = 100 } );
			_editor = _rootWidget.Layout.Add( new LineEdit(), 1 );
			_editor.TextChanged += ( text ) =>
			{
				if ( float.TryParse( text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value ) )
				{
					NotifyValueChanged( value );
				}
			};

			// Add the root widget to the parent layout
			layout.Add( _rootWidget );

			// Set the RootWidget property
			RootWidget = _rootWidget;
			return _rootWidget;
		}

		public override void SetValueSilently( string value )
		{
			if ( _editor == null ) return;

			// If the value is empty/null, leave the editor blank instead of defaulting to zero
			if ( string.IsNullOrEmpty( value ) )
			{
				_editor.Text = "";
				return;
			}

			// Remove unit if present
			foreach ( var unit in new[] { "px", "%", "em", "rem", "vh", "vw" } )
			{
				if ( value.EndsWith( unit, StringComparison.OrdinalIgnoreCase ) )
				{
					value = value.Substring( 0, value.Length - unit.Length );
					break;
				}
			}

			// Parse and set value
			if ( float.TryParse( value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue ) )
			{
				_editor.Text = floatValue.ToString( CultureInfo.InvariantCulture );
			}
			else
			{
				// Leave blank if parsing fails instead of defaulting to zero
				_editor.Text = "";
			}
		}

		// Helper to get the formatted value with units
		public string GetFormattedValue()
		{
			if ( _editor == null || string.IsNullOrEmpty( _editor.Text ) ) return "";

			if ( float.TryParse( _editor.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value ) )
			{
				return value.ToString( CultureInfo.InvariantCulture ) + _unit;
			}

			return "";
		}
	}


	/// <summary>
	/// Color editor with a color picker
	/// </summary>
	public class ColorPropertyEditor : PropertyEditor
	{
		private ColorPicker _colorPicker;
		private Widget _rootWidget;
		private bool _hasSetInitialValue = false;

		public ColorPropertyEditor( string propertyName, string displayName, bool isStyle = false )
			: base( propertyName, displayName, isStyle )
		{
		}

		public override Widget CreateUI( Layout layout )
		{
			// Create a container widget for the editor
			_rootWidget = new Widget( null );
			_rootWidget.Layout = Layout.Row();
			_rootWidget.Layout.Margin = 0;
			_rootWidget.Layout.Spacing = 2;

			_rootWidget.Layout.Add( new Editor.Label( DisplayName ) { FixedWidth = 100 } );
			_colorPicker = _rootWidget.Layout.Add( new Editor.ColorPicker( null ), 1 );

			// Don't set a default initial color - leave as default from ColorPicker
			_hasSetInitialValue = false;

			_colorPicker.ValueChanged = ( color ) => NotifyValueChanged( color );

			// Add the root widget to the parent layout
			layout.Add( _rootWidget );

			// Set the RootWidget property
			RootWidget = _rootWidget;
			return _rootWidget;
		}

		public override void SetValueSilently( string cssColor )
		{
			if ( _colorPicker == null ) return;

			// If the value is empty and we've never set a value, don't do anything
			// This prevents overriding with a default value
			if ( string.IsNullOrEmpty( cssColor ) && !_hasSetInitialValue )
			{
				return;
			}

			if ( Color.TryParse( cssColor, out var color ) )
			{
				_colorPicker.Value = color;
				_hasSetInitialValue = true;
			}
		}

		// Helper to get CSS hex string
		public string GetHexString()
		{
			if ( _colorPicker == null || !_hasSetInitialValue ) return "";

			var color = _colorPicker.Value;
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

	/// <summary>
	/// Boolean editor with a checkbox
	/// </summary>
	public class BoolPropertyEditor : PropertyEditor
	{
		private Editor.Checkbox _checkbox;
		private Widget _rootWidget;

		public BoolPropertyEditor( string propertyName, string displayName, bool isStyle = false )
			: base( propertyName, displayName, isStyle )
		{
		}

		public override Widget CreateUI( Layout layout )
		{
			// Create a container widget for the editor
			_rootWidget = new Widget( null );
			_rootWidget.Layout = Layout.Row();
			_rootWidget.Layout.Margin = 0;
			_rootWidget.Layout.Spacing = 2;

			_rootWidget.Layout.Add( new Editor.Label( DisplayName ) { FixedWidth = 100 } );
			_checkbox = _rootWidget.Layout.Add( new Editor.Checkbox() );
			_checkbox.StateChanged = ( state ) => NotifyValueChanged( _checkbox.Value );

			// Add the root widget to the parent layout
			layout.Add( _rootWidget );

			// Set the RootWidget property
			RootWidget = _rootWidget;
			return _rootWidget;
		}

		public override void SetValueSilently( string value )
		{
			if ( _checkbox == null ) return;
			_checkbox.Value = !string.IsNullOrEmpty( value ) && value != "false" && value != "0";
		}
	}

	/// <summary>
	/// Dropdown editor with a list of options
	/// </summary>
	public class DropdownPropertyEditor : PropertyEditor
	{
		private Editor.ComboBox _dropdown;
		private Widget _rootWidget;
		private readonly string[] _options;

		public DropdownPropertyEditor( string propertyName, string displayName, string[] options, bool isStyle = false )
			: base( propertyName, displayName, isStyle )
		{
			_options = options;
		}

		public override Widget CreateUI( Layout layout )
		{
			// Create a container widget for the editor
			_rootWidget = new Widget( null );
			_rootWidget.Layout = Layout.Row();
			_rootWidget.Layout.Margin = 0;
			_rootWidget.Layout.Spacing = 2;

			_rootWidget.Layout.Add( new Editor.Label( DisplayName ) { FixedWidth = 100 } );
			_dropdown = _rootWidget.Layout.Add( new Editor.ComboBox(), 1 );

			foreach ( var option in _options )
			{
				_dropdown.AddItem( option );
			}

			_dropdown.TextChanged += () => NotifyValueChanged( _dropdown.CurrentText );
			_dropdown.ItemChanged += () => NotifyValueChanged( _dropdown.CurrentText );

			// Add the root widget to the parent layout
			layout.Add( _rootWidget );

			// Set the RootWidget property
			RootWidget = _rootWidget;
			return _rootWidget;
		}

		public override void SetValueSilently( string value )
		{
			if ( _dropdown == null ) return;

			// Try to find a matching option
			if ( _options.Contains( value, StringComparer.OrdinalIgnoreCase ) )
			{
				_dropdown.CurrentText = value;
			}
			else if ( _options.Length > 0 )
			{
				_dropdown.CurrentText = _options[0]; // Default to first option
			}
		}
	}
}
