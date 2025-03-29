using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Text;
using System.Windows.Forms;

namespace ACNH_Pattern_Viewer
{
    public partial class Form1 : Form
    {
        private PictureBox pictureBox;
        private Button loadButton;

        public Form1()
        {
            Text = "ACNH Pattern Converter";
            ClientSize = new Size(320, 320);

            loadButton = new Button { Text = "Select Folder", Dock = DockStyle.Top };
            loadButton.Click += LoadButton_Click;

            pictureBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };

            Controls.Add(pictureBox);
            Controls.Add(loadButton);
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

                string[] files = Directory.GetFiles(folderPath, "*.nhd");
                files = files.Concat(Directory.GetFiles(folderPath, "*.nhpd")).ToArray();

                foreach (var file in files) {
                    byte[] fileData = File.ReadAllBytes(file);

                    byte[] patternNameBytes = new byte[40];
                    Array.Copy(fileData, 0x10, patternNameBytes, 0, 40);
                    string patternName = Encoding.Unicode.GetString(patternNameBytes).TrimEnd('\0');

                    byte[] playerNameBytes = new byte[20];
                    Array.Copy(fileData, 0x58, playerNameBytes, 0, 20);
                    string playerName = Encoding.Unicode.GetString(playerNameBytes).TrimEnd('\0');

                    string filename = patternName + " - " + playerName;
                    filename = filename.Replace('?', '_').Replace('/', '_').Replace('\\','_').Replace('*','_').Replace(':','_').Replace('<','_').Replace('>','_').Replace('|','_').Replace('"','_');

                    byte[] paletteData = new byte[45];
                    Array.Copy(fileData, 0x78, paletteData, 0, 45);
                    Color[] palette = new Color[16];
                    for (int i = 0; i < 15; i++) {
                        palette[i] = Color.FromArgb(paletteData[i * 3], paletteData[i * 3 + 1], paletteData[i * 3 + 2]);
                    }

                    int imageSize = (file.EndsWith(".nhd")) ? 512 : 2048;
                    byte[] pixelData = new byte[imageSize];
                    Array.Copy(fileData, 0xA5, pixelData, 0, imageSize);

                    int width = (imageSize == 512) ? 32 : 64;
                    int height = (imageSize == 512) ? 32 : 64;

                    Bitmap bitmap = new Bitmap(width, height);

                    for (int y = 0; y < height; y++) {
                        for (int x = 0; x < width; x++) {
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

                    bitmap.Save(outputFileName, System.Drawing.Imaging.ImageFormat.Png);

                    Bitmap scaledBitmap = new Bitmap(pictureBox.Width, pictureBox.Height);
                    using (Graphics g = Graphics.FromImage(scaledBitmap)) {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.DrawImage(bitmap, 0, 0, pictureBox.Width, pictureBox.Height);
                    }

                    pictureBox.Image = scaledBitmap;
                    Application.DoEvents();
                }
            }
        }
    }
}
