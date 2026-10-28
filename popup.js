/**
 * popup.js - Manuel resim yükleme ve dönüştürme kontrolcüsü
 */

const dropZone = document.getElementById("dropZone");
const fileInput = document.getElementById("fileInput");
const statusMessage = document.getElementById("statusMessage");

// Tıklama ile dosya seçiciyi aç
dropZone.addEventListener("click", () => {
  fileInput.click();
});

fileInput.addEventListener("change", (e) => {
  if (e.target.files && e.target.files[0]) {
    processLocalFile(e.target.files[0]);
  }
});

// Sürükle & Bırak olayları
["dragenter", "dragover"].forEach(eventName => {
  dropZone.addEventListener(eventName, (e) => {
    e.preventDefault();
    e.stopPropagation();
    dropZone.classList.add("dragover");
  });
});

["dragleave", "drop"].forEach(eventName => {
  dropZone.addEventListener(eventName, (e) => {
    e.preventDefault();
    e.stopPropagation();
    dropZone.classList.remove("dragover");
  });
});

dropZone.addEventListener("drop", (e) => {
  if (e.dataTransfer.files && e.dataTransfer.files[0]) {
    processLocalFile(e.dataTransfer.files[0]);
  }
});

function showStatus(text, type) {
  statusMessage.textContent = text;
  statusMessage.className = `status ${type}`;
}

async function processLocalFile(file) {
  if (!file.type.startsWith("image/")) {
    showStatus("Lütfen geçerli bir resim dosyası seçin.", "error");
    return;
  }

  try {
    showStatus("ICO formatına dönüştürülüyor...", "loading");

    // ico-utils.js fonksiyonu ile ICO ikili verisine dönüştür
    const icoBytes = await convertBlobToIco(file);
    const dataUrl = uint8ArrayToDataUrl(icoBytes, "image/x-icon");

    // Çıkış dosya adı
    const originalName = file.name.replace(/\.[^/.]+$/, "");
    const filename = `${originalName || "icon"}.ico`;

    // Chrome indirmeler API'si ile indir
    await chrome.downloads.download({
      url: dataUrl,
      filename: filename,
      saveAs: false
    });

    showStatus(`✓ "${filename}" indirildi!`, "success");
  } catch (error) {
    console.error("Dönüştürme hatası:", error);
    showStatus("Dönüştürme sırasında bir hata oluştu.", "error");
  } finally {
    fileInput.value = "";
  }
}
