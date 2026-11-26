// Splits a sheet of characters who touch each other (holding hands, a braid on a
// neighbour's sleeve) without slicing anyone: every drawn pixel goes to the figure whose
// body centre reaches it by the shortest path INSIDE the drawing, so the cut runs
// through the point of contact instead of a straight column through a hand.
// Each figure is cropped with a margin; a thin seam where two figures touched is then
// removed by clean.js (opening keeps the figure, drops slivers of the neighbour).
// usage: node geo.js sheet.png outDir prefix K [pad]
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');
const fs = require('fs'), path = require('path');
const [file, outDir, prefix, Ks, pads] = process.argv.slice(2);
const K = +Ks, PAD = +(pads || 12);
fs.mkdirSync(outDir, { recursive: true });

(async () => {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width: w, height: h } = info, n = w * h;
  const lum = (i) => (data[i * 4] * 0.3 + data[i * 4 + 1] * 0.59 + data[i * 4 + 2] * 0.11);
  const paper = (i) => {
    const r = data[i * 4], g = data[i * 4 + 1], b = data[i * 4 + 2];
    const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
    return mn > 222 && mx - mn < 20;
  };

  // Background = paper reachable from the border (white kufi / socks inside outlines stay).
  const bg = new Uint8Array(n), st = [];
  const push = (i) => { if (!bg[i] && paper(i)) { bg[i] = 1; st.push(i); } };
  for (let x = 0; x < w; x++) { push(x); push((h - 1) * w + x); }
  for (let y = 0; y < h; y++) { push(y * w); push(y * w + w - 1); }
  while (st.length) {
    const i = st.pop(), x = i % w, y = (i / w) | 0;
    if (x > 0) push(i - 1); if (x < w - 1) push(i + 1); if (y > 0) push(i - w); if (y < h - 1) push(i + w);
  }

  // Figure centres from the column profile (minima between figures).
  const proj = new Array(w).fill(0);
  for (let i = 0; i < n; i++) if (!bg[i]) proj[i % w]++;
  let x0 = 0; while (x0 < w && proj[x0] === 0) x0++;
  let x1 = w - 1; while (x1 > 0 && proj[x1] === 0) x1--;
  const cuts = [x0];
  for (let k = 1; k < K; k++) {
    const c = x0 + (x1 - x0) * k / K, r = (x1 - x0) / K * 0.35;
    let best = -1, bv = 1e9;
    for (let x = Math.round(c - r); x <= Math.round(c + r); x++) if (proj[x] < bv) { bv = proj[x]; best = x; }
    cuts.push(best);
  }
  cuts.push(x1 + 1);

  // Seeds: a narrow vertical strip through the middle of each figure.
  const owner = new Int8Array(n).fill(-1), dist = new Int32Array(n).fill(1 << 30);
  let q = [];
  for (let k = 0; k < K; k++) {
    const c = Math.round((cuts[k] + cuts[k + 1]) / 2), half = Math.max(6, Math.round((cuts[k + 1] - cuts[k]) * 0.06));
    for (let y = 0; y < h; y++) for (let x = c - half; x <= c + half; x++) {
      const i = y * w + x;
      if (!bg[i]) { owner[i] = k; dist[i] = 0; q.push(i); }
    }
  }
  // Shortest paths inside the drawing; stepping onto ink costs more, so the split between
  // two figures runs along a drawn line rather than through a coloured area.
  const ink = new Uint8Array(n);
  for (let i = 0; i < n; i++) ink[i] = !bg[i] && lum(i) < 95 ? 1 : 0;
  const INK = 7, buckets = [q];
  for (let d = 0; d < buckets.length; d++) {
    const b = buckets[d]; if (!b) continue;
    for (let t = 0; t < b.length; t++) {
      const i = b[t]; if (dist[i] !== d) continue;
      const x = i % w, y = (i / w) | 0;
      const nb = [x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1];
      for (const j of nb) {
        if (j < 0 || bg[j]) continue;
        const nd = d + (ink[j] ? INK : 1);
        if (nd < dist[j]) { dist[j] = nd; owner[j] = owner[i]; (buckets[nd] = buckets[nd] || []).push(j); }
      }
    }
    buckets[d] = null;
  }

  // Where a figure was cut off its neighbour, draw an outline along the seam so the
  // edge reads as drawn, not sliced.
  const seam = new Uint8Array(n);
  for (let i = 0; i < n; i++) {
    if (owner[i] < 0) continue;
    const x = i % w, y = (i / w) | 0;
    for (let dy = -2; dy <= 2 && !seam[i]; dy++) for (let dx = -2; dx <= 2; dx++) {
      const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= w || Y >= h) continue;
      const o = owner[Y * w + X]; if (o >= 0 && o !== owner[i] && dx * dx + dy * dy <= 5) { seam[i] = 1; break; }
    }
  }

  for (let k = 0; k < K; k++) {
    let minY = h, maxY = 0, minX = w, maxX = 0;
    for (let i = 0; i < n; i++) if (owner[i] === k) {
      const x = i % w, y = (i / w) | 0;
      if (y < minY) minY = y; if (y > maxY) maxY = y; if (x < minX) minX = x; if (x > maxX) maxX = x;
    }
    minX -= PAD; minY -= PAD; maxX += PAD; maxY += PAD;
    const cw = maxX - minX + 1, ch = maxY - minY + 1, out = Buffer.alloc(cw * ch * 4);
    for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) {
      const X = x + minX, Y = y + minY;
      if (X < 0 || Y < 0 || X >= w || Y >= h) continue;
      const si = Y * w + X, di = (y * cw + x) * 4;
      if (owner[si] !== k) continue;
      if (seam[si]) { out[di] = 40; out[di + 1] = 36; out[di + 2] = 40; out[di + 3] = 255; continue; }
      out[di] = data[si * 4]; out[di + 1] = data[si * 4 + 1]; out[di + 2] = data[si * 4 + 2];
      // Outer edge pixels are a blend of ink and paper: their whiteness becomes transparency.
      const edge = (X > 0 && bg[si - 1]) || (X < w - 1 && bg[si + 1]) || (Y > 0 && bg[si - w]) || (Y < h - 1 && bg[si + w]);
      out[di + 3] = edge ? Math.max(60, Math.min(255, (255 - lum(si)) * 2.2)) : 255;
    }
    const name = `${prefix}_${k + 1}.png`;
    await sharp(out, { raw: { width: cw, height: ch, channels: 4 } }).png({ compressionLevel: 9 }).toFile(path.join(outDir, name));
    console.log(name, `${minX},${minY} ${cw}x${ch}`);
  }
})();
