using System.ComponentModel;

namespace WinformsUI;

public sealed class Avatar : UserControl
{
    private readonly PictureBox _picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label _fallback = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private Image? _ownedImage;
    public Avatar(string fallback, Image? image = null)
    {
        Size = new Size(48, 48); Controls.AddRange([_fallback, _picture]);
        Fallback = fallback; Image = image; AccessibleName = fallback;
    }
    [DefaultValue("")]
    public string Fallback { get => _fallback.Text; set => _fallback.Text = value; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image
    {
        get => _picture.Image;
        set { _picture.Image = value; _picture.Visible = value is not null; _fallback.Visible = value is null; }
    }
    public bool LoadImage(string path)
    {
        try
        {
            using var loaded = System.Drawing.Image.FromFile(path);
            var copy = new Bitmap(loaded);
            var previous = _ownedImage; _ownedImage = copy; Image = copy; previous?.Dispose();
            return true;
        }
        catch (Exception error) when (error is IOException or ArgumentException or OutOfMemoryException)
        { Image = null; return false; }
    }
    protected override void Dispose(bool disposing) { if (disposing) _ownedImage?.Dispose(); base.Dispose(disposing); }
}
