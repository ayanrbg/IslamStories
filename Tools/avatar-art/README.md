# Avatar and lesson art (2D crayon stand-ins)

Generated with Higgsfield `z_image` in the Speech Blubs-like crayon style (prompts: `gen.sh` style
suffix + `list.txt`; the first three sheets were prompted inline, see `sheets/`). Stand-ins until
the artist draws the final set: bodies WITHOUT heads, heads with a chin point, a front layer
(collar / scarf front) per body if the artist provides one.

Pipeline (node + sharp):

1. `node split.js sheets/heads_kids.png cut headA 0.004 512` — separate items on white into PNGs.
2. `node cols.js sheets/bodies_sheet.png cut body 6` — characters touching each other: split by columns.
3. `node parts.js parts body:cut/body_1.png:body_boy ... head:cut/headA_03.png:head_g1 ...`
   — cuts the head off each body at the bottom of the face skin (override: `body:file:name:0.315`),
   records the neck point; for heads records the chin point; writes `parts.json`.
4. Copy `parts/*` to `Assets/UI/Lessons/Avatar2D/`, then **Nooria ▸ Lessons ▸ Build avatar screen**.
