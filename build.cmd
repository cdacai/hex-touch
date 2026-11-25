@echo off
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe not found. Need .NET Framework 4.x
  exit /b 1
)

if not exist dist mkdir dist

set COMMON=/nologo /target:winexe /platform:anycpu /optimize+ /warn:4

echo [1/3] HexTouch.exe
"%CSC%" %COMMON% /out:dist\HexTouch.exe /win32icon:assets\app.ico ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  src\HexTouch.cs
if errorlevel 1 goto fail

echo [2/3] HexTouchUninstall.exe
"%CSC%" %COMMON% /out:dist\HexTouchUninstall.exe /win32icon:assets\app.ico ^
  /reference:System.Windows.Forms.dll ^
  src\Uninstaller.cs
if errorlevel 1 goto fail

echo [3/3] HexTouchSetup.exe
"%CSC%" %COMMON% /out:dist\HexTouchSetup.exe /win32icon:assets\app.ico ^
  /resource:dist\HexTouch.exe,HexTouch.exe ^
  /resource:dist\HexTouchUninstall.exe,HexTouchUninstall.exe ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ^
  src\Installer.cs
if errorlevel 1 goto fail

echo.
echo BUILD OK
dir /b dist
exit /b 0

:fail
echo.
echo BUILD FAILED
exit /b 1
