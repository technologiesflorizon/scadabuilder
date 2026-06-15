# SCADA Builder V2 Icons

This folder contains the first WPF icon resource dictionary for the V2 shell.

## Scope

`Icons.xaml` defines stable `DrawingImage` resources for the commands currently visible in the shell:

- `Icon.Project.New`
- `Icon.Project.Open`
- `Icon.Project.Save`
- `Icon.Import.Legacy`
- `Icon.Build.FT100`
- `Icon.Edit.Undo`
- `Icon.Edit.Redo`
- `Icon.Tool.Select`
- `Icon.Tool.Move`
- `Icon.Tool.Text`
- `Icon.Tool.Image`
- `Icon.Tool.Group`
- `Icon.Tool.Zoom`
- `Icon.Selection.Lock`
- `Icon.Object.Lock`
- `Icon.Panel.Restore`

## Source And License

The icons are internal original vector primitives authored for SCADA Builder V2. They are not copied from Lucide, Fluent UI, Material Symbols, Bootstrap Icons, or another external icon set.

No NuGet package or third-party icon dependency is introduced by this resource dictionary.

## Usage Notes

The public contract is the semantic resource key, not the visual metaphor. Future UI wiring should reference keys such as `Icon.Project.Save` instead of file names or library-specific names.

The icons use a shared `Icon.OutlinePen` and `Icon.StrokeBrush` inside the dictionary. When the dictionary is integrated into the application resource tree, those shared resources can be replaced or bridged to the application theme brushes if needed.
