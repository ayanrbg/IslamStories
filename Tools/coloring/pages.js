// Coloring pages from the crayon art: the drawings' dark outlines become clean line art, the
// spaces between them become the areas a tap fills. Writes, per page, into Assets/Resources/Coloring/:
//   <id>_lines.png       black lines on transparency (what the child sees on top)
//   <id>_regions.bytes   a lossless PNG: area number per pixel in R + G*256 (0 = none); line pixels
//                        belong to the nearest area so a fill runs under the line without a gap
// and pages.json with the titles, sizes and each area's original colour (the magic crayon).
// usage: node pages.js [id ...]     (no ids = all pages)
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');
const fs = require('fs');
const path = require('path');

const ART = path.join(__dirname, '../avatar-art');
const OUT = process.env.OUT_DIR || path.join(__dirname, '../../Assets/Resources/Coloring');
const PREVIEW = process.env.PREVIEW_DIR || null;
const W = 1280, H = 960;

const TOPICS = 'raw/topics_sheet.png', ICONS = 'sheets/icons_sheet.jpg', EXTRAS = 'sheets/extras_sheet.jpg',
  QUIZ = 'sheets/quiz_items.jpg', FAMILY = 'sheets/family_sheet.jpg';

// item: sheet, box in sheet pixels (erase: boxes to blank out), centre (cx, cy) and height on the page.
// seal: lines drawn in page pixels to close a shape the drawing left open (a sleeve cut by the frame).
const PAGES = [
  { id: 'mosque', ru: 'Мечеть', kz: 'Мешіт', en: 'Mosque', bg: '#bfd8f2',
    items: [{ src: TOPICS, box: [1420, 979, 1989, 1438], cx: 700, cy: 560, h: 640 },
            { src: TOPICS, box: [137, 674, 518, 1056], cx: 230, cy: 230, h: 300 }] },
  { id: 'lantern', ru: 'Фонарик', kz: 'Шам', en: 'Lantern', bg: '#2f3f73',
    items: [{ src: ICONS, box: [94, 1071, 400, 1452], cx: 640, cy: 480, h: 840 }] },
  { id: 'camel', ru: 'Верблюд', kz: 'Түйе', en: 'Camel', bg: '#f6e3b5',
    items: [{ src: TOPICS, box: [1356, 152, 1992, 855], cx: 640, cy: 480, h: 840 }] },
  { id: 'family', ru: 'Мама и папа', kz: 'Анам мен әкем', en: 'Mum and Dad', bg: '#d9f0d0',
    items: [{ src: FAMILY, box: [405, 40, 848, 1126], cx: 440, cy: 480, h: 860 },
            { src: FAMILY, box: [850, 140, 1350, 1126], cx: 850, cy: 525, h: 780 }] },
  { id: 'grandpa', ru: 'Дедушка и внучка', kz: 'Атам мен немересі', en: 'Grandpa and me', bg: '#fbe0e6',
    items: [{ src: FAMILY, box: [1360, 40, 2000, 1126], cx: 480, cy: 480, h: 860 },
            { src: FAMILY, box: [37, 360, 420, 1126], cx: 960, cy: 600, h: 620 }] },
  { id: 'moon', ru: 'Месяц и звезда', kz: 'Ай мен жұлдыз', en: 'Moon and star', bg: '#2f3f73',
    items: [{ src: TOPICS, box: [137, 674, 518, 1056], erase: [[389, 690, 518, 830]], cx: 600, cy: 500, h: 760 },
            { src: ICONS, box: [74, 100, 463, 464], cx: 1030, cy: 260, h: 330 }] },
  { id: 'gift', ru: 'Подарок', kz: 'Сыйлық', en: 'Gift', bg: '#ffe6a8',
    items: [{ src: EXTRAS, box: [104, 159, 599, 718], cx: 640, cy: 480, h: 820 }] },
  { id: 'crown', ru: 'Корона', kz: 'Тәж', en: 'Crown', bg: '#e6dcff',
    items: [{ src: ICONS, box: [1615, 653, 1986, 991], cx: 640, cy: 500, h: 600 }] },
  { id: 'dua', ru: 'Дуа', kz: 'Дұға', en: 'Dua', bg: '#d6ecff',
    items: [{ src: TOPICS, box: [100, 126, 527, 569], cx: 640, cy: 480, h: 800 }],
    seal: [[[276, 756], [530, 850]], [[1004, 756], [750, 850]]] },
  { id: 'meal', ru: 'Обед', kz: 'Түскі ас', en: 'Lunch', bg: '#fff1d6',
    items: [{ src: TOPICS, box: [742, 136, 1260, 569], cx: 640, cy: 480, h: 720 }] },
  { id: 'heart', ru: 'Сердечко', kz: 'Жүрек', en: 'Heart', bg: '#fff0f3',
    items: [{ src: TOPICS, box: [755, 1059, 1245, 1444], cx: 640, cy: 490, h: 760 }] },
  { id: 'tv', ru: 'Телевизор', kz: 'Теледидар', en: 'TV', bg: '#e3f4ec',
    items: [{ src: QUIZ, box: [23, 87, 576, 734], cx: 640, cy: 480, h: 840 }] },
  { id: 'table', ru: 'Ужин', kz: 'Кешкі ас', en: 'Dinner', bg: '#fdeedd',
    items: [{ src: QUIZ, box: [671, 272, 1310, 749], cx: 640, cy: 520, h: 640 }] },
];

// ---------- helpers on Uint8 masks ----------
function dilate(m, r) { // square, separable
  const t = new Uint8Array(W * H), o = new Uint8Array(W * H);
  for (let y = 0; y < H; y++) {
    let last = -1e9; for (let x = 0; x < W; x++) { if (m[y * W + x]) last = x; if (x - last <= r) t[y * W + x] = 1; }
    last = 1e9; for (let x = W - 1; x >= 0; x--) { if (m[y * W + x]) last = x; if (last - x <= r) t[y * W + x] = 1; }
  }
  for (let x = 0; x < W; x++) {
    let last = -1e9; for (let y = 0; y < H; y++) { if (t[y * W + x]) last = y; if (y - last <= r) o[y * W + x] = 1; }
    last = 1e9; for (let y = H - 1; y >= 0; y--) { if (t[y * W + x]) last = y; if (last - y <= r) o[y * W + x] = 1; }
  }
  return o;
}
const invert = (m) => m.map((v) => 1 - v);
const erode = (m, r) => invert(dilate(invert(m), r));

// Connected components (4-way) of pixels where m == val. Returns { lab, sizes, n }.
function components(m, val) {
  const lab = new Int32Array(W * H), sizes = [0];
  const st = new Int32Array(W * H);
  let n = 0;
  for (let s = 0; s < W * H; s++) {
    if (m[s] !== val || lab[s]) continue;
    n++; let top = 0, size = 0; st[top++] = s; lab[s] = n;
    while (top) {
      const i = st[--top]; size++;
      const x = i % W;
      if (x > 0 && m[i - 1] === val && !lab[i - 1]) { lab[i - 1] = n; st[top++] = i - 1; }
      if (x < W - 1 && m[i + 1] === val && !lab[i + 1]) { lab[i + 1] = n; st[top++] = i + 1; }
      if (i >= W && m[i - W] === val && !lab[i - W]) { lab[i - W] = n; st[top++] = i - W; }
      if (i < W * (H - 1) && m[i + W] === val && !lab[i + W]) { lab[i + W] = n; st[top++] = i + W; }
    }
    sizes.push(size);
  }
  return { lab, sizes, n };
}

function boxBlur(src, r) { // Float32 in/out, separable, two passes ≈ gaussian
  let a = src, b = new Float32Array(W * H);
  for (let pass = 0; pass < 2; pass++) {
    for (let y = 0; y < H; y++) {
      let acc = 0; const row = y * W;
      for (let x = -r; x <= r; x++) acc += a[row + Math.min(W - 1, Math.max(0, x))];
      for (let x = 0; x < W; x++) {
        b[row + x] = acc / (2 * r + 1);
        acc += a[row + Math.min(W - 1, x + r + 1)] - a[row + Math.max(0, x - r)];
      }
    }
    const c = new Float32Array(W * H);
    for (let x = 0; x < W; x++) {
      let acc = 0;
      for (let y = -r; y <= r; y++) acc += b[Math.min(H - 1, Math.max(0, y)) * W + x];
      for (let y = 0; y < H; y++) {
        c[y * W + x] = acc / (2 * r + 1);
        acc += b[Math.min(H - 1, y + r + 1) * W + x] - b[Math.max(0, y - r) * W + x];
      }
    }
    a = c; b = new Float32Array(W * H);
  }
  return a;
}

const hex = (r, g, b) => '#' + [r, g, b].map((v) => Math.round(v).toString(16).padStart(2, '0')).join('');

async function page(p) {
  // 1. The drawings on white, at page size.
  const layers = [];
  for (const it of p.items) {
    if (it.skip) continue;
    const [x0, y0, x1, y1] = it.box, pad = 6;
    const left = Math.max(0, x0 - pad), top = Math.max(0, y0 - pad);
    const meta = await sharp(path.join(ART, it.src)).metadata();
    const width = Math.min(meta.width, x1 + pad) - left, height = Math.min(meta.height, y1 + pad) - top;
    const h = it.h, w = Math.round(width * h / height);
    let img = sharp(path.join(ART, it.src)).extract({ left, top, width, height });
    if (it.erase) {
      const svg = `<svg width="${width}" height="${height}">${it.erase.map(([a, b, c, d]) =>
        `<rect x="${a - left}" y="${b - top}" width="${c - a}" height="${d - b}" fill="#fff"/>`).join('')}</svg>`;
      img = sharp(await img.composite([{ input: Buffer.from(svg) }]).png().toBuffer());
    }
    const png = await img.resize(w, h, { kernel: 'lanczos3' }).png().toBuffer();
    layers.push({ input: png, left: Math.round(it.cx - w / 2), top: Math.round(it.cy - h / 2), blend: 'darken' });
  }
  const src = await sharp({ create: { width: W, height: H, channels: 3, background: '#ffffff' } })
    .composite(layers).removeAlpha().raw().toBuffer();

  // 2. Ink: dark and nearly grey. Coloured darks (a brown beard, a blue bow) stay paint.
  const ink = new Uint8Array(W * H);
  for (let i = 0; i < W * H; i++) {
    const r = src[i * 3], g = src[i * 3 + 1], b = src[i * 3 + 2];
    const lum = 0.3 * r + 0.59 * g + 0.11 * b, sat = Math.max(r, g, b) - Math.min(r, g, b);
    ink[i] = (lum < 105 && sat < 48) || lum < 62 ? 1 : 0;
  }
  // Thin lighter lines (a mouth, brows drawn in brown) are drawing too; a lighter AREA (brown
  // trousers) is not — tell them apart by width: an area survives an erosion, a line does not.
  {
    // …and only on a light area (a face), never inside a darker painted one (a crayon robe's grain).
    const lumAll = new Float32Array(W * H);
    for (let i = 0; i < W * H; i++) lumAll[i] = 0.3 * src[i * 3] + 0.59 * src[i * 3 + 1] + 0.11 * src[i * 3 + 2];
    const around = boxBlur(lumAll, 6);
    const med = new Uint8Array(W * H);
    for (let i = 0; i < W * H; i++) {
      if (around[i] < 170) continue;
      const r = src[i * 3], g = src[i * 3 + 1], b = src[i * 3 + 2];
      const lum = 0.3 * r + 0.59 * g + 0.11 * b, sat = Math.max(r, g, b) - Math.min(r, g, b);
      med[i] = !ink[i] && lum < 140 && sat < 120 ? 1 : 0;
    }
    const c = components(med, 1), core = erode(med, 3), thick = new Uint8Array(c.n + 1);
    for (let i = 0; i < W * H; i++) if (core[i]) thick[c.lab[i]] = 1;
    for (let i = 0; i < W * H; i++) { const k = c.lab[i]; if (k && !thick[k] && c.sizes[k] >= 60 && c.sizes[k] < 3000) ink[i] = 1; }
  }
  if (p.seal) {
    const svg = `<svg width="${W}" height="${H}">${p.seal.map((pts) =>
      `<polyline points="${pts.map((q) => q.join(',')).join(' ')}" fill="none" stroke="#000" stroke-width="9" stroke-linecap="round"/>`).join('')}</svg>`;
    const sl = await sharp({ create: { width: W, height: H, channels: 3, background: '#fff' } })
      .composite([{ input: Buffer.from(svg) }]).removeAlpha().raw().toBuffer();
    for (let i = 0; i < W * H; i++) if (sl[i * 3] < 128) ink[i] = 1;
  }
  // 3. Clean: drop crayon specks, seal pinholes and gaps of a few pixels.
  let m = ink;
  { const c = components(m, 1); m = m.map((v, i) => (v && c.sizes[c.lab[i]] >= 60 ? 1 : 0)); }
  // A big solid dark patch (a black beard, shoes) becomes its outline, so it can be coloured.
  // Lines and eye dots are too thin or too small to survive the erosion.
  {
    const core = erode(m, 13), c = components(core, 1);
    const big = core.map((v, i) => (v && c.sizes[c.lab[i]] > 400 ? 1 : 0));
    const blob = dilate(big, 13).map((v, i) => v & m[i]);
    const hollow = erode(blob, 7);
    m = m.map((v, i) => (v && !hollow[i] ? 1 : 0));
  }
  m = erode(dilate(m, 3), 3);
  { const c = components(m, 0); m = m.map((v, i) => (v || c.sizes[c.lab[i]] < 90 ? 1 : 0)); }
  // 4. Smooth line art: blur the mask and cut it softly (anti-aliased edges).
  const soft = boxBlur(Float32Array.from(m), 2);
  const alpha = new Uint8Array(W * H);
  for (let i = 0; i < W * H; i++) {
    const t = Math.min(1, Math.max(0, (soft[i] - 0.3) / 0.4));
    alpha[i] = Math.round(255 * t * t * (3 - 2 * t));
  }
  // 5. Areas: what the lines enclose. Tiny ones (eye glints) join their neighbour.
  const open = new Uint8Array(W * H);
  for (let i = 0; i < W * H; i++) open[i] = alpha[i] < 110 ? 1 : 0;
  const c = components(open, 1);
  const minArea = 260;
  const keep = new Int32Array(c.n + 1);
  // The outside (touches the border) is area 1, the rest by size.
  const order = [];
  for (let k = 1; k <= c.n; k++) if (c.sizes[k] >= minArea) order.push(k);
  const touches = new Set();
  for (let x = 0; x < W; x++) { touches.add(c.lab[x]); touches.add(c.lab[(H - 1) * W + x]); }
  for (let y = 0; y < H; y++) { touches.add(c.lab[y * W]); touches.add(c.lab[y * W + W - 1]); }
  order.sort((a, b) => (touches.has(b) - touches.has(a)) || c.sizes[b] - c.sizes[a]);
  order.forEach((k, i) => (keep[k] = i + 1));
  const reg = new Int32Array(W * H);
  const q = new Int32Array(W * H); let qh = 0, qt = 0;
  for (let i = 0; i < W * H; i++) { const k = keep[c.lab[i]]; if (k) { reg[i] = k; q[qt++] = i; } }
  // Everything else (lines, glints, the outside's other bits) goes to the nearest area.
  while (qh < qt) {
    const i = q[qh++], x = i % W, k = reg[i];
    if (x > 0 && !reg[i - 1]) { reg[i - 1] = k; q[qt++] = i - 1; }
    if (x < W - 1 && !reg[i + 1]) { reg[i + 1] = k; q[qt++] = i + 1; }
    if (i >= W && !reg[i - W]) { reg[i - W] = k; q[qt++] = i - W; }
    if (i < W * (H - 1) && !reg[i + W]) { reg[i + W] = k; q[qt++] = i + W; }
  }
  const n = order.length;
  // 6. Each area's original colour, from its pixels away from the lines.
  const sum = new Float64Array((n + 1) * 4);
  const far = dilate(Uint8Array.from(alpha, (a) => (a > 20 ? 1 : 0)), 3);
  for (let i = 0; i < W * H; i++) {
    if (far[i]) continue;
    const k = reg[i] * 4; sum[k] += src[i * 3]; sum[k + 1] += src[i * 3 + 1]; sum[k + 2] += src[i * 3 + 2]; sum[k + 3]++;
  }
  const colors = [null];
  for (let k = 1; k <= n; k++) {
    const s = sum[k * 4 + 3] || 1;
    let r = sum[k * 4] / s, g = sum[k * 4 + 1] / s, b = sum[k * 4 + 2] / s;
    if (k === 1) [r, g, b] = [parseInt(p.bg.slice(1, 3), 16), parseInt(p.bg.slice(3, 5), 16), parseInt(p.bg.slice(5, 7), 16)];
    colors.push(hex(r, g, b));
  }

  // 7. Files.
  const lines = Buffer.alloc(W * H * 4);
  for (let i = 0; i < W * H; i++) { lines[i * 4] = 0x2b; lines[i * 4 + 1] = 0x24; lines[i * 4 + 2] = 0x33; lines[i * 4 + 3] = alpha[i]; }
  await sharp(lines, { raw: { width: W, height: H, channels: 4 } }).png({ compressionLevel: 9 }).toFile(path.join(OUT, `${p.id}_lines.png`));
  const rb = Buffer.alloc(W * H * 3);
  for (let i = 0; i < W * H; i++) { rb[i * 3] = reg[i] & 255; rb[i * 3 + 1] = reg[i] >> 8; }
  fs.writeFileSync(path.join(OUT, `${p.id}_regions.bytes`),
    await sharp(rb, { raw: { width: W, height: H, channels: 3 } }).png({ compressionLevel: 9, palette: false }).toBuffer());
  if (PREVIEW) {
    // Areas in random colours under the lines: shows leaks (two things in one colour) at a glance.
    const pv = Buffer.alloc(W * H * 3);
    const rnd = (k, ch) => { const h = (k * 0.618034) % 1 * 6, f = h % 1, v = [[1, f, 0], [1 - f, 1, 0], [0, 1, f], [0, 1 - f, 1], [f, 0, 1], [1, 0, 1 - f]][h | 0]; return 70 + v[ch] * 170; };
    for (let i = 0; i < W * H; i++) {
      const a = alpha[i] / 255, k = reg[i];
      for (let ch = 0; ch < 3; ch++) pv[i * 3 + ch] = Math.round((k === 1 ? 255 : rnd(k, ch)) * (1 - a) + 30 * a);
    }
    await sharp(pv, { raw: { width: W, height: H, channels: 3 } }).png().toFile(path.join(PREVIEW, `${p.id}_areas.png`));
  }
  return { id: p.id, title: { ru: p.ru, kz: p.kz, en: p.en }, width: W, height: H, areas: n, colors: colors.slice(1) };
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const want = process.argv.slice(2);
  const file = path.join(OUT, 'pages.json');
  const old = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')).pages : [];
  const out = [];
  for (const p of PAGES) {
    if (want.length && !want.includes(p.id)) { const o = old.find((x) => x.id === p.id); if (o) out.push(o); continue; }
    const t = Date.now();
    const r = await page(p);
    console.log(p.id, r.areas, 'areas', Date.now() - t, 'ms');
    out.push(r);
  }
  fs.writeFileSync(file, JSON.stringify({ pages: out }, null, 1));
})().catch((e) => { console.error(e); process.exit(1); });
