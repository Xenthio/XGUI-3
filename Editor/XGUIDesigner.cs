using Editor;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace XGUI.XGUIEditor
{
	public static class XGUIMenu
	{
		[Menu( "Editor", "XGUI/Designer" )]
		public static void OpenMyMenu()
		{
			var b = new XGUIDesigner();
		}
	}

	public class XGUIDesigner : DockWindow
	{
		// UI Elements
		private Menu _recentFilesMenu;
		private XGUIView _view;
		private Widget _heirarchy;
		private PanelInspector _inspector;
		private Widget _componentPalette;
		private Widget _codeView;
		private TextEdit _codeTextEditor; // Direct reference to the code editor

		// State
		private readonly List<string> _recentFiles = new();
		private string _currentFilePath;
		private bool _isDirty;
		private enum ViewMode { Design, Code, Split }
		private ViewMode _currentViewMode = ViewMode.Design;
		private bool _isUpdatingCodeFromUI = false; // Flag to prevent update loops
		private bool _isUpdatingUIFromCode = false; // Flag to prevent update loops

		// Parsed Document State
		private string _fullRazorContentCache = ""; // Cache of the full content for modification
		private List<MarkupNode> _rootMarkupNodes = new List<MarkupNode>();
		private Dictionary<Panel, MarkupNode> _panelToMarkupNodeMap = new Dictionary<Panel, MarkupNode>();
		private Dictionary<MarkupNode, Panel> _markupNodeToPanelMap = new Dictionary<MarkupNode, Panel>();

		// Regexes
		private static readonly Regex _rootContentRegex = new Regex( @"(<root[^>]*>)([\s\S]*?)(</root>)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline );
		private static readonly Regex _codeBlockRegex = new Regex( @"(@code\s*{[\s\S]*?})", RegexOptions.Multiline | RegexOptions.Compiled );
		private static readonly Regex _directivesRegex = new Regex( @"^((?:@using|@inherits)[\s\S]*?\n)", RegexOptions.Multiline | RegexOptions.Compiled ); // Simple greedy match for directives at start


		public XGUIDesigner()
		{
			DeleteOnClose = true;
			Title = "XGUI Razor Designer";
			Size = new Vector2( 1280, 720 );
			CreateUI();
			Show();
			New(); // Start with a blank document
		}

		protected override bool OnClose()
		{
			_view?.CleanUp();
			return base.OnClose();
		}

		public void CreateUI()
		{
			BuildMenuBar();

			// --- Views ---
			_view = new XGUIView { OnElementSelected = OnDesignViewElementSelected };
			_view.SetSizeMode( SizeMode.Expand, SizeMode.Expand );

			_codeView = new Widget( null );
			_codeView.SetSizeMode( SizeMode.Expand, SizeMode.Expand );
			CreateCodeViewInternal(); // Creates _codeTextEditor

			// --- Panels ---
			_heirarchy = new Widget( null ) { Layout = Layout.Column() };
			_inspector = new PanelInspector() { OnPropertyChanged = OnInspectorPropertyChanged }; // Hook up delegate
			_componentPalette = new Widget( null ) { Layout = Layout.Column() };
			CreateComponentPaletteInternal();


			// --- Docking ---
			_view.WindowTitle = "Design View";
			_view.SetWindowIcon( "visibility" );
			DockManager.AddDock( null, _view );

			_heirarchy.WindowTitle = "Hierarchy";
			_heirarchy.SetWindowIcon( "view_list" );
			DockManager.AddDock( null, _heirarchy, dockArea: DockArea.Left, split: 0.20f );

			_inspector.WindowTitle = "Inspector";
			_inspector.SetWindowIcon( "info" );
			DockManager.AddDock( null, _inspector, dockArea: DockArea.Right, split: 0.20f );

			_componentPalette.WindowTitle = "Component Palette";
			_componentPalette.SetWindowIcon( "view_module" );
			_codeView.WindowTitle = "Code View";
			_codeView.SetWindowIcon( "code" );

			DockManager.AddDock( null, _componentPalette, dockArea: DockArea.Bottom, split: 0.30f );
			DockManager.AddDock( _componentPalette, _codeView, dockArea: DockArea.Right, split: 0.40f );
		}

		//---------------------------------------------------------------------
		// Event Handlers & Core Logic
		//---------------------------------------------------------------------

		/// <summary>
		/// Called when an element is clicked in the Design View (XGUIView).
		/// </summary>
		private void OnDesignViewElementSelected( Panel selectedPanel )
		{
			if ( selectedPanel != null )
			{
				// Find the MarkupNode that created this Panel
				if ( _panelToMarkupNodeMap.TryGetValue( selectedPanel, out MarkupNode sourceNode ) )
				{
					SelectAndInspect( sourceNode, selectedPanel );
				}
				else
				{
					// Panel clicked has no corresponding source node (e.g., dynamic child like a Tab)
					Log.Info( $"Clicked panel {selectedPanel.GetType().Name} has no direct source mapping." );
					// Try selecting the first ancestor that *does* have a mapping
					Panel ancestor = selectedPanel.Parent;
					MarkupNode ancestorNode = null;
					while ( ancestor != null && !_panelToMarkupNodeMap.TryGetValue( ancestor, out ancestorNode ) )
					{
						ancestor = ancestor.Parent;
					}
					SelectAndInspect( ancestorNode, ancestor ); // Inspect ancestor or clear if null
				}
			}
			else
			{
				SelectAndInspect( null, null ); // Clear selection
			}
		}

		/// <summary>
		/// Called when an item is selected in the Hierarchy TreeView.
		/// </summary>
		private void OnHierarchyNodeSelected( object item )
		{
			// *** LOG 1: Check if the event fires at all ***
			Log.Info( $"OnHierarchyNodeSelected Fired! Received item of type: {item?.GetType()?.FullName ?? "null"}" );

			if ( item is MarkupNode node && node.Type == NodeType.Element )
			{
				// *** LOG 2: Check if item is a valid MarkupNode Element ***
				Log.Info( $"  Item is MarkupNode Element: <{node.TagName}>" );

				_markupNodeToPanelMap.TryGetValue( node, out Panel correspondingPanel );
				SelectAndInspect( node, correspondingPanel );
			}
			else
			{
				// *** LOG 3: Check if item is something else (or null) ***
				Log.Info( $"  Item is NOT a MarkupNode Element (or is null). Clearing Inspector." );
				SelectAndInspect( null, null ); // Clear selection if text node or something else selected
			}
		}

		/// <summary>
		/// Central method to update inspector and potentially highlight selection.
		/// </summary>
		private void SelectAndInspect( MarkupNode node, Panel panel )
		{
			_inspector.SetTarget( panel, node );
			// TODO: Add visual highlighting in TreeView and DesignView if needed
			// FindTreeNodeAndSelect(node);
			// _view?.HighlightPanel(panel);
		}


		/// <summary>
		/// Called when a property is changed in the Inspector.
		/// </summary>
		private void OnInspectorPropertyChanged( MarkupNode node, string propertyOrAttributeName, object newValue )
		{
			Log.Info( $"Designer: OnInspectorPropertyChanged received -> Node Type: {node?.GetType()?.FullName ?? "null"}, TagName: '{node?.TagName ?? "N/A"}', propertyOrAttributeName: '{propertyOrAttributeName}', newValue: '{newValue}'" );

			// --- >>> !! THE CRITICAL GUARD CLAUSE - ENSURE THIS EXACT CODE IS PRESENT !! <<< ---
			if ( node == null || node.Type != NodeType.Element || string.IsNullOrEmpty( node.TagName ) || _isUpdatingUIFromCode )
			{
				Log.Warning( $"Designer: OnInspectorPropertyChanged ignored. Reason: " +
							$"{(node == null ? "Node is null" : "")} " +
							$"{(node?.Type != NodeType.Element ? $"Node is not Element (Type:{node?.Type})" : "")} " +
							$"{(string.IsNullOrEmpty( node?.TagName ) ? "Node TagName is empty" : "")} " +
							$"{(_isUpdatingUIFromCode ? "UI is updating from code" : "")}. " +
							$"Node TagName: '{node?.TagName ?? "N/A"}', IsUpdating: {_isUpdatingUIFromCode}" );
				return; // <<< =================== EXIT POINT ===================
			}
			// --- End Guard Clause ---


			// Prevent re-entry during source code modification triggered by this callback
			if ( _isUpdatingCodeFromUI )
			{
				Log.Info( "Designer: OnInspectorPropertyChanged ignored (already updating code from this UI change)" );
				return;
			}

			// Log 2: If Guard Clause Passed, Log processing details
			Log.Info( $"Designer: Processing Inspector change -> NodeTag='{node.TagName}', Property='{propertyOrAttributeName}', NewValue='{newValue}'" ); // We know node and TagName are valid here

			_isDirty = true;

			_isDirty = true;

			// --- Trigger Source Code Modification ---
			try
			{
				_isUpdatingCodeFromUI = true; // Set flag BEFORE calling ModifySourceCode

				// Log 3: Right before calling ModifySourceCode
				Log.Info( $"Designer: Calling ModifySourceCode for NodeTag='{node.TagName}'..." );

				string modifiedContent = ModifySourceCode( node, propertyOrAttributeName, newValue ); // Pass the validated node

				// Check result AFTER calling ModifySourceCode
				if ( modifiedContent != _fullRazorContentCache )
				{
					SetCodeEditorText( modifiedContent );
					_fullRazorContentCache = modifiedContent;
					Log.Info( "Designer: Source code updated successfully from inspector change." );
					// Optional re-parse here if needed immediately: ParseAndUpdateUI(modifiedContent);
				}
				else
				{
					Log.Warning( "Designer: Inspector change resulted in no source code modification (ModifySourceCode returned original content or failed)." );
				}
			}
			catch ( Exception ex )
			{
				Log.Error( $"Designer: CRITICAL Error during ModifySourceCode execution: {ex.Message}\n{ex.StackTrace}" );
				// Still return original content if ModifySourceCode throws
			}
			finally
			{
				_isUpdatingCodeFromUI = false; // Clear flag AFTER ModifySourceCode finishes or throws
			}

		}

		/// <summary>
		/// Called when the text in the Code View editor changes.
		/// </summary>
		private void OnCodeTextChanged( string newContent )
		{
			if ( _isUpdatingCodeFromUI ) return; // Ignore changes triggered by UI modifications

			_isDirty = true;
			_fullRazorContentCache = newContent; // Update cache immediately

			// Debounce this? Parsing on every keystroke might be slow.
			// For now, parse immediately.
			ParseAndUpdateUI( newContent );
		}

		//---------------------------------------------------------------------
		// Core Update/Parsing/Generation Logic
		//---------------------------------------------------------------------

		/// <summary>
		/// Parses the full Razor content and updates the MarkupNode tree, Panel tree (Design View), and Hierarchy View.
		/// </summary>
		private void ParseAndUpdateUI( string fullRazorContent )
		{
			if ( _isUpdatingCodeFromUI ) return; // Prevent re-entry

			try
			{
				_isUpdatingUIFromCode = true; // Prevent inspector changes during UI rebuild

				// Clear previous state
				_panelToMarkupNodeMap.Clear();
				_markupNodeToPanelMap.Clear();
				_rootMarkupNodes.Clear();
				// Don't delete _view.Window / WindowContent, just clear children
				_view?.WindowContent?.DeleteChildren( true );
				if ( _view?.WindowContent == null )
				{
					_view?.CreateBlankWindow(); // Ensure base window exists
				}


				// --- Parsing Phase ---
				int contentOffset = 0;
				string htmlContent = "";
				var rootContentMatch = _rootContentRegex.Match( fullRazorContent );
				if ( rootContentMatch.Success )
				{
					htmlContent = rootContentMatch.Groups[2].Value;
					contentOffset = rootContentMatch.Groups[2].Index;
				}
				else
				{
					Log.Warning( "Could not find <root>...</root> content in razor code during ParseAndUpdateUI." );
					// Handle case with no root? Maybe parse entire content as potential fragments?
					// For now, assume root exists or parsing yields empty nodes.
					// htmlContent = fullRazorContent; // Or parse the whole thing? Risky.
					// contentOffset = 0;
				}

				_rootMarkupNodes = MarkupParser.Parse( htmlContent );
				AdjustSourcePositions( _rootMarkupNodes, contentOffset );


				// --- Panel Creation Phase (UI Update) ---
				if ( _view?.WindowContent != null )
				{
					foreach ( var node in _rootMarkupNodes )
					{
						CreatePanelsRecursive( node, _view.WindowContent );
					}
					RegisterSelectionHandlersForChildren( _view.WindowContent );
				}

				// --- Hierarchy Update ---
				UpdateHierarchyPanelInternal();

			}
			catch ( System.Exception ex )
			{
				Log.Error( $"Error in ParseAndUpdateUI: {ex.Message}\n{ex.StackTrace}" );
			}
			finally
			{
				_isUpdatingUIFromCode = false;
			}
		}

		/// <summary>
		/// Recursively creates Panel objects based on the MarkupNode tree.
		/// </summary>
		private void CreatePanelsRecursive( MarkupNode node, Panel parentPanel )
		{
			// (Identical to previous implementation)
			if ( node.Type == NodeType.Element )
			{
				Panel newElement = CreateElementFromTag( node.TagName );

				if ( newElement != null )
				{
					parentPanel.AddChild( newElement );
					ApplyAttributesToElement( newElement, node.Attributes ); // Apply parsed attributes

					// Store mapping
					_panelToMarkupNodeMap[newElement] = node;
					_markupNodeToPanelMap[node] = newElement;

					// Recurse for child nodes
					foreach ( var childNode in node.Children )
					{
						CreatePanelsRecursive( childNode, newElement );
					}
					// Handle simple text content AFTER children and attributes are done
					ApplyTextContent( newElement, node );
				}
			}
		}
		// <summary>
		/// Updates the source code string based on a change from the inspector.
		/// </summary>
		private string ModifySourceCode( MarkupNode node, string propertyOrAttributeName, object newValue )
		{
			// Final paranoid check (keep this)
			if ( node == null || string.IsNullOrEmpty( node.TagName ) || node.Type != NodeType.Element || node.SourceStart < 0 || node.SourceEnd <= node.SourceStart )
			{
				Log.Error( $"ModifySourceCode: Received invalid node despite checks! Tag='{node?.TagName ?? "N/A"}', Type={node?.Type}, Start={node?.SourceStart}, End={node?.SourceEnd}. Aborting modification." );
				return _fullRazorContentCache; // Return original content
			}
			string currentContent = _fullRazorContentCache;
			string modifiedContent = currentContent; // <-- Starts as a copy of the original full source code
			int delta = 0; // Initialize change in length to zero
			int modificationEndPosition = -1; // Initialize position marker to invalid

			int nodeStart = node.SourceStart;
			int nodeLength = node.SourceLength; // Use the calculated length property
			int nodeEnd = node.SourceEnd; // Keep for reference/verification if needed

			// --- >>> DETAILED LOGGING FOR SOURCE EXTRACTION <<< ---
			Log.Info( $"ModifySourceCode: Processing <{node.TagName}> received from Designer." );
			Log.Info( $"  - Node SourceStart: {nodeStart}" );
			Log.Info( $"  - Node SourceEnd: {nodeEnd}" );
			Log.Info( $"  - Node SourceLength: {nodeLength}" ); // Log the property used
			Log.Info( $"  - Cache Length: {currentContent?.Length ?? -1}" );

			// Check validity of indices BEFORE Substring
			if ( currentContent == null || nodeStart < 0 || nodeLength <= 0 || nodeStart + nodeLength > currentContent.Length )
			{
				Log.Error( $"  !!! INVALID Node Source Indices for Cache! Start={nodeStart}, Length={nodeLength}, CacheLen={currentContent?.Length}. Cannot extract source." );
				return currentContent; // Abort
			}
			// --- >>> END INDEX CHECK <<< ---

			string nodeOriginalSource = ""; // Initialize

			try // Wrap substring extraction in try-catch as well
			{
				// Extract the substring based on Start and Length
				nodeOriginalSource = currentContent.Substring( nodeStart, nodeLength );

				// --- >>> LOG THE EXTRACTED SOURCE <<< ---
				Log.Info( $"  - Extracted Node Source String (Length={nodeOriginalSource.Length}):" );
				Log.Info( $"------BEGIN SOURCE SNIPPET------\n{nodeOriginalSource}\n------END SOURCE SNIPPET------" );
				// --- >>> END LOGGING <<< ---
			}
			catch ( ArgumentOutOfRangeException ex )
			{
				Log.Error( $"  !!! CRASH trying to Substring! Start={nodeStart}, Length={nodeLength}, CacheLen={currentContent?.Length}. Exception: {ex.Message}" );
				return currentContent; // Abort
			}
			catch ( Exception ex )
			{
				Log.Error( $"  !!! Unexpected Error during Substring! {ex.Message}" );
				return currentContent; // Abort
			}

			// Fallback if extraction failed silently (shouldn't happen with checks/catch)
			if ( string.IsNullOrEmpty( nodeOriginalSource ) )
			{
				Log.Error( "  !!! Extracted nodeOriginalSource is unexpectedly empty after Substring!" );
				return currentContent;
			}


			// --- Now proceed with the rest of the try-catch block ---
			try
			{
				string nodeModifiedSource = nodeOriginalSource; // Start modifying from extracted source

				int changeOffsetInNode = -1; // Where the change occurred relative to nodeOriginalSource start
				int originalLengthInNode = -1; // Length of the part being replaced within nodeOriginalSource
				string replacementString = ""; // The new string to insert/replace with

				// --- Determine Modification Type and Apply Change to nodeModifiedSource ---
				string attributeName = propertyOrAttributeName.ToLowerInvariant();
				string stringValue = newValue?.ToString() ?? ""; // Ensure string representation

				if ( attributeName == "style" || attributeName == "class" || node.Attributes.ContainsKey( attributeName ) || CanHaveAttribute( node.TagName, attributeName ) )
				{
					// --- Attribute Modification ---
					Log.Info( $"  -> Modifying Attribute: '{attributeName}'" ); // Log which path we're taking

					bool isValueless = IsValuelessAttribute( attributeName );
					bool shouldExist = isValueless ? Convert.ToBoolean( newValue ) : !string.IsNullOrWhiteSpace( stringValue );

					// --- >>> LOG BEFORE INDEXOF('>') <<< ---
					Log.Info( $"     - Searching for '>' char within the extracted source snippet..." );
					int openingTagEnd = nodeModifiedSource.IndexOf( '>' );
					Log.Info( $"     - Result of nodeModifiedSource.IndexOf('>'): {openingTagEnd}" );
					// --- >>> END LOGGING <<< ---

					// Regex approach needs the opening tag BOUNDARY:
					// Find the end of the opening tag strictly within the extracted snippet
					if ( openingTagEnd == -1 )
					{
						Log.Error( $"     !!! Cannot find closing '>' bracket of the opening tag within the extracted source snippet!" );
						return currentContent; // Abort modification if tag seems malformed/truncated
					}

					string openingTagContent = nodeModifiedSource.Substring( 0, openingTagEnd + 1 );
					Log.Info( $"     - Deduced Opening Tag content: `{openingTagContent}`" );

					// Regex to find the attribute within the OPENING TAG content
					// Added word boundaries (\b) for more precise matching
					var attrRegex = new Regex( $@"\s+({Regex.Escape( attributeName )})(?:\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>""]+)))?", RegexOptions.IgnoreCase );
					// Using the openingTagContent reduces risk of matching closing tag attributes
					Match attrMatch = attrRegex.Match( openingTagContent );


					if ( attrMatch.Success )
					{
						Log.Info( $"     - Attribute '{attributeName}' Found in opening tag at index {attrMatch.Index} (relative to opening tag start)" );
						// --- Attribute Exists ---
						if ( shouldExist ) // MODIFY or KEEP Value (if valueless)
						{
							// Modify existing attribute value
							changeOffsetInNode = attrMatch.Index; // Start of the matched attribute string (relative to opening tag)
							originalLengthInNode = attrMatch.Length;

							if ( isValueless )
							{
								replacementString = $" {attributeName}"; // Just the name
							}
							else
							{
								char quote = '"'; // Default to double
								if ( attrMatch.Groups[2].Success ) quote = '"';
								else if ( attrMatch.Groups[3].Success ) quote = '\'';
								// TODO: Handle unquoted values better if needed
								replacementString = $" {attributeName}={quote}{EncodeHtml( stringValue )}{quote}";
							}
							Log.Info( $"     - Modifying attribute: Replacing length {originalLengthInNode} at offset {changeOffsetInNode} with `{replacementString}`" );
							nodeModifiedSource = nodeModifiedSource.Remove( changeOffsetInNode, originalLengthInNode ).Insert( changeOffsetInNode, replacementString );
						}
						else // REMOVE
						{
							// Remove existing attribute
							changeOffsetInNode = attrMatch.Index; // Start including leading space
							originalLengthInNode = attrMatch.Length;
							replacementString = ""; // Replace with nothing
							Log.Info( $"     - Removing attribute: Removing length {originalLengthInNode} at offset {changeOffsetInNode}" );
							nodeModifiedSource = nodeModifiedSource.Remove( changeOffsetInNode, originalLengthInNode );
						}
					}
					else // Attribute Does NOT Exist
					{
						Log.Info( $"     - Attribute '{attributeName}' Not Found in opening tag." );
						if ( shouldExist ) // ADD
						{
							// Add new attribute
							// Find where to insert: just before '/>' or '>' IN THE OPENING TAG
							int insertPosInOpeningTag = openingTagContent.LastIndexOf( '>' );
							if ( node.IsSelfClosing )
							{
								int selfCloseMarker = openingTagContent.LastIndexOf( '/' );
								if ( selfCloseMarker > 0 && selfCloseMarker == insertPosInOpeningTag - 1 )
								{
									insertPosInOpeningTag = selfCloseMarker; // Insert before '/>'
								}
							}

							if ( insertPosInOpeningTag <= 0 )
							{
								Log.Error( $"     !!! Cannot find insert position ('>' or '/') for new attribute within deduced opening tag: `{openingTagContent}`" );
								return currentContent; // Abort
							}

							if ( isValueless )
							{
								replacementString = $" {attributeName}";
							}
							else
							{
								replacementString = $" {attributeName}=\"{EncodeHtml( stringValue )}\"";
							}

							changeOffsetInNode = insertPosInOpeningTag; // Insert position relative to opening tag start
							originalLengthInNode = 0; // Nothing is being replaced
							Log.Info( $"     - Adding attribute: Inserting `{replacementString}` at offset {changeOffsetInNode}" );
							nodeModifiedSource = nodeModifiedSource.Insert( changeOffsetInNode, replacementString ); // Insert into the *full node source*
						}
						else
						{
							// Attribute doesn't exist and shouldn't - No change needed
							Log.Info( $"     - Attribute should not exist, no change needed." );
							return currentContent; // IMPORTANT: Return original full content if no modification occurred
						}
					}
				} // End Attribute Handling
				else if ( attributeName == "innertext" )
				{
					// --- Inner Text Modification ---
					Log.Info( $"  -> Modifying Inner Text" );
					if ( node.IsSelfClosing )
					{
						Log.Warning( $"     - Cannot set inner text for self-closing tag <{node.TagName}>" );
						return currentContent; // No change
					}

					// Find where content starts (after opening tag '>') and ends (before closing tag '</')
					// These offsets are relative to the start of nodeOriginalSource
					int contentStartOffsetInNode = nodeOriginalSource.IndexOf( '>' ) + 1;
					int contentEndOffsetInNode = nodeOriginalSource.LastIndexOf( "</" );

					if ( contentStartOffsetInNode == 0 || contentEndOffsetInNode == -1 || contentEndOffsetInNode < contentStartOffsetInNode )
					{
						Log.Error( $"     !!! Could not determine content boundaries ('>' to '</') within extracted source snippet for <{node.TagName}>" );
						return currentContent; // Abort
					}
					Log.Info( $"     - Found content boundaries: StartOffset={contentStartOffsetInNode}, EndOffset={contentEndOffsetInNode} (relative to snippet start)" );

					// Calculate change details
					changeOffsetInNode = contentStartOffsetInNode; // Change starts right after '>'
					originalLengthInNode = contentEndOffsetInNode - contentStartOffsetInNode; // Length of existing content
					replacementString = EncodeHtml( stringValue ); // Encode the new value
					Log.Info( $"     - Replacing inner text: Original length {originalLengthInNode} at offset {changeOffsetInNode} with encoded text (len={replacementString.Length}): `{replacementString}`" );


					// Replace the content area in the node's source copy
					nodeModifiedSource = nodeModifiedSource
						.Remove( changeOffsetInNode, originalLengthInNode )
						.Insert( changeOffsetInNode, replacementString );

					// Update the MarkupNode tree's logical text node structure (this doesn't affect the source string modification)
					var textNode = node.Children.FirstOrDefault( c => c.Type == NodeType.Text );
					if ( textNode != null )
					{
						if ( string.IsNullOrWhiteSpace( stringValue ) ) node.Children.Remove( textNode );
						else textNode.TextContent = stringValue; // Update live node
					}
					else if ( !string.IsNullOrWhiteSpace( stringValue ) )
					{
						var newTextNode = new MarkupNode( NodeType.Text ) { TextContent = stringValue, Parent = node, SourceStart = -1, SourceEnd = -1 };
						node.Children.Insert( 0, newTextNode );
					}
				}
				else
				{
					Log.Warning( $"ModifySourceCode: Unhandled property '{propertyOrAttributeName}' for node <{node.TagName}>" );
					return currentContent; // No change for unhandled properties
				}

				// --- Apply calculated change back to the FULL content string ---
				if ( changeOffsetInNode != -1 /*&& nodeModifiedSource != nodeOriginalSource*/) // Ensure a change was actually calculated
				{
					// Calculate delta (change in length) based on the specific modification
					delta = replacementString.Length - originalLengthInNode;

					// Calculate the **absolute** position in the full string where the change starts
					int absoluteChangeStart = node.SourceStart + changeOffsetInNode;
					// Calculate the end position of the change *in the original full string* for position updates later
					modificationEndPosition = absoluteChangeStart + originalLengthInNode;

					Log.Info( $"  - Applying change to full source string:" );
					Log.Info( $"    - Absolute Change Start: {absoluteChangeStart}" );
					Log.Info( $"    - Original Length Replaced: {originalLengthInNode}" );
					Log.Info( $"    - Replacement String Length: {replacementString.Length}" );
					Log.Info( $"    - Delta: {delta}" );

					// Apply the change to the full content cache
					modifiedContent = currentContent
						.Remove( absoluteChangeStart, originalLengthInNode )
						.Insert( absoluteChangeStart, replacementString );

					// --- Update node positions ---
					Log.Info( $"  - Updating node source positions recursively..." );
					// Update the target node's end position first (important!)
					node.SourceEnd += delta;
					Log.Info( $"    - Updated self ({node.TagName}) SourceEnd to: {node.SourceEnd}" );
					// Then update all subsequent nodes/siblings/children recursively
					// The modificationEndPosition tells UpdateNodePositionsRecursive where the change stopped in the *original* string
					UpdateNodePositionsRecursive( _rootMarkupNodes, modificationEndPosition, delta );

					Log.Info( $"  - Source modification applied successfully." );
					return modifiedContent; // Return the new full content
				}
				else if ( nodeModifiedSource == nodeOriginalSource )
				{
					Log.Warning( "  - Modification logic resulted in no change to the node's source snippet." );
					return currentContent; // Return original full content
				}
				else
				{
					Log.Error( "  !!! Modification failed: changeOffsetInNode was -1, indicating calculation error." );
					return currentContent; // Return original full content
				}
			}
			catch ( Exception ex )
			{
				Log.Error( $"CRITICAL Error during ModifySourceCode's main processing block for <{node.TagName}> ({propertyOrAttributeName}): {ex.Message}\n{ex.StackTrace}" );
				return currentContent; // Return original content on unexpected error
			}
		}



		// --- Helpers for ModifySourceCode ---

		/// <summary>
		/// Checks if an attribute is typically valueless (e.g., checked, disabled).
		/// </summary>
		private bool IsValuelessAttribute( string attributeName )
		{
			return attributeName.ToLowerInvariant() switch
			{
				"checked" => true,
				"disabled" => true,
				"readonly" => true,
				"required" => true,
				// Add others if needed
				_ => false
			};
		}

		/// <summary>
		/// Placeholder check if a tag can generally have a specific attribute.
		/// Can be expanded for more accuracy based on HTML standards or component definitions.
		/// </summary>
		private bool CanHaveAttribute( string tagName, string attributeName )
		{
			// For now, allow common attributes on most elements
			return attributeName switch
			{
				"id" => true,
				"class" => true,
				"style" => true,
				"title" => true, // Global attribute
								 // Add specific checks: e.g., 'value' mostly on input/button/option etc.
				"value" => tagName.ToLowerInvariant() is "input" or "button" or "option" or "textentry",
				"min" or "max" or "step" => tagName.ToLowerInvariant() is "input" or "sliderscale",
				"checked" => tagName.ToLowerInvariant() is "input" or "check",
				_ => true // Allow unknown attributes for custom components? Or return false? Let's be permissive.
			};
		}

		// Placeholder/Helper needed for ModifySourceCode
		/// <summary>
		/// Recursively updates the SourceStart and SourceEnd positions of MarkupNodes
		/// in the tree after a string modification.
		/// </summary>
		/// <param name="nodes">The collection of nodes to check (initially _rootMarkupNodes).</param>
		/// <param name="modificationEndPosition">The position *after* the modification occurred in the original string.</param>
		/// <param name="delta">The change in length (positive for insertion, negative for deletion).</param>
		private void UpdateNodePositionsRecursive( IEnumerable<MarkupNode> nodes, int modificationEndPosition, int delta )
		{
			if ( delta == 0 || nodes == null ) return; // No change or no nodes

			foreach ( var node in nodes )
			{
				bool updated = false;

				// Adjust SourceStart if the modification happened at or before its start
				if ( node.SourceStart >= modificationEndPosition )
				{
					node.SourceStart += delta;
					updated = true; // Mark that SourceStart was shifted
				}

				// Adjust SourceEnd if the modification happened strictly before its end
				// OR if the modification happened at or before its start (handled above)
				if ( node.SourceEnd >= modificationEndPosition )
				{
					// If SourceStart was also shifted, SourceEnd gets the same shift.
					// If SourceStart was *not* shifted, but the modification happened
					// *within* the node or at its end, SourceEnd still needs adjusting.
					node.SourceEnd += delta;
				}
				// Special case: If modification happens *exactly* at SourceEnd, SourceStart is unaffected, but SourceEnd increases.


				// Recurse for children regardless of whether parent was updated,
				// as children might start after the modification point even if parent starts before.
				if ( node.Type == NodeType.Element && node.Children.Any() )
				{
					UpdateNodePositionsRecursive( node.Children, modificationEndPosition, delta );
				}
			}
		}


		//---------------------------------------------------------------------
		// UI Construction & Updates (Internal Helpers)
		//---------------------------------------------------------------------

		private void CreateCodeViewInternal()
		{
			_codeView.Layout = Layout.Column();
			_codeTextEditor = _codeView.Layout.Add( new TextEdit( null ), 1 );
			_codeTextEditor.TextChanged = OnCodeTextChanged;
			// Configure TextEdit (e.g., font, line numbers) if needed
		}

		private void CreateComponentPaletteInternal()
		{
			// (Identical to previous implementation)
			var container = _componentPalette.Layout.Add( new Widget( null ) );
			container.SetSizeMode( SizeMode.Expand, SizeMode.Expand );
			container.Layout = Layout.Row();
			container.Layout.Spacing = 4;
			var layoutsCategory = CreateComponentCategory( container.Layout, "Layouts" );
			var controlsCategory = CreateComponentCategory( container.Layout, "Controls" );
			var containersCategory = CreateComponentCategory( container.Layout, "Containers" );
			AddComponentButton( layoutsCategory, "Div", "div" );
			AddComponentButton( layoutsCategory, "Row", "div", "class=\"self-layout self-layout-row\"" );
			AddComponentButton( layoutsCategory, "Column", "div", "class=\"self-layout self-layout-column\"" );
			AddComponentButton( controlsCategory, "Button", "button" );
			AddComponentButton( controlsCategory, "Checkbox", "check" );
			AddComponentButton( controlsCategory, "Label", "label" );
			AddComponentButton( controlsCategory, "Text Entry", "textentry" );
			AddComponentButton( controlsCategory, "Slider", "sliderscale", "min=\"0\" max=\"100\" step=\"1\"" ); // Ensure quotes for parser
			AddComponentButton( containersCategory, "Group Box", "groupbox", "title=\"Group\"" );
			// AddComponentButton(containersCategory, "Tab Control", "tabcontrol");
			// AddComponentButton(containersCategory, "Combo Box", "combobox");
		}

		// Helper for Palette
		private Layout CreateComponentCategory( Layout parentLayout, string categoryName )
		{
			var categoryWidget = parentLayout.Add( new Widget( null ) );
			categoryWidget.Layout = Layout.Column();
			categoryWidget.Layout.Spacing = 2;
			categoryWidget.Layout.Add( new Editor.Label( categoryName ) );
			return categoryWidget.Layout;
		}

		// Helper for Palette
		private void AddComponentButton( Layout layout, string displayName, string tagName, string attributes = "" )
		{
			var button = layout.Add( new Editor.Button( displayName ) );
			button.Clicked = () => AddComponentToSource( tagName, attributes );
		}

		/// <summary>
		/// Adds a component by modifying the source code.
		/// </summary>
		private void AddComponentToSource( string tagName, string attributes = "" )
		{
			Log.Info( $"Adding component <{tagName}> to source" );
			if ( _isUpdatingUIFromCode ) return; // Avoid changes during UI rebuild

			string componentCode;
			if ( IsSelfClosingTag( tagName ) ) // Use helper
			{
				componentCode = $"<{tagName}{(string.IsNullOrWhiteSpace( attributes ) ? "" : " " + attributes)} />";
			}
			else
			{
				// For elements that can have inner content, don't add innertext attribute
				// but keep the opening and closing tags empty for now
				componentCode = $"<{tagName}{(string.IsNullOrWhiteSpace( attributes ) ? "" : " " + attributes)}></{tagName}>";
			}

			// Find insertion point:
			// Option 1: End of the root content
			// Option 2: After the currently selected node in the hierarchy? (More complex)
			int insertPosition = -1;
			var rootMatch = _rootContentRegex.Match( _fullRazorContentCache );
			if ( rootMatch.Success )
			{
				// Insert just before the closing </root> tag
				insertPosition = rootMatch.Groups[3].Index;
				// Add indentation based on context? Hard without full parse. Add basic indent.
				componentCode = "\n    " + componentCode; // Add newline and indent
			}
			else
			{
				Log.Warning( "Cannot add component: <root> tag not found." );
				return;
			}

			if ( insertPosition >= 0 )
			{
				string modifiedContent = _fullRazorContentCache.Insert( insertPosition, componentCode );

				// Calculate delta and update subsequent nodes/spans
				int delta = componentCode.Length;
				// UpdateNodePositionsRecursive(_rootMarkupNodes, insertPosition, delta); // Required!

				// Update editor and cache
				SetCodeEditorText( modifiedContent );
				_fullRazorContentCache = modifiedContent;

				// Trigger reparse immediately to see the new element
				ParseAndUpdateUI( modifiedContent );
				_isDirty = true;
			}
		}



		/// <summary>
		/// Updates the Hierarchy TreeView based on the _rootMarkupNodes.
		/// </summary>
		private void UpdateHierarchyPanelInternal()
		{
			// Find existing TreeView or create a new one
			TreeView treeView = _heirarchy.Children.OfType<TreeView>().FirstOrDefault();
			if ( treeView == null )
			{
				// Clear any old non-TreeView widgets if necessary before adding
				// _heirarchy.Layout.Clear(true); // Use if layout needs full reset
				treeView = new TreeView( _heirarchy );
				_heirarchy.Layout.Add( treeView );
			}
			else
			{
				treeView.Clear(); // Clear existing items efficiently
			}

			treeView.MultiSelect = false;
			treeView.ItemSelected = OnHierarchyNodeSelected; // Use specific handler

			// Build tree from the root MarkupNodes
			foreach ( var rootNode in _rootMarkupNodes )
			{
				BuildTreeForMarkupNodeRecursive( rootNode, null, treeView ); // Pass treeview for root items
			}
			// Don't expand all by default? User can expand.
			// treeView.ExpandAll();
		}


		/// <summary>
		/// Recursively builds the TreeView structure from MarkupNodes.
		/// </summary>
		private void BuildTreeForMarkupNodeRecursive( MarkupNode node, TreeNode parentTreeNode, TreeView treeView )
		{
			// (Identical to previous implementation)
			if ( node.Type == NodeType.Element )
			{
				string displayName = $"{node.TagName}";
				if ( node.Attributes.TryGetValue( "class", out var cls ) && !string.IsNullOrWhiteSpace( cls ) ) displayName += $" .{cls.Split( ' ' )[0]}";
				if ( node.Attributes.TryGetValue( "id", out var id ) && !string.IsNullOrWhiteSpace( id ) ) displayName += $" #{id}";

				var treeNode = new TreeNode( node ) { Name = displayName, /*ToolTip = $"Pos: {node.SourceStart}-{node.SourceEnd}",*/ Value = node };

				if ( parentTreeNode != null ) parentTreeNode.AddItem( treeNode );
				else treeView.AddItem( treeNode );

				foreach ( var childNode in node.Children )
				{
					BuildTreeForMarkupNodeRecursive( childNode, treeNode, treeView );
				}
			}
			// Optionally add text nodes here if desired
		}


		/// <summary>
		/// Recursively registers selection handlers for Panels in the Design View.
		/// </summary>
		private void RegisterSelectionHandlersForChildren( Panel parent )
		{
			if ( parent == null ) return;
			foreach ( var child in parent.Children )
			{
				if ( child is Panel childPanel )
				{
					// Check if this panel should be selectable (does it map back to a source node?)
					// For now, register all, but selection logic will handle mapping back.
					_view?.RegisterSelectionHandlers( childPanel ); // Use method from XGUIView
					RegisterSelectionHandlersForChildren( childPanel ); // Recurse
				}
			}
		}


		/// <summary>
		/// Safely sets the text of the code editor without triggering its TextChanged event.
		/// </summary>
		private void SetCodeEditorText( string text )
		{
			if ( _codeTextEditor == null ) return;
			var originalHandler = _codeTextEditor.TextChanged;
			try
			{
				_codeTextEditor.TextChanged = null;
				_codeTextEditor.PlainText = text;
			}
			finally
			{
				_codeTextEditor.TextChanged = originalHandler;
			}
		}

		//---------------------------------------------------------------------
		// Menu Actions & File I/O
		//---------------------------------------------------------------------

		public void BuildMenuBar() { /* (Identical to previous) */ }
		void New()
		{
			string template = @"@using Sandbox;
@using Sandbox.UI;
@using XGUI;
@inherits Panel

<root>
    <div class=""window-content"">
        <!-- Design your UI here -->
    </div>
</root>

@code {
    // Add your code here
}";
			SetCodeEditorText( template ); // Set initial text
			_fullRazorContentCache = template; // Prime the cache
			ParseAndUpdateUI( template ); // Parse initial state
			_currentFilePath = null;
			_isDirty = false;
			Title = "XGUI Razor Designer - Untitled";
		}
		void Open() { /* (Identical to previous) */ }
		void OpenFile( string path )
		{
			if ( !File.Exists( path ) ) return;
			try
			{
				string content = File.ReadAllText( path );
				SetCodeEditorText( content ); // Update editor
				_fullRazorContentCache = content; // Update cache
				ParseAndUpdateUI( content ); // Parse new file
				_currentFilePath = path;
				_isDirty = false;
				Title = $"XGUI Razor Designer - {Path.GetFileName( path )}";
				AddRecentFile( path );
			}
			catch ( System.Exception ex ) { Log.Error( $"Error opening file: {ex.Message}" ); }
		}
		void AddRecentFile( string path )
		{
			_recentFiles.Remove( path ); // Remove if exists to move to top
			_recentFiles.Insert( 0, path );
			if ( _recentFiles.Count > 10 ) _recentFiles.RemoveAt( 10 );
			UpdateRecentFilesMenu();
		}
		void UpdateRecentFilesMenu() { /* (Identical to previous) */ }
		void Save( bool saveas = false ) { /* (Identical to previous) */ }
		void SaveFile( string path )
		{
			if ( _codeTextEditor == null ) return;
			try
			{
				File.WriteAllText( path, _codeTextEditor.PlainText ); // Save current editor text
				_currentFilePath = path;
				_isDirty = false;
				Title = $"XGUI Razor Designer - {Path.GetFileName( path )}";
				AddRecentFile( path );
				Log.Info( $"File saved: {path}" );
			}
			catch ( System.Exception ex ) { Log.Error( $"Error saving file: {ex.Message}" ); }
		}


		//---------------------------------------------------------------------
		// MarkupNode to Panel Creation Helpers (Refined from previous)
		//---------------------------------------------------------------------

		// Base element creation (no attributes/content applied here)
		private Panel CreateElementFromTag( string tagName )
		{
			switch ( tagName.ToLowerInvariant() )
			{
				case "div": return new Panel();
				case "button": return new Sandbox.UI.Button();
				case "label": return new Sandbox.UI.Label();
				case "check": return new XGUI.CheckBox();
				case "textentry": return new Sandbox.UI.TextEntry();
				case "sliderscale": return new XGUI.SliderScale();
				case "groupbox": return new XGUI.GroupBox();
				// Add other cases
				default:
					Log.Warning( $"Unsupported tag for Panel creation: {tagName}" );
					return new Panel(); // Create generic Panel for unknown tags?
			}
		}

		// Apply attributes from parsed dictionary
		private void ApplyAttributesToElement( Panel element, Dictionary<string, string> attributes )
		{
			// (Identical to previous implementation - uses switch statement)
			if ( element == null || attributes == null ) return;
			foreach ( var kvp in attributes )
			{
				string name = kvp.Key; // Already lowercased if dictionary uses OrdinalIgnoreCase
				string value = kvp.Value; // Already decoded by parser
				switch ( name )
				{
					case "class": if ( value != null ) foreach ( var cls in value.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) ) element.AddClass( cls ); break;
					case "style": if ( value != null ) ApplyInlineStyles( element, value ); break;
					case "title": if ( element is XGUI.GroupBox gb && value != null ) gb.Title = value; break;
					case "min": if ( element is XGUI.SliderScale sl && float.TryParse( value, CultureInfo.InvariantCulture, out var v ) ) sl.MinValue = v; break;
					case "max": if ( element is XGUI.SliderScale slm && float.TryParse( value, CultureInfo.InvariantCulture, out var v2 ) ) slm.MaxValue = v2; break;
					case "checked": if ( element is XGUI.CheckBox cb ) cb.Checked = true; break; // Valueless implies true
																								 // Add other attributes (id, src, disabled, etc.)
					default: /* Log unknown? Store in Tags? */ break;
				}
			}
		}

		// Apply text content after attributes/children are processed
		private void ApplyTextContent( Panel element, MarkupNode node )
		{
			if ( node == null || node.Type != NodeType.Element ) return;

			// Find first direct child Text node
			var textNode = node.Children.FirstOrDefault( c => c.Type == NodeType.Text && !string.IsNullOrWhiteSpace( c.TextContent ) );
			if ( textNode != null )
			{
				string text = textNode.TextContent; // Already decoded
				if ( element is Sandbox.UI.Button btn ) btn.Text = text;
				else if ( element is Sandbox.UI.Label lbl ) lbl.Text = text;
				else if ( element is XGUI.CheckBox chk ) chk.LabelText = text;
				// Add other elements that take direct text content
			}
		}
		private void ApplyInlineStyles( Panel panel, string styleText )
		{
			// (Implementation identical to previous response - uses ParseLength/ParseColor)
			if ( panel == null || string.IsNullOrWhiteSpace( styleText ) ) return;

			var styles = styleText.Split( ';', StringSplitOptions.RemoveEmptyEntries );
			foreach ( var style in styles )
			{
				var parts = style.Split( ':', 2 ); // Split only on the first colon
				if ( parts.Length != 2 ) continue;

				string property = parts[0].Trim().ToLowerInvariant();
				string value = parts[1].Trim();

				try
				{
					switch ( property )
					{
						case "width": panel.Style.Width = ParseLength( value ); break;
						case "height": panel.Style.Height = ParseLength( value ); break;
						case "min-width": panel.Style.MinWidth = ParseLength( value ); break;
						case "min-height": panel.Style.MinHeight = ParseLength( value ); break;
						case "max-width": panel.Style.MaxWidth = ParseLength( value ); break;
						case "max-height": panel.Style.MaxHeight = ParseLength( value ); break;

						case "margin": /* TODO: Handle shorthand */ panel.Style.Margin = ParseLength( value ); break;
						case "margin-top": panel.Style.MarginTop = ParseLength( value ); break;
						case "margin-right": panel.Style.MarginRight = ParseLength( value ); break;
						case "margin-bottom": panel.Style.MarginBottom = ParseLength( value ); break;
						case "margin-left": panel.Style.MarginLeft = ParseLength( value ); break;

						case "padding": /* TODO: Handle shorthand */ panel.Style.Padding = ParseLength( value ); break;
						case "padding-top": panel.Style.PaddingTop = ParseLength( value ); break;
						case "padding-right": panel.Style.PaddingRight = ParseLength( value ); break;
						case "padding-bottom": panel.Style.PaddingBottom = ParseLength( value ); break;
						case "padding-left": panel.Style.PaddingLeft = ParseLength( value ); break;

						case "background-color": panel.Style.BackgroundColor = ParseColor( value ); break;
						case "color": panel.Style.FontColor = ParseColor( value ); break;
						case "font-size": panel.Style.FontSize = ParseLength( value ); break;
						// Add other CSS properties you want to parse
						// e.g., flex-direction, align-items, justify-content, border, border-radius, etc.
						default: break; // Ignore unknown styles
					}
				}
				catch ( Exception ex )
				{
					Log.Warning( $"Failed to apply style '{property}:{value}'. Error: {ex.Message}" );
				}
			}
			panel.Style.Dirty(); // Ensure styles are applied visually
		}
		// Parses CSS length (px, %) string
		private Length? ParseLength( string value ) { return Length.Parse( value ); }
		// Parses CSS color string (#rgb, #rrggbb, rgb(), rgba())
		private Color ParseColor( string colorValue ) { return Color.Parse( colorValue ).Value; }
		// Optional helpers from ParseColor
		/// <summary>
		/// Helper for ParseColor: Parses a single R, G, or B component (number or percentage).
		/// </summary>
		private float ParseColorComponent( string component )
		{
			component = component.Trim();
			if ( component.EndsWith( '%' ) )
			{
				if ( float.TryParse( component.Substring( 0, component.Length - 1 ), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent ) )
				{
					return Math.Clamp( percent / 100.0f, 0f, 1f );
				}
			}
			else
			{
				if ( int.TryParse( component, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value ) )
				{
					return Math.Clamp( value / 255.0f, 0f, 1f );
				}
				// Handle float values 0-1 directly? CSS Color 4 allows this.
				else if ( float.TryParse( component, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal ) )
				{
					return Math.Clamp( floatVal, 0f, 1f );
				}
			}
			throw new FormatException( $"Invalid color component format: {component}" );
		}

		/// <summary>
		/// Helper for ParseColor: Parses the alpha component (number 0-1 or percentage).
		/// </summary>
		private float ParseAlphaComponent( string alpha )
		{
			alpha = alpha.Trim();
			if ( alpha.EndsWith( '%' ) )
			{
				if ( float.TryParse( alpha.Substring( 0, alpha.Length - 1 ), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent ) )
				{
					return Math.Clamp( percent / 100.0f, 0f, 1f );
				}
			}
			else
			{
				if ( float.TryParse( alpha, NumberStyles.Float, CultureInfo.InvariantCulture, out float value ) )
				{
					return Math.Clamp( value, 0f, 1f );
				}
			}
			throw new FormatException( $"Invalid alpha component format: {alpha}" );
		}

		//---------------------------------------------------------------------
		// Source Code Generation / Manipulation Helpers (Mostly Placeholders)
		//---------------------------------------------------------------------

		// Adjusts SourceStart/SourceEnd after parsing only a fragment
		private void AdjustSourcePositions( IEnumerable<MarkupNode> nodes, int offset ) { /* (Identical to previous) */ }
		/// <summary>
		/// Basic HTML entity decoder.
		/// </summary>
		private string DecodeHtml( string text )
		{
			if ( string.IsNullOrEmpty( text ) ) return "";
			// Manual basic version: Order matters! Ampersand must be last.
			return text.Replace( "&lt;", "<" )
					   .Replace( "&gt;", ">" )
					   .Replace( "&quot;", "\"" )
					   .Replace( "&#39;", "'" ) // Handle apostrophe
					   .Replace( "&apos;", "'" ) // Handle apostrophe XML entity
					   .Replace( "&amp;", "&" ); // Ampersand last
		}

		/// <summary>
		/// Basic HTML entity encoder.
		/// </summary>
		private string EncodeHtml( string text )
		{
			if ( string.IsNullOrEmpty( text ) ) return "";
			// Manual basic version: Order matters! Ampersand must be first.
			return text.Replace( "&", "&amp;" )
					   .Replace( "<", "&lt;" )
					   .Replace( ">", "&gt;" )
					   .Replace( "\"", "&quot;" )
					   .Replace( "'", "&#39;" ); // Use numeric entity for apostrophe for broader compatibility
		}        /// <summary>
				 /// Checks if a tag name typically represents a self-closing element in HTML/Razor context.
				 /// Customize this list based on the elements you commonly use.
				 /// </summary>
		private bool IsSelfClosingTag( string tagName )
		{
			if ( string.IsNullOrEmpty( tagName ) ) return false;
			return tagName.ToLowerInvariant() switch
			{
				"textentry" => true,
				"input" => true,
				"img" => true,
				"br" => true,
				"hr" => true,
				// Add others like <meta>, <link> if relevant, though unlikely in UI markup
				_ => false,
			};
		}/// <summary>
		 /// Converts a Sandbox.Color to a CSS hex string (#RRGGBB or #RRGGBBAA).
		 /// </summary>
		private string ColorToHex( Color color )
		{
			// Include Alpha only if it's not fully opaque (or nearly opaque due to float precision)
			if ( color.a < 0.999f )
				return $"#{color.r:X2}{color.g:X2}{color.b:X2}{color.a:X2}";
			else
				return $"#{color.r:X2}{color.g:X2}{color.b:X2}";
		}

	}
}
