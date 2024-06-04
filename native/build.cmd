@echo off
for %%I in ("%cd%") do set "currentDirName=%%~nxI"

if /I not "%currentDirName%" == "native" (
  echo Please run this command from the native directory.
  exit /b 1
)

cl protozero_test.cpp /std:c++17 /O2 /LD /Fe:protozero.dll
