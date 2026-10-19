# Transparent Naqla logo

Request: remove the black background from the application's supplied logo.

- Input: `../LOGO.png` (original retained unchanged).
- Output: `Naqla-logo-transparent.png`, 1254 × 1254 RGBA with transparent corners.
- Tool: built-in `image_gen.imagegen`, edit mode, `transparent_background: true`.
- Consumers: generated multi-resolution `Naqla.ico`, app/tray/setup resources,
  and `../preview/LOGO.png` copied by `scripts/update-icons.ps1`.

Final edit prompt:

> Edit target: the provided existing Naqla logo PNG. Use case: background-extraction.
> Remove ONLY its black canvas/background and make that background genuinely
> transparent with an alpha channel, including the open background spaces inside the
> orbit. Preserve the existing exact gold/amber document, curved conversion arrow/orbit,
> white pixel cursor, all silhouettes, dark outlines, shading, bevels, proportions and
> placement. Preserve the dark interior details of the logo itself (document's three
> dark lines and borders); do not erase them as background. Keep a clean antialiased
> cutout and only a restrained translucent amber glow immediately around the object.
> No redesign, no new shapes, no text, no new backdrop, no black rectangle, no white
> matte, and no checkerboard baked into the pixels. Keep the complete logo uncut and
> centered on a square transparent canvas.
