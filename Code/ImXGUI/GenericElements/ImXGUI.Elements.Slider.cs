using Sandbox;
using System;

namespace XGUI.ImmediateMode;

public partial class ImXGUI
{

	/// <summary>
	/// Float input control.
	/// </summary>
	/// <param name="label">The text label placed before the control.</param>
	/// <param name="value">A reference to the int value the control will get and set</param>
	/// <param name="min">The minimum allowed int value</param>
	/// <param name="max">The maximum allowed int value</param>
	/// <param name="step">How much to step the slider by</param>
	/// <returns></returns>
	public static bool SliderInt( string label, ref int value, float min, float max, int step = 1 )
	{

		return HandleValueControl<int, SliderScale>(
			label,
			ref value,
			( slider, val ) => slider.Value = val,
			( slider ) => MathF.Round( slider.Value ).CeilToInt(),
			null,
			slider =>
			{
				slider.MinValue = min;
				slider.MaxValue = max;
				slider.Step = step;
				slider.Style.FlexGrow = 1;
			}
		);
	}

	/// <summary>
	/// Float input control.
	/// </summary>
	/// <param name="label">The text label placed before the control.</param>
	/// <param name="value">A reference to the float value the control will get and set</param>
	/// <param name="min">The minimum allowed float value</param>
	/// <param name="max">The maximum allowed float value</param>
	/// <param name="step">How much to step the slider by</param>
	/// <returns></returns>
	public static bool SliderFloat( string label, ref float value, float min, float max, float step = 0.0f )
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
				slider.Step = step;
				slider.Style.FlexGrow = 1;
			}
		);
	}
}
