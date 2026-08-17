@echo off
rem ============================================================
rem  DeepSeek Harness Launcher - WPF build (no VS / no XAML toolchain)
rem  Uses only the built-in .NET Framework csc + GAC WPF assemblies
rem ============================================================
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

set GAC=C:\Windows\Microsoft.NET\assembly
set WPF=/r:%GAC%\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll
set WPF=%WPF% /r:%GAC%\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll
set WPF=%WPF% /r:%GAC%\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll
set WPF=%WPF% /r:%GAC%\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll
set WPF=%WPF% /r:%GAC%\GAC_MSIL\System.IO.Compression\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.dll
set WPF=%WPF% /r:System.Web.Extensions.dll

set RES=
if exist deepseek_logo.png set RES=%RES% /resource:deepseek_logo.png,DeepSeekHarness.logo.png
if exist deepseek_whale_white.png set RES=%RES% /resource:deepseek_whale_white.png,DeepSeekHarness.whale-white.png
if exist deepseek_whale_blue.png set RES=%RES% /resource:deepseek_whale_blue.png,DeepSeekHarness.whale-blue.png
set ICO=
if exist deepseek.ico set ICO=/win32icon:deepseek.ico
set MANIFEST=
if exist app.manifest set MANIFEST=/win32manifest:app.manifest

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ %WPF% %RES% %ICO% %MANIFEST% /out:DeepSeekHarness.exe WpfApp.cs Logic.cs StoreWindow.cs Net.cs AssemblyInfo.cs
if %errorlevel%==0 (
    echo [OK] DeepSeekHarness.exe ^(WPF^) built
) else (
    echo [FAIL] compile error
)
