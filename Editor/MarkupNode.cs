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
			// add or modify left/top style properties
			string style = "";

			// replace left/top properties if they exist
			if ( Attributes.ContainsKey( "style" ) )
			{
				style = Attributes["style"];
				if ( style.Contains( $"{name}:" ) )
				{
					// Replace any "name: *;" or "name:*;" pattern with the new value 
					style = System.Text.RegularExpressions.Regex.Replace( style, $"{name}:\\s*[^;]*;", $"{name}: {value};" );
				}
				else
				{
					style += $" {name}: {value};";
				}
			}
			else
			{
				style = $"{name}: {value};";
			}

			Log.Info( $"Original style: {Attributes["style"]}" );
			Attributes["style"] = style;
			Log.Info( $"Modified style: {style}" );
		}
	}
}
