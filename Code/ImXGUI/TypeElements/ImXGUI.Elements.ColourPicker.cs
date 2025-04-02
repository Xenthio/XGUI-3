using Sandbox.UI;

namespace XGUI.ImmediateMode;

public partial class ImXGUI
{
	/// <summary>
	/// Color picker control
	/// </summary>
	/// <param name="label">The text label placed before the control.</param>
	/// <param name="color">A reference to the colour the control will get and set</param>
	/// <returns></returns>
	public static bool ColorPicker( string label, ref Color color )
	{
		// You might need to adjust the container and control sizes to fit your UI.
		return HandleValueControl<Color, ColourPickerControl>(
			label,
			ref color,
			( picker, val ) => picker.CurrentColor = val,
			( picker ) => picker.CurrentColor,
			setupContainer: p =>
			{
				p.Style.FlexDirection = FlexDirection.Row;
				p.Style.AlignItems = Align.Center;
				p.Style.MarginBottom = 5;
			},
			additionalSetup: picker =>
			{
				picker.Style.Width = 150;  // Adjust as needed.
				picker.Style.Height = 24;   // Adjust as needed.
				picker.Style.MarginLeft = 10;
			}
		);
	}
}
