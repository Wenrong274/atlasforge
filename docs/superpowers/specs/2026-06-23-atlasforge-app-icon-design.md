# AtlasForge App Icon Design

## Objective

Create a production-ready Windows app icon that identifies AtlasForge as a
sprite-atlas packing tool while retaining the energy implied by "Forge".

## Approved Direction

Use the **Forge Grid** concept selected by the user:

- A compact 3 × 3 atlas grid is the primary symbol.
- Eight cells use the existing UI accent cyan (`#38BDF8`).
- The lower-right cell uses forge orange (`#F97316`) as the focal point.
- A small four-point spark rises from the orange cell to suggest forging and
  export completion.
- The mark sits on a dark navy rounded square derived from the app's dark UI.

## Visual Language

- Style: minimal, flat, vector-like, geometric.
- Palette: dark navy (`#09111F` to `#16243A`), cyan (`#38BDF8`), forge orange
  (`#F97316`), and optional warm highlight (`#FBBF24`).
- Composition: centered, symmetrical grid with generous outer padding.
- Silhouette: strong at 16–32 px; the grid and orange corner remain distinct.
- Surface: subtle depth is acceptable only on the navy base. Grid cells stay
  crisp and largely flat.

## Constraints

- No letters, wordmark, text, watermark, mockup, or background scene.
- No hammer or anvil; these compete with the atlas-grid meaning at small sizes.
- No thin outlines or details that disappear below 32 px.
- No visual similarity to an existing product or trademark.
- Keep all important geometry inside an 80% safe area.

## Deliverables

- `src/AtlasForge/Assets/AppIcon.png`: 1024 × 1024 RGBA master.
- `src/AtlasForge/Assets/AppIcon.ico`: Windows multi-size icon containing
  256, 128, 64, 48, 32, 24, and 16 px variants.
- Transparent pixels outside the rounded-square base.
- WPF project metadata updated to use the `.ico` asset.

## Validation

- Inspect the master image at full size for clean geometry and color balance.
- Inspect 256, 48, 32, and 16 px variants for silhouette and contrast.
- Confirm the PNG has an alpha channel and transparent corners.
- Confirm the ICO exposes all required sizes and the project builds with it.
