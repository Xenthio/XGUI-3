using Sandbox;
namespace XGUI;
[Title( "XGUI Root Component" )]
public class XGUIRootComponent : PanelComponent
{
	public XGUIRootPanel XGUIPanel { get; private set; }
	protected override void OnStart()
	{
		// check if there's a screenpanel here, create one if not.
		if ( GameObject.Components.TryGet<ScreenPanel>( out var screenPanel ) == false )
		{
			var pnl = GameObject.AddComponent<ScreenPanel>();
			pnl.AutoScreenScale = false;
		}
		base.OnStart();

		XGUIPanel = new XGUIRootPanel();
		Panel.AddChild( XGUIPanel );

		Scene.GetSystem<XGUISystem>().Panel = XGUIPanel;
	}
}
