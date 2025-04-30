using Sandbox;

namespace XGUI;

public class XGUISystem : GameObjectSystem
{
	/// <summary>
	/// The Default theme that windows will use if not manually set by the window.
	/// </summary>
	public string GlobalTheme { get; internal set; } = "/XGUI/DefaultStyles/OliveGreen.scss";
	public XGUIRootComponent Component { get; internal set; }
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

	public void SetGlobalTheme( string theme )
	{
		GlobalTheme = theme;
		// Find all Window type panels in the hierarchy
		foreach ( var window in Panel.ChildrenOfType<Window>() )
		{
			window.SetTheme( GlobalTheme );
		}
	}

}
