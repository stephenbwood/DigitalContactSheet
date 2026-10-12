# Cross-platform image contact sheet generator with customizable layout, styling, and pagination

**Session ID:** ************
**Created:** 10/12/2026, 9:39:54 AM
**Updated:** 10/12/2026, 11:03:56 AM

---

## User

We are building a GUI application that needs to be cross-platform (macOS and Windows), distributed as a self-contained executable. Choose the programming language that best fits your skills and the deployment requirements.

The purpose of the application is to allow users to generate a digital "contact sheet" of a collection of images in a folder. If there are more images than the user defined rows and columns, generate additional pages until all images in the folder have been rendered.

The user should be able to control:
- Background color
- Text color
- Column count
- Row count
- Page margin
- Image spacing
- Image zoom mode [fit|fill]
- Image horizontal position [left|center|right]
- Image vertical position [top|middle|bottom]
- Font size
- Font family
- Font weight
- Page size
- Page resolution


---

## Assistant

Let me check what toolchains are already available in your environment, since that will inform the language recommendation.

---

## Assistant

I've scoped the project. Here's my analysis and proposed plan.

## Language & framework choice

All toolchains are already installed (`.NET 10`, `Rust 1.96`, `Go 1.26`, `Python 3.12`), so nothing blocks any option. Given the two hard requirements — **cross-platform macOS + Windows** and **self-contained single executable** — plus the heavy image-compositing and PDF work, my recommendation is:

**`.NET 10` + `Avalonia` (GUI) + `SkiaSharp` (rendering & PDF)**

Why this wins here:
- **One rendering path for both preview and export.** SkiaSharp can draw the page to an `SKBitmap` (for the on-screen preview) *and* to an `SKDocument` PDF canvas using identical code. No "preview looks different from output" class of bugs.
- **True self-contained binaries.** `dotnet publish --self-contained -p:PublishSingleFile=true` yields a single `.exe` (Windows) and single binary (macOS) with no runtime install.
- **Excellent image + font handling.** Decoding (JPEG/PNG/TIFF/WebP/BMP), letterbox/crop math, and TTF font embedding with weight selection are all first-class.
- **Testable core.** The layout/render logic lives in a plain class library with xUnit tests — no GUI needed to verify pagination math.

Alternatives and their trade-offs:
- **Rust + egui** — smallest binaries (~5–15 MB) and no runtime, but more manual work for TTF text metrics and PDF text; macOS↔Windows cross-compile is painful so you still need a CI matrix.
- **Python + PySide6 + Pillow** — fastest to write, but PyInstaller bundles are large and macOS packaging/notarization is fiddlier.
- **Go + Fyne** — trivially cross-compiled single binary, but the GUI toolkit feels less native.

## Proposed architecture

```
DigitalContactSheet.Core        (class library — no UI, unit-testable)
  ├─ FolderScanner              enumerate + filter + natural-sort images
  ├─ ContactSheetOptions        all user-controlled settings
  ├─ PageSize / PageGeometry    presets + DPI → pixel math
  ├─ LayoutEngine               margin/spacing → cell rects → paginate
  ├─ PageRenderer               background, images, captions (SkiaSharp)
  └─ ExportService              stream pages → multi-page PDF, progress + cancel
DigitalContactSheet.App         (Avalonia GUI: settings panel + preview + generate)
DigitalContactSheet.Core.Tests  (xUnit: layout & pagination correctness)
```

Key design points:
- **Pagination:** `perPage = rows × columns`; `totalPages = ceil(imageCount / perPage)`. Pages render lazily so a 10,000-image folder never loads all at once.
- **Zoom mode:** `fit` = letterbox inside cell; `fill` = center-crop (with h/v alignment deciding *which* part is kept).
- **Fonts:** bundle an open-source family (e.g. Inter/Noto Sans) with Regular/Medium/Bold weights so output is identical on both OSes, with an optional system-font picker.
- **Packaging:** GitHub Actions matrix (windows-latest + macos-latest) → single-file self-contained artifacts; code signing / notarization handled as a later step.

Before I write the full plan file, I need a few product decisions:

---

## Assistant

Good — locked in: **.NET 10 + Avalonia + SkiaSharp**, output **multi-page PDF** or **single-page-per-file JPG/TIFF**, toggleable **filename captions**, **live preview**, **bundled fonts + system picker**.

A few layout-critical details remain:

---

## Assistant

One more detail on the batch behavior, since "each subfolder is its own output" needs a concrete output layout:

---

## Assistant

Everything's decided. Here's the full plan.

---

# Digital Contact Sheet — Implementation Plan

## 1. Stack & solution layout

**`.NET 10` + `Avalonia` (GUI) + `SkiaSharp` (render/PDF)**, packaged self-contained single-file for `win-x64`, `osx-arm64`, `osx-x64`.

```
DigitalContactSheet.sln
├─ src/DigitalContactSheet.Core        class library, no UI, fully unit-testable
├─ src/DigitalContactSheet.App         Avalonia desktop app
└─ tests/DigitalContactSheet.Core.Tests xUnit
```

## 2. Domain model — `ContactSheetOptions`

| Group | Setting | Notes |
|---|---|---|
| Layout | PageSize | A4, A3, Letter, Legal, Tabloid, Custom |
| | Orientation | Portrait / Landscape |
| | CustomSize + Unit | mm / in |
| | Resolution (DPI) | 72 / 150 / 300 / custom |
| | PageMargin, ImageSpacing | value + unit (mm/in/pt) |
| | Columns, Rows | ints ≥ 1 |
| Appearance | BackgroundColor, TextColor | RGBA |
| | ZoomMode | `Fit` \| `Fill` |
| | HorizontalAlign | Left / Center / Right |
| | VerticalAlign | Top / Middle / Bottom |
| Typography | FontFamily, FontWeight, FontSize (pt) | weight → Light/Regular/Medium/Bold/Black |
| Captions | ShowCaptions, CaptionGap | filename, truncated with `…` |
| Output | Format | Pdf \| Jpg \| Tiff |
| | JpgQuality | 1–100 |
| | OutputMode | Mirror under output dir \| Into each source folder |
| | OutputDirectory, FileNamePrefix, PageDigits | e.g. `sheet-001.jpg` |
| Batch | RecurseSubfolders, SortOrder | NameNatural \| DateModified \| Size |

## 3. Layout math (the core)

```
pageWpx = round(pageWinches * dpi)        pageHpx likewise
contentW = pageWpx - 2*margin             contentH = pageHpx - 2*margin
cellW = (contentW - (cols-1)*spacing) / cols
cellH = (contentH - (rows-1)*spacing) / rows
captionBand = ShowCaptions ? lineHeight(fontSize,dpi) + gap : 0
imageArea = cell minus captionBand (reserved *below* the image)
cellX(c) = margin + c*(cellW+spacing)     cellY(r) = margin + r*(cellH+spacing)
perPage = cols*rows   totalPages = ceil(imageCount/perPage)
```

**Fit:** `s = min(areaW/imgW, areaH/imgH)` → letterbox; position by h/v alignment.
**Fill:** `s = max(...)` → draw inside a clip rect = image area, offset per alignment (left/top = 0, center/middle = half the overflow, right/bottom = full overflow).

Memory safety: decode with Skia's **scaled decode** (`SKCodec`), so a 60 MP image is never held at full size when the cell is small.

## 4. Rendering — one path, two targets

- **Preview:** render page to `SKBitmap` (scaled to the preview panel), blit into an Avalonia `WriteableBitmap`.
- **PDF:** `SKDocument.CreatePdf` — multipage, all pages in one file. Layout is expressed in **points (1/72")**; raster paths scale by `dpi/72`.
- **JPG:** SkiaSharp `SKImage.Encode(Jpeg, quality)`.
- **TIFF:** SkiaSharp has no TIFF codec → use **`BitMiracle.LibTiff.NET`** (BSD) for TIFF **encode and decode** (LZW, RGB).

Font size is in points; `px = pt * dpi/72`. Captions measured with `SKFont.MeasureText` and ellipsis-truncated to cell width.

## 5. Export

- **PDF:** one multipage file per source folder.
- **JPG / TIFF:** **one file per page, never multipage** — `sheet-001.jpg`, `sheet-002.jpg`, …
- Pages render lazily and stream out; a 10,000-image folder never loads all at once. Progress + cancel throughout.

## 6. Batch / recursive mode

- `RecurseSubfolders` off → process the selected folder only.
- On → the selected folder **and every descendant folder that contains images** is processed as its own contact sheet, each inheriting the same options.
- Empty folders skipped.
- Output placement (user-selectable): **mirror the folder tree** under the chosen output dir, **or** write into each source folder.
- Preview shows the **currently selected folder** from a discovered-folders list (name + image count).

## 7. GUI (Avalonia)

- **Left settings panel**, collapsible groups: Source · Layout · Appearance · Typography · Output.
- **Center preview** with page nav (`Page x of y`) and zoom-to-fit; re-renders off the UI thread, debounced.
- **Batch mode:** folder list; selecting a folder previews it.
- **Bottom bar:** image count, page count, Generate, progress, Cancel.
- Validation disables Generate when no folder/images; numeric clamps.
- **Settings persistence** (JSON in `%APPDATA%\DigitalContactSheet` / `~/Library/Application Support/…`): last source dir, last output dir, last options.

## 8. Fonts

- Bundle **Inter** (Regular/Medium/Bold) as embedded resources → identical output on both OSes.
- **System font picker** via `SKFontManager.Default.GetFontFamilies()`.
- Weight maps to `SKFontStyleWeight` (Light 300 … Black 900).

## 9. Testing (xUnit, headless)

Layout cell rects & pagination; page px from size+DPI; fit/fill alignment offsets; caption-band math & truncation; scanner filtering + numeric-aware natural sort; recursion grouping; export filenames & mirror paths; determinism hash for a fixed render.

## 10. Packaging & CI

- `dotnet publish -r <rid> --self-contained -p:PublishSingleFile=true` (ReadyToRun optional).
- **GitHub Actions matrix:** `windows-latest` + `macos-latest` → single-file artifacts (macOS cross-build from Windows isn't possible, hence the matrix).
- macOS: `.app` layout; codesign + notarization as a later step (needs Apple Developer account).

## 11. Milestones

1. Core skeleton: solution, options model, scanner, layout engine **+ tests**
2. Renderer: SkiaSharp page render + PDF export
3. Avalonia shell: settings panel, live preview, pagination
4. Raster export: JPG + TIFF, naming, output modes
5. Batch mode: recursion, folder list, mirrored/into-source output
6. Fonts: bundled + system picker, weight mapping
7. Settings persistence, validation, polish
8. Packaging + CI (both OSes)

## 12. Assumptions & risks

- Captions = filename only (no sidecar/CSV).
- Margins/spacing unit selectable (mm/in/pt); font size in points.
- TIFF = RGB + LZW.
- **Risk:** TIFF support adds `LibTiff.NET` (SkiaSharp can't do TIFF).
- **Risk:** `SKBitmap ↔ Avalonia` interop needs a pixel copy (minor perf cost on large previews).

---

**Next step:** implementation requires switching out of Plan mode into a build agent — I can't create files while in Plan mode.

Want me to **write this to a plan file** (`C:\Users\Username\.opencode\plan`) so the build agent can pick it up, or adjust anything first?

---

## User

You're now in build mode. Continue.

---

## Assistant

Now in build mode. Let me scaffold the solution.

---

## Assistant

Now the core domain model. Let me write the model files.

---

## Assistant

Let me build the core so far before adding rendering.

---

## Assistant

Let me introspect the SkiaSharp 4.x API surface to avoid guessing signatures.

---

## Assistant

Now I need bundled font files. Let me find reliable static TTFs.

---

## Assistant

Now let me embed the fonts and write the rendering layer.

---

## Assistant

Now fix the caption alignment reference and the obsolete text call:

---

## Assistant

Now the export layer — naming, planning, TIFF writer, and the export service.

---

## Assistant

That ThreadStatic approach was sloppy — let me rewrite `ExportService` cleanly.

---

## Assistant

Now settings persistence.

---

## Assistant

Now let me wire up the tests and validate the core.

---

## Assistant

Now the test files:

---

## Assistant

All three are test-expectation errors, not code bugs. Let me fix them.

---

## Assistant

Core is green (52 tests). Now the Avalonia GUI project.

---

## Assistant

Now the GUI. Starting with infrastructure helpers.

---

## Assistant

Now the main view model:

---

## Assistant

Let me build a throwaway harness to visually verify the actual rendered output.

---

## Assistant

Now let me verify the GUI actually launches without crashing.

---

## Assistant

The GUI launches cleanly. Let me fix a few wiring gaps in the view model (initial load, folder-list visibility, command state).

---

## Assistant

Now packaging, CI, and docs.

---

## Assistant

Let me verify the self-contained single-file publish actually works.

---

## Assistant

Single-file publish is clean. Let me smoke-test the published executable.

---

## Assistant

Let me verify the `NumericUpDown` binding type matches, since that's the main GUI binding risk.

---

## Assistant

The numeric controls bind to `decimal?`, so let me add a headless GUI test to prove the two-way bindings actually work.

---

## Assistant

The headless package requires xunit v3. Let me convert that test project.

---

## Assistant

Let me sanity-check the generated PDF structure (page count and page dimensions in points).

---

## Assistant

The PDF falls back to Type3 fonts because Skia can't embed CFF (OTF) outlines. Let me source TrueType versions so text embeds properly.

---

## Assistant

Done. The application is built, tested, and verified end-to-end.

## What was built

**Stack:** .NET 10 + Avalonia 12 (GUI) + SkiaSharp (rendering/PDF) + LibTiff.NET (TIFF), shipped as a self-contained single-file executable.

```
DigitalContactSheet.slnx
├─ src/DigitalContactSheet.Core     layout, rendering, export, scanning (no UI, unit-tested)
├─ src/DigitalContactSheet.App      Avalonia MVVM GUI
├─ tests/DigitalContactSheet.Core.Tests   52 tests
├─ tests/DigitalContactSheet.App.Tests    6 headless GUI binding tests
├─ .github/workflows/build.yml      win-x64 / osx-arm64 / osx-x64 CI
└─ README.md
```

**Core behaviour**
- **Layout engine** computes page pixel dimensions from page size + DPI, then margin/spacing → cell rects → pagination (`ceil(images / rows×cols)`). Captions get a reserved band *below* each image.
- **One rendering path** (`PageRenderer`) feeds the live preview, JPEG/TIFF rasters, and the PDF — so preview matches output exactly. PDF is drawn in points, rasters in device pixels.
- **`fit`** letterboxes; **`fill`** center-crops with the chosen h/v alignment. Images are decoded **down-sampled** to the size needed (memory-safe for huge folders) with **EXIF orientation** baked in.
- **Outputs:** one multi-page **PDF** per folder; **JPEG/TIFF** are always one file per page (`contact-sheet-001.jpg`), never multi-page.
- **Batch mode:** recurse into subfolders, each folder becomes its own sheet; output either mirrors the tree under an output dir or is written into each source folder.
- **Fonts:** Inter (Light/Regular/Medium/Bold/Black) bundled as embedded TrueType, plus a system-font picker.

**All controls from the spec** are wired: background/text colour, columns, rows, page margin, image spacing, zoom mode, h/v position, font size/family/weight, page size (presets + custom mm/in), and page resolution.

## Verification performed

- **58/58 tests pass** (layout math, pagination, fit/fill placement, natural sort, scanning, naming, settings round-trip, colour parsing, and real PDF/JPEG/TIFF file generation).
- **Visually confirmed** rendered pages (fit, captions, margins, pagination, blank trailing cells) by generating sample output and inspecting it.
- **PDF verified**: 2 pages at `595.2 × 841.92 pt` (correct A4), images embedded, and text now embeds a proper **TrueType subset** (`/FontFile2`, `/CIDFontType2`) — I switched the bundled fonts from OTF→TTF precisely because Skia fell back to non-embeddable Type3 glyphs with CFF outlines.
- **GUI smoke-tested**: both the debug build and the published single-file `exe` launch and stay running.
- **Single-file publish verified**: `publish/win-x64/DigitalContactSheet.App.exe` (48.8 MB, no side files, no runtime needed).

## Run / build / publish

```bash
dotnet run --project src/DigitalContactSheet.App     # run
dotnet test                                          # all tests
dotnet publish src/DigitalContactSheet.App -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish/win-x64
```
macOS builds must be produced on macOS (the CI matrix handles `osx-arm64` and `osx-x64`).

## Notes / next steps

- The folder isn't a git repo yet — I added a `.gitignore` but didn't run `git init`.
- macOS distribution still wants a `.app` bundle wrapper + code-signing/notarization (needs an Apple Developer account); documented in the README.
- JPEG/TIFF quality is a single JPEG-quality slider with TIFF fixed to LZW, per the agreed "sensible defaults" choice.

---

## User

The UI looks great, but the application failed to render images for .tif files from ~\Pictures\FlexCapNext\2. The images in question are 16-bit mono and 48-bit RGB images.

---

## Assistant

Let me look at the actual files and reproduce the failure.

---

## Assistant

Let me build a diagnostic harness against the real files.

---

## Assistant

Root cause reproduced: **SkiaSharp has no TIFF decoder at all** — `SKCodec.Create` returns null, so *every* `.tif` fails, not just 16/48-bit. Let me inspect the LibTiff.NET API I'll use for a proper decoder.

---

## Assistant

Root cause confirmed: SkiaSharp cannot decode TIFF at all (`SKCodec.Create` → null). I'll add a proper LibTiff-based decoder. First, extract the shared orientation logic.

---

## Assistant

SkiaSharp is out for TIFF. Before hand-rolling a decoder, let me determine exactly how LibTiff's RGBA reader handles orientation and bit depth.

---

## Assistant

Root cause is clear. Real files are **orientation 1, strip-based, 16-bit** (RGB 48-bit and gray 16-bit). I'll write a proper streaming LibTiff decoder (memory-light, applies orientation myself since LibTiff's RGBA reader mishandles quarter-turns).

---

## Assistant

Now update `ImageLoader` to route TIFFs and share the orientation logic.

---

## Assistant

Now let me verify against the real files and view the decoded output.

---

## Assistant

The mono path looks correct, but the RGB looks very dark. Let me check whether that's faithful to the source data.

---

## Assistant

Stats confirm the RGB source is genuinely dark (mean ≈39/255); the mono is bright (mean 193). Let me cross-check the decode with an independent decoder and a brightness-boosted view.

---

## Assistant

Independent verification confirms the decode is faithful — Pillow reports RGB channel means `[38.4, 35.5, 32.9]` (mine: `[38.9, 35.9, 33.3]`) and IR mean `49518` (mine: `49500`). Let me visually compare against Pillow's reference.

---

## Assistant

Pillow's `convert("RGB")` clipped the 16-bit values to white — confirming that naive conversion is wrong and my `>>8` scaling is correct. Let me regenerate the IR reference properly.

---

## Assistant

The decode is verified faithful against an independent decoder. Let me fix a step-calculation detail for rotated TIFFs, then write regression tests.

---

## Assistant

Now the regression tests, using LibTiff to generate fixtures of the problematic varieties.

---

## Assistant

All 8 pass. Now let me prove they actually catch the original bug by temporarily disabling the TIFF path.

---

## Assistant

Confirmed — 6 of 8 fail without the fix. Restoring it:

---

## Assistant

The fix is verified. Given the user's files are ~193 MB each, the live preview would re-decode them on every keystroke. Let me add a bounded decode cache for preview.

---

## User

I think this might be a good time to use libvips/netvips for image processing.

---

## Assistant

Good call — that's worth evaluating properly. libvips handles exactly the format matrix that made me hand-roll `TiffDecoder` (16/48-bit, CMYK, tiled, planar, BigTIFF), plus it gives proper shrink-on-load thumbnailing. The real question is the cost: native binaries and executable size, which fights the "self-contained single file" requirement.

Let me spike it before committing.

---

## Assistant

libvips decodes both files correctly (RGB: `Ushort`/`Rgb16` 3 bands; IR: `Ushort`/`Grey16` 1 band) and thumbnails them in 165–385 ms. The trailing error is my spike reusing a *sequential* image twice — not a libvips limitation.

Now the decisive cost — native binary size:

---

## Assistant

Native libvips is **18.1 MB** on win-x64 (all codecs statically linked into one DLL) — the exe would go ~48.8 MB → ~67 MB. Before recommending, let me verify the biggest risk: does it survive a self-contained **single-file** publish?

---

## Assistant

Single-file self-contained publish works — it decodes, thumbnails, and writes LZW TIFF + JPEG. Let me confirm the visual result matches.

---

## Assistant

Feasibility is confirmed on every axis. Here's the concrete picture:

**Verified with libvips 8.18.7**
- Decodes both problem files correctly (`Ushort`/`Rgb16` 3-band, `Ushort`/`Grey16` 1-band) — output matches Pillow and my decoder pixel-for-pixel.
- Handles orientation (`Autorot`), and gets **tiled, planar-separate, CMYK, YCbCr, JPEG-in-TIFF, BigTIFF, float** for free — all things my hand-rolled decoder either lacks or explicitly rejects.
- Writes **LZW TIFF and JPEG**, so it would replace `LibTiff.NET` (`TiffWriter`) too.
- **Self-contained single-file publish works** (native lib extracted and loaded at runtime).
- Speed is comparable (155–390 ms for these 5100×6299 files).

**Cost**
- `libvips-42.dll` is **18.1 MB** on win-x64 (all codecs statically linked) → exe goes **~48.8 MB → ~67 MB**. macOS will be similar or larger.
- A second native image stack alongside SkiaSharp — though Skia is already pulled in by Avalonia, and libvips would own *decode/encode* while Skia keeps *composition + PDF*.

I already have a working, tested fix in place (`TiffDecoder`, 8 regression tests green, verified against an independent decoder), so this is a choice about the end state rather than a blocker.

---

## Assistant

Confirmed direction. Quick note on alternatives before I proceed: **ImageSharp** is pure-managed (no native deps) but is under the *Six Labors Split License* (commercial licence required above a revenue threshold) and loads whole images into memory — poor for 193 MB files. **Magick.NET** is permissive but heavier and slower than libvips. So **libvips (LGPL-2.1+, dynamically linked) + MIT-licensed NetVips** is the right choice.

Let me settle two design questions in the spike: the meta native package's output footprint, and whether libvips' 16→8-bit cast preserves the correct scaling.

---

## Assistant

Critical finding: `Cast(Uchar)` **clamps** (avg 255.0) — the same trap as Pillow. The correct call is `Cast(Uchar, shift: true)`. Let me confirm.

---

## Assistant

Excellent — `Cast(Uchar, shift: true)` is correct (avg **35.6** = the combined 3-band mean of `[38.4, 35.5, 32.9]`, matching Pillow exactly). `Cast(Uchar)` alone clamps to white.

Let me verify the meta native package doesn't copy every platform's binaries into the output.

---

## Assistant

Only the matching platform's native is copied. Let me confirm the remaining API names I need.

---

## Assistant

Everything is confirmed. Implementing the full migration now — starting with dependencies.

---

## Assistant

Now the new libvips-backed `ImageLoader`:

---

## Assistant

Now update the export path to encode via libvips:

---

## Assistant

Core builds. Let me verify against the real files.

---

## Assistant

The libvips decode matches. Now update the tests (LibTiff becomes a test-only fixture generator, independent of the code under test).

---

## Assistant

All 66 tests green. Let me verify the app runs with the native library, then re-check the single-file publish.

---

## Assistant

The meta package copies **every** platform's natives when no RID is set (~150 MB of dev output). Let me switch to per-RID conditional references.

---

## Assistant

Only the matching native is copied now. Let me re-run everything and publish.

---

## Assistant

56.3 MB single-file exe (compression absorbs most of libvips' 18 MB) and it launches. Now a true end-to-end run over the user's actual folder:

---

## Assistant

The full pipeline works on the real files — 30 images (15× 48-bit RGB + 15× 16-bit IR), 4 pages, all three formats. Let me confirm no stale references remain and update the docs.

---

## Assistant

`OrientationTransform` is now dead code — libvips' `Autorot` handles orientation. Removing it.

---

## Assistant

Final verification — clean rebuild, full test suite, and publish:

---

## Assistant

Done. The migration is complete and verified end-to-end.

## What happened

**Root cause of the original bug:** SkiaSharp has *no* TIFF codec — `SKCodec.Create` returned null for every `.tif`, so all TIFF input failed (not just 16/48-bit). I first fixed it with a hand-rolled LibTiff decoder (verified against Pillow, 8 regression tests), then migrated to libvips as you suggested.

## The libvips migration

| Concern | Resolution |
|---|---|
| Decode | `ImageLoader` now uses libvips via NetVips: `NewFromFile → Autorot → ThumbnailImage → Cast → ToSkia` |
| Encode | JPEG (quality) and **LZW TIFF** written by libvips; `TiffWriter` + `LibTiff.NET` removed from production |
| Composition/PDF | SkiaSharp retained (Avalonia needs it anyway) |
| Removed | `TiffDecoder`, `TiffWriter`, `OrientationTransform` (libvips `Autorot` does it), `LibTiff.NET` prod dependency |

**Three traps I hit and handled:**
1. `Cast(Uchar)` **clamps** 16-bit to white — the correct call is `Cast(Uchar, shift: true)` (verified: avg `35.6` = the combined mean of Pillow's per-channel `[38.4, 35.5, 32.9]`).
2. A `Sequential`-access image can only be read once; the pipeline materialises/resizes in one pass.
3. The `NetVips.Native` meta-package copies **every** platform's binaries (~150 MB) when no RID is set — replaced with **per-RID conditional references**, so only the matching native ships.

**Kept as a deliberate design choice:** `LibTiff.NET` remains a **test-only** dependency, used to *write* TIFF fixtures so the decoder under test (libvips) is never validating its own output.

## Verification

- **66/66 tests pass** (60 core + 6 GUI). The TIFF regression suite covers 16-bit grey, 48-bit RGB, 8-bit RGB, bilevel, orientation-6 rotation, down-sampling and oriented-size — and I confirmed 6 of them fail with the TIFF path disabled.
- **Real folder end-to-end**: your `~\Pictures\FlexCapNext\2` — 30 images (15× 48-bit RGB + 15× 16-bit IR), 4 pages, all three formats exported (PDF 4 pages; JPEG ×4; LZW TIFF ×4) and the rendered sheet looks correct.
- **Self-contained single-file publish works**: `DigitalContactSheet.App.exe` is **56.3 MB** (compression absorbs most of libvips' 18 MB), launches cleanly.
- Added a **decode cache** so the live preview doesn't re-decode 184 MB files on every settings change (second pass: 0 ms).

## Notes

- **Licence:** libvips is LGPL-2.1-or-later, linked dynamically as a separate native library (`libvips-42.dll` / `libvips.42.dylib`) — documented in the README's third-party section along with NetVips/SkiaSharp/Avalonia (MIT) and Inter (OFL).
- macOS natives (`osx-arm64`/`osx-x64`) resolve correctly for the CI matrix but couldn't be built here on Windows.
