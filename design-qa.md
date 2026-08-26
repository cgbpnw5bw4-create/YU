# Design QA

## Comparison target

- Source visual truth: `design/reference/selected-cool-office-blueprint.png`
- Rendered implementation: `artifacts/golden/game-golden.png`
- Full-view comparison: `artifacts/screenshots/design-comparison-final.png`
- Native-window evidence: `artifacts/screenshots/game-focused-perf-final-build-150dpi.png`
- Source pixels: `1586×992`; implementation pixels: `256×160`
- Native viewport: fixed `256×160 DIP` (`16px` HUD + `256×144` playfield); CSS size is not applicable to WinForms.
- Density normalization: the source was resized to `512×320` with high-quality sampling; the implementation was enlarged exactly 2× with nearest-neighbor sampling. Both panels therefore use the same `512×320` comparison frame and approximately the same `1.6:1` aspect.
- State: map 1, 42 seconds remaining, one folder collected, player crouched in lower-left cover, partial detection meter, locked exit.

## Findings

- No actionable P0, P1, or P2 visual mismatch remains.
- [P3] The three production maps use clearer serpentine office aisles and fewer open rooms than the illustrative reference. This is an intentional gameplay constraint: the fixed `16×9` grid must also provide 28–36 second walking routes, deterministic guard windows and readable collision at `256×160`. The navy office hierarchy, partitions, equipment, folders, characters and sight cones preserve the selected art direction.
- [P3] The source uses a bespoke block-pixel display face; the implementation uses `Consolas` with a monospace fallback and `Microsoft YaHei UI` for the clock date. At the logical 1× canvas the hierarchy and wrapping match, but the exact source font is not distributed with the project.

## Required fidelity surfaces

- Fonts and typography: timer, overlays and score text use hard-edged single-bit rendering, bold optical weights and no wrapping or truncation. The clock date has a Chinese-capable fallback. The 16px HUD omits long labels in favor of outlined icons and bars so text does not become cramped.
- Spacing and layout rhythm: frame ratio, 16px HUD, map crop and edge-to-edge blueprint grid match the target. HUD groups remain separated; overlays stay centered and do not cover persistent edge feedback. No clipping is visible at the fixed canvas or in the real 150% DPI capture.
- Colors and tokens: deep navy floor/walls, slate outlines, cyan player/stamina, yellow folders, coral guard vision/detection and green unlocked exit map directly to the source palette. Foreground/background contrast remains strong.
- Image quality and asset fidelity: player, guard, office equipment, folders and exit use generated raster assets, not emoji, SVG substitutes or placeholder shapes. The embedded atlases use fixed `16×16` cells, nearest-neighbor reduction, binary alpha and aspect-preserving centered drawing; no transparency halos or soft scaling remain.
- Copy and content: `READY / 1 SEC / WASD`, `CLEAR`, `FAILED`, `ENTER NEXT` and `ENTER RETRY` are short, coherent at this resolution and do not leak implementation prompts. Clock mode contains only true local time/date.
- Icons and state signals: collected/uncollected folders change fill and outline; detection is segmented; cover uses a dotted outline; catch/grace use distinct solid/dotted borders; locked/unlocked exits use different raster symbols and outlines. Information is not encoded by color alone.
- Interaction and accessibility: all core actions are keyboard-reachable with WASD/Shift/Ctrl/Esc/Enter, Ctrl wins over Shift, focus loss clears keys, and Esc auto-repeat is latched. High-contrast outlines and pattern changes support color-deficient users. Fixed-pixel scaling uses nearest neighbor; there are no hover-only or mouse-only core actions.

## Evidence

- Full-view comparison evidence: `artifacts/screenshots/design-comparison-final.png` shows both source and implementation in one `1024×320` image.
- Focused-region comparison: a separate crop was not needed. The implementation is only `256×160` and is displayed at an exact 2× scale in the combined image, so every HUD segment, sprite and one-pixel outline is directly inspectable. `game-focused-perf-final-build-150dpi.png` provides the additional `384×240` physical-pixel check at 150% DPI.

## Comparison history

- Pass 1 evidence: `artifacts/screenshots/design-comparison-pass1.png`.
  - Earlier P1: the generated high-resolution sheets were reduced with bicubic sampling, retained many half-transparent edge pixels, then tight-cropped and stretched per sprite.
  - Fix: changed asset preparation to nearest-neighbor plus binary alpha; changed runtime atlas sampling to fixed cells with aspect-preserving centering; enlarged character destinations without distortion.
- Pass 2 evidence: `artifacts/screenshots/design-comparison-final.png`.
  - Post-fix result: pixel edges are crisp, character/guard proportions are stable, folders and equipment retain their intended aspect, and no P0/P1/P2 visual issue remains.

## Open questions

- None blocking. More varied room furnishing would be optional future polish, not a correction required by the current fixed-grid gameplay brief.

## Implementation checklist

- [x] Match the cold office-blueprint palette and hierarchy.
- [x] Embed generated character and office assets through `.resx`.
- [x] Preserve hard pixel edges at 1× and scaled window sizes.
- [x] Verify Clock, Ready and Playing states in a real WinForms window.
- [x] Compare the source and implementation in a single normalized image.

## Follow-up polish

- Optional P3: add a fourth decorative tile variant per wall run while preserving collision and route timing.

final result: passed
