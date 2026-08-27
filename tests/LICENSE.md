# Test Asset Licenses

## rac_basic_sample_project.rvt

Integration tests use **rac_basic_sample_project.rvt**, a sample project provided by Autodesk with Revit. The sample project is subject to Autodesk's terms of use; see Autodesk's documentation for licensing details.

This model is **not** distributed with the repository. Download the sample project for your Revit version and place `rac_basic_sample_project.rvt` in the matching test case folder:

| Revit version | Download |
|---|---|
| 2024 | [Revit 2024 sample projects](https://help.autodesk.com/view/RVT/2024/ENU/?guid=GUID-61EF2F22-3A1F-4317-B925-1E85F138BE88) |
| 2025 | [Revit 2025 sample projects](https://help.autodesk.com/view/RVT/2025/ENU/?guid=GUID-61EF2F22-3A1F-4317-B925-1E85F138BE88) |
| 2026 | [Revit 2026 sample projects](https://help.autodesk.com/view/RVT/2026/ENU/?guid=GUID-61EF2F22-3A1F-4317-B925-1E85F138BE88) |

Target path (replace `<ver>` with your Revit version):

```text
tests/inputs/<ver>/rac_basic/rac_basic_sample_project.rvt
```
