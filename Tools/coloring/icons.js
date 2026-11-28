// Icons of the coloring screen (fill, crayon, undo, magic) in the app's sticker look — thick
// dark outline, flat colours with a crayon grain — and the home tile: the mosque page, half
// coloured. usage: node icons.js   (run pages.js first: the tile reads the mosque page)
const sharp = require('E:/Backends/Backend-Islam-Stories/node_modules/sharp');
const fs = require('fs');
const path = require('path');

const ART = path.join(__dirname, '../../Assets/UI/Lessons/Art');
const PAGES = path.join(__dirname, '../../Assets/Resources/Coloring');
const INK = '#2B2547';

// Crayon grain over the fills only (the outline stays clean).
const grain = `<filter id="g" x="0" y="0" width="100%" height="100%">
  <feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed="4" result="n"/>
  <feColorMatrix in="n" type="matrix" values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  0 0 0 -1.3 0.78" result="w"/>
  <feComposite in="w" in2="SourceGraphic" operator="in" result="wg"/>
  <feBlend in="SourceGraphic" in2="wg" mode="screen"/>
</filter>`;
const svg = (body) => `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256"><defs>${grain}</defs>${body}</svg>`;
const S = `stroke="${INK}" stroke-width="11" stroke-linejoin="round" stroke-linecap="round"`;

const ICONS = {
  // A paint bucket tipping, a drop falling from its lip.
  icon_bucket: svg(`
    <g transform="rotate(-18 128 128)">
      <path d="M58 92 L198 92 L182 214 Q128 232 74 214 Z" fill="#7FC8F2" filter="url(#g)"/>
      <path d="M58 92 L198 92 L182 214 Q128 232 74 214 Z" fill="none" ${S}/>
      <ellipse cx="128" cy="92" rx="70" ry="22" fill="#E8453C" filter="url(#g)"/>
      <ellipse cx="128" cy="92" rx="70" ry="22" fill="none" ${S}/>
      <path d="M70 96 Q128 10 186 96" fill="none" ${S}/>
    </g>
    <path d="M214 150 Q232 182 214 196 Q196 182 214 150 Z" fill="#E8453C" filter="url(#g)"/>
    <path d="M214 150 Q232 182 214 196 Q196 182 214 150 Z" fill="none" ${S}/>`),
  // A crayon lying diagonally, paper wrapper with two stripes.
  icon_crayon: svg(`
    <g transform="rotate(-45 128 128)">
      <path d="M40 104 L176 104 L176 152 L40 152 Z" fill="#FFD93B" filter="url(#g)"/>
      <path d="M176 104 L222 128 L176 152 Z" fill="#E8453C" filter="url(#g)"/>
      <path d="M64 104 L64 152 M150 104 L150 152" ${S}/>
      <path d="M40 104 L176 104 L222 128 L176 152 L40 152 Z" fill="none" ${S}/>
    </g>`),
  // Back-arrow curling round.
  icon_undo: svg(`
    <path d="M86 92 L196 92 Q232 92 232 134 Q232 176 196 176 L104 176" fill="none" stroke="${INK}" stroke-width="26" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="M100 48 L42 92 L100 136 Z" fill="${INK}" stroke="${INK}" stroke-width="14" stroke-linejoin="round"/>`),
  // A pointing hand for the «Обведи» hint; the fingertip is near (100, 30) — the builder pivots there.
  icon_hand: svg(`
    <g transform="rotate(-14 128 128)">
      <rect x="96" y="20" width="40" height="130" rx="20" fill="#FFD3B0" filter="url(#g)"/>
      <rect x="96" y="20" width="40" height="130" rx="20" fill="none" ${S}/>
      <rect x="92" y="108" width="112" height="104" rx="36" fill="#FFD3B0" filter="url(#g)"/>
      <rect x="92" y="108" width="112" height="104" rx="36" fill="none" ${S}/>
      <rect x="96" y="20" width="40" height="100" rx="20" fill="#FFD3B0"/>
      <path d="M136 112 L136 108" stroke="${INK}" stroke-width="11" stroke-linecap="round"/>
      <path d="M150 140 Q176 132 200 142 M150 168 Q176 160 200 170" fill="none" stroke="${INK}" stroke-width="8" stroke-linecap="round"/>
      <path d="M92 168 Q118 150 150 176" fill="none" stroke="${INK}" stroke-width="8" stroke-linecap="round"/>
      <rect x="104" y="206" width="88" height="34" rx="10" fill="#CDBEF6" filter="url(#g)"/>
      <rect x="104" y="206" width="88" height="34" rx="10" fill="none" ${S}/>
    </g>`),
  // A wand with a star and two sparkles.
  icon_magic: svg(`
    <path d="M60 214 L150 124" stroke="${INK}" stroke-width="30" stroke-linecap="round"/>
    <path d="M60 214 L150 124" stroke="#B45CFA" stroke-width="12" stroke-linecap="round"/>
    <path d="M176 36 L188 74 L228 76 L196 100 L208 138 L176 116 L144 138 L156 100 L124 76 L164 74 Z" fill="#FFD93B" filter="url(#g)"/>
    <path d="M176 36 L188 74 L228 76 L196 100 L208 138 L176 116 L144 138 L156 100 L124 76 L164 74 Z" fill="none" ${S}/>
    <path d="M62 60 L68 78 L86 84 L68 90 L62 108 L56 90 L38 84 L56 78 Z" fill="#FF8FB8" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>
    <path d="M222 176 L226 188 L238 192 L226 196 L222 208 L218 196 L206 192 L218 188 Z" fill="#7FC8F2" stroke="${INK}" stroke-width="7" stroke-linejoin="round"/>`),
};

async function tile() {
  // The mosque page: a few areas coloured with the drawing's own colours, the rest still white.
  const pages = JSON.parse(fs.readFileSync(path.join(PAGES, 'pages.json'), 'utf8')).pages;
  const p = pages.find((x) => x.id === 'mosque');
  const W = p.width, H = p.height;
  const reg = await sharp(fs.readFileSync(path.join(PAGES, 'mosque_regions.bytes'))).raw().toBuffer({ resolveWithObject: true });
  const ch = reg.info.channels;
  const lines = await sharp(path.join(PAGES, 'mosque_lines.png')).ensureAlpha().raw().toBuffer();
  // Coloured: the areas from the dome leftwards (dome, moon, left tower, door…) but not the walls.
  const sumX = new Float64Array(p.areas + 1), sumY = new Float64Array(p.areas + 1), n = new Float64Array(p.areas + 1);
  for (let i = 0; i < W * H; i++) { const k = reg.data[i * ch] | (reg.data[i * ch + 1] << 8); sumX[k] += i % W; sumY[k] += (i / W) | 0; n[k]++; }
  let wall = 2; for (let k = 2; k <= p.areas; k++) if (n[k] > n[wall]) wall = k;
  const coloured = (k) => k > 1 && k !== wall && sumX[k] / n[k] < 760;
  const out = Buffer.alloc(W * H * 4);
  for (let i = 0; i < W * H; i++) {
    const k = reg.data[i * ch] | (reg.data[i * ch + 1] << 8);
    let r = 255, g = 255, b = 255, a = k === 1 ? 0 : 255;
    if (coloured(k)) { const c = p.colors[k - 1]; r = parseInt(c.slice(1, 3), 16); g = parseInt(c.slice(3, 5), 16); b = parseInt(c.slice(5, 7), 16); }
    const la = lines[i * 4 + 3] / 255;
    out[i * 4] = r * (1 - la) + 0x2b * la; out[i * 4 + 1] = g * (1 - la) + 0x24 * la; out[i * 4 + 2] = b * (1 - la) + 0x33 * la;
    out[i * 4 + 3] = Math.max(a, lines[i * 4 + 3]);
  }
  await sharp(out, { raw: { width: W, height: H, channels: 4 } }).trim({ threshold: 1 }).resize(560, 560, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png().toFile(path.join(ART, 'coloring_tile.png'));
}

(async () => {
  for (const [name, s] of Object.entries(ICONS)) {
    await sharp(Buffer.from(s)).png().toFile(path.join(ART, name + '.png'));
    console.log('ok', name);
  }
  await tile();
  console.log('ok coloring_tile');
})().catch((e) => { console.error(e); process.exit(1); });
