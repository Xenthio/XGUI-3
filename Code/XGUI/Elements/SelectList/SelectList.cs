using Sandbox.UI;

namespace XGUI;

public class SelectList : Panel
{
	public ListOption SelectedOption;
	public SelectList()
	{
		SetClass( "selectlist", true );
	}
	protected override void OnChildAdded( Panel child )
	{
		base.OnChildAdded( child );
		if ( child is ListOption opt )
		{
			opt.ParentList = this;
		}
	}
	public void OptionSelected( ListOption option )
	{
		if ( SelectedOption != null ) SelectedOption.Selected = false;
		SelectedOption = option;
		SelectedOption.Selected = true;
	}
}
