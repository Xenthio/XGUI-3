using Sandbox;
using Sandbox.UI;
using System.Linq;

namespace XGUI;

/// <summary>
/// A themeable panel designed to be at the root of the XGUI hierarchy. For example, windows, popups, etc.
/// </summary>
public class XGUIPanel : Panel
{
	public string CurrentTheme = "";

	public XGUIPanel()
	{
		AddClass( "xgui-panel" );
	}

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );
		if ( firstTime )
		{
			// Set the initial theme
			if ( CurrentTheme == "" ) SetTheme( Scene.GetSystem<XGUISystem>().GlobalTheme );
		}
	}
	public void SetTheme( string theme )
	{
		var parent = this.Parent;

		// Remove existing style sheets (except .razor.scss ones) 
		foreach ( var style in AllStyleSheets.ToList() )
		{
			if ( !style.FileName.EndsWith( ".razor.scss" ) && !style.FileName.EndsWith( ".cs.scss" ) )
			{

				//Log.Info( style.FileName );
				StyleSheet.Remove( style.FileName );
			}
		}
		CurrentTheme = theme;
		var styleToApply = Sandbox.UI.StyleSheet.FromFile( theme );

		// Apply the new style
		StyleSheet.Add( styleToApply );

		// Force immediate style update
		Style.Dirty();

		// Force a complete rebuild by temporarily removing from parent and re-adding
		// This is more aggressive but guarantees a full refresh
		Parent = null;
		Parent = parent;

		// Force layout recalculation - traverse child hierarchy
		ForceStyleUpdateRecursive( this );
	}

	private void ForceStyleUpdateRecursive( Panel panel )
	{
		// Mark this panel's style as dirty to force recalculation
		panel.Style.Dirty();

		// Update all immediate children
		foreach ( var child in panel.Children )
		{
			if ( child == null || !child.IsValid() ) continue;

			// Mark the child's style as dirty
			child.Style.Dirty();

			// Recursively update this child's children
			ForceStyleUpdateRecursive( child );
		}
	}

}
