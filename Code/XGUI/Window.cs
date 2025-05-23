using Sandbox;
using Sandbox.UI;
using System;
using System.Linq;
namespace XGUI;

public partial class Window : XGUIPanel
{
	public string Title = "Window";
	public TitleBar TitleBar { get; set; }


	public Vector2 Position = new Vector2( 22, 22 );
	public Vector2 Size;
	public Vector2 MinSize = new Vector2();

	public int ZIndex;

	public bool HasControls = true;

	public bool HasTitleBar = true;

	public bool HasMinimise = false;
	public bool HasMaximise = false;
	public bool HasClose = true;

	public bool IsResizable = true;
	public bool IsDraggable = true;
	public bool AutoFocus = true;

	public Button ControlsClose { get; set; } = new Button();
	public Button ControlsMinimise { get; set; } = new Button();
	public Button ControlsMaximise { get; set; } = new Button();

	public Panel WindowContent;
	public Vector2? InitalInnerSize = null;


	public Window()
	{
		if ( HasTitleBar )
		{
			TitleBar = new TitleBar();
			TitleBar.ParentWindow = this;
			AddChild( TitleBar );
		}

		AddClass( "Panel" );
		AddClass( "Window" );
		Style.Position = PositionMode.Absolute;
		Style.FlexDirection = FlexDirection.Column;
	}

	bool hasInitInnerSize = false;
	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );
		if ( firstTime )
		{
			// warn if we dont have a child with class window-content
			if ( Children.Where( x => x.HasClass( "window-content" ) ).FirstOrDefault() is Panel contentpnl )
			{
				WindowContent = contentpnl;
			}
			else
			{
				Log.Warning( $"The window {this} does not have a child with class window-content, this is standard practice as of XGUI-3" );
			}

			CreateTitleBar();
			this.AddEventListener( "onmousedown", ResizeDown );
			this.AddEventListener( "onmouseup", ResizeUp );
			this.AddEventListener( "onmousemove", ResizeMove );
			OverrideButtons();

			if ( AutoFocus )
			{
				FocusWindow();
				AutoFocus = false;
			}

			// If size is set, set the width and height
			if ( Size != Vector2.Zero )
			{
				Style.Width = Size.x;
				Style.Height = Size.y;
			}
		}

		if ( TitleBar.IsValid )
			SetChildIndex( TitleBar, 0 );
	}


	private void TryInitInnerSize()
	{
		if ( hasInitInnerSize ) return;
		if ( InitalInnerSize.HasValue )
		{
			float currentWindowWidth = Box.Rect.Width;
			float currentWindowHeight = Box.Rect.Height;

			float currentWindowContentWidth = WindowContent.Box.Rect.Width;
			float currentWindowContentHeight = WindowContent.Box.Rect.Height;

			if ( currentWindowContentHeight == 0 && currentWindowContentWidth == 0 )
			{
				Log.Info( $"hi {currentWindowContentHeight}" );
				return;
			}

			float chromeWidth = currentWindowWidth - currentWindowContentWidth;
			float chromeHeight = currentWindowHeight - currentWindowContentHeight;

			Log.Info( $"Window: {this} - Size: {Box.Rect} - WindowContentSize {WindowContent.Box.Rect} - Chrome: {chromeWidth}, {chromeHeight}" );

			Size = new Vector2( InitalInnerSize.Value.x + chromeWidth, InitalInnerSize.Value.y + chromeHeight );

			Style.Width = Size.x;
			Style.Height = Size.y;
			hasInitInnerSize = true;
		}
	}

	public Panel CreateWindowContentPanel()
	{
		// Create a new panel to hold the window content
		var contentPanel = AddChild<Panel>( "window-content" );
		return contentPanel;
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
		if ( !HasTitleBar ) return;
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

	public static event Action<Window> OnMinimised;
	public static event Action<Window> OnRestored;

	public bool IsMinimised = false;
	Vector2 PreMinimisedSize;
	Vector2 PreMinimisedPos;
	public void Minimise()
	{
		if ( !IsMinimised )
		{
			PreMinimisedSize = Box.Rect.Size;

			PreMinimisedPos = Position;

			var offset = 0;

			// offset x for other minimised windows
			foreach ( Window window in Parent.Children.OfType<Window>() )
			{
				if ( window.IsMinimised )
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
			IsMinimised = true;
			OnMinimised?.Invoke( this );
		}
		else
		{
			IsMinimised = false;
			Style.Width = PreMinimisedSize.x;
			Style.Height = PreMinimisedSize.y;

			Position = PreMinimisedPos;
			OnRestored?.Invoke( this );
		}
		Log.Info( "minimise" );
	}

	public bool IsMaximised = false;
	Vector2 PreMaximisedSize;
	Vector2 PreMaximisedPos;
	public void Maximise()
	{
		if ( !IsMaximised )
		{
			PreMaximisedSize = Box.Rect.Size;

			PreMaximisedPos = Position;

			Position = 0;

			Style.Height = Parent.Box.Rect.Size.y;
			Style.Width = Parent.Box.Rect.Size.x;
			IsMaximised = true;
		}
		else
		{
			IsMaximised = false;
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
		OnCloseAction?.Invoke();
		Delete();
	}


	// onclose action too
	public Action OnCloseAction;
	public virtual void OnClose()
	{
		// Override this to do something when the window closes
	}

	public override void Tick()
	{
		base.Tick();
		TryInitInnerSize();
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

		SetClass( "minimised", this.IsMinimised );
		SetClass( "maximised", this.IsMaximised );
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
			case "hastitlebar":
				{
					HasTitleBar = bool.Parse( value );
					if ( !HasTitleBar )
					{
						TitleBar.Delete();
					}
					this.SetClass( "notitlebar", !HasTitleBar );
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
			default:
				{
					base.SetProperty( name, value );
					return;
				}
		}
	}
}
