using Sandbox;

namespace XGUI.ImmediateMode;

public class ImXGUISystem : GameObjectSystem
{
	public ImXGUISystem( Scene scene ) : base( scene )
	{
		var currentScene = scene;

		Listen( Stage.StartUpdate, 10, () => { ImXGUI.NewFrame( "OnUpdate", currentScene ); }, "StartImXGUIFrame" );
		Listen( Stage.FinishUpdate, 10, () => { ImXGUI.EndFrame( "OnUpdate" ); }, "FinishImXGUIFrame" );

		Listen( Stage.StartFixedUpdate, 10, () => { ImXGUI.NewFrame( "OnFixedUpdate", currentScene ); }, "StartImXGUIFrame_FixedUpdate" );
		Listen( Stage.FinishFixedUpdate, 10, () => { ImXGUI.EndFrame( "OnFixedUpdate" ); }, "FinishImXGUIFrame_FixedUpdate" );
	}
}
