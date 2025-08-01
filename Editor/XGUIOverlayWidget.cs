﻿using Editor;
using Sandbox.UI;
using System;

namespace XGUI.XGUIEditor
{
	/// <summary>
	/// An overlay widget that allows absolute positioning on top of the editor view.
	/// Used for selection indicators, handles, and other UI designer elements.
	/// </summary>
	public class XGUIOverlayWidget : Widget
	{
		public XGUIView View { get; set; }
		public Panel SelectedPanel => View?.SelectedPanel;
		public Action OverlayDraw { get; set; }

		public XGUIOverlayWidget( Widget parent = null ) : base( parent )
		{

			// Position should be absolute to the parent
			Position = Vector2.Zero;
			SetSizeMode( SizeMode.Expand, SizeMode.Expand );
			this.TranslucentBackground = true;
			this.TransparentForMouseEvents = true;
		}


		bool IsWindowMode = false;
		/// <summary>
		/// Connect this overlay to a view and start tracking selection changes
		/// </summary>
		public void ConnectToView( XGUIView view, bool aswindow = true )
		{
			View = view;
			OverlayDraw += view.OnOverlayDraw;

			Parent = view.Parent;
			SetSizeMode( SizeMode.Expand, SizeMode.Expand );
			IsWindowMode = aswindow;

			if ( IsWindowMode )
			{
				//do overlay as transparent window
				// popout as a window
				//this.IsWindow = true;
				this.WindowTitle = "XGUI Overlay";
				this.Parent = null;
				this.WindowFlags = WindowFlags.FramelessWindowHint | WindowFlags.WindowStaysOnTopHint | WindowFlags.SubWindow | WindowFlags.WindowTransparentForInput;

				this.MakeWindowed();
				this.TransparentForMouseEvents = true;
				this.TranslucentBackground = true;
				this.NoSystemBackground = true;


				//this.Focus();
				//this.Blur();

			}

		}
		[EditorEvent.Frame]
		public void onFrame()
		{
			if ( !View.IsValid )
			{
				Destroy();
				return;
			}
			Size = View.Size;
			if ( IsWindowMode )
			{
				// position over the view, get screen position
				var screenPos = View.ScreenPosition;
				Position = screenPos;
			}
			// Request a redraw
			Update();

		}
		protected override void OnPaint()
		{
			base.OnPaint();

			if ( View == null )
				return;

			OverlayDraw.Invoke();

		}
	}
}
