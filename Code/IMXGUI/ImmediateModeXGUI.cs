using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XGUI.ImmediateMode;

public enum IMXGUIWindowFlags
{
	None = 0,
	NoTitleBar = 1 << 0,
	NoResize = 1 << 1,
	NoMove = 1 << 2,
	NoScrollbar = 1 << 3,
	NoScrollWithMouse = 1 << 4,
	NoCollapse = 1 << 5,
	AlwaysAutoResize = 1 << 6,
	NoSavedSettings = 1 << 7,
	NoInputs = 1 << 8,
	MenuBar = 1 << 9,
	HorizontalScrollbar = 1 << 10,
	NoFocusOnAppearing = 1 << 11,
	NoBringToFrontOnFocus = 1 << 12,
	AlwaysVerticalScrollbar = 1 << 13,
	AlwaysHorizontalScrollbar = 1 << 14,
	AlwaysUseWindowPadding = 1 << 15,
	NoNavInputs = 1 << 16,
	NoNavFocus = 1 << 17,
	UnsavedDocument = 1 << 18,
	NoNav = NoNavInputs | NoNavFocus
}

/// <summary>
/// Stores state for UI elements between frames
/// </summary>
public class IMXGUIState
{
	// Widget state
	public Dictionary<string, object> Values { get; set; } = new();
	public bool Changed { get; set; }
}

/// <summary>
/// ImGui Clone for s&box using Razor UI components.
/// </summary>
public class IMXGUI
{
	public static string CurrentStyle = "/XGUI/DefaultStyles/OliveGreen.scss";

	// Track active windows and elements
	private static Dictionary<string, Window> _windows = new();
	private static HashSet<string> _activeWindows = new();
	private static Dictionary<string, Dictionary<string, Panel>> _elements = new();
	private static HashSet<string> _activeElements = new();

	// Keep state for each element
	private static Dictionary<string, Dictionary<string, IMXGUIState>> _elementState = new();

	// Current state
	private static Window _currentWindow;
	private static Panel _currentPanel;
	private static string _currentWindowId;
	private static string _idStack;
	private static bool _initialized = false;
	private static Panel _rootPanel;

	// Initialize the system
	public static bool IsInitialized() => _initialized && _rootPanel.IsValid;

	public static void Initialize()
	{
		if ( IsInitialized() ) return;

		if ( XGUIRootPanel.Current == null ) return;

		_rootPanel = XGUIRootPanel.Current;
		_windows.Clear();
		_elements.Clear();
		_elementState.Clear();
		_initialized = true;
	}

	// Start a new frame
	public static void NewFrame()
	{
		if ( !IsInitialized() ) Initialize();
		_activeWindows.Clear();
		_activeElements.Clear();
		_idStack = "";
	}

	// Begin a window
	public static bool Begin( string title, ref bool open, IMXGUIWindowFlags flags = IMXGUIWindowFlags.None )
	{
		if ( !open ) return false;
		if ( !IsInitialized() ) Initialize();

		_currentWindowId = title;
		_activeWindows.Add( title );

		// Check if window exists and is valid
		if ( _windows.TryGetValue( title, out _currentWindow ) )
		{
			if ( !_currentWindow.IsValid || _currentWindow.Parent == null )
			{
				_windows.Remove( title );
				_currentWindow = null;
			}
		}

		// Create new window if needed
		if ( _currentWindow == null )
		{
			_currentWindow = new Window();
			_currentWindow.TitleLabel.Text = title;
			ApplyWindowFlags( _currentWindow, flags );

			_currentWindow.StyleSheet.Load( CurrentStyle );
			_rootPanel.AddChild( _currentWindow );
			_windows[title] = _currentWindow;

			// Setup content panel
			_currentPanel = new Panel();
			_currentPanel.Style.FlexDirection = FlexDirection.Column;
			_currentPanel.Style.OverflowY = OverflowMode.Scroll;
			_currentPanel.Style.FlexGrow = 1;
			_currentWindow.AddChild( _currentPanel );

			// Initialize element dictionaries for this window
			_elements[title] = new Dictionary<string, Panel>();
			_elementState[title] = new Dictionary<string, IMXGUIState>();
		}
		else
		{
			_currentWindow = _windows[title];
			_currentPanel = _currentWindow.Children.FirstOrDefault( x =>
				!(x is Label || x is Button || x.HasClass( "titlebar" )) ) as Panel;

			// Create dictionaries if they don't exist
			if ( !_elements.ContainsKey( title ) )
				_elements[title] = new Dictionary<string, Panel>();

			if ( !_elementState.ContainsKey( title ) )
				_elementState[title] = new Dictionary<string, IMXGUIState>();
		}

		_currentWindow.FocusWindow();
		_idStack = title;

		return true;
	}

	// Apply window flags
	private static void ApplyWindowFlags( Window window, IMXGUIWindowFlags flags )
	{
		if ( (flags & IMXGUIWindowFlags.NoTitleBar) != 0 )
			window.TitleBar.Style.Display = DisplayMode.None;

		if ( (flags & IMXGUIWindowFlags.NoResize) != 0 )
			window.IsResizable = false;

		if ( (flags & IMXGUIWindowFlags.NoMove) != 0 )
			window.IsDraggable = false;

		if ( (flags & IMXGUIWindowFlags.NoInputs) != 0 )
			window.Style.PointerEvents = PointerEvents.None;

		window.Style.Width = 300;
		window.Style.Height = 200;
	}

	// End a window
	public static void End()
	{
		_currentWindow = null;
		_currentPanel = null;
		_idStack = "";
	}

	// Push ID to stack (for hierarchical controls)
	public static void PushId( string id )
	{
		_idStack = string.IsNullOrEmpty( _idStack ) ? id : $"{_idStack}/{id}";
	}

	// Pop ID from stack
	public static void PopId()
	{
		int lastSlash = _idStack.LastIndexOf( '/' );
		if ( lastSlash >= 0 )
			_idStack = _idStack.Substring( 0, lastSlash );
		else
			_idStack = "";
	}

	// Generate a unique ID for a control based on its label and ID stack
	private static string GetId( string label )
	{
		return string.IsNullOrEmpty( _idStack ) ? label : $"{_idStack}/{label}";
	}

	// Get or create state for an element
	private static IMXGUIState GetState( string id )
	{
		if ( !_elementState[_currentWindowId].TryGetValue( id, out var state ) )
		{
			state = new IMXGUIState();
			_elementState[_currentWindowId][id] = state;
		}

		return state;
	}

	// Generic element getter/creator
	private static T GetElement<T>( string label, Action<T> initAction = null ) where T : Panel, new()
	{
		if ( _currentPanel == null ) return null;

		string id = GetId( label );
		_activeElements.Add( id );

		// Check if element exists
		if ( _elements[_currentWindowId].TryGetValue( id, out var element ) && element is T typedElement )
		{
			initAction?.Invoke( typedElement );
			return typedElement;
		}

		// Create new element
		var newElement = new T();
		newElement.ElementName = id;

		// Initialize with provided action
		initAction?.Invoke( newElement );

		// Add to panel and tracking
		_currentPanel.AddChild( newElement );
		_elements[_currentWindowId][id] = newElement;

		return newElement;
	}

	// Button control
	public static bool Button( string label )
	{
		string id = GetId( label );
		var state = GetState( id );

		// Initialize state values if needed
		if ( !state.Values.ContainsKey( "wasActive" ) )
			state.Values["wasActive"] = false;

		// Reset changed state at the start of frame
		bool clicked = false;

		var button = GetElement<Button>( label, b =>
		{
			b.Text = label;

			// Track state change from inactive to active (initial click)
			bool wasActive = (bool)state.Values["wasActive"];
			bool isActive = b.HasActive;

			// Only register click on transition from not active to active
			if ( isActive && !wasActive )
			{
				clicked = true;
			}

			// Update the stored state for next frame
			state.Values["wasActive"] = isActive;
		} );

		return clicked;
	}

	// Text display
	public static void Text( string text )
	{
		GetElement<Label>( text, l => l.Text = text );
	}

	// Separator
	public static void Separator()
	{
		GetElement<Panel>( $"sep_{_idStack}", p =>
		{
			p.Style.Height = 1;
			p.Style.BackgroundColor = Color.Parse( "#333333" );
		} );
	}
	/// <summary>
	/// Generic value control handler that manages state updates between UI controls and ref values
	/// </summary>
	private static bool HandleValueControl<T, TControl>( string label, ref T value, Action<TControl, T> setControlValue,
		Func<TControl, T> getControlValue, Action<Panel> setupContainer = null,
		Action<TControl> additionalSetup = null ) where TControl : Panel, new()
	{
		string id = GetId( label );
		var state = GetState( id );

		// First time initialization
		if ( !state.Values.ContainsKey( "value" ) )
			state.Values["value"] = value;

		var currentValue = value;

		var container = GetElement<Panel>( label, p =>
		{
			// Default container setup
			p.Style.FlexDirection = FlexDirection.Row;
			p.Style.AlignItems = Align.Center;
			p.Style.MarginBottom = 5;

			// Allow custom container setup
			setupContainer?.Invoke( p );

			if ( p.ChildrenCount == 0 )
			{
				// Create label for controls that need it
				if ( typeof( TControl ) != typeof( CheckBox ) )
				{
					var labelElement = new Label();
					labelElement.Text = label;
					labelElement.Style.Width = 100;
					p.AddChild( labelElement );
				}

				// Create the control
				var control = new TControl();

				// Set initial value
				setControlValue( control, (T)state.Values["value"] );

				// Additional control setup
				additionalSetup?.Invoke( control );

				p.AddChild( control );
			}
			else
			{
				// Get the control (either the panel itself for CheckBox or the second child)
				TControl control;
				if ( typeof( TControl ) == typeof( CheckBox ) )
					control = p as TControl;
				else
					control = p.Children.ElementAt( 1 ) as TControl;

				if ( control != null )
				{
					// Handle external value changes
					if ( !object.Equals( state.Values["value"], currentValue ) )
					{
						state.Values["value"] = currentValue;
						setControlValue( control, currentValue );
					}

					// Update state value if UI changed
					T currentControlValue = getControlValue( control );
					if ( !object.Equals( currentControlValue, state.Values["value"] ) )
					{
						state.Values["value"] = currentControlValue;
						state.Changed = true;
					}
				}
			}
		} );

		// If state changed, update ref param
		if ( state.Changed )
		{
			value = (T)state.Values["value"];
			state.Changed = false;
			return true;
		}

		return false;
	}

	// Checkbox control - refactored
	public static bool Checkbox( string label, ref bool value )
	{
		return HandleValueControl<bool, CheckBox>(
			label,
			ref value,
			( cb, val ) => cb.Checked = val,
			( cb ) => cb.Checked,
			p => { /* Custom container setup not needed for checkbox */ },
			cb =>
			{
				cb.LabelText = label;
				// Any additional checkbox setup
			}
		);
	}

	// Slider control - refactored
	public static bool Slider( string label, ref float value, float min, float max )
	{
		return HandleValueControl<float, SliderScale>(
			label,
			ref value,
			( slider, val ) => slider.Value = val,
			( slider ) => slider.Value,
			null, // Use default container setup
			slider =>
			{
				slider.MinValue = min;
				slider.MaxValue = max;
				slider.Style.FlexGrow = 1;
			}
		);
	}

	// Text input field - refactored
	public static bool InputText( string label, ref string value )
	{
		return HandleValueControl<string, TextEntry>(
			label,
			ref value,
			( input, val ) => input.Text = val,
			( input ) => input.Text,
			null, // Use default container setup
			input =>
			{
				input.Style.FlexGrow = 1;
			}
		);
	}

	// Clean up at the end of the frame
	public static void EndFrame()
	{
		if ( !IsInitialized() ) return;

		// Clean up unused windows
		var windowsToRemove = new List<string>();
		foreach ( var entry in _windows )
		{
			if ( !_activeWindows.Contains( entry.Key ) )
			{
				entry.Value.Delete();
				windowsToRemove.Add( entry.Key );
			}
		}

		foreach ( var key in windowsToRemove )
		{
			_windows.Remove( key );
			_elements.Remove( key );
			_elementState.Remove( key );
		}

		// Clean up unused elements in active windows
		foreach ( var windowId in _activeWindows )
		{
			if ( !_elements.ContainsKey( windowId ) ) continue;

			var elementsToRemove = new List<string>();
			foreach ( var entry in _elements[windowId] )
			{
				if ( !_activeElements.Contains( entry.Key ) )
				{
					entry.Value.Delete();
					elementsToRemove.Add( entry.Key );
				}
			}

			foreach ( var key in elementsToRemove )
			{
				_elements[windowId].Remove( key );
				if ( _elementState.ContainsKey( windowId ) )
					_elementState[windowId].Remove( key );
			}
		}
	}
}

