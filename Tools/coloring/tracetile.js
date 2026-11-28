// The home's «Обведи» card: the heart page half traced — the pink crayon line along the
// first part of its path, the dotted rest, the green dot and the pointing hand.
// usage: node tracetile.js   (after pages.js and icons.js)
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');
const fs = require('fs');
const path = require('path');

const ART = path.join(__dirname, '../../Assets/UI/Lessons/Art');
const PAGES = path.join(__dirname, '../../Assets/Resources/Coloring');

(async () => {
  const pages = JSON.parse(fs.readFileSync(path.join(PAGES, 'pages.json'), 'utf8')).pages;
  const p = pages.find((x) => x.id === 'heart');
  const W = p.width, H = p.height;
  const pts = [];
  for (let i = 0; i < p.trace.length; i += 2) pts.push([p.trace[i], p.trace[i + 1]]);
  const cut = Math.round(pts.length * 0.58);
  const done = pts.slice(0, cut + 1);
  const dots = pts.slice(cut).filter((_, i) => i % 3 === 0);
  const [dx, dy] = pts[cut];
  const svg = `<svg width="${W}" height="${H}">
    ${dots.map(([x, y]) => `<circle cx="${x}" cy="${y}" r="13" fill="#7B5BD6" fill-opacity="0.8"/>`).join('')}
    <polyline points="${done.map((q) => q.join(',')).join(' ')}" fill="none" stroke="#E0577B" stroke-width="40" stroke-linecap="round" stroke-linejoin="round"/>
    <circle cx="${dx}" cy="${dy}" r="40" fill="#3FB673" stroke="#2B2547" stroke-width="9"/>
    <circle cx="${dx}" cy="${dy}" r="15" fill="#fff"/>
  </svg>`;
  // Lines faded like while tracing.
  const lines = await sharp(path.join(PAGES, 'heart_lines.png')).ensureAlpha().raw().toBuffer();
  for (let i = 3; i < lines.length; i += 4) lines[i] = Math.round(lines[i] * 0.35);
  const faded = await sharp(lines, { raw: { width: W, height: H, channels: 4 } }).png().toBuffer();
  const handSize = 300;
  const hand = await sharp(path.join(ART, 'icon_hand.png')).resize(handSize, handSize).png().toBuffer();
  await sharp({ create: { width: W, height: H + 340, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
    .composite([
      { input: faded, left: 0, top: 0 },
      { input: Buffer.from(svg), left: 0, top: 0 },
      // The fingertip (0.39, 0.12 of the icon) on the dot.
      { input: hand, left: Math.round(dx - handSize * 0.39), top: Math.round(dy - handSize * 0.12) },
    ])
    .png().toBuffer()
    .then((b) => sharp(b).trim({ threshold: 1 }).resize(560, 560, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(path.join(ART, 'trace_tile.png')));
  console.log('ok trace_tile');
})().catch((e) => { console.error(e); process.exit(1); });
