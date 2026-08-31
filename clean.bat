@echo off

@rem Build outputs only — preserve _build\target-deps and _build\host-deps.
for %%d in (cmake windows-x86_64 intermediate test unittest unsignedpackages signedpackages) do (
    if exist "_build\%%d" rmdir "_build\%%d" /s /q
)

if exist "_repo" rmdir "_repo" /s /q
if exist "source\UsdExporterRevitSetup\bin" rmdir "source\UsdExporterRevitSetup\bin" /s /q
if exist "source\UsdExporterRevitSetup\obj" rmdir "source\UsdExporterRevitSetup\obj" /s /q
if exist "tools\repoman\__pycache__" rmdir "tools\repoman\__pycache__" /s /q
