# 🎨 Ultra HD (256x256) İkon ve Klasör Özelleştirici v2.1

Modern Windows 10 & 11 standartlarında **256x256 Ultra HD** kristal netliğinde klasör simgeleri uygulayan, resimleri anında çok katmanlı HD `.ico` formatına dönüştüren ve tek tıkla Windows'a kurulan **hepsi-bir-arada masaüstü yazılımı**.

---

## 🚀 Öne Çıkan Yenilikler (v2.1)

1. **Program Files Kurulum Sihirbazı (`UltraHDIkonKurulum.exe`):**
   * Windows standartlarına tam uyumlu olarak `C:\Program Files\Ultra HD Ikon Ozellestirici` dizinine kurulur.
   * Başlat Menüsü ve Masaüstü kısayollarını oluşturur, Windows Denetim Masası / "Program Ekle veya Kaldır" listesine kaydolur ve klasör sağ tık menüsüne anında entegre olur.
   * Doğrudan [GitHub Releases](https://github.com/volkanbilgisayar/Ultra-HD-Icon-Folder-Customizer/releases) sayfasından sürümüne göre tek tıkla indirilebilir.
2. **Özel 3D Fluent Tasarım İkonu:**
   * Çok katmanlı (256x256, 128x128, 64x64, 48x48, 32x32, 16x16) özel 3D kristal cam detaylı klasör simgesi ile Windows görev çubuğu ve masaüstünde daima cam gibi net görünür.
3. **Dahili Görsel Dönüştürücü (Resimden HD ICO Yap):**
   * Artık tarayıcıya veya internete bile gerek yok! Bilgisayarınızdaki herhangi bir resmi (PNG, JPG, BMP) program penceresine veya `.exe`'nin üzerine sürükleyip bıraktığınız anda saf 256x256 Ultra HD `.ico` oluşturur.
4. **Gömülü Chrome Eklentisi (Tek Tıkla Kur):**
   * Chrome eklentisinin tüm dosyaları programın içine gömülmüştür. Program arayüzündeki tek butona basarak eklentiyi dışa aktarabilir ve Chrome'a anında ekleyebilirsiniz.
5. **Taşınabilir Bağıl Yol Desteği (`folder_icon.ico,0`):**
   * Klasörlerinizi nereye taşırsanız taşıyın (Flash bellek, harici disk, başka bilgisayar), simgeler asla kaybolmaz.

---

## 📦 Dosyalar

| Dosya | Açıklama |
| :--- | :--- |
| **`UltraHDIkonKurulum.exe`** | **Önerilen:** Windows Kurulum Sihirbazı (Masaüstü, Başlat ve Sağ tık entegrasyonu yapar). |
| **`KlasorIkonYapici.exe`** | Kurulumsuz doğrudan çalışan taşınabilir (Portable) bağımsız uygulama. |
| **`folder_icon.ico` / `app_icon.png`** | Özel 256x256 Ultra HD çok katmanlı uygulama simgesi. |
| **`Setup.cs` & `KlasorIkonYapici.cs`** | C# kaynak kodları (Windows dahili `csc.exe` ile sıfır bağımlılıkla derlenebilir). |
| **Chrome Eklentisi Dosyaları** | `manifest.json`, `background.js`, `popup.*` vb. (ayrıca exe içine gömülüdür). |

---

## 💻 Kullanım Yolları

### Yöntem 1: Windows Sağ Tık Menüsü (En Kolay Yol ⭐)
* Kurulum yapıldıktan sonra bilgisayarınızdaki veya harici diskinizdeki herhangi bir klasöre **sağ tıklayın**:
  👉 **"İçindeki İkonu Klasör Simgesi Yap (HD)"** seçeneğine basmanız yeterlidir.

### Yöntem 2: Program Arayüzü ile Toplu Tarama
* Programı açın, ana klasörünüzü seçin (veya sürükleyip bırakın).
* **"🚀 HD İkonları Uygula"** butonuna basın. Yüzlerce oyun ve arşiv klasörü saniyeler içinde taranıp güncellenir.

### Yöntem 3: Sürükle-Bırak ile Resimden ICO Yapma
* İster masaüstündeki bir görseli doğrudan `KlasorIkonYapici.exe`'nin üzerine sürükleyin, ister programdaki **"🎨 Resimden 256x256 ICO Yap"** sekmesine bırakın; anında 256x256 saf ICO çıktısı alırsınız.

### Yöntem 4: Canlı İzleme (Otomatik Takip 👁️)
* İndirme klasörünüzü seçip **"👁️ Bu klasörü otomatik izle"** kutusunu işaretleyin. Klasöre yeni bir `.ico` veya resim geldiğinde siz hiçbir şeye dokunmadan klasörün simgesi anında değişir.

---

## ⚙️ Kaynak Koddan Yeniden Derleme

Windows'un dahili `csc.exe` derleyicisi ile harici hiçbir araca ihtiyaç duymadan komut satırından derleyebilirsiniz:

```cmd
:: 1. Ana Uygulamayı Derle (İkon ve Eklenti Kaynakları Gömülü)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /platform:anycpu /win32icon:folder_icon.ico /resource:manifest.json /resource:background.js /resource:ico-utils.js /resource:popup.html /resource:popup.js /resource:icon16.png /resource:icon48.png /resource:icon128.png /out:KlasorIkonYapici.exe KlasorIkonYapici.cs

:: 2. Tek Exe Kurulum Sihirbazını Derle
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /platform:anycpu /win32icon:folder_icon.ico /resource:KlasorIkonYapici.exe /resource:folder_icon.ico /out:UltraHDIkonKurulum.exe Setup.cs
```
