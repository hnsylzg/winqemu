using System.Windows;

namespace WinqEmuLauncher
{
    public partial class MsgDialog : Window
    {
        public MsgDialog(string title, string message, bool isConfirm = false)
        {
            InitializeComponent();
            Title = title;
            txtMsg.Text = message;
            btnCancel.Visibility = isConfirm ? Visibility.Visible : Visibility.Collapsed;
        }

        // 信息/错误提示：只显示“确定”
        public static bool? Info(Window owner, string title, string msg)
        {
            var d = new MsgDialog(title, msg, false);
            d.Owner = owner;
            return d.ShowDialog();
        }

        // 确认框：显示“确定 / 取消”，返回 true=确定
        public static bool? Confirm(Window owner, string title, string msg)
        {
            var d = new MsgDialog(title, msg, true);
            d.Owner = owner;
            return d.ShowDialog();
        }

        void btnOk_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        void btnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
