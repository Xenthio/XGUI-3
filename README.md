# XGUI-3

## Plans
- [X] Layout Designer
- [ ] Resource files for layout + layout designer.
- [ ] Less confusing element names
- [ ] Stop relying on built-in s&box elements
- [ ] Unify the 3 different sliders

## World-space UI

XGUI uses s&box's built-in `Sandbox.WorldPanel` for UI rendered in the 3D world. Add `WorldPanel` and `XGUIRootComponent` to the same GameObject. `PanelSize` is the UI layout resolution and `RenderScale` controls the physical render scale without changing that layout resolution.

Add `WorldInput` to the camera (or another input object) to route the native mouse cursor and click input to world panels:

```csharp
var screen = Scene.CreateObject();
screen.Components.Create<Sandbox.WorldPanel>();
screen.Components.Create<XGUIRootComponent>();

var worldPanel = screen.Components.Get<Sandbox.WorldPanel>();
worldPanel.PanelSize = new Vector2( 640, 480 );
worldPanel.RenderScale = 1.0f;

var camera = Scene.Camera.GameObject;
camera.Components.GetOrCreate<WorldInput>();
```

When `Mouse.Active` is true, `WorldInput` uses the native `Mouse.Position` ray. Keep `XGUIRootComponent.MouseUnlocked` enabled for interaction.

Multiple roots are supported. Add a `WorldPanel` and an `XGUIRootComponent` to each world-space GameObject, then add content to that root's `XGUIPanel`:

```csharp
var leftWorld = Scene.CreateObject();
leftWorld.Components.Create<Sandbox.WorldPanel>();
var leftRoot = leftWorld.Components.Create<XGUIRootComponent>();

var rightWorld = Scene.CreateObject();
rightWorld.Components.Create<Sandbox.WorldPanel>();
var rightRoot = rightWorld.Components.Create<XGUIRootComponent>();

leftRoot.XGUIPanel.AddChild<LeftWorldView>();
rightRoot.XGUIPanel.AddChild<RightWorldView>();
```

`XGUISystem.Roots` contains every registered root. `XGUISystem.Panel` remains the default root for existing code and prefers the screen-space root, so adding a world root does not move screen UI onto the newest world panel.

World panels are pixel-perfect by default: keep `PanelSize` fixed and leave `RenderScale` at `1.0`. Change `RenderScale` explicitly when the panel needs a different physical scale. Screen panels use `XGUIRootComponent.UseDesktopScale` by default, following s&box's current desktop/DPI scale; disable it when a fixed screen scale is wanted.

### Shader-backed panels

World panels still use the normal XGUI panel render tree, including custom `Panel.DrawBackground` and `Panel.DrawContent` implementations. A panel can call `Graphics.DrawQuad` with a material created by `Material.FromShader` and supply its shader attributes through `RenderAttributes`. The s&box world-panel renderer supplies the world transform for those custom draws, so effects such as the existing `UI.FluidGlass.shader` remain usable in world space.

Use `PanelSize` for the shader panel's logical coordinate space and `RenderScale` when the shader surface needs a higher or lower physical render resolution. The world-panel component itself does not need a replacement material for this style of effect.
