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

		private void DrawSnappingGuides()
		{

			// Draw snapping guides when dragging
			if ( isDragging && DraggingPanel != null )
			{
				// Adjust coordinates for WindowContent position
				Vector2 offset = WindowContent.Box.Rect.Position;

				// Draw horizontal snapping guide
				if ( _isSnappedY )
				{
					Paint.ClearPen();

					// Check if this is a center snap (using the midpoint of the dragging panel)
					bool isCenterSnap = Math.Abs( _snapLineStartY.y - (DraggingPanel.Box.Rect.Top + DraggingPanel.Box.Rect.Height / 2 - offset.y) ) < 1f;

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

					// Check if this is a center snap (using the midpoint of the dragging panel)
					bool isCenterSnap = Math.Abs( _snapLineStartX.x - (DraggingPanel.Box.Rect.Left + DraggingPanel.Box.Rect.Width / 2 - offset.x) ) < 1f;

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

		private Vector2 ApplySnappingToPosition( Vector2 position )
		{
			if ( DraggingPanel == null || WindowContent == null )
				return position;

			float snapDistance = 10f;
			float margin = 6f;

			Vector2 panelSize = DraggingPanel.Box.Rect.Size;
			Rect parentRect = DraggingPanel.Parent.Box.RectOuter.Shrink( WindowContent.Box.Border );
			float viewportWidth = parentRect.Width;
			float viewportHeight = parentRect.Height;

			// Dragged panel features
			float dragLeft = position.x;
			float dragRight = position.x + panelSize.x;
			float dragTop = position.y;
			float dragBottom = position.y + panelSize.y;
			float dragCenterX = position.x + panelSize.x / 2;
			float dragCenterY = position.y + panelSize.y / 2;

			// Prepare snap candidates: (offset, snap-to, lineStart, lineEnd, siblingRect)
			List<(float offset, float snapTo, string dragFeature, string targetFeature, Vector2 lineStart, Vector2 lineEnd, Rect? siblingRect)> xCandidates = new();
			List<(float offset, float snapTo, string dragFeature, string targetFeature, Vector2 lineStart, Vector2 lineEnd, Rect? siblingRect)> yCandidates = new();

			// Helper to add snap candidates
			void AddXCandidate( string dragFeature, float dragValue, string targetFeature, float targetValue, Vector2 lineStart, Vector2 lineEnd, Rect? siblingRect )
			{
				float offset = targetValue - dragValue;
				if ( Math.Abs( offset ) < snapDistance )
					xCandidates.Add( (Math.Abs( offset ), offset, dragFeature, targetFeature, lineStart, lineEnd, siblingRect) );
			}
			void AddYCandidate( string dragFeature, float dragValue, string targetFeature, float targetValue, Vector2 lineStart, Vector2 lineEnd, Rect? siblingRect )
			{
				float offset = targetValue - dragValue;
				if ( Math.Abs( offset ) < snapDistance )
					yCandidates.Add( (Math.Abs( offset ), offset, dragFeature, targetFeature, lineStart, lineEnd, siblingRect) );
			}

			// --- Parent container snapping ---
			// X axis: left, right, center
			AddXCandidate( "left", dragLeft, "left", margin, new Vector2( margin, 0 ), new Vector2( margin, viewportHeight ), parentRect );
			AddXCandidate( "right", dragRight, "right", parentRect.Width - margin, new Vector2( parentRect.Width - margin, 0 ), new Vector2( parentRect.Width - margin, viewportHeight ), parentRect );
			AddXCandidate( "center", dragCenterX, "center", parentRect.Width / 2, new Vector2( parentRect.Width / 2, 0 ), new Vector2( parentRect.Width / 2, viewportHeight ), parentRect );

			// Y axis: top, bottom, center
			AddYCandidate( "top", dragTop, "top", margin, new Vector2( 0, margin ), new Vector2( viewportWidth, margin ), parentRect );
			AddYCandidate( "bottom", dragBottom, "bottom", parentRect.Height - margin, new Vector2( 0, parentRect.Height - margin ), new Vector2( viewportWidth, parentRect.Height - margin ), parentRect );
			AddYCandidate( "center", dragCenterY, "center", parentRect.Height / 2, new Vector2( 0, parentRect.Height / 2 ), new Vector2( viewportWidth, parentRect.Height / 2 ), parentRect );

			// --- Sibling snapping ---
			var siblings = DraggingPanel.Parent.Children.OfType<Panel>()
				.Where( p => p != DraggingPanel )
				.ToList();

			foreach ( var sibling in siblings )
			{
				var r = sibling.Box.Rect;
				float sLeft = r.Left;
				float sRight = r.Right;
				float sTop = r.Top;
				float sBottom = r.Bottom;
				float sCenterX = r.Left + r.Width / 2;
				float sCenterY = r.Top + r.Height / 2;

				// X axis: left, right, center
				AddXCandidate( "left", dragLeft, "left", sLeft, new Vector2( sLeft, 0 ), new Vector2( sLeft, viewportHeight ), r );
				AddXCandidate( "right", dragRight, "right", sRight, new Vector2( sRight, 0 ), new Vector2( sRight, viewportHeight ), r );
				AddXCandidate( "center", dragCenterX, "center", sCenterX, new Vector2( sCenterX, 0 ), new Vector2( sCenterX, viewportHeight ), r );

				// Margin snapping
				AddXCandidate( "left", dragLeft, "right+margin", sRight + margin, new Vector2( sRight + margin, 0 ), new Vector2( sRight + margin, viewportHeight ), r );
				AddXCandidate( "right", dragRight, "left-margin", sLeft - margin, new Vector2( sLeft - margin, 0 ), new Vector2( sLeft - margin, viewportHeight ), r );

				// Y axis: top, bottom, center
				AddYCandidate( "top", dragTop, "top", sTop, new Vector2( 0, sTop ), new Vector2( viewportWidth, sTop ), r );
				AddYCandidate( "bottom", dragBottom, "bottom", sBottom, new Vector2( 0, sBottom ), new Vector2( viewportWidth, sBottom ), r );
				AddYCandidate( "center", dragCenterY, "center", sCenterY, new Vector2( 0, sCenterY ), new Vector2( viewportWidth, sCenterY ), r );

				// Margin snapping
				AddYCandidate( "top", dragTop, "bottom+margin", sBottom + margin, new Vector2( 0, sBottom + margin ), new Vector2( viewportWidth, sBottom + margin ), r );
				AddYCandidate( "bottom", dragBottom, "top-margin", sTop - margin, new Vector2( 0, sTop - margin ), new Vector2( viewportWidth, sTop - margin ), r );
			}

			// --- Pick best snap for each axis ---
			_snappingToSiblingRectX = null;
			_snappingToSiblingRectY = null;
			_isSnappedX = false;
			_isSnappedY = false;

			Vector2 snapped = position;

			if ( xCandidates.Count > 0 )
			{
				var best = xCandidates.OrderBy( c => c.Item1 ).First();
				float dx = best.Item2; // offset
				snapped.x += dx;
				_isSnappedX = true;
				_snapLineStartX = best.Item5;
				_snapLineEndX = best.Item6;
				_snappingToSiblingRectX = best.Item7;
			}
			if ( yCandidates.Count > 0 )
			{
				var best = yCandidates.OrderBy( c => c.Item1 ).First();
				float dy = best.Item2; // offset
				snapped.y += dy;
				_isSnappedY = true;
				_snapLineStartY = best.Item5;
				_snapLineEndY = best.Item6;
				_snappingToSiblingRectY = best.Item7;
			}

			return snapped;
		}
	}
}
