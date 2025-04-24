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
		Panel.AddChild( Window );

		WindowContent = new Panel();
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
			DrawSnappingGuides();
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
		if ( e.Accepted )
			return;
		isMouseDown = true;

		ResizeMousePress( e );

		if ( e.Accepted )
			return;
		if ( e.Button != MouseButtons.Left )
			return;

		// Check if we're in nested selection mode (multiple clicks at the same position)
		CheckNestedSelectionMode( e.LocalPosition );

		TryInteractAtPosition( Window, e );

		if ( e.Accepted )
			return;

		// Find the panel under the mouse (excluding WindowContent itself)
		Panel hovered = FindPanelAtPosition( Window, e.LocalPosition, skipSelf: true, selectNested: _isNestedSelectionMode ) as Panel;

		if ( hovered == WindowContent )
		{
			hovered = Window;
		}
		Select( hovered );
	}

	private void Select( Panel panel )
	{
		SelectedPanel = panel;
		OnElementSelected?.Invoke( panel );
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );

		if ( isMouseDown && !_isDraggingHandle && !isDragging )
		{
			var result = FindPanelAtPosition( WindowContent, e.LocalPosition, skipSelf: true );
			Panel hovered = result as Panel;
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
			Panel dropTarget = FindPanelAtPosition( WindowContent, e.LocalPosition, skipSelf: true, skip: DraggingPanel ) as Panel;
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
			if ( DraggingPanel.ComputedStyle?.Position == PositionMode.Absolute )
			{
				DraggingPanel.Style.Position = PositionMode.Absolute;
				var newPosition = e.LocalPosition - dragOffset;
				newPosition = ApplySnappingToPosition( newPosition );
				newPosition -= DraggingPanel.Parent.Box.Rect.Position; // Adjust for WindowContent position

				// Apply snapping to nearby elements and parent container

				var node = OwnerDesigner.LookupNodeByPanel( DraggingPanel );
				if ( node != null )
				{
					// Get the panel's alignment settings
					var alignment = GetPanelAlignment( DraggingPanel );
					//Log.Info( $"Alignment Left: {alignment.Left}" );
					//Log.Info( $"Alignment Top: {alignment.Top}" );
					//Log.Info( $"Alignment Right: {alignment.Right}" );
					//Log.Info( $"Alignment Bottom: {alignment.Bottom}" );
					//Log.Info( $"New Position: {newPosition}" );

					// Clear any existing positioning styles first
					if ( alignment.Left )
						node.TryModifyStyle( "left", $"{newPosition.x}px" );
					else
						node.TryModifyStyle( "left", null );

					if ( alignment.Top )
						node.TryModifyStyle( "top", $"{newPosition.y}px" );
					else
						node.TryModifyStyle( "top", null );

					if ( alignment.Right && DraggingPanel.Parent != null )
					{
						// Calculate right value as: parent_width - (left + width)
						float parentWidth = DraggingPanel.Parent.Box.Rect.Width;
						float panelWidth = DraggingPanel.Box.Rect.Width;
						float rightValue = parentWidth - (newPosition.x + panelWidth);
						node.TryModifyStyle( "right", $"{rightValue}px" );
					}
					else
					{
						node.TryModifyStyle( "right", null );
					}

					if ( alignment.Bottom && DraggingPanel.Parent != null )
					{
						// Calculate bottom value as: parent_height - (top + height)
						float parentHeight = DraggingPanel.Parent.Box.Rect.Height;
						float panelHeight = DraggingPanel.Box.Rect.Height;
						float bottomValue = parentHeight - (newPosition.y + panelHeight);
						node.TryModifyStyle( "bottom", $"{bottomValue}px" );
					}
					else
					{
						node.TryModifyStyle( "bottom", null );
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
					FlexDirection flexDirection = parent.ComputedStyle?.FlexDirection ?? FlexDirection.Column; // Default to column


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

	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );
		isMouseDown = false;
		ResizeMouseReleased( e );

		_isSnappedX = false;
		_isSnappedY = false;

		if ( isDragging && DraggingPanel != null )
		{
			// 1. Check for reparenting (hovering over a panel and within margin)
			var result = FindPanelAtPosition( WindowContent, e.LocalPosition, skipSelf: true, skip: DraggingPanel );
			Panel dropTarget = result as Panel;
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


	private void TryInteractAtPosition( Panel root, MouseEvent e )
	{
		var pos = e.LocalPosition;
		// recursively look for a tab button anywhere in the root panel 
		foreach ( var child in root.Children )
		{
			if ( child != null )
			{
				if ( child is Sandbox.UI.Button tabButton && tabButton.Parent?.Parent is TabContainer tabContainer )
				{
					if ( tabButton.Box.Rect.IsInside( pos ) )
					{
						//find the <tab> node (not the button's node) that the button belongs to

						Log.Info( tabContainer );

						// find tab entry in the tab container
						var tabentry = tabContainer.Tabs.FirstOrDefault( x => x.Button == tabButton );

						var page = tabentry.Page;
						Log.Info( page );

						// select, else click if already selected
						if ( page == SelectedPanel )
						{
							// Click event
							tabButton.Click();
							e.Accepted = true;
						}
						else
						{
							// Select the owner <tab> node
							Select( page );
							e.Accepted = true;
						}

						return;
					}
				}
				else if ( child is Panel panel )
				{
					TryInteractAtPosition( panel, e );
				}
			}
		}
	}

	// Utility: Recursively find the hovering panel at a position
	private object FindPanelAtPosition( Panel root, Vector2 pos, bool skipSelf = false, Panel skip = null, bool selectNested = false )
	{
		if ( root == null || root == skip ) return null;

		// Store all panels at this position that have corresponding markup nodes
		List<Panel> panelsAtPosition = selectNested ? new List<Panel>() : null;
		bool foundAny = false;

		// First check all children (reverse order to prioritize elements on top)
		foreach ( var child in root.Children.OfType<Panel>().Reverse() )
		{
			var found = FindPanelAtPosition( child, pos, skipSelf: false, skip: skip, selectNested: selectNested );

			if ( found != null )
			{
				foundAny = true;

				if ( selectNested )
				{
					if ( found is List<Panel> foundList )
					{
						panelsAtPosition.AddRange( foundList );
					}
					else if ( found is Panel foundPanel )
					{
						panelsAtPosition.Add( foundPanel );
					}
				}
				else
				{
					return found; // Return the first (deepest) child found
				}
			}
		}

		// Then check if this panel contains the position AND has a corresponding markup node
		if ( !skipSelf && root.Box.Rect.IsInside( pos ) )
		{
			// Only consider panels that have a corresponding markup node
			bool hasMarkupNode = OwnerDesigner != null && OwnerDesigner.LookupNodeByPanel( root ) != null;

			if ( hasMarkupNode )
			{
				if ( selectNested )
				{
					panelsAtPosition.Add( root );
				}
				else if ( !foundAny )
				{
					return root; // Return this panel if no children were found
				}
			}
		}

		// Return the list for nested selection mode, or null for normal mode
		if ( selectNested && panelsAtPosition.Count > 0 )
		{
			return panelsAtPosition;
		}

		return null;
	}

	// Keep track of which nested panel we're selecting at a specific position
	private Dictionary<Vector2, int> _panelSelectionIndices = new Dictionary<Vector2, int>();

	// Helper to handle multiple clicks at the same position
	private Vector2 _lastClickPosition;
	private bool _isNestedSelectionMode = false;

	// Add this to your OnMousePress method before calling FindPanelAtPosition
	private void CheckNestedSelectionMode( Vector2 position )
	{
		// If clicking at the same spot, enable nested selection mode
		if ( Vector2.Distance( _lastClickPosition, position ) < 1.0f )
		{
			_isNestedSelectionMode = true;
		}
		else
		{
			_isNestedSelectionMode = false;
			_panelSelectionIndices.Clear();
		}

		_lastClickPosition = position;
	}

	private PanelAlignment GetPanelAlignment( Panel panel )
	{
		if ( panel == null )
			return new PanelAlignment();

		var node = OwnerDesigner.LookupNodeByPanel( panel );
		if ( node == null )
			return new PanelAlignment();

		string styleAttr = node.Attributes.GetValueOrDefault( "style", "" );
		var styles = ParseStyleAttribute( styleAttr );

		return XGUIEditor.PanelAlignment.FromStyles( styles );
	}

	// Helper method to parse style strings
	private Dictionary<string, string> ParseStyleAttribute( string styleString )
	{
		var styles = new Dictionary<string, string>( System.StringComparer.OrdinalIgnoreCase );
		if ( string.IsNullOrWhiteSpace( styleString ) )
			return styles;

		var declarations = styleString.Split( ';', StringSplitOptions.RemoveEmptyEntries );

		foreach ( var declaration in declarations )
		{
			var parts = declaration.Split( ':', 2 );
			if ( parts.Length == 2 )
			{
				string property = parts[0].Trim();
				string value = parts[1].Trim();

				if ( !string.IsNullOrEmpty( property ) )
				{
					styles[property] = value;
				}
			}
		}
		return styles;
	}

}
