using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace RecipeItemCreator.Controls;

/// <summary>
/// Kompaktes einzeiliges Eingabefeld für das Dark-Theme.
/// Der eigentliche TextBox-Editor wird innerhalb des Controls vertikal zentriert.
/// Das Control kann normal im WinForms-Designer verwendet werden.
/// </summary>
[DefaultEvent(nameof(TextChanged))]
public sealed class DarkTextBox : UserControl
{
    private readonly TextBox _editor;
    private Color _borderColor = Color.FromArgb(78, 82, 91);
    private Color _focusedBorderColor = Color.FromArgb(102, 108, 120);

    public DarkTextBox()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        BackColor = Color.FromArgb(43, 46, 52);
        ForeColor = Color.FromArgb(236, 238, 241);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        Size = new Size(150, 28);
        MinimumSize = new Size(40, 24);
        Cursor = Cursors.IBeam;

        _editor = new TextBox
        {
            AutoSize = true,
            BorderStyle = BorderStyle.None,
            BackColor = BackColor,
            ForeColor = ForeColor,
            Font = Font,
            Margin = Padding.Empty
        };

        _editor.TextChanged += (_, e) => OnTextChanged(e);
        _editor.Enter += (_, _) => Invalidate();
        _editor.Leave += (_, _) => Invalidate();

        Controls.Add(_editor);
        LayoutEditor();
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "78, 82, 91")]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "102, 108, 120")]
    public Color FocusedBorderColor
    {
        get => _focusedBorderColor;
        set
        {
            _focusedBorderColor = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue("")]
    public string? PlaceholderText
    {
        get => _editor.PlaceholderText;
        set => _editor.PlaceholderText = value ?? string.Empty;
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _editor.ReadOnly;
        set => _editor.ReadOnly = value;
    }

    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [AllowNull]
    public override string Text
    {
        get => _editor.Text;
        set => _editor.Text = value ?? string.Empty;
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        _editor?.BackColor = BackColor;
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        _editor?.ForeColor = ForeColor;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_editor is not null)
        {
            _editor.Font = Font;
            LayoutEditor();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutEditor();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.None;
        Color color = _editor.Focused ? FocusedBorderColor : BorderColor;
        using var pen = new Pen(color);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _editor.Focus();
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        _editor.Focus();
    }

    private void LayoutEditor()
    {
        if (_editor is null)
            return;

        const int horizontalPadding = 7;
        int preferredHeight = _editor.PreferredHeight;
        int y = Math.Max(1, (ClientSize.Height - preferredHeight) / 2);

        _editor.Location = new Point(horizontalPadding, y);
        _editor.Width = Math.Max(1, ClientSize.Width - horizontalPadding * 2);
    }
}
