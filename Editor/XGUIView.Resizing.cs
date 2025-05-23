﻿using Editor;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XGUI
{
	public partial class XGUIView
	{
		private void ResizeMousePress( MouseEvent e )
		{
			if ( e.Button == MouseButtons.Left && SelectedPanel != null )
			{
				// Check if we clicked on a resize handle
				int handleIndex = GetHandleAtPosition( e.LocalPosition );

				if ( handleIndex >= 0 )
				{
					_isDraggingHandle = true;
					_activeHandle = handleIndex;
					_dragStartPos = e.LocalPosition - WindowContent.Box.Rect.Position;
					_originalRect = SelectedPanel.Box.Rect;
					_originalRect.Position -= WindowContent.Box.Rect.Position; // Adjust for WindowContent position
					e.Accepted = true;
				}
			}
		}
		private void ResizeMouseMove( MouseEvent e )
		{
			if ( _isDraggingHandle && SelectedPanel != null )
			{
				var pos = e.LocalPosition;
				// Calculate how much we've moved
				var delta = pos - _dragStartPos;

				// Apply snapping during resize
				delta = ApplyResizeSnapping( delta );
				delta -= SelectedPanel.Parent.Box.Rect.Position; // Adjust for WindowContent position


				// Apply the resize based on which handle is being dragged
				if ( ShouldOnlyHorizontalResize( SelectedPanel ) )
				{
					ApplyResize( delta, true, verticalenabled: false, diagonalenabled: false );
				}
				else
				{
					ApplyResize( delta, true );
				}
				e.Accepted = true;
			}
		}
		private void ResizeMouseReleased( MouseEvent e )
		{
			if ( _isDraggingHandle && SelectedPanel != null )
			{

				_isDraggingHandle = false;
				_activeHandle = -1;
				e.Accepted = true;
			}
		}
		/// <summary>
		/// Elements like buttons and textboxes should only resize horizontally
		/// </summary>
		/// <param name="position"></param>
		/// <returns></returns>
		private bool ShouldOnlyHorizontalResize( Panel panel )
		{
			if ( panel == null ) return false;
			// Check if the panel is a button or textbox
			if ( panel is Sandbox.UI.Button || panel is Sandbox.UI.TextEntry )
			{
				return true;
			}
			// Check if the panel has a specific class that indicates horizontal resizing
			if ( panel.Class.Any( x => x == "horizontal-resize-only" ) )
			{
				return true;
			}
			return false;
		}
		private void DrawResizeHandles( Rect rect, bool verticalenabled = true, bool horizontalenabled = true, bool diagonalenabled = true )
		{
			float handleSize = 3;

			Paint.SetBrush( Color.White );
			Paint.SetPen( Color.Black, 1.0f );

			// Draw the 8 resize handles (clockwise from top-left)
			Rect[] handleRects = new Rect[8];

			// Top-left
			if ( diagonalenabled ) handleRects[0] = new Rect( rect.Left - handleSize, rect.Top - handleSize, handleSize * 2, handleSize * 2 );
			// Top-middle
			if ( verticalenabled ) handleRects[1] = new Rect( rect.Left + (rect.Width - handleSize) / 2, rect.Top - handleSize, handleSize * 2, handleSize * 2 );
			// Top-right
			if ( diagonalenabled ) handleRects[2] = new Rect( rect.Right - handleSize, rect.Top - handleSize, handleSize * 2, handleSize * 2 );
			// Middle-right
			if ( horizontalenabled ) handleRects[3] = new Rect( rect.Right - handleSize, rect.Top + (rect.Height - handleSize) / 2, handleSize * 2, handleSize * 2 );
			// Bottom-right
			if ( diagonalenabled ) handleRects[4] = new Rect( rect.Right - handleSize, rect.Bottom - handleSize, handleSize * 2, handleSize * 2 );
			// Bottom-middle
			if ( verticalenabled ) handleRects[5] = new Rect( rect.Left + (rect.Width - handleSize) / 2, rect.Bottom - handleSize, handleSize * 2, handleSize * 2 );
			// Bottom-left
			if ( diagonalenabled ) handleRects[6] = new Rect( rect.Left - handleSize, rect.Bottom - handleSize, handleSize * 2, handleSize * 2 );
			// Middle-left
			if ( horizontalenabled ) handleRects[7] = new Rect( rect.Left - handleSize, rect.Top + (rect.Height - handleSize) / 2, handleSize * 2, handleSize * 2 );

			// Draw all handles
			for ( int i = 0; i < handleRects.Length; i++ )
			{
				Paint.DrawRect( handleRects[i] );
			}
		}

		private int GetHandleAtPosition( Vector2 position )
		{
			if ( SelectedPanel == null ) return -1;

			var rect = SelectedPanel.Box.Rect;
			float handleSize = 3;

			// Check each handle in order
			Rect[] handleRects = new Rect[8];

			// Top-left
			handleRects[0] = new Rect( rect.Left - handleSize, rect.Top - handleSize, handleSize * 2, handleSize * 2 );
			// Top-middle
			handleRects[1] = new Rect( rect.Left + (rect.Width - handleSize) / 2, rect.Top - handleSize, handleSize * 2, handleSize * 2 );
			// Top-right
			handleRects[2] = new Rect( rect.Right - handleSize, rect.Top - handleSize, handleSize * 2, handleSize * 2 );
			// Middle-right
			handleRects[3] = new Rect( rect.Right - handleSize, rect.Top + (rect.Height - handleSize) / 2, handleSize * 2, handleSize * 2 );
			// Bottom-right
			handleRects[4] = new Rect( rect.Right - handleSize, rect.Bottom - handleSize, handleSize * 2, handleSize * 2 );
			// Bottom-middle
			handleRects[5] = new Rect( rect.Left + (rect.Width - handleSize) / 2, rect.Bottom - handleSize, handleSize * 2, handleSize * 2 );
			// Bottom-left
			handleRects[6] = new Rect( rect.Left - handleSize, rect.Bottom - handleSize, handleSize * 2, handleSize * 2 );
			// Middle-left
			handleRects[7] = new Rect( rect.Left - handleSize, rect.Top + (rect.Height - handleSize) / 2, handleSize * 2, handleSize * 2 );

			// Check if position is inside any handle
			for ( int i = 0; i < handleRects.Length; i++ )
			{
				if ( handleRects[i].IsInside( position ) )
					return i;
			}

			return -1;
		}

		private void UpdateResizeCursor( int handleIndex, bool verticalenabled = true, bool horizontalenabled = true, bool diagonalenabled = true )
		{
			switch ( handleIndex )
			{
				case 0: // Top-left
				case 4: // Bottom-right
					if ( diagonalenabled )
						Cursor = CursorShape.SizeFDiag;
					break;

				case 2: // Top-right
				case 6: // Bottom-left
					if ( diagonalenabled )
						Cursor = CursorShape.SizeBDiag;
					break;

				case 1: // Top-middle
				case 5: // Bottom-middle
					if ( verticalenabled )
						Cursor = CursorShape.SizeV;
					break;

				case 3: // Middle-right
				case 7: // Middle-left
					if ( horizontalenabled )
						Cursor = CursorShape.SizeH;
					break;
			}
		}
		private void ApplyResize( Vector2 delta, bool finalResize = false, bool verticalenabled = true, bool horizontalenabled = true, bool diagonalenabled = true )
		{
			if ( SelectedPanel == null || _activeHandle < 0 )
				return;

			// Calculate new rectangle based on delta
			var oldRect = _originalRect;
			var newRect = oldRect;

			// Get the panel's alignment settings
			var alignment = GetPanelAlignment( SelectedPanel );
			var node = OwnerDesigner.LookupNodeByPanel( SelectedPanel );

			// Determine which edges we're adjusting
			bool isLeftEdge = (_activeHandle == 0 || _activeHandle == 7 || _activeHandle == 6) && horizontalenabled;
			bool isRightEdge = (_activeHandle == 2 || _activeHandle == 3 || _activeHandle == 4) && horizontalenabled;
			bool isTopEdge = (_activeHandle == 0 || _activeHandle == 1 || _activeHandle == 2) && verticalenabled;
			bool isBottomEdge = (_activeHandle == 4 || _activeHandle == 5 || _activeHandle == 6) && verticalenabled;

			// Parent dimensions for calculations
			float parentLeft = (SelectedPanel.Parent?.Box.Rect.Left ?? 0);
			float parentTop = (SelectedPanel.Parent?.Box.Rect.Top ?? 0);
			float parentWidth = (SelectedPanel.Parent?.Box.Rect.Width ?? 400);
			float parentHeight = (SelectedPanel.Parent?.Box.Rect.Height ?? 300);

			// Apply changes based on which handle is being dragged
			if ( isLeftEdge )
			{
				float newLeft = oldRect.Left + delta.x;
				newRect.Left = newLeft;
				newRect.Width = Math.Max( oldRect.Width - delta.x, 5 );
			}
			else if ( isRightEdge )
			{
				newRect.Width = Math.Max( oldRect.Width + delta.x, 5 );
			}

			if ( isTopEdge )
			{
				float newTop = oldRect.Top + delta.y;
				newRect.Top = newTop;
				newRect.Height = Math.Max( oldRect.Height - delta.y, 5 );
			}
			else if ( isBottomEdge )
			{
				newRect.Height = Math.Max( oldRect.Height + delta.y, 5 );
			}

			// Check if SelectedPanel is a Window
			bool isWindow = SelectedPanel is Window;

			// Apply visual preview - for Windows use width/height attributes, for others use style
			if ( isWindow )
			{
				// For Window panels, update width/height attributes directly
				SelectedPanel.SetAttribute( "width", $"{newRect.Width}" );
				SelectedPanel.SetAttribute( "height", $"{newRect.Height}" );

				if ( isLeftEdge && alignment.Left )
					SelectedPanel.SetAttribute( "x", $"{newRect.Left - parentLeft}" );
				if ( isTopEdge && alignment.Top )
					SelectedPanel.SetAttribute( "y", $"{newRect.Top - parentTop}" );
			}
			else
			{
				// For non-Window panels, update the style as before
				SelectedPanel.Style.Width = newRect.Width;
				SelectedPanel.Style.Height = newRect.Height;

				if ( isLeftEdge && alignment.Left )
					SelectedPanel.Style.Left = newRect.Left - parentLeft;
				if ( isTopEdge && alignment.Top )
					SelectedPanel.Style.Top = newRect.Top - parentTop;
			}

			// If this is the final resize, update the node's style properties
			if ( finalResize && node != null )
			{
				// Get current styles to check anchoring state
				Dictionary<string, string> currentStyles = new Dictionary<string, string>();
				if ( node.Attributes.TryGetValue( "style", out var styleString ) )
				{
					foreach ( var part in styleString.Split( ';', StringSplitOptions.RemoveEmptyEntries ) )
					{
						var kv = part.Split( ':', 2 );
						if ( kv.Length == 2 )
							currentStyles[kv[0].Trim()] = kv[1].Trim();
					}
				}

				// Check if we have both horizontal anchors
				bool hasHorizontalStretch = alignment.Left && alignment.Right;
				bool hasVerticalStretch = alignment.Top && alignment.Bottom;

				if ( isWindow )
				{
					// For Window panels, update width/height attributes in the markup node
					if ( !hasHorizontalStretch && (isLeftEdge || isRightEdge) )
					{
						node.Attributes["width"] = $"{newRect.Width}";
					}

					if ( !hasVerticalStretch && (isTopEdge || isBottomEdge) )
					{
						node.Attributes["height"] = $"{newRect.Height}";
					}

					// Update position attributes if needed
					if ( isLeftEdge && alignment.Left )
					{
						node.Attributes["x"] = $"{newRect.Left - parentLeft}";
					}

					if ( isTopEdge && alignment.Top )
					{
						node.Attributes["y"] = $"{newRect.Top - parentTop}";
					}
				}
				else
				{
					// Only update width if we're not stretching horizontally
					if ( !hasHorizontalStretch && (isLeftEdge || isRightEdge) )
					{
						node.TryModifyStyle( "width", $"{newRect.Width}px" );
					}

					// Only update height if we're not stretching vertically AND the resize operation involved vertical changes
					if ( !hasVerticalStretch && (isTopEdge || isBottomEdge) )
					{
						node.TryModifyStyle( "height", $"{newRect.Height}px" );
					}

					// Update the position properties based on which handle was dragged and alignment
					if ( SelectedPanel.ComputedStyle?.Position == PositionMode.Absolute )
					{
						// When resizing from left edge, update left position
						if ( isLeftEdge && alignment.Left )
						{
							node.TryModifyStyle( "left", $"{newRect.Left - parentLeft}px" );
						}

						// When resizing from top edge, update top position
						if ( isTopEdge && alignment.Top )
						{
							node.TryModifyStyle( "top", $"{newRect.Top - parentTop}px" );
						}

						// When resizing from right edge, update right position
						if ( isRightEdge && alignment.Right )
						{
							float rightValue = parentWidth - (newRect.Left + newRect.Width - parentLeft);
							node.TryModifyStyle( "right", $"{rightValue}px" );
						}

						// When resizing from bottom edge, update bottom position
						if ( isBottomEdge && alignment.Bottom )
						{
							float bottomValue = parentHeight - (newRect.Top + newRect.Height - parentTop);
							node.TryModifyStyle( "bottom", $"{bottomValue}px" );
						}
					}
				}

				// Force update in the designer
				OwnerDesigner.ForceUpdate( false );
			}

			SelectedPanel.Style.Dirty();
		}

	}
}
