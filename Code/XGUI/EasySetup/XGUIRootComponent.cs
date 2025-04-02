using Sandbox;
namespace XGUI;
public class XGUIRootComponent : PanelComponent
{
	public XGUIRootPanel XGUIPanel { get; private set; }
	protected override void OnStart()
	{
		base.OnStart();
		XGUIPanel = new XGUIRootPanel();
		Panel.AddChild( XGUIPanel );

		Scene.GetSystem<XGUISystem>().Panel = XGUIPanel;
	}
}
