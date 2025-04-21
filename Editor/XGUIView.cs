using Editor;
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using XGUI.XGUIEditor;

namespace XGUI;

public partial class XGUIView : SceneRenderingWidget
{
	XGUIRootPanel Panel;
	XGUIRootComponent _rootComponent;

	public Window Window;
	public Panel WindowContent;

	// Add delegate for selection callback
	public Action<Panel> OnElementSelected { get; set; }
	public XGUIDesigner OwnerDesigner;

	public XGUIView()
	{
		MinimumSize = 300;
		Scene = new Scene();

		var cam = Scene.CreateObject();

		Camera = cam.AddComponent<CameraComponent>();
		_rootComponent = cam.AddComponent<XGUIRootComponent>();
		_rootComponent.MouseUnlocked = false;
		Scene.GameTick();
		Scene.GameTick();
		Scene.GameTick();
		Panel = _rootComponent.XGUIPanel;

	}

	public void CreateBlankWindow()
	{
		Window = new Window();
		Window.StyleSheet.Load( "/XGUI/DefaultStyles/OliveGreen.scss" );
		Window.Title = "My New Window";
		Window.MinSize = new Vector2( 200, 200 );
		Panel.AddChild( Window );

		WindowContent = new Panel();
		WindowContent.Style.MinHeight = 200;
		WindowContent.Style.MinWidth = 200;
		WindowContent.AddClass( "window-content" );
		Window.AddChild( WindowContent );

		Window.FocusWindow();
	}

	public void Setup()
	{
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();
		CleanUp();
	}
	public void CleanUp()
	{
		Scene.Destroy();
		Panel.Delete();
	}
	int mouseIconHash = 0;
	public override void PreFrame()
	{
		base.PreFrame();

		Scene.GameTick();

		var mousePosLocal = Editor.Application.CursorPosition - ScreenPosition;
		var hash = HashCode.Combine( mousePosLocal, SelectedPanel, isDragging );
		if ( hash != mouseIconHash )
		{
			mouseIconHash = hash;
			Cursor = CursorShape.None;

			if ( isDragging ) Cursor = CursorShape.DragMove;

			int handleIndex = GetHandleAtPosition( mousePosLocal );
			if ( ShouldOnlyHorizontalResize( SelectedPanel ) )
			{
				UpdateResizeCursor( handleIndex, verticalenabled: false, diagonalenabled: false );
			}
			else
			{
				UpdateResizeCursor( handleIndex );
			}
		}
	}

	// Resize handle tracking
	private bool _isDraggingHandle = false;
	private int _activeHandle = -1; // -1 = none, 0-7 = handles clockwise from top-left
	private Vector2 _dragStartPos;
	private Rect _originalRect;
	public void OnOverlayDraw()
	{

		if ( SelectedPanel != null )
		{
			// Draw selection outline
			Paint.ClearPen();
			Paint.SetPen( Color.Cyan.WithAlpha( 0.8f ), 1.0f, PenStyle.Dot );
			Paint.DrawRect( SelectedPanel.Box.Rect );

			// Draw resize handles
			if ( ShouldOnlyHorizontalResize( SelectedPanel ) )
			{
				DrawResizeHandles( SelectedPanel.Box.Rect, verticalenabled: false, diagonalenabled: false );
			}
			else
			{
				DrawResizeHandles( SelectedPanel.Box.Rect );
			}
		}
	}

	public Panel SelectedPanel;

	public Panel DraggingPanel;

	private bool isDragging = false;
	private bool isMouseDown = false;
	private Vector2 dragOffset;
	private Vector2 dragStartPos;
	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );
		isMouseDown = true;

		ResizeMousePress( e );
		if ( e.Button != MouseButtons.Left )
			return;

		// Find the topmost panel under the mouse (excluding WindowContent itself)
		Panel hovered = FindPanelAtPosition( WindowContent, e.LocalPosition, skipSelf: true );
		if ( hovered != null )
		{
			SelectedPanel = hovered;
			OnElementSelected?.Invoke( hovered );
		}
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );


		if ( isMouseDown && !_isDraggingHandle && !isDragging )
		{
			Panel hovered = FindPanelAtPosition( WindowContent, e.LocalPosition, skipSelf: true );
			if ( hovered != null )
			{
				// Start dragging
				isDragging = true;
				DraggingPanel = hovered;
				dragStartPos = e.LocalPosition;
				dragOffset = e.LocalPosition - hovered.Box.Rect.Position;
			}
		}

		if ( !isDragging )
		{
			ResizeMouseMove( e );
		}

		if ( isDragging && DraggingPanel != null )
		{
			// 1. Check for reparenting (hovering over a panel and within margin)
			Panel dropTarget = FindPanelAtPosition( WindowContent, e.LocalPosition, skipSelf: true, skip: DraggingPanel );
			if ( dropTarget != null && dropTarget != DraggingPanel.Parent && dropTarget != DraggingPanel )
			{
				var rect = dropTarget.Box.Rect;
				float margin = 4f;
				if ( e.LocalPosition.x > rect.Left + margin && e.LocalPosition.x < rect.Right - margin &&
					e.LocalPosition.y > rect.Top + margin && e.LocalPosition.y < rect.Bottom - margin )
				{
					return; // Don't allow reparenting if within margin, this will be reparenting
				}
			}

			// 2. If absolute, update position
			if ( DraggingPanel.ComputedStyle.Position == PositionMode.Absolute )
			{
				DraggingPanel.Style.Position = PositionMode.Absolute;
				var newPosition = e.LocalPosition - dragOffset;
				Log.Info( dragOffset );
				newPosition -= WindowContent.Box.Rect.Position; // Adjust for WindowContent position

				var node = OwnerDesigner.LookupNodeByPanel( DraggingPanel );
				if ( node != null )
				{
					/*// add or modify left/top style properties
					string style = node.Attributes["style"];
					// replace left/top properties if they exist
					if ( style != null )
					{
						if ( style.Contains( "top:" ) )
						{
							// Replace any "top: Xpx;" or "top:Xpx;" pattern with the new value
							style = System.Text.RegularExpressions.Regex.Replace(
								style,
								@"top:\s*[-\d\.]+px;",
								$"top: {newPosition.y}px;"
							);
						}
						else
						{
							style += $" top: {newPosition.y}px;";
						}
						if ( style.Contains( "left:" ) )
						{
							// Replace any "left: Xpx;" or "left:Xpx;" pattern with the new value
							style = System.Text.RegularExpressions.Regex.Replace(
								style,
								@"left:\s*[-\d\.]+px;",
								$"left: {newPosition.x}px;"
							);
						}
						else
						{
							style += $" left: {newPosition.x}px;";
						}
					}
					else
					{
						style = $"left: {newPosition.x}px; top: {newPosition.y}px;";
					}
					node.Attributes["style"] = style;*/
					node.TryModifyStyle( "left", $"{newPosition.x}px" );
					node.TryModifyStyle( "top", $"{newPosition.y}px" );
				}
				else
				{
					DraggingPanel.Style.Left = e.LocalPosition.x - dragOffset.x;
					DraggingPanel.Style.Top = e.LocalPosition.y - dragOffset.y;
				}
				OwnerDesigner.ForceUpdate( false );
				DraggingPanel.Style.Dirty();
				DraggingPanel = OwnerDesigner.LookupPanelByNode( node );
				if ( DraggingPanel == null )
				{
					Log.Warning( "DraggingPanel is null after UI rebuild!" );
				}
				return;
			}

			// 3. Rearranging among siblings (auto-layout)
			else
			{
				var parent = DraggingPanel.Parent;
				if ( parent != null )
				{
					var siblings = parent.Children.OfType<Panel>().Where( p => p != DraggingPanel ).ToList();
					Panel targetSibling = null;
					bool insertAfter = false;
					FlexDirection flexDirection = parent.ComputedStyle.FlexDirection ?? FlexDirection.Column; // Default to column


					foreach ( var sibling in siblings )
					{
						var rect = sibling.Box.Rect;

						if ( flexDirection == FlexDirection.Row || flexDirection == FlexDirection.RowReverse )
						{
							// Horizontal
							if ( e.LocalPosition.x > rect.Left && e.LocalPosition.x < rect.Right )
							{
								if ( e.LocalPosition.x < rect.Left + rect.Width / 2 )
								{
									targetSibling = sibling;
									insertAfter = (flexDirection == FlexDirection.RowReverse) ^ false;
									break;
								}
								else if ( e.LocalPosition.x >= rect.Left + rect.Width / 2 )
								{
									targetSibling = sibling;
									insertAfter = (flexDirection == FlexDirection.RowReverse) ^ true;
									break;
								}
							}
						}
						else
						{
							// Vertical (column or column-reverse)
							if ( e.LocalPosition.y > rect.Top && e.LocalPosition.y < rect.Bottom )
							{
								if ( e.LocalPosition.y < rect.Top + rect.Height / 2 )
								{
									targetSibling = sibling;
									insertAfter = (flexDirection == FlexDirection.ColumnReverse) ^ false;
									break;
								}
								else if ( e.LocalPosition.y >= rect.Top + rect.Height / 2 )
								{
									targetSibling = sibling;
									insertAfter = (flexDirection == FlexDirection.ColumnReverse) ^ true;
									break;
								}
							}
						}
					}

					if ( targetSibling != null && targetSibling != DraggingPanel )
					{
						Log.Info( $"Rearranging {DraggingPanel} before {targetSibling}" );

						// Update MarkupNode tree as well
						var parentNode = OwnerDesigner.LookupNodeByPanel( parent );
						var draggedNode = OwnerDesigner.LookupNodeByPanel( DraggingPanel );
						var targetSiblingNode = OwnerDesigner.LookupNodeByPanel( targetSibling );
						Log.Info( $"Parent: {parentNode} aka {parent}, InsertBefore: {targetSiblingNode}, Dragged: {draggedNode}" );
						if ( parentNode != null && draggedNode != null && targetSiblingNode != null )
						{
							var nodeChildrenList = parentNode.Children as List<MarkupNode>;
							if ( nodeChildrenList != null )
							{
								nodeChildrenList.Remove( draggedNode );
								int nodeInsertIndex = nodeChildrenList.IndexOf( targetSiblingNode );
								if ( insertAfter )
									nodeInsertIndex++;
								nodeChildrenList.Insert( nodeInsertIndex, draggedNode );
								draggedNode.Parent = parentNode;
							}
						}

						OwnerDesigner.ForceUpdate( false );
						DraggingPanel.Style.Dirty();
						// Restore DraggingPanel after UI rebuild
						DraggingPanel = OwnerDesigner.LookupPanelByNode( draggedNode );
						if ( DraggingPanel == null )
						{
							Log.Warning( "DraggingPanel is null after UI rebuild!" );
						}
					}
				}
			}
		}
	}

	// // Restore DraggingPanel after UI rebuild
	// DraggingPanel = OwnerDesigner.LookupPanelByNode(draggedNode);
	// if (DraggingPanel == null)
	// {
	// 	Log.Warning("DraggingPanel is null after UI rebuild!");
	// }
	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );
		isMouseDown = false;
		ResizeMouseReleased( e );

		if ( isDragging && DraggingPanel != null )
		{
			// 1. Check for reparenting (hovering over a panel and within margin)
			Panel dropTarget = FindPanelAtPosition( WindowContent, e.LocalPosition, skipSelf: true, skip: DraggingPanel );
			if ( dropTarget != null && dropTarget != DraggingPanel.Parent && dropTarget != DraggingPanel )
			{
				var rect = dropTarget.Box.Rect;
				float margin = 6f;
				if ( e.LocalPosition.x > rect.Left + margin && e.LocalPosition.x < rect.Right - margin &&
					e.LocalPosition.y > rect.Top + margin && e.LocalPosition.y < rect.Bottom - margin )
				{
					// --- Reparent ---
					DraggingPanel.Parent = null;
					dropTarget.AddChild( DraggingPanel );

					// --- Update MarkupNode tree as well ---
					var draggedNode = OwnerDesigner.LookupNodeByPanel( DraggingPanel );
					var newParentNode = OwnerDesigner.LookupNodeByPanel( dropTarget );
					if ( draggedNode != null && newParentNode != null && draggedNode.Parent != null )
					{
						draggedNode.Parent.Children.Remove( draggedNode );
						newParentNode.Children.Add( draggedNode );
						draggedNode.Parent = newParentNode;

						OwnerDesigner.ForceUpdate();
						DraggingPanel.Style.Dirty();
						isDragging = false;
						DraggingPanel = null;
						return;
					}
				}
			}

			// 2. Rearranging among siblings (auto-layout) -- only on release!


			isDragging = false;
			DraggingPanel = null;
		}
	}

	// Utility: Recursively find the topmost panel at a position
	private Panel FindPanelAtPosition( Panel root, Vector2 pos, bool skipSelf = false, Panel skip = null )
	{
		if ( root == null || root == skip ) return null;
		if ( !skipSelf && root.Box.Rect.IsInside( pos ) ) return root;
		foreach ( var child in root.Children.OfType<Panel>().Reverse() )
		{
			var found = FindPanelAtPosition( child, pos, skipSelf: false, skip: skip );
			if ( found != null ) return found;
		}
		return null;
	}
}
