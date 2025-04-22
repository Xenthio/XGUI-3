using Editor;
using Sandbox;
using Sandbox.UI;
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
			//Log.Info( e.LocalPosition );
			if ( _isDraggingHandle && SelectedPanel != null )
			{
				var pos = e.LocalPosition;
				pos -= WindowContent.Box.Rect.Position; // Adjust for WindowContent position
														// Calculate how much we've moved
				var delta = pos - _dragStartPos;
				//Log.Info( pos );
				//Log.Info( _dragStartPos );
				//Log.Info( delta );
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
			if ( SelectedPanel == null ) return;

			var rect = _originalRect;
			bool changed = false;

			// Apply resize based on which handle is active
			switch ( _activeHandle )
			{
				case 0: // Top-left
					if ( diagonalenabled )
					{
						rect = new Rect(
							rect.Left + delta.x,
							rect.Top + delta.y,
							rect.Width - delta.x,
							rect.Height - delta.y
						);
						changed = true;
					}
					break;

				case 1: // Top-middle
					if ( verticalenabled )
					{
						rect = new Rect(
							rect.Left,
							rect.Top + delta.y,
							rect.Width,
							rect.Height - delta.y
						);
						changed = true;
					}
					break;

				case 2: // Top-right
					if ( diagonalenabled )
					{
						rect = new Rect(
							rect.Left,
							rect.Top + delta.y,
							rect.Width + delta.x,
							rect.Height - delta.y
						);
						changed = true;
					}
					break;

				case 3: // Middle-right
					if ( horizontalenabled )
					{
						rect = new Rect(
							rect.Left,
							rect.Top,
							rect.Width + delta.x,
							rect.Height
						);
						changed = true;
					}
					break;

				case 4: // Bottom-right
					if ( diagonalenabled )
					{
						rect = new Rect(
							rect.Left,
							rect.Top,
							rect.Width + delta.x,
							rect.Height + delta.y
						);
						changed = true;
					}
					break;

				case 5: // Bottom-middle
					if ( verticalenabled )
					{
						rect = new Rect(
							rect.Left,
							rect.Top,
							rect.Width,
							rect.Height + delta.y
						);
						changed = true;
					}
					break;

				case 6: // Bottom-left
					if ( diagonalenabled )
					{
						rect = new Rect(
							rect.Left + delta.x,
							rect.Top,
							rect.Width - delta.x,
							rect.Height + delta.y
						);
						changed = true;
					}
					break;

				case 7: // Middle-left
					if ( horizontalenabled )
					{
						rect = new Rect(
							rect.Left + delta.x,
							rect.Top,
							rect.Width - delta.x,
							rect.Height
						);
						changed = true;
					}
					break;
			}

			// Make sure we don't have negative dimensions
			if ( rect.Width < 10 ) rect.Width = 10;
			if ( rect.Height < 10 ) rect.Height = 10;

			if ( changed )
			{
				if ( finalResize )
				{
					// Update the model through XGUIDesigner
					var node = OwnerDesigner.LookupNodeByPanel( SelectedPanel );
					if ( node != null )
					{
						// Convert rect to style attributes
						//node.Attributes["style"] = $"width: {rect.Width}px; height: {rect.Height}px;" +
						//						 $"left: {rect.Left}px; top: {rect.Top}px; position: absolute;";

						node.TryModifyStyle( "width", $"{rect.Width}px" );
						node.TryModifyStyle( "height", $"{rect.Height}px" );
						node.TryModifyStyle( "left", $"{rect.Left}px" );
						node.TryModifyStyle( "top", $"{rect.Top}px" );

						Panel.Style.Left = rect.Left;
						Panel.Style.Top = rect.Top;
						Panel.Style.Width = rect.Width;
						Panel.Style.Height = rect.Height;


						// Force update in the designer
						OwnerDesigner.ForceUpdate( false );
						OwnerDesigner.OverlayWidget.Update();
					}
				}
				else
				{
					// Just visually update for now during drag
					// We could apply temporary visual changes to the selected panel here
				}
			}
		}
	}
}
