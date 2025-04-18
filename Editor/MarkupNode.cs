using System.Collections.Generic;
using System.Text;

namespace XGUI.XGUIEditor
{
	/// <summary>
	/// Represents a node (element or text) parsed from the HTML-like markup.
	/// </summary>
	public class MarkupNode
	{
		/// <summary>
		/// The type of node (Element or Text).
		/// </summary>
		public NodeType Type { get; set; }

		/// <summary>
		/// The tag name if Type is Element (e.g., "div", "button"). Null for Text nodes.
		/// </summary>
		public string TagName { get; set; }

		/// <summary>
		/// The attributes if Type is Element. Null for Text nodes. Key=Name, Value=Value.
		/// </summary>
		public Dictionary<string, string> Attributes { get; set; }

		/// <summary>
		/// The direct text content of this node (if Type is Text) or the text between child elements.
		/// Might be null or empty for elements that only contain other elements.
		/// </summary>
		public string TextContent { get; set; }

		/// <summary>
		/// The parent node in the tree. Null for the root node(s).
		/// </summary>
		public MarkupNode Parent { get; set; }

		/// <summary>
		/// The child nodes of this element. Empty for Text nodes or self-closing elements.
		/// </summary>
		public List<MarkupNode> Children { get; set; } = new List<MarkupNode>();

		/// <summary>
		/// The starting position of this node's representation in the original source string.
		/// For elements, this is the start of the opening tag '<'.
		/// For text, this is the start of the text content.
		/// </summary>
		public int SourceStart { get; set; }

		/// <summary>
		/// The ending position (exclusive) of this node's representation in the original source string.
		/// For elements, this is *after* the closing tag '>'.
		/// For text, this is *after* the last character of the text.
		/// </summary>
		public int SourceEnd { get; set; }

		/// <summary>
		/// The length of this node's representation in the original source string.
		/// </summary>
		public int SourceLength => SourceEnd - SourceStart;

		/// <summary>
		/// Indicates if the original source tag was self-closing (e.g., <textentry />).
		/// Only relevant for Element nodes.
		/// </summary>
		public bool IsSelfClosing { get; set; }


		public MarkupNode( NodeType type )
		{
			Type = type;
			if ( type == NodeType.Element )
			{
				Attributes = new Dictionary<string, string>( System.StringComparer.OrdinalIgnoreCase ); // Case-insensitive attribute names
			}
		}

		// Optional: Helper methods for debugging or display
		public override string ToString()
		{
			if ( Type == NodeType.Element )
			{
				StringBuilder sb = new StringBuilder();
				sb.Append( $"<{TagName}" );
				if ( Attributes != null )
				{
					foreach ( var attr in Attributes )
					{
						sb.Append( $" {attr.Key}=\"{attr.Value}\"" ); // Simplified representation
					}
				}
				sb.Append( IsSelfClosing ? " />" : ">" );
				return sb.ToString();
			}
			else // Text
			{
				return $"Text: \"{TextContent?.Trim()}\"";
			}
		}
	}


	public enum NodeType
	{
		Element,
		Text,
		Directive, // @using, @inherits, @layout etc.
		CodeBlock // @code { ... }
				  // Add Comment if you want to represent @*...*@ in the tree
	}
}
