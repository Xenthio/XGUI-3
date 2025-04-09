using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;
namespace XGUI;

[Library( "radiooption" ), Alias( "radiobutton" )]
public class RadioButton : Panel
{

	/// <summary>
	/// The radio button icon. Although no guarentees it's an icon!
	/// </summary>
	public Panel CheckMark { get; protected set; }


	/// <summary>
	/// The radio button segments. for themes that use characters that make up the radio button.
	/// </summary>
	internal Label OptionalRadioSegment1 { get; set; }
	internal Label OptionalRadioSegment2 { get; set; }
	internal Label OptionalRadioSegment3 { get; set; }

	protected bool isSelected = false;

	/// <summary>
	/// Returns true if this checkbox is checked
	/// </summary>
	public bool Selected
	{
		get => isSelected;
		set
		{
			if ( isSelected == value )
				return;

			isSelected = value;
			OnValueChanged();
		}
	}

	/// <summary>
	/// Returns true if this checkbox is checked
	/// </summary>
	public bool Value
	{
		get => Selected;
		set => Selected = value;
	}

	public Label Label { get; protected set; }

	public string LabelText
	{
		get => Label?.Text;
		set
		{
			if ( Label == null )
			{
				Label = Add.Label();
			}

			Label.Text = value;
		}
	}

	public RadioButton()
	{
		AddClass( "radiobutton" );
		CheckMark = Add.Panel( "checkpanel" );
		var b = Add.Label( "a", "checklabel" );
		CheckMark.AddChild( b );
		OptionalRadioSegment1 = Add.Label( "", "radio-seg1" );
		OptionalRadioSegment2 = Add.Label( "", "radio-seg2" );
		OptionalRadioSegment3 = Add.Label( "", "radio-seg3" );
		CheckMark.AddChild( OptionalRadioSegment1 );
		CheckMark.AddChild( OptionalRadioSegment2 );
		CheckMark.AddChild( OptionalRadioSegment3 );
	}

	public override void SetProperty( string name, string value )
	{
		base.SetProperty( name, value );

		if ( name == "checked" || name == "value" )
		{
			Selected = value.ToBool();
		}

		if ( name == "text" )
		{
			LabelText = value;
		}
	}

	public override void SetContent( string value )
	{
		LabelText = value?.Trim() ?? "";
	}

	public Action<bool> ValueChanged { get; set; }

	public virtual void OnValueChanged()
	{
		UpdateState();
		CreateEvent( "onchange", Selected );
		ValueChanged?.Invoke( Selected );

		if ( Selected )
		{
			CreateEvent( "onchecked" );
		}
		else
		{
			CreateEvent( "onunchecked" );
		}
	}

	protected virtual void UpdateState()
	{
		SetClass( "checked", Selected );
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );

		if ( Parent is RadioButtons radbuttons )
		{
			if ( radbuttons.SelectedRadioOption != this )
			{
				if ( radbuttons.SelectedRadioOption != null )
				{
					radbuttons.SelectedRadioOption.Selected = false;
				}
				radbuttons.SelectedRadioOption = this;
				Selected = true;
				CreateValueEvent( "checked", Selected );
				CreateValueEvent( "value", Selected );
			}
		}
		e.StopPropagation();
	}
}
