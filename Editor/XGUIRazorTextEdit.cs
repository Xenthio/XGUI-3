using Editor;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace XGUI.XGUIEditor;

public class XGUIRazorTextEdit : Editor.TextEdit
{
	// Regex patterns for Razor syntax highlighting
	private static readonly Regex CSharpKeywordsRegex = new( @"\b(abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while)\b", RegexOptions.Compiled );
	private static readonly Regex RazorDirectivesRegex = new( @"(@page|@model|@using|@implements|@inherits|@inject|@layout|@namespace|@addTagHelper|@removeTagHelper|@tagHelperPrefix|@attribute|@code|@functions)", RegexOptions.Compiled );
	// Modified to specifically include root tags and other custom tags
	// 1) Change your HtmlTagsRegex to capture the tag name in a named group:
	private static readonly Regex HtmlTagsRegex = new(
		@"</?(?<name>(?:root|div|button|label|check|textentry|sliderscale|groupbox|[A-Za-z][A-Za-z0-9\-_:]*))(?:\s+[^>]*)?/?>",
		RegexOptions.Compiled
	);
	private static readonly Regex HtmlAttributesRegex = new(
	@"\b(?<name>[A-Za-z_:][A-Za-z0-9_:\-\.]*)\b(?=\s*=)",
	RegexOptions.Compiled
);

	private static readonly Regex CommentsRegex = new(
		@"(<!--.*?-->)|(/\*.*?\*/)|(@\*.*?\*@)|(//.*?$)|(///.*?$)",
		RegexOptions.Compiled | RegexOptions.Multiline
	);
	// Modified string literals regex to avoid matching inside HTML tags but correctly match attribute values
	private static readonly Regex StringLiteralsRegex = new(
		@"(=""([^""\\]|\\.)*"")|('([^'\\]|\\.)*')|(?<![=<>])\s*""([^""\\]|\\.)*""",
		RegexOptions.Compiled
	);
	private static readonly Regex RazorCodeBlockRegex = new( @"@{.*?}", RegexOptions.Compiled | RegexOptions.Singleline );
	private static readonly Regex RazorExpressionRegex = new( @"@(?!\s)(?:[a-zA-Z0-9_\.]+|\(.*?\))", RegexOptions.Compiled );

	// Color definitions for syntax elements as CSS hex values
	private const string CommentColor = "#669966";
	private const string KeywordColor = "#457ACC";
	private const string StringColor = "#CC6633";
	private const string TagColor = "#CC3333";
	private const string AttributeColor = "#999911";
	private const string RazorDirectiveColor = "#9933CC";
	private const string RazorExpressionColor = "#9933CC";

	// Track when we're updating to prevent recursive calls
	private bool _updating = false;
	private string _lastText = "";
	private TextCursor _lastCursor;

	public XGUIRazorTextEdit( Widget parent = null ) : base( parent )
	{
		// Use a CSS style that preserves tabs properly
		SetStyles( "font-family: Consolas, monospace; font-size: 12px; white-space: pre; tab-size: 4;" );
		TabSize = 24;
		TextChanged += OnTextChanged;
	}

	protected override void OnTextChanged( string value )
	{
		base.OnTextChanged( value );

		// Don't refresh highlighting if we're currently updating
		if ( _updating ) return;

		// Apply syntax highlighting
		ApplySyntaxHighlighting();
	}

	public void ApplySyntaxHighlighting()
	{
		if ( _updating ) return;

		string text = PlainText;
		if ( string.IsNullOrEmpty( text ) || text == _lastText ) return;

		try
		{
			_updating = true;

			// Save cursor position
			_lastCursor = GetTextCursor();

			// Generate highlighted HTML with proper tab handling
			string highlightedHtml = GenerateHighlightedHtml( text );

			// Update the content
			Clear();
			AppendHtml( highlightedHtml );

			Log.Info( $"Updated text: {text}" );

			// Restore cursor if possible
			if ( _lastCursor != null )
			{
				SetTextCursor( _lastCursor );
			}

			// Update last text to avoid unnecessary updates
			_lastText = text;
		}
		finally
		{
			_updating = false;
		}
	}

	private string GenerateHighlightedHtml( string text )
	{
		// We'll create segments with their styles
		var segments = new List<(int Start, int End, string Style)>();

		// Order matters for segment priority!
		CollectCommentSegments( text, segments );      // Comments first
		CollectStringSegments( text, segments );       // Then string literals
		CollectHtmlTagSegments( text, segments );      // Then HTML tags (including attributes)
		CollectRazorDirectiveSegments( text, segments );
		CollectCodeBlockSegments( text, segments );
		CollectKeywordSegments( text, segments );

		// Sort segments by start position to handle overlaps correctly
		segments.Sort( ( a, b ) => a.Start.CompareTo( b.Start ) );

		// Merge with priority to avoid string literals overriding attributes
		segments = MergeOverlappingSegmentsWithPriority( segments );

		// Generate and return the final HTML
		return CreateHtmlWithHighlighting( text, segments );
	}

	private string CreateHtmlWithHighlighting( string text, List<(int Start, int End, string Style)> segments )
	{
		var html = new StringBuilder( "<pre style=\"white-space: pre; tab-size: 4; margin: 0; padding: 0;\">" );
		int currentPos = 0;

		foreach ( var segment in segments )
		{
			// Add any unstyled text before this segment
			if ( segment.Start > currentPos )
			{
				html.Append( EncodeHtml( text.Substring( currentPos, segment.Start - currentPos ) ) );
			}

			// Add the styled segment
			string segmentText = text.Substring( segment.Start, segment.End - segment.Start );
			html.Append( $"<span style=\"{segment.Style}\">{EncodeHtml( segmentText )}</span>" );

			// Update current position
			currentPos = segment.End;
		}

		// Add any remaining text
		if ( currentPos < text.Length )
		{
			html.Append( EncodeHtml( text.Substring( currentPos ) ) );
		}

		html.Append( "</pre>" );
		return html.ToString();
	}

	private void CollectCommentSegments( string text, List<(int, int, string)> segments )
	{
		foreach ( Match match in CommentsRegex.Matches( text ) )
		{
			segments.Add( (match.Index, match.Index + match.Length, $"color: {CommentColor};") );
		}
	}

	private void CollectStringSegments( string text, List<(int, int, string)> segments )
	{
		foreach ( Match match in StringLiteralsRegex.Matches( text ) )
		{
			segments.Add( (match.Index, match.Index + match.Length, $"color: {StringColor};") );
		}
	}

	private void CollectRazorDirectiveSegments( string text, List<(int, int, string)> segments )
	{
		foreach ( Match match in RazorDirectivesRegex.Matches( text ) )
		{
			segments.Add( (match.Index, match.Index + match.Length, $"color: {RazorDirectiveColor};") );
		}

		foreach ( Match match in RazorExpressionRegex.Matches( text ) )
		{
			segments.Add( (match.Index, match.Index + match.Length, $"color: {RazorExpressionColor};") );
		}
	}

	private void CollectHtmlTagSegments( string text, List<(int, int, string)> segments )
	{
		foreach ( Match tagMatch in HtmlTagsRegex.Matches( text ) )
		{
			// highlight just the tag name
			var nameGroup = tagMatch.Groups["name"];
			segments.Add( (nameGroup.Index, nameGroup.Index + nameGroup.Length, $"color: {TagColor};") );

			// Debug output for the tag
			Log.Info( $"TAG: '{tagMatch.Value}', Name: '{nameGroup.Value}', Index: {nameGroup.Index}" );

			// highlight only the attribute names
			foreach ( Match attrMatch in HtmlAttributesRegex.Matches( tagMatch.Value ) )
			{
				var attrNameGroup = attrMatch.Groups["name"];
				int attrStart = tagMatch.Index + attrMatch.Index;
				int attrEnd = attrStart + attrNameGroup.Length;

				// Debug output for each attribute match
				Log.Info( $"  ATTR: '{attrMatch.Value}', Name: '{attrNameGroup.Value}', " +
						 $"Match Index: {attrMatch.Index}, Name Index: {attrNameGroup.Index}, " +
						 $"Final Range: {attrStart}-{attrEnd}, Text: '{text.Substring( attrStart, attrEnd - attrStart )}'" );

				segments.Add( (attrStart, attrEnd, $"color: {AttributeColor};") );
			}
		}
	}

	private void CollectCodeBlockSegments( string text, List<(int, int, string)> segments )
	{
		foreach ( Match match in RazorCodeBlockRegex.Matches( text ) )
		{
			// Highlight the entire block
			segments.Add( (match.Index, match.Index + match.Length, $"color: {RazorDirectiveColor};") );

			// Highlight C# keywords within the code block
			if ( match.Length > 3 ) // Make sure there's content between @{ and }
			{
				string codeBlockContent = match.Value.Substring( 2, match.Length - 3 );
				foreach ( Match keywordMatch in CSharpKeywordsRegex.Matches( codeBlockContent ) )
				{
					int keywordStart = match.Index + 2 + keywordMatch.Index;
					int keywordEnd = keywordStart + keywordMatch.Length;
					segments.Add( (keywordStart, keywordEnd, $"color: {KeywordColor};") );
				}
			}
		}
	}

	private void CollectKeywordSegments( string text, List<(int, int, string)> segments )
	{
		foreach ( Match match in CSharpKeywordsRegex.Matches( text ) )
		{
			segments.Add( (match.Index, match.Index + match.Length, $"color: {KeywordColor};") );
		}
	}
	private List<(int Start, int End, string Style)> MergeOverlappingSegmentsWithPriority( List<(int Start, int End, string Style)> segments )
	{
		if ( segments.Count <= 1 ) return segments;

		var result = new List<(int Start, int End, string Style)>();
		var current = segments[0];

		for ( int i = 1; i < segments.Count; i++ )
		{
			var next = segments[i];

			// If segments overlap
			if ( next.Start < current.End )
			{
				// Always prioritize attribute color over string color
				if ( current.Style.Contains( AttributeColor ) && next.Style.Contains( StringColor ) )
				{
					// Keep attribute color and extend the range if needed
					current = (current.Start, Math.Max( current.End, next.End ), current.Style);
				}
				// Always prioritize tag color over string color
				else if ( current.Style.Contains( TagColor ) && next.Style.Contains( StringColor ) )
				{
					// Keep tag color and extend the range if needed
					current = (current.Start, Math.Max( current.End, next.End ), current.Style);
				}
				else
				{
					// For other overlaps, take the later style
					current = (current.Start, Math.Max( current.End, next.End ), next.Style);
				}
			}
			else
			{
				result.Add( current );
				current = next;
			}
		}

		result.Add( current );
		return result;
	}
	private List<(int Start, int End, string Style)> MergeOverlappingSegments( List<(int Start, int End, string Style)> segments )
	{
		if ( segments.Count <= 1 ) return segments;

		var result = new List<(int Start, int End, string Style)>();
		var current = segments[0];

		for ( int i = 1; i < segments.Count; i++ )
		{
			var next = segments[i];

			// If segments overlap
			if ( next.Start <= current.End )
			{
				// Take the style from the later segment (higher priority)
				current = (current.Start, Math.Max( current.End, next.End ), next.Style);
			}
			else
			{
				result.Add( current );
				current = next;
			}
		}

		result.Add( current );
		return result;
	}

	private string EncodeHtml( string text )
	{
		if ( string.IsNullOrEmpty( text ) ) return "";

		// Standard HTML encoding while preserving whitespace characters
		return text
			.Replace( "&", "&amp;" )
			.Replace( "<", "&lt;" )
			.Replace( ">", "&gt;" )
			.Replace( "\"", "&quot;" )
			.Replace( "'", "&#39;" )
			.Replace( " ", "&nbsp;" )
			.Replace( "\t", "&#9;" ) // HTML tab character
			.Replace( "\n", "<br>" ); // Line breaks
	}
}
