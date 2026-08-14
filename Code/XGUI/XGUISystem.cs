using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace XGUI;

public class XGUISystem : GameObjectSystem
{
	private string _globalTheme = "/XGUI/DefaultStyles/OliveGreen.scss";

	/// <summary>
	/// The Default theme that windows will use if not manually set by the window.
	/// </summary>
	public string GlobalTheme
	{
		get => _globalTheme;
		set
		{
			if ( _globalTheme != value )
			{
				_globalTheme = value;

				// Get the name of theme without the path and extension
				var themeName = value.Split( '/' ).Last().Replace( ".scss", "" );
				XGUIIconSystem.CurrentTheme = themeName;
			}
		}
	}

	private readonly List<XGUIRootComponent> _roots = new();

	/// <summary>
	/// All XGUI roots registered in this scene.
	/// </summary>
	public IReadOnlyList<XGUIRootComponent> Roots => _roots;

	/// <summary>
	/// The default root. Screen-space roots take priority so existing callers keep using screen UI
	/// when world roots are also present.
	/// </summary>
	public XGUIRootComponent Component { get; private set; }
	public XGUIRootPanel Panel { get; private set; }
	public static XGUISystem Instance => Game.ActiveScene.GetSystem<XGUISystem>();
	public XGUISystem( Scene scene ) : base( scene )
	{
	}

	internal void RegisterRoot( XGUIRootComponent root )
	{
		if ( root is null || _roots.Contains( root ) )
			return;

		_roots.Add( root );
		SelectDefaultRoot();
	}

	private void SelectDefaultRoot()
	{
		var root = _roots.FirstOrDefault( x => x.ScreenPanel?.IsValid() == true && x.XGUIPanel?.IsValid() == true )
			?? _roots.FirstOrDefault( x => x.XGUIPanel?.IsValid() == true );

		Component = root;
		Panel = root?.XGUIPanel;
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

		foreach ( var root in _roots )
			root.XGUIPanel?.Delete();

		_roots.Clear();
		Component = null;
		Panel = null;
	}

	public void SetGlobalTheme( string theme )
	{
		GlobalTheme = theme;

		foreach ( var root in _roots )
			foreach ( var xguiPanel in root.XGUIPanel?.ChildrenOfType<XGUIPanel>() ?? Enumerable.Empty<XGUIPanel>() )
				xguiPanel.SetTheme( GlobalTheme );
	}

}
