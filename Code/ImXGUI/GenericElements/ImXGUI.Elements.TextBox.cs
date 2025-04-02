using Sandbox.UI;

namespace XGUI.ImmediateMode;

public partial class ImXGUI
{
	/// <summary>
	/// Text input field
	/// </summary>
	/// <param name="label">The text label placed before the control.</param>
	/// <param name="value">A reference to the string the control will get and set.</param>
	/// <returns></returns>
	public static bool InputText( string label, ref string value )
	{
		return HandleValueControl<string, TextEntry>(
			label,
			ref value,
			( input, val ) => input.Text = val,
			( input ) => input.Text,
			null,
			input => input.Style.FlexGrow = 1
		);
	}
}
