@echo off
for %%I in ("%cd%") do set "currentDirName=%%~nxI"

if /I not "%currentDirName%" == "native" (
  echo Please run this command from the native directory.
  exit /b 1
)

mkdir ../Benchmarks/bin/Release/net8.0
mkdir ../Benchmarks/bin/Debug/net8.0
cl protozero_test.cpp /std:c++17 /O2 /LD /Fe:protozero.dll
copy protozero.dll ../Benchmarks/bin/Release/net8.0
copy protozero.dll ../Benchmarks/bin/Debug/net8.0