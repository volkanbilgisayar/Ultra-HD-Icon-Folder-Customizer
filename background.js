/**
 * background.js - Chrome Eklentisi Arka Plan Servis Çalışanı (Service Worker)
 */

importScripts("ico-utils.js");

// Eklenti yüklendiğinde veya güncellendiğinde sağ tık menüsünü oluştur
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: "download-as-ico",
      title: "Resmi ICO Olarak İndir (.ico)",
      contexts: ["image"]
    });
  });
});

// Eklenti simgesi üzerinde geçici durum rozeti gösterme
function setBadge(text, color, duration = 3000) {
  try {
    chrome.action.setBadgeText({ text });
    chrome.action.setBadgeBackgroundColor({ color });
    if (duration > 0) {
      setTimeout(() => {
        chrome.action.setBadgeText({ text: "" });
      }, duration);
    }
  } catch (e) {
    // Rozet hatalarını yoksay
  }
}

/**
 * Google Görseller ve diğer sitelerdeki küçük önizlemelerin (thumbnail)
 * arkasındaki gerçek yüksek çözünürlüklü orijinal görsel URL'sini yakalar.
 * @param {chrome.contextMenus.OnClickData} info
 * @returns {string}
 */
function resolveBestImageUrl(info) {
  let targetUrl = info.srcUrl || "";

  // 1. Google Görseller: Arama ızgarasındaki linkUrl parametresinde gerçek 'imgurl' yatar
  if (info.linkUrl) {
    try {
      const u = new URL(info.linkUrl);
      const imgurl = u.searchParams.get("imgurl");
      if (imgurl) {
        console.log("Google Görseller HD orijinal URL yakalandı:", imgurl);
        return imgurl;
      }
    } catch (e) {}
  }

  // 2. Google'ın küçük önizleme sunucusunda (encrypted-tbn0) parametre kontrolü
  if (targetUrl.includes("encrypted-tbn0.gstatic.com") || targetUrl.includes("googleusercontent.com")) {
    try {
      const u = new URL(targetUrl);
      const imgurl = u.searchParams.get("imgurl");
      if (imgurl) return imgurl;
    } catch (e) {}
  }

  // 3. Reddit önizleme URL'si ise orijinal i.redd.it adresine dönüştür
  if (targetUrl.includes("preview.redd.it")) {
    targetUrl = targetUrl.replace("preview.redd.it", "i.redd.it").split("?")[0];
  }

  return targetUrl;
}

// Sağ tık menüsü tıklandığında çalışacak fonksiyon
chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === "download-as-ico" && info.srcUrl) {
    await handleImageDownload(info);
  }
});

/**
 * Resmi en yüksek çözünürlükte indirir, 256x256 HD ICO'ya dönüştürür ve bilgisayara kaydeder.
 * @param {chrome.contextMenus.OnClickData} info
 */
async function handleImageDownload(info) {
  try {
    setBadge("...", "#2563eb", 0);

    // 1. Orijinal yüksek çözünürlüklü URL'yi belirle
    const highResUrl = resolveBestImageUrl(info);
    let blob;

    // 2. Resmi Blob olarak çek (önce HD adresi dene, hata verirse tıklanan adrese dön)
    try {
      blob = await fetchImageBlob(highResUrl);
    } catch (err) {
      if (highResUrl !== info.srcUrl) {
        console.warn("HD URL çekilemedi, önizleme deneniyor:", err);
        blob = await fetchImageBlob(info.srcUrl);
      } else {
        throw err;
      }
    }

    // 3. Resmi 256x256 Ultra HD çok boyutlu Windows ICO formatına dönüştür
    const icoBytes = await convertBlobToIco(blob);

    // 4. İndirme için Data URL oluştur
    const dataUrl = uint8ArrayToDataUrl(icoBytes, "image/x-icon");

    // 5. Uygun dosya adını belirle
    const filename = getIcoFilename(highResUrl || info.srcUrl, "ikon");

    // 6. Chrome İndirmeler API'si ile bilgisayara kaydet
    await chrome.downloads.download({
      url: dataUrl,
      filename: filename,
      saveAs: false
    });

    setBadge("OK", "#16a34a", 2500);
  } catch (error) {
    console.error("ICO dönüştürme ve indirme hatası:", error);
    setBadge("HATA", "#dc2626", 4000);
  }
}

/**
 * Verilen URL'den resim Blob verisini güvenli bir şekilde çeker.
 * @param {string} srcUrl
 * @returns {Promise<Blob>}
 */
async function fetchImageBlob(srcUrl) {
  if (srcUrl.startsWith("data:")) {
    const res = await fetch(srcUrl);
    return await res.blob();
  }

  try {
    const response = await fetch(srcUrl, { credentials: "include" });
    if (response.ok) {
      return await response.blob();
    }
  } catch (e) {
    // Fallback
  }

  const response = await fetch(srcUrl);
  if (!response.ok) {
    throw new Error(`Resim çekilemedi (HTTP ${response.status})`);
  }
  return await response.blob();
}
