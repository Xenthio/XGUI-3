﻿using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;
using System.Linq;
namespace XGUI
{
	/// <summary>
	/// A horizontal slider. Can be float or whole number.
	/// </summary>
	public class SliderScale : Panel
	{
		public bool HasScales = true;
		public Panel SliderArea { get; protected set; }
		public Panel SliderControl { get; protected set; }
		public Panel Track { get; protected set; }
		public Panel TrackInner { get; protected set; }
		public Panel Thumb { get; protected set; }
		public Label ThumbIconLabel { get; protected set; }
		public Panel ScaleSteps { get; protected set; }
		public Label ScaleStepsMin { get; protected set; }
		public Label ScaleStepsMax { get; protected set; }
		public Label Label { get; protected set; }

		/// <summary>
		/// The right side of the slider
		/// </summary>
		public float MaxValue { get; set; } = 100;

		/// <summary>
		/// The left side of the slider
		/// </summary>
		public float MinValue { get; set; } = 0;

		/// <summary>
		/// If set to 1, value will be rounded to 1's
		/// If set to 10, value will be rounded to 10's
		/// If set to 0.1, value will be rounded to 0.1's
		/// </summary>
		public float Step { get; set; } = 1.0f;

		public SliderScale()
		{
			AddClass( "sliderroot" );

			Label = AddChild<Label>();
			Label.Style.Display = DisplayMode.None;
			Label.AcceptsFocus = true;
			SliderArea = AddChild<Panel>();
			SliderArea.AddClass( "sliderarea" );

			SliderControl = SliderArea.AddChild<Panel>();
			SliderControl.AcceptsFocus = true;
			SliderControl.AddClass( "slider" );

			Track = SliderControl.Add.Panel( "track" );
			TrackInner = Track.Add.Panel( "inner" );

			Thumb = SliderControl.Add.Panel( "thumb" );
			ThumbIconLabel = Thumb.Add.Label( "", "thumbicon" );

			if ( HasScales )
			{
				AddClass( "hasscales" );
				ScaleSteps = SliderControl.Add.Panel( "scalesteps" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );
				ScaleSteps.Add.Panel( "step" );

				ScaleStepsMin = SliderControl.Add.Label( "", "scalestepmin" );
				ScaleStepsMax = SliderControl.Add.Label( "", "scalestepmax" );
			}
		}
		public override void Tick()
		{
			base.Tick();
			Label.SetClass( "focus", SliderControl.HasFocus || Thumb.HasFocus || Label.HasFocus );

		}
		protected float _value = float.MaxValue;

		/// <summary>
		/// The actual value. Setting the value will snap and clamp it.
		/// </summary>
		public float Value
		{
			get => _value.Clamp( MinValue, MaxValue );
			set
			{
				var snapped = Step > 0 ? value.SnapToGrid( Step ) : value;
				snapped = snapped.Clamp( MinValue, MaxValue );

				if ( _value == snapped ) return;

				_value = snapped;

				CreateEvent( "onchange" );
				CreateValueEvent( "value", _value );
				UpdateSliderPositions();
			}
		}

		public override void SetProperty( string name, string value )
		{
			if ( name == "min" && float.TryParse( value, out var floatValue ) )
			{
				MinValue = floatValue;
				ScaleStepsMin.Text = floatValue.ToString();
				UpdateSliderPositions();
				return;
			}

			if ( name == "step" && float.TryParse( value, out floatValue ) )
			{
				Step = floatValue;
				UpdateSliderPositions();
				return;
			}

			if ( name == "max" && float.TryParse( value, out floatValue ) )
			{
				MaxValue = floatValue;
				UpdateSliderPositions();
				return;
			}

			if ( name == "mintext" )
			{
				ScaleStepsMin.Text = value;
				UpdateSliderPositions();
				return;
			}

			if ( name == "maxtext" )
			{
				ScaleStepsMax.Text = value;
				UpdateSliderPositions();
				return;
			}

			if ( name == "value" && float.TryParse( value, out floatValue ) )
			{
				Value = floatValue;
				return;
			}


			if ( name == "label" )
			{
				Label.Style.Display = DisplayMode.Flex;
				Label.Text = value;
				return;
			}
			base.SetProperty( name, value );
		}

		/// <summary>
		/// Convert a screen position to a value. The value is clamped, but not snapped.
		/// </summary>
		public virtual float ScreenPosToValue( Vector2 pos )
		{
			var localPos = SliderControl.ScreenPositionToPanelPosition( pos );
			var thumbSize = Thumb.Box.Rect.Width * 0.5f;
			var normalized = MathX.LerpInverse( localPos.x, thumbSize, SliderControl.Box.Rect.Width - thumbSize, true );
			var scaled = MathX.LerpTo( MinValue, MaxValue, normalized, true );
			return Step > 0 ? scaled.SnapToGrid( Step ) : scaled;
		}

		/// <summary>
		/// If we move the mouse while we're being pressed then set the position,
		/// but skip transitions.
		/// </summary>
		protected override void OnMouseMove( MousePanelEvent e )
		{
			base.OnMouseMove( e );

			if ( !HasActive ) return;

			Value = ScreenPosToValue( Mouse.Position );
			UpdateSliderPositions();
			SkipTransitions();
			e.StopPropagation();
		}

		/// <summary>
		/// On mouse press jump to that position
		/// </summary>
		protected override void OnMouseDown( MousePanelEvent e )
		{
			base.OnMouseDown( e );

			Value = ScreenPosToValue( Mouse.Position );
			UpdateSliderPositions();
			e.StopPropagation();
		}

		int positionHash;

		bool ThumbPosDoNotCompensateForThumbSize()
		{
			/*foreach ( var b in StyleSheet.CollectVariables() )
			{
				Log.Info( $"{b.key}: {b.value}" );
			}*/
			if ( StyleSheet.CollectVariables().Any( x => x.key == "$xgui-slider-thumbsizedonotcompensate" && x.value.ToBool() ) )
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Updates the styles for TrackInner and Thumb to position us based on the current value.
		/// Note this purposely uses percentages instead of pixels when setting up, this way we don't
		/// have to worry about parent size, screen scale etc.
		/// </summary>
		void UpdateSliderPositions()
		{
			var hash = HashCode.Combine( Value, MinValue, MaxValue );
			if ( hash == positionHash ) return;

			positionHash = hash;

			var pos = MathX.LerpInverse( Value, MinValue, MaxValue, true );

			if ( !ThumbPosDoNotCompensateForThumbSize() )
			{
				var thumbSize = Thumb.Box.Rect.Width;
				// pos needs to be adjusted to account for the thumb size in pixels, but still needs to be a fraction
				var pixelPos = pos * SliderControl.Box.Rect.Width - (thumbSize);
				pos = MathX.LerpInverse( pixelPos, 0, SliderControl.Box.Rect.Width, true );
			}

			TrackInner.Style.Width = Length.Fraction( pos );
			Thumb.Style.Left = Length.Fraction( pos );

			TrackInner.Style.Dirty();
			Thumb.Style.Dirty();
		}

	}

	namespace Construct
	{
		public static class SliderScaleConstructor
		{
			public static SliderScale SliderScale( this PanelCreator self, float min, float max, float step, string mintext, string maxtext )
			{
				var control = self.panel.AddChild<SliderScale>();
				control.MinValue = min;
				control.MaxValue = max;
				control.Step = step;

				return control;
			}
		}
	}
}
