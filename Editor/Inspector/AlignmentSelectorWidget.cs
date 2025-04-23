using Editor;
using Sandbox.UI;
using System;
using System.Collections.Generic;

namespace XGUI.XGUIEditor
{
	/// <summary>
	/// A widget that displays and controls panel edge alignments similar to the Windows Forms Anchor property
	/// </summary>
	public class AlignmentSelectorWidget : PropertyEditor
	{
		// The panel and node being edited
		private Panel _targetPanel;
		private MarkupNode _targetNode;

		// UI elements
		private Widget _container;
		private Editor.Button _leftBtn;
		private Editor.Button _topBtn;
		private Editor.Button _rightBtn;
		private Editor.Button _bottomBtn;

		// Current alignment state
		private PanelAlignment _alignment = new PanelAlignment();

		// Boolean indicating if the position mode is absolute (only enable alignment in absolute mode)
		private bool _isAbsolutePosition;

		public AlignmentSelectorWidget( string propertyName, string displayName, bool isStyle = false )
			: base( propertyName, displayName, isStyle )
		{
		}

		/// <summary>
		/// Sets the current target panel and node to modify
		/// </summary>
		public void SetTarget( Panel panel, MarkupNode node, Dictionary<string, string> styles )
		{
			_targetPanel = panel;
			_targetNode = node;

			// Extract position mode and update enable state
			_isAbsolutePosition = styles.TryGetValue( "position", out var position ) && position == "absolute";
			RootWidget.Enabled = _isAbsolutePosition;

			// Update alignment state from styles
			_alignment = PanelAlignment.FromStyles( styles );
			UpdateButtonVisuals();
		}

		public override Widget CreateUI( Layout layout )
		{
			// Create a container widget for the editor, following the standard pattern
			var rootWidget = new Widget( null );
			rootWidget.Layout = Layout.Row();
			rootWidget.Layout.Margin = 0;
			rootWidget.Layout.Spacing = 2;

			// Add label like other editors
			rootWidget.Layout.Add( new Editor.Label( DisplayName ) { FixedWidth = 100 } );

			// Create the alignment control container
			_container = new Widget( null );
			rootWidget.Layout.Add( _container, 1 );
			_container.Layout = Layout.Column();
			_container.Layout.Spacing = 4;

			// Top row (with spacers for centering)
			var topRow = new Widget( null );
			_container.Layout.Add( topRow );
			topRow.Layout = Layout.Row();
			topRow.Layout.Spacing = 4;

			// Add first spacer (fixed width)
			var topLeftSpacer = new Widget( null );
			topLeftSpacer.FixedWidth = 60;
			topRow.Layout.Add( topLeftSpacer );

			// Add top button
			_topBtn = CreateAlignmentButton( "arrow_upward" );
			_topBtn.ToolTip = "Anchor to top edge";
			_topBtn.Clicked += () => ToggleAlignment( "top", !_alignment.Top );
			topRow.Layout.Add( _topBtn );

			// Add second spacer (fixed width - must match first spacer)
			var topRightSpacer = new Widget( null );
			topRightSpacer.FixedWidth = 60;
			topRow.Layout.Add( topRightSpacer );

			// Middle row
			var middleRow = new Widget( null );
			_container.Layout.Add( middleRow );
			middleRow.Layout = Layout.Row();
			middleRow.Layout.Spacing = 8; // More spacing in middle row

			// Left button
			_leftBtn = CreateAlignmentButton( "arrow_back" );
			_leftBtn.ToolTip = "Anchor to left edge";
			_leftBtn.Clicked += () => ToggleAlignment( "left", !_alignment.Left );
			middleRow.Layout.Add( _leftBtn );

			// Center panel (fixed width to maintain spacing)
			var centerPanel = new Widget( null );
			centerPanel.FixedWidth = 48;
			centerPanel.MinimumSize = new Vector2( 24, 24 );
			centerPanel.ToolTip = "Panel representation";
			centerPanel.SetStyles( "background-color: #444444; border: 1px solid #666666;" );
			middleRow.Layout.Add( centerPanel );

			// Right button
			_rightBtn = CreateAlignmentButton( "arrow_forward" );
			_rightBtn.ToolTip = "Anchor to right edge";
			_rightBtn.Clicked += () => ToggleAlignment( "right", !_alignment.Right );
			middleRow.Layout.Add( _rightBtn );

			// Bottom row (with spacers for centering)
			var bottomRow = new Widget( null );
			_container.Layout.Add( bottomRow );
			bottomRow.Layout = Layout.Row();
			bottomRow.Layout.Spacing = 4;

			// Add first spacer (fixed width)
			var bottomLeftSpacer = new Widget( null );
			bottomLeftSpacer.FixedWidth = 60;
			bottomRow.Layout.Add( bottomLeftSpacer );

			// Add bottom button
			_bottomBtn = CreateAlignmentButton( "arrow_downward" );
			_bottomBtn.ToolTip = "Anchor to bottom edge";
			_bottomBtn.Clicked += () => ToggleAlignment( "bottom", !_alignment.Bottom );
			bottomRow.Layout.Add( _bottomBtn );

			// Add second spacer (fixed width - must match first spacer)
			var bottomRightSpacer = new Widget( null );
			bottomRightSpacer.FixedWidth = 60;
			bottomRow.Layout.Add( bottomRightSpacer );

			// Add the root widget to the parent layout
			layout.Add( rootWidget );

			// Set the RootWidget property
			RootWidget = rootWidget;

			// Initial button states
			UpdateButtonVisuals();

			// Default to disabled until initialized with absolute positioning
			RootWidget.Enabled = false;

			return rootWidget;
		}

		/// <summary>
		/// Creates a button for alignment selection
		/// </summary>
		private Editor.Button CreateAlignmentButton( string text )
		{
			var btn = new Editor.Button( null );
			btn.Text = text;
			btn.MinimumSize = new Vector2( 24, 24 );
			btn.MaximumSize = new Vector2( 24, 24 );
			return btn;
		}

		/// <summary>
		/// Updates the visual state of buttons based on current alignment
		/// </summary>
		private void UpdateButtonVisuals()
		{
			// Ensure buttons have appropriate styling
			UpdateButtonStyle( _leftBtn, _alignment.Left );
			UpdateButtonStyle( _topBtn, _alignment.Top );
			UpdateButtonStyle( _rightBtn, _alignment.Right );
			UpdateButtonStyle( _bottomBtn, _alignment.Bottom );
		}

		/// <summary>
		/// Updates the visual style of a button based on its active state
		/// </summary>
		private void UpdateButtonStyle( Editor.Button btn, bool isActive )
		{
			if ( btn == null ) return;
			btn.SetStyles( $"Button {{ font-family: 'Material Icons'; padding: 0; border: 0; font-size: 13px;}}" );
			btn.Tint = isActive ? Theme.Primary : Color.FromRgb( 0x5b5d62 );
		}

		/// <summary>
		/// Toggles an alignment direction and updates the UI and node styles
		/// </summary>
		private void ToggleAlignment( string edge, bool newState )
		{
			// Don't allow removing both horizontal alignments
			if ( edge == "left" && !newState && !_alignment.Right )
				return;

			// Don't allow removing both vertical alignments
			if ( edge == "top" && !newState && !_alignment.Bottom )
				return;

			// Update alignment state
			switch ( edge )
			{
				case "left": _alignment.Left = newState; break;
				case "top": _alignment.Top = newState; break;
				case "right": _alignment.Right = newState; break;
				case "bottom": _alignment.Bottom = newState; break;
			}

			// Update button visuals
			UpdateButtonVisuals();

			// Apply the changes to the styles
			ApplyAlignmentChanges( edge, newState );
		}

		/// <summary>
		/// Applies alignment changes to the panel styles
		/// </summary>
		private void ApplyAlignmentChanges( string edge, bool newState )
		{
			if ( _targetNode == null || _targetPanel == null || !_isAbsolutePosition )
				return;

			// Current styles for checking/updating
			var styles = GetCurrentStyles();

			if ( newState )
			{
				// Calculate edge position based on current box/rect values
				string cssValue = CalculateEdgePosition( edge );

				// Apply the position to the style
				_targetNode.TryModifyStyle( edge, cssValue );

				// Special handling for horizontal and vertical stretching:
				// If both left+right set, remove width to enable stretching
				if ( edge == "left" || edge == "right" )
				{
					bool hasLeft = edge == "left" ? true : styles.ContainsKey( "left" );
					bool hasRight = edge == "right" ? true : styles.ContainsKey( "right" );

					// Both edges are now anchored, remove width to allow stretching
					if ( hasLeft && hasRight )
					{
						_targetNode.TryModifyStyle( "width", null );
					}
				}

				// Similarly for top+bottom with height
				if ( edge == "top" || edge == "bottom" )
				{
					bool hasTop = edge == "top" ? true : styles.ContainsKey( "top" );
					bool hasBottom = edge == "bottom" ? true : styles.ContainsKey( "bottom" );

					// Both edges are now anchored, remove height to allow stretching
					if ( hasTop && hasBottom )
					{
						_targetNode.TryModifyStyle( "height", null );
					}
				}

				NotifyValueChanged( new AlignmentChangeInfo { Edge = edge, Value = cssValue } );
			}
			else
			{
				// Remove the style property
				_targetNode.TryModifyStyle( edge, null );

				// When removing an alignment, we may need to add back width/height
				// based on the current box size so the element doesn't collapse

				// Handle horizontal case
				if ( edge == "left" || edge == "right" )
				{
					bool hasLeft = edge == "left" ? false : styles.ContainsKey( "left" );
					bool hasRight = edge == "right" ? false : styles.ContainsKey( "right" );

					// We no longer have both edges anchored, so set explicit width
					if ( !(hasLeft && hasRight) && !styles.ContainsKey( "width" ) )
					{
						string widthValue = $"{Math.Max( Math.Round( _targetPanel.Box.Rect.Width ), 10 )}px";
						_targetNode.TryModifyStyle( "width", widthValue );
					}
				}

				// Handle vertical case
				if ( edge == "top" || edge == "bottom" )
				{
					bool hasTop = edge == "top" ? false : styles.ContainsKey( "top" );
					bool hasBottom = edge == "bottom" ? false : styles.ContainsKey( "bottom" );

					// We no longer have both edges anchored, so set explicit height
					if ( !(hasTop && hasBottom) && !styles.ContainsKey( "height" ) )
					{
						string heightValue = $"{Math.Max( Math.Round( _targetPanel.Box.Rect.Height ), 10 )}px";
						_targetNode.TryModifyStyle( "height", heightValue );
					}
				}

				NotifyValueChanged( new AlignmentChangeInfo { Edge = edge, Value = null } );
			}
		}

		/// <summary>
		/// Helper method to get current styles as a dictionary
		/// </summary>
		private Dictionary<string, string> GetCurrentStyles()
		{
			if ( _targetNode == null || !_targetNode.Attributes.TryGetValue( "style", out var styleStr ) )
				return new Dictionary<string, string>();

			// Parse style string into dictionary
			var styles = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			var declarations = styleStr.Split( ';', StringSplitOptions.RemoveEmptyEntries );

			foreach ( var decl in declarations )
			{
				var parts = decl.Split( ':', 2 );
				if ( parts.Length == 2 )
				{
					string property = parts[0].Trim();
					string value = parts[1].Trim();
					styles[property] = value;
				}
			}

			return styles;
		}

		/// <summary>
		/// Calculates the position value for an edge based on current panel layout
		/// </summary>
		private string CalculateEdgePosition( string edge )
		{
			if ( _targetPanel == null || _targetPanel.Parent == null )
				return "0px";

			float value = 0;
			var rect = _targetPanel.Box.Rect;
			var parentRect = _targetPanel.Parent.Box.Rect;

			switch ( edge )
			{
				case "left":
					value = rect.Left - parentRect.Left;
					break;

				case "top":
					value = rect.Top - parentRect.Top;
					break;

				case "right":
					// Right edge is calculated as parent width - (panel left + panel width)
					value = parentRect.Width - (rect.Left - parentRect.Left + rect.Width);
					break;

				case "bottom":
					// Bottom edge is calculated as parent height - (panel top + panel height)
					value = parentRect.Height - (rect.Top - parentRect.Top + rect.Height);
					break;
			}

			return $"{Math.Max( Math.Round( value ), 0 )}px";
		}

		// This is just a dummy implementation to satisfy the abstract method
		public override void SetValueSilently( string value )
		{
			// Nothing to do here
		}

		// Class to pass alignment change info when notifying value changed
		public class AlignmentChangeInfo
		{
			public string Edge { get; set; }
			public string Value { get; set; }
		}
	}
}

