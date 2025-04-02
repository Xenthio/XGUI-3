using Editor;
using Sandbox;
using Sandbox.UI;

namespace XGUI;

public class XGUIView : SceneRenderingWidget
{
	XGUIRootPanel Panel;
	XGUIRootComponent _rootComponent;

	public Window Window;
	public Panel WindowContent;

	public XGUIView()
	{
		MinimumSize = 300;
		Scene = new Scene();

		var cam = Scene.CreateObject();

		Camera = cam.AddComponent<CameraComponent>();
		_rootComponent = cam.AddComponent<XGUIRootComponent>();
		Panel = _rootComponent.XGUIPanel;
	}

	public void CreateBlankWindow()
	{
		Window = new Window();
		Window.StyleSheet.Load( "/XGUI/DefaultStyles/OliveGreen.scss" );
		Window.TitleLabel.Text = "My New Window";
		Window.MinSize = new Vector2( 200, 200 );
		Panel.AddChild( Window );

		WindowContent = new Panel();
		WindowContent.Style.MinHeight = 200;
		WindowContent.Style.MinWidth = 200;
		Window.AddChild( WindowContent );
	}

	public void CleanUp()
	{
		base.OnDestroyed();
		Panel.Delete();
	}
	public override void PreFrame()
	{
		base.PreFrame();
	}
}
