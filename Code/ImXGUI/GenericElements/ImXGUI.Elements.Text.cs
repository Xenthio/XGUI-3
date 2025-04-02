using Sandbox.UI;

namespace XGUI.ImmediateMode;

public partial class ImXGUI
{
	// Text display
	public static void Text( string text )
	{
		GetOrCreateElement<Label>( text, l => l.Text = text );
	}
}
