using System.ComponentModel;

namespace WinformsUI;

public sealed class AspectRatioPanel : Panel
{
    private double _ratio;
    public AspectRatioPanel(Control content, double ratio = 16d / 9)
    {
        Ratio = ratio; Controls.Add(content); content.Dock = DockStyle.None; Margin = Padding.Empty;
    }
    [DefaultValue(16d / 9)]
    public double Ratio
    {
        get => _ratio;
        set { if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); _ratio = value; PerformLayout(); LayoutInvalidation.NotifyAncestors(this); }
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(Width, 320);
        return new Size(width, (int)Math.Ceiling(width / _ratio));
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_ratio <= 0) return;
        int width = Math.Min(ClientSize.Width, (int)Math.Floor(ClientSize.Height * _ratio));
        int height = Math.Min(ClientSize.Height, (int)Math.Floor(width / _ratio));
        foreach (Control child in Controls) child.SetBounds((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
    }
}
