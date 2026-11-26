// Removes bits of neighbours left at the edges of figures cut from a sheet (a hand, a scarf
// end), even when a thin outline still joins them to the figure: the mask is opened (eroded,
// largest piece kept, grown back), so thin bridges break. Size stays, anchors stay valid.
// usage: node clean.js <file.png> [...]   (overwrites)
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');

const R = 5; // bridge thickness that gets cut, px

(async () => {
  for (const file of process.argv.slice(2)) {
    const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const w = info.width, h = info.height, n = w * h;
    const on = new Uint8Array(n);
    for (let i = 0; i < n; i++) on[i] = data[i * 4 + 3] >= 40 ? 1 : 0;
    const er = morph(on, w, h, R, false);
    // largest piece of the eroded mask
    const lab = new Int32Array(n); let best = 0, bestN = 0, id = 0; const st = [];
    for (let s = 0; s < n; s++) {
      if (!er[s] || lab[s]) continue;
      id++; let c = 0; lab[s] = id; st.push(s);
      while (st.length) {
        const i = st.pop(), x = i % w, y = (i / w) | 0; c++;
        const nb = [x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1];
        for (const j of nb) if (j >= 0 && er[j] && !lab[j]) { lab[j] = id; st.push(j); }
      }
      if (c > bestN) { bestN = c; best = id; }
    }
    const core = new Uint8Array(n);
    for (let i = 0; i < n; i++) core[i] = lab[i] === best ? 1 : 0;
    const keep = morph(core, w, h, R + 2, true);
    let removed = 0;
    for (let i = 0; i < n; i++) if (!keep[i] && data[i * 4 + 3] > 0) { data[i * 4 + 3] = 0; removed++; }
    if (removed > 0) await sharp(data, { raw: { width: w, height: h, channels: 4 } }).png().toFile(file + '.tmp.png');
    if (removed > 0) require('fs').renameSync(file + '.tmp.png', file);
    console.log(file.split('/').pop(), 'removed', removed);
  }
})();

// Disc dilation (grow=true) or erosion, via two passes of a square approximated by distance.
function morph(m, w, h, r, grow) {
  const out = new Uint8Array(w * h);
  const offs = [];
  for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) if (dx * dx + dy * dy <= r * r) offs.push([dx, dy]);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    let v = grow ? 0 : 1;
    for (const [dx, dy] of offs) {
      const xx = x + dx, yy = y + dy;
      const p = xx < 0 || yy < 0 || xx >= w || yy >= h ? 0 : m[yy * w + xx];
      if (grow && p) { v = 1; break; }
      if (!grow && !p) { v = 0; break; }
    }
    out[y * w + x] = v;
  }
  return out;
}
