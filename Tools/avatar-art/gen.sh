#!/bin/bash
# 2D crayon picture-book style (the Speech Blubs look). usage: gen.sh name aspect prompt
cd "$(dirname "$0")"
mkdir -p raw
name="$1"; ar="$2"; p="$3"
STYLE="Children's picture book illustration, hand drawn with crayons and colored pencils: thick slightly wobbly dark charcoal outlines, soft pastel colors filled with visible grainy crayon texture, flat 2D, almost no shading, simple cute faces with small dot eyes and pink cheeks. No text, no letters."
case "$name" in
  bg_*|story_*) suf="$STYLE Soft painterly crayon background scene, gentle light.";;
  *) suf="$STYLE Every item separate and fully visible, nothing overlapping, isolated on a plain pure white background, no shadows.";;
esac
[ -s "raw/$name.png" ] && { echo "have $name"; exit 0; }
for attempt in 1 2 3; do
  out=$(higgsfield generate create z_image --prompt "$p. $suf" --aspect_ratio "$ar" --wait 2>&1)
  url=$(echo "$out" | grep -o 'https://[^ ]*\.png' | tail -1)
  if [ -n "$url" ]; then curl -s -o "raw/$name.png" "$url" && echo "ok $name"; exit 0; fi
  echo "retry $name: $(echo "$out" | tail -1)"
  sleep 10
done
echo "FAIL $name"
