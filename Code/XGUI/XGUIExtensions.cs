using Sandbox.UI;

namespace XGUI;

public static class XGUIExtensions
{
	/// <summary>
	/// Find the owner XGUIPanel/Window of this panel
	/// </summary>
	public static XGUIPanel? GetOwnerXGUIPanel( this Panel panel )
	{
		// loop through parents until we find a Window
		var parent = panel.Parent;
		while ( parent != null )
		{
			if ( parent is XGUIPanel window )
			{
				return window;
			}
			parent = parent.Parent;
		}
		return null;
	}
}
