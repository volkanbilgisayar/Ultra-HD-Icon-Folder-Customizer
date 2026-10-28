# 🎨 Ultra HD (256x256) İkon ve Klasör Özelleştirme Paketi

Bu paket, hem web'den tek tıkla **256x256 Ultra HD** `.ico` simgeleri indirmenizi sağlayan bir **Chrome Eklentisi**, hem de bilgisayarınızdaki veya harici diskinizdeki (PS3, PC oyunları, arşivler vb.) yüzlerce klasörün simgesini içlerindeki ikonlarla saniyeler içinde değiştiren **Klasör İkon Yapıcı** Windows aracını içerir.

---

## 📦 Paket İçeriği

| Dosya | Açıklama |
| :--- | :--- |
| **`KlasorIkonYapici.exe`** | Klasörleri tarayan, 256x256 HD simgeleri uygulayan grafik arayüzlü Windows programı. |
| **`KlasorIkonUygula.bat`** | Klasörleri üzerine sürükleyip bırakarak toplu ikon uygulamak için pratik başlatıcı. |
| **`manifest.json`, `background.js`, `ico-utils.js`** | Web'den sağ tıkla HD `.ico` indiren Google Chrome eklentisi. |
| **`popup.html`, `popup.js`** | Chrome eklentisi için sürükle-bırak hızlı dönüştürme penceresi. |
| **`KlasorIkonYapici.cs`** | C# kaynak kodu (Windows'un dahili `csc.exe` derleyicisiyle derlenebilir, sıfır bağımlılık). |

---

## 🌐 1. Chrome Eklentisi: Kurulum ve Kullanım

Web sitelerinden veya Google Görseller'den kapak/resim ararken doğrudan **256x256 Ultra HD** `.ico` olarak indirmek için:

### Kurulum:
1. Google Chrome'u açın ve adres çubuğuna `chrome://extensions` yazıp `Enter`a basın.
2. Sağ üst köşedeki **"Geliştirici modu"** (Developer mode) anahtarını açın.
3. Sol üstte beliren **"Paketlenmemiş öğe yükle"** (Load unpacked) butonuna tıklayın.
4. Bu klasörü (`c:\Users\Volkan\Desktop\ikon`) seçin. Eklenti anında kurulacaktır.

### Kullanım:
* **Sağ Tıkla İndirme:** Web'de herhangi bir görsele (veya Google Görseller sonucuna) sağ tıklayın ve **"Resmi ICO Olarak İndir (.ico)"** seçeneğini seçin. Eklenti, görselin orijinal yüksek çözünürlüğünü yakalar ve tek katman saf 256x256 HD `.ico` olarak İndirilenler klasörünüze kaydeder.
* **Masaüstünden Dönüştürme:** Chrome'un sağ üstündeki eklenti simgesine tıklayın, açılan kutucuğa bilgisayarınızdaki herhangi bir resmi (PNG, JPG, WebP) sürükleyip bırakın; anında 256x256 `.ico` olarak insin.

---

## 📁 2. Klasör İkon Yapıcı (`KlasorIkonYapici.exe`) Kullanımı

Klasörlerin içine indirdiğiniz `.ico` dosyalarını veya oyunların kendi resimlerini (`ICON0.PNG`, `cover.jpg` vb.) klasörün dış simgesi yapmak için 3 pratik yöntem vardır:

### Yöntem A: Windows Sağ Tık Menüsü (En Kolay Yol ⭐)
1. `KlasorIkonYapici.exe` programını bir kere açın.
2. **"🖱️ Sağ Tık Menüsüne Ekle"** butonuna tıklayın.
3. Artık programı açmanıza gerek kalmaz! Windows Gezgini'nde veya harici diskinizde istediğiniz herhangi bir klasöre **sağ tıklayıp**:
   👉 **"İçindeki İkonu Klasör Simgesi Yap (HD)"** seçeneğine basmanız yeterlidir.

### Yöntem B: Program Arayüzü ile Toplu Tarama
1. `KlasorIkonYapici.exe` uygulamasını çalıştırın.
2. **Gözat...** butonuna basarak ana klasörünüzü (örneğin `F:\3000-4000` veya `F:\ATILIYOR`) seçin (veya klasörü pencereye sürükleyip bırakın).
3. **"Tüm alt klasörleri de tara ve uygula"** kutusu işaretliyken **"🚀 HD İkonları Uygula"** butonuna basın.
4. Yüzlerce oyun klasörü birkaç saniye içinde taranıp simgeleri güncellenecektir.

### Yöntem C: Otomatik İzleme (Canlı Takip 👁️)
* İndirme yaptığınız klasörü veya oyun arşivinizi seçip **"👁️ Bu klasörü otomatik izle"** kutusunu işaretleyin.
* Siz Chrome'dan klasörün içine yeni bir `.ico` veya resim indirdiğiniz anda, program bunu algılar ve hiç bir butona basmanıza gerek kalmadan klasör simgesini anında günceller.

---

## 🎯 Çözülen Sorunlar & Akıllı Özellikler

1. **Bulanıklık ("Çamur Gibi" Görünüm) Engellendi:**
   * Çok boyutlu eski ICO dosyalarında Windows bazen 16x16 küçük katmanı seçip esnetiyordu. Bu paketteki tüm araçlar **saf 256x256 Ultra HD** formatında çalışır. Windows Explorer ve Fotoğraflar uygulamasında boyut her zaman "256 x 256" görünür, piksellenme yapmaz.
2. **Sarı Klasör İçinde Ezilme (`FolderType=Generic`) Temizlendi:**
   * Klasörlerin varsayılan sarı klasör içinde minik resim şeklinde görünmesine sebep olan hatalı `desktop.ini` şablonları kaldırılmıştır. Sadece saf 2 satırlı standart simge formatı yazılır (`[.ShellClassInfo] IconResource=...`).
3. **Kardeş Klasör Eşleştirme (PS3 BLES / NPEB Desteği):**
   * PS3 oyunlarında disk klasörü (`BLES...`) ve dijital klasör (`NPEB...`) ikili bulunur. İkon bu klasörlerden sadece birinde olsa bile program aynı kod numarasına sahip eşini bulur ve iki klasöre de simgeyi otomatik uygular.
   * Klasörde `.ico` yoksa bile PlayStation'ın orijinal `ICON0.PNG` görselini veya klasördeki herhangi bir kapak resmini otomatik 256x256 HD simgeye dönüştürür.
4. **Harici Disklerde (FAT32) Donma Yok:**
   * Oyunların `USRDIR`, `TROPDIR`, `LICDIR` gibi binlerce dosyalık derin klasörleri akıllı arama kuyruğu sayesinde atlanır, işlem saniyeler içinde tamamlanır.
5. **Windows 10 & 11 Simge Önbelleği Temizleyici:**
   * Değiştirdiğiniz simgeler Windows'un eski önbelleği yüzünden hemen güncellenmezse, programdaki **"🧹 Simge Önbelleğini Temizle"** butonuna basmanız yeterlidir. Windows 11 `iconcache_*.db` dosyalarını temizleyip Gezgin'i anında yeniler.

---

## ⚙️ Yeniden Derleme (İsteğe Bağlı)

Program herhangi bir kuruluma veya harici kütüphaneye ihtiyaç duymaz. Kaynak kodda (`KlasorIkonYapici.cs`) değişiklik yaparsanız, Windows'un kendi dahili C# derleyicisi ile komut satırından tek satırda derleyebilirsiniz:

```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /platform:anycpu /out:KlasorIkonYapici.exe KlasorIkonYapici.cs
```
