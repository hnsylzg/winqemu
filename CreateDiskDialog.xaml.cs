using System;
using System.Windows;
using Microsoft.Win32;

namespace WinqEmuLauncher
{
    public partial class CreateDiskDialog : Window
    {
        public string DiskPath { get; private set; } = "";
        public int SizeGB { get; private set; } = 20;
        public string Format { get; private set; } = "qcow2";
        public string DefaultDirectory { get; set; } = "";

        public CreateDiskDialog()
        {
            InitializeComponent();
        }

        void btnPath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = Lang.T("CdSaveFilter"),
                FileName = "disk.qcow2",
                InitialDirectory = DefaultDirectory
            };
            if (dlg.ShowDialog() == true) txtPath.Text = dlg.FileName;
        }

        void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPath.Text))
            {
                MessageBox.Show(Lang.T("NeedPath"));
                return;
            }
            if (!int.TryParse(txtSize.Text, out int sz) || sz < 1)
            {
                MessageBox.Show(Lang.T("NeedSize"));
                return;
            }
            DiskPath = txtPath.Text.Trim();
            SizeGB = sz;
            Format = (cmbFormat.SelectedIndex == 1) ? "raw" : "qcow2";
            DialogResult = true;
        }

        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
