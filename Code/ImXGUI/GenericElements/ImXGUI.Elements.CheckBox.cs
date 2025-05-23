﻿namespace XGUI.ImmediateMode;

public partial class ImXGUI
{
	/// <summary>
	/// Checkbox control
	/// </summary>
	/// <param name="label">The text label placed after the control.</param>
	/// <param name="value">A reference to the bool the control will get and set.</param>
	/// <returns></returns>
	public static bool Checkbox( string label, ref bool value )
	{
		return HandleValueControl<bool, XGUI.CheckBox>(
			label: label,
			value: ref value,
			setControlValue: ( cb, val ) => cb.Checked = val,
			getControlValue: ( cb ) => cb.Checked,
			setupContainer: null,
			additionalSetup: cb => cb.LabelText = label
		);
	}
}
