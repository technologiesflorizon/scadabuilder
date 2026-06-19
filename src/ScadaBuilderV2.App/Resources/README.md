# SCADA Builder V2 Icons

This folder contains the WPF icon resource dictionary for the V2 shell.

## Scope

`Icons.xaml` defines stable `DrawingImage` resources for the commands currently visible in the shell. The public registry is the semantic key family, not the drawing geometry:

- `Icon.Project.New`
- `Icon.Project.Open`
- `Icon.Project.Save`
- `Icon.Import.Legacy`
- `Icon.Import.Tags`
- `Icon.Build.FT100`
- `Icon.Export.Folder`
- `Icon.Export.Package`
- `Icon.Edit.Undo`
- `Icon.Edit.Redo`
- `Icon.Edit.Copy`
- `Icon.Edit.Paste`
- `Icon.Tool.Select`
- `Icon.Tool.Move`
- `Icon.Tool.Text`
- `Icon.Tool.Image`
- `Icon.Tool.Group`
- `Icon.Tool.Zoom`
- `Icon.Tool.Settings`
- `Icon.Selection.Lock`
- `Icon.Selection.Group`
- `Icon.Selection.Ungroup`
- `Icon.Object.Lock`
- `Icon.Panel.Restore`
- `Icon.Layer.Forward`
- `Icon.Layer.Backward`
- `Icon.View.Desktop`
- `Icon.View.Tablet`
- `Icon.View.Mobile`
- `Icon.View.Rotate`
- `Icon.View.Measure`
- `Icon.Field.Numeric`
- `Icon.Shape.Rectangle`
- `Icon.Shape.Ellipse`
- `Icon.Shape.Line`
- `Icon.Shape.Arrow`
- `Icon.Hmi.*`
- `Icon.Button.*`

## Source And License

The icons are internal original vector primitives authored for SCADA Builder V2. They are not copied from Lucide, Fluent UI, Material Symbols, Bootstrap Icons, or another external icon set.

No NuGet package or third-party icon dependency is introduced by this resource dictionary.

## Usage Notes

The public contract is the semantic resource key, not the visual metaphor. Future UI wiring should reference keys such as `Icon.Project.Save` instead of file names or library-specific names.

The icons use a shared `Icon.OutlinePen` and `Icon.StrokeBrush` inside the dictionary. When the dictionary is integrated into the application resource tree, those shared resources can be replaced or bridged to the application theme brushes if needed.

Every command exposed in the top ribbon should use one of these keys or add a new semantic key before the control is made visible. Temporary text glyphs such as `BTN`, `LED`, `TNK`, or `123` should stay out of command surfaces because they do not scale consistently with the shell icon language.
