# Placement panel readability follow-up

Native activity-prop placement succeeded, but the existing placement panel has pale cream labels on pale cream molding panels. Search exists already; do not add duplicate filtering. The factory source uses the legacy runtime font and a single pale text color for labels on both dark controls and light panels.

After the current content build and saved-bundle reload proof:
- Reproduce contrast/font behavior with factory-level checks. Inspect actual runtime label colors and search visibility before changing code.
- Keep the approved ComfyUI molding. Use the shared Korean-capable font for this panel, dark text on light panels, and retain light labels on dark buttons/input/dropdowns.
- Verify search/paging/selection behavior and actual pointer/typing search against the newly imported props. Do not change placement/occupancy/save/progression behavior.
- Check Game View contrast and controls, then restore QA defaults. No new raster assets or Addressables regrouping are needed for this readability fix.
