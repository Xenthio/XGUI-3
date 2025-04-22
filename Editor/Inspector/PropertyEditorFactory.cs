
namespace XGUI.XGUIEditor;

/// <summary>
/// Factory for creating appropriate property editors
/// </summary>
public class PropertyEditorFactory
{
	/// <summary>
	/// Creates an appropriate editor for the given property
	/// </summary>
	public static PropertyEditor CreateEditor( string propertyName, string displayName, bool isStyle = false )
	{
		// CSS style properties
		if ( isStyle )
		{
			switch ( propertyName.ToLowerInvariant() )
			{
				// Size and position properties (numeric with units)
				case "width":
				case "height":
				case "top":
				case "left":
				case "right":
				case "bottom":
				case "margin-top":
				case "margin-right":
				case "margin-bottom":
				case "margin-left":
				case "padding-top":
				case "padding-right":
				case "padding-bottom":
				case "padding-left":
				case "font-size":
					return new FloatPropertyEditor( propertyName, displayName, true );

				// Color properties
				case "background-color":
				case "color":
				case "border-color":
					return new ColorPropertyEditor( propertyName, displayName, true );

				// Position type
				case "position":
					return new DropdownPropertyEditor( propertyName, displayName,
						new[] { "relative", "absolute" }, true );

				// Display type
				case "display":
					return new DropdownPropertyEditor( propertyName, displayName,
						new[] { "flex", "none", "block", "inline" }, true );

				// Flex properties
				case "flex-direction":
					return new DropdownPropertyEditor( propertyName, displayName,
						new[] { "row", "column", "row-reverse", "column-reverse" }, true );

				// Default to text input for other CSS properties
				default:
					return new TextPropertyEditor( propertyName, displayName, true );
			}
		}

		// General properties (non-CSS)
		switch ( propertyName )
		{
			case "checked":
				return new BoolPropertyEditor( propertyName, displayName );

			// Default to text input for other properties
			default:
				return new TextPropertyEditor( propertyName, displayName );
		}
	}

	/// <summary>
	/// Creates an editor for a specific type of value
	/// </summary>
	public static PropertyEditor CreateTypedEditor<T>( string propertyName, string displayName, bool isStyle = false )
	{
		if ( typeof( T ) == typeof( float ) || typeof( T ) == typeof( int ) || typeof( T ) == typeof( double ) )
		{
			return new FloatPropertyEditor( propertyName, displayName, isStyle );
		}
		else if ( typeof( T ) == typeof( bool ) )
		{
			return new BoolPropertyEditor( propertyName, displayName, isStyle );
		}
		else if ( typeof( T ) == typeof( Color ) )
		{
			return new ColorPropertyEditor( propertyName, displayName, isStyle );
		}
		else
		{
			return new TextPropertyEditor( propertyName, displayName, isStyle );
		}
	}
}
