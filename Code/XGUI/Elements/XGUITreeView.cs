using Microsoft.AspNetCore.Components;
using Sandbox.UI;
using System;

namespace XGUI;

/// <summary>A native s&amp;box tree with XGUI presentation. Items remain application-owned data.</summary>
public class XGUITreeView<T> : Sandbox.UI.TreeView<T>
{
	[Parameter] public Func<T, string> GetText { get; set; }
	[Parameter] public Func<T, string> GetIcon { get; set; }
	[Parameter] public bool MultiSelect { get; set; } = true;

	public XGUITreeView()
	{
		AddClass( "xgui-native-tree" );
		RowHeight = 18;
		IndentWidth = 19;
	}

	protected override void BindRow( int row, TreeRow panel )
	{
		// Preserve native custom row callbacks and Razor templates when supplied.
		base.BindRow( row, panel );
		if ( OnRow != null || Row != null ) return;
		var item = GetRowItem( row );
		NativeTreeRow.Bind( panel, GetText != null ? GetText( item ) : item?.ToString(), GetIcon?.Invoke( item ) );
	}

	protected override void OnSelectionFinished()
	{
		if ( !MultiSelect && Selection.Count > 0 && CursorRow >= 0 )
		{
			bool changed = Selection.Count > 1 || !IsSelected( CursorItem );
			ClearSelectionInternal();
			SetRowSelected( CursorRow, true );
			if ( changed ) Refresh();
		}
		base.OnSelectionFinished();
	}
}
