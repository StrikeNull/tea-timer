@echo off
setlocal
set "compiler=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%compiler%" set "compiler=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%compiler%" (
    echo Windows .NET Framework compiler was not found.
    exit /b 1
)
pushd "%~dp0"
"%compiler%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /win32manifest:app.manifest /win32icon:tea.ico /out:TeaTimer.exe /resource:assets\tea-maid.png,TeaTimer.TeaMaid /resource:assets\gpt-maid.png,TeaTimer.GptMaid /resource:assets\gpt-dragon.png,TeaTimer.GptDragon /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Core.dll TeaTimer.cs CompactUi.cs
if errorlevel 1 (
    popd
    exit /b 1
)
echo Build succeeded: TeaTimer.exe
popd
exit /b 0
