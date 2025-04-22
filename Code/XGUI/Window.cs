using Sandbox;
using Sandbox.UI;
using System.Linq;
namespace XGUI;

public partial class Window : Panel
{
	public string Title = "Window";
	public TitleBar TitleBar { get; set; }


	public Vector2 Position;
	public Vector2 Size;
	public Vector2 MinSize = new Vector2();

	public int ZIndex;

	public bool HasControls = true;

	public bool HasMinimise = false;
	public bool HasMaximise = false;
	public bool HasClose = true;

	public bool IsResizable = true;
	public bool IsDraggable = true;

	public Button ControlsClose { get; set; } = new Button();
	public Button ControlsMinimise { get; set; } = new Button();
	public Button ControlsMaximise { get; set; } = new Button();


	public Window()
	{
		TitleBar = new TitleBar();
		TitleBar.ParentWindow = this;
		AddChild( TitleBar );

		AddClass( "Panel" );
		AddClass( "Window" );
		Style.Position = PositionMode.Absolute;
		Style.FlexDirection = FlexDirection.Column;
	}
	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );
		if ( firstTime )
		{
			// warn if we dont have a child with class window-content
			if ( !Children.Any( x => x.HasClass( "window-content" ) ) )
			{
				Log.Warning( $"The window {this} does not have a child with class window-content, this is standard practice as of XGUI-3" );
			}

			CreateTitleBar();
			this.AddEventListener( "onmousedown", ResizeDown );
			this.AddEventListener( "onmouseup", ResizeUp );
			this.AddEventListener( "onmousemove", ResizeMove );
			OverrideButtons();
		}
		SetChildIndex( TitleBar, 0 );
	}

	// theres no way by default to make buttons focusable so hack it in
	public void OverrideButtons()
	{
		foreach ( Panel button in Descendants.OfType<Button>() )
		{
			var focusallowed = button.GetAttribute( "focus", "0" );
			if ( focusallowed == "1" )
			{
				button.AcceptsFocus = true;
			}
			var autofocus = button.GetAttribute( "autofocus", "0" );
			if ( autofocus == "1" )
			{
				button.Focus();
				button.AddClass( "autofocused" );
			}
		}
	}
	Panel LastFocus;
	public void FocusUpdate()
	{
		if ( InputFocus.Current == null || InputFocus.Current is Window ) return;
		if ( InputFocus.Current != LastFocus )
		{
			if ( LastFocus != null )
			{
				LastFocus.SetClass( "focus", false );
			}
			LastFocus = InputFocus.Current;
			LastFocus.SetClass( "focus", true );
		}
	}

	public void CreateTitleBar()
	{
		/*
		<style>
			Window {
				pointer-events:all;
				position: absolute;
				flex-direction:column;
				.TitleBar {
					.TitleIcon {

					}
					.TitleSpacer {
						flex-grow: 1;
						background-color: rgba(0,0,0,1);
					}
					.Control {
					}
				}
			}
		</style>
		<div class="TitleBar" @ref=TitleBar>
			<div class="TitleIcon" @ref=TitleIcon></div>
			<div>@Title</div>
			<div class="TitleSpacer" onmousedown=@DragBarDown onmouseup=@DragBarUp onmousemove=@Drag></div>
			<button class="Control" @ref=ControlsClose onclick=@Close>X</button>
		</div>
		*/


		AddChild( TitleBar );
		var bg = TitleBar.AddChild<Panel>( "TitleBackground" );
		TitleBar.Style.ZIndex = 100;

		// The "0", "1" and "r" are for the marlett/webdings font
		// Ideally i want these to be set from the theming CSS space
		// but unfortunately s&box does not support the css content property
		ControlsMinimise.AddEventListener( "onclick", Minimise );
		ControlsMinimise.Text = "0";

		ControlsMaximise.AddEventListener( "onclick", Maximise );
		ControlsMaximise.Text = "1";

		ControlsClose.AddEventListener( "onclick", Close );
		ControlsClose.Text = "r";

	}

	bool Minimised = false;
	Vector2 PreMinimisedSize;
	Vector2 PreMinimisedPos;
	public void Minimise()
	{
		if ( !Minimised )
		{
			PreMinimisedSize = Box.Rect.Size;

			PreMinimisedPos = Position;

			var offset = 0;

			// offset x for other minimised windows
			foreach ( Window window in Parent.Children.OfType<Window>() )
			{
				if ( window.Minimised )
				{
					offset += 180;
				}
			}
			Position.x = 0 + offset;

			var newheight = TitleBar.Box.Rect.Size.y + ((TitleBar.Box.Rect.Position.y - Box.Rect.Position.y) * 2);
			Log.Info( newheight );
			Position.y = Parent.Box.Rect.Size.y - newheight;


			Style.Height = newheight;
			Style.Width = 180;
			Minimised = true;
		}
		else
		{
			Minimised = false;
			Style.Width = PreMinimisedSize.x;
			Style.Height = PreMinimisedSize.y;

			Position = PreMinimisedPos;
		}
		Log.Info( "minimise" );
	}

	bool Maximised = false;
	Vector2 PreMaximisedSize;
	Vector2 PreMaximisedPos;
	public void Maximise()
	{
		if ( !Maximised )
		{
			PreMaximisedSize = Box.Rect.Size;

			PreMaximisedPos = Position;

			Position = 0;

			Style.Height = Parent.Box.Rect.Size.y;
			Style.Width = Parent.Box.Rect.Size.x;
			Maximised = true;
		}
		else
		{
			Maximised = false;
			Style.Width = PreMaximisedSize.x;
			Style.Height = PreMaximisedSize.y;

			Position = PreMaximisedPos;
		}
		Log.Info( "maximise" );
	}
	public void Close()
	{
		Log.Info( "close" );
		OnClose();
		Delete();
	}

	public virtual void OnClose()
	{
		// Override this to do something when the window closes
	}

	public override void Tick()
	{
		base.Tick();

		// Todo - use something nicer that doesn't rely on this being named Attack1
		if ( Input.Released( "Attack1" ) )
		{
			Dragging = false;
			ResizeUp();
		}

		Drag();

		if ( Style.Left == null )
		{
			Style.Left = 0;
			Style.Top = 0;
		}
		Style.Position = PositionMode.Absolute;
		Style.Left = Position.x * ScaleFromScreen;
		Style.Top = Position.y * ScaleFromScreen;

		Style.ZIndex = (Parent.ChildrenCount - Parent.GetChildIndex( this )) * 10;

		SetClass( "minimised", this.Minimised );
		SetClass( "maximised", this.Maximised );
		SetClass( "unfocused", !this.HasFocus );
		FocusUpdate();
	}
	public void FocusWindow()
	{
		AcceptsFocus = true;
		if ( !HasFocus )
			Focus();
		Parent.SetChildIndex( this, 0 );
	}

	Vector2 MousePos()
	{
		if ( FindRootPanel().IsWorldPanel && Game.ActiveScene.IsValid() && Game.ActiveScene.IsValid() )
		{
			Ray ray = Game.ActiveScene.Camera.ScreenPixelToRay( Mouse.Position );
			FindRootPanel().RayToLocalPosition( ray, out var pos, out var distance );
			return pos;
		}
		return FindRootPanel().MousePosition;
	}

	Vector2 LocalMousePos()
	{
		return Parent.MousePosition;
	}

	// -------------
	// Dragging
	// -------------
	bool Dragging = false;
	float xoff = 0;
	float yoff = 0;
	public void Drag()
	{
		if ( !Dragging ) return;
		var mousePos = LocalMousePos();
		Position.x = ((mousePos.x) - xoff);
		Position.y = ((mousePos.y) - yoff);

		// Window edge to edge snapping
		foreach ( Window window in Parent.Children.OfType<Window>() )
		{
			var snapDistance = 10;

			var window1leftpos = Position.x;
			var window1rightpos = Position.x + Box.Rect.Size.x;
			var window1uppos = Position.y;
			var window1downpos = Position.y + Box.Rect.Size.y;

			var window2leftpos = window.Position.x;
			var window2rightpos = window.Position.x + window.Box.Rect.Size.x;
			var window2uppos = window.Position.y;
			var window2downpos = window.Position.y + window.Box.Rect.Size.y;

			if ( !(window1downpos < window2uppos || window1uppos > window2downpos) )
			{
				if ( window1leftpos.AlmostEqual( window2rightpos, snapDistance ) ) Position.x -= window1leftpos - window2rightpos;
				if ( window1rightpos.AlmostEqual( window2leftpos, snapDistance ) ) Position.x += window2leftpos - window1rightpos;
			}
			if ( !(window1rightpos < window2leftpos || window1leftpos > window2rightpos) )
			{
				if ( window1uppos.AlmostEqual( window2downpos, snapDistance ) ) Position.y -= window1uppos - window2downpos;
				if ( window1downpos.AlmostEqual( window2uppos, snapDistance ) ) Position.y += window2uppos - window1downpos;
			}
		}
	}
	public void DragBarDown()
	{
		if ( !IsDraggable ) return;

		var mousePos = MousePos();

		Log.Info( this.Parent.MousePosition );

		xoff = (float)((mousePos.x) - Box.Rect.Left);
		yoff = (float)((mousePos.y) - Box.Rect.Top);
		Dragging = true;
	}
	public void DragBarUp()
	{
		Dragging = false;
	}

	// -------------


	// -------------
	// Focusing
	// -------------

	protected override void OnMouseDown( MousePanelEvent e )
	{
		FocusWindow();

		//Parent.SortChildren( x => x.HasFocus ? 1 : 0 );
		base.OnMouseDown( e );
	}

	// -------------
	// Resizing
	// ------------- 
	// I feel like everything about resizing sucks.

	internal bool draggingR = false;
	internal bool draggingL = false;
	internal bool draggingT = false;
	internal bool draggingB = false;

	public void ResizeDown()
	{
		if ( !IsResizable ) return;
		// TODO FIXME: Don't resize if were dragging a window by the title bar!!
		var Distance = 5;
		var mousePos = MousePos();
		if ( mousePos.y.AlmostEqual( this.Box.Rect.Bottom, Distance ) ) draggingB = true;
		if ( mousePos.x.AlmostEqual( this.Box.Rect.Right, Distance ) ) draggingR = true;
		if ( mousePos.y.AlmostEqual( this.Box.Rect.Top, Distance ) ) draggingT = true;
		if ( mousePos.x.AlmostEqual( this.Box.Rect.Left, Distance ) ) draggingL = true;
		//draggingT = true;
		//draggingL = true;

		xoff1 = (float)((mousePos.x) - this.Box.Rect.Right);
		yoff1 = (float)((mousePos.y) - this.Box.Rect.Bottom);
		xoff2 = (float)((mousePos.x) - this.Box.Rect.Left);
		yoff2 = (float)((mousePos.y) - this.Box.Rect.Top);
	}
	public void ResizeUp()
	{
		draggingB = false;
		draggingR = false;
		draggingT = false;
		draggingL = false;
	}
	internal float xoff1 = 0;
	internal float yoff1 = 0;
	internal float xoff2 = 0;
	internal float yoff2 = 0;
	public void ResizeMove()
	{
		var mousePos = MousePos();
		var mousePosLocal = LocalMousePos();
		if ( IsResizable )
		{
			var Distance = 5;

			var almostbottom = mousePos.y.AlmostEqual( this.Box.Rect.Bottom, Distance );
			var almostright = mousePos.x.AlmostEqual( this.Box.Rect.Right, Distance );
			var almosttop = mousePos.y.AlmostEqual( this.Box.Rect.Top, Distance );
			var almostleft = mousePos.x.AlmostEqual( this.Box.Rect.Left, Distance );


			if ( (almostleft && almostbottom) || (draggingL && draggingB) ) Style.Cursor = "nesw-resize";
			else if ( (almostright && almosttop) || (draggingR && draggingT) ) Style.Cursor = "nesw-resize";
			else if ( (almostright && almostbottom) || (draggingR && draggingB) ) Style.Cursor = "nwse-resize";
			else if ( (almostleft && almosttop) || (draggingL && draggingT) ) Style.Cursor = "nwse-resize";
			else if ( almostbottom || draggingB ) Style.Cursor = "ns-resize";
			else if ( almostright || draggingR ) Style.Cursor = "ew-resize";
			else if ( almosttop || draggingT ) Style.Cursor = "ns-resize";
			else if ( almostleft || draggingL ) Style.Cursor = "ew-resize";
			else Style.Cursor = "unset";
		}

		/*if ( Mouse )
		{
			ResizeUp( e );
		}*/

		// This sucks.

		if ( draggingB )
		{
			//Parent.Style.Width = (FindRootPanel().MousePosition.x - Parent.Box.Rect.Left) - xoff;
			var newheight = (mousePos.y - Box.Rect.Top) - yoff1;
			if ( newheight > MinSize.y )
			{
				Style.Height = newheight;
			}
		}

		if ( draggingR )
		{
			var newwidth = (mousePos.x - Box.Rect.Left) - xoff1;
			if ( newwidth > MinSize.x )
			{
				Style.Width = newwidth;
			}
			//Parent.Style.Height = (FindRootPanel().MousePosition.y - Parent.Box.Rect.Top) - yoff;
		}
		if ( draggingT )
		{
			var newheight = Box.Rect.Height - ((mousePos.y - yoff2) - Box.Rect.Top);
			if ( newheight > MinSize.y )
			{
				Style.Height = newheight;
				Position.y = mousePosLocal.y - yoff2;
			}
		}

		if ( draggingL )
		{
			var newwidth = Box.Rect.Width - ((mousePos.x - xoff2) - Box.Rect.Left);
			if ( newwidth > MinSize.x )
			{
				Style.Width = newwidth;
				Position.x = mousePosLocal.x - xoff2;
			}
		}


	}

	public void SetTheme( string theme )
	{
		var parent = this.Parent;

		// Remove existing style sheets (except .razor.scss ones) 
		foreach ( var style in AllStyleSheets.ToList() )
		{
			if ( !style.FileName.EndsWith( ".razor.scss" ) && !style.FileName.EndsWith( ".cs.scss" ) )
			{

				//Log.Info( style.FileName );
				StyleSheet.Remove( style.FileName );
			}
		}
		var styleToApply = Sandbox.UI.StyleSheet.FromFile( theme );

		// Apply the new style
		StyleSheet.Add( styleToApply );

		// Force immediate style update
		Style.Dirty();

		// Force a complete rebuild by temporarily removing from parent and re-adding
		// This is more aggressive but guarantees a full refresh
		Parent = null;
		Parent = parent;

		// Force layout recalculation - traverse child hierarchy
		ForceStyleUpdateRecursive( this );
	}

	private void ForceStyleUpdateRecursive( Panel panel )
	{
		// Mark this panel's style as dirty to force recalculation
		panel.Style.Dirty();

		// Update all immediate children
		foreach ( var child in panel.Children )
		{
			if ( child == null || !child.IsValid() ) continue;

			// Mark the child's style as dirty
			child.Style.Dirty();

			// Recursively update this child's children
			ForceStyleUpdateRecursive( child );
		}
	}

	// -------------
	public override void SetProperty( string name, string value )
	{
		switch ( name )
		{
			case "title":
				{
					Title = value;
					return;
				}
			case "hasminimise":
				{
					HasMinimise = bool.Parse( value );
					return;
				}
			case "hasmaximise":
				{
					HasMaximise = bool.Parse( value );
					return;
				}
			case "hasclose":
				{
					HasClose = bool.Parse( value );
					return;
				}

			case "isresizable":
				{
					IsResizable = bool.Parse( value );
					return;
				}
			case "isdraggable":
				{
					IsDraggable = bool.Parse( value );
					return;
				}


			case "width":
				{
					Style.Width = Length.Parse( value );
					return;
				}
			case "height":
				{
					Style.Height = Length.Parse( value );
					return;
				}


			case "x":
				{
					Position.x = Length.Parse( value ).Value.Value;
					return;
				}
			case "y":
				{
					Position.y = Length.Parse( value ).Value.Value;
					return;
				}


			case "minwidth":
				{
					MinSize.x = Length.Parse( value ).Value.Value;
					return;
				}
			case "minheight":
				{
					MinSize.y = Length.Parse( value ).Value.Value;
					return;
				}
		}
	}
}
