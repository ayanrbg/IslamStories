// Lists the drawings on a sheet (blobs on white paper) with their boxes, to pick crops for pages.js.
// usage: node blobs.js <sheet> [preview.png]
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');

const [file, preview] = process.argv.slice(2);

(async () => {
  const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const W = info.width, H = info.height;
  const ink = new Uint8Array(W * H);
  for (let i = 0; i < W * H; i++) {
    const r = data[i * 3], g = data[i * 3 + 1], b = data[i * 3 + 2];
    const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
    ink[i] = mn > 222 && mx - mn < 20 ? 0 : 1;
  }
  // Grow by a few pixels so a drawing's loose bits (rays, sparkles) join it.
  const grow = 14, tmp = new Uint8Array(W * H), g2 = new Uint8Array(W * H);
  for (let y = 0; y < H; y++) {
    let last = -1e9; for (let x = 0; x < W; x++) { if (ink[y * W + x]) last = x; if (x - last <= grow) tmp[y * W + x] = 1; }
    last = 1e9; for (let x = W - 1; x >= 0; x--) { if (ink[y * W + x]) last = x; if (last - x <= grow) tmp[y * W + x] = 1; }
  }
  for (let x = 0; x < W; x++) {
    let last = -1e9; for (let y = 0; y < H; y++) { if (tmp[y * W + x]) last = y; if (y - last <= grow) g2[y * W + x] = 1; }
    last = 1e9; for (let y = H - 1; y >= 0; y--) { if (tmp[y * W + x]) last = y; if (last - y <= grow) g2[y * W + x] = 1; }
  }
  const seen = new Uint8Array(W * H), out = [];
  for (let s = 0; s < W * H; s++) {
    if (!g2[s] || seen[s]) continue;
    let x0 = W, y0 = H, x1 = 0, y1 = 0, n = 0;
    const st = [s]; seen[s] = 1;
    while (st.length) {
      const i = st.pop(), x = i % W, y = (i / W) | 0; n++;
      if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
      for (const j of [i - 1, i + 1, i - W, i + W]) if (j >= 0 && j < W * H && g2[j] && !seen[j]) { seen[j] = 1; st.push(j); }
    }
    if (n > W * H * 0.004) out.push([x0 + grow, y0 + grow, x1 - grow, y1 - grow]);
  }
  out.sort((a, b) => (Math.round(a[1] / 300) - Math.round(b[1] / 300)) || a[0] - b[0]);
  out.forEach((b, i) => console.log(i, JSON.stringify(b)));
  if (preview) {
    const svg = `<svg width="${W}" height="${H}">${out.map((b, i) =>
      `<rect x="${b[0]}" y="${b[1]}" width="${b[2] - b[0]}" height="${b[3] - b[1]}" fill="none" stroke="red" stroke-width="6"/>` +
      `<text x="${b[0] + 10}" y="${b[1] + 60}" font-size="60" fill="red">${i}</text>`).join('')}</svg>`;
    await sharp(file).composite([{ input: Buffer.from(svg) }]).resize(900).toFile(preview);
  }
})();
