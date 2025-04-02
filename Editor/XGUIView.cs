using Editor;
using Sandbox;
using Sandbox.UI;

namespace XGUI;

public class XGUIView : SceneRenderingWidget
{
	XGUIRootPanel panel;
	ScreenPanel _screenPanel;

	public Window Window;
	public Panel WindowContent;

	public XGUIView()
	{
		MinimumSize = 300;
		Scene = new Scene();

		var cam = Scene.CreateObject();

		Camera = cam.AddComponent<CameraComponent>();
		_screenPanel = cam.AddComponent<ScreenPanel>();
		_screenPanel.AutoScreenScale = false;
		_screenPanel.Scale = 1.0f;

		panel = new XGUIRootPanel();
		_screenPanel.GetPanel().AddChild( panel );
		Window = new Window();
		Window.StyleSheet.Load( "/XGUI/DefaultStyles/OliveGreen.scss" );
		Window.TitleLabel.Text = "My New Window";
		Window.MinSize = new Vector2( 200, 200 );
		panel.AddChild( Window );

		WindowContent = new Panel();
		WindowContent.Style.MinHeight = 200;
		WindowContent.Style.MinWidth = 200;
		Window.AddChild( WindowContent );

	}
	public void CleanUp()
	{
		base.OnDestroyed();
		_screenPanel.Destroy();
		panel.Delete();
	}
	public override void PreFrame()
	{
		base.PreFrame();
	}
}
