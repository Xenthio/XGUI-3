using System;
using System.Collections.Generic;
using System.Text;

namespace XGUI.XGUIEditor
{
	// TODO: store razor statements and code blocks
	public static class SimpleMarkupParser
	{
		// Entry point: parses markup string into a tree of MarkupNode
		public static List<MarkupNode> Parse( string input )
		{
			var nodes = new List<MarkupNode>();
			int pos = 0;
			while ( pos < input.Length )
			{
				SkipWhitespace( input, ref pos );
				if ( pos >= input.Length ) break;

				if ( input[pos] == '<' )
				{
					var node = ParseElement( input, ref pos );
					if ( node != null ) nodes.Add( node );
				}
				else
				{
					var text = ParseText( input, ref pos );
					if ( !string.IsNullOrWhiteSpace( text ) )
						nodes.Add( new MarkupNode { Type = NodeType.Text, TextContent = text } );
				}
			}
			return nodes;
		}

		private static MarkupNode ParseElement( string input, ref int pos )
		{
			// Assumes input[pos] == '<'
			int start = pos;
			pos++; // skip '<'
			SkipWhitespace( input, ref pos );

			// Read tag name
			var tagName = ReadWhile( input, ref pos, c => char.IsLetterOrDigit( c ) || c == '-' || c == '_' );
			if ( string.IsNullOrEmpty( tagName ) ) return null;

			var node = new MarkupNode { Type = NodeType.Element, TagName = tagName };

			// Read attributes
			while ( true )
			{
				SkipWhitespace( input, ref pos );
				if ( pos >= input.Length ) break;
				if ( input[pos] == '/' || input[pos] == '>' ) break;

				// Attribute name
				var attrName = ReadWhile( input, ref pos, c => char.IsLetterOrDigit( c ) || c == '-' || c == '_' );
				if ( string.IsNullOrEmpty( attrName ) ) break;
				SkipWhitespace( input, ref pos );

				string attrValue = null;
				if ( pos < input.Length && input[pos] == '=' )
				{
					pos++; // skip '='
					SkipWhitespace( input, ref pos );
					attrValue = ReadAttributeValue( input, ref pos );
				}
				node.Attributes[attrName] = attrValue ?? "";
			}

			// Self-closing tag
			if ( pos < input.Length - 1 && input[pos] == '/' && input[pos + 1] == '>' )
			{
				pos += 2;
				return node;
			}

			// End of open tag
			if ( pos < input.Length && input[pos] == '>' )
			{
				pos++;
				// Parse children until </tag>
				while ( true )
				{
					SkipWhitespace( input, ref pos );
					if ( pos >= input.Length ) break;
					if ( input[pos] == '<' && pos + 1 < input.Length && input[pos + 1] == '/' )
					{
						// End tag
						pos += 2;
						var endTag = ReadWhile( input, ref pos, c => char.IsLetterOrDigit( c ) || c == '-' || c == '_' );
						SkipWhitespace( input, ref pos );
						if ( pos < input.Length && input[pos] == '>' ) pos++;
						break;
					}
					else if ( input[pos] == '<' )
					{
						var child = ParseElement( input, ref pos );
						if ( child != null )
						{
							child.Parent = node;
							node.Children.Add( child );
						}
					}
					else
					{
						var text = ParseText( input, ref pos );
						if ( !string.IsNullOrWhiteSpace( text ) )
							node.Children.Add( new MarkupNode { Type = NodeType.Text, TextContent = text, Parent = node } );
					}
				}
			}
			return node;
		}

		private static string ParseText( string input, ref int pos )
		{
			int start = pos;
			while ( pos < input.Length && input[pos] != '<' )
				pos++;
			var str = input.Substring( start, pos - start );
			return str.Trim();
		}

		private static string ReadWhile( string input, ref int pos, Func<char, bool> predicate )
		{
			int start = pos;
			while ( pos < input.Length && predicate( input[pos] ) )
				pos++;
			return input.Substring( start, pos - start );
		}

		private static string ReadAttributeValue( string input, ref int pos )
		{
			if ( pos >= input.Length ) return "";
			if ( input[pos] == '"' || input[pos] == '\'' )
			{
				char quote = input[pos++];
				int start = pos;
				while ( pos < input.Length && input[pos] != quote )
					pos++;
				var val = input.Substring( start, pos - start );
				if ( pos < input.Length ) pos++; // skip closing quote
				return val;
			}
			else
			{
				// Unquoted value
				return ReadWhile( input, ref pos, c => !char.IsWhiteSpace( c ) && c != '>' && c != '/' );
			}
		}

		private static void SkipWhitespace( string input, ref int pos )
		{
			while ( pos < input.Length && char.IsWhiteSpace( input[pos] ) )
				pos++;
		}

		// Serialize tree back to markup with formatting
		public static string Serialize( IEnumerable<MarkupNode> nodes )
		{
			var sb = new StringBuilder();
			foreach ( var node in nodes )
				SerializeNode( node, sb, 0 );
			return sb.ToString();
		}

		private static void SerializeNode( MarkupNode node, StringBuilder sb, int indentLevel )
		{
			string indent = new string( '\t', indentLevel );

			if ( node.Type == NodeType.Text )
			{
				// For text nodes, trim excess whitespace but preserve content
				string trimmed = node.TextContent.Trim();
				if ( !string.IsNullOrEmpty( trimmed ) )
				{
					sb.Append( indent );
					sb.Append( trimmed );
					sb.AppendLine();
				}
			}
			else if ( node.Type == NodeType.Element )
			{
				// Start element tag with indentation
				sb.Append( indent );
				sb.Append( '<' ).Append( node.TagName );

				// Add attributes
				foreach ( var attr in node.Attributes )
				{
					sb.Append( ' ' ).Append( attr.Key );
					if ( !string.IsNullOrEmpty( attr.Value ) )
						sb.Append( "=\"" ).Append( attr.Value.Replace( "\"", "&quot;" ) ).Append( '"' );
				}

				if ( node.Children.Count == 0 )
				{
					// Self-closing tag
					sb.AppendLine( " />" );
				}
				else
				{
					sb.AppendLine( ">" );

					// Process children with increased indentation
					foreach ( var child in node.Children )
						SerializeNode( child, sb, indentLevel + 1 );

					// Closing tag with proper indentation
					sb.Append( indent );
					sb.Append( "</" ).Append( node.TagName ).AppendLine( ">" );
				}
			}
		}
		public static Dictionary<string, string> ParseAttributes( string input )
		{
			var dict = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			if ( string.IsNullOrWhiteSpace( input ) )
				return dict;

			// Regex: key="value", key='value', key=value, or key (valueless)
			var regex = new System.Text.RegularExpressions.Regex(
				@"(\w+)(?:\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""']+)))?",
				System.Text.RegularExpressions.RegexOptions.Compiled );

			foreach ( System.Text.RegularExpressions.Match match in regex.Matches( input ) )
			{
				var key = match.Groups[1].Value;
				var value = match.Groups[2].Success ? match.Groups[2].Value
					: match.Groups[3].Success ? match.Groups[3].Value
					: match.Groups[4].Success ? match.Groups[4].Value
					: ""; // For valueless attributes like "checked"
				dict[key] = value;
			}
			return dict;
		}
	}
}
