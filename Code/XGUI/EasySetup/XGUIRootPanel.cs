using Sandbox.UI;
namespace XGUI;
public class XGUIRootPanelOld : Panel
{
	public static XGUIRootPanelOld Current = null;

	public XGUIRootPanelOld()
	{
		Current = this;
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.Cursor = "unset";
		Log.Info( "XGUI Root Panel Initialised." );
	}

	public override void Tick()
	{
		Current = this;
		base.Tick();
	}
	/*protected override void UpdateScale( Rect screenSize )
	{
		Scale = 1;
		if ( screenSize.Width > 1920 ) Scale = 1.50f;
		if ( screenSize.Width > 2200 ) Scale = 2.00f;
	}*/
}
