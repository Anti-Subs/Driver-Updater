# Third-party notices

- **nvidia-data** by ZenitH-AT (https://github.com/ZenitH-AT/nvidia-data) - GPU name to NVIDIA product-family ID list,
  embedded in `src/NvidiaData.cs` and `third_party/`. Public domain (Unlicense), see `third_party/nvidia-data-LICENSE.txt`.
- **mono/reference-assemblies** (https://github.com/mono/reference-assemblies) - .NET Framework 4.8 reference assemblies,
  used only at build time on Linux/macOS; not redistributed here.
- Web service details for ASUS and NVIDIA lookups were learned from the open-source projects G-Helper
  (https://github.com/seerge/g-helper) and TinyNvidiaUpdateChecker (https://github.com/ElPumpo/TinyNvidiaUpdateChecker).
  No code from those projects is included.
- Intel silent-install switches: Intel support articles 000055825 and 000006773, and the Intel Community forum.
- NVIDIA silent-install switches: NVIDIA Driver Installation Guide (docs.nvidia.com).


## Dell catalog sample (tests only)

`tests/fixtures/dell-catalog-sample.xml` is a few entries copied from Dell's public driver catalog
(downloads.dell.com/catalog), used only to test the catalog reader. It is not shipped in the app.
