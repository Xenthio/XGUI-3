using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

namespace XGUI;

/// <summary>
/// A panel with custom vertical and horizontal scrollbar functionality.
/// </summary>
public class ScrollPanel : Panel
{
	public Panel VerticalScrollbar { get; private set; }
	public Button UpButton { get; private set; }
	public Panel ScrollArea { get; private set; }
	public Panel ScrollThumb { get; private set; }
	public Button DownButton { get; private set; }

	// Horizontal scrollbar elements
	public Panel HorizontalScrollbar { get; private set; }
	public Button LeftButton { get; private set; }
	public Panel HScrollArea { get; private set; }
	public Panel HScrollThumb { get; private set; }
	public Button RightButton { get; private set; }

	// Corner Panel (For when both scrollbars are visible)
	public Panel CornerPanel { get; private set; }

	// Scrollbar dragging state
	private bool _isDraggingThumb = false;
	private float _dragMouseStartY;
	private float _dragThumbStartY;

	private bool _isDraggingHThumb = false;
	private float _dragMouseStartX;
	private float _dragThumbStartX;

	// Scroll settings
	public float ScrollStep { get; set; } = 50f;
	public float PageScrollStepMultiplier { get; set; } = 0.9f;
	public bool DisableScrollBounce { get; set; } = true;

	// For tracking changes
	private Vector2 _lastScrollOffset;
	private Vector2 _lastSize;

	public ScrollPanel()
	{
		AddClass( "scrollpanel" );

		// Main vertical scrollbar container
		VerticalScrollbar = Add.Panel( "scrollbar_vertical scrollbar" );
		VerticalScrollbar.AddEventListener( "onmousedown", OnTrackMouseDown );

		// Up button
		//UpButton = VerticalScrollbar.Add.Button( "", OnUpButtonClick ); Add.Button removed.
		UpButton = VerticalScrollbar.AddChild<Button>( "" );
		UpButton.AddEventListener( "onclick", OnUpButtonClick );
		UpButton.AddClass( "scrollbar_button_up scrollbar_button" );
		UpButton.Icon = "arrow_upward";

		// Scrollable track area
		ScrollArea = VerticalScrollbar.Add.Panel( "scrollbar_track" );

		// Draggable thumb
		ScrollThumb = ScrollArea.Add.Panel( "scrollbar_thumb" );
		ScrollThumb.AddEventListener( "onmousedown", StartThumbDrag );

		// Down button
		//DownButton = VerticalScrollbar.Add.Button( "", OnDownButtonClick );
		DownButton = VerticalScrollbar.AddChild<Button>( "" );
		DownButton.AddEventListener( "onclick", OnDownButtonClick );
		DownButton.AddClass( "scrollbar_button_down scrollbar_button" );
		DownButton.Icon = "arrow_downward";

		// --- Horizontal scrollbar ---
		HorizontalScrollbar = Add.Panel( "scrollbar_horizontal scrollbar" );
		HorizontalScrollbar.AddEventListener( "onmousedown", OnHTrackMouseDown );

		// Left button
		//LeftButton = HorizontalScrollbar.Add.Button( "", OnLeftButtonClick );
		LeftButton = HorizontalScrollbar.AddChild<Button>( "" );
		LeftButton.AddEventListener( "onclick", OnLeftButtonClick );
		LeftButton.AddClass( "scrollbar_button_left scrollbar_button" );
		LeftButton.Icon = "arrow_back";

		// Scrollable horizontal track area
		HScrollArea = HorizontalScrollbar.Add.Panel( "scrollbar_track" );

		// Draggable horizontal thumb
		HScrollThumb = HScrollArea.Add.Panel( "scrollbar_thumb" );
		HScrollThumb.AddEventListener( "onmousedown", StartHThumbDrag );

		// Right button
		//RightButton = HorizontalScrollbar.Add.Button( "", OnRightButtonClick );
		RightButton = HorizontalScrollbar.AddChild<Button>( "" );
		RightButton.AddEventListener( "onclick", OnRightButtonClick );
		RightButton.AddClass( "scrollbar_button_right scrollbar_button" );
		RightButton.Icon = "arrow_forward";

		// --- Corner Panel ---
		CornerPanel = Add.Panel( "scrollbar_corner" );

		// Initialize state tracking
		UpdateScrollbarVisuals();
		UpdateHScrollbarVisuals();
		_lastScrollOffset = ScrollOffset;
		_lastSize = Vector2.Zero;
	}

	/// <summary>
	/// Handles clicking on the scrollbar track (not the thumb)
	/// </summary>
	private void OnTrackMouseDown( PanelEvent e )
	{
		// Ignore if clicking on interactive elements
		if ( e.Target == ScrollThumb || e.Target == UpButton || e.Target == DownButton ||
			ScrollThumb.HasHovered || UpButton.HasHovered || DownButton.HasHovered )
			return;

		if ( e is not MousePanelEvent me )
			return;

		// Calculate where the click happened relative to the thumb
		float clickY = me.LocalPosition.y;
		float thumbTop = ScrollThumb.Style.Top?.Value ?? 0f;
		float pageScrollAmount = Box.Rect.Height * PageScrollStepMultiplier;

		// Page up/down based on click position
		if ( clickY < thumbTop )
		{
			ScrollOffset = new Vector2( ScrollOffset.x, ScrollOffset.y - pageScrollAmount );
		}
		else if ( clickY > thumbTop + (ScrollThumb.Style.Height?.Value ?? 0f) )
		{
			ScrollOffset = new Vector2( ScrollOffset.x, ScrollOffset.y + pageScrollAmount );
		}

		e.StopPropagation();
	}

	private void OnUpButtonClick() =>
		ScrollOffset = new Vector2( ScrollOffset.x, ScrollOffset.y - ScrollStep );

	private void OnDownButtonClick() =>
		ScrollOffset = new Vector2( ScrollOffset.x, ScrollOffset.y + ScrollStep );

	private void StartThumbDrag( PanelEvent e )
	{
		if ( e.Target != ScrollThumb ) return;

		_isDraggingThumb = true;
		_dragMouseStartY = MousePosition.y;
		_dragThumbStartY = ScrollThumb.Style.Top?.Value ?? 0;
		ScrollThumb.AddClass( "active" );
		ScrollVelocity = Vector2.Zero;

		e.StopPropagation();

		// Global mouse handlers would improve drag behavior when cursor leaves the panel
		AddEventListener( "onmouseup", StopThumbDrag );
		AddEventListener( "onmousemove", UpdateThumbDrag );
	}

	private void UpdateThumbDrag( PanelEvent e )
	{
		if ( !_isDraggingThumb ) return;

		float currentMouseY = MousePosition.y;
		float mouseDeltaY = currentMouseY - _dragMouseStartY;

		float scrollableTrackHeight = GetScrollableTrackHeight();
		float thumbHeight = ScrollThumb.Style.Height?.Value ?? 20f;
		float maxThumbTop = Math.Max( 0, scrollableTrackHeight - thumbHeight );

		if ( scrollableTrackHeight <= 0 || thumbHeight <= 0 ) return;

		float newThumbTop = Math.Clamp( _dragThumbStartY + mouseDeltaY, -0.05f, maxThumbTop );
		float contentMaxScroll = ScrollSize.y;

		if ( contentMaxScroll <= 0 ) return;

		float thumbTopPercent = maxThumbTop > 0 ? newThumbTop / maxThumbTop : 0f;
		ScrollVelocity = new Vector2(
			0,//ScrollVelocity.x,
			thumbTopPercent * contentMaxScroll - _lastScrollOffset.y
		);
	}

	private void StopThumbDrag( PanelEvent e = null )
	{
		if ( !_isDraggingThumb ) return;

		_isDraggingThumb = false;
		ScrollThumb.RemoveClass( "active" );
		ScrollVelocity = Vector2.Zero;

		// Remove global listeners
		//RemoveEventListener( "onmouseup", StopThumbDrag );
		//RemoveEventListener( "onmousemove", UpdateThumbDrag );
	}

	/// <summary>
	/// Handles clicking on the horizontal scrollbar track (not the thumb)
	/// </summary>
	private void OnHTrackMouseDown( PanelEvent e )
	{
		if ( e.Target == HScrollThumb || e.Target == LeftButton || e.Target == RightButton ||
			HScrollThumb.HasHovered || LeftButton.HasHovered || RightButton.HasHovered )
			return;

		if ( e is not MousePanelEvent me )
			return;

		float clickX = me.LocalPosition.x;
		float thumbLeft = HScrollThumb.Style.Left?.Value ?? 0f;
		float pageScrollAmount = Box.Rect.Width * PageScrollStepMultiplier;

		if ( clickX < thumbLeft )
		{
			ScrollOffset = new Vector2( ScrollOffset.x - pageScrollAmount, ScrollOffset.y );
		}
		else if ( clickX > thumbLeft + (HScrollThumb.Style.Width?.Value ?? 0f) )
		{
			ScrollOffset = new Vector2( ScrollOffset.x + pageScrollAmount, ScrollOffset.y );
		}

		e.StopPropagation();
	}

	private void OnLeftButtonClick() =>
		ScrollOffset = new Vector2( ScrollOffset.x - ScrollStep, ScrollOffset.y );

	private void OnRightButtonClick() =>
		ScrollOffset = new Vector2( ScrollOffset.x + ScrollStep, ScrollOffset.y );

	private void StartHThumbDrag( PanelEvent e )
	{
		if ( e.Target != HScrollThumb ) return;

		_isDraggingHThumb = true;
		_dragMouseStartX = MousePosition.x;
		_dragThumbStartX = HScrollThumb.Style.Left?.Value ?? 0;
		HScrollThumb.AddClass( "active" );
		ScrollVelocity = Vector2.Zero;

		e.StopPropagation();

		AddEventListener( "onmouseup", StopHThumbDrag );
		AddEventListener( "onmousemove", UpdateHThumbDrag );
	}

	private void UpdateHThumbDrag( PanelEvent e )
	{
		if ( !_isDraggingHThumb ) return;

		float currentMouseX = MousePosition.x;
		float mouseDeltaX = currentMouseX - _dragMouseStartX;

		float scrollableTrackWidth = GetScrollableHTrackWidth();
		float thumbWidth = HScrollThumb.Style.Width?.Value ?? 20f;
		float maxThumbLeft = Math.Max( 0, scrollableTrackWidth - thumbWidth );

		if ( scrollableTrackWidth <= 0 || thumbWidth <= 0 ) return;

		float newThumbLeft = Math.Clamp( _dragThumbStartX + mouseDeltaX, -0.05f, maxThumbLeft );
		float contentMaxScroll = ScrollSize.x;

		if ( contentMaxScroll <= 0 ) return;

		float thumbLeftPercent = maxThumbLeft > 0 ? newThumbLeft / maxThumbLeft : 0f;
		ScrollVelocity = new Vector2(
			thumbLeftPercent * contentMaxScroll - _lastScrollOffset.x,
			0//ScrollVelocity.y
		);
	}

	private void StopHThumbDrag( PanelEvent e = null )
	{
		if ( !_isDraggingHThumb ) return;

		_isDraggingHThumb = false;
		HScrollThumb.RemoveClass( "active" );
		ScrollVelocity = Vector2.Zero;
	}

	public bool CanScrollHorizontally()
	{
		if ( HorizontalScrollbar == null || Box.Rect.Size == Vector2.Zero )
			return false;
		float contentWidth = Box.RectInner.Width + ScrollSize.x;
		return contentWidth > Box.Rect.Width;
	}

	public bool CanScrollVertically()
	{
		if ( VerticalScrollbar == null || Box.Rect.Size == Vector2.Zero )
			return false;
		float contentHeight = Box.RectInner.Height + ScrollSize.y;
		return contentHeight > Box.Rect.Height;
	}

	public override void Tick()
	{
		base.Tick();

		// Show scrollbars only if needed
		VerticalScrollbar.Style.Display = CanScrollVertically() ? DisplayMode.Flex : DisplayMode.None;
		HorizontalScrollbar.Style.Display = CanScrollHorizontally() ? DisplayMode.Flex : DisplayMode.None;
		CornerPanel.Style.Display = CanScrollHorizontally() && CanScrollVertically() ? DisplayMode.Flex : DisplayMode.None;

		// Check for scroll or size changes
		if ( ScrollOffset != _lastScrollOffset || ScrollSize != _lastSize )
		{
			UpdateScrollbarVisuals();
			UpdateHScrollbarVisuals();
			UpdateScrollbarPosition();
			UpdateHScrollbarPosition();
			UpdateCornerPosition();
			_lastScrollOffset = ScrollOffset;
			_lastSize = ScrollSize;
		}

		// Prevent scroll bounce if enabled
		if ( DisableScrollBounce && HasScrollY )
		{
			if ( ScrollOffset.y < 0 || ScrollOffset.y > ScrollSize.y )
			{
				ScrollOffset = new Vector2(
					ScrollOffset.x,
					Math.Clamp( ScrollOffset.y, 0, Math.Max( 0, ScrollSize.y ) )
				);
			}
		}
		if ( DisableScrollBounce && HasScrollX )
		{
			if ( ScrollOffset.x < 0 || ScrollOffset.x > ScrollSize.x )
			{
				ScrollOffset = new Vector2(
					Math.Clamp( ScrollOffset.x, 0, Math.Max( 0, ScrollSize.x ) ),
					ScrollOffset.y
				);
			}
		}

		UpdatePadding();
		KeepScrollOffsetInBounds();
	}
	/// <summary>
	/// Gets the size of the visible content area, excluding scrollbars.
	/// </summary>
	public Vector2 ViewportSize
	{
		get
		{
			float width = Box.RectInner.Width;
			float height = Box.RectInner.Height;

			// Subtract vertical scrollbar width if visible
			if ( VerticalScrollbar != null && VerticalScrollbar.Style.Display != DisplayMode.None )
				width -= VerticalScrollbar.Box.Rect.Width;

			// Subtract horizontal scrollbar height if visible
			if ( HorizontalScrollbar != null && HorizontalScrollbar.Style.Display != DisplayMode.None )
				height -= HorizontalScrollbar.Box.Rect.Height;

			// Clamp to non-negative values
			return new Vector2( Math.Max( 0, width ), Math.Max( 0, height ) );
		}
	}
	/// <summary>
	/// Gets the total scrollable content size, excluding scrollbars themselves.
	/// </summary>
	public Vector2 ScrollContentSize
	{
		get
		{
			// If you have a dedicated content panel, use its size:
			// return ContentPanel?.Box.Rect.Size ?? Vector2.Zero;

			// Otherwise, measure all children except scrollbars and corner panel:
			float maxRight = 0, maxBottom = 0;
			foreach ( var child in Children )
			{
				if ( child == VerticalScrollbar || child == HorizontalScrollbar || child == CornerPanel )
					continue;

				var rect = child.Box.Rect;
				maxRight = Math.Max( maxRight, rect.Right );
				maxBottom = Math.Max( maxBottom, rect.Bottom );
			}
			return new Vector2( maxRight, maxBottom );
		}
	}

	private void KeepScrollOffsetInBounds()
	{
		if ( ScrollSize == Vector2.Zero ) return;
		float maxX = Math.Max( ScrollSize.x - Box.Rect.Width + HorizontalScrollbar.Box.Rect.Width, 0 );
		float maxY = Math.Max( ScrollSize.y - Box.Rect.Height + VerticalScrollbar.Box.Rect.Height, 0 );
		ScrollOffset = new Vector2(
			Math.Clamp( ScrollOffset.x, 0, maxX ),
			Math.Clamp( ScrollOffset.y, 0, maxY )
		);
	}

	public Vector2 ClampedScrollOffset
	{
		get
		{
			float maxX = Math.Max( ScrollSize.x - Box.Rect.Width + HorizontalScrollbar.Box.Rect.Width, 0 );
			float maxY = Math.Max( ScrollSize.y - Box.Rect.Height + VerticalScrollbar.Box.Rect.Height, 0 );
			return new Vector2(
				Math.Clamp( ScrollOffset.x, 0, maxX ),
				Math.Clamp( ScrollOffset.y, 0, maxY )
			);
		}
	}

	/// <summary>
	/// Updates the scrollbar position to appear fixed in the viewport
	/// </summary>
	private void UpdateScrollbarPosition()
	{
		if ( VerticalScrollbar == null || Box.Rect.Size == Vector2.Zero ) return;

		VerticalScrollbar.Style.Top = MathF.Round( ClampedScrollOffset.y );
		VerticalScrollbar.Style.Bottom = MathF.Round( -ClampedScrollOffset.y ) + Style.PaddingBottom.GetValueOrDefault().Value;


		HorizontalScrollbar.Style.Bottom = MathF.Round( -ClampedScrollOffset.y );
	}

	private void UpdateCornerPosition()
	{
		if ( CornerPanel == null || Box.Rect.Size == Vector2.Zero ) return;
		// Position the corner panel at the bottom right of the scrollable area
		float rightPad = Style.PaddingRight.GetValueOrDefault().Value;
		float bottomPad = Style.PaddingBottom.GetValueOrDefault().Value;
		CornerPanel.Style.Width = Length.Pixels( rightPad );
		CornerPanel.Style.Height = Length.Pixels( bottomPad );
		CornerPanel.Style.Right = MathF.Round( -ClampedScrollOffset.x );
		CornerPanel.Style.Bottom = MathF.Round( -ClampedScrollOffset.y );
	}

	/// <summary>
	/// Updates the horizontal scrollbar position to appear fixed in the viewport
	/// </summary>
	private void UpdateHScrollbarPosition()
	{
		if ( HorizontalScrollbar == null || Box.Rect.Size == Vector2.Zero ) return;

		HorizontalScrollbar.Style.Left = MathF.Round( ClampedScrollOffset.x );
		HorizontalScrollbar.Style.Right = MathF.Round( -ClampedScrollOffset.x ) + Style.PaddingRight.GetValueOrDefault().Value;


		VerticalScrollbar.Style.Right = MathF.Round( -ClampedScrollOffset.x );
	}

	/// <summary>
	/// Updates the right and bottom padding to accommodate the scrollbars
	/// </summary>
	private void UpdatePadding()
	{
		float rightPad = 0f;
		float bottomPad = 0f;

		if ( HasScrollY && VerticalScrollbar != null && Box.Rect.Size != Vector2.Zero )
		{
			float scrollbarWidth = VerticalScrollbar.Box.Rect.Width;
			rightPad = scrollbarWidth > 0 ? scrollbarWidth : 0f;
		}
		if ( HasScrollX && HorizontalScrollbar != null && Box.Rect.Size != Vector2.Zero )
		{
			float scrollbarHeight = HorizontalScrollbar.Box.Rect.Height;
			bottomPad = scrollbarHeight > 0 ? scrollbarHeight : 0f;
		}

		Style.PaddingRight = Length.Pixels( rightPad );
		Style.PaddingBottom = Length.Pixels( bottomPad );
	}

	public override void FinalLayout( Vector2 offset )
	{
		base.FinalLayout( offset );
		UpdateScrollbarPosition();
		UpdateHScrollbarPosition();
		UpdateCornerPosition();
	}

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );
		UpdateScrollbarVisuals();
		UpdateHScrollbarVisuals();
		_lastScrollOffset = ScrollOffset;
	}

	/// <summary>
	/// Calculates the available height for the scrollbar thumb to move
	/// </summary>
	private float GetScrollableTrackHeight()
	{
		float upButtonHeight = UpButton?.Box.Rect.Height ?? 0f;
		float downButtonHeight = DownButton?.Box.Rect.Height ?? 0f;
		float scrollbarHeight = VerticalScrollbar?.Box.RectInner.Height ?? 0f;

		return scrollbarHeight - upButtonHeight - downButtonHeight;
	}

	/// <summary>
	/// Calculates the available width for the horizontal scrollbar thumb to move
	/// </summary>
	private float GetScrollableHTrackWidth()
	{
		float leftButtonWidth = LeftButton?.Box.Rect.Width ?? 0f;
		float rightButtonWidth = RightButton?.Box.Rect.Width ?? 0f;
		float scrollbarWidth = HorizontalScrollbar?.Box.RectInner.Width ?? 0f;

		return scrollbarWidth - leftButtonWidth - rightButtonWidth;
	}

	/// <summary>
	/// Updates the thumb size and position based on content and scroll position
	/// </summary>
	private void UpdateScrollbarVisuals()
	{
		if ( VerticalScrollbar == null || ScrollThumb == null || Box.Rect.Size == Vector2.Zero ) return;

		float viewportHeight = Box.Rect.Height;
		float contentHeight = Box.RectInner.Height + ScrollSize.y;

		// Hide scrollbar if no scrolling needed
		if ( contentHeight <= viewportHeight )
		{
			VerticalScrollbar.SetClass( "hidden", true );
			return;
		}

		VerticalScrollbar.SetClass( "hidden", false );

		float scrollableTrackHeight = GetScrollableTrackHeight();
		float thumbMinHeight = 8f;

		// Ensure we have space for the thumb
		if ( scrollableTrackHeight <= thumbMinHeight )
		{
			ScrollThumb.Style.Height = Length.Pixels( Math.Max( 0, scrollableTrackHeight ) );
			ScrollThumb.Style.Top = 0f;
			ScrollThumb.Style.Display = DisplayMode.None;
			ScrollThumb.Style.Dirty();
			return;
		}

		ScrollThumb.Style.Display = DisplayMode.Flex;

		// Calculate thumb size proportional to visible content
		float thumbHeightRatio = viewportHeight / contentHeight;
		float thumbHeight = MathF.Min(
			MathF.Max( scrollableTrackHeight * thumbHeightRatio, thumbMinHeight ),
			scrollableTrackHeight
		);
		ScrollThumb.Style.Height = Length.Pixels( thumbHeight );

		// Position the thumb based on scroll position
		float maxScroll = contentHeight - viewportHeight;
		float scrollRatio = maxScroll > 0 ? Math.Clamp( ScrollOffset.y / maxScroll, 0f, 1f ) : 0f;
		float thumbTravel = Math.Max( 0, scrollableTrackHeight - thumbHeight );
		float thumbPosition = scrollRatio * thumbTravel;

		ScrollThumb.Style.Top = Length.Pixels( thumbPosition );
		ScrollThumb.Style.Dirty();
	}

	/// <summary>
	/// Updates the horizontal thumb size and position based on content and scroll position
	/// </summary>
	private void UpdateHScrollbarVisuals()
	{
		if ( HorizontalScrollbar == null || HScrollThumb == null || Box.Rect.Size == Vector2.Zero ) return;

		float viewportWidth = Box.Rect.Width;
		float contentWidth = Box.RectInner.Width + ScrollSize.x;

		// Hide scrollbar if no scrolling needed
		if ( contentWidth <= viewportWidth )
		{
			HorizontalScrollbar.SetClass( "hidden", true );
			return;
		}

		HorizontalScrollbar.SetClass( "hidden", false );

		float scrollableTrackWidth = GetScrollableHTrackWidth();
		float thumbMinWidth = 8f;

		if ( scrollableTrackWidth <= thumbMinWidth )
		{
			HScrollThumb.Style.Width = Length.Pixels( Math.Max( 0, scrollableTrackWidth ) );
			HScrollThumb.Style.Left = 0f;
			HScrollThumb.Style.Display = DisplayMode.None;
			HScrollThumb.Style.Dirty();
			return;
		}

		HScrollThumb.Style.Display = DisplayMode.Flex;

		float thumbWidthRatio = viewportWidth / contentWidth;
		float thumbWidth = MathF.Min(
			MathF.Max( scrollableTrackWidth * thumbWidthRatio, thumbMinWidth ),
			scrollableTrackWidth
		);
		HScrollThumb.Style.Width = Length.Pixels( thumbWidth );

		float maxScroll = contentWidth - viewportWidth;
		float scrollRatio = maxScroll > 0 ? Math.Clamp( ScrollOffset.x / maxScroll, 0f, 1f ) : 0f;
		float thumbTravel = Math.Max( 0, scrollableTrackWidth - thumbWidth );
		float thumbPosition = scrollRatio * thumbTravel;

		HScrollThumb.Style.Left = Length.Pixels( thumbPosition );
		HScrollThumb.Style.Dirty();
	}
}
