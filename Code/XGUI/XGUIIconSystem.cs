using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XGUI;

/// <summary>
/// Look up icons respective to the current theme, you can look up icons by name to use in buttons and panels, or icons for file types to use in a file browser.
/// </summary>
public static class XGUIIconSystem
{
	private const string DefaultThemeName = "Computer95";
	private static string _currentThemeName = DefaultThemeName;

	// Cache for icon paths - theme/type/name/size/variant -> path
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
		Folder,

		/// <summary>
		/// Miscellaneous icons (other types)
		/// </summary>
		Misc
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
			IconType.Misc => "Misc",
			_ => string.Empty
		};
	}

	/// <summary>
	/// Look up an icon by name and size
	/// </summary>
	/// <param name="iconName">The name of the icon</param>
	/// <param name="iconType">The type of icon</param>
	/// <param name="size">The desired size of the icon (16, 24, 32, 48, etc.)</param>
	/// <param name="variant">Optional variant of the icon (e.g., "hover", "active", "disabled")</param>
	/// <returns>The path to the icon file, or null if not found</returns>
	public static string GetIcon( string iconName, IconType iconType = IconType.UI, int size = 16, string variant = null )
	{
		// Standardize icon name
		if ( string.IsNullOrEmpty( iconName ) )
			return null;

		iconName = iconName.ToLowerInvariant();

		// Standardize variant if provided
		if ( !string.IsNullOrEmpty( variant ) )
		{
			variant = variant.ToLowerInvariant();
		}

		// Check cache first
		string cacheKey = $"{_currentThemeName}/{iconType}/{iconName}/{size}/{variant}";
		if ( IconPathCache.TryGetValue( cacheKey, out string cachedPath ) )
			return cachedPath;

		// First, try with variant if specified
		string iconPath = null;
		if ( !string.IsNullOrEmpty( variant ) )
		{
			iconPath = FindIconInTheme( _currentThemeName, iconName, iconType, size, variant );

			// If not found in current theme, try default theme with variant
			if ( string.IsNullOrEmpty( iconPath ) && _currentThemeName != DefaultThemeName )
			{
				iconPath = FindIconInTheme( DefaultThemeName, iconName, iconType, size, variant );
			}

			// If still not found with variant, fall back to standard icon (without variant)
			if ( string.IsNullOrEmpty( iconPath ) )
			{
				// Try again without the variant
				return GetIcon( iconName, iconType, size );
			}
		}
		else
		{
			// Standard lookup without variant
			iconPath = FindIconInTheme( _currentThemeName, iconName, iconType, size );

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
		}

		// Cache the result for faster lookups
		if ( !string.IsNullOrEmpty( iconPath ) )
		{
			IconPathCache[cacheKey] = iconPath;
		}

		return iconPath;
	}

	private static string GetDefaultIconName( IconType iconType )
	{
		return iconType switch
		{
			IconType.FileType => "file",
			IconType.Folder => "folder",
			IconType.UI => "default",
			IconType.Misc => "default",
			_ => "default"
		};
	}

	/// <summary>
	/// Get an icon for a specific file extension with optional variant
	/// </summary>
	/// <param name="extension">The file extension (with or without the dot)</param>
	/// <param name="size">The desired icon size</param>
	/// <param name="variant">Optional variant of the icon (e.g., "hover", "active", "disabled")</param>
	/// <returns>The path to the icon file</returns>
	public static string GetFileIcon( string extension, int size = 16, string variant = null )
	{
		if ( string.IsNullOrEmpty( extension ) )
			return GetIcon( "file", IconType.FileType, size, variant );

		// Normalize extension
		if ( extension.StartsWith( "." ) )
			extension = extension.Substring( 1 );

		extension = extension.ToLowerInvariant();

		// Try to find an icon for this specific extension
		return GetIcon( extension, IconType.FileType, size, variant );
	}

	/// <summary>
	/// Get a folder icon with optional variant
	/// </summary>
	/// <param name="folderType">The type of folder (optional)</param>
	/// <param name="size">The desired icon size</param>
	/// <param name="variant">Optional variant of the icon (e.g., "hover", "active", "disabled")</param>
	/// <returns>The path to the icon file</returns>
	public static string GetFolderIcon( string folderType = "folder", int size = 16, string variant = null )
	{
		return GetIcon( folderType, IconType.Folder, size, variant );
	}


	static bool findingDefaultIconPreventRecursion = false;
	/// <summary>
	/// Find an icon in a specific theme
	/// </summary>
	private static string FindIconInTheme( string themeName, string iconName, IconType iconType, int size, string variant = null )
	{
		string baseDir = GetThemeIconBaseDirectory( themeName );
		string typeDir = GetIconTypeDirectory( iconType );

		// Build file name pattern based on whether variant is specified
		string fileNamePattern = !string.IsNullOrEmpty( variant ) ?
			$"{iconName}_{size}_{variant}.png" : $"{iconName}_{size}.png";

		// Try exact size and variant/non-variant first
		string exactPath = $"{baseDir}/{typeDir}/{fileNamePattern}";
		if ( FileSystem.Mounted.FileExists( exactPath ) )
			return exactPath;

		// If not found, try to find the closest size with the same variant (if specified)
		List<(int Size, string Variant, string Path)> availableIcons = new List<(int, string, string)>();

		// Try to find all available sizes for this icon
		foreach ( var file in FileSystem.Mounted.FindFile( $"{baseDir}/{typeDir}" ) )
		{
			string fileName = Path.GetFileNameWithoutExtension( file );

			// Skip files that don't start with our icon name
			if ( !fileName.StartsWith( iconName + "_" ) )
				continue;

			// Parse out size and variant from filename (format: name_size.png or name_size_variant.png)
			string[] parts = fileName.Substring( iconName.Length + 1 ).Split( '_' );

			if ( parts.Length >= 1 && int.TryParse( parts[0], out int fileSize ) )
			{
				string fileVariant = parts.Length > 1 ? parts[1] : null;
				availableIcons.Add( (fileSize, fileVariant, $"{baseDir}/{typeDir}/{fileName}.png") );
			}
		}

		// If no sizes found, return null
		if ( availableIcons.Count == 0 )
		{
			Log.Warning( $"Icon not found: {iconName} in theme {themeName} of type {iconType}" );
			Log.Info( $"Add to path: {baseDir}/{typeDir}/{fileNamePattern}" );

			// Return a generic file icon if this is a file type
			if ( iconType == IconType.FileType && !findingDefaultIconPreventRecursion )
			{
				findingDefaultIconPreventRecursion = true;
				return null;
			}
			findingDefaultIconPreventRecursion = false;

			return null;
		}

		// If variant is specified, try to find an exact match for the variant first
		if ( !string.IsNullOrEmpty( variant ) )
		{
			// Filter for the requested variant
			var variantMatches = availableIcons.Where( i => i.Variant == variant ).ToList();

			if ( variantMatches.Count > 0 )
			{
				// Sort by size
				variantMatches.Sort( ( a, b ) => a.Size.CompareTo( b.Size ) );

				// Find closest size (prefer larger)
				var closest = FindClosestSize( variantMatches, size );
				return closest.Path;
			}
		}

		// Otherwise, filter for icons with no variant
		var standardIcons = availableIcons.Where( i => i.Variant == null ).ToList();
		if ( standardIcons.Count > 0 )
		{
			// Sort by size
			standardIcons.Sort( ( a, b ) => a.Size.CompareTo( b.Size ) );

			// Find closest size (prefer larger)
			var closest = FindClosestSize( standardIcons, size );
			return closest.Path;
		}

		// If we get here, we have icons but none with the requested variant or no variant
		// Just return the first one as a last resort
		return availableIcons[0].Path;
	}

	/// <summary>
	/// Find the icon with the closest size to the requested size, preferring larger sizes
	/// </summary>
	private static (int Size, string Variant, string Path) FindClosestSize( List<(int Size, string Variant, string Path)> icons, int requestedSize )
	{
		// Find the first icon that's at least as large as the requested size
		foreach ( var icon in icons )
		{
			if ( icon.Size >= requestedSize )
			{
				return icon;
			}
		}

		// If no icons are large enough, return the largest available
		return icons[icons.Count - 1];
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
	private string _variant;
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

	/// <summary>
	/// The variant of the icon (e.g., "hover", "active", "disabled")
	/// </summary>
	public string Variant
	{
		get => _variant;
		set
		{
			if ( _variant != value )
			{
				_variant = value;
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

		// Set up event handlers for hover state
		//AddEventListener( "onmouseover", OnMouseEnter );
		//AddEventListener( "onmouseout", OnMouseLeave );
		//AddEventListener( "onmousedown", OnMouseDown );
		//AddEventListener( "onmouseup", OnMouseUp );
	}

	public XGUIIconPanel( string iconName, XGUIIconSystem.IconType iconType = XGUIIconSystem.IconType.UI, int iconSize = 16, string variant = null )
		: this()
	{
		_iconName = iconName;
		_iconType = iconType;
		_iconSize = iconSize;
		_variant = variant;
		UpdateIcon();
	}

	private void OnMouseEnter( PanelEvent e )
	{
		// If auto-hover variants are desired, set variant to "hover"
		if ( string.IsNullOrEmpty( _variant ) )
		{
			Variant = "hover";
		}
	}

	private void OnMouseLeave( PanelEvent e )
	{
		// Reset variant when mouse leaves
		if ( _variant == "hover" || _variant == "active" )
		{
			Variant = null;
		}
	}

	private void OnMouseDown( PanelEvent e )
	{
		// Set to active when mouse is pressed
		if ( string.IsNullOrEmpty( _variant ) || _variant == "hover" )
		{
			Variant = "active";
		}
	}

	private void OnMouseUp( PanelEvent e )
	{
		// Return to hover state when mouse is released
		if ( _variant == "active" )
		{
			Variant = HasHovered ? "hover" : null;
		}
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

		if ( _iconName.StartsWith( "url:data:" ) )
		{
			var imagePath = IconName.Substring( 9 ); // Remove "url:data:" prefix
			_iconImage.Style.Display = DisplayMode.Flex;
			_materialIconLabel.Style.Display = DisplayMode.None;

			var tex = Texture.LoadFromFileSystem( imagePath, FileSystem.Data );

			_iconImage.Style.SetBackgroundImage( tex );
			_iconImage.Style.Width = Length.Pixels( _iconSize );
			_iconImage.Style.Height = Length.Pixels( _iconSize );
			return;
		}
		if ( _iconName.StartsWith( "url:vtex:" ) )
		{
			var imagePath = IconName.Substring( 9 ); // Remove "url:vtex:" prefix
			_iconImage.Style.Display = DisplayMode.Flex;
			_materialIconLabel.Style.Display = DisplayMode.None;
			var tex = Texture.Find( imagePath );
			_iconImage.Style.SetBackgroundImage( tex );
			_iconImage.Style.Width = Length.Pixels( _iconSize );
			_iconImage.Style.Height = Length.Pixels( _iconSize );
			return;
		}
		if ( _iconName.StartsWith( "url:" ) )
		{
			var imagePath = IconName.Substring( 4 ); // Remove "url:" prefix
			_iconImage.Style.Display = DisplayMode.Flex;
			_materialIconLabel.Style.Display = DisplayMode.None;
			_iconImage.Style.SetBackgroundImage( imagePath );
			_iconImage.Style.Width = Length.Pixels( _iconSize );
			_iconImage.Style.Height = Length.Pixels( _iconSize );
			return;
		}

		string iconPath = XGUIIconSystem.GetIcon( _iconName, _iconType, _iconSize, _variant );

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
			_iconImage.Style.BackgroundImage = Texture.LoadFromFileSystem( iconPath, FileSystem.Mounted );
			_iconImage.Style.Width = Length.Pixels( _iconSize );
			_iconImage.Style.Height = Length.Pixels( _iconSize );
		}
	}

	/// <summary>
	/// Set the icon by name
	/// </summary>
	public void SetIcon( string iconName, XGUIIconSystem.IconType iconType = XGUIIconSystem.IconType.UI, int iconSize = 16, string variant = null )
	{
		_iconName = iconName;
		_iconType = iconType;
		_iconSize = iconSize;
		_variant = variant;
		UpdateIcon();
	}

	/// <summary>
	/// Set the icon for a file extension
	/// </summary>
	public void SetFileIcon( string extension, int iconSize = 16, string variant = null )
	{
		_iconType = XGUIIconSystem.IconType.FileType;
		_iconSize = iconSize;
		_variant = variant;

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
	public void SetFolderIcon( string folderType = "folder", int iconSize = 16, string variant = null )
	{
		_iconType = XGUIIconSystem.IconType.Folder;
		_iconSize = iconSize;
		_iconName = folderType;
		_variant = variant;
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

