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
using System.Reflection;

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

        // Windows için en yüksek kaliteli çok katmanlı 256x256 Ultra HD boyutu (256 başta olmak üzere tüm ölçekler)
        public static readonly int[] HIGH_RES_SIZES = new int[] { 256, 128, 64, 48, 32, 16 };

        public const string APP_NAME = "Ultra HD İkon ve Klasör Özelleştirici";
        public const string APP_VERSION = "2.1.0";
        public const string APP_PUBLISHER = "Volkan Bilgisayar";

        public static string GetDefaultInstallDir()
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localApp, @"Programs\Ultra HD Ikon Ozellestirici");
        }

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
        /// Herhangi bir resmi (PNG, JPG, BMP vb.) Windows'un en yüksek çözünürlüklü 256x256 HD çok boyutlu ICO dosyasına dönüştürür.
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

                    for (int i = 0; i < HIGH_RES_SIZES.Length; i++)
                    {
                        bw.Write(pngBuffers[i]);
                    }
                }
            }
        }

        public static bool FixIcoResolution(string icoPath)
        {
            try
            {
                using (FileStream fs = new FileStream(icoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < 6) return false;
                    br.ReadUInt16();
                    ushort type = br.ReadUInt16();
                    ushort count = br.ReadUInt16();
                    if (type != 1 || count == 0) return false;

                    byte firstW = br.ReadByte();
                    byte firstH = br.ReadByte();

                    // İlk katman 256x256 ise uygundur
                    if (firstW == 0 && firstH == 0) return false;
                }

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
                        if (IsIgnoredDirectory(dirName)) continue;

                        result.Add(sub);

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

                // 1. Klasördeki .ico dosyalarını ara
                string[] icoFiles = Directory.GetFiles(folderPath, "*.ico", SearchOption.TopDirectoryOnly);
                if (icoFiles.Length > 0)
                {
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

                    FixIcoResolution(selectedIco);
                }
                else
                {
                    // 2. PlayStation (PS3_GAME\ICON0.PNG) veya klasördeki PNG/JPG'leri ara
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
                            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
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
                        string generatedIco = Path.Combine(folderPath, "folder_icon.ico");
                        ConvertImageToHighResIco(autoImg, generatedIco);
                        selectedIco = generatedIco;
                    }
                }

                if (selectedIco == null)
                {
                    // 3. Eş PS3 / Oyun klasörü araması (BLES vs NPEB)
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

                if (File.Exists(iniPath))
                {
                    File.SetAttributes(iniPath, FileAttributes.Normal);
                }

                // desktop.ini içeriğini oluştur (Taşınabilir Bağıl Yol)
                string content = "[.ShellClassInfo]\r\n" +
                                 "IconResource=" + icoFileName + ",0\r\n" +
                                 "IconFile=" + icoFileName + "\r\n" +
                                 "IconIndex=0\r\n";
                File.WriteAllText(iniPath, content, System.Text.Encoding.Default);

                // desktop.ini dosyasını Gizli ve Sistem yap
                File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

                // Klasörün kendisini Salt Okunur yap
                FileAttributes folderAttrs = File.GetAttributes(folderPath);
                File.SetAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);

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
                    key.SetValue("Icon", "\"" + exePath + "\",0");
                    using (RegistryKey cmdKey = key.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", "\"" + exePath + "\" /silent \"%1\"");
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\Background\shell\SetFolderIconFromInside"))
                {
                    key.SetValue("", menuText);
                    key.SetValue("Icon", "\"" + exePath + "\",0");
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

        public static void CreateShortcut(string shortcutPath, string targetPath, string description, string iconPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                if (!string.IsNullOrEmpty(iconPath))
                {
                    shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                }
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }

        public static bool ExtractChromeExtension(string targetDir)
        {
            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string[] fileNames = new string[] {
                    "manifest.json", "background.js", "ico-utils.js",
                    "popup.html", "popup.js", "icon16.png", "icon48.png", "icon128.png"
                };

                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string name in fileNames)
                {
                    string targetFile = Path.Combine(targetDir, name);
                    using (Stream s = asm.GetManifestResourceStream(name))
                    {
                        if (s != null)
                        {
                            using (FileStream fs = new FileStream(targetFile, FileMode.Create, FileAccess.Write))
                            {
                                byte[] buf = new byte[4096];
                                int read;
                                while ((read = s.Read(buf, 0, buf.Length)) > 0)
                                {
                                    fs.Write(buf, 0, read);
                                }
                            }
                        }
                        else
                        {
                            string localFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
                            if (File.Exists(localFile))
                            {
                                File.Copy(localFile, targetFile, true);
                            }
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void CreateShortcutsAndRegistry(string exePath, string installDir)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

                string desktopShortcut = Path.Combine(desktop, "Ultra HD İkon Yapıcı.lnk");
                string startShortcut = Path.Combine(startMenu, "Ultra HD İkon Yapıcı.lnk");

                CreateShortcut(desktopShortcut, exePath, APP_NAME, exePath);
                CreateShortcut(startShortcut, exePath, APP_NAME, exePath);

                // Windows Program Ekle/Kaldır (Uninstall) Kaydı
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\UltraHDIkonOzellestirici"))
                {
                    key.SetValue("DisplayName", APP_NAME);
                    key.SetValue("DisplayVersion", APP_VERSION);
                    key.SetValue("Publisher", APP_PUBLISHER);
                    key.SetValue("DisplayIcon", exePath + ",0");
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", "\"" + exePath + "\" /uninstall");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }

                ToggleContextMenu(exePath, true);
                RefreshShell(null);
            }
            catch { }
        }

        public static void UninstallApp()
        {
            if (MessageBox.Show("Ultra HD İkon ve Klasör Özelleştirici uygulamasını bilgisayarınızdan kaldırmak istediğinize emin misiniz?", "Program Kaldırma", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

                string desktopShortcut = Path.Combine(desktop, "Ultra HD İkon Yapıcı.lnk");
                string startShortcut = Path.Combine(startMenu, "Ultra HD İkon Yapıcı.lnk");

                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
                if (File.Exists(startShortcut)) File.Delete(startShortcut);

                ToggleContextMenu("", false);

                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\UltraHDIkonOzellestirici", false);
                }
                catch { }

                RefreshShell(null);

                string currentExe = Application.ExecutablePath;
                string installDir = Path.GetDirectoryName(currentExe);

                MessageBox.Show("Uygulama başarıyla kaldırıldı.", "Kaldırma Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);

                Process.Start(new ProcessStartInfo("cmd.exe", string.Format("/c ping 127.0.0.1 -n 2 > nul & rmdir /s /q \"{0}\"", installDir))
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kaldırma sırasında bir hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                string targetImage = null;
                bool recursive = true;

                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    if (a.Equals("/silent", StringComparison.OrdinalIgnoreCase) || a.Equals("-silent", StringComparison.OrdinalIgnoreCase))
                    {
                        silent = true;
                    }
                    else if (a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        UninstallApp();
                        return;
                    }
                    else if (a.Equals("/norecursive", StringComparison.OrdinalIgnoreCase))
                    {
                        recursive = false;
                    }
                    else if (Directory.Exists(a))
                    {
                        targetDir = a;
                    }
                    else if (File.Exists(a))
                    {
                        string ext = Path.GetExtension(a).ToLowerInvariant();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                        {
                            targetImage = a;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(targetImage))
                {
                    string destIco = Path.ChangeExtension(targetImage, ".ico");
                    ConvertImageToHighResIco(targetImage, destIco);
                    if (!silent)
                    {
                        MessageBox.Show("256x256 Ultra HD İkon başarıyla oluşturuldu:\n" + destIco, "Görsel Dönüştürüldü", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
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
        private TabControl tabs;
        private TabPage tabFolders;
        private TabPage tabConverter;
        private TabPage tabChrome;
        private TabPage tabSettings;

        // Sekme 1: Klasörler
        private TextBox txtPath;
        private Button btnBrowse;
        private CheckBox chkRecursive;
        private CheckBox chkHideIcon;
        private CheckBox chkWatch;
        private Button btnApply;
        private Button btnReset;
        private Button btnClearCache;
        private ListBox lstLog;
        private FileSystemWatcher watcher;

        // Sekme 2: Görsel Dönüştürücü
        private PictureBox picPreview;
        private Label lblImageInfo;
        private Button btnSelectImage;
        private Button btnSaveIco;
        private string loadedImagePath = null;

        // Sekme 3: Chrome Eklentisi
        private Button btnExportChromeExt;

        // Sekme 4: Ayarlar & Entegrasyon
        private Button btnContextMenu;
        private Button btnCreateShortcuts;
        private Button btnUninstall;

        public MainForm()
        {
            InitializeComponent();
            UpdateContextMenuButton();
        }

        private void InitializeComponent()
        {
            this.Text = Program.APP_NAME + " v" + Program.APP_VERSION;
            this.Size = new Size(720, 640);
            this.MinimumSize = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.AllowDrop = true;

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // 1. Üst Başlık Paneli (Modern Degrade / Lacivert)
            Panel pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 72;
            pnlHeader.BackColor = Color.FromArgb(12, 35, 75);

            Label lblTitle = new Label();
            lblTitle.Text = "📁 " + Program.APP_NAME;
            lblTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(16, 12);
            lblTitle.AutoSize = true;
            pnlHeader.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "256x256 Ultra HD Simge Motoru & Klasör Özelleştirme Paketi • " + Program.APP_PUBLISHER;
            lblSub.Font = new Font("Segoe UI", 9f);
            lblSub.ForeColor = Color.FromArgb(190, 220, 250);
            lblSub.Location = new Point(18, 42);
            lblSub.AutoSize = true;
            pnlHeader.Controls.Add(lblSub);

            this.Controls.Add(pnlHeader);

            // 2. Sekme Kontrolü (TabControl)
            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Segoe UI", 9.5f);

            // --- SEKME 1: KLASÖR İKON ÖZELLEŞTİRİCİ ---
            tabFolders = new TabPage("📁 Klasör Özelleştirici");
            tabFolders.Padding = new Padding(12);
            BuildFolderTab(tabFolders);
            tabs.TabPages.Add(tabFolders);

            // --- SEKME 2: GÖRSEL DÖNÜŞTÜRÜCÜ (RESİMDEN HD ICO) ---
            tabConverter = new TabPage("🎨 Resimden 256x256 ICO Yap");
            tabConverter.Padding = new Padding(16);
            BuildConverterTab(tabConverter);
            tabs.TabPages.Add(tabConverter);

            // --- SEKME 3: CHROME EKLENTİSİ ---
            tabChrome = new TabPage("🌐 Chrome Eklentisi");
            tabChrome.Padding = new Padding(16);
            BuildChromeTab(tabChrome);
            tabs.TabPages.Add(tabChrome);

            // --- SEKME 4: AYARLAR & SİSTEM ---
            tabSettings = new TabPage("⚙️ Sistem & Ayarlar");
            tabSettings.Padding = new Padding(16);
            BuildSettingsTab(tabSettings);
            tabs.TabPages.Add(tabSettings);

            this.Controls.Add(tabs);

            // Tüm pencere için Sürükle-Bırak Olayları
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;
        }

        private void BuildFolderTab(TabPage page)
        {
            GroupBox grpPath = new GroupBox();
            grpPath.Text = " Hedef Ana Klasör ";
            grpPath.Location = new Point(12, 10);
            grpPath.Size = new Size(675, 70);

            txtPath = new TextBox();
            txtPath.Location = new Point(14, 28);
            txtPath.Size = new Size(540, 25);
            txtPath.Text = Directory.GetCurrentDirectory();
            grpPath.Controls.Add(txtPath);

            btnBrowse = new Button();
            btnBrowse.Text = "Gözat...";
            btnBrowse.Location = new Point(562, 26);
            btnBrowse.Size = new Size(100, 28);
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
            page.Controls.Add(grpPath);

            chkRecursive = new CheckBox();
            chkRecursive.Text = "Tüm alt klasörleri de tara ve uygula (Toplu Tarama)";
            chkRecursive.Location = new Point(18, 90);
            chkRecursive.AutoSize = true;
            chkRecursive.Checked = true;
            page.Controls.Add(chkRecursive);

            chkHideIcon = new CheckBox();
            chkHideIcon.Text = "İkon dosyasını gizle (Hidden)";
            chkHideIcon.Location = new Point(360, 90);
            chkHideIcon.AutoSize = true;
            page.Controls.Add(chkHideIcon);

            chkWatch = new CheckBox();
            chkWatch.Text = "👁️ Bu klasörü otomatik izle (İçine yeni .ico / .png gelince anında uygula)";
            chkWatch.Location = new Point(18, 118);
            chkWatch.AutoSize = true;
            chkWatch.ForeColor = Color.FromArgb(20, 60, 160);
            chkWatch.CheckedChanged += ChkWatch_CheckedChanged;
            page.Controls.Add(chkWatch);

            btnApply = new Button();
            btnApply.Text = "🚀 HD İkonları Uygula";
            btnApply.Location = new Point(12, 150);
            btnApply.Size = new Size(175, 38);
            btnApply.BackColor = Color.FromArgb(25, 90, 200);
            btnApply.ForeColor = Color.White;
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnApply.Click += BtnApply_Click;
            page.Controls.Add(btnApply);

            btnClearCache = new Button();
            btnClearCache.Text = "🧹 Simge Önbelleğini Temizle";
            btnClearCache.Location = new Point(198, 150);
            btnClearCache.Size = new Size(200, 38);
            btnClearCache.Click += (s, e) => {
                if (MessageBox.Show("Windows simge önbelleği temizlenecek ve Dosya Gezgini yeniden başlatılacak. Devam edilsin mi?", "Önbellek Temizleme", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Program.ClearWindowsIconCache();
                    Log("Windows simge önbelleği temizlendi.");
                }
            };
            page.Controls.Add(btnClearCache);

            btnReset = new Button();
            btnReset.Text = "🔄 Sıfırla";
            btnReset.Location = new Point(410, 150);
            btnReset.Size = new Size(95, 38);
            btnReset.Click += BtnReset_Click;
            page.Controls.Add(btnReset);

            Label lblLog = new Label();
            lblLog.Text = "İşlem Günlüğü:";
            lblLog.Location = new Point(12, 202);
            lblLog.AutoSize = true;
            page.Controls.Add(lblLog);

            lstLog = new ListBox();
            lstLog.Location = new Point(12, 224);
            lstLog.Size = new Size(675, 260);
            page.Controls.Add(lstLog);
        }

        private void BuildConverterTab(TabPage page)
        {
            GroupBox grpConvert = new GroupBox();
            grpConvert.Text = " Tek Resimden 256x256 Ultra HD ICO Üretici ";
            grpConvert.Location = new Point(12, 12);
            grpConvert.Size = new Size(675, 470);

            Label lblDesc = new Label();
            lblDesc.Text = "Bilgisayarınızdaki herhangi bir resmi (PNG, JPG, BMP) buraya sürükleyip bırakabilir ya da 'Resim Seç' butonuna tıklayarak doğrudan saf 256x256 Ultra HD .ico simgesine dönüştürebilirsiniz.";
            lblDesc.Location = new Point(18, 28);
            lblDesc.Size = new Size(640, 40);
            grpConvert.Controls.Add(lblDesc);

            btnSelectImage = new Button();
            btnSelectImage.Text = "🖼️ Resim Seç (PNG, JPG, BMP)...";
            btnSelectImage.Location = new Point(18, 75);
            btnSelectImage.Size = new Size(220, 36);
            btnSelectImage.Click += BtnSelectImage_Click;
            grpConvert.Controls.Add(btnSelectImage);

            btnSaveIco = new Button();
            btnSaveIco.Text = "💾 256x256 HD ICO Olarak Kaydet...";
            btnSaveIco.Location = new Point(250, 75);
            btnSaveIco.Size = new Size(250, 36);
            btnSaveIco.BackColor = Color.FromArgb(20, 120, 60);
            btnSaveIco.ForeColor = Color.White;
            btnSaveIco.FlatStyle = FlatStyle.Flat;
            btnSaveIco.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnSaveIco.Enabled = false;
            btnSaveIco.Click += BtnSaveIco_Click;
            grpConvert.Controls.Add(btnSaveIco);

            lblImageInfo = new Label();
            lblImageInfo.Text = "Resim durumu: Resim seçilmedi. (İpucu: Resmi doğrudan bu alana sürükleyebilirsiniz)";
            lblImageInfo.Location = new Point(18, 122);
            lblImageInfo.AutoSize = true;
            lblImageInfo.ForeColor = Color.DarkSlateGray;
            grpConvert.Controls.Add(lblImageInfo);

            picPreview = new PictureBox();
            picPreview.Location = new Point(18, 150);
            picPreview.Size = new Size(280, 280);
            picPreview.BorderStyle = BorderStyle.FixedSingle;
            picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            picPreview.BackColor = Color.FromArgb(245, 247, 250);
            grpConvert.Controls.Add(picPreview);

            Label lblDropHint = new Label();
            lblDropHint.Text = "💡 Özellikler:\n\n" +
                               "• Çift yönlü filtreleme: Bulanıklık yapmayan Yüksek Kaliteli Çift Kübik (Bicubic) ölçekleme.\n" +
                               "• Windows 10 & 11 Explorer için tam uyumlu 256x256 saf simge başlığı.\n" +
                               "• Oluşturulan ikonları klasörlerin içine koyarak anında klasör simgesi yapabilirsiniz.";
            lblDropHint.Location = new Point(320, 160);
            lblDropHint.Size = new Size(335, 200);
            grpConvert.Controls.Add(lblDropHint);

            page.Controls.Add(grpConvert);
        }

        private void BuildChromeTab(TabPage page)
        {
            GroupBox grpChrome = new GroupBox();
            grpChrome.Text = " Google Chrome Sağ Tık HD ICO İndirici Eklentisi ";
            grpChrome.Location = new Point(12, 12);
            grpChrome.Size = new Size(675, 470);

            Label lblInfo = new Label();
            lblInfo.Text = "Web sitelerinden ve Google Görseller'den kapak/resim ararken doğrudan 256x256 Ultra HD .ico olarak indirmek için dahili Chrome eklentisini kullanabilirsiniz.\n\n" +
                           "Eklenti dosyaları bu programın içerisine tam paket olarak gömülüdür. Tek tıkla dosyaları çıkartabilir ve Chrome'a ekleyebilirsiniz.";
            lblInfo.Location = new Point(20, 30);
            lblInfo.Size = new Size(635, 75);
            grpChrome.Controls.Add(lblInfo);

            btnExportChromeExt = new Button();
            btnExportChromeExt.Text = "🚀 Chrome Eklentisini Dışa Aktar ve Kur...";
            btnExportChromeExt.Location = new Point(20, 115);
            btnExportChromeExt.Size = new Size(280, 42);
            btnExportChromeExt.BackColor = Color.FromArgb(25, 90, 200);
            btnExportChromeExt.ForeColor = Color.White;
            btnExportChromeExt.FlatStyle = FlatStyle.Flat;
            btnExportChromeExt.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnExportChromeExt.Click += BtnExportChromeExt_Click;
            grpChrome.Controls.Add(btnExportChromeExt);

            Label lblGuide = new Label();
            lblGuide.Text = "📌 Kurulum Adımları (20 Saniye Sürer):\n\n" +
                            "1. Yukarıdaki butona tıklayın. Eklenti dosyaları hazır klasör halinde açılacaktır.\n" +
                            "2. Google Chrome'u açıp adres satırına chrome://extensions yazın.\n" +
                            "3. Sağ üst köşedeki 'Geliştirici Modu' (Developer mode) anahtarını açın.\n" +
                            "4. Sol üstte beliren 'Paketlenmemiş öğe yükle' (Load unpacked) butonuna basın.\n" +
                            "5. Açılan 'ChromeEklentisi' klasörünü seçin. Eklentiniz anında kullanıma hazırdır!\n\n" +
                            "⭐ Kullanım: İnternette beğendiğiniz herhangi bir resme sağ tıklayıp 'Resmi ICO Olarak İndir (.ico)' demeniz yeterlidir.";
            lblGuide.Location = new Point(20, 180);
            lblGuide.Size = new Size(635, 260);
            lblGuide.Font = new Font("Segoe UI", 9.5f);
            grpChrome.Controls.Add(lblGuide);

            page.Controls.Add(grpChrome);
        }

        private void BuildSettingsTab(TabPage page)
        {
            GroupBox grpIntegrate = new GroupBox();
            grpIntegrate.Text = " Windows Entegrasyonu & Masaüstü Kısayolları ";
            grpIntegrate.Location = new Point(12, 12);
            grpIntegrate.Size = new Size(675, 470);

            btnContextMenu = new Button();
            btnContextMenu.Location = new Point(24, 38);
            btnContextMenu.Size = new Size(260, 40);
            btnContextMenu.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnContextMenu.Click += BtnContextMenu_Click;
            grpIntegrate.Controls.Add(btnContextMenu);

            Label lblCtxDesc = new Label();
            lblCtxDesc.Text = "Windows Gezgini'nde herhangi bir klasöre sağ tıkladığınızda 'İçindeki İkonu Klasör Simgesi Yap (HD)' seçeneği görünür. Tıkladığınız klasör saniyesinde güncellenir.";
            lblCtxDesc.Location = new Point(24, 85);
            lblCtxDesc.Size = new Size(620, 45);
            grpIntegrate.Controls.Add(lblCtxDesc);

            btnCreateShortcuts = new Button();
            btnCreateShortcuts.Text = "📌 Masaüstü ve Başlat Menüsü Kısayollarını Güncelle";
            btnCreateShortcuts.Location = new Point(24, 150);
            btnCreateShortcuts.Size = new Size(360, 38);
            btnCreateShortcuts.Click += (s, e) => {
                string currentExe = Application.ExecutablePath;
                string dir = Path.GetDirectoryName(currentExe);
                Program.CreateShortcutsAndRegistry(currentExe, dir);
                MessageBox.Show("Masaüstü ve Başlat Menüsü kısayolları başarıyla oluşturuldu!", "Kısayollar Eklendi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            grpIntegrate.Controls.Add(btnCreateShortcuts);

            Label lblShortDesc = new Label();
            lblShortDesc.Text = "Programı kolayca çalıştırmak için Masaüstünüze ve Windows Başlat Menünüze özel HD simgesiyle kısayol yerleştirir.";
            lblShortDesc.Location = new Point(24, 195);
            lblShortDesc.Size = new Size(620, 40);
            grpIntegrate.Controls.Add(lblShortDesc);

            Label lblDivider = new Label();
            lblDivider.BorderStyle = BorderStyle.Fixed3D;
            lblDivider.Location = new Point(24, 250);
            lblDivider.Size = new Size(620, 2);
            grpIntegrate.Controls.Add(lblDivider);

            Label lblUninstallTitle = new Label();
            lblUninstallTitle.Text = "Programı Kaldırma:";
            lblUninstallTitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblUninstallTitle.Location = new Point(24, 270);
            lblUninstallTitle.AutoSize = true;
            grpIntegrate.Controls.Add(lblUninstallTitle);

            btnUninstall = new Button();
            btnUninstall.Text = "🗑️ Programı Bilgisayardan Kaldır (Uninstall)";
            btnUninstall.Location = new Point(24, 300);
            btnUninstall.Size = new Size(300, 38);
            btnUninstall.ForeColor = Color.FromArgb(170, 30, 30);
            btnUninstall.Click += (s, e) => {
                Program.UninstallApp();
            };
            grpIntegrate.Controls.Add(btnUninstall);

            Label lblUnDesc = new Label();
            lblUnDesc.Text = "Uygulamanın kayıt defteri girdilerini, kısayollarını ve sağ tık menüsü bağlantılarını sistemden temizler.";
            lblUnDesc.Location = new Point(24, 345);
            lblUnDesc.Size = new Size(620, 40);
            grpIntegrate.Controls.Add(lblUnDesc);

            page.Controls.Add(grpIntegrate);
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            string first = files[0];
            if (Directory.Exists(first))
            {
                txtPath.Text = first;
                tabs.SelectedTab = tabFolders;
                Log("Klasör seçildi: " + first);
            }
            else if (File.Exists(first))
            {
                string ext = Path.GetExtension(first).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                {
                    LoadImageToConverter(first);
                    tabs.SelectedTab = tabConverter;
                }
            }
        }

        private void LoadImageToConverter(string path)
        {
            try
            {
                if (picPreview.Image != null)
                {
                    picPreview.Image.Dispose();
                }
                using (Image img = Image.FromFile(path))
                {
                    picPreview.Image = new Bitmap(img);
                    lblImageInfo.Text = string.Format("Resim: {0} ({1}x{2} px) -> 256x256 Ultra HD ICO olarak kaydedilmeye hazır.",
                        Path.GetFileName(path), img.Width, img.Height);
                }
                loadedImagePath = path;
                btnSaveIco.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Resim yüklenemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnSelectImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Dönüştürülecek Resmi Seçin";
                ofd.Filter = "Görsel Dosyaları (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Tüm Dosyalar (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadImageToConverter(ofd.FileName);
                }
            }
        }

        private void BtnSaveIco_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(loadedImagePath) || !File.Exists(loadedImagePath)) return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "256x256 Ultra HD ICO Kaydet";
                sfd.Filter = "Simge Dosyası (*.ico)|*.ico";
                sfd.FileName = Path.GetFileNameWithoutExtension(loadedImagePath) + ".ico";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        Program.ConvertImageToHighResIco(loadedImagePath, sfd.FileName);
                        MessageBox.Show("256x256 Ultra HD İkon başarıyla oluşturuldu!\n\n" + sfd.FileName, "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("İkon kaydedilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnExportChromeExt_Click(object sender, EventArgs e)
        {
            try
            {
                string targetDir = Path.Combine(Program.GetDefaultInstallDir(), "ChromeEklentisi");
                if (Program.ExtractChromeExtension(targetDir))
                {
                    Process.Start("explorer.exe", targetDir);
                    try
                    {
                        Process.Start("chrome.exe", "chrome://extensions");
                    }
                    catch { }

                    MessageBox.Show(
                        "Chrome Eklentisi klasörü hazırlandı ve açıldı!\n\n" +
                        "1. Chrome'da chrome://extensions sayfasını açın.\n" +
                        "2. 'Geliştirici modu' anahtarını açın.\n" +
                        "3. 'Paketlenmemiş öğe yükle' butonuna basıp bu klasörü seçin:\n" + targetDir,
                        "Chrome Eklentisi Kurulumu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Eklenti dosyaları çıkartılamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                        Thread.Sleep(700);
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
