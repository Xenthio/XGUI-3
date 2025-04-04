using Sandbox;
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
public partial class ImXGUI // Todo, partial to move things out into seperate files.
{
	public static string CurrentStyle = "/XGUI/DefaultStyles/OliveGreen.scss";

	// --- Global State ---
	private static Dictionary<string, Window> _windows = new(); // All windows that exist
	private static Dictionary<string, List<Panel>> _windowElements = new(); // Elements per window
	private static Dictionary<string, Dictionary<string, IMXGUIState>> _elementState = new(); // State per element per window
	private static bool _initialized = false;
	private static Panel _rootPanel;

	// --- Context-Specific State ---
	private static string _currentContext = null; // Identifies the currently active frame context ("Update", "FixedUpdate", etc.)
	private static Scene _currentScene = null; // Identifies the currently active frame context ("Update", "FixedUpdate", etc.)
	private static Dictionary<string, HashSet<string>> _activeWindowsPerContext = new(); // Windows active *this frame* for each context
	private static Dictionary<string, Dictionary<string, int>> _elementCountersPerContext = new(); // Element counters per window *within each context*

	// --- Current Processing State (scoped by _currentContext) ---
	private static Window _currentWindow;
	private static Panel _currentPanel;
	private static string _currentWindowId;
	private static string _idStack;


	// Initialize the system
	public static bool IsInitialized() => _initialized && _rootPanel != null && _rootPanel.IsValid;

	public static void Initialize()
	{
		if ( IsInitialized() ) return;

		if ( _rootPanel == null )
		{
			return;
		}

		//_rootPanel = XGUIRootPanel.Current;
		_windows.Clear();
		_windowElements.Clear();
		_elementState.Clear();
		_activeWindowsPerContext.Clear();
		_elementCountersPerContext.Clear();
		_initialized = true;
		Log.Info( "ImXGUI Initialized." );
	}

	// Start a new frame for a specific context
	public static void NewFrame( string context, Scene scene = null )
	{
		if ( scene == null ) scene = Game.ActiveScene;

		_currentScene = scene;
		_rootPanel = scene.GetSystem<XGUISystem>()?.Panel;

		if ( string.IsNullOrWhiteSpace( context ) )
		{
			Log.Error( "ImXGUI.NewFrame: Context cannot be null or empty." );
			return;
		}
		if ( !IsInitialized() ) Initialize();
		if ( !IsInitialized() ) return; // Check again if Initialize failed

		_currentContext = context;
		_idStack = ""; // Reset ID stack for the new frame context

		// Ensure context exists in tracking dictionaries
		if ( !_activeWindowsPerContext.ContainsKey( context ) )
		{
			_activeWindowsPerContext[context] = new HashSet<string>();
		}
		else
		{
			// Clear active windows *only for this context*
			_activeWindowsPerContext[context].Clear();
		}

		if ( !_elementCountersPerContext.ContainsKey( context ) )
		{
			_elementCountersPerContext[context] = new Dictionary<string, int>();
		}
		// else: Don't clear counters here, they get reset in Begin() per window

	}

	// Begin a window (operates within the _currentContext)
	public static bool Begin( string title, ref bool open, IMXGUIWindowFlags flags = IMXGUIWindowFlags.None )
	{
		if ( _currentContext == null )
		{
			Log.Error( $"ImXGUI.Begin('{title}'): Must be called between NewFrame(context) and EndFrame(context)." );
			return false;
		}
		if ( !open ) return false;
		if ( !IsInitialized() ) Initialize();
		if ( !IsInitialized() ) return false;

		_currentWindowId = title;
		_activeWindowsPerContext[_currentContext].Add( title ); // Add to the current context's active set

		// Ensure element counter dictionary exists for this context
		if ( !_elementCountersPerContext.ContainsKey( _currentContext ) )
		{
			_elementCountersPerContext[_currentContext] = new Dictionary<string, int>();
		}

		// Check if window exists and is valid globally
		if ( _windows.TryGetValue( title, out _currentWindow ) )
		{
			if ( !_currentWindow.IsValid || _currentWindow.Parent == null )
			{
				// Clean up if invalid (might happen if deleted externally)
				CleanupWindowReferences( title );
				_currentWindow = null;
			}
		}

		// Create new window if needed (global creation)
		if ( _currentWindow == null )
		{
			_currentWindow = new Window();
			_currentWindow.TitleLabel.Text = title;
			ApplyWindowFlags( _currentWindow, flags );

			_currentWindow.StyleSheet.Load( CurrentStyle );
			_rootPanel.AddChild( _currentWindow );
			_windows[title] = _currentWindow; // Add to global list

			// Setup content panel
			_currentPanel = new Panel();
			_currentPanel.ElementName = $"{title}_ContentPanel"; // Give it a name for debugging
			_currentPanel.SetClass( "window-content-panel", true ); // Add a class for styling
			_currentPanel.Style.FlexDirection = FlexDirection.Column;
			_currentPanel.Style.OverflowY = OverflowMode.Scroll; // Default scroll
			_currentPanel.Style.FlexGrow = 1;
			_currentWindow.AddChild( _currentPanel );

			// Initialize global tracking collections for this window
			_windowElements[title] = new List<Panel>();
			_elementState[title] = new Dictionary<string, IMXGUIState>();

			_currentWindow.FocusWindow();
		}
		else // Window already exists
		{
			_currentWindow = _windows[title];
			// Find the content panel (assuming it's the main panel that's not titlebar elements)
			_currentPanel = _currentWindow.Children.FirstOrDefault( x =>
				x is Panel && !(x is Label || x is Button || x.HasClass( "titlebar" )) ) as Panel;

			if ( _currentPanel == null )
			{
				Log.Error( $"ImXGUI.Begin('{title}'): Could not find content panel for existing window." );
				// Attempt recovery: Create a new one? Or just fail? Failing is safer.
				return false;
			}

			// Ensure global tracking lists exist (might have been cleared?)
			if ( !_windowElements.ContainsKey( title ) )
				_windowElements[title] = new List<Panel>();
			if ( !_elementState.ContainsKey( title ) )
				_elementState[title] = new Dictionary<string, IMXGUIState>();

			// Hide all existing elements *associated with this window globally*.
			// They will be re-shown if GetOrCreateElement is called for them in this frame.
			foreach ( var element in _windowElements[title] )
			{
				if ( element.IsValid() ) // Check validity before accessing style
				{
					element.Style.Display = DisplayMode.None;
				}
			}

		}

		// Reset element counter *for this window within the current context*
		_elementCountersPerContext[_currentContext][title] = 0;
		_idStack = title; // Set ID stack root for this window

		return true;
	}

	/// <summary>
	/// Apply window flags
	/// </summary>
	/// <param name="window"></param>
	/// <param name="flags"></param>
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

		window.Style.Width = 320;
		window.Style.Height = 300;
	}

	// End a window (operates within the _currentContext)
	public static void End()
	{
		if ( _currentContext == null )
		{
			Log.Error( "ImXGUI.End(): Must be called between NewFrame(context) and EndFrame(context)." );
			return;
		}
		if ( _currentWindow == null || !_windowElements.ContainsKey( _currentWindowId ) )
		{
			// Likely called End() without a matching Begin() or window got deleted improperly
			Log.Warning( "ImXGUI.End(): No current window or element list found. Skipping cleanup for this frame." );
			_currentWindow = null;
			_currentPanel = null;
			_idStack = "";
			_currentWindowId = null;
			return;
		}


		// Clean up unused elements *within this window* based on their display state
		List<Panel> elementsToRemove = new List<Panel>();
		foreach ( var element in _windowElements[_currentWindowId] )
		{
			// Check IsValid before accessing properties
			if ( !element.IsValid() || element.Style.Display == DisplayMode.None )
			{
				elementsToRemove.Add( element );
			}
		}

		foreach ( var element in elementsToRemove )
		{
			if ( element.IsValid() ) element.Delete(); // Delete the panel itself
			_windowElements[_currentWindowId].Remove( element ); // Remove from tracking
																 // TODO: Optionally remove state from _elementState here if elements are truly temporary?
																 // For now, state persists even if element is removed temporarily.
		}

		// Reset current window state for the *next* Begin() call in this context
		_currentWindow = null;
		_currentPanel = null;
		_idStack = "";
		_currentWindowId = null;
	}

	/// <summary>
	/// Push ID to stack (for hierarchical controls)
	/// </summary>
	/// <param name="id"></param>
	public static void PushId( string id )
	{
		_idStack = string.IsNullOrEmpty( _idStack ) ? id : $"{_idStack}/{id}";
	}

	/// <summary>
	/// Pop ID from stack
	/// </summary>
	public static void PopId()
	{
		int lastSlash = _idStack.LastIndexOf( '/' );
		if ( lastSlash >= 0 )
			_idStack = _idStack.Substring( 0, lastSlash );
		else
			_idStack = "";
	}

	// Generate a unique ID (uses _currentContext)
	private static string GenerateId<T>( string label = null ) where T : Panel
	{
		if ( _currentContext == null || _currentWindowId == null )
		{
			Log.Error( "ImXGUI.GenerateId: Cannot generate ID outside of a Begin/End block or NewFrame/EndFrame context." );
			return $"ERROR_NO_CONTEXT_{Guid.NewGuid()}"; // Return something unique but indicative of error
		}

		string typeKey = typeof( T ).Name;
		// Use _idStack which is reset per-window in Begin()
		string baseId = string.IsNullOrEmpty( _idStack ) ? typeKey : $"{_idStack}/{typeKey}";

		// Get the element counter dictionary for the current context
		var contextCounters = _elementCountersPerContext[_currentContext];

		// Get the counter for the current window *within the current context*
		if ( !contextCounters.TryGetValue( _currentWindowId, out int counter ) )
		{
			counter = 0; // Should have been initialized in Begin, but safety first
		}

		// Generate the unique ID (Context/Window/ElementPath/Counter)
		// Adding context helps debugging but might make IDs very long. Let's omit for now.
		// string uniqueId = $"{_currentContext}/{_currentWindowId}/{baseId}_{counter}";
		string uniqueId = $"{_currentWindowId}/{baseId}_{counter}"; // Keep it simpler

		// Increment the counter for the current window in the current context
		contextCounters[_currentWindowId] = counter + 1;

		return uniqueId;
	}

	// Get or create state (operates on global state, keyed by window/element ID)
	private static IMXGUIState GetState( string elementId ) // Renamed param for clarity
	{
		// Element state is tied to the window ID, which is global
		if ( _currentWindowId == null ) return null; // Safety check

		// Ensure the window dictionary exists
		if ( !_elementState.ContainsKey( _currentWindowId ) )
		{
			_elementState[_currentWindowId] = new Dictionary<string, IMXGUIState>();
		}

		// Get or create the specific element's state
		if ( !_elementState[_currentWindowId].TryGetValue( elementId, out var state ) )
		{
			state = new IMXGUIState();
			_elementState[_currentWindowId][elementId] = state;
		}

		return state;
	}

	// Get or create element (no fundamental change, uses context indirectly via GenerateId)
	private static T GetOrCreateElement<T>( string label, Action<T> setupAction ) where T : Panel, new()
	{
		if ( _currentPanel == null || !_currentPanel.IsValid() ) // Check validity
		{
			Log.Warning( $"ImXGUI.GetOrCreateElement<{typeof( T ).Name}>('{label}'): Current panel is null or invalid. Cannot create element." );
			return null;
		}


		// Generate a unique ID for this element *within the current context and window*
		string id = GenerateId<T>( label );
		if ( id.StartsWith( "ERROR_" ) ) return null; // Don't proceed if ID generation failed

		// Ensure element list exists for the current window *globally*
		EnsureWindowElementsListExists( _currentWindowId );

		// Try to find an existing element with this ID within the current window's global list
		// Use ElementName which we set during creation
		T element = _windowElements[_currentWindowId]
						.OfType<T>()
						.FirstOrDefault( e => e.IsValid() && e.ElementName == id ); // Add IsValid check

		// If an element with the same ID *and* type exists and is valid, reuse it.
		if ( element != null )
		{
			element.Style.Display = DisplayMode.Flex; // Make sure it's visible
			element.Parent = _currentPanel; // Ensure correct parent if panel changed
			setupAction?.Invoke( element ); // Re-apply setup (e.g., update text)
			return element;
		}
		else
		{
			// Element might exist in the list but be invalid, remove the invalid ref
			_windowElements[_currentWindowId].RemoveAll( p => !p.IsValid() );
		}

		// --- Create a new element ---
		element = new T();
		element.ElementName = id; // Assign the generated ID

		setupAction?.Invoke( element );
		_currentPanel.AddChild( element ); // Add to the current content panel
		_windowElements[_currentWindowId].Add( element ); // Add to the window's global element list

		return element;
	}

	// Ensure window element list exists
	private static void EnsureWindowElementsListExists( string windowId )
	{
		if ( !_windowElements.ContainsKey( windowId ) )
		{
			_windowElements[windowId] = new List<Panel>();
		}
	}

	// End the frame for a specific context and perform cleanup
	public static void EndFrame( string context )
	{
		if ( _currentContext != context && _currentContext != null ) // Allow calling EndFrame if no context was active
		{
			Log.Error( $"ImXGUI.EndFrame: Mismatched context. Expected '{_currentContext ?? "null"}', got '{context}'. Make sure NewFrame/EndFrame calls are balanced for each context." );
			// Don't proceed with cleanup for the wrong context, but reset current context maybe?
			_currentContext = null; // Attempt to recover state
			return;
		}
		if ( !IsInitialized() ) return; // Nothing to do if not initialized

		// --- Window Cleanup Logic ---
		// A window should only be deleted if it wasn't active in *any* context during this cycle.
		// This requires a more complex cleanup strategy, usually done once per game frame AFTER all contexts have run.
		// For now, let's stick to removing windows that were *globally* tracked but *not* activated
		// in the context that just finished. This might prematurely delete windows intended
		// for other contexts if called out of order. A dedicated system is better.

		// **** TEMPORARY / SIMPLIFIED CLEANUP ****
		// This might remove windows needed by other contexts if EndFrame calls are interleaved incorrectly.
		// A better approach uses a separate global cleanup step (like in the new system below).
		/*
		var globallyKnownWindows = _windows.Keys.ToList();
		var activeInThisContext = _activeWindowsPerContext.ContainsKey(context) ? _activeWindowsPerContext[context] : new HashSet<string>();

		foreach ( var windowId in globallyKnownWindows )
		{
			if ( !activeInThisContext.Contains( windowId ) )
			{
				// Before deleting, maybe check if it's active in *other* contexts?
				bool activeElsewhere = false;
				foreach(var kvp in _activeWindowsPerContext)
				{
					if (kvp.Key != context && kvp.Value.Contains(windowId))
					{
						activeElsewhere = true;
						break;
					}
				}

				if (!activeElsewhere)
				{
					CleanupWindowReferences( windowId ); // Delete and remove refs
				}
			}
		}
		*/
		// **** END TEMPORARY CLEANUP ****

		// Reset the current context indicator
		_currentContext = null;
	}

	/// <summary>
	/// Cleans up all global references to a specific window ID.
	/// Should be called when a window is determined to be truly inactive across all contexts.
	/// </summary>
	private static void CleanupWindowReferences( string windowId )
	{
		if ( _windows.TryGetValue( windowId, out var window ) )
		{
			if ( window.IsValid() )
			{
				window.Delete();
			}
			_windows.Remove( windowId );
		}
		_windowElements.Remove( windowId );
		_elementState.Remove( windowId );

		// Also remove from context-specific counters
		foreach ( var contextCounters in _elementCountersPerContext.Values )
		{
			contextCounters.Remove( windowId );
		}
		// Also remove from context-specific active lists (though they should be clear already)
		foreach ( var activeSet in _activeWindowsPerContext.Values )
		{
			activeSet.Remove( windowId );
		}
		Log.Info( $"ImXGUI Cleanup: Removed window '{windowId}'" );
	}

	/// <summary>
	/// Performs a global cleanup pass, removing windows that were not active in *any* context
	/// during the last full update cycle. Designed to be called once per game frame after all
	/// ImXGUI contexts have finished (e.g., in PostRender).
	/// </summary>
	public static void PerformGlobalCleanup()
	{
		if ( !IsInitialized() ) return;
		if ( _currentContext != null )
		{
			Log.Warning( $"ImXGUI.PerformGlobalCleanup: Called while context '{_currentContext}' is still active. EndFrame may not have been called properly." );
			// Optionally force EndFrame? Or just skip cleanup this cycle.
			// return;
		}

		// Combine all windows that were active in *any* context during this cycle
		HashSet<string> allActiveWindowsThisCycle = new HashSet<string>();
		foreach ( var activeSet in _activeWindowsPerContext.Values )
		{
			allActiveWindowsThisCycle.UnionWith( activeSet );
		}

		var windowsToRemove = new List<string>();
		// Find globally known windows that were NOT active in any context
		foreach ( var windowId in _windows.Keys )
		{
			if ( !allActiveWindowsThisCycle.Contains( windowId ) )
			{
				windowsToRemove.Add( windowId );
			}
		}

		// Perform the actual cleanup
		foreach ( var windowId in windowsToRemove )
		{
			CleanupWindowReferences( windowId );
		}

		// Clear the context-specific active lists for the *next* cycle AFTER cleanup
		// foreach(var kvp in _activeWindowsPerContext)
		// {
		//     kvp.Value.Clear(); // Clearing here might be wrong if NewFrame hasn't run yet for next cycle
		// }
		// NewFrame already handles clearing its own context's list.
	}

}
