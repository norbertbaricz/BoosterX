namespace BoosterX.App.Controls;

/// <summary>Application messages and confirmations share the same dark, rounded window and explicit button colors.</summary>
internal sealed class DarkDialog : DarkForm
{
    private readonly DialogResult cancelResult;
    private DarkDialog(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        Text = title; AccessibleDescription = icon.ToString();
        ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(590, 360); MinimumSize = new Size(420, 240); Padding = new Padding(20);
        var layout = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var header = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 14) };
        header.ColumnStyles.Add(new(SizeType.AutoSize));
        header.ColumnStyles.Add(new(SizeType.Percent, 100)); header.ColumnStyles.Add(new(SizeType.AutoSize));
        var statusIcon = new VectorIcon { Size = new Size(26, 26), Margin = new Padding(0, 4, 10, 0),
            Symbol = icon == MessageBoxIcon.Error ? UiIcon.Error : icon == MessageBoxIcon.Warning ? UiIcon.Warning
                : icon == MessageBoxIcon.Question ? UiIcon.Shield : UiIcon.Info,
            ForeColor = icon == MessageBoxIcon.Error ? UiPalette.Error : icon == MessageBoxIcon.Warning ? UiPalette.Warning : UiPalette.Muted,
            AccessibleName = icon.ToString(), Visible = icon != MessageBoxIcon.None };
        var heading = new Label { Text = title, AutoSize = true, Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), ForeColor = UiPalette.Text, Padding = new Padding(0, 8, 8, 0) };
        var close = new RoundedButton { Glyph = CaptionGlyph.Close, Size = new Size(34, 34), AccessibleName = "Close dialog", Margin = new Padding(0) };
        header.Controls.Add(statusIcon, 0, 0); header.Controls.Add(heading, 1, 0); header.Controls.Add(close, 2, 0);
        var text = new DarkTextView { Text = message, ReadOnly = true, Dock = DockStyle.Fill,
            ForeColor = UiPalette.Text, BackColor = UiPalette.Background, Font = Font, DetectUrls = false, Margin = new Padding(0, 0, 0, 18) };
        var footer = new BufferedFlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Margin = new Padding(0) };
        (string Title, DialogResult Result)[] choices = buttons switch
        {
            MessageBoxButtons.OKCancel => [("Cancel", DialogResult.Cancel), ("OK", DialogResult.OK)],
            MessageBoxButtons.YesNo => [("No", DialogResult.No), ("Yes", DialogResult.Yes)],
            _ => [("OK", DialogResult.OK)]
        };
        cancelResult = choices[0].Result;
        var controls = new List<RoundedButton>();
        foreach (var choice in choices)
        {
            var button = new RoundedButton { Text = choice.Title, DialogResult = choice.Result, AutoSize = true,
                Symbol = choice.Result is DialogResult.OK or DialogResult.Yes ? UiIcon.Check : UiIcon.Clear,
                MinimumSize = new Size(100, 38), Padding = new Padding(14, 7, 14, 7), Margin = new Padding(8, 0, 0, 0),
                Active = choice.Result is DialogResult.OK or DialogResult.Yes };
            controls.Add(button); footer.Controls.Add(button);
        }
        CancelButton = controls[0];
        AcceptButton = defaultButton == MessageBoxDefaultButton.Button2 && controls.Count > 1 ? controls[0] : controls[^1];
        close.Click += (_, _) => { DialogResult = cancelResult; Close(); };
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(text, 0, 1); layout.Controls.Add(footer, 0, 2); Controls.Add(layout);
        AttachTitleDrag(header, heading, statusIcon);
        void FitDialog()
        {
            Rectangle available = ResponsiveGeometry.Available(Screen.FromControl(this).WorkingArea, DeviceDpi);
            Size target = new(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
            MinimumSize = new Size(Math.Min(LogicalToDeviceUnits(360), target.Width), Math.Min(LogicalToDeviceUnits(220), target.Height));
            Bounds = new Rectangle(Math.Clamp(Left, available.Left, available.Right - target.Width),
                Math.Clamp(Top, available.Top, available.Bottom - target.Height), target.Width, target.Height);
            bool compact = ResponsiveGeometry.Logical(ClientSize.Width, DeviceDpi) < 500
                || ResponsiveGeometry.Logical(ClientSize.Height, DeviceDpi) < 320;
            Padding = new Padding(LogicalToDeviceUnits(compact ? UiLayout.CompactPadding : 20));
            close.Size = new Size(LogicalToDeviceUnits(compact ? 28 : 34), LogicalToDeviceUnits(compact ? 28 : 34));
            heading.AutoEllipsis = true;
            heading.MaximumSize = new Size(Math.Max(1, ClientSize.Width - Padding.Horizontal - close.Width
                - (icon != MessageBoxIcon.None ? statusIcon.Width + statusIcon.Margin.Horizontal : 0)), heading.Font.Height * 2 + heading.Padding.Vertical);
            header.Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(compact ? 8 : 14));
            text.Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(compact ? 8 : 18));
            foreach (RoundedButton button in controls)
            {
                button.MinimumSize = Size.Empty;
                button.Padding = new Padding(LogicalToDeviceUnits(compact ? 10 : 14), LogicalToDeviceUnits(7), LogicalToDeviceUnits(compact ? 10 : 14), LogicalToDeviceUnits(7));
                button.MinimumSize = button.GetPreferredSize(Size.Empty);
                button.Margin = new Padding(LogicalToDeviceUnits(compact ? 4 : 8), 0, 0, 0);
            }
        }
        Load += (_, _) => FitDialog();
        ResizeEnd += (_, _) => FitDialog();
        DpiChanged += (_, _) => BeginInvoke((Action)(() => { if (!IsDisposed && !Disposing) FitDialog(); }));
        Shown += (_, _) =>
        {
            AcceptButton?.NotifyDefault(true);
            if (AcceptButton is Control selected) selected.Focus();
        };
    }

    internal static DialogResult Show(IWin32Window? owner, string message, string title, MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        using var dialog = new DarkDialog(message, title, buttons, icon, defaultButton);
        return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
    internal static DialogResult Show(string message, string title, MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information) => Show(null, message, title, buttons, icon);
    protected override void OnFormClosing(FormClosingEventArgs e) { if (DialogResult == DialogResult.None) DialogResult = cancelResult; base.OnFormClosing(e); }
}
