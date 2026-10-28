using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;
using System.Threading;

namespace FolderIconTool
{
    static class Program
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public const uint SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_IDLIST = 0x0000;
        public const uint SHCNE_UPDATEITEM = 0x00002000;
        public const uint SHCNF_PATHW = 0x0005;

        // Windows için saf 256x256 Ultra HD boyutu (Windows'un 16x16 gibi minik katmanları seçmesini engeller)
        public static readonly int[] HIGH_RES_SIZES = new int[] { 256 };

        public static void RefreshShell(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    IntPtr pPath = Marshal.StringToHGlobalUni(path);
                    try
                    {
                        SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pPath, IntPtr.Zero);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pPath);
                    }
                }
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        /// <summary>
        /// Herhangi bir resmi (PNG, JPG, vb.) Windows'un en yüksek çözünürlüğü olan 256x256 HD çok boyutlu ICO dosyasına dönüştürür.
        /// </summary>
        public static void ConvertImageToHighResIco(string sourceImagePath, string destIcoPath)
        {
            using (Image src = Image.FromFile(sourceImagePath))
            {
                List<byte[]> pngBuffers = new List<byte[]>();

                foreach (int size in HIGH_RES_SIZES)
                {
                    using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.Clear(Color.Transparent);

                        // En boy oranını koru ve merkeze yerleştir
                        float scale = Math.Min((float)size / src.Width, (float)size / src.Height);
                        int drawW = Math.Max(1, (int)Math.Round(src.Width * scale));
                        int drawH = Math.Max(1, (int)Math.Round(src.Height * scale));
                        int drawX = (size - drawW) / 2;
                        int drawY = (size - drawH) / 2;

                        g.DrawImage(src, drawX, drawY, drawW, drawH);

                        using (MemoryStream ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png);
                            pngBuffers.Add(ms.ToArray());
                        }
                    }
                }

                // ICO dosyasını oluştur (256x256 ilk sırada)
                using (FileStream fs = new FileStream(destIcoPath, FileMode.Create, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    // ICO Header (6 bayt)
                    bw.Write((ushort)0); // Reserved
                    bw.Write((ushort)1); // Type: 1 = ICO
                    bw.Write((ushort)HIGH_RES_SIZES.Length); // Image count

                    int offset = 6 + HIGH_RES_SIZES.Length * 16;
                    for (int i = 0; i < HIGH_RES_SIZES.Length; i++)
                    {
                        int s = HIGH_RES_SIZES[i];
                        bw.Write((byte)(s >= 256 ? 0 : s)); // Width: 0 = 256
                        bw.Write((byte)(s >= 256 ? 0 : s)); // Height: 0 = 256
                        bw.Write((byte)0); // Palette
                        bw.Write((byte)0); // Reserved
                        bw.Write((ushort)1); // Planes
                        bw.Write((ushort)32); // Bits per pixel
                        bw.Write((uint)pngBuffers[i].Length); // Data size
                        bw.Write((uint)offset); // Offset
                        offset += pngBuffers[i].Length;
                    }

                    // PNG baytlarını yaz
                    for (int i = 0; i < HIGH_RES_SIZES.Length; i++)
                    {
                        bw.Write(pngBuffers[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Mevcut bir .ico dosyasının çözünürlüğünü inceler, eğer 256x256 içermiyorsa veya düşük çözünürlüklüyse yeniden yüksek kaliteli üretir.
        /// </summary>
        public static bool FixIcoResolution(string icoPath)
        {
            try
            {
                using (FileStream fs = new FileStream(icoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < 6) return false;
                    br.ReadUInt16(); // 0
                    ushort type = br.ReadUInt16(); // 1
                    ushort count = br.ReadUInt16();
                    if (type != 1 || count == 0) return false;

                    byte firstW = br.ReadByte();
                    byte firstH = br.ReadByte();

                    // Eğer sadece 1 adet saf 256x256 simge varsa (genişlik/yükseklik 0 = 256) zaten kusursuzdur
                    if (count == 1 && firstW == 0 && firstH == 0) return false;
                }

                // Saf 256x256 değilse, dosyayı açıp 256x256 Ultra HD olarak yeniden üret
                string tempIco = icoPath + ".tmp";
                ConvertImageToHighResIco(icoPath, tempIco);
                if (File.Exists(tempIco) && new FileInfo(tempIco).Length > 0)
                {
                    File.Delete(icoPath);
                    File.Move(tempIco, icoPath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsIgnoredDirectory(string dirName)
        {
            string n = dirName.ToUpperInvariant();
            return n == "USRDIR" || n == "TROPDIR" || n == "LICDIR" || n == "SYSDIR" ||
                   n == "PS3_GAME" || n == "PS3_UPDATE" || n == "PS3_EXTRA" ||
                   n == "$RECYCLE.BIN" || n == "SYSTEM VOLUME INFORMATION" ||
                   n == ".GIT" || n == "NODE_MODULES";
        }

        public static List<string> GetAllSubFoldersToProcess(string root)
        {
            List<string> result = new List<string>();
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                try
                {
                    string[] subDirs = Directory.GetDirectories(current);
                    foreach (string sub in subDirs)
                    {
                        string dirName = Path.GetFileName(sub);
                        if (IsIgnoredDirectory(dirName))
                        {
                            continue;
                        }

                        result.Add(sub);

                        // Eğer bu klasör zaten bir oyun kök diziniyse (PS3_GAME veya PARAM.SFO içeriyorsa),
                        // derin alt klasörlerine (USRDIR vs.) dalıp takılmayı engelle
                        bool isGameFolder = Directory.Exists(Path.Combine(sub, "PS3_GAME")) ||
                                           File.Exists(Path.Combine(sub, "PARAM.SFO"));
                        if (!isGameFolder)
                        {
                            queue.Enqueue(sub);
                        }
                    }
                }
                catch { }
            }
            return result;
        }

        public static bool ApplyFolderIcon(string folderPath, bool hideIconFile, out string message)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    message = "Klasör bulunamadı.";
                    return false;
                }

                string selectedIco = null;

                // 1. Önce doğrudan klasördeki .ico dosyalarını ara
                string[] icoFiles = Directory.GetFiles(folderPath, "*.ico", SearchOption.TopDirectoryOnly);
                if (icoFiles.Length > 0)
                {
                    // En yeni değiştirilen veya özel isimli simgeyi önceliklendir
                    Array.Sort(icoFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
                    selectedIco = icoFiles[0];
                    foreach (string file in icoFiles)
                    {
                        string name = Path.GetFileName(file).ToLowerInvariant();
                        if (name == "icon.ico" || name == "folder.ico" || name == "cover.ico" || name == "app.ico" || name.StartsWith("indir"))
                        {
                            selectedIco = file;
                            break;
                        }
                    }

                    // Eğer mevcut .ico düşük çözünürlüklüyse veya 16x16 başa yerleştiyse onu 256x256 HD yap
                    FixIcoResolution(selectedIco);
                }
                else
                {
                    // 2. Eğer .ico yoksa PlayStation (PS3_GAME\ICON0.PNG) veya klasördeki PNG/JPG'leri ara ve OTOMATİK DÖNÜŞTÜR!
                    string autoImg = null;
                    string ps3Icon = Path.Combine(folderPath, @"PS3_GAME\ICON0.PNG");
                    if (File.Exists(ps3Icon))
                    {
                        autoImg = ps3Icon;
                    }
                    else
                    {
                        string[] candidateImages = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (string img in candidateImages)
                        {
                            string ext = Path.GetExtension(img).ToLowerInvariant();
                            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                            {
                                string name = Path.GetFileName(img).ToLowerInvariant();
                                if (name == "icon0.png" || name == "icon.png" || name == "cover.png" || name == "cover.jpg" || name == "folder.png" || name == "folder.jpg")
                                {
                                    autoImg = img;
                                    break;
                                }
                                if (autoImg == null && !name.Contains("desktop.ini"))
                                {
                                    autoImg = img;
                                }
                            }
                        }
                    }

                    if (autoImg != null)
                    {
                        // Otomatik olarak yüksek çözünürlüklü 256x256 HD .ico oluştur
                        string generatedIco = Path.Combine(folderPath, "folder_icon.ico");
                        ConvertImageToHighResIco(autoImg, generatedIco);
                        selectedIco = generatedIco;
                    }
                }

                if (selectedIco == null)
                {
                    // 3. Eş PS3 / Oyun klasörü araması: Eğer bu klasörde ikon yoksa ama kardeş klasörde (örn. BLES00455 vs NPEB00455) varsa ödünç al
                    try
                    {
                        string currentName = Path.GetFileName(folderPath);
                        System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(currentName, @"\d{4,5}");
                        if (m.Success)
                        {
                            string code = m.Value;
                            string parentDir = Path.GetDirectoryName(folderPath);
                            if (parentDir != null && Directory.Exists(parentDir))
                            {
                                string[] siblings = Directory.GetDirectories(parentDir, "*" + code + "*");
                                foreach (string sib in siblings)
                                {
                                    if (sib.Equals(folderPath, StringComparison.OrdinalIgnoreCase)) continue;

                                    string[] sibIcos = Directory.GetFiles(sib, "*.ico", SearchOption.TopDirectoryOnly);
                                    if (sibIcos.Length > 0)
                                    {
                                        string targetIco = Path.Combine(folderPath, "folder_icon.ico");
                                        File.Copy(sibIcos[0], targetIco, true);
                                        selectedIco = targetIco;
                                        break;
                                    }

                                    string sibPs3 = Path.Combine(sib, @"PS3_GAME\ICON0.PNG");
                                    if (!File.Exists(sibPs3)) sibPs3 = Path.Combine(sib, "ICON0.PNG");
                                    if (File.Exists(sibPs3))
                                    {
                                        string targetIco = Path.Combine(folderPath, "folder_icon.ico");
                                        ConvertImageToHighResIco(sibPs3, targetIco);
                                        selectedIco = targetIco;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (selectedIco == null)
                {
                    message = "Klasörde simge yapılacak bir .ico veya resim dosyası bulunamadı.";
                    return false;
                }

                string icoFileName = Path.GetFileName(selectedIco);
                string iniPath = Path.Combine(folderPath, "desktop.ini");

                // Eğer desktop.ini varsa önce özniteliklerini temizle
                if (File.Exists(iniPath))
                {
                    File.SetAttributes(iniPath, FileAttributes.Normal);
                }

                // desktop.ini içeriğini oluştur (Windows standart 2 satırlı saf simge formatı)
                string fullIcoPath = Path.Combine(folderPath, icoFileName);
                string content = "[.ShellClassInfo]\r\nIconResource=" + fullIcoPath + ",0\r\n";
                File.WriteAllText(iniPath, content, System.Text.Encoding.Default);

                // desktop.ini dosyasını Gizli ve Sistem yap (Windows şart koşar)
                File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

                // Klasörün kendisini Salt Okunur ve Sistem yap
                FileAttributes folderAttrs = File.GetAttributes(folderPath);
                File.SetAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);

                // İsteğe bağlı: İkon dosyasını gizle
                if (hideIconFile && File.Exists(selectedIco))
                {
                    File.SetAttributes(selectedIco, File.GetAttributes(selectedIco) | FileAttributes.Hidden);
                }

                RefreshShell(folderPath);
                message = "Kristal netliğinde (256x256 HD) ikon ayarlandı: " + icoFileName;
                return true;
            }
            catch (Exception ex)
            {
                message = "Hata: " + ex.Message;
                return false;
            }
        }

        public static bool ResetFolderIcon(string folderPath, out string message)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    message = "Klasör bulunamadı.";
                    return false;
                }

                string iniPath = Path.Combine(folderPath, "desktop.ini");
                if (File.Exists(iniPath))
                {
                    File.SetAttributes(iniPath, FileAttributes.Normal);
                    File.Delete(iniPath);
                }

                FileAttributes attrs = File.GetAttributes(folderPath);
                if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(folderPath, attrs & ~FileAttributes.ReadOnly);
                }

                RefreshShell(folderPath);
                message = "Klasör simgesi varsayılana sıfırlandı.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Hata: " + ex.Message;
                return false;
            }
        }

        public static void ClearWindowsIconCache()
        {
            try
            {
                // Windows 10 & 11 IconCache ve Thumbnail önbelleğini temizleyip Explorer'ı yeniden başlat
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string expCache = Path.Combine(localApp, @"Microsoft\Windows\Explorer");
                string oldCache = Path.Combine(localApp, "IconCache.db");

                string cmd = string.Format(
                    "/c taskkill /f /im explorer.exe & " +
                    "del /f /q \"{0}\" 2>nul & " +
                    "del /f /q \"{1}\\iconcache_*.db\" 2>nul & " +
                    "del /f /q \"{1}\\thumbcache_*.db\" 2>nul & " +
                    "start explorer.exe",
                    oldCache, expCache);

                Process.Start(new ProcessStartInfo("cmd.exe", cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        public static bool IsContextMenuInstalled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\SetFolderIconFromInside"))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void ToggleContextMenu(string exePath, bool install)
        {
            if (install)
            {
                string menuText = "İçindeki İkonu Klasör Simgesi Yap (HD)";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell\SetFolderIconFromInside"))
                {
                    key.SetValue("", menuText);
                    key.SetValue("Icon", "shell32.dll,4");
                    using (RegistryKey cmdKey = key.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", "\"" + exePath + "\" /silent \"%1\"");
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\Background\shell\SetFolderIconFromInside"))
                {
                    key.SetValue("", menuText);
                    key.SetValue("Icon", "shell32.dll,4");
                    using (RegistryKey cmdKey = key.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", "\"" + exePath + "\" /silent \"%V\"");
                    }
                }
            }
            else
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\SetFolderIconFromInside", false); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\Background\shell\SetFolderIconFromInside", false); } catch { }
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0)
            {
                bool silent = false;
                string targetDir = null;
                bool recursive = true;

                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    if (a.Equals("/silent", StringComparison.OrdinalIgnoreCase) || a.Equals("-silent", StringComparison.OrdinalIgnoreCase))
                    {
                        silent = true;
                    }
                    else if (a.Equals("/norecursive", StringComparison.OrdinalIgnoreCase))
                    {
                        recursive = false;
                    }
                    else if (Directory.Exists(a))
                    {
                        targetDir = a;
                    }
                }

                if (!string.IsNullOrEmpty(targetDir))
                {
                    string msg;
                    if (recursive)
                    {
                        int count = 0;
                        if (ApplyFolderIcon(targetDir, false, out msg)) count++;
                        try
                        {
                            List<string> subs = GetAllSubFoldersToProcess(targetDir);
                            foreach (string sub in subs)
                            {
                                if (ApplyFolderIcon(sub, false, out msg)) count++;
                            }
                        }
                        catch { }
                        RefreshShell(targetDir);
                        if (!silent)
                        {
                            MessageBox.Show("İşlem tamamlandı!\nToplam " + count + " adet klasörün simgesi 256x256 HD olarak güncellendi.", "Toplu İkon Tarama", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    else
                    {
                        bool ok = ApplyFolderIcon(targetDir, false, out msg);
                        if (!silent)
                        {
                            MessageBox.Show(msg, ok ? "Başarılı" : "Uyarı", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                        }
                    }
                    return;
                }
            }

            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private TextBox txtPath;
        private Button btnBrowse;
        private CheckBox chkRecursive;
        private CheckBox chkHideIcon;
        private CheckBox chkWatch;
        private Button btnApply;
        private Button btnReset;
        private Button btnContextMenu;
        private Button btnClearCache;
        private ListBox lstLog;
        private FileSystemWatcher watcher;

        public MainForm()
        {
            InitializeComponent();
            UpdateContextMenuButton();
        }

        private void InitializeComponent()
        {
            this.Text = "Klasör İkon Eşleştirici (256x256 HD Destekli)";
            this.Size = new Size(640, 530);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.AllowDrop = true;

            // Header Panel
            Panel pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 68;
            pnlHeader.BackColor = Color.FromArgb(15, 60, 110);

            Label lblTitle = new Label();
            lblTitle.Text = "📁 Klasör İkon Eşleştirici (Ultra HD 256x256)";
            lblTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(16, 10);
            lblTitle.AutoSize = true;
            pnlHeader.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "Windows standartlarında 256x256 en yüksek çözünürlüklü simgeleri uygular (Bulanıklık yapmaz).";
            lblSub.Font = new Font("Segoe UI", 9f);
            lblSub.ForeColor = Color.FromArgb(200, 225, 250);
            lblSub.Location = new Point(18, 38);
            lblSub.AutoSize = true;
            pnlHeader.Controls.Add(lblSub);

            this.Controls.Add(pnlHeader);

            // Path Selection Group
            GroupBox grpPath = new GroupBox();
            grpPath.Text = " Hedef Ana Klasör ";
            grpPath.Location = new Point(16, 78);
            grpPath.Size = new Size(592, 70);

            txtPath = new TextBox();
            txtPath.Location = new Point(14, 28);
            txtPath.Size = new Size(460, 25);
            txtPath.Text = Directory.GetCurrentDirectory();
            grpPath.Controls.Add(txtPath);

            btnBrowse = new Button();
            btnBrowse.Text = "Gözat...";
            btnBrowse.Location = new Point(482, 26);
            btnBrowse.Size = new Size(96, 28);
            btnBrowse.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtPath.Text = fbd.SelectedPath;
                    }
                }
            };
            grpPath.Controls.Add(btnBrowse);
            this.Controls.Add(grpPath);

            // Options
            chkRecursive = new CheckBox();
            chkRecursive.Text = "Tüm alt klasörleri de tara ve uygula (Toplu Tarama)";
            chkRecursive.Location = new Point(20, 155);
            chkRecursive.AutoSize = true;
            chkRecursive.Checked = true;
            this.Controls.Add(chkRecursive);

            chkHideIcon = new CheckBox();
            chkHideIcon.Text = "İkon dosyasını gizle (Hidden)";
            chkHideIcon.Location = new Point(340, 155);
            chkHideIcon.AutoSize = true;
            this.Controls.Add(chkHideIcon);

            chkWatch = new CheckBox();
            chkWatch.Text = "👁️ Bu klasörü otomatik izle (İçine yeni .ico / .png gelince anında uygula)";
            chkWatch.Location = new Point(20, 185);
            chkWatch.AutoSize = true;
            chkWatch.ForeColor = Color.FromArgb(30, 64, 175);
            chkWatch.CheckedChanged += ChkWatch_CheckedChanged;
            this.Controls.Add(chkWatch);

            // Action Buttons
            btnApply = new Button();
            btnApply.Text = "🚀 HD İkonları Uygula";
            btnApply.Location = new Point(16, 218);
            btnApply.Size = new Size(160, 36);
            btnApply.BackColor = Color.FromArgb(37, 99, 235);
            btnApply.ForeColor = Color.White;
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnApply.Click += BtnApply_Click;
            this.Controls.Add(btnApply);

            btnContextMenu = new Button();
            btnContextMenu.Location = new Point(184, 218);
            btnContextMenu.Size = new Size(185, 36);
            btnContextMenu.Click += BtnContextMenu_Click;
            this.Controls.Add(btnContextMenu);

            btnClearCache = new Button();
            btnClearCache.Text = "🧹 Simge Önbelleğini Temizle";
            btnClearCache.Location = new Point(377, 218);
            btnClearCache.Size = new Size(150, 36);
            btnClearCache.Click += (s, e) => {
                if (MessageBox.Show("Windows simge önbelleği temizlenecek ve Dosya Gezgini yeniden başlatılacak. Devam edilsin mi?", "Önbellek Temizleme", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Program.ClearWindowsIconCache();
                    Log("Windows simge önbelleği temizlendi.");
                }
            };
            this.Controls.Add(btnClearCache);

            btnReset = new Button();
            btnReset.Text = "🔄 Sıfırla";
            btnReset.Location = new Point(535, 218);
            btnReset.Size = new Size(73, 36);
            btnReset.Click += BtnReset_Click;
            this.Controls.Add(btnReset);

            // Log
            Label lblLog = new Label();
            lblLog.Text = "İşlem Günlüğü:";
            lblLog.Location = new Point(16, 265);
            lblLog.AutoSize = true;
            this.Controls.Add(lblLog);

            lstLog = new ListBox();
            lstLog.Location = new Point(16, 287);
            lstLog.Size = new Size(592, 190);
            this.Controls.Add(lstLog);

            // Drag and drop onto form
            this.DragEnter += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            this.DragDrop += (s, e) => {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Directory.Exists(files[0]))
                {
                    txtPath.Text = files[0];
                    Log("Klasör bırakıldı: " + files[0]);
                }
            };
        }

        private void Log(string msg)
        {
            if (lstLog.InvokeRequired)
            {
                lstLog.Invoke(new Action<string>(Log), msg);
                return;
            }
            string time = DateTime.Now.ToString("HH:mm:ss");
            lstLog.Items.Insert(0, "[" + time + "] " + msg);
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            string root = txtPath.Text.Trim();
            if (!Directory.Exists(root))
            {
                MessageBox.Show("Lütfen geçerli bir klasör yolu seçin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int count = 0;
            string msg;
            if (Program.ApplyFolderIcon(root, chkHideIcon.Checked, out msg))
            {
                count++;
                Log(Path.GetFileName(root) + " -> " + msg);
            }
            else
            {
                Log(Path.GetFileName(root) + " -> " + msg);
            }

            if (chkRecursive.Checked)
            {
                try
                {
                    List<string> dirs = Program.GetAllSubFoldersToProcess(root);
                    foreach (string dir in dirs)
                    {
                        if (Program.ApplyFolderIcon(dir, chkHideIcon.Checked, out msg))
                        {
                            count++;
                            Log(Path.GetFileName(dir) + " -> " + msg);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("Alt dizin tarama hatası: " + ex.Message);
                }
            }

            Program.RefreshShell(root);
            Log("Tamamlandı! Toplam " + count + " klasörün simgesi 256x256 HD olarak güncellendi.");
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            string root = txtPath.Text.Trim();
            if (!Directory.Exists(root)) return;

            string msg;
            if (Program.ResetFolderIcon(root, out msg))
            {
                Log(msg);
            }
            else
            {
                Log("Sıfırlama başarısız: " + msg);
            }
        }

        private void UpdateContextMenuButton()
        {
            bool installed = Program.IsContextMenuInstalled();
            if (installed)
            {
                btnContextMenu.Text = "❌ Sağ Tık Menüsünden Kaldır";
                btnContextMenu.ForeColor = Color.FromArgb(180, 30, 30);
            }
            else
            {
                btnContextMenu.Text = "🖱️ Sağ Tık Menüsüne Ekle";
                btnContextMenu.ForeColor = Color.Black;
            }
        }

        private void BtnContextMenu_Click(object sender, EventArgs e)
        {
            bool installed = Program.IsContextMenuInstalled();
            string myExe = Application.ExecutablePath;
            Program.ToggleContextMenu(myExe, !installed);
            UpdateContextMenuButton();

            if (!installed)
            {
                Log("Sağ tık menüsüne eklendi! Artık herhangi bir klasöre sağ tıklayıp HD ikon yapabilirsiniz.");
                MessageBox.Show("Windows Explorer sağ tık menüsüne eklendi!\nArtık herhangi bir klasöre sağ tıklayıp 'İçindeki İkonu Klasör Simgesi Yap (HD)' seçeneğini kullanabilirsiniz.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log("Sağ tık menüsünden kaldırıldı.");
            }
        }

        private void ChkWatch_CheckedChanged(object sender, EventArgs e)
        {
            if (chkWatch.Checked)
            {
                string path = txtPath.Text.Trim();
                if (!Directory.Exists(path))
                {
                    chkWatch.Checked = false;
                    MessageBox.Show("İzlemek için geçerli bir klasör seçin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    watcher = new FileSystemWatcher(path, "*.*");
                    watcher.IncludeSubdirectories = chkRecursive.Checked;
                    watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;

                    FileSystemEventHandler handler = (s, ev) => {
                        string ext = Path.GetExtension(ev.FullPath).ToLowerInvariant();
                        if (ext != ".ico" && ext != ".png" && ext != ".jpg" && ext != ".jpeg") return;
                        Thread.Sleep(700); // Kopyalamanın tamamlanmasını bekle
                        string dir = Path.GetDirectoryName(ev.FullPath);
                        string msg;
                        if (Program.ApplyFolderIcon(dir, chkHideIcon.Checked, out msg))
                        {
                            Log("[Otomatik Algılandı] " + Path.GetFileName(dir) + " -> " + msg);
                        }
                    };

                    watcher.Created += handler;
                    watcher.Changed += handler;
                    watcher.EnableRaisingEvents = true;

                    Log("Otomatik HD izleme başlatıldı: " + path);
                }
                catch (Exception ex)
                {
                    chkWatch.Checked = false;
                    Log("İzleme başlatılamadı: " + ex.Message);
                }
            }
            else
            {
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    watcher = null;
                }
                Log("Otomatik izleme durduruldu.");
            }
        }
    }
}
