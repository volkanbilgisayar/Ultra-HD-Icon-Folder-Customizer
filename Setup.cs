using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace FolderIconSetup
{
    static class SetupProgram
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    public class SetupForm : Form
    {
        private TextBox txtTarget;
        private Button btnBrowse;
        private CheckBox chkDesktop;
        private CheckBox chkStartMenu;
        private CheckBox chkContextMenu;
        private Button btnInstall;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblStatus;
        private CheckBox chkLaunchNow;

        private const string APP_NAME = "Ultra HD İkon ve Klasör Özelleştirici";
        private const string APP_VERSION = "2.1.0";
        private const string APP_PUBLISHER = "Volkan Bilgisayar";

        public SetupForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = APP_NAME + " - Kurulum Sihirbazı";
            this.Size = new Size(580, 470);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // 1. Üst Banner
            Panel pnlBanner = new Panel();
            pnlBanner.Dock = DockStyle.Top;
            pnlBanner.Height = 75;
            pnlBanner.BackColor = Color.FromArgb(12, 35, 75);

            Label lblTitle = new Label();
            lblTitle.Text = "📁 " + APP_NAME;
            lblTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(18, 14);
            lblTitle.AutoSize = true;
            pnlBanner.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "Kurulum Sihirbazı • Sürüm " + APP_VERSION + " • " + APP_PUBLISHER;
            lblSub.Font = new Font("Segoe UI", 9f);
            lblSub.ForeColor = Color.FromArgb(190, 220, 250);
            lblSub.Location = new Point(20, 44);
            lblSub.AutoSize = true;
            pnlBanner.Controls.Add(lblSub);

            this.Controls.Add(pnlBanner);

            // 2. Açıklama
            Label lblDesc = new Label();
            lblDesc.Text = "Bu sihirbaz, Ultra HD İkon ve Klasör Özelleştirici uygulamasını bilgisayarınıza kuracaktır.\n" +
                           "Kurulum sonrasında program Masaüstünüze, Başlat Menünüze ve klasör sağ tık menünüze normal bir Windows programı gibi entegre edilecektir.";
            lblDesc.Location = new Point(20, 95);
            lblDesc.Size = new Size(525, 45);
            this.Controls.Add(lblDesc);

            // 3. Kurulum Konumu
            GroupBox grpPath = new GroupBox();
            grpPath.Text = " Kurulum Konumu ";
            grpPath.Location = new Point(20, 150);
            grpPath.Size = new Size(525, 70);

            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Ultra HD Ikon Ozellestirici");
            txtTarget = new TextBox();
            txtTarget.Location = new Point(15, 28);
            txtTarget.Size = new Size(395, 25);
            txtTarget.Text = defaultDir;
            grpPath.Controls.Add(txtTarget);

            btnBrowse = new Button();
            btnBrowse.Text = "Gözat...";
            btnBrowse.Location = new Point(420, 26);
            btnBrowse.Size = new Size(90, 28);
            btnBrowse.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtTarget.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtTarget.Text = fbd.SelectedPath;
                    }
                }
            };
            grpPath.Controls.Add(btnBrowse);
            this.Controls.Add(grpPath);

            // 4. Kısayol & Entegrasyon Seçenekleri
            GroupBox grpOptions = new GroupBox();
            grpOptions.Text = " Kurulum Seçenekleri ";
            grpOptions.Location = new Point(20, 230);
            grpOptions.Size = new Size(525, 110);

            chkDesktop = new CheckBox();
            chkDesktop.Text = "Masaüstü kısayolu oluştur (Ultra HD İkon Yapıcı)";
            chkDesktop.Location = new Point(18, 26);
            chkDesktop.AutoSize = true;
            chkDesktop.Checked = true;
            grpOptions.Controls.Add(chkDesktop);

            chkStartMenu = new CheckBox();
            chkStartMenu.Text = "Başlat Menüsü kısayolu oluştur";
            chkStartMenu.Location = new Point(18, 52);
            chkStartMenu.AutoSize = true;
            chkStartMenu.Checked = true;
            grpOptions.Controls.Add(chkStartMenu);

            chkContextMenu = new CheckBox();
            chkContextMenu.Text = "Klasör sağ tık menüsüne 'İçindeki İkonu Klasör Simgesi Yap (HD)' ekle";
            chkContextMenu.Location = new Point(18, 78);
            chkContextMenu.AutoSize = true;
            chkContextMenu.Checked = true;
            grpOptions.Controls.Add(chkContextMenu);

            this.Controls.Add(grpOptions);

            // 5. İlerleme Çubuğu ve Durum
            progressBar = new ProgressBar();
            progressBar.Location = new Point(20, 350);
            progressBar.Size = new Size(525, 20);
            progressBar.Visible = false;
            this.Controls.Add(progressBar);

            lblStatus = new Label();
            lblStatus.Location = new Point(20, 375);
            lblStatus.Size = new Size(350, 22);
            lblStatus.ForeColor = Color.DarkSlateBlue;
            lblStatus.Text = "Kuruluma hazır.";
            this.Controls.Add(lblStatus);

            chkLaunchNow = new CheckBox();
            chkLaunchNow.Text = "Programı şimdi çalıştır";
            chkLaunchNow.Location = new Point(20, 350);
            chkLaunchNow.AutoSize = true;
            chkLaunchNow.Checked = true;
            chkLaunchNow.Visible = false;
            this.Controls.Add(chkLaunchNow);

            // 6. Alt Butonlar
            btnInstall = new Button();
            btnInstall.Text = "🚀 Kur";
            btnInstall.Location = new Point(345, 380);
            btnInstall.Size = new Size(95, 34);
            btnInstall.BackColor = Color.FromArgb(25, 90, 200);
            btnInstall.ForeColor = Color.White;
            btnInstall.FlatStyle = FlatStyle.Flat;
            btnInstall.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnInstall.Click += BtnInstall_Click;
            this.Controls.Add(btnInstall);

            btnCancel = new Button();
            btnCancel.Text = "İptal";
            btnCancel.Location = new Point(450, 380);
            btnCancel.Size = new Size(95, 34);
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            if (btnInstall.Text == "Son")
            {
                if (chkLaunchNow.Checked)
                {
                    string targetExe = Path.Combine(txtTarget.Text.Trim(), "KlasorIkonYapici.exe");
                    if (File.Exists(targetExe))
                    {
                        Process.Start(targetExe);
                    }
                }
                this.Close();
                return;
            }

            string targetDir = txtTarget.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("Lütfen geçerli bir kurulum klasörü seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnInstall.Enabled = false;
            btnCancel.Enabled = false;
            progressBar.Visible = true;
            progressBar.Value = 10;
            lblStatus.Text = "Kurulum başlatılıyor...";
            Application.DoEvents();

            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                progressBar.Value = 30;
                lblStatus.Text = "Program dosyaları yükleniyor...";
                Application.DoEvents();

                string targetExe = Path.Combine(targetDir, "KlasorIkonYapici.exe");

                // Gömülü KlasorIkonYapici.exe dosyasını hedef klasöre çıkart
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("KlasorIkonYapici.exe"))
                {
                    if (s != null)
                    {
                        using (FileStream fs = new FileStream(targetExe, FileMode.Create, FileAccess.Write))
                        {
                            byte[] buf = new byte[8192];
                            int read;
                            while ((read = s.Read(buf, 0, buf.Length)) > 0)
                            {
                                fs.Write(buf, 0, read);
                            }
                        }
                    }
                    else
                    {
                        // Aynı klasördeki mevcut KlasorIkonYapici.exe dosyasını kopyala
                        string localExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KlasorIkonYapici.exe");
                        if (File.Exists(localExe))
                        {
                            File.Copy(localExe, targetExe, true);
                        }
                    }
                }

                // Gömülü folder_icon.ico dosyasını hedef klasöre çıkart
                string targetIco = Path.Combine(targetDir, "folder_icon.ico");
                using (Stream s = asm.GetManifestResourceStream("folder_icon.ico"))
                {
                    if (s != null)
                    {
                        using (FileStream fs = new FileStream(targetIco, FileMode.Create, FileAccess.Write))
                        {
                            byte[] buf = new byte[8192];
                            int read;
                            while ((read = s.Read(buf, 0, buf.Length)) > 0)
                            {
                                fs.Write(buf, 0, read);
                            }
                        }
                    }
                    else
                    {
                        string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "folder_icon.ico");
                        if (File.Exists(localIco))
                        {
                            File.Copy(localIco, targetIco, true);
                        }
                    }
                }

                progressBar.Value = 60;
                lblStatus.Text = "Kısayollar ve Windows entegrasyonu yapılıyor...";
                Application.DoEvents();

                // Kısayollar
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

                if (chkDesktop.Checked)
                {
                    string dLnk = Path.Combine(desktop, "Ultra HD İkon Yapıcı.lnk");
                    CreateShortcut(dLnk, targetExe, APP_NAME, targetExe);
                }

                if (chkStartMenu.Checked)
                {
                    string sLnk = Path.Combine(startMenu, "Ultra HD İkon Yapıcı.lnk");
                    CreateShortcut(sLnk, targetExe, APP_NAME, targetExe);
                }

                // Sağ Tık Menüsü
                if (chkContextMenu.Checked)
                {
                    ToggleContextMenu(targetExe, true);
                }

                progressBar.Value = 85;
                lblStatus.Text = "Kayıt defteri ayarlanıyor...";
                Application.DoEvents();

                // Windows Program Ekle/Kaldır Kaydı
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\UltraHDIkonOzellestirici"))
                {
                    key.SetValue("DisplayName", APP_NAME);
                    key.SetValue("DisplayVersion", APP_VERSION);
                    key.SetValue("Publisher", APP_PUBLISHER);
                    key.SetValue("DisplayIcon", targetExe + ",0");
                    key.SetValue("InstallLocation", targetDir);
                    key.SetValue("UninstallString", "\"" + targetExe + "\" /uninstall");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }

                progressBar.Value = 100;
                lblStatus.Text = "Tebrikler! Kurulum başarıyla tamamlandı.";

                // Ekranı güncelle
                progressBar.Visible = false;
                chkLaunchNow.Visible = true;
                btnInstall.Text = "Son";
                btnInstall.Enabled = true;
                btnCancel.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kurulum sırasında bir hata oluştu:\n" + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnInstall.Enabled = true;
                btnCancel.Enabled = true;
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string description, string iconPath)
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

        private static void ToggleContextMenu(string exePath, bool install)
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
        }
    }
}
