using Sandbox.UI;

namespace XGUI.ImmediateMode;

public partial class ImXGUI
{
	/// <summary>
	/// Button control
	/// </summary>
	/// <param name="label">The label that the button will have.</param>
	/// <returns>A bool indicating whether or not the button was just clicked.</returns>
	public static bool Button( string label )
	{
		bool clicked = false;
		var button = GetOrCreateElement<Button>( label, b =>
		{
			b.Text = label;

			// Get the unique ID for this button.
			string id = b.ElementName;

			// Check for click state
			if ( b.HasActive &&
				(!_elementState.TryGetValue( _currentWindowId, out var windowState ) ||
				 !windowState.TryGetValue( id, out var state ) ||
				 !state.Values.TryGetValue( "wasActive", out var wasActive ) ||
				 !(bool)wasActive) )
			{
				clicked = true;
			}

			// Update state
			var btnState = GetState( id ); // Use GetState here for proper state retrieval.
			btnState.Values["wasActive"] = b.HasActive;
		} );

		return clicked;
	}
}
