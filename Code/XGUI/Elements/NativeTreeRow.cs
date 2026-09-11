using Sandbox.UI;
using System.Linq;

namespace XGUI;

/// <summary>Presentation for native, recycled tree rows using XGUI icon assets.</summary>
public static class NativeTreeRow
{
	public static void Bind( TreeRow row, string text, string iconName )
	{
		row.Text = text;
		row.IconName = null;
		// Labels cannot own children. Wrap the native click target and the two strokes.
		row.Expander.Text = "";
		if ( row.Expander.Parent == row )
		{
			var expander = row.AddChild<Panel>( "xgui-tree-expander" );
			row.SetChildIndex( expander, row.GetChildIndex( row.Expander ) );
			row.Expander.Parent = expander;
			expander.AddChild<Panel>( "xgui-expander-horizontal" );
			expander.AddChild<Panel>( "xgui-expander-vertical" );
		}
		var icon = row.Children.OfType<XGUIIconPanel>().FirstOrDefault();
		if ( icon == null )
		{
			icon = row.AddChild<XGUIIconPanel>();
			icon.AddClass( "xgui-tree-icon" );
			row.SetChildIndex( icon, row.GetChildIndex( row.Label ) );
		}
		icon.IconName = iconName;
		icon.Style.Display = string.IsNullOrEmpty( iconName ) ? DisplayMode.None : DisplayMode.Flex;
	}
}
