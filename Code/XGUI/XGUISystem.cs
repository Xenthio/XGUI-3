using Sandbox;

namespace XGUI;

public class XGUISystem : GameObjectSystem
{
	public XGUIRootPanel Panel { get; internal set; }
	public XGUISystem( Scene scene ) : base( scene )
	{
	}
	/*public XGUISystem( Scene scene ) : base( scene )
	{
		// Create an XGUI Root Panel for the scene
		Panel = new XGUIRootPanel();
		Panel.RenderedManually = true;
		Panel.Scene = scene;

		Listen( Stage.StartUpdate, 10, AddHook, "TryAddXGUIHook" );
	}

	bool hookAdded = false;
	void AddHook()
	{
		if ( hookAdded ) return;
		hookAdded = true;
		Scene.Camera.AddHookBeforeOverlay( "XGUI", 1, Draw );
	}

	void Draw( SceneCamera cam )
	{
		Panel.RenderManual();
	}*/
	public override void Dispose()
	{
		base.Dispose();
		Panel?.Delete();
	}
}
