using System;
using System.Collections.Generic;
using System.Linq; // For Linq methods like Any
using System.Text.RegularExpressions;

namespace XGUI.XGUIEditor
{
	public static class MarkupParser
	{
		// More robust Regex:
		// Group 1: Opening tag name (e.g., div)
		// Group 2: Attributes string (e.g., class="xyz" style="color:red;")
		// Group 3: Set if self-closing (e.g., /)
		// Group 4: Inner content if not self-closing
		// Group 5: Closing tag name (should match Group 1)
		// This needs careful balancing for nested tags. Regex is often not the best tool for full HTML/XML parsing.
		// Let's try a less greedy Regex combined with iterative processing.

		// Regex to find the *next* opening tag, self-closing tag, or closing tag.
		private static readonly Regex _htmlTagFinderRegex = new Regex(
			@"<(/?)(\w[\w:]*)([^>]*?)(\/?)>",
			RegexOptions.Compiled | RegexOptions.IgnoreCase );

		// Regex to find attributes within a tag's attribute string part
		private static readonly Regex _attributeRegex = new Regex(
			@"(?<name>[\w-]+)(?:\s*=\s*(?:""(?<value_double>[^""]*)""|'(?<value_single>[^']*)'|(?<value_unquoted>[^\s>""]+)))?",
			RegexOptions.Compiled | RegexOptions.IgnoreCase );


		public static List<MarkupNode> Parse( string htmlContent )
		{
			List<MarkupNode> rootNodes = new List<MarkupNode>();
			if ( string.IsNullOrWhiteSpace( htmlContent ) )
			{
				return rootNodes;
			}

			// We'll use a stack to keep track of the current parent node as we parse nesting
			Stack<MarkupNode> nodeStack = new Stack<MarkupNode>();
			int currentPosition = 0;

			// Find all tag matches in the content
			var matches = _htmlTagFinderRegex.Matches( htmlContent );

			foreach ( Match match in matches )
			{
				int tagStart = match.Index;
				int tagEnd = tagStart + match.Length;

				// 1. Handle any text content *before* this tag
				if ( tagStart > currentPosition )
				{
					string text = htmlContent.Substring( currentPosition, tagStart - currentPosition );
					if ( !string.IsNullOrWhiteSpace( text ) )
					{
						var textNode = new MarkupNode( NodeType.Text )
						{
							TextContent = DecodeHtml( text ), // Store decoded text
							SourceStart = currentPosition,
							SourceEnd = tagStart,
						};

						// Add text node to the current parent (if any) or to root nodes
						if ( nodeStack.Any() )
						{
							nodeStack.Peek().Children.Add( textNode );
							textNode.Parent = nodeStack.Peek();
						}
						else
						{
							rootNodes.Add( textNode );
						}
					}
				}

				// 2. Process the tag itself
				bool isClosingTag = !string.IsNullOrEmpty( match.Groups[1].Value ); // Has '/' at start?
				string tagName = match.Groups[2].Value;
				string attributesString = match.Groups[3].Value;
				bool isSelfClosing = !string.IsNullOrEmpty( match.Groups[4].Value ); // Has '/' at end?

				if ( !isClosingTag )
				{
					Log.Info( $"Parser Creating Node: {tagName} | Start: {tagStart} | End: {tagEnd} | CurrentParserIndex: {currentPosition}" );
					// --- Opening or Self-Closing Tag ---	
					var elementNode = new MarkupNode( NodeType.Element )
					{
						TagName = tagName,
						SourceStart = tagStart,
						SourceEnd = tagEnd, // Initial end, might extend if not self-closing
						IsSelfClosing = isSelfClosing
					};

					// Parse attributes
					ParseAttributes( attributesString, elementNode.Attributes );

					// Add to parent or root
					if ( nodeStack.Any() )
					{
						nodeStack.Peek().Children.Add( elementNode );
						elementNode.Parent = nodeStack.Peek();
					}
					else
					{
						rootNodes.Add( elementNode );
					}

					// If it's not self-closing, push onto the stack to become the new parent
					if ( !isSelfClosing )
					{
						nodeStack.Push( elementNode );
					}
				}
				else
				{
					// --- Closing Tag --- </tag>
					if ( nodeStack.Any() && nodeStack.Peek().TagName.Equals( tagName, System.StringComparison.OrdinalIgnoreCase ) )
					{
						// Matching closing tag found, pop the stack
						MarkupNode closedNode = nodeStack.Pop();
						// Update the end position of the element node to include the closing tag
						closedNode.SourceEnd = tagEnd;
					}
					else
					{
						// Mismatched closing tag or closing tag with no opening tag!
						Log.Warning( $"Markup Parse Warning: Found closing tag '</{tagName}>' without matching opening tag at position {tagStart}." );
						// We could try to recover or just ignore it. Ignoring for now.
					}
				}

				// Update current position in the source string
				currentPosition = tagEnd;
			}

			// 3. Handle any remaining text content *after* the last tag
			if ( currentPosition < htmlContent.Length )
			{
				string trailingText = htmlContent.Substring( currentPosition );
				if ( !string.IsNullOrWhiteSpace( trailingText ) )
				{
					var textNode = new MarkupNode( NodeType.Text )
					{
						TextContent = DecodeHtml( trailingText ),
						SourceStart = currentPosition,
						SourceEnd = htmlContent.Length,
					};

					if ( nodeStack.Any() ) // Should technically not happen if markup is well-formed
					{
						nodeStack.Peek().Children.Add( textNode );
						textNode.Parent = nodeStack.Peek();
					}
					else
					{
						rootNodes.Add( textNode );
					}
				}
			}

			// Check for unclosed tags
			if ( nodeStack.Any() )
			{
				foreach ( var unclosedNode in nodeStack ) // Iterate from top (most nested) down
				{
					Log.Warning( $"Markup Parse Warning: Tag '<{unclosedNode.TagName}>' starting at {unclosedNode.SourceStart} was never closed." );
					// Optionally, we could try to "auto-close" them at the end of the content,
					// but this can be ambiguous. For now, just warn.
					// Set end position to end of content for recovery?
					unclosedNode.SourceEnd = htmlContent.Length;
				}
			}


			return rootNodes;
		}

		private static void ParseAttributes( string attributesString, Dictionary<string, string> attributes )
		{
			if ( string.IsNullOrWhiteSpace( attributesString ) ) return;

			var attrMatches = _attributeRegex.Matches( attributesString );
			foreach ( Match match in attrMatches )
			{
				string name = match.Groups["name"].Value;
				string value = match.Groups["value_double"].Success ? match.Groups["value_double"].Value :
							   match.Groups["value_single"].Success ? match.Groups["value_single"].Value :
							   match.Groups["value_unquoted"].Success ? match.Groups["value_unquoted"].Value :
							   null; // Attribute might be valueless (e.g., "checked")

				// Use null for valueless, empty string for value="", non-null otherwise
				if ( value == null && match.Groups[0].Value.Contains( '=' ) ) // It had an equals sign but no parseable value (e.g. attr= or attr="")
				{
					value = string.Empty;
				}

				// Decode the value before storing
				string decodedValue = (value != null) ? DecodeHtml( value ) : null;

				// Add or update the attribute. Using OrdinalIgnoreCase in dictionary handles duplicates.
				attributes[name] = decodedValue;
			}
		}

		// Simple HTML Decoder (reuse from previous attempt or use library if available)
		private static string DecodeHtml( string text )
		{
			if ( string.IsNullOrEmpty( text ) ) return "";
			// Basic decoding, needs System.Web if available or manual implementation
			// return System.Web.HttpUtility.HtmlDecode(text); // Requires System.Web
			// Manual basic version:
			return text.Replace( "&lt;", "<" )
					   .Replace( "&gt;", ">" )
					   .Replace( "&quot;", "\"" )
					   .Replace( "&#39;", "'" )
					   .Replace( "&amp;", "&" ); // Ampersand last
		}

		// Add this method to handle more error cases during extraction
		public static List<MarkupNode> ParseWithDiagnostics( string htmlContent, out List<string> warnings )
		{
			warnings = new List<string>();

			if ( string.IsNullOrWhiteSpace( htmlContent ) )
			{
				warnings.Add( "Empty content provided for parsing" );
				return new List<MarkupNode>();
			}

			try
			{
				var nodes = Parse( htmlContent );

				// Validate nodes after parsing
				foreach ( var node in nodes.Where( n => n.Type == NodeType.Element ) )
				{
					// Check for suspicious source positions
					if ( node.SourceEnd <= node.SourceStart )
					{
						warnings.Add( $"Node <{node.TagName}> has invalid source positions: Start={node.SourceStart}, End={node.SourceEnd}" );
					}

					// Check if source extraction works
					try
					{
						string nodeSource = htmlContent.Substring( node.SourceStart, node.SourceLength );
						if ( !nodeSource.StartsWith( "<" ) || !nodeSource.EndsWith( ">" ) )
						{
							warnings.Add( $"Node <{node.TagName}> source doesn't begin with '<' or end with '>': {nodeSource}" );
						}
					}
					catch ( Exception ex )
					{
						warnings.Add( $"Cannot extract source for node <{node.TagName}>: {ex.Message}" );
					}
				}

				return nodes;
			}
			catch ( Exception ex )
			{
				warnings.Add( $"Critical parsing error: {ex.Message}" );
				return new List<MarkupNode>();
			}
		}
	}
}
