using System.Windows;

namespace WinqEmuLauncher
{
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; } = "";

        public InputDialog(string title, string prompt, string defaultText = "")
        {
            InitializeComponent();
            Title = title;
            txtPrompt.Text = prompt;
            txtInput.Text = defaultText;
        }

        void btnOk_Click(object sender, RoutedEventArgs e)
        {
            InputText = txtInput.Text.Trim();
            DialogResult = true;
        }

        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
