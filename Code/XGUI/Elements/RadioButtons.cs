using Sandbox;
using Sandbox.UI;
namespace XGUI;

[Library( "radiobuttons" ), Alias( "radio" )]
public class RadioButtons : Panel
{
	public RadioButton SelectedRadioOption;
	public RadioButtons()
	{

		AddClass( "radiobuttons" );
	}
}
