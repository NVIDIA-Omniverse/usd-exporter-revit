<!-- SPDX-FileCopyrightText: Copyright (c) 2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved. -->
<!-- SPDX-License-Identifier: Apache-2.0 AND CC-BY-4.0 -->

# Export Troubleshooting

Use this reference for empty or incorrect USD export results after the plugin is installed.

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Empty or nearly empty stage | Export view hides elements, section box too tight, or wrong 3D view | Switch to the intended 3D view; check visibility, phase filter, detail level, and section box. |
| Missing cameras / lights / rooms / spaces / drawings / BIM / links | Corresponding include option disabled | Enable only the requested `Include*` options and re-export. |
| Wrong scale or units | `UnitType` mismatch with downstream expectation | Set `UnitType` to the requested linear unit; confirm stage `metersPerUnit`. |
| Wrong world placement | `CoordinateSystem` mismatch | Choose Internal Origin, Project Base Point, Survey Point, or Shared Coordinates as needed. |
| Missing or fallback materials | Unhandled appearance schema or mapping not set | Check `MaterialStyle`, `MaterialFolderName`, and `Mappings.Materials.UserMapped`; see `docs/concept_mapping.md`. |
| Links missing | `IncludeLinks=false` or unloaded Revit links | Enable `IncludeLinks`; load links in Revit; inspect `Links/` sidecar stages and `RVT Links/` payloads. |
| Family instances not shared | Instancing disabled or style not set | Set `InstanceFamilies` and `FamilyInstanceStyle` as requested. |
| Export cancelled mid-run | User cancel or harness interrupt | Check progress UI/logs; re-run without cancel. |
| Crash or unexplained failure | Plugin/native error | Collect log under `%USERPROFILE%\.usd_exporter_revit\logs\...` and note plugin, Revit, and Windows versions. |

For mapping semantics rather than failure symptoms, read `docs/concept_mapping.md`.
If the plugin is missing or will not build, switch to `../../usd-exporter-revit-getting-started/SKILL.md`.
