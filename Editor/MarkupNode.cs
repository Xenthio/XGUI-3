using System.Collections.Generic;
using System.Text;

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
    }
}
