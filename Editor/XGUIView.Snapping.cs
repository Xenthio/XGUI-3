using Editor;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XGUI
{
	public partial class XGUIView
	{

		private bool _isSnappedX = false;
		private bool _isSnappedY = false;
		private Vector2 _snapLineStartX = Vector2.Zero;
		private Vector2 _snapLineEndX = Vector2.Zero;
		private Vector2 _snapLineStartY = Vector2.Zero;
		private Vector2 _snapLineEndY = Vector2.Zero;
		private Rect? _snappingToSiblingRectX = null;
		private Rect? _snappingToSiblingRectY = null;

		private struct SnapCandidate
		{
			public float AbsOffset; // Absolute distance to snap target
			public float Offset;    // Signed offset needed to snap
			public Vector2 LineStart; // Guide line start (parent local space)
			public Vector2 LineEnd;   // Guide line end (parent local space)
			public Rect? TargetRect; // Rect of the target (parent local space), null for parent edge

			public SnapCandidate( float dragValue, float targetValue, Vector2 lineStart, Vector2 lineEnd, Rect? targetRect )
			{
				Offset = targetValue - dragValue;
				AbsOffset = Math.Abs( Offset );
				LineStart = lineStart;
				LineEnd = lineEnd;
				TargetRect = targetRect;
			}
		}

		private void DrawSnappingGuides()
		{
			// Draw snapping guides when dragging or resizing
			if ( (isDragging && DraggingPanel != null) || (_isDraggingHandle && SelectedPanel != null) )
			{
				// Adjust coordinates for WindowContent position
				Vector2 offset = Vector2.Zero;// WindowContent.Box.Rect.Position;

				// Select the active panel based on whether we're dragging or resizing
				Panel activePanel = isDragging ? DraggingPanel : SelectedPanel;

				// Draw horizontal snapping guide
				if ( _isSnappedY )
				{
					Paint.ClearPen();

					// Check if this is a center snap (using the midpoint of the panel)
					bool isCenterSnap = Math.Abs( _snapLineStartY.y - (activePanel.Box.Rect.Top + activePanel.Box.Rect.Height / 2 - offset.y) ) < 1f;

					// Use a different color for center snaps
					if ( isCenterSnap )
					{
						Paint.SetPen( Color.Magenta.WithAlpha( 0.8f ), 1.0f, PenStyle.Dash );
					}
					else
					{
						Paint.SetPen( Color.Yellow.WithAlpha( 0.8f ), 1.0f, PenStyle.Dash );
					}

					Paint.DrawLine( _snapLineStartY + offset, _snapLineEndY + offset );
				}

				// Draw vertical snapping guide
				if ( _isSnappedX )
				{
					Paint.ClearPen();

					// Check if this is a center snap (using the midpoint of the panel)
					bool isCenterSnap = Math.Abs( _snapLineStartX.x - (activePanel.Box.Rect.Left + activePanel.Box.Rect.Width / 2 - offset.x) ) < 1f;

					// Use a different color for center snaps
					if ( isCenterSnap )
					{
						Paint.SetPen( Color.Magenta.WithAlpha( 0.8f ), 1.0f, PenStyle.Dash );
					}
					else
					{
						Paint.SetPen( Color.Yellow.WithAlpha( 0.8f ), 1.0f, PenStyle.Dash );
					}

					Paint.DrawLine( _snapLineStartX + offset, _snapLineEndX + offset );
				}

				// Draw a rectangle around the snapping target
				if ( _snappingToSiblingRectX != null )
				{
					// No fill
					Paint.ClearBrush();

					Paint.SetPen( Color.Orange.WithAlpha( 0.5f ), 1.0f, PenStyle.Dash );
					Paint.DrawRect( _snappingToSiblingRectX.Value );
				}
				if ( _snappingToSiblingRectY != null )
				{
					// No fill
					Paint.ClearBrush();

					Paint.SetPen( Color.Orange.WithAlpha( 0.5f ), 1.0f, PenStyle.Dash );
					Paint.DrawRect( _snappingToSiblingRectY.Value );
				}
			}
		}

		private Vector2 ApplySnappingToPosition( Vector2 proposedPosition )
		{
			if ( DraggingPanel?.Parent == null )
				return proposedPosition;

			var parent = DraggingPanel.Parent;
			float snapDistance = 10f;
			float margin = 6f; // Margin for parent and sibling snapping

			// --- Coordinate Setup (Parent Local Space) ---
			Rect parentBounds = parent.Box.RectInner; // Inner bounds of the parent

			Vector2 panelSize = DraggingPanel.Box.Rect.Size;
			Rect proposedRect = new Rect( proposedPosition, panelSize );

			// Dragged panel features in parent local space
			float dragLeft = proposedRect.Left;
			float dragRight = proposedRect.Right;
			float dragTop = proposedRect.Top;
			float dragBottom = proposedRect.Bottom;
			float dragCenterX = proposedRect.Center.x;
			float dragCenterY = proposedRect.Center.y;

			var siblings = parent.Children.OfType<Panel>()
				.Where( p => p != DraggingPanel )
				.Select( p => p.Box.Rect ) // Sibling Rects are already in parent local space
				.ToList();

			// --- Candidate Generation ---
			List<SnapCandidate> xCandidates = new();
			List<SnapCandidate> yCandidates = new();

			// Parent Snapping
			float parentInnerX = parentBounds.Position.x;
			float parentInnerY = parentBounds.Position.y;
			float parentClientLeft = parentInnerX + margin;
			float parentClientRight = parentInnerX + parentBounds.Width - margin;
			float parentClientTop = parentInnerY + margin;
			float parentClientBottom = parentInnerY + parentBounds.Height - margin;
			float parentClientCenterX = parentInnerX + parentBounds.Width / 2f;
			float parentClientCenterY = parentInnerY + parentBounds.Height / 2f;

			// X-Axis (Parent) - Use adjusted targets and define lines within inner bounds
			xCandidates.Add( new SnapCandidate( dragLeft, parentClientLeft, new( parentClientLeft, parentInnerY ), new( parentClientLeft, parentInnerY + parentBounds.Height ), parentBounds ) );
			xCandidates.Add( new SnapCandidate( dragRight, parentClientRight, new( parentClientRight, parentInnerY ), new( parentClientRight, parentInnerY + parentBounds.Height ), parentBounds ) );
			xCandidates.Add( new SnapCandidate( dragCenterX, parentClientCenterX, new( parentClientCenterX, parentInnerY ), new( parentClientCenterX, parentInnerY + parentBounds.Height ), parentBounds ) );

			// Y-Axis (Parent) - Use adjusted targets and define lines within inner bounds
			yCandidates.Add( new SnapCandidate( dragTop, parentClientTop, new( parentInnerX, parentClientTop ), new( parentInnerX + parentBounds.Width, parentClientTop ), parentBounds ) );
			yCandidates.Add( new SnapCandidate( dragBottom, parentClientBottom, new( parentInnerX, parentClientBottom ), new( parentInnerX + parentBounds.Width, parentClientBottom ), parentBounds ) );
			yCandidates.Add( new SnapCandidate( dragCenterY, parentClientCenterY, new( parentInnerX, parentClientCenterY ), new( parentInnerX + parentBounds.Width, parentClientCenterY ), parentBounds ) );

			// Sibling Snapping
			foreach ( var siblingRect in siblings )
			{
				float sLeft = siblingRect.Left;
				float sRight = siblingRect.Right;
				float sTop = siblingRect.Top;
				float sBottom = siblingRect.Bottom;
				float sCenterX = siblingRect.Center.x;
				float sCenterY = siblingRect.Center.y;

				// X-Axis (Siblings)
				xCandidates.Add( new SnapCandidate( dragLeft, sLeft, new( sLeft, 0 ), new( sLeft, parentBounds.Height ), siblingRect ) ); // L-L
				xCandidates.Add( new SnapCandidate( dragRight, sRight, new( sRight, 0 ), new( sRight, parentBounds.Height ), siblingRect ) ); // R-R
				xCandidates.Add( new SnapCandidate( dragCenterX, sCenterX, new( sCenterX, 0 ), new( sCenterX, parentBounds.Height ), siblingRect ) ); // C-C
				xCandidates.Add( new SnapCandidate( dragLeft, sRight, new( sRight, 0 ), new( sRight, parentBounds.Height ), siblingRect ) ); // L-R
				xCandidates.Add( new SnapCandidate( dragRight, sLeft, new( sLeft, 0 ), new( sLeft, parentBounds.Height ), siblingRect ) ); // R-L
				xCandidates.Add( new SnapCandidate( dragLeft, sRight + margin, new( sRight + margin, 0 ), new( sRight + margin, parentBounds.Height ), siblingRect ) ); // L-(R+m)
				xCandidates.Add( new SnapCandidate( dragRight, sLeft - margin, new( sLeft - margin, 0 ), new( sLeft - margin, parentBounds.Height ), siblingRect ) ); // R-(L-m)

				// Y-Axis (Siblings)
				yCandidates.Add( new SnapCandidate( dragTop, sTop, new( 0, sTop ), new( parentBounds.Width, sTop ), siblingRect ) ); // T-T
				yCandidates.Add( new SnapCandidate( dragBottom, sBottom, new( 0, sBottom ), new( parentBounds.Width, sBottom ), siblingRect ) ); // B-B
				yCandidates.Add( new SnapCandidate( dragCenterY, sCenterY, new( 0, sCenterY ), new( parentBounds.Width, sCenterY ), siblingRect ) ); // C-C
				yCandidates.Add( new SnapCandidate( dragTop, sBottom, new( 0, sBottom ), new( parentBounds.Width, sBottom ), siblingRect ) ); // T-B
				yCandidates.Add( new SnapCandidate( dragBottom, sTop, new( 0, sTop ), new( parentBounds.Width, sTop ), siblingRect ) ); // B-T
				yCandidates.Add( new SnapCandidate( dragTop, sBottom + margin, new( 0, sBottom + margin ), new( parentBounds.Width, sBottom + margin ), siblingRect ) ); // T-(B+m)
				yCandidates.Add( new SnapCandidate( dragBottom, sTop - margin, new( 0, sTop - margin ), new( parentBounds.Width, sTop - margin ), siblingRect ) ); // B-(T-m)
			}

			// Filter candidates within snap distance
			xCandidates = xCandidates.Where( c => c.AbsOffset < snapDistance ).ToList();
			yCandidates = yCandidates.Where( c => c.AbsOffset < snapDistance ).ToList();

			// --- Apply Best Snap ---
			Vector2 snappedPosition = proposedPosition;
			_isSnappedX = false;
			_isSnappedY = false;
			_snappingToSiblingRectX = null;
			_snappingToSiblingRectY = null;

			if ( xCandidates.Count > 0 )
			{
				var bestX = xCandidates.OrderBy( c => c.AbsOffset ).First();
				snappedPosition.x += bestX.Offset;
				_isSnappedX = true;
				_snapLineStartX = bestX.LineStart;
				_snapLineEndX = bestX.LineEnd;
				_snappingToSiblingRectX = bestX.TargetRect; // Don't highlight parent
			}

			if ( yCandidates.Count > 0 )
			{
				var bestY = yCandidates.OrderBy( c => c.AbsOffset ).First();
				snappedPosition.y += bestY.Offset;
				_isSnappedY = true;
				_snapLineStartY = bestY.LineStart;
				_snapLineEndY = bestY.LineEnd;
				_snappingToSiblingRectY = bestY.TargetRect; // Don't highlight parent
			}

			return snappedPosition;
		}
		/// <summary>
		/// Applies snapping logic to resize operations.
		/// Input delta is the mouse movement vector (assumed relative to parent's local space for simplicity here,
		/// ensure conversion if delta originates from XGUIView's space).
		/// Returns the adjusted delta based on snapping.
		/// </summary>
		private Vector2 ApplyResizeSnapping( Vector2 delta )
		{
			if ( SelectedPanel?.Parent == null || _activeHandle < 0 )
				return delta;

			var parent = SelectedPanel.Parent;
			float snapDistance = 10f;
			float margin = 6f;
			float minSize = 5f; // Minimum panel size during resize

			// --- Coordinate Setup (Parent Local Space) ---
			Rect parentBounds = parent.Box.RectInner;
			Rect originalRect = _originalRect; // Assumes _originalRect is already in parent local space

			// Determine which edges are being moved
			bool isLeftEdge = (_activeHandle == 0 || _activeHandle == 7 || _activeHandle == 6);
			bool isRightEdge = (_activeHandle == 2 || _activeHandle == 3 || _activeHandle == 4);
			bool isTopEdge = (_activeHandle == 0 || _activeHandle == 1 || _activeHandle == 2);
			bool isBottomEdge = (_activeHandle == 4 || _activeHandle == 5 || _activeHandle == 6);

			// Calculate the proposed rect *after* applying the raw delta
			Rect proposedRect = originalRect;
			if ( isLeftEdge ) { proposedRect.Left += delta.x; proposedRect.Width = Math.Max( minSize, originalRect.Width - delta.x ); }
			else if ( isRightEdge ) { proposedRect.Width = Math.Max( minSize, originalRect.Width + delta.x ); }
			if ( isTopEdge ) { proposedRect.Top += delta.y; proposedRect.Height = Math.Max( minSize, originalRect.Height - delta.y ); }
			else if ( isBottomEdge ) { proposedRect.Height = Math.Max( minSize, originalRect.Height + delta.y ); }

			// Features of the *proposed* rectangle's moving edges
			float movingLeft = proposedRect.Left;
			float movingRight = proposedRect.Right;
			float movingTop = proposedRect.Top;
			float movingBottom = proposedRect.Bottom;
			// We might also want to snap the center if only one axis is moving
			float movingCenterX = proposedRect.Center.x;
			float movingCenterY = proposedRect.Center.y;


			var siblings = parent.Children.OfType<Panel>()
				.Where( p => p != SelectedPanel )
				.Select( p => p.Box.Rect )
				.ToList();

			// --- Candidate Generation ---
			List<SnapCandidate> xCandidates = new();
			List<SnapCandidate> yCandidates = new();

			// Parent Snapping Targets
			float parentInnerX = parentBounds.Position.x;
			float parentInnerY = parentBounds.Position.y;
			float parentClientLeft = parentInnerX + margin;
			float parentClientRight = parentInnerX + parentBounds.Width - margin;
			float parentClientTop = parentInnerY + margin;
			float parentClientBottom = parentInnerY + parentBounds.Height - margin;
			float parentClientCenterX = parentInnerX + parentBounds.Width / 2f;
			float parentClientCenterY = parentInnerY + parentBounds.Height / 2f;


			// X-Axis Snapping (Parent & Siblings)
			if ( isLeftEdge || isRightEdge )
			{
				float edgeToSnapX = isLeftEdge ? movingLeft : movingRight;
				Action<SnapCandidate> addX = c => { if ( c.AbsOffset < snapDistance ) xCandidates.Add( c ); };

				// Parent
				addX( new SnapCandidate( edgeToSnapX, parentClientLeft, new( parentClientLeft, 0 ), new( parentClientLeft, parentBounds.Height ), parentBounds ) );
				addX( new SnapCandidate( edgeToSnapX, parentClientRight, new( parentClientRight, 0 ), new( parentClientRight, parentBounds.Height ), parentBounds ) );
				addX( new SnapCandidate( edgeToSnapX, parentClientCenterX, new( parentClientCenterX, 0 ), new( parentClientCenterX, parentBounds.Height ), parentBounds ) );
				// Snap center only if not resizing diagonally
				if ( !isTopEdge && !isBottomEdge ) addX( new SnapCandidate( movingCenterX, parentClientCenterX, new( parentClientCenterX, 0 ), new( parentClientCenterX, parentBounds.Height ), parentBounds ) );


				// Siblings
				foreach ( var siblingRect in siblings )
				{
					float sLeft = siblingRect.Left; float sRight = siblingRect.Right; float sCenterX = siblingRect.Center.x;
					addX( new SnapCandidate( edgeToSnapX, sLeft, new( sLeft, 0 ), new( sLeft, parentBounds.Height ), siblingRect ) );
					addX( new SnapCandidate( edgeToSnapX, sRight, new( sRight, 0 ), new( sRight, parentBounds.Height ), siblingRect ) );
					addX( new SnapCandidate( edgeToSnapX, sCenterX, new( sCenterX, 0 ), new( sCenterX, parentBounds.Height ), siblingRect ) );
					addX( new SnapCandidate( edgeToSnapX, sLeft - margin, new( sLeft - margin, 0 ), new( sLeft - margin, parentBounds.Height ), siblingRect ) );
					addX( new SnapCandidate( edgeToSnapX, sRight + margin, new( sRight + margin, 0 ), new( sRight + margin, parentBounds.Height ), siblingRect ) );
					if ( !isTopEdge && !isBottomEdge ) addX( new SnapCandidate( movingCenterX, sCenterX, new( sCenterX, 0 ), new( sCenterX, parentBounds.Height ), siblingRect ) );
				}
			}

			// Y-Axis Snapping (Parent & Siblings)
			if ( isTopEdge || isBottomEdge )
			{
				float edgeToSnapY = isTopEdge ? movingTop : movingBottom;
				Action<SnapCandidate> addY = c => { if ( c.AbsOffset < snapDistance ) yCandidates.Add( c ); };

				// Parent
				addY( new SnapCandidate( edgeToSnapY, parentClientTop, new( 0, parentClientTop ), new( parentBounds.Width, parentClientTop ), parentBounds ) );
				addY( new SnapCandidate( edgeToSnapY, parentClientBottom, new( 0, parentClientBottom ), new( parentBounds.Width, parentClientBottom ), parentBounds ) );
				addY( new SnapCandidate( edgeToSnapY, parentClientCenterY, new( 0, parentClientCenterY ), new( parentBounds.Width, parentClientCenterY ), parentBounds ) );
				// Snap center only if not resizing diagonally
				if ( !isLeftEdge && !isRightEdge ) addY( new SnapCandidate( movingCenterY, parentClientCenterY, new( 0, parentClientCenterY ), new( parentBounds.Width, parentClientCenterY ), parentBounds ) );

				// Siblings
				foreach ( var siblingRect in siblings )
				{
					float sTop = siblingRect.Top; float sBottom = siblingRect.Bottom; float sCenterY = siblingRect.Center.y;
					addY( new SnapCandidate( edgeToSnapY, sTop, new( 0, sTop ), new( parentBounds.Width, sTop ), siblingRect ) );
					addY( new SnapCandidate( edgeToSnapY, sBottom, new( 0, sBottom ), new( parentBounds.Width, sBottom ), siblingRect ) );
					addY( new SnapCandidate( edgeToSnapY, sCenterY, new( 0, sCenterY ), new( parentBounds.Width, sCenterY ), siblingRect ) );
					addY( new SnapCandidate( edgeToSnapY, sTop - margin, new( 0, sTop - margin ), new( parentBounds.Width, sTop - margin ), siblingRect ) );
					addY( new SnapCandidate( edgeToSnapY, sBottom + margin, new( 0, sBottom + margin ), new( parentBounds.Width, sBottom + margin ), siblingRect ) );
					if ( !isLeftEdge && !isRightEdge ) addY( new SnapCandidate( movingCenterY, sCenterY, new( 0, sCenterY ), new( parentBounds.Width, sCenterY ), siblingRect ) );
				}
			}

			// --- Apply Best Snap Adjustments ---
			Vector2 adjustedDelta = delta;
			_isSnappedX = false;
			_isSnappedY = false;
			_snappingToSiblingRectX = null;
			_snappingToSiblingRectY = null;

			if ( xCandidates.Count > 0 )
			{
				var bestX = xCandidates.OrderBy( c => c.AbsOffset ).First();
				// Adjust the original delta by the offset needed to snap
				adjustedDelta.x += bestX.Offset;
				_isSnappedX = true;
				_snapLineStartX = bestX.LineStart;
				_snapLineEndX = bestX.LineEnd;
				_snappingToSiblingRectX = bestX.TargetRect;
			}

			if ( yCandidates.Count > 0 )
			{
				var bestY = yCandidates.OrderBy( c => c.AbsOffset ).First();
				// Adjust the original delta by the offset needed to snap
				adjustedDelta.y += bestY.Offset;
				_isSnappedY = true;
				_snapLineStartY = bestY.LineStart;
				_snapLineEndY = bestY.LineEnd;
				_snappingToSiblingRectY = bestY.TargetRect;
			}

			return adjustedDelta;
		}
	}
}
