using Sandbox;
using Sandbox.UI;
namespace XGUI;
[Title( "XGUI Root Component" )]
public class XGUIRootComponent : PanelComponent
{
	[Property]
	public bool UseDesktopScale { get; set; } = true;
	[Property]
	public bool MouseUnlocked { get; set; } = true;

	public XGUIRootPanel XGUIPanel { get; private set; }
	public ScreenPanel ScreenPanel { get; private set; }
	protected override void OnStart()
	{
		// check if there's a screenpanel here, create one if not.
		if ( GameObject.Components.TryGet<ScreenPanel>( out var screenPanel ) )
		{
			ScreenPanel = screenPanel;
		}
		else
		{
			ScreenPanel = GameObject.AddComponent<ScreenPanel>();
			ScreenPanel.AutoScreenScale = false;
		}
		base.OnStart();

		XGUIPanel = new XGUIRootPanel();
		Panel.AddChild( XGUIPanel );

		Scene.GetSystem<XGUISystem>().Panel = XGUIPanel;
	}
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		XGUIPanel.Style.PointerEvents = MouseUnlocked ? PointerEvents.All : PointerEvents.None;
		if ( UseDesktopScale )
		{
			ScreenPanel.Scale = Screen.DesktopScale;
		}

	}
}
