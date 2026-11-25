// Splits a crayon sheet on white into separate transparent PNGs (one per connected blob).
// usage: node split.js <sheet.png> <outDir> <prefix> [minAreaShare=0.002] [maxSize=1024]
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');
const fs = require('fs');
const path = require('path');

const [file, outDir, prefix, minShareArg, maxSizeArg] = process.argv.slice(2);
const minShare = parseFloat(minShareArg || '0.002');
const maxSize = parseInt(maxSizeArg || '1024', 10);
fs.mkdirSync(outDir, { recursive: true });

(async () => {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width: w, height: h } = info;
  const n = w * h;
  // Paper: light and nearly grey. Flood from the borders so white inside outlines stays.
  const paper = (i) => {
    const r = data[i * 4], g = data[i * 4 + 1], b = data[i * 4 + 2];
    const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
    return mn > 222 && mx - mn < 20;
  };
  const bg = new Uint8Array(n);
  const st = [];
  const push = (i) => { if (!bg[i] && paper(i)) { bg[i] = 1; st.push(i); } };
  for (let x = 0; x < w; x++) { push(x); push((h - 1) * w + x); }
  for (let y = 0; y < h; y++) { push(y * w); push(y * w + w - 1); }
  while (st.length) {
    const i = st.pop(), x = i % w, y = (i / w) | 0;
    if (x > 0) push(i - 1); if (x < w - 1) push(i + 1);
    if (y > 0) push(i - w); if (y < h - 1) push(i + w);
  }
  // Light halo around outlines (antialiasing against paper) → background too.
  for (let pass = 0; pass < 2; pass++) {
    const add = [];
    for (let i = 0; i < n; i++) {
      if (bg[i]) continue;
      const x = i % w, y = (i / w) | 0;
      const near = (x > 0 && bg[i - 1]) || (x < w - 1 && bg[i + 1]) || (y > 0 && bg[i - w]) || (y < h - 1 && bg[i + w]);
      if (!near) continue;
      const r = data[i * 4], g = data[i * 4 + 1], b = data[i * 4 + 2];
      if (Math.min(r, g, b) > 200 && Math.max(r, g, b) - Math.min(r, g, b) < 30) add.push(i);
    }
    for (const i of add) bg[i] = 1;
  }
  // Blobs.
  const label = new Int32Array(n);
  const blobs = [];
  for (let s = 0; s < n; s++) {
    if (bg[s] || label[s]) continue;
    const id = blobs.length + 1;
    const q = [s]; label[s] = id;
    const b = { minX: w, minY: h, maxX: 0, maxY: 0, n: 0, id };
    while (q.length) {
      const i = q.pop(), x = i % w, y = (i / w) | 0; b.n++;
      if (x < b.minX) b.minX = x; if (x > b.maxX) b.maxX = x; if (y < b.minY) b.minY = y; if (y > b.maxY) b.maxY = y;
      for (const j of [x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1])
        if (j >= 0 && !bg[j] && !label[j]) { label[j] = id; q.push(j); }
    }
    blobs.push(b);
  }
  const big = blobs.filter((b) => b.n > n * minShare);
  // Reading order: rows by vertical centre, then left to right.
  const rowH = h / 8;
  big.sort((a, b) => Math.round(((a.minY + a.maxY) / 2) / rowH) - Math.round(((b.minY + b.maxY) / 2) / rowH) || a.minX - b.minX);
  let k = 0;
  for (const b of big) {
    k++;
    const pad = 4;
    const x0 = Math.max(0, b.minX - pad), y0 = Math.max(0, b.minY - pad);
    const x1 = Math.min(w - 1, b.maxX + pad), y1 = Math.min(h - 1, b.maxY + pad);
    const cw = x1 - x0 + 1, ch = y1 - y0 + 1;
    const out = Buffer.alloc(cw * ch * 4);
    for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) {
      const si = (y + y0) * w + (x + x0), di = (y * cw + x) * 4;
      // Holes inside the blob (paper enclosed by it) stay; other blobs' pixels are cleared.
      const mine = label[si] === b.id || (!bg[si] && false);
      const inside = mine || (!bg[si] ? false : false);
      if (!mine) { out[di + 3] = 0; continue; }
      out[di] = data[si * 4]; out[di + 1] = data[si * 4 + 1]; out[di + 2] = data[si * 4 + 2];
      const x2 = x + x0, y2 = y + y0;
      const edge = (x2 > 0 && bg[si - 1]) || (x2 < w - 1 && bg[si + 1]) || (y2 > 0 && bg[si - w]) || (y2 < h - 1 && bg[si + w]);
      out[di + 3] = edge ? 170 : 255;
    }
    const name = `${prefix}_${String(k).padStart(2, '0')}.png`;
    await sharp(out, { raw: { width: cw, height: ch, channels: 4 } })
      .resize({ width: maxSize, height: maxSize, fit: 'inside', withoutEnlargement: true })
      .png({ compressionLevel: 9 }).toFile(path.join(outDir, name));
    console.log(name, `${b.minX},${b.minY} ${cw}x${ch}`);
  }
})();
