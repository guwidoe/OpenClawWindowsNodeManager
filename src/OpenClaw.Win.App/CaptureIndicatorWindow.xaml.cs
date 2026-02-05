using System.Windows;

namespace OpenClaw.Win.App;

public partial class CaptureIndicatorWindow : Window
{
    public CaptureIndicatorWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionWindow();
    }

    public void SetText(string text)
    {
        IndicatorText.Text = text;
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Top + 20;
    }
}
