namespace ScadaBuilderV2.ElementStudio.App;

public partial class App : System.Windows.Application
{
    private void OnStartup(object sender, System.Windows.StartupEventArgs e)
    {
        var packagePath = e.Args.FirstOrDefault(argument =>
            argument.EndsWith(".ft1", StringComparison.OrdinalIgnoreCase))
            ?? e.Args.FirstOrDefault();

        var window = new MainWindow(packagePath);
        window.Show();
    }
}
