using Sandbox;

namespace XGUI.ImmediateMode;

public class ImXGUISystem : GameObjectSystem
{
	public ImXGUISystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 10, StartImXGUIFrame, "StartImXGUIFrame" );
		Listen( Stage.FinishUpdate, 10, FinishImXGUIFrame, "FinishImXGUIFrame" );
	}

	void StartImXGUIFrame()
	{
		ImXGUI.NewFrame();
	}

	void FinishImXGUIFrame()
	{
		ImXGUI.EndFrame();
	}
}
