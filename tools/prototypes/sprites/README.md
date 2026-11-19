# Grunt sprite art test

Historical woodcut-style idle-frame experiment. The runtime now uses the later [reference-led starting Grunt](reference-study/README.md), copied to `game/art/goblins/grunt-idle.png`; this earlier experiment remains separate from the game.

## Art direction

Working style: **scrapwood storybook**. Woodcut-inspired angular pixel clusters, an asymmetrical silhouette, moss-green skin, ochre brows, a crooked nose and one tooth. A patched rust-red tunic, plum outlines and muddy boots give the Grunt the appearance of an earnest, scruffy worker. The style is an experiment, not a final art bible.

The sprite is intended for the overhead square map with a slightly front-facing character. There are no animation frames, weapons or carried items in this test.

## Files

- `grunt-idle.png`: actual 32×32 RGBA sprite, 14 colours including transparency, with binary alpha.
- `grunt-idle-preview.png`: the exact sprite enlarged to 512×512 with nearest-neighbour scaling; no added detail.
- `grunt-idle-source.png`: selected high-resolution generated source, retained for reference. This is not the game-size sprite.

Generated with the built-in `image_gen` tool, followed by one targeted simplification edit. ImageMagick exported the selected image at exactly 32×32 with nearest-neighbour sampling, a binary alpha threshold and palette reduction to at most 16 colours. The large generated concept alone was not a native 32×32 sprite. The first reduction lost its eyes; the selected revision was simplified and visually checked after export.

This experiment was checked for dimensions, palette and alpha and was not selected for runtime use. The later reference-led sprite has in-game rendered checks for palette, transparency and overlays, plus 1×–4× camera zoom. The simulator imports no art. See the [runtime art guide](../../../game/art/README.md) for the current assets and known limits.

## Generation prompt

Use case: stylized-concept. Asset type: production game sprite, one static idle Grunt goblin for A Thousand Goblins Deep, an overhead square-grid colony simulation. Create ONE original goblin sprite, genuinely transparent background, no floor, no ground shadow, no scenery, no lettering, no border, no extra poses. Target EXACTLY 32 by 32 actual pixels with hard pixel edges, limited to about 12 flat colors, no antialiasing, no gradients, no tiny high-resolution detail. If the output canvas must be larger, the entire image must still represent exactly a 32x32 logical pixel grid uniformly enlarged with nearest-neighbor: every logical pixel is an identical solid square, aligned to the same grid. Character occupies roughly 26 pixels wide including ears and 28 pixels tall, centered with clear transparent margins. Viewing angle: overhead map but character shown slightly from the front, face and chest visible, approximately 3/4 down-facing, standing still with both feet visible. Original art direction: SCRAPWOOD STORYBOOK, a woodcut-inspired goblin made from bold angular pixel clusters. Moss and lichen green skin, enormous asymmetrical pointed ears, heavy ochre brow, long crooked nose, two small warm ivory eyes under the brow, tiny single off-center tooth, earnest stubborn expression. Head large, shoulders hunched, squat stocky body. Warm rust-red patched sleeveless work tunic, dark plum-brown belt and trousers, oversized muddy boots, one pale square repair patch. Bare green hands resting at sides, no held items or weapons. Deep aubergine outline with purposeful broken highlights, flat carved planes, asymmetric silhouette. Affectionate scruffy underdog, not scary, not glossy, not generic cute chibi, not a detailed fantasy illustration. Readable first as a goblin at 32 pixels. One idle frame only; no animation sheet. Actual game asset, not a mockup or concept sheet. Preserve real alpha transparency.

## Simplification prompt

Edit this goblin into a much lower resolution, truly production-ready 32x32 sprite. Keep the moss-green skin, asymmetrical huge pointed ears, rust-red tunic with single ivory repair patch, dark plum outline and trousers, muddy boots, crooked nose, single tooth, affectionate stubborn expression, idle pose and transparent background. CHANGE ONLY PIXEL COMPLEXITY: rebuild using EXACTLY a 32-column by 32-row pixel grid. Every output pixel block must be the same large size, absolutely no smaller details inside blocks. This is a 32x32 bitmap enlarged to display, NOT high resolution art that looks vaguely pixelated. Head about 16 logical pixels wide, ears extending to 28 logical pixels total width. Whole character 28 logical pixels tall within a 32x32 transparent canvas. Very large chunky blocks: each eye is ONE solid ivory square with ONE dark square brow, nose is a simple 2x3 green cluster, shirt patch is a 2x2 ivory square. No stitching, no tiny contour steps, no texture, no gradients, no soft lighting. At most 12 fully flat solid opaque colors plus transparent. Both eyes MUST remain visible at native 32px scale. Full body feet included, overhead-map slightly front-facing camera, bare empty hands at sides. One static idle sprite only. No text, no labels, no scene, no floor, no shadow, no grid lines. Real transparent background.
