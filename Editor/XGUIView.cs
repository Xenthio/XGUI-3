using Editor;
using Sandbox;
using Sandbox.UI;
using System;
using XGUI.XGUIEditor;

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

		Window.FocusWindow();
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

	private Panel SelectedPanel;
	private MarkupNode SelectedNode;

	private Panel DraggingPanel;
	private MarkupNode DraggingNode;
	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );
		// do selection here, find what we're hovering and call SelectAndInspect, we'll also have to detect mouse movement to do dragging
	}
	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );
		// if mouse down, go into dragging mode, find what we're hovering and set it as the dragging panel
		// if dragging, update the position of the dragging panel
		// if dragging in position absolute, update the position by desired alignment (top,bottom,left,right), if otherwise, just change child order.
	}
	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );
		// stop dragging if so,
	}
}
