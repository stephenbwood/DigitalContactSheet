# Digital Contact Sheet

A cross-platform desktop app (Windows and macOS) that builds a printable **contact sheet** from a
folder of images. If a folder holds more images than fit on one page, additional pages are generated
until every image has been rendered.

Built with **.NET 10 + Avalonia + SkiaSharp** (composition, text and PDF) and **libvips** (image
decode/encode), distributed as a **self-contained single executable** (no runtime install required).

## Features

- **Multi-page output** — pages are produced automatically until all images are placed.
- **Output formats**
  - `PDF` — one multi-page document per source folder.
  - `JPEG` — one single-page file per page (`contact-sheet-001.jpg`, `contact-sheet-002.jpg`, …).
  - `TIFF` — one single-page LZW file per page (never multi-page).
- **Live preview** with page navigation before you export.
- **Batch mode** — optionally recurse into subfolders, treating **each subfolder as its own contact
  sheet** while keeping the same settings. Output can mirror the folder tree under a chosen output
  directory or be written into each source folder.
- **Bundled font (Inter)** with Regular/Medium/Bold/Light/Black weights, plus any system font.

### Controls

| Group | Settings |
| --- | --- |
| Source | Folder, include subfolders, sort order (name / date / size) |
| Layout | Page size (A5/A4/A3/Letter/Legal/Tabloid/Custom), orientation, custom size + unit, resolution (DPI), page margin, image spacing, columns, rows |
| Appearance | Background colour, text colour, zoom mode (`fit` / `fill`), horizontal position (left/center/right), vertical position (top/middle/bottom) |
| Typography | Font family, font weight, font size, show file-name captions, include file extension, caption gap |
| Output | Format, JPEG quality, destination mode, output folder, file-name prefix, page-number digits |

- **Fit** letterboxes the whole image inside its cell; **Fill** crops to cover the cell, using the
  horizontal/vertical alignment to decide which part is kept.
- Captions are drawn as a reserved band **below** each image, so they never cover the artwork.
  Long file names are truncated from the **left** (`…_000123.tif`) because the tail is what
  distinguishes one file from another, and the extension can be hidden entirely.
- Colour values are entered as hex (`#RRGGBB` or `#RRGGBBAA`).

## Performance

- The preview is rendered at a reduced DPI chosen to fit the preview area, never at full output
  resolution.
- Images for a page are decoded **in parallel** (decoding dominates render time).
- Decoded thumbnails are held in a byte-budgeted LRU cache. Each image is decoded **once at a
  canonical size** (a 1024 px bounding box) and smaller requests are served from that bitmap, so
  changing columns, rows, margins or spacing — which changes the cell size — re-renders in tens of
  milliseconds instead of re-reading every source file. Decoding cost is dominated by reading and
  converting the source and is almost independent of the target size, so the larger canonical decode
  is essentially free. The cache is pinned while a page is rendering so eviction can never dispose a
  bitmap that is still being drawn.
- The preview bitmap is copied straight into the Avalonia surface rather than round-tripping
  through PNG.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/) to build from source.

## Build and run

```bash
dotnet build DigitalContactSheet.slnx
dotnet run --project src/DigitalContactSheet.App
```

## Tests

The core layout, pagination, placement, scanning, naming, settings and rendering logic is covered by
unit tests (rendering tests write real PDF/JPEG/TIFF files and verify them):

```bash
dotnet test
```

## Publishing a self-contained executable

Windows (x64):

```bash
dotnet publish src/DigitalContactSheet.App/DigitalContactSheet.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish/win-x64
```

macOS (Apple Silicon / Intel):

```bash
dotnet publish src/DigitalContactSheet.App/DigitalContactSheet.App.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish/osx-arm64
```

> macOS builds must be produced on macOS (the CI workflow in `.github/workflows/build.yml` runs a
> `windows-latest` / `macos-latest` / `macos-13` matrix). For distribution, wrap the macOS binary in
> a `.app` bundle and code-sign/notarize it.

## Project structure

```
src/DigitalContactSheet.Core   Rendering, layout, export, scanning (no UI, unit-tested)
src/DigitalContactSheet.App    Avalonia GUI (MVVM)
tests/DigitalContactSheet.Core.Tests
```

- `Layout/LayoutEngine` computes page/cell geometry and pagination from the options.
- `Rendering/PageRenderer` draws a page to a Skia canvas — the same code path feeds the preview, the
  raster exports and the PDF.
- `Rendering/ImageLoader` decodes via **libvips** (through `NetVips`), down-sampling to the size
  actually needed and applying EXIF/TIFF orientation, so large folders stay memory-friendly.
- `Rendering/ImageCache` keeps decoded thumbnails for the live preview so re-rendering a page does
  not re-decode large source files.
- `Export/ExportService` streams pages to disk folder by folder with progress and cancellation;
  JPEG and LZW TIFF are written by libvips.

## Image formats

Decoding is handled by libvips, which covers the full matrix: JPEG, PNG, TIFF (8/16-bit, 48-bit RGB,
greyscale, CMYK, palette, bilevel, tiled, planar and compressed), WebP, BMP and GIF — including the
16-bit mono and 48-bit RGB TIFFs that SkiaSharp cannot read at all.

## Notes

- Supported input extensions: JPEG, PNG, TIFF, WebP, BMP, GIF.
- Fonts: `Inter` (SIL Open Font License) is bundled under `src/DigitalContactSheet.Core/Fonts`.
- Settings are remembered in `%APPDATA%\DigitalContactSheet` (Windows) or
  `~/Library/Application Support/DigitalContactSheet` (macOS).

## Third-party licences

- **libvips** — LGPL-2.1-or-later, linked dynamically as a separate native library
  (`libvips-42.dll` / `libvips.42.dylib`); `NetVips.Native` ships the pre-built binaries.
- **NetVips** — MIT.
- **SkiaSharp** — MIT.
- **Avalonia** — MIT.
- **Inter** font — SIL Open Font License 1.1.
