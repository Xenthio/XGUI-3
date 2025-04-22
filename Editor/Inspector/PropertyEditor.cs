using Editor;
using System;
namespace XGUI.XGUIEditor;

/// <summary>
/// Base class for all property editors in the inspector
/// </summary>
public abstract class PropertyEditor
{
	// The property name this editor controls
	public string PropertyName { get; }

	// Display name shown in the UI
	public string DisplayName { get; }

	// True if this editor handles a CSS style property
	public bool IsStyleProperty { get; }

	// The root widget containing all UI elements for this editor
	public Widget RootWidget { get; protected set; }

	// Triggered when the property value changes through user interaction
	public event Action<object> ValueChanged;

	protected PropertyEditor( string propertyName, string displayName, bool isStyle = false )
	{
		PropertyName = propertyName;
		DisplayName = displayName;
		IsStyleProperty = isStyle;
	}

	// Creates and returns the editor UI
	public abstract Widget CreateUI( Layout layout );

	// Updates the editor's value without triggering the ValueChanged event
	public abstract void SetValueSilently( string value );

	// Allows inspector to wire up the editor with the data model
	protected void NotifyValueChanged( object value )
	{
		ValueChanged?.Invoke( value );
	}
}
