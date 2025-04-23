using System.Collections.Generic;

namespace XGUI.XGUIEditor
{
	public enum NodeType { Element, Text }

	public class MarkupNode
	{
		public NodeType Type;
		public string TagName; // For elements
		public string TextContent; // For text nodes
		public Dictionary<string, string> Attributes = new();
		public List<MarkupNode> Children = new();

		public MarkupNode Parent;

		public override string ToString() => Type == NodeType.Element ? $"<{TagName}>" : TextContent;

		public void TryModifyStyle( string name, string value )
		{
			// If the style attribute doesn't exist and value is null or empty,
			// there's nothing to do
			if ( !Attributes.ContainsKey( "style" ) && (value == null || value == "") )
				return;

			// Initialize style string
			string style = Attributes.ContainsKey( "style" ) ? Attributes["style"] : "";

			// If value is null or empty, remove the property entirely
			if ( value == null || value == "" )
			{
				// Create a more robust pattern to catch all variations of the property
				// This handles: name:value; or name: value; with any amount of whitespace
				string pattern = $@"(^|\s){name}\s*:\s*[^;]*;\s*";
				style = System.Text.RegularExpressions.Regex.Replace( style, pattern, " " );

				// Clean up any double-spaces that might result from the replacement
				while ( style.Contains( "  " ) )
					style = style.Replace( "  ", " " );

				// Remove the style attribute if it's empty after removing properties
				style = style.Trim();
				if ( string.IsNullOrWhiteSpace( style ) )
				{
					Attributes.Remove( "style" );
					return;
				}
			}
			else
			{
				// Add or modify the style property
				bool propertyExists = false;

				// Check if the property exists using a more precise pattern
				string pattern = $@"(^|\s){name}\s*:";
				if ( System.Text.RegularExpressions.Regex.IsMatch( style, pattern ) )
				{
					// Replace the existing property with the new value
					propertyExists = true;
					pattern = $@"(^|\s){name}\s*:\s*[^;]*;?";
					style = System.Text.RegularExpressions.Regex.Replace( style, pattern, $"$1{name}: {value};" );
				}

				if ( !propertyExists )
				{
					// Add the new property
					if ( !string.IsNullOrWhiteSpace( style ) )
					{
						// Ensure the style ends with a semicolon before adding the new property
						if ( !style.EndsWith( ";" ) )
							style += ";";

						// Add a space for readability
						style += " ";
					}

					style += $"{name}: {value};";
				}
			}

			// Clean up the style string and ensure it's well-formatted
			style = CleanupStyleString( style );

			// Update or remove the style attribute
			if ( string.IsNullOrWhiteSpace( style ) )
				Attributes.Remove( "style" );
			else
				Attributes["style"] = style;
		}

		private string CleanupStyleString( string style )
		{
			// Handle null or empty styles
			if ( string.IsNullOrWhiteSpace( style ) )
				return "";

			// Remove extra spaces
			while ( style.Contains( "  " ) )
				style = style.Replace( "  ", " " );

			// Ensure proper spacing around semicolons
			style = style.Replace( " ;", ";" );

			// Keep a space after semicolons for readability
			style = style.Replace( ";", "; " );

			// Remove semicolon-space at the end if present
			if ( style.EndsWith( "; " ) )
				style = style.Substring( 0, style.Length - 2 );

			// Ensure proper spacing around colons
			style = style.Replace( " :", ":" );
			style = System.Text.RegularExpressions.Regex.Replace( style, ":([^\\s])", ": $1" );

			// Remove whitespace at the beginning and end
			style = style.Trim();

			// Handle the case where we might have accidentally added an extra semicolon at the end
			if ( style.EndsWith( ";" ) )
				style = style.Substring( 0, style.Length - 1 );

			return style;
		}
	}
}
