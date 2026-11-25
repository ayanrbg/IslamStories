// Column split for sheets where characters touch: node cols.js sheet.png outDir prefix K
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');
const fs = require('fs'), path = require('path');
const [file, outDir, prefix, Ks] = process.argv.slice(2); const K = +Ks;
fs.mkdirSync(outDir, { recursive: true });
(async () => {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width: w, height: h } = info, n = w * h;
  const paper = (i) => { const r = data[i*4], g = data[i*4+1], b = data[i*4+2]; const mx = Math.max(r,g,b), mn = Math.min(r,g,b); return mn > 222 && mx - mn < 20; };
  const bg = new Uint8Array(n), st = [];
  const push = (i) => { if (!bg[i] && paper(i)) { bg[i] = 1; st.push(i); } };
  for (let x = 0; x < w; x++) { push(x); push((h-1)*w + x); } for (let y = 0; y < h; y++) { push(y*w); push(y*w + w - 1); }
  while (st.length) { const i = st.pop(), x = i % w, y = (i / w) | 0; if (x > 0) push(i-1); if (x < w-1) push(i+1); if (y > 0) push(i-w); if (y < h-1) push(i+w); }
  for (let i = 0; i < n; i++) { if (bg[i]) continue; const x = i % w, y = (i / w) | 0; const near = (x>0&&bg[i-1])||(x<w-1&&bg[i+1])||(y>0&&bg[i-w])||(y<h-1&&bg[i+w]); if (!near) continue; const r = data[i*4], g = data[i*4+1], b = data[i*4+2]; if (Math.min(r,g,b) > 200 && Math.max(r,g,b) - Math.min(r,g,b) < 30) bg[i] = 2; }
  const proj = new Array(w).fill(0); for (let i = 0; i < n; i++) if (!bg[i]) proj[i % w]++;
  let x0 = 0; while (x0 < w && proj[x0] === 0) x0++; let x1 = w - 1; while (x1 > 0 && proj[x1] === 0) x1--;
  const cuts = [x0];
  for (let k = 1; k < K; k++) { const c = x0 + (x1 - x0) * k / K, r = (x1 - x0) / K * 0.35; let best = -1, bv = 1e9; for (let x = Math.round(c - r); x <= Math.round(c + r); x++) if (proj[x] < bv) { bv = proj[x]; best = x; } cuts.push(best); }
  cuts.push(x1 + 1);
  console.log('cuts', cuts.join(','));
  for (let k = 0; k < K; k++) {
    const a = cuts[k], b = cuts[k+1]; let minY = h, maxY = 0, minX = b, maxX = a;
    for (let y = 0; y < h; y++) for (let x = a; x < b; x++) if (!bg[y*w+x]) { if (y < minY) minY = y; if (y > maxY) maxY = y; if (x < minX) minX = x; if (x > maxX) maxX = x; }
    const cw = maxX - minX + 1, ch = maxY - minY + 1, out = Buffer.alloc(cw * ch * 4);
    for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) { const si = (y+minY)*w + (x+minX), di = (y*cw+x)*4; if (bg[si]) continue; out[di] = data[si*4]; out[di+1] = data[si*4+1]; out[di+2] = data[si*4+2]; const X = x+minX, Y = y+minY; const edge = (X>0&&bg[si-1])||(X<w-1&&bg[si+1])||(Y>0&&bg[si-w])||(Y<h-1&&bg[si+w]); out[di+3] = edge ? 170 : 255; }
    const name = `${prefix}_${k+1}.png`;
    await sharp(out, { raw: { width: cw, height: ch, channels: 4 } }).png({ compressionLevel: 9 }).toFile(path.join(outDir, name));
    console.log(name, `${minX},${minY} ${cw}x${ch}`);
  }
})();
