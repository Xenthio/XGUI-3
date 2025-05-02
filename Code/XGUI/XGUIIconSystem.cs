using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System.IO;

namespace XGUI;

/// <summary>
/// Look up icons respective to the current theme, you can look up icons by name to use in buttons and panels, or icons for file types to use in a file browser.
/// </summary>
public static class XGUIIconSystem
{
	private const string DefaultThemeName = "Computer95";
	private static string _currentThemeName = DefaultThemeName;

	// Cache for icon paths - theme/type/name/size -> path
	private static Dictionary<string, string> IconPathCache = new Dictionary<string, string>();

	/// <summary>
	/// Supported icon types
	/// </summary>
	public enum IconType
	{
		/// <summary>
		/// Standard UI icons (menus, buttons, controls)
		/// </summary>
		UI,

		/// <summary>
		/// Icons for file types
		/// </summary>
		FileType,

		/// <summary>
		/// Icons for folders
		/// </summary>
		Folder
	}

	/// <summary>
	/// Get the current theme name
	/// </summary>
	public static string CurrentTheme
	{
		get => _currentThemeName;
		set
		{
			if ( _currentThemeName != value )
			{
				_currentThemeName = value;
				IconPathCache.Clear(); // Invalidate cache when theme changes
			}
		}
	}

	/// <summary>
	/// Get the base directory for the current theme's icons
	/// </summary>
	private static string GetThemeIconBaseDirectory( string themeName )
	{
		return $"XGUI/Resources/{themeName}/Icons";
	}

	/// <summary>
	/// Get the type-specific sub-directory for icons
	/// </summary>
	private static string GetIconTypeDirectory( IconType iconType )
	{
		return iconType switch
		{
			IconType.UI => "UI",
			IconType.FileType => "FileTypes",
			IconType.Folder => "Folders",
			_ => string.Empty
		};
	}

	/// <summary>
	/// Look up an icon by name and size
	/// </summary>
	/// <param name="iconName">The name of the icon</param>
	/// <param name="iconType">The type of icon</param>
	/// <param name="size">The desired size of the icon (16, 24, 32, 48, etc.)</param>
	/// <returns>The path to the icon file, or null if not found</returns>
	public static string GetIcon( string iconName, IconType iconType = IconType.UI, int size = 16 )
	{
		// Standardize icon name
		if ( string.IsNullOrEmpty( iconName ) )
			return null;

		iconName = iconName.ToLowerInvariant();

		// Check cache first
		string cacheKey = $"{_currentThemeName}/{iconType}/{iconName}/{size}";
		if ( IconPathCache.TryGetValue( cacheKey, out string cachedPath ) )
			return cachedPath;

		// Look up icon in current theme
		string iconPath = FindIconInTheme( _currentThemeName, iconName, iconType, size );

		// If not found in current theme, try default theme
		if ( string.IsNullOrEmpty( iconPath ) && _currentThemeName != DefaultThemeName )
		{
			iconPath = FindIconInTheme( DefaultThemeName, iconName, iconType, size );
		}

		// If still not found, try Material Icons as fallback
		if ( string.IsNullOrEmpty( iconPath ) )
		{
			// For UI icons, try to use Material Icons
			if ( iconType == IconType.UI && IsMaterialIcon( iconName ) )
			{
				// Return the icon name with special prefix to indicate it's a material icon
				iconPath = $"material:{iconName}";
			}
			else
			{
				// For file types, use a generic file icon
				if ( iconType == IconType.FileType )
				{
					iconPath = FindIconInTheme( _currentThemeName, "file", iconType, size ) ??
							   FindIconInTheme( DefaultThemeName, "file", iconType, size );
				}
				// For folders, use a generic folder icon
				else if ( iconType == IconType.Folder )
				{
					iconPath = FindIconInTheme( _currentThemeName, "folder", iconType, size ) ??
							   FindIconInTheme( DefaultThemeName, "folder", iconType, size );
				}
			}
		}

		// Cache the result for faster lookups
		if ( !string.IsNullOrEmpty( iconPath ) )
		{
			IconPathCache[cacheKey] = iconPath;
		}

		return iconPath;
	}

	/// <summary>
	/// Get an icon for a specific file extension
	/// </summary>
	/// <param name="extension">The file extension (with or without the dot)</param>
	/// <param name="size">The desired icon size</param>
	/// <returns>The path to the icon file</returns>
	public static string GetFileIcon( string extension, int size = 16 )
	{
		if ( string.IsNullOrEmpty( extension ) )
			return GetIcon( "file", IconType.FileType, size );

		// Normalize extension
		if ( extension.StartsWith( "." ) )
			extension = extension.Substring( 1 );

		extension = extension.ToLowerInvariant();

		// Try to find an icon for this specific extension
		return GetIcon( extension, IconType.FileType, size );
	}

	/// <summary>
	/// Get a folder icon
	/// </summary>
	/// <param name="folderType">The type of folder (optional)</param>
	/// <param name="size">The desired icon size</param>
	/// <returns>The path to the icon file</returns>
	public static string GetFolderIcon( string folderType = "folder", int size = 16 )
	{
		return GetIcon( folderType, IconType.Folder, size );
	}


	static bool findingDefaultIconPreventRecursion = false;
	/// <summary>
	/// Find an icon in a specific theme
	/// </summary>
	private static string FindIconInTheme( string themeName, string iconName, IconType iconType, int size )
	{
		string baseDir = GetThemeIconBaseDirectory( themeName );
		string typeDir = GetIconTypeDirectory( iconType );

		// Try exact size first
		string exactSizePath = $"{baseDir}/{typeDir}/{iconName}_{size}.png";
		if ( FileSystem.Mounted.FileExists( exactSizePath ) )
			return exactSizePath;

		// If not found, try to find the closest size
		List<int> availableSizes = new List<int>();

		// Try to find all available sizes for this icon
		foreach ( var file in FileSystem.Mounted.FindFile( $"{baseDir}/{typeDir}" ) )
		{
			string fileName = Path.GetFileNameWithoutExtension( file );
			if ( !fileName.StartsWith( iconName + "_" ) )
				continue;

			// Extract size from filename (format: name_size.png)
			string sizeStr = fileName.Substring( iconName.Length + 1 );
			if ( int.TryParse( sizeStr, out int fileSize ) )
			{
				availableSizes.Add( fileSize );
			}
		}

		// If no sizes found, return null
		if ( availableSizes.Count == 0 )
		{
			Log.Warning( $"Icon not found: {iconName} in theme {themeName} of type {iconType}" );
			Log.Info( $"Add to path: {baseDir}/{typeDir}/{iconName}_{size}.png" );

			// Return a generic file icon if this is a file type

			if ( iconType == IconType.FileType && !findingDefaultIconPreventRecursion )
			{
				findingDefaultIconPreventRecursion = true;
				return FindIconInTheme( themeName, "file", iconType, size ) ??
					   FindIconInTheme( DefaultThemeName, "file", iconType, size );
			}
			findingDefaultIconPreventRecursion = false;

			return null;
		}

		// Find closest size (prefer larger sizes)
		availableSizes.Sort();
		int closestSize = availableSizes[0];
		foreach ( int availableSize in availableSizes )
		{
			if ( availableSize >= size )
			{
				closestSize = availableSize;
				break;
			}

			// Keep track of the largest size smaller than requested
			closestSize = availableSize;
		}

		// Return the closest size
		return $"{baseDir}/{typeDir}/{iconName}_{closestSize}.png";
	}

	/// <summary>
	/// Check if an icon name is a valid Material Icons name
	/// </summary>
	private static bool IsMaterialIcon( string iconName )
	{
		// This is a simplification - in a real implementation you'd check against a list of valid Material Icons
		// For now, we'll just return true to use Material Icons as a fallback
		return true;
	}

	/// <summary>
	/// Clear the icon cache
	/// </summary>
	public static void ClearCache()
	{
		IconPathCache.Clear();
	}

	[ConCmd( "xgui_icon_reset_cache" )]
	public static void ResetIconCache()
	{
		ClearCache();
		Log.Info( "XGUI Icon cache cleared." );
	}
}

/// <summary>
/// Icon panel that uses the XGUIIconSystem to look up icons based on the current theme
/// </summary>
public class XGUIIconPanel : Panel
{
	private string _iconName;
	private XGUIIconSystem.IconType _iconType = XGUIIconSystem.IconType.UI;
	private int _iconSize = 16;
	private Image _iconImage;
	private Label _materialIconLabel;

	/// <summary>
	/// The name of the icon
	/// </summary>
	public string IconName
	{
		get => _iconName;
		set
		{
			if ( _iconName != value )
			{
				_iconName = value;
				UpdateIcon();
			}
		}
	}

	/// <summary>
	/// The type of icon
	/// </summary>
	public XGUIIconSystem.IconType IconType
	{
		get => _iconType;
		set
		{
			if ( _iconType != value )
			{
				_iconType = value;
				UpdateIcon();
			}
		}
	}

	/// <summary>
	/// The desired size of the icon
	/// </summary>
	public int IconSize
	{
		get => _iconSize;
		set
		{
			if ( _iconSize != value )
			{
				_iconSize = value;
				UpdateIcon();
			}
		}
	}

	public XGUIIconPanel()
	{
		AddClass( "xgui-icon-panel" );

		// Create the icon image
		_iconImage = AddChild<Image>();
		_iconImage.AddClass( "icon-image" );

		// Create the material icon label
		_materialIconLabel = AddChild<Label>();
		_materialIconLabel.AddClass( "material-icon" );

		// Hide both by default
		_iconImage.Style.Display = DisplayMode.None;
		_materialIconLabel.Style.Display = DisplayMode.None;
	}

	public XGUIIconPanel( string iconName, XGUIIconSystem.IconType iconType = XGUIIconSystem.IconType.UI, int iconSize = 16 )
		: this()
	{
		_iconName = iconName;
		_iconType = iconType;
		_iconSize = iconSize;
		UpdateIcon();
	}

	/// <summary>
	/// Update the icon based on current properties
	/// </summary>
	private void UpdateIcon()
	{
		if ( string.IsNullOrEmpty( _iconName ) )
		{
			_iconImage.Style.Display = DisplayMode.None;
			_materialIconLabel.Style.Display = DisplayMode.None;
			return;
		}

		string iconPath = XGUIIconSystem.GetIcon( _iconName, _iconType, _iconSize );

		if ( string.IsNullOrEmpty( iconPath ) )
		{
			_iconImage.Style.Display = DisplayMode.None;
			_materialIconLabel.Style.Display = DisplayMode.None;
		}
		else if ( iconPath.StartsWith( "material:" ) )
		{
			// Show material icon
			_iconImage.Style.Display = DisplayMode.None;
			_materialIconLabel.Style.Display = DisplayMode.Flex;
			_materialIconLabel.Text = iconPath.Substring( 9 ); // Remove "material:" prefix
			_materialIconLabel.Style.FontSize = Length.Pixels( _iconSize );
		}
		else
		{
			// Show image icon
			_iconImage.Style.Display = DisplayMode.Flex;
			_materialIconLabel.Style.Display = DisplayMode.None;
			Log.Info( $"Loading icon from path: {iconPath}" );
			_iconImage.Style.BackgroundImage = Texture.Load( FileSystem.Mounted, iconPath );
			_iconImage.Style.Width = Length.Pixels( _iconSize );
			_iconImage.Style.Height = Length.Pixels( _iconSize );
		}
	}

	/// <summary>
	/// Set the icon by name
	/// </summary>
	public void SetIcon( string iconName, XGUIIconSystem.IconType iconType = XGUIIconSystem.IconType.UI, int iconSize = 16 )
	{
		_iconName = iconName;
		_iconType = iconType;
		_iconSize = iconSize;
		UpdateIcon();
	}

	/// <summary>
	/// Set the icon for a file extension
	/// </summary>
	public void SetFileIcon( string extension, int iconSize = 16 )
	{
		_iconType = XGUIIconSystem.IconType.FileType;
		_iconSize = iconSize;

		if ( string.IsNullOrEmpty( extension ) )
		{
			_iconName = "file";
		}
		else
		{
			// Normalize extension
			if ( extension.StartsWith( "." ) )
				extension = extension.Substring( 1 );

			_iconName = extension.ToLowerInvariant();
		}

		UpdateIcon();
	}

	/// <summary>
	/// Set the icon for a folder
	/// </summary>
	public void SetFolderIcon( string folderType = "folder", int iconSize = 16 )
	{
		_iconType = XGUIIconSystem.IconType.Folder;
		_iconSize = iconSize;
		_iconName = folderType;
		UpdateIcon();
	}

	/// <summary>
	/// Reset to default theme
	/// </summary>
	public void ResetTheme()
	{
		XGUIIconSystem.ClearCache();
		UpdateIcon();
	}
}

