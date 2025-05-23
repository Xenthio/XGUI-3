﻿using Sandbox.UI;

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

	/// <summary>
	/// Int input field
	/// </summary>
	/// <param name="label">The text label placed before the control.</param>
	/// <param name="value">A reference to the int the control will get and set.</param>
	/// <returns></returns>
	public static bool InputInt( string label, ref int value )
	{
		return HandleValueControl<int, TextEntry>(
			label,
			ref value,
			( input, val ) => input.Text = val.ToString(),
			( input ) => int.TryParse( input.Text, out var _ ) ? int.Parse( input.Text ) : 0,
			null,
			input => input.Style.FlexGrow = 1
		);
	}

	/// <summary>
	/// Float input field
	/// </summary>
	/// <param name="label">The text label placed before the control.</param>
	/// <param name="value">A reference to the float the control will get and set.</param>
	/// <returns></returns>
	public static bool InputFloat( string label, ref float value )
	{
		return HandleValueControl<float, TextEntry>(
			label,
			ref value,
			( input, val ) => input.Text = val.ToString(),
			( input ) => float.TryParse( input.Text, out var _ ) ? float.Parse( input.Text ) : 0,
			null,
			input => input.Style.FlexGrow = 1
		);
	}
}
