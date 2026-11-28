// Full-bleed art for the home's cards (Speech Blubs-style: a little scene on every card, not
// an icon on a flat colour) and the small decorations around them. Made from the app's own
// art plus drawn bits (sparkles, splats, crayons, leaves, rays). usage: node cards.js
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');
const path = require('path');

const ART = path.join(__dirname, '../../Assets/UI/Lessons/Art');
const INK = '#2B2547';
const W = 640, H = 600;

// Deterministic scatter.
let seed = 7;
const rnd = () => { seed = (seed * 16807) % 2147483647; return (seed - 1) / 2147483646; };

const sparkle = (x, y, r, fill, op = 1) =>
  `<path d="M${x} ${y - r} Q${x + r * 0.18} ${y - r * 0.18} ${x + r} ${y} Q${x + r * 0.18} ${y + r * 0.18} ${x} ${y + r} Q${x - r * 0.18} ${y + r * 0.18} ${x - r} ${y} Q${x - r * 0.18} ${y - r * 0.18} ${x} ${y - r} Z" fill="${fill}" fill-opacity="${op}"/>`;
const sparkles = (n, w, h, colors, rmin, rmax, avoid) => {
  let out = '';
  for (let i = 0; i < n; i++) {
    const x = rnd() * w, y = rnd() * h;
    if (avoid && avoid(x, y)) { i--; continue; }
    out += sparkle(x, y, rmin + rnd() * (rmax - rmin), colors[i % colors.length], 0.55 + rnd() * 0.45);
  }
  return out;
};
const dots = (n, w, h, fill, op) => {
  let out = '';
  for (let i = 0; i < n; i++) out += `<circle cx="${rnd() * w}" cy="${rnd() * h}" r="${3 + rnd() * 5}" fill="${fill}" fill-opacity="${op}"/>`;
  return out;
};
const grain = `<filter id="g"><feTurbulence type="fractalNoise" baseFrequency="0.8" numOctaves="2" seed="3"/>
  <feColorMatrix type="matrix" values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  0 0 0 -1.4 0.55"/><feComposite in2="SourceGraphic" operator="in"/></filter>`;
const bg = (top, bottom, extra = '') => `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
  <defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${top}"/><stop offset="1" stop-color="${bottom}"/></linearGradient>
  <radialGradient id="glow" cx="0.5" cy="0.45" r="0.5"><stop offset="0" stop-color="#fff" stop-opacity="0.75"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>${grain}</defs>
  <rect width="${W}" height="${H}" fill="url(#bg)"/>
  <rect width="${W}" height="${H}" fill="#fff" filter="url(#g)"/>
  <circle cx="${W / 2}" cy="${H * 0.45}" r="${H * 0.46}" fill="url(#glow)"/>
  ${extra}</svg>`;

async function place(file, size, x, y, rot = 0) {
  let img = sharp(path.join(ART, file)).resize(size, size, { fit: 'inside' });
  if (rot) img = sharp(await img.png().toBuffer()).rotate(rot, { background: { r: 0, g: 0, b: 0, alpha: 0 } });
  const buf = await img.png().toBuffer();
  const m = await sharp(buf).metadata();
  return { input: buf, left: Math.round(x - m.width / 2), top: Math.round(y - m.height / 2) };
}
const svgLayer = (body) => ({ input: Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">${body}</svg>`), left: 0, top: 0 });
const crayon = (x, y, rot, body, tip) => `<g transform="translate(${x} ${y}) rotate(${rot})">
  <path d="M-80 -20 L56 -20 L56 20 L-80 20 Z" fill="${body}"/><path d="M56 -20 L96 0 L56 20 Z" fill="${tip}"/>
  <path d="M-56 -20 L-56 20 M32 -20 L32 20" stroke="${INK}" stroke-width="7"/>
  <path d="M-80 -20 L56 -20 L96 0 L56 20 L-80 20 Z" fill="none" stroke="${INK}" stroke-width="9" stroke-linejoin="round"/></g>`;
const splat = (x, y, r, fill) => {
  let d = '';
  const n = 16;
  for (let i = 0; i <= n; i++) {
    const a = (i / n) * Math.PI * 2, rr = r * (0.75 + rnd() * 0.45);
    d += `${i ? 'L' : 'M'}${(x + Math.cos(a) * rr).toFixed(1)} ${(y + Math.sin(a) * rr).toFixed(1)} `;
  }
  return `<path d="${d}Z" fill="${fill}" fill-opacity="0.55" stroke-linejoin="round"/>` +
    `<circle cx="${x + r * 1.3}" cy="${y - r * 0.6}" r="${r * 0.18}" fill="${fill}" fill-opacity="0.55"/>`;
};

async function make(name, svg, layers) {
  await sharp(Buffer.from(svg)).composite(layers).png().toFile(path.join(ART, name + '.png'));
  console.log('ok', name);
}

(async () => {
  // «Обведи»: the half-traced heart on lavender with a dotted paper and sparkles.
  await make('card_trace', bg('#EDE5FF', '#C9B6FF',
    dots(40, W, H, '#fff', 0.35) + sparkles(10, W, H, ['#fff', '#FFE08A'], 10, 22, (x, y) => Math.hypot(x - W / 2, y - H / 2) < 230)), [
    await place('trace_tile.png', 470, W / 2, H / 2 + 6),
    svgLayer(crayon(110, 520, -28, '#7FC8F2', '#E0577B')),
  ]);

  // «Раскраски»: the half-coloured mosque, paint splats and a fan of crayons.
  seed = 11;
  await make('card_coloring', bg('#DDF2FF', '#A9DCF6',
    splat(110, 130, 60, '#FF8FB8') + splat(540, 110, 50, '#FFD93B') + splat(560, 470, 56, '#8BD450') +
    sparkles(8, W, H, ['#fff'], 10, 20, (x, y) => Math.hypot(x - W / 2, y - H / 2) < 220)), [
    await place('coloring_tile.png', 400, W / 2 - 50, H / 2 - 30),
    svgLayer(crayon(470, 452, -32, '#E8453C', '#E8453C') + crayon(500, 506, -32, '#FFD93B', '#FF9A2E') + crayon(530, 560, -32, '#3D6FD9', '#3D6FD9')),
  ]);

  // «Дерево»: the tree on a hill under sun rays, leaves floating down.
  seed = 23;
  let rays = '';
  for (let i = 0; i < 12; i++) {
    const a = Math.PI * (0.55 + i * 0.07);
    rays += `<path d="M560 40 L${560 + Math.cos(a) * 700} ${40 + Math.sin(a) * 700} L${560 + Math.cos(a + 0.03) * 700} ${40 + Math.sin(a + 0.03) * 700} Z" fill="#fff" fill-opacity="0.18"/>`;
  }
  await make('card_tree', bg('#E9FAEF', '#BDEBD2',
    rays + `<circle cx="560" cy="40" r="60" fill="#FFE08A"/><circle cx="560" cy="40" r="60" fill="none" stroke="${INK}" stroke-width="8"/>` +
    `<path d="M-20 560 Q320 470 660 560 L660 620 L-20 620 Z" fill="#9FDDB2"/><path d="M-20 560 Q320 470 660 560" fill="none" stroke="${INK}" stroke-width="8"/>` +
    sparkles(6, W, 380, ['#fff'], 10, 18)), [
    await place('tree_full.png', 470, W / 2 - 20, H / 2 + 10),
    await place('leaf_green.png', 70, 90, 170, 30),
    await place('leaf_gold.png', 62, 520, 260, -40),
    await place('leaf_green.png', 54, 130, 400, -60),
    await place('leaf_gold.png', 48, 470, 420, 20),
  ]);

  // The lesson of the day: a starry sky-blue stage with a burst of light behind the child.
  seed = 31;
  const HW = 1200, HH = 640;
  let burst = '';
  for (let i = 0; i < 16; i++) {
    const a = (i / 16) * Math.PI * 2;
    burst += `<path d="M230 330 L${230 + Math.cos(a) * 1500} ${330 + Math.sin(a) * 1500} L${230 + Math.cos(a + 0.14) * 1500} ${330 + Math.sin(a + 0.14) * 1500} Z" fill="#fff" fill-opacity="0.13"/>`;
  }
  let stars = '';
  for (let i = 0; i < 26; i++) stars += sparkle(rnd() * HW, rnd() * HH * 0.8, 6 + rnd() * 14, i % 4 ? '#fff' : '#FFE08A', 0.5 + rnd() * 0.5);
  await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${HW}" height="${HH}">
    <defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8ED2FB"/><stop offset="1" stop-color="#4FA8EA"/></linearGradient>${grain}
    <radialGradient id="glow" cx="0.2" cy="0.5" r="0.4"><stop offset="0" stop-color="#fff" stop-opacity="0.6"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient></defs>
    <rect width="${HW}" height="${HH}" fill="url(#bg)"/><rect width="${HW}" height="${HH}" fill="#fff" filter="url(#g)"/>
    ${burst}<rect width="${HW}" height="${HH}" fill="url(#glow)"/>${stars}
    <path d="M-20 590 Q300 520 620 585 Q900 640 1220 560 L1220 660 L-20 660 Z" fill="#B7E3FF"/>
    <path d="M-20 590 Q300 520 620 585 Q900 640 1220 560" fill="none" stroke="${INK}" stroke-width="9"/></svg>`))
    .png().toFile(path.join(ART, 'hero_bg.png'));
  console.log('ok hero_bg');

  // A spiky sticker badge («Урок дня», like «Most Popular»).
  let pts = '';
  for (let i = 0; i < 28; i++) {
    const a = (i / 28) * Math.PI * 2 - Math.PI / 2, r = i % 2 ? 118 : 150;
    pts += `${(160 + Math.cos(a) * r).toFixed(1)},${(160 + Math.sin(a) * r * 0.62).toFixed(1)} `;
  }
  await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="320" height="320">
    <polygon points="${pts}" fill="#B83E60" transform="translate(0 10)"/>
    <polygon points="${pts}" fill="#F06C8F" stroke="${INK}" stroke-width="8" stroke-linejoin="round"/></svg>`))
    .png().toFile(path.join(ART, 'badge_burst.png'));
  console.log('ok badge_burst');

  // «Hi!» lines by a waving hand.
  await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="160" height="160">
    <path d="M40 70 Q30 40 46 18 M74 58 Q74 30 92 12 M100 80 Q118 60 144 56" fill="none" stroke="${INK}" stroke-width="11" stroke-linecap="round"/></svg>`))
    .png().toFile(path.join(ART, 'hi_lines.png'));
  console.log('ok hi_lines');

  // A tileable scatter of small sparkles and dots for plain cards and bands.
  seed = 41;
  let pat = '';
  for (let i = 0; i < 14; i++) {
    const x = rnd() * 256, y = rnd() * 256;
    pat += i % 3 ? `<circle cx="${x}" cy="${y}" r="${3 + rnd() * 4}" fill="#fff"/>` : sparkle(x, y, 8 + rnd() * 6, '#fff', 1);
  }
  await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256">${pat}</svg>`))
    .png().toFile(path.join(ART, 'pattern_sparkles.png'));
  console.log('ok pattern_sparkles');
})().catch((e) => { console.error(e); process.exit(1); });
