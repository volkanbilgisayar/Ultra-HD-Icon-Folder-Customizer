/**
 * ico-utils.js - Resimleri Windows ICO formatına dönüştürme araçları
 */

// Saf 256x256 Ultra HD boyutu (Windows'un 16x16 küçük katmanlara sapmasını ve 'Boyutlar: 16x16' göstermesini engeller)
const ICO_SIZES = [256];

/**
 * Verilen PNG dizilerinden standart bir ICO dosyası (ikili Uint8Array) oluşturur.
 * @param {Array<{ width: number, height: number, data: Uint8Array }>} images
 * @returns {Uint8Array}
 */
function createIco(images) {
  const count = images.length;
  const headerSize = 6;
  const dirEntrySize = 16;
  let offset = headerSize + count * dirEntrySize;

  const totalSize = offset + images.reduce((sum, img) => sum + img.data.length, 0);
  const buffer = new Uint8Array(totalSize);
  const view = new DataView(buffer.buffer);

  // 1. ICO Başlığı (6 bayt)
  view.setUint16(0, 0, true);     // Ayrılmış alan (0)
  view.setUint16(2, 1, true);     // Dosya tipi: 1 = ICO
  view.setUint16(4, count, true); // İçerdiği simge sayısı

  // 2. Dizin Girişleri (Her simge için 16 bayt)
  let currentOffset = offset;
  images.forEach((img, i) => {
    const entryOffset = headerSize + i * dirEntrySize;
    // ICO formatında 256 piksel değeri 0 olarak kodlanır
    const w = img.width >= 256 ? 0 : img.width;
    const h = img.height >= 256 ? 0 : img.height;

    buffer[entryOffset + 0] = w;                          // Genişlik
    buffer[entryOffset + 1] = h;                          // Yükseklik
    buffer[entryOffset + 2] = 0;                          // Renk paleti sayısı (0 = paletsiz / 32bpp)
    buffer[entryOffset + 3] = 0;                          // Ayrılmış (0)
    view.setUint16(entryOffset + 4, 1, true);              // Renk düzlemi (1)
    view.setUint16(entryOffset + 6, 32, true);             // Piksel başına bit (32bpp RGBA)
    view.setUint32(entryOffset + 8, img.data.length, true); // Resim veri boyutu (bayt)
    view.setUint32(entryOffset + 12, currentOffset, true);  // Resim verisinin dosyadaki ofseti

    // Resim ikili verisini (PNG) ofsete yerleştir
    buffer.set(img.data, currentOffset);
    currentOffset += img.data.length;
  });

  return buffer;
}

/**
 * ImageBitmap nesnesini standart ICO boyutlarında yeniden boyutlandırıp PNG tamponlarına dönüştürür.
 * @param {ImageBitmap} bitmap
 * @returns {Promise<Uint8Array>}
 */
async function renderIcoFromBitmap(bitmap) {
  const images = [];

  for (const size of ICO_SIZES) {
    const canvas = new OffscreenCanvas(size, size);
    const ctx = canvas.getContext("2d");

    // Yüksek kaliteli küçültme/yumuşatma
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = "high";

    // En-boy oranını koruyarak ortala
    const scale = Math.min(size / bitmap.width, size / bitmap.height);
    const drawW = Math.max(1, Math.round(bitmap.width * scale));
    const drawH = Math.max(1, Math.round(bitmap.height * scale));
    const drawX = Math.round((size - drawW) / 2);
    const drawY = Math.round((size - drawH) / 2);

    ctx.drawImage(bitmap, drawX, drawY, drawW, drawH);

    const pngBlob = await canvas.convertToBlob({ type: "image/png" });
    const arrayBuffer = await pngBlob.arrayBuffer();

    images.push({
      width: size,
      height: size,
      data: new Uint8Array(arrayBuffer)
    });
  }

  return createIco(images);
}

/**
 * Herhangi bir resim Blob'unu (PNG, JPG, WebP, SVG vb.) çok boyutlu ICO ikili dizisine dönüştürür.
 * @param {Blob} blob
 * @returns {Promise<Uint8Array>}
 */
async function convertBlobToIco(blob) {
  const bitmap = await createImageBitmap(blob);
  try {
    return await renderIcoFromBitmap(bitmap);
  } finally {
    bitmap.close();
  }
}

/**
 * Uint8Array ikili dizisini Base64 Data URL formatına dönüştürür.
 * @param {Uint8Array} uint8Array
 * @param {string} mimeType
 * @returns {string}
 */
function uint8ArrayToDataUrl(uint8Array, mimeType = "image/x-icon") {
  let binary = "";
  const len = uint8Array.byteLength;
  const chunkSize = 8192;
  for (let i = 0; i < len; i += chunkSize) {
    const chunk = uint8Array.subarray(i, Math.min(i + chunkSize, len));
    binary += String.fromCharCode.apply(null, chunk);
  }
  return `data:${mimeType};base64,${btoa(binary)}`;
}

/**
 * URL'den veya veri kaynağından uygun bir .ico dosya adı türetir.
 * @param {string} url
 * @param {string} fallbackPrefix
 * @returns {string}
 */
function getIcoFilename(url, fallbackPrefix = "icon") {
  try {
    if (url && !url.startsWith("data:") && !url.startsWith("blob:")) {
      const cleanUrl = url.split("?")[0].split("#")[0];
      const rawName = cleanUrl.substring(cleanUrl.lastIndexOf("/") + 1);
      const decodedName = decodeURIComponent(rawName);
      const nameWithoutExt = decodedName.replace(/\.[^/.]+$/, "");
      const sanitized = nameWithoutExt.replace(/[<>:"/\\|?*]+/g, "_").trim();
      if (sanitized.length > 0) {
        return `${sanitized}.ico`;
      }
    }
  } catch (e) {
    // Hata durumunda varsayılan ada dön
  }
  return `${fallbackPrefix}_${Date.now()}.ico`;
}

// Global kapsamda erişilebilir kıl (hem Service Worker hem Window)
self.ICO_SIZES = ICO_SIZES;
self.createIco = createIco;
self.renderIcoFromBitmap = renderIcoFromBitmap;
self.convertBlobToIco = convertBlobToIco;
self.uint8ArrayToDataUrl = uint8ArrayToDataUrl;
self.getIcoFilename = getIcoFilename;
