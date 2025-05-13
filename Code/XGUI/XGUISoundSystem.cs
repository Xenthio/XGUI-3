using Sandbox;
using System.Collections.Generic;

namespace XGUI;

/// <summary>
/// Look up sounds respective to the current theme. You can look up sounds by name for UI events, notifications, etc.
/// </summary>
public static class XGUISoundSystem
{
	private const string DefaultThemeName = "Computer95";
	private static string _currentThemeName = DefaultThemeName;

	// Cache for sound paths - theme/soundName -> path
	private static Dictionary<string, string> SoundPathCache = new();

	/// <summary>
	/// Get or set the current theme name.
	/// </summary>
	public static string CurrentTheme
	{
		get => _currentThemeName;
		set
		{
			if ( _currentThemeName != value )
			{
				_currentThemeName = value;
				SoundPathCache.Clear(); // Invalidate cache when theme changes
			}
		}
	}

	/// <summary>
	/// Get the base directory for the current theme's sounds.
	/// </summary>
	private static string GetThemeSoundBaseDirectory( string themeName )
	{
		return $"XGUI/Resources/{themeName}/Sounds";
	}

	/// <summary>
	/// Look up a sound by name.
	/// </summary>
	/// <param name="soundName">The name of the sound (without extension)</param>
	/// <returns>The path to the sound file, or null if not found</returns>
	public static string GetSound( string soundName )
	{
		if ( string.IsNullOrEmpty( soundName ) )
			return null;

		soundName = soundName.ToLowerInvariant();

		string cacheKey = $"{_currentThemeName}/{soundName}";
		if ( SoundPathCache.TryGetValue( cacheKey, out var cachedPath ) )
			return cachedPath;

		// Try current theme
		string soundPath = FindSoundInTheme( _currentThemeName, soundName );

		// Fallback to default theme if not found
		if ( string.IsNullOrEmpty( soundPath ) && _currentThemeName != DefaultThemeName )
		{
			soundPath = FindSoundInTheme( DefaultThemeName, soundName );
		}

		// Optionally, fallback to a generic sound (e.g., "default.wav") if not found
		if ( string.IsNullOrEmpty( soundPath ) )
		{
			soundPath = FindSoundInTheme( _currentThemeName, "default" ) ??
						FindSoundInTheme( DefaultThemeName, "default" );
		}

		if ( !string.IsNullOrEmpty( soundPath ) )
		{
			SoundPathCache[cacheKey] = soundPath;
		}

		return soundPath;
	}

	/// <summary>
	/// Find a sound in a specific theme.
	/// </summary>
	private static string FindSoundInTheme( string themeName, string soundName )
	{
		string baseDir = GetThemeSoundBaseDirectory( themeName );
		string filePath = $"{baseDir}/{soundName}.wav";

		if ( FileSystem.Mounted.FileExists( filePath ) )
			return filePath;

		return null;
	}

	/// <summary>
	/// Clear the sound cache.
	/// </summary>
	public static void ClearCache()
	{
		SoundPathCache.Clear();
	}

	[ConCmd( "xgui_sound_reset_cache" )]
	public static void ResetSoundCache()
	{
		ClearCache();
		Log.Info( "XGUI Sound cache cleared." );
	}
}
