using Tansu.Forms.Sample;

if (args.Contains("--verify"))
    FormsApp.Start(() => Environment.ExitCode = Verification.Run());
else
    FormsApp.Run(() => args.Contains("--automation-sample") ? new AutomationSampleForm()
        : args.Contains("--gallery") ? new ComponentGalleryForm() : new SampleForm());
