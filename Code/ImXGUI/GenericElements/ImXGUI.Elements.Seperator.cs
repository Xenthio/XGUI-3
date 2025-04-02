using Sandbox.UI;

namespace XGUI.ImmediateMode;

public partial class ImXGUI
{
	/// <summary>
	/// A simple horizontal line to separate elements.
	/// </summary>
	public static void Separator()
	{
		GetOrCreateElement<Panel>( "separator", p =>
		{
			p.Style.Height = 1;
			p.Style.BackgroundColor = Color.Parse( "#333333" );
		} );
	}
}
