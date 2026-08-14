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
	public Sandbox.WorldPanel WorldPanel { get; private set; }

	public XGUIRootComponent()
	{

	}
	protected override void OnStart()
	{
		if ( GameObject.Components.TryGet<Sandbox.WorldPanel>( out var worldPanel ) )
		{
			WorldPanel = worldPanel;
		}
		else if ( GameObject.Components.TryGet<ScreenPanel>( out var screenPanel ) )
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
		Panel.Parent = WorldPanel?.GetPanel() ?? ScreenPanel?.GetPanel();
		Panel.AddChild( XGUIPanel );

		Scene.GetSystem<XGUISystem>().RegisterRoot( this );
	}
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( XGUIPanel.IsValid() )
			XGUIPanel.Style.PointerEvents = MouseUnlocked ? PointerEvents.All : PointerEvents.None;
		if ( UseDesktopScale && ScreenPanel?.IsValid() == true )
		{
			ScreenPanel.Scale = Screen.DesktopScale;
		}

	}
}
