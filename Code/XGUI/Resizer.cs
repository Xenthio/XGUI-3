using Sandbox.UI;
using Sandbox.UI.Construct;
namespace XGUI;
public class Resizer : Panel
{
	public Resizer()
	{

		AddClass( "Resizer" );
		Add.Label( "", "rs-a" );
		Add.Label( "", "rs-b" );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		Window parent;
		if ( Parent is Window )
			parent = Parent as Window;
		else
			parent = Parent.Parent as Window;
		parent.draggingB = true;
		parent.draggingR = true;
		//draggingT = true;
		//draggingL = true;
		parent.xoff1 = (float)((FindRootPanel().MousePosition.x) - Parent.Box.Rect.Right);
		parent.yoff1 = (float)((FindRootPanel().MousePosition.y) - Parent.Box.Rect.Bottom);
		parent.xoff2 = (float)((FindRootPanel().MousePosition.x) - Parent.Box.Rect.Left);
		parent.yoff2 = (float)((FindRootPanel().MousePosition.y) - Parent.Box.Rect.Top);
	}
	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		Window parent;
		if ( Parent is Window )
			parent = Parent as Window;
		else
			parent = Parent.Parent as Window;
		parent.draggingB = false;
		parent.draggingR = false;
		parent.draggingT = false;
		parent.draggingL = false;
		parent.xoff1 = 0;
		parent.yoff1 = 0;
		parent.xoff2 = 0;
		parent.yoff2 = 0;
	}
	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );

		Window parent;
		if ( Parent is Window )
			parent = Parent as Window;
		else
			parent = Parent.Parent as Window;

		parent.ResizeMove();
	}
}
