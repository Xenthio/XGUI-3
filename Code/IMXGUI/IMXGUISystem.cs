using Sandbox;

namespace XGUI.ImmediateMode;

public class IMXGUISystem : GameObjectSystem
{
	public IMXGUISystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 10, StartIMXGUIFrame, "StartIMXGUIFrame" );
		Listen( Stage.FinishUpdate, 10, FinishIMXGUIFrame, "FinishIMXGUIFrame" );
	}

	void StartIMXGUIFrame()
	{
		IMXGUI.NewFrame();
	}

	void FinishIMXGUIFrame()
	{
		IMXGUI.EndFrame();
	}
}
