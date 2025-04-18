using Editor;
using Sandbox;
using Sandbox.UI;
using System;

namespace XGUI;

public class XGUIView : SceneRenderingWidget
{
	XGUIRootPanel Panel;
	XGUIRootComponent _rootComponent;

	public Window Window;
	public Panel WindowContent;

	// Add delegate for selection callback
	public Action<Panel> OnElementSelected { get; set; }

	public XGUIView()
	{
		MinimumSize = 300;
		Scene = new Scene();

		var cam = Scene.CreateObject();

		Camera = cam.AddComponent<CameraComponent>();
		_rootComponent = cam.AddComponent<XGUIRootComponent>();
		Scene.GameTick();
		Scene.GameTick();
		Scene.GameTick();
		Panel = _rootComponent.XGUIPanel;
	}

	public void CreateBlankWindow()
	{
		Window = new Window();
		Window.StyleSheet.Load( "/XGUI/DefaultStyles/OliveGreen.scss" );
		Window.Title = "My New Window";
		Window.MinSize = new Vector2( 200, 200 );
		Panel.AddChild( Window );

		WindowContent = new Panel();
		WindowContent.Style.MinHeight = 200;
		WindowContent.Style.MinWidth = 200;
		WindowContent.AddClass( "window-content" );
		Window.AddChild( WindowContent );

		RegisterSelectionHandlers( Window );
		RegisterSelectionHandlers( WindowContent );
	}

	public void CleanUp()
	{
		base.OnDestroyed();
		Panel.Delete();
	}
	public override void PreFrame()
	{
		base.PreFrame();
		Scene.GameTick();
	}

	// Add this to your existing methods that create UI elements
	public void RegisterSelectionHandlers( Panel panel )
	{
		// Add event handlers to track selection
		panel.AddEventListener( "onmousedown", ( e ) =>
		{
			OnElementSelected?.Invoke( panel );
			e.StopPropagation();
		} );

		// Recursively register handlers for children
		foreach ( var child in panel.Children )
		{
			if ( child is Panel childPanel )
			{
				RegisterSelectionHandlers( childPanel );
			}
		}
	}
}
