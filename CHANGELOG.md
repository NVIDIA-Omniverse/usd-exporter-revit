# 2.0
- Updated to OpenUSD 26.08
- Updated bundled Python runtime to 3.12.16
- Replaced the legacy Asset Validator with usd-validation-nvidia 1.21.0
- Removed fmt dependency
- Added USDZ export. Unsupported MDL shader prims, MDL output connections, and `.mdl` asset paths are removed from USDZ packages
- Improved USD export performance for large Revit models with thousands of materials
- Renamed UsdExporterRevitSDK to UsdExporterRevitCore
- Extended Room and MEP Space export with stable identifiers, normalized metadata, boundaries, and available relationship data

# 1.0

- Initial release of usd-exporter-revit