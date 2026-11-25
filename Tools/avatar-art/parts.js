// Avatar parts from the cut-out characters:
//   bodies → head removed above the neck (lowest row of the face's skin), neck point + face width recorded
//   heads  → chin point (lowest face-skin row) + face width recorded
// usage: node parts.js <outDir> body:<file>:<name> ... head:<file>:<name> ...
// writes <outDir>/<name>.png and <outDir>/parts.json
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');
const fs = require('fs');
const path = require('path');

const [outDir, ...items] = process.argv.slice(2);
fs.mkdirSync(outDir, { recursive: true });
const metaPath = path.join(outDir, 'parts.json');
const meta = fs.existsSync(metaPath) ? JSON.parse(fs.readFileSync(metaPath, 'utf8')) : {};

const isSkin = (r, g, b) => r > 150 && g > 95 && r >= g && g > b + 12 && r - b > 35 && r - b < 125 && r - g < 75;

function faceComponent(data, w, h, maxY) {
  const n = w * h, skin = new Uint8Array(n);
  for (let i = 0; i < n; i++) {
    if (data[i * 4 + 3] < 200) continue;
    if ((i / w | 0) > maxY) continue;
    if (isSkin(data[i * 4], data[i * 4 + 1], data[i * 4 + 2])) skin[i] = 1;
  }
  const label = new Int32Array(n);
  let best = null;
  for (let s = 0; s < n; s++) {
    if (!skin[s] || label[s]) continue;
    const id = s + 1, q = [s];
    label[s] = id;
    const c = { id, n: 0, minX: w, maxX: 0, minY: h, maxY: 0, sy: 0 };
    while (q.length) {
      const i = q.pop(), x = i % w, y = (i / w) | 0;
      c.n++; c.sy += y;
      if (x < c.minX) c.minX = x; if (x > c.maxX) c.maxX = x; if (y < c.minY) c.minY = y; if (y > c.maxY) c.maxY = y;
      for (const j of [x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1])
        if (j >= 0 && skin[j] && !label[j]) { label[j] = id; q.push(j); }
    }
    if (!best || c.n > best.n) best = c;
  }
  if (!best) return null;
  // Face width: widest skin row of the component; chin x: centre of its lowest rows.
  let widest = 0, lowSum = 0, lowCnt = 0;
  for (let y = best.minY; y <= best.maxY; y++) {
    let a = -1, b = -1;
    for (let x = best.minX; x <= best.maxX; x++) if (label[y * w + x] === best.id) { if (a < 0) a = x; b = x; }
    if (a >= 0) {
      widest = Math.max(widest, b - a + 1);
      if (y >= best.maxY - 6) { lowSum += (a + b) / 2; lowCnt++; }
    }
  }
  return { ...best, width: widest, chinX: lowCnt ? lowSum / lowCnt : (best.minX + best.maxX) / 2 };
}

(async () => {
  for (const item of items) {
    const [kind, file, name, opt] = item.split(':');
    const img = sharp(file).ensureAlpha();
    const { data, info } = await img.raw().toBuffer({ resolveWithObject: true });
    const { width: w, height: h } = info;
    const face = faceComponent(data, w, h, kind === 'body' ? Math.round(h * 0.45) : h);
    if (!face) { console.log('FAIL no face', name); continue; }
    if (kind === 'body') {
      const cut = opt ? Math.round(parseFloat(opt) * h) : face.maxY + 2;
      for (let y = 0; y < cut; y++) for (let x = 0; x < w; x++) data[(y * w + x) * 4 + 3] = 0;
      {
        const n = w * h, lab = new Int32Array(n); let best = 0, bestN = 0;
        for (let s0 = 0; s0 < n; s0++) { if (data[s0*4+3] < 40 || lab[s0]) continue; const q=[s0]; lab[s0]=s0+1; let c=0;
          while (q.length) { const i=q.pop(), x=i%w, y=(i/w)|0; c++; for (const j of [x>0?i-1:-1,x<w-1?i+1:-1,y>0?i-w:-1,y<h-1?i+w:-1]) if (j>=0 && !lab[j] && data[j*4+3] >= 40) { lab[j]=s0+1; q.push(j); } }
          if (c > bestN) { bestN = c; best = s0+1; } }
        for (let i = 0; i < n; i++) if (lab[i] !== best) data[i*4+3] = 0;
      }
      // Trim the empty top.
      const top = cut;
      const outH = h - top;
      const buf = Buffer.from(data.subarray(top * w * 4));
      await sharp(buf, { raw: { width: w, height: outH, channels: 4 } }).png({ compressionLevel: 9 }).toFile(path.join(outDir, name + '.png'));
      meta[name] = { kind, width: w, height: outH, neck: [face.chinX / w, 0], faceWidth: face.width / w, removedHead: top };
      console.log(name, 'body', `${w}x${outH}`, 'neckX', (face.chinX / w).toFixed(3), 'faceW', (face.width / w).toFixed(3));
    } else {
      if (opt === 'trim') for (let y = face.maxY + 3; y < h; y++) for (let x = 0; x < w; x++) data[(y * w + x) * 4 + 3] = 0;
      await sharp(data, { raw: { width: w, height: h, channels: 4 } }).png({ compressionLevel: 9 }).toFile(path.join(outDir, name + '.png'));
      meta[name] = { kind, width: w, height: h, chin: [face.chinX / w, (face.maxY + 1) / h], faceWidth: face.width / w };
      console.log(name, 'head', `${w}x${h}`, 'chin', (face.chinX / w).toFixed(3), ((face.maxY + 1) / h).toFixed(3), 'faceW', (face.width / w).toFixed(3));
    }
  }
  fs.writeFileSync(metaPath, JSON.stringify(meta, null, 1));
})();
