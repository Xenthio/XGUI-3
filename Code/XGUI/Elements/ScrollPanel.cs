using Sandbox.UI;
using Sandbox.UI.Construct;
using System; // Required for Add.Panel if not already there

namespace XGUI;

public class ScrollPanel : Panel
{
	public Panel Canvas { get; private set; }
	public Panel VerticalScrollbar { get; private set; } // This is the scrollbar track
	public Button UpButton { get; private set; }
	public Panel ScrollArea { get; private set; }
	public Panel ScrollThumb { get; private set; }
	public Button DownButton { get; private set; }

	private bool _isInitializing = false;

	// Scrollbar dragging state
	private bool _isDraggingThumb = false;
	private float _dragMouseStartY; // Mouse position when drag started
	private float _dragThumbStartY; // Thumb position when drag started

	// How much to scroll by when up/down buttons are clicked or track is clicked
	public float ScrollStep { get; set; } = 50f; // For button clicks
	public float PageScrollStepMultiplier { get; set; } = 0.9f; // For track clicks (multiplies canvas height)

	private Vector2 _lastCanvasScrollOffset; // To track scroll changes

	public ScrollPanel()
	{
		_isInitializing = true;

		// The ScrollPanel itself is a row containing the Canvas and the VerticalScrollbar
		Style.FlexDirection = FlexDirection.Row;
		AddClass( "scrollpanel" );

		// Canvas where user content goes. It handles the actual scrolling.
		// Its default scrollbars should be hidden via SCSS.
		Canvas = Add.Panel( "scrollpanel_canvas" );
		// Canvas.OnScroll += UpdateScrollbarVisuals; // This event is not available

		// VerticalScrollbar (the track)
		VerticalScrollbar = Add.Panel( "scrollbar_vertical" ); // Existing class from your SCSS
		VerticalScrollbar.AddEventListener( "onmousedown", OnTrackMouseDown );


		// Up Button
		UpButton = VerticalScrollbar.Add.Button( "▲", OnUpButtonClick ); // Text can be replaced by icon class
		UpButton.AddClass( "scrollbar_button_up" );
		UpButton.AddClass( "scrollbar_button" ); // Common class for styling

		ScrollArea = VerticalScrollbar.Add.Panel( "scrollbar_track" );
		ScrollArea.AddClass( "scrollbar_track" ); // Existing class from your SCSS

		// Thumb
		ScrollThumb = ScrollArea.Add.Panel( "scrollbar_thumb" );
		ScrollThumb.AddEventListener( "onmousedown", StartThumbDrag );
		// Listen for mouseup on the window/document in case the mouse is released outside the thumb
		// This typically requires adding a global mouse event listener or handling it in a parent panel.
		// For simplicity here, we'll keep StopThumbDrag on the thumb itself, but be aware of this limitation.
		ScrollThumb.AddEventListener( "onmouseup", StopThumbDrag );


		// Down Button
		DownButton = VerticalScrollbar.Add.Button( "▼", OnDownButtonClick ); // Text can be replaced by icon class
		DownButton.AddClass( "scrollbar_button_down" );
		DownButton.AddClass( "scrollbar_button" ); // Common class for styling

		_isInitializing = false;

		// Initial update and store initial scroll offset
		UpdateScrollbarVisuals();
		_lastCanvasScrollOffset = Canvas.ScrollOffset;
	}

	private void OnTrackMouseDown( PanelEvent e )
	{
		if ( e.Target == ScrollThumb || e.Target == UpButton || e.Target == DownButton )
		{
			// Let their specific handlers take over
			return;
		}

		// Click on the track itself
		float clickY = 0; // Y position relative to the scrollbar track
		if ( e is MousePanelEvent me )
		{
			clickY = me.LocalPosition.y;
		}
		// Calculate thumb's position relative to the start of the scrollable area of the track
		float thumbTopRelativeToTrackButtons = (ScrollThumb.Style.Top?.Value ?? 0f);

		float pageScrollAmount = Canvas.Box.Rect.Height * PageScrollStepMultiplier;

		if ( clickY < thumbTopRelativeToTrackButtons )
		{
			Canvas.ScrollOffset = new Vector2( Canvas.ScrollOffset.x, Canvas.ScrollOffset.y - pageScrollAmount );
		}
		else if ( clickY > thumbTopRelativeToTrackButtons + (ScrollThumb.Style.Height?.Value ?? 0f) )
		{
			Canvas.ScrollOffset = new Vector2( Canvas.ScrollOffset.x, Canvas.ScrollOffset.y + pageScrollAmount );
		}
		// Visual update will be handled by Tick or OnAfterTreeRender
		e.StopPropagation();
	}


	private void OnUpButtonClick()
	{
		Canvas.ScrollOffset = new Vector2( Canvas.ScrollOffset.x, Canvas.ScrollOffset.y - ScrollStep );
		// Visual update will be handled by Tick or OnAfterTreeRender
	}

	private void OnDownButtonClick()
	{
		Canvas.ScrollOffset = new Vector2( Canvas.ScrollOffset.x, Canvas.ScrollOffset.y + ScrollStep );
		// Visual update will be handled by Tick or OnAfterTreeRender
	}

	private void StartThumbDrag( PanelEvent e )
	{
		if ( e.Target != ScrollThumb ) return;
		_isDraggingThumb = true;
		// Use global mouse position for drag calculations to be robust
		_dragMouseStartY = MousePosition.y;
		_dragThumbStartY = ScrollThumb.Style.Top?.Value ?? 0; // Get current top in pixels
		ScrollThumb.AddClass( "active" ); // For styling the thumb while dragging
										  // Add a global mouse move listener, or listen on a parent that covers the screen
										  // For now, we rely on Tick and mouse being over the panel.
										  // Consider adding listeners to Scene.Document or similar for robust off-panel dragging.
		e.StopPropagation();
	}

	private void StopThumbDrag() // This should ideally be on a global mouse up listener
	{
		if ( !_isDraggingThumb ) return;
		_isDraggingThumb = false;
		ScrollThumb.RemoveClass( "active" );
		// Remove global mouse move listener if one was added in StartThumbDrag
	}

	public override void Tick()
	{
		base.Tick();

		if ( _isDraggingThumb )
		{
			// This part handles dragging the thumb
			float currentMouseY = MousePosition.y;
			float mouseDeltaY = currentMouseY - _dragMouseStartY;

			float scrollableTrackHeight = GetScrollableTrackHeight();
			float thumbHeight = ScrollThumb.Style.Height?.Value ?? 20f;
			float maxThumbTop = scrollableTrackHeight - thumbHeight;
			maxThumbTop = MathF.Max( 0, maxThumbTop );

			if ( scrollableTrackHeight <= 0 || thumbHeight <= 0 )
			{
				// No scrollable area or thumb, potentially stop drag or handle appropriately
			}
			else
			{
				float newThumbTop = _dragThumbStartY + mouseDeltaY;
				newThumbTop = Math.Clamp( newThumbTop, 0, maxThumbTop );

				float contentMaxScroll = Canvas.ScrollSize.y - Canvas.Box.Rect.Height;
				if ( contentMaxScroll <= 0 )
				{
					// No content to scroll
				}
				else
				{
					float thumbTopPercent = 0f;
					if ( maxThumbTop > 0 )
					{
						thumbTopPercent = newThumbTop / maxThumbTop;
					}
					Canvas.ScrollOffset = new Vector2( Canvas.ScrollOffset.x, thumbTopPercent * contentMaxScroll );
				}
			}
		}

		// Check if canvas scroll offset has changed by any means (drag, buttons, wheel, etc.)
		if ( Canvas != null && Canvas.ScrollOffset != _lastCanvasScrollOffset )
		{
			UpdateScrollbarVisuals();
			_lastCanvasScrollOffset = Canvas.ScrollOffset;
		}
	}

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );
		UpdateScrollbarVisuals();
		if ( Canvas != null ) // Ensure canvas is not null
		{
			_lastCanvasScrollOffset = Canvas.ScrollOffset;
		}
	}


	private float GetScrollableTrackHeight()
	{
		// Ensure buttons are valid and have rendered to get their height
		float upButtonHeight = UpButton?.Box.Rect.Height ?? 0f;
		float downButtonHeight = DownButton?.Box.Rect.Height ?? 0f;

		// Ensure VerticalScrollbar is valid and has rendered
		float scrollbarHeight = VerticalScrollbar?.Box.Rect.Height ?? 0f;

		// This is the height of the area where the thumb can actually move
		return scrollbarHeight - upButtonHeight - downButtonHeight;
	}

	private void UpdateScrollbarVisuals()
	{
		if ( Canvas == null || VerticalScrollbar == null || ScrollThumb == null || UpButton == null || DownButton == null )
			return;

		// Ensure elements have computed geometry. This might be an issue if called too early.
		if ( Canvas.Box.Rect.Size == Vector2.Zero || VerticalScrollbar.Box.Rect.Size == Vector2.Zero )
		{
			// Postpone update if geometry is not ready, or rely on OnAfterTreeRender
			return;
		}

		float canvasVisibleHeight = Canvas.Box.Rect.Height;
		float canvasTotalContentHeight = Canvas.ScrollSize.y;

		if ( canvasTotalContentHeight <= canvasVisibleHeight )
		{
			VerticalScrollbar.SetClass( "hidden", true );
			return;
		}

		VerticalScrollbar.SetClass( "hidden", false );

		float scrollableTrackHeight = GetScrollableTrackHeight();
		float thumbMinHeight = 20f;

		if ( scrollableTrackHeight <= thumbMinHeight )
		{
			ScrollThumb.Style.Height = Length.Pixels( Math.Max( 0, scrollableTrackHeight ) );
			ScrollThumb.Style.Top = 0f;
			ScrollThumb.Style.Display = DisplayMode.None; // Hide thumb if no space
			ScrollThumb.Style.Dirty();
			return;
		}
		ScrollThumb.Style.Display = DisplayMode.Flex; // Show thumb if there is space

		// Calculate thumb height
		float thumbHeightRatio = canvasVisibleHeight / canvasTotalContentHeight;
		float calculatedThumbHeight = MathF.Max( scrollableTrackHeight * thumbHeightRatio, thumbMinHeight );
		calculatedThumbHeight = MathF.Min( calculatedThumbHeight, scrollableTrackHeight );
		ScrollThumb.Style.Height = Length.Pixels( calculatedThumbHeight );

		// Calculate thumb position
		float scrollableContentRange = canvasTotalContentHeight - canvasVisibleHeight;
		float currentScrollRatio = 0f;
		if ( scrollableContentRange > 0 )
		{
			currentScrollRatio = Canvas.ScrollOffset.y / scrollableContentRange;
		}
		currentScrollRatio = Math.Clamp( currentScrollRatio, 0f, 1f );

		float availableThumbTravelSpace = scrollableTrackHeight - calculatedThumbHeight;
		availableThumbTravelSpace = MathF.Max( 0, availableThumbTravelSpace );

		float thumbTopOffset = currentScrollRatio * availableThumbTravelSpace;

		ScrollThumb.Style.Top = Length.Pixels( thumbTopOffset );
		ScrollThumb.Style.Dirty();
	}

	protected override void OnChildAdded( Panel child )
	{
		if ( _isInitializing )
		{
			base.OnChildAdded( child );
			return;
		}

		if ( child != Canvas && child != VerticalScrollbar && child.Parent != Canvas )
		{
			Canvas.AddChild( child );
		}
		else
		{
			base.OnChildAdded( child );
		}
	}
}
