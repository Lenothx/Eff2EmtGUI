using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Eff2EmtGUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textEQFolder.Text = Properties.Settings.Default.EQFolder;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.EQFolder = textEQFolder.Text;

            Properties.Settings.Default.Save();
        }

        private void CheckIfReady()
        {
            labelZonesSelected.Text = listZoneEffs.CheckedItems.Count.ToString();
            labelZoneCount.Text = listZoneEffs.Items.Count.ToString();

            buttonConvert.Enabled = (listZoneEffs.CheckedItems.Count > 0);
        }

        private void CheckZoneList()
        {
            listZoneEffs.Items.Clear();
            var directoryPath = textEQFolder.Text;
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                PopulateZoneListFromDirectory(textEQFolder.Text);
            }

            CheckIfReady();
        }

        private void PopulateZoneListFromDirectory(string directoryPath)
        {
            var zoneFiles = new DirectoryInfo(directoryPath)
                .GetFiles("*_sounds.eff")
                .Select(file => Path.GetFileNameWithoutExtension(file.Name)
                .Replace("_sounds", ""));
            foreach (var zoneName in zoneFiles)
            {
                listZoneEffs.Items.Add(zoneName);
            }
        }

        private void TextEQFolder_TextChanged(object sender, EventArgs e)
        {
            CheckZoneList();
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length == 0) return;

            SetEQFolder(files[0]);
            CheckZoneList();
            CheckZoneEffs(files);
        }

        private void SetEQFolder(string filePath)
        {
            textEQFolder.Text = Path.GetDirectoryName(filePath);
        }

        private void CheckZoneEffs(string[] files)
        {
            foreach (string filePath in files)
            {
                FileInfo file = new FileInfo(filePath);
                if (!file.Exists) continue;

                CheckMatchingZones(file.Name);
            }
        }

        private void CheckMatchingZones(string fileName)
        {
            for (int index = 0; index < listZoneEffs.Items.Count; index++)
            {
                if (fileName.StartsWith(listZoneEffs.Items[index].Text, StringComparison.CurrentCultureIgnoreCase))
                {
                    listZoneEffs.Items[index].Checked = true;
                    listZoneEffs.EnsureVisible(index);
                }
            }
        }

        private void ButtonBrowseEQFolder_Click(object sender, EventArgs e)
        {
            dialogEQFolder.SelectedPath = textEQFolder.Text;

            if (dialogEQFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textEQFolder.Text = dialogEQFolder.SelectedPath;

                CheckZoneList();
            }
        }

        private void SetFormEnabled(bool YesNo)
        {
            textEQFolder.Enabled = YesNo;
            buttonBrowseEQFolder.Enabled = YesNo;
            listZoneEffs.Enabled = YesNo;
            buttonConvert.Text = YesNo ? "Convert!" : "Abort";
        }

        private void ButtonConvert_Click(object sender, EventArgs e)
        {
            switch (buttonConvert.Text)
            {
                case "Convert!":
                    SetFormEnabled(false);

                    progressConversion.Value = 0;
                    progressConversion.Maximum = listZoneEffs.CheckedItems.Count;

                    List<string> _selectedZones = new List<string>(listZoneEffs.CheckedItems.Count);

                    foreach (ListViewItem _zone in listZoneEffs.CheckedItems)
                    {
                        _selectedZones.Add(_zone.Text);
                    }

                    threadConverter.RunWorkerAsync(_selectedZones);
                    break;

                case "Abort":
                    threadConverter.CancelAsync();
                    break;
            }
        }

        private void ListZoneEffs_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            CheckIfReady();
        }

        private void ThreadConverter_DoWork(object sender, DoWorkEventArgs e)
        {
            List<string> _zoneList = e.Argument as List<string>;
            int _zonesConverted = 0;
            e.Result = DialogResult.OK;

            foreach (string _zoneNick in _zoneList)
            {
                switch (Eff2EmtConverter.ConvertZone(textEQFolder.Text, _zoneNick))
                {
                    case DialogResult.OK:
                        threadConverter.ReportProgress(++_zonesConverted);
                        break;

                    case DialogResult.Abort:
                        e.Result = DialogResult.Abort;
                        return;

                    case DialogResult.Ignore:
                        break;
                }

                if (threadConverter.CancellationPending)
                {
                    e.Result = DialogResult.Cancel;
                    return;
                }
            }
        }

        private void ThreadConverter_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressConversion.Value = e.ProgressPercentage;

            if (listZoneEffs.CheckedItems.Count < 4 && File.Exists(GetEmtFilename(e.ProgressPercentage)))
            {
                string emtFilename = GetEmtFilename(e.ProgressPercentage);
                OpenFileInEditor(emtFilename);
            }
        }

        private string GetEmtFilename(int progressPercentage)
        {
            string zoneName = listZoneEffs.CheckedItems[progressPercentage - 1].Text;
            return Path.Combine(textEQFolder.Text, $"{zoneName}.emt");
        }

        private static void OpenFileInEditor(string filename)
        {
            foreach (string editorPath in TextEditors.Where(File.Exists))
            {
                System.Diagnostics.Process.Start(editorPath, filename);
                break; // Open with the first available editor.
            }
        }

        // Can be customized with any available text editors. Will be checked from top to bottom, and the first one found will be execute for an .emt file.
        private static readonly List<string> TextEditors = new List<string>()
        {
            @"C:\Program Files (x86)\Notepad++\notepad++.exe",
            @"C:\Program Files\Notepad++\notepad++.exe",
            @"C:\Windows\notepad.exe"
        };

        private void ThreadConverter_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetFormEnabled(true);

            switch ((DialogResult)e.Result)
            {
                case DialogResult.OK:
                    MessageBox.Show("Conversion process completed.", "Conversion Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;

                case DialogResult.Abort:
                    MessageBox.Show("Conversion process aborted.", "Conversion Status", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;

                case DialogResult.Cancel:
                    MessageBox.Show("Conversion process cancelled.", "Conversion Status", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
            }
        }
    }
}