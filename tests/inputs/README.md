# Revit Integration Test Inputs

Each Revit version has one or more **case folders** under `tests/inputs/<ver>/`. Each case folder contains:

- One or more `.json` export-setting files
- A matching `.rvt` Revit model

The test harness converts every case folder it finds. Add or remove case folders to control which scenarios run.

## Required model: rac_basic_sample_project.rvt

The `rac_basic` case requires **rac_basic_sample_project.rvt**, which is not included in this repository.

1. Download the Revit sample project for your version (see [tests/LICENSE.md](../LICENSE.md)).
2. Place the file here:

```text
tests/inputs/<ver>/rac_basic/rac_basic_sample_project.rvt
```

3. Run integration tests:

```powershell
.\repo.bat test --suite revit2024 --config release
```

Replace `2024` with `2025` or `2026` as needed. Run from an elevated command prompt on Windows with the matching Revit version installed.
