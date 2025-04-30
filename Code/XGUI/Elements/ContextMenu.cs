using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;
using System.Collections.Generic;

namespace XGUI
{
	/// <summary>
	/// A Windows 95-style expandable context menu that supports hierarchical items
	/// </summary>
	public class ContextMenu : Pane
	{
		/// <summary>
		/// The parent menu if this is a submenu
		/// </summary>
		public ContextMenu ParentMenu { get; private set; }

		/// <summary>
		/// The currently open child submenu, if any
		/// </summary>
		public ContextMenu ActiveSubmenu { get; private set; }

		/// <summary>
		/// Delay before showing submenu on hover in seconds
		/// </summary>
		public float SubmenuDelay { get; set; } = 0.3f;

		private TimeSince _hoverTime = 0;
		private Panel _hoveredItem = null;
		private bool _openingSubmenu = false;

		// Dictionary to store submenu populate actions
		private Dictionary<Panel, Action<ContextMenu>> _submenuActions = new();

		public ContextMenu() : base()
		{
			AddClass( "ContextMenu" );
			AcceptsFocus = true;
		}

		public ContextMenu( Panel sourcePanel, PositionMode position = PositionMode.BelowLeft, float offset = 0 )
			: base( sourcePanel, position, offset )
		{
			AddClass( "ContextMenu" );
		}

		/// <summary>
		/// Add a standard menu item
		/// </summary>
		public Panel AddMenuItem( string text, Action action = null )
		{
			var item = Add.Panel( "MenuItem" );
			item.Add.Label( text, "ItemText" );

			if ( action != null )
			{
				item.AddEventListener( "onclick", () =>
				{
					action?.Invoke();
					Success();
				} );
			}
			item.AddEventListener( "onmouseover", () =>
			{
				if ( !_openingSubmenu )
				{
					CloseActiveSubmenu();
				}
			} );

			return item;
		}

		/// <summary>
		/// Add a menu item with an icon
		/// </summary>
		public Panel AddMenuItem( string text, string icon, Action action = null )
		{
			var item = Add.Panel( "MenuItem" );
			item.Add.Icon( icon, "ItemIcon" );
			item.Add.Label( text, "ItemText" );

			if ( action != null )
			{
				item.AddEventListener( "onclick", () =>
				{
					action?.Invoke();
					Success();
				} );
			}
			item.AddEventListener( "onmouseover", () =>
			{
				if ( !_openingSubmenu )
				{
					CloseActiveSubmenu();
				}
			} );

			return item;
		}

		/// <summary>
		/// Add a submenu item that expands when hovered
		/// </summary>
		public Panel AddSubmenuItem( string text, Action<ContextMenu> populateSubmenu )
		{
			var item = Add.Panel( "MenuItem SubmenuItem" );
			item.Add.Label( text, "ItemText" );
			item.Add.Icon( "arrow_right", "ItemArrow" );

			// Track mouse hover for expanding submenu
			item.AddEventListener( "onmouseover", () =>
			{
				// Only handle mouse enter if we're not already opening a submenu
				if ( !_openingSubmenu )
				{
					CloseActiveSubmenu();
					_hoveredItem = item;
					_hoverTime = 0;
					if ( _submenuActions.TryGetValue( _hoveredItem, out var populateAction ) )
					{
						OpenSubmenu( _hoveredItem, populateAction );
						_hoveredItem = null; // Reset to prevent reopening
					}
				}
			} );

			item.AddEventListener( "onmouseleave", () =>
			{
				if ( _hoveredItem == item )
					_hoveredItem = null;
			} );

			// Store the populate action in our dictionary
			_submenuActions[item] = populateSubmenu;

			return item;
		}

		/// <summary>
		/// Add a separator line
		/// </summary>
		public Panel AddSeparator()
		{
			return Add.Panel( "menu-separator" );
		}

		/// <summary>
		/// Open a submenu from the given menu item
		/// </summary>
		public ContextMenu OpenSubmenu( Panel menuItem, Action<ContextMenu> populateFunc )
		{
			// Prevent re-entry into submenu opening
			if ( _openingSubmenu || ActiveSubmenu != null )
				return ActiveSubmenu;

			_openingSubmenu = true;

			CloseActiveSubmenu();

			var submenu = new ContextMenu( menuItem, PositionMode.Right, 0 );
			submenu.ParentMenu = this;

			populateFunc?.Invoke( submenu );

			ActiveSubmenu = submenu;
			_openingSubmenu = false;

			// Add "open" class to the menu item that has an open submenu
			menuItem.AddClass( "open" );

			return submenu;
		}

		public void CloseActiveSubmenu()
		{
			if ( ActiveSubmenu != null )
			{
				// Find the parent menu item that opened this submenu and remove its "open" class
				foreach ( var child in Children )
				{
					if ( child.HasClass( "SubmenuItem" ) && child.HasClass( "open" ) )
					{
						child.RemoveClass( "open" );
					}
				}

				ActiveSubmenu.CloseActiveSubmenu();
				ActiveSubmenu.Delete( true );
				ActiveSubmenu = null;
			}
		}

		public override void Tick()
		{
			base.Tick();


			// Close menu when clicking outside
			if ( Input.Pressed( "attack1" ) && !IsMouseOver() )
			{
				Popup.CloseAll();
			}
		}

		private bool IsMouseOver()
		{
			// Check if mouse is over this menu or any active submenu
			if ( IsInside( Mouse.Position ) )
				return true;

			return ActiveSubmenu?.IsMouseOver() ?? false;
		}

		public override void OnDeleted()
		{
			// Clear references when menu is deleted
			_submenuActions.Clear();
			CloseActiveSubmenu();
			base.OnDeleted();
		}
	}
}
