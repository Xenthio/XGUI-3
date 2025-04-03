using Sandbox.UI;
using System;
using System.Linq;

namespace XGUI.ImmediateMode;

public partial class ImXGUI
{
	// Generic value control handler
	private static bool HandleValueControl<T, TControl>( string label, ref T value,
		Action<TControl, T> setControlValue, Func<TControl, T> getControlValue,
		Action<Panel> setupContainer = null, Action<TControl> additionalSetup = null )
		where TControl : Panel, new()
	{
		// Get the unique ID for this control
		string id = GenerateId<TControl>( label );

		// Get the state for this control
		var state = GetState( id );

		bool changed = false;
		var currentValue = value;

		var container = GetOrCreateElement<Panel>( label, p =>
		{
			// Default container setup
			p.Style.FlexDirection = FlexDirection.Row;
			p.Style.AlignItems = Align.Center;
			p.Style.Margin = Length.Pixels( 2 );

			// Allow custom container setup
			setupContainer?.Invoke( p );

			// Initialize value if needed
			if ( !state.Values.ContainsKey( "value" ) )
			{
				state.Values["value"] = currentValue;
			}

			// Create or update children
			if ( p.ChildrenCount == 0 )
			{
				// Create label for controls that need it
				if ( typeof( TControl ) != typeof( XGUI.CheckBox ) )
				{
					var labelElement = new Label();
					labelElement.Text = label;
					labelElement.Style.MinWidth = Length.Pixels( 80 );
					p.AddChild( labelElement );
				}

				// Create control
				var control = new TControl();

				// Set initial value
				setControlValue( control, (T)state.Values["value"] );

				// Additional setup
				additionalSetup?.Invoke( control );

				p.AddChild( control );
			}
			else
			{
				// Get the control
				TControl control = p.Children.OfType<TControl>().FirstOrDefault();
				additionalSetup?.Invoke( control );

				if ( control != null )
				{
					// Get the current value from the control
					T controlValue = getControlValue( control );

					// Check if the control value has changed from our stored state
					if ( !object.Equals( controlValue, state.Values["value"] ) )
					{
						// Update the state with the new control value
						state.Values["value"] = controlValue;
						state.Changed = true; // Mark that the value has changed

					}  // Check if external value changed - Update state value regardless of control change.
					else if ( !object.Equals( state.Values["value"], currentValue ) )
					{
						state.Values["value"] = currentValue; // update state
						setControlValue( control, currentValue ); // update control with value to prevent external setting from not updating the UI
					}

				}
			}

		} );

		// Update ref parameter if state changed
		if ( state != null && state.Changed )
		{
			value = (T)state.Values["value"];
			changed = true;
			state.Changed = false; // Reset the flag after processing

		}

		return changed;
	}
}
