namespace XGUI.ImmediateMode;

public partial class ImXGUI
{
	/// <summary>
	/// Slider control
	/// </summary>
	/// <param name="label">The text label placed before the control.</param>
	/// <param name="value">A reference to the float value the control will get and set</param>
	/// <param name="min">The minimum allowed float value</param>
	/// <param name="max">The maximum allowed float value</param>
	/// <returns></returns>
	public static bool Slider( string label, ref float value, float min, float max )
	{
		return HandleValueControl<float, SliderScale>(
			label,
			ref value,
			( slider, val ) => slider.Value = val,
			( slider ) => slider.Value,
			null,
			slider =>
			{
				slider.MinValue = min;
				slider.MaxValue = max;
				slider.Style.FlexGrow = 1;
			}
		);
	}
}
