using Editor;
using System;
using System.Collections.Generic;
namespace XGUI.XGUIEditor;

/// <summary>
/// Manages a group of related properties
/// </summary>
public class PropertyGroup
{
	public string GroupName { get; }
	public Widget GroupWidget { get; private set; }
	public Layout GroupLayout { get; private set; }
	private List<PropertyEditor> _editors = new List<PropertyEditor>();

	public PropertyGroup( string name )
	{
		GroupName = name;
	}

	public Widget CreateUI( Layout parentLayout )
	{
		// Create group widget
		GroupWidget = new Widget( null );
		GroupWidget.Layout = Layout.Column();
		GroupWidget.Layout.Spacing = 2;

		// Add header
		var header = new Editor.Label( GroupName );
		header.SetStyles( "font-weight: bold; margin-top: 5px;" );
		GroupWidget.Layout.Add( header );

		// Add to parent
		parentLayout.Add( GroupWidget );
		GroupLayout = GroupWidget.Layout;

		return GroupWidget;
	}

	public T AddEditor<T>( string propertyName, string displayName, bool isStyle = false ) where T : PropertyEditor
	{
		// Create editor of specific type
		T editor = (T)Activator.CreateInstance( typeof( T ), propertyName, displayName, isStyle );
		_editors.Add( editor );

		// Create UI if group is initialized
		if ( GroupLayout != null )
		{
			editor.CreateUI( GroupLayout );
		}

		return editor;
	}
	/// <summary>
	/// Add a float property editor to this property group
	/// </summary>
	public FloatPropertyEditor AddFloatEditor(
		string propertyName,
		string displayName,
		bool isStyle = false,
		string unit = "px" )
	{
		// Create float editor specifically
		var editor = new FloatPropertyEditor( propertyName, displayName, isStyle, unit );
		_editors.Add( editor );

		// Create UI if group is initialized
		if ( GroupLayout != null )
		{
			editor.CreateUI( GroupLayout );
		}

		return editor;
	}

	/// <summary>
	/// Add a dropdown editor to this property group
	/// </summary>
	public DropdownPropertyEditor AddDropdownEditor(
		string propertyName,
		string displayName,
		string[] options,
		bool isStyle = false )
	{
		// Create dropdown editor specifically
		var editor = new DropdownPropertyEditor( propertyName, displayName, options, isStyle );
		_editors.Add( editor );

		// Create UI if group is initialized
		if ( GroupLayout != null )
		{
			editor.CreateUI( GroupLayout );
		}

		return editor;
	}
	/// <summary>
	/// Add a color property editor to this property group
	/// </summary>
	public ColorPropertyEditor AddColorEditor(
		string propertyName,
		string displayName,
		bool isStyle = false )
	{
		// Create color editor specifically
		var editor = new ColorPropertyEditor( propertyName, displayName, isStyle );
		_editors.Add( editor );

		// Create UI if group is initialized
		if ( GroupLayout != null )
		{
			editor.CreateUI( GroupLayout );
		}

		return editor;
	}


	public PropertyEditor AddEditor( string propertyName, string displayName, bool isStyle = false )
	{
		// Create appropriate editor through factory
		var editor = PropertyEditorFactory.CreateEditor( propertyName, displayName, isStyle );
		_editors.Add( editor );

		// Create UI if group is initialized
		if ( GroupLayout != null )
		{
			editor.CreateUI( GroupLayout );
		}

		return editor;
	}

	public void UpdateValues( Dictionary<string, string> values )
	{
		foreach ( var editor in _editors )
		{
			if ( values.TryGetValue( editor.PropertyName, out string value ) )
			{
				editor.SetValueSilently( value );
			}
		}
	}
}
