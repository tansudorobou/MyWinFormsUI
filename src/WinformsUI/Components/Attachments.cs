namespace WinformsUI;

public enum AttachmentState { Idle, Uploading, Processing, Error, Done }

public sealed class AttachmentView : UserControl
{
    private readonly Label _title = new() { AutoSize = true };
    private readonly Label _description = new() { AutoSize = false };
    private readonly PictureBox _preview = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(96, 72) };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100 };
    private readonly StackPanel _body;
    private Image? _ownedPreview;
    public string FileName => _title.Text;
    public long FileSize { get; }
    public AttachmentState State { get; private set; }
    public int Progress => _progress.Value;
    public Button OpenButton { get; }
    public Button RemoveButton { get; }
    public event EventHandler? OpenRequested;
    public event EventHandler? RemoveRequested;

    public AttachmentView(string fileName, long fileSize = 0, Image? preview = null, Action? open = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileSize);
        _title.Text = fileName; FileSize = fileSize; _preview.Image = preview; _preview.Visible = preview is not null;
        OpenButton = Ui.Button("表示", () =>
        {
            if (open is not null) open();
            else if (_preview.Image is not null) ShowImagePreview();
            OpenRequested?.Invoke(this, EventArgs.Empty);
        });
        OpenButton.Enabled = open is not null || preview is not null;
        RemoveButton = Ui.Button("削除", () => { RemoveRequested?.Invoke(this, EventArgs.Empty); Dispose(); });
        _body = WinformsUI.Layout.Stack(_title, _description, _preview, _progress, WinformsUI.Layout.Row(OpenButton, RemoveButton));
        _body.Gap = 4; _body.Dock = DockStyle.Fill; Controls.Add(_body); Margin = Padding.Empty;
        SetState(AttachmentState.Done);
    }
    public static AttachmentView FromFile(string path, Action? open = null)
    {
        var file = new FileInfo(path);
        Image? preview = null;
        if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" }.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
        {
            try { using var source = Image.FromFile(path); preview = new Bitmap(source); }
            catch (Exception error) when (error is IOException or ArgumentException or OutOfMemoryException) { }
        }
        Action? openFile = open ?? (preview is null ? () => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(file.FullName) { UseShellExecute = true }) : null);
        var view = new AttachmentView(file.Name, file.Length, preview, openFile) { _ownedPreview = preview };
        return view;
    }
    public void SetState(AttachmentState state, int progress = 0, string? error = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(progress); ArgumentOutOfRangeException.ThrowIfGreaterThan(progress, 100);
        State = state; _progress.Value = progress;
        _progress.Visible = state is AttachmentState.Uploading or AttachmentState.Processing;
        _progress.Style = state == AttachmentState.Processing ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        _description.Text = state switch
        {
            AttachmentState.Uploading => $"送信中 {progress}%", AttachmentState.Processing => "処理中",
            AttachmentState.Error => error ?? "処理に失敗しました。", AttachmentState.Idle => "待機中",
            _ => $"完了 · {FileSize:N0} bytes"
        };
        _description.WithRole(state == AttachmentState.Error ? "danger" : "muted");
        LayoutInvalidation.NotifyAncestors(this);
    }
    private void ShowImagePreview()
    {
        using var form = new Form { Text = FileName, ClientSize = new Size(640, 480), StartPosition = FormStartPosition.CenterParent };
        form.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = _preview.Image, SizeMode = PictureBoxSizeMode.Zoom });
        form.ShowDialog(FindForm());
    }
    public override Size GetPreferredSize(Size proposedSize) => _body is null ? base.GetPreferredSize(proposedSize) : _body.GetPreferredSize(proposedSize);
    protected override void Dispose(bool disposing) { if (disposing) _ownedPreview?.Dispose(); base.Dispose(disposing); }
}

/// <summary>A file-selection list; uploads remain application callbacks rather than hidden network work.</summary>
public sealed class AttachmentList : UserControl
{
    private readonly StackPanel _files = WinformsUI.Layout.Stack();
    private readonly StackPanel _body;
    public Button AddButton { get; }
    public event EventHandler? FilesChanged;
    public IReadOnlyList<AttachmentView> Files => _files.Controls.OfType<AttachmentView>().ToArray();
    public AttachmentList()
    {
        AddButton = Ui.Button("ファイルを追加", () =>
        {
            using var picker = new OpenFileDialog { Multiselect = true };
            if (picker.ShowDialog(FindForm()) == DialogResult.OK) AddFiles(picker.FileNames);
        });
        _body = WinformsUI.Layout.Stack(AddButton, _files); _body.Dock = DockStyle.Fill;
        Controls.Add(_body); Margin = Padding.Empty;
    }
    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            var attachment = AttachmentView.FromFile(path);
            attachment.Disposed += (_, _) => FilesChanged?.Invoke(this, EventArgs.Empty);
            _files.Add(attachment);
        }
        FilesChanged?.Invoke(this, EventArgs.Empty); LayoutInvalidation.NotifyAncestors(this);
    }
    public override Size GetPreferredSize(Size proposedSize) => _body is null ? base.GetPreferredSize(proposedSize) : _body.GetPreferredSize(proposedSize);
}
