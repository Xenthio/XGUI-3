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
	private static Dictionary<string, List<Panel>> _windowElements = new();
	private static Dictionary<string, Dictionary<string, IMXGUIState>> _elementState = new();

	// Current state
	private static Window _currentWindow;
	private static Panel _currentPanel;
	private static string _currentWindowId;
	private static string _idStack;
	private static bool _initialized = false;
	private static Panel _rootPanel;

	// Element order tracking
	private static Dictionary<string, int> _elementCounters = new();
	private static Dictionary<string, List<string>> _elementOrder = new();
	private static int _currentLayoutIndex = 0;

	// Initialize the system
	public static bool IsInitialized() => _initialized && _rootPanel.IsValid;

	public static void Initialize()
	{
		if ( IsInitialized() ) return;

		if ( XGUIRootPanel.Current == null ) return;

		_rootPanel = XGUIRootPanel.Current;
		_windows.Clear();
		_windowElements.Clear();
		_elementState.Clear();
		_elementCounters.Clear();
		_elementOrder.Clear();
		_initialized = true;
	}

	// Start a new frame
	public static void NewFrame()
	{
		if ( !IsInitialized() ) Initialize();
		_activeWindows.Clear();
		_idStack = "";
	}

	// Begin a window
	public static bool Begin( string title, ref bool open, IMXGUIWindowFlags flags = IMXGUIWindowFlags.None )
	{
		if ( !open ) return false;
		if ( !IsInitialized() ) Initialize();

		_currentWindowId = title;
		_activeWindows.Add( title );
		_currentLayoutIndex = 0;

		// Initialize element order tracking for this window if needed
		if ( !_elementOrder.ContainsKey( title ) )
		{
			_elementOrder[title] = new List<string>();
		}

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

			// Initialize tracking collections for this window
			_windowElements[title] = new List<Panel>();
			_elementState[title] = new Dictionary<string, IMXGUIState>();
		}
		else
		{
			_currentWindow = _windows[title];
			_currentPanel = _currentWindow.Children.FirstOrDefault( x =>
				!(x is Label || x is Button || x.HasClass( "titlebar" )) ) as Panel;

			// Create collections if they don't exist
			if ( !_windowElements.ContainsKey( title ) )
				_windowElements[title] = new List<Panel>();

			if ( !_elementState.ContainsKey( title ) )
				_elementState[title] = new Dictionary<string, IMXGUIState>();

			// Hide all existing elements - we'll show them again as needed
			foreach ( var element in _windowElements[title] )
			{
				element.Style.Display = DisplayMode.None;
			}
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
		// Remove any elements that weren't used in this frame
		for ( int i = _currentLayoutIndex; i < _elementOrder[_currentWindowId].Count; i++ )
		{
			string id = _elementOrder[_currentWindowId][i];
			int elementIndex = _windowElements[_currentWindowId].FindIndex( e => e.ElementName == id );
			if ( elementIndex >= 0 )
			{
				_windowElements[_currentWindowId][elementIndex].Delete();
				_windowElements[_currentWindowId].RemoveAt( elementIndex );
			}
		}

		// Truncate the element order list
		if ( _currentLayoutIndex < _elementOrder[_currentWindowId].Count )
		{
			_elementOrder[_currentWindowId].RemoveRange( _currentLayoutIndex,
				_elementOrder[_currentWindowId].Count - _currentLayoutIndex );
		}

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

	// Generate a unique ID for a control based on its type and layout order
	private static string GenerateId<T>( string label = null ) where T : Panel
	{
		// Create a base ID from the stack and control type
		string typeKey = typeof( T ).Name;
		string baseId = string.IsNullOrEmpty( _idStack ) ? typeKey : $"{_idStack}/{typeKey}";

		// Get the counter for this type of element
		if ( !_elementCounters.TryGetValue( baseId, out int counter ) )
		{
			counter = 0;
		}

		// Generate the unique ID
		string uniqueId = $"{baseId}_{counter}";

		// Increment the counter for next time
		_elementCounters[baseId] = counter + 1;

		return uniqueId;
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

	// Get or create an element with proper ordering
	private static T GetOrCreateElement<T>( string label, Action<T> setupAction ) where T : Panel, new()
	{
		if ( _currentPanel == null ) return null;

		// Generate a unique ID for this element based on its position in the layout
		string id;

		// Check if we're reusing an existing element
		if ( _currentLayoutIndex < _elementOrder[_currentWindowId].Count )
		{
			// Reuse existing element ID
			id = _elementOrder[_currentWindowId][_currentLayoutIndex];
			_currentLayoutIndex++;

			// Find element with this ID
			Panel existingElement = _windowElements[_currentWindowId].FirstOrDefault( e => e.ElementName == id );

			// If found and type matches, reuse it
			if ( existingElement != null && existingElement is T typedElement )
			{
				existingElement.Style.Display = DisplayMode.Flex;
				setupAction?.Invoke( typedElement );
				return typedElement;
			}

			// If found but wrong type, delete and recreate
			if ( existingElement != null )
			{
				int index = _windowElements[_currentWindowId].IndexOf( existingElement );
				existingElement.Delete();
				_windowElements[_currentWindowId].RemoveAt( index );
			}
		}
		else
		{
			// Create a new ID for this element
			id = GenerateId<T>( label );
			_elementOrder[_currentWindowId].Add( id );
			_currentLayoutIndex++;
		}

		// Create a new element
		T element = new T();
		element.ElementName = id;

		// Setup the element
		setupAction?.Invoke( element );

		// Add to panel and tracking
		_currentPanel.AddChild( element );
		_windowElements[_currentWindowId].Add( element );

		return element;
	}

	// Button control
	public static bool Button( string label )
	{
		bool clicked = false;
		var button = GetOrCreateElement<Button>( label, b =>
		{
			b.Text = label;

			// Check for click state
			if ( b.HasActive &&
				(!_elementState[_currentWindowId].TryGetValue( b.ElementName, out var state ) ||
				 !state.Values.TryGetValue( "wasActive", out var wasActive ) ||
				 !(bool)wasActive) )
			{
				clicked = true;
			}

			// Update state
			var btnState = GetState( b.ElementName );
			btnState.Values["wasActive"] = b.HasActive;
		} );

		return clicked;
	}

	// Text display
	public static void Text( string text )
	{
		GetOrCreateElement<Label>( text, l => l.Text = text );
	}

	// Separator
	public static void Separator()
	{
		GetOrCreateElement<Panel>( "separator", p =>
		{
			p.Style.Height = 1;
			p.Style.BackgroundColor = Color.Parse( "#333333" );
		} );
	}

	// Generic value control handler
	private static bool HandleValueControl<T, TControl>( string label, ref T value,
		Action<TControl, T> setControlValue, Func<TControl, T> getControlValue,
		Action<Panel> setupContainer = null, Action<TControl> additionalSetup = null )
		where TControl : Panel, new()
	{
		// Get state for this control
		string id = GenerateId<TControl>( label );
		var state = GetState( id );

		bool changed = false;
		var currentValue = value;

		var container = GetOrCreateElement<Panel>( label, p =>
		{
			// Default container setup
			p.Style.FlexDirection = FlexDirection.Row;
			p.Style.AlignItems = Align.Center;
			p.Style.MarginBottom = 5;

			// Allow custom container setup
			setupContainer?.Invoke( p );

			// Initialize value if needed
			if ( !state.Values.ContainsKey( "value" ) )
			{
				state.Values["value"] = currentValue;
			}

			// Create or update children
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

				// Create control
				var control = new TControl();

				// Set initial value
				setControlValue( control, (T)state.Values["value"] );

				// Additional setup
				additionalSetup?.Invoke( control );

				p.AddChild( control );
			}
			else
			{
				// Get the control
				TControl control;
				if ( typeof( TControl ) == typeof( CheckBox ) )
					control = p as TControl;
				else
					control = p.Children.ElementAt( 1 ) as TControl;

				if ( control != null )
				{
					// Check if external value changed
					if ( !object.Equals( state.Values["value"], currentValue ) )
					{
						state.Values["value"] = currentValue;
						setControlValue( control, currentValue );
					}

					// Check if control value changed
					T controlValue = getControlValue( control );
					if ( !object.Equals( controlValue, state.Values["value"] ) )
					{
						state.Values["value"] = controlValue;
						state.Changed = true;
					}
				}
			}

		} );
		// Update ref parameter if state changed
		if ( state != null && state.Changed )
		{
			value = (T)state.Values["value"];
			changed = true;
			state.Changed = false;
		}

		return changed;
	}

	// Checkbox control
	public static bool Checkbox( string label, ref bool value )
	{
		return HandleValueControl<bool, CheckBox>(
			label,
			ref value,
			( cb, val ) => cb.Checked = val,
			( cb ) => cb.Checked,
			null,
			cb => cb.LabelText = label
		);
	}

	// Slider control
	public static bool Slider( string label, ref float value, float min, float max )
	{
		return HandleValueControl<float, SliderScale>(
			label,
			ref value,
			( slider, val ) => slider.Value = val,
			( slider ) => slider.Value,
			null,
			slider =>
			{
				slider.MinValue = min;
				slider.MaxValue = max;
				slider.Style.FlexGrow = 1;
			}
		);
	}

	// Text input field
	public static bool InputText( string label, ref string value )
	{
		return HandleValueControl<string, TextEntry>(
			label,
			ref value,
			( input, val ) => input.Text = val,
			( input ) => input.Text,
			null,
			input => input.Style.FlexGrow = 1
		);
	}

	// Clean up at the end of the frame
	public static void EndFrame()
	{
		if ( !IsInitialized() ) return;

		// Reset element counters for next frame
		_elementCounters.Clear();

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
			_windowElements.Remove( key );
			_elementState.Remove( key );
			_elementOrder.Remove( key );
		}
	}
}
