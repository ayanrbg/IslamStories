// Stand-in «star crown» mask from existing art: the crown recoloured sky-blue with the three
// gold stars of the old headband sitting on its tips. Until the artist draws the final.
// usage: node starcrown.js ../../Assets/UI/Lessons/Art/mask_crown.png mask_stars_headband.png ../../Assets/UI/Lessons/Art/mask_stars.png
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');

const [crownFile, starsFile, outFile] = process.argv.slice(2);

// The headband's stars: yellow blobs in its upper part, with their dark outline.
async function stars() {
  const { data, info } = await sharp(starsFile).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const W = info.width, H = info.height;
  const at = (x, y) => (y * W + x) * 4;
  const yellow = (i) => data[i + 3] > 128 && data[i] > 190 && data[i + 1] > 150 && data[i + 2] < 140;
  const seen = new Uint8Array(W * H);
  const blobs = [];
  for (let y = 0; y < H * 0.5; y++) for (let x = 0; x < W; x++) {
    if (seen[y * W + x] || !yellow(at(x, y))) continue;
    let x0 = x, x1 = x, y0 = y, y1 = y, n = 0;
    const st = [[x, y]]; seen[y * W + x] = 1;
    while (st.length) {
      const [cx, cy] = st.pop(); n++;
      x0 = Math.min(x0, cx); x1 = Math.max(x1, cx); y0 = Math.min(y0, cy); y1 = Math.max(y1, cy);
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = cx + dx, ny = cy + dy;
        if (nx < 0 || ny < 0 || nx >= W || ny >= H || seen[ny * W + nx] || !yellow(at(nx, ny))) continue;
        seen[ny * W + nx] = 1; st.push([nx, ny]);
      }
    }
    if (n > 800) blobs.push({ x0, x1, y0, y1, n });
  }
  blobs.sort((a, b) => a.x0 - b.x0);
  const pad = 9; // the crayon outline round the fill
  const out = [];
  for (const b of blobs) {
    const left = Math.max(0, b.x0 - pad), top = Math.max(0, b.y0 - pad);
    const width = Math.min(W, b.x1 + pad + 1) - left, height = Math.min(H, b.y1 + pad + 1) - top;
    // Keep only pixels near the star's fill (drops the stick and the band).
    const crop = Buffer.from(await sharp(starsFile).ensureAlpha().extract({ left, top, width, height }).raw().toBuffer());
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      let near = false;
      for (let dy = -pad; dy <= pad && !near; dy += 3) for (let dx = -pad; dx <= pad && !near; dx += 3) {
        const sx = left + x + dx, sy = top + y + dy;
        if (sx >= 0 && sy >= 0 && sx < W && sy < H && seen[sy * W + sx] && yellow(at(sx, sy))) near = true;
      }
      if (!near) crop[(y * width + x) * 4 + 3] = 0;
    }
    out.push({ png: await sharp(crop, { raw: { width, height, channels: 4 } }).png().toBuffer(), width, height });
  }
  return out;
}

(async () => {
  const s = await stars();
  if (s.length !== 3) throw new Error(`expected 3 stars, found ${s.length}`);
  // Yellow (≈45°) → sky blue (≈200°); the dark outline barely changes.
  const crown = await sharp(crownFile).ensureAlpha().modulate({ hue: 160, saturation: 0.7, brightness: 1.06 }).png().toBuffer();
  const cm = await sharp(crown).metadata(); // 378×345
  const W = 470, top = 105, left = Math.round((W - cm.width) / 2);
  const H = top + cm.height;
  // The crown's tips (in its own pixels) and each star's size there.
  const tips = [[28, 62, 0.95], [195, 8, 1.15], [350, 62, 0.95]];
  const layers = [{ input: crown, left, top }];
  for (let i = 0; i < 3; i++) {
    const [tx, ty, k] = tips[i];
    const w = Math.round(s[i].width * k), h = Math.round(s[i].height * k);
    const png = await sharp(s[i].png).resize(w, h).png().toBuffer();
    layers.push({ input: png, left: Math.round(left + tx - w / 2), top: Math.round(top + ty - h * 0.72) });
  }
  await sharp({ create: { width: W, height: H, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
    .composite(layers).png().toFile(outFile);
  console.log('ok', outFile, W, H);
})().catch((e) => { console.error(e.message); process.exit(1); });
