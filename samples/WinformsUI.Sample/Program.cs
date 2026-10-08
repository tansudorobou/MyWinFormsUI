namespace WinformsUI.Sample;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--verify"))
        {
            Environment.ExitCode = Verification.Run();
            return;
        }
        Application.Run(new SampleForm());
    }
}
