namespace Tansu.Forms.Sample;

internal static class InputRangeVerification
{
    internal static void Run(ICollection<string> results)
    {
        using var ip = Field.IpAddress("設備IP");
        foreach (string value in new[] { "", "192.168.1.10", "0.0.0.0", "255.255.255.255", "::1", "2001:db8::1", "::ffff:192.168.1.10", " 192.168.1.10 " })
        { ip.Value = value; Check(ip.ValidateValue(), $"Valid IP: {value}"); }
        foreach (string value in new[] { "256.1.1.1", "127.1", "2130706433", "0x7f000001", "192.168.001.1", "1.2.3", "1.2.3.4.5", "device.local", "[::1]", "192.168.1.1:80", "2001:db8::1/64", "fe80:::1" })
        { ip.Value = value; Check(!ip.ValidateValue() && ip.ErrorMessage is not null, $"Invalid IP: {value}"); }
        using var v4 = Field.Text("IPv4").IpAddress(IpAddressVersion.IPv4, "IPv4のみ").Required();
        v4.Value = "::1"; Check(!v4.ValidateValue() && v4.ErrorMessage == "IPv4のみ", "Version/custom message validation");
        v4.Value = ""; Check(!v4.ValidateValue(), "Required rejects blank IP");
        v4.Value = "10.0.0.1"; Check(v4.ValidateValue(), "IPv4-only accepts dotted addresses");
        using var v6 = Field.IpAddress("IPv6", "::1", IpAddressVersion.IPv6);
        Check(v6.ValidateValue(), "IPv6-only accepts IPv6"); v6.Value = "10.0.0.1"; Check(!v6.ValidateValue(), "IPv6-only rejects IPv4");
        results.Add("PASS: IP literal validation, strict IPv4, IPv6/mapped IPv4, family restriction, optional/required input and custom errors");

        DateTime start = new(2026, 10, 1), end = new(2026, 10, 5);
        using var range = Field.DateRange("期間", start, end);
        var editor = (DateRangeEditor)range.Editor;
        Check(range.ValidateValue() && range.Value.Start == start && range.Value.End == end, "Typed start/end values");
        Check(range.Value.Contains(start) && range.Value.Contains(end.AddHours(23)) && !range.Value.Contains(end.AddDays(1)), "Inclusive date containment ignores time");
        int changes = 0; range.ValueChanged += (_, _) => changes++;
        range.Value = new(start.AddHours(12), end.AddHours(23)); Check(changes == 0, "Unchanged calendar dates do not notify");
        range.Value = new(start.AddDays(1), end.AddDays(1)); Check(changes == 1, "Range assignment notifies once after both dates are updated");
        editor.StartPicker.Value = start.AddDays(10); Check(changes == 2 && !range.ValidateValue(), "Native edits notify and reversed ranges fail validation");
        editor.EndPicker.Value = start.AddDays(10); Check(range.ValidateValue(), "Same-day ranges are valid");
        DateRange retained = range.Value;
        try { range.Value = new(start, DateTime.MaxValue); throw new InvalidOperationException("Expected bounds validation"); }
        catch (ArgumentOutOfRangeException) { Check(range.Value == retained && changes == 3, "Invalid bounds do not partially update either date"); }
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        host.Controls.Add(range); host.Show(); Application.DoEvents();
        foreach (int width in new[] { 220, 480 })
        {
            int pixels = (int)Math.Round(width * editor.DeviceDpi / 96d);
            range.Size = range.GetPreferredSize(new Size(pixels, 0)); range.PerformLayout(); editor.PerformLayout();
            Check(editor.StartPicker.Bounds.Right <= editor.ClientSize.Width && editor.EndPicker.Bounds.Right <= editor.ClientSize.Width
                && editor.EndPicker.Bottom <= editor.ClientSize.Height, "Responsive range pickers fit within the editor");
            Check(width == 220 ? editor.EndPicker.Top > editor.StartPicker.Bottom : editor.EndPicker.Top == editor.StartPicker.Top, "Narrow ranges stack; wide ranges share a row");
        }
        host.Close();
        results.Add("PASS: DateRange typed/inclusive dates, order validation, native notifications, atomic writes, bounds checking and responsive native pickers");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
