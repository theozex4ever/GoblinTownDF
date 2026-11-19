# Reference-led Grunt study

## Direction

Theo supplied `control-room-reference.png` as the overall aesthetic target: warm fantasy pixel art, detailed stone and timber, rich foliage, amber lights and small goblin inhabitants. Its goblins have rounded yellow-green faces, broad sideways ears and compact bodies. The reference's distorted anatomy should be corrected, not copied.

The control room depicts a mid-to-late-game society. Starting goblins are rough versions of those inhabitants. Over time, blessings of knowledge let them develop a society on top of the chaos. This is long-term visual direction; the source, timing and gameplay rules of those blessings are unresolved.

For the starting Grunt, the art test uses a ragged russet tunic, a rope belt, plain shorts and bare feet. The face and ears preserve a family resemblance to the reference inhabitants. Later clothes, tools and buildings can express accumulated knowledge; exact stages and assets are not yet specified.

## Files

- `control-room-reference.png`: the supplied visual reference, copied unchanged.
- `grunt-idle-source.png`: preliminary reference-led generated concept with more developed clothes.
- `grunt-idle.png`: its preliminary 32×32 export.
- `grunt-idle-preview.png`: that export enlarged with nearest-neighbour scaling.
- `grunt-idle-early-source.png`: revised generated starting Grunt with ragged clothes and bare feet.
- `grunt-idle-early.png`: selected starting Grunt export, 32×32 RGBA, one static idle pose, binary alpha, 27 colours including transparency.
- `grunt-idle-early-preview.png`: the exact selected sprite enlarged to 512×512 using nearest-neighbour scaling, without added detail.

Both concepts were generated with the built-in `image_gen` tool. The tool returned high-resolution images, not native 32×32 bitmaps. ImageMagick prepared the small PNGs. The selected early export thresholds alpha before trimming, pads by 24 source pixels, shifts the sampling alignment by 20 source pixels horizontally and 10 vertically, resizes proportionally to fit 28×30, and centres the result on a 32×32 transparent canvas. Point sampling and palette reduction preserve hard pixel edges. Several sampling alignments were visually compared because a direct reduction lost the eye highlights and facial features.

Dimensions and binary alpha were checked. The selected export was visually inspected enlarged. The selected early PNG has since been copied to `game/art/goblins/grunt-idle.png` for Theo's requested in-game check. The renderer draws it inside the existing 18-pixel overview tiles with nearest-neighbour filtering. Render checks cover its palette, transparency, starvation tint and the existing carried-item and status overlays; judging its appearance and readability in play is still a human check. These source experiments remain outside the game and sim; the runtime uses the copied asset. The earlier woodcut-style test remains intact in the parent directory.

## Reference-led concept prompt

Use case: stylized-concept.
Asset type: one original static 32x32 Grunt goblin sprite for a top-down colony simulation.
Input image: the attached Goblin Town control-room screenshot is a STYLE REFERENCE ONLY. Study the tiny goblins on the town paths and in the lower-left agent list very carefully. The user likes this overall aesthetic and these goblin designs, but wants their distorted anatomy corrected. Do not reproduce the screenshot, user interface, buildings or text.

Primary request: a single full-body idle goblin sprite that fits that reference's charming, warm, richly shaded fantasy pixel-art town. Character seen slightly from above and from the front, suitable for a square overhead map. ONE goblin, ONE idle pose, empty hands, no animation sheet.
Character design: rounded broad yellow-olive green head with cheeks, two pointed ears sticking SIDEWAYS, small broad nose, two clean distinct dark eyes with tiny pale glints, expressive arched brows, small cheeky closed-mouth smile with at most one tiny tooth. Head slightly oversized but a clearly visible compact body below it: neck, shoulders, rust-brown short work tunic or leather apron, worn dark trousers, small brass belt buckle, TWO arms with distinct mitten-shaped hands at sides and TWO short legs with separated brown boots. Slight proud, earnest stance. Anatomy must be coherent, face balanced, hands separate from torso. No fused limbs, extra limbs, uneven eyes or melted outlines. No enormous horns, no huge pointed chin, no angry monster expression.
Proportions on a STRICT 32x32 logical pixel canvas: character about 24 pixels wide with ears, 27 pixels tall, centered with transparent margin. Head INCLUDING ears 22 pixels wide, face about 12 pixels wide and 10 pixels tall. Ears smaller than face and roughly horizontal. Torso about 10 pixels wide, about 8 pixels tall, plus 4 pixels of legs and boots. Both boots have distinct silhouettes. Feet centered on the same ground line.
Style: compact classic fantasy colony-game pixel art matching the reference. Clean dark warm-brown contour, moss and golden chartreuse skin with olive shadows, honey highlights, reddish-brown clothes and brass accent. Pixel clusters should suggest warm light from upper left, with NO cast ground shadow. Sparse deliberate shading, approximately 16 to 24 flat palette colors. Prioritize face, ears, tunic, hands and boots reading clearly at NATIVE 32x32.
Hard constraints: EXACTLY 32x32 logical pixels, every logical pixel a uniform square with NO finer details within the square. If the output must be bigger, provide only a nearest-neighbor enlarged rendition of that 32x32 bitmap. TRUE transparent background. No scene, no floor, no labels, no grid, no border, no logos, no text, no decorative frame. No painterly blur, no gradients, no smoothing, no tiny high-resolution stitching. This is the actual single idle sprite, not an illustration or presentation sheet.

## Early Grunt revision prompt

Use case: stylized-concept. Asset type: ONE static idle starting Grunt goblin sprite for a 32x32 top-down colony game.
Input image 1 is an AESTHETIC REFERENCE: a mid-to-late-game goblin society. Input image 2 is a preliminary character design to simplify and correct, not final anatomy.
The user clarifies that the starting goblins are rough ancestors of the society in image 1. Preserve family resemblance: round olive-green face, broad sideways pointed ears, small broad nose, cheeky earnest expression. But early goblin should be scruffy, poorly equipped, wearing a single crudely tied ragged dull russet hide tunic with rope belt, simple brown cloth shorts, BARE FEET. No brass buckles, armor, epaulettes, fancy clothing, gemstones, helmet or tools. Hands empty.
IMPORTANT ANATOMY CORRECTION: exactly TWO green arms, ONE on each side, each ending in ONE green mitten hand near the hips. Shoulders are plain tunic fabric. No tan shapes or finger-like decorations at shoulders. Exactly TWO short legs and TWO clearly separated flat green feet, aligned to the same ground line. Balanced upright stance, visible torso and limbs. The head accounts for about 40 percent of character height, NOT a giant baby head. Two dark eyes, one tiny pale glint each, at a consistent height; a small nose; simple closed mouth with just a hint of a crooked smile, NOT an open dark hole.
STRICTLY native 32-by-32-pixel design, presented enlarged if needed. 32 rows by 32 columns of IDENTICAL LARGE SQUARE PIXEL BLOCKS only. For a 1280 canvas every pixel block is exactly 40x40. Absolutely NO finer details, no gradients within blocks, no antialiasing. At most 16 flat palette colors, with brown-black outline, olive shadows, muted yellow-green skin and warm russet cloth. Eyes must survive at 32 pixels: each eye is at least a 2x2 dark logical pixel cluster with one pale logical pixel, both equal sized. Character overall 25 logical pixels tall with 3 transparent pixels above and 4 below, head including ears 22 pixels wide, centered. Give him a squat adult goblin worker's proportions and a slightly front-facing overhead map view. Clean simple contours, deliberate clusters, sparse warm upper-left shading, affectionate scruffy character.
Create one full-body idle sprite ONLY on TRUE TRANSPARENT background. No scene, floor, shadow, text, frame, labels, grid, or animation sheet. This is an actual small sprite, not a fantasy illustration. The appearance should feel like the rough beginnings of the reference's goblin civilization.
