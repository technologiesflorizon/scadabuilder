namespace ScadaBuilderV2.ElementStudio.App;

public partial class App : System.Windows.Application
{
    private void OnStartup(object sender, System.Windows.StartupEventArgs e)
    {
        var packagePath = e.Args.FirstOrDefault(argument =>
            argument.EndsWith(".ft1", StringComparison.OrdinalIgnoreCase))
            ?? e.Args.FirstOrDefault();

        try
        {
            var window = new MainWindow(packagePath);
            window.Show();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Erreur au demarrage de Studio Element+:\n\n{exception.GetType().Name}: {exception.Message}\n\n" +
                $"Package: {packagePath ?? "(aucun)"}\n\n" +
                $"Le Studio ne peut pas s'ouvrir. Verifiez le fichier .ft1 et reessayez.",
                "Studio Element+ - Erreur",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
