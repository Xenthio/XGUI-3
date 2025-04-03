using Sandbox;
using Sandbox.UI;
using System.Linq;
namespace XGUI;

[Library( "controllabel" )]
public class ControlLabel : Panel
{
	public Label Label;
	public ControlLabel()
	{
		AddClass( "controllabel" );
		Label = AddChild<Label>();
	}
	public override void Tick()
	{
		base.Tick();
		var shouldFocus = PanelHasFocus( this ) || AnyChildHasFocus( this );
		SetClass( "focus", shouldFocus );
	}
	public bool AnyChildHasFocus( Panel panel )
	{
		return panel.Children.Where( x => PanelHasFocus( x ) ).Any() || panel.Children.OfType<ComboBox>().Where( x => x.IsOpen ).Any();
	}
	public bool PanelHasFocus( Panel panel )
	{
		return panel.HasFocus; //panel.HasClass( "focus" );
	}
	public override void SetProperty( string name, string value )
	{
		if ( name == "label" )
		{
			Label.Text = value;
			return;
		}
		base.SetProperty( name, value );
	}
}
