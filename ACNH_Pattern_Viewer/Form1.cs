using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace ACNH_Pattern_Viewer
{
    public partial class ACNH : Form
    {
        private bool keepOriginalFilenames = false;
        private bool scanSubfolders = false;

        private PictureBox pictureBox;
        private Button loadButton;

        public ACNH()
        {
            Text = "ACNH Pattern Converter";
            ClientSize = new Size(320, 320);
            this.Icon = new Icon(GetType().Assembly.GetManifestResourceStream("ACNH_Pattern_Viewer.DeviceIconMyDesign^w.ico"));

            LoadSettings();

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.Dock = DockStyle.Top;
            buttonPanel.FlowDirection = FlowDirection.LeftToRight;
            buttonPanel.WrapContents = false;

            Button selectSaveFileButton = new Button { Text = "Select Save File", Width = (int)(ClientSize.Width * 0.25) };
            selectSaveFileButton.Click += SelectSaveFileButton_Click;

            loadButton = new Button { Text = "Select NHD Folder", Width = (int)(ClientSize.Width * 0.44) };
            loadButton.Click += LoadButton_Click;

            Button settingsButton = new Button { Text = "Settings", Width = (int)(ClientSize.Width * 0.25) };
            settingsButton.Click += SettingsButton_Click;

            buttonPanel.Controls.Add(loadButton);
            buttonPanel.Controls.Add(settingsButton);

            pictureBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };

            //buttonPanel.Controls.Add(selectSaveFileButton); // TEMPORARILY REMOVED, SAVE IS ENCRYPTED
            buttonPanel.Controls.Add(loadButton);
            buttonPanel.Controls.Add(settingsButton);
            Controls.Add(pictureBox);
            Controls.Add(buttonPanel);
        }
        private void SelectSaveFileButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                openFileDialog.Filter = "DAT files (*.dat)|*.dat";
                openFileDialog.Title = "Select ACNH main.dat Save File";

                if (openFileDialog.ShowDialog() == DialogResult.OK) {
                    string filePath = openFileDialog.FileName;
                    string outputFolderPath = Path.Combine(Path.GetDirectoryName(filePath), "output");
                    Console.WriteLine(outputFolderPath);

                    if (!filePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)) {
                        MessageBox.Show("Not a .dat file", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    byte[] data = File.ReadAllBytes(filePath);
                    long baseOffset = 0x1E3968;

                    for (int i = 0; i < 200; i++) {
                        long offset = baseOffset + i * 0x8A8;
                        if (offset + 0x77 >= data.Length)
                            break;

                        byte patternType = data[offset + 0x77];
                        int length = (patternType == 0) ? 680 : 2216;

                        if (offset + length > data.Length)
                            break;

                        byte[] patternData = new byte[length];
                        Array.Copy(data, offset, patternData, 0, length);

                        int imageSize = (patternType == 0) ? 512 : 2048;
                        processImage(false, patternData, imageSize, outputFolderPath);
                    }
                }
            }
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog {
                Description = "Select Folder Containing .nhd or .nhpd Files"
            };

            if (folderDialog.ShowDialog() == DialogResult.OK) {
                string folderPath = folderDialog.SelectedPath;
                string outputFolderPath = Path.Combine(folderPath, "output");

                if (!Directory.Exists(outputFolderPath)) {
                    Directory.CreateDirectory(outputFolderPath);
                }

                string[] files;

                if (scanSubfolders) {
                    files = Directory.GetFiles(folderPath, "*.nhd", SearchOption.AllDirectories).Concat(Directory.GetFiles(folderPath, "*.nhpd", SearchOption.AllDirectories)).ToArray();
                }
                else {
                    files = Directory.GetFiles(folderPath, "*.nhd").Concat(Directory.GetFiles(folderPath, "*.nhpd")).ToArray();
                }

                foreach (var file in files) {
                    byte[] fileData = File.ReadAllBytes(file);
                    int imageSize = (file.EndsWith(".nhd")) ? 512 : 2048;
                    string originalFilename = Path.GetFileNameWithoutExtension(file);
                    processImage(true, fileData, imageSize, outputFolderPath, originalFilename);
                }
            }
        }
        private void processImage(bool fromNHD, byte[] patternData, int imageSize, string outputFolderPath, string originalFilename = "")
        {
            byte[] patternNameBytes = new byte[40];
            Array.Copy(patternData, 0x10, patternNameBytes, 0, 40);
            string patternName = Encoding.Unicode.GetString(patternNameBytes).TrimEnd('\0');

            byte[] playerNameBytes = new byte[20];
            Array.Copy(patternData, 0x58, playerNameBytes, 0, 20);
            string playerName = Encoding.Unicode.GetString(playerNameBytes).TrimEnd('\0');

            string filename = patternName + " - " + playerName;
            filename = filename.Replace('?', '_').Replace('/', '_').Replace('\\', '_').Replace('*', '_').Replace(':', '_').Replace('<', '_').Replace('>', '_').Replace('|', '_').Replace('"', '_');

            if (keepOriginalFilenames && fromNHD) {
                filename = originalFilename;
            }

            byte[] paletteData = new byte[45];
            Array.Copy(patternData, 0x78, paletteData, 0, 45);
            Color[] palette = new Color[16];
            for (int i = 0; i< 15; i++) {
                palette[i] = Color.FromArgb(paletteData[i * 3], paletteData[i * 3 + 1], paletteData[i * 3 + 2]);
            }

            byte[] pixelData = new byte[imageSize];
            Array.Copy(patternData, 0xA5, pixelData, 0, imageSize);

            int width = (imageSize == 512) ? 32 : 64;
            int height = (imageSize == 512) ? 32 : 64;

            Bitmap bitmap = new Bitmap(width, height);

            for (int y = 0; y<height; y++) {
                for (int x = 0; x<width; x++) {
                    int index = y * (width / 2) + (x / 2);
                    byte pixelByte = pixelData[index];
                    int firstPixelIndex = pixelByte & 0x0F;
                    int secondPixelIndex = (pixelByte >> 4) & 0x0F;
                    Color firstPixelColor = palette[firstPixelIndex];
                    Color secondPixelColor = palette[secondPixelIndex];
                    Color finalColor = (x % 2 == 0) ? firstPixelColor : secondPixelColor;
                    bitmap.SetPixel(x, y, finalColor);
                }
            }

            palette[0] = Color.Empty;

            string outputFileName = Path.Combine(outputFolderPath, filename + ".png");

            ushort num = 2;
            while (File.Exists(outputFileName)) {
                string newFilename = filename + "_" + num.ToString();
                num++;
                outputFileName = Path.Combine(outputFolderPath, newFilename + ".png");
            }

            Directory.CreateDirectory(outputFolderPath);
            bitmap.Save(outputFileName, System.Drawing.Imaging.ImageFormat.Png);

            Bitmap scaledBitmap = new Bitmap(pictureBox.Width, pictureBox.Height);
            using (Graphics g = Graphics.FromImage(scaledBitmap)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bitmap, 0, 0, pictureBox.Width, pictureBox.Height);
            }

            pictureBox.Image = scaledBitmap;
            Application.DoEvents();
        }
        public class SettingsForm : Form
        {
            public CheckBox KeepOriginalFilenamesCheckBox { get; private set; }
            public CheckBox ScanSubfoldersCheckBox { get; private set; }
            private Button saveButton;
            private Button cancelButton;

            public bool KeepOriginalFilenames => KeepOriginalFilenamesCheckBox.Checked;
            public bool ScanSubfolders => ScanSubfoldersCheckBox.Checked;
            public SettingsForm(bool keepOriginalFilenames, bool scanSubfolders)
            {
                Text = "Settings";
                Size = new Size(280, 160);

                KeepOriginalFilenamesCheckBox = new CheckBox {
                    Text = "Keep original NHD filenames",
                    Location = new Point(10, 20),
                    AutoSize = true,
                    Checked = keepOriginalFilenames
                };

                ScanSubfoldersCheckBox = new CheckBox {
                    Text = "Scan subfolders",
                    Location = new Point(10, 50),
                    AutoSize = true,
                    Checked = scanSubfolders
                };

                saveButton = new Button {
                    Text = "Save",
                    Location = new Point(50, 80),
                    Size = new Size(75, 30)
                };
                saveButton.Click += SaveButton_Click;

                cancelButton = new Button {
                    Text = "Cancel",
                    Location = new Point(150, 80),
                    Size = new Size(75, 30)
                };
                cancelButton.Click += CancelButton_Click;

                Controls.Add(KeepOriginalFilenamesCheckBox);
                Controls.Add(ScanSubfoldersCheckBox);
                Controls.Add(saveButton);
                Controls.Add(cancelButton);
            }
            private void SaveButton_Click(object sender, EventArgs e)
            {
                string configFilePath = "config.txt";
                var settings = new List<string>
                {
                    "KeepOriginalFilenames=" + KeepOriginalFilenamesCheckBox.Checked.ToString(),
                    "ScanSubfolders=" + ScanSubfoldersCheckBox.Checked.ToString()
                };
                File.WriteAllLines(configFilePath, settings);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            private void CancelButton_Click(object sender, EventArgs e)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }
        private void SettingsButton_Click(object sender, EventArgs e)
        {
            SettingsForm settingsForm = new SettingsForm(keepOriginalFilenames, scanSubfolders);
            Point mainWindowLocation = this.Location;
            settingsForm.StartPosition = FormStartPosition.Manual;
            settingsForm.Location = new Point(mainWindowLocation.X + (this.Width - settingsForm.Width) / 2, mainWindowLocation.Y + (this.Height - settingsForm.Height) / 2);
            settingsForm.KeepOriginalFilenamesCheckBox.Checked = keepOriginalFilenames;
            settingsForm.ScanSubfoldersCheckBox.Checked = scanSubfolders;

            DialogResult result = settingsForm.ShowDialog();

            if (result == DialogResult.OK) {
                keepOriginalFilenames = settingsForm.KeepOriginalFilenamesCheckBox.Checked;
                scanSubfolders = settingsForm.ScanSubfoldersCheckBox.Checked;
                SaveSettings();
            }
        }
        private void LoadSettings()
        {
            string configFilePath = "config.txt";

            if (File.Exists(configFilePath)) {
                var lines = File.ReadAllLines(configFilePath);

                foreach (var line in lines) {
                    var parts = line.Split('=');
                    if (parts.Length == 2) {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        if (key == "KeepOriginalFilenames") {
                            bool.TryParse(value, out keepOriginalFilenames);
                        }
                        else if (key == "ScanSubfolders") {
                            bool.TryParse(value, out scanSubfolders);
                        }
                    }
                }
            }
        }
        private void SaveSettings()
        {
            string configFilePath = "config.txt";
            var settings = new List<string>
            {
            "KeepOriginalFilenames=" + keepOriginalFilenames.ToString(),
            "ScanSubfolders=" + scanSubfolders.ToString()
        };
            File.WriteAllLines(configFilePath, settings);
        }
    }
}
