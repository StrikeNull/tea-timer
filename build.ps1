param([string]$OutputPath = '一盏茶.exe')
$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '未找到 Windows .NET Framework 编译器。' }
Push-Location $PSScriptRoot
try {
    & $compilerPath /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /win32manifest:app.manifest /win32icon:tea.ico "/out:$OutputPath" /resource:assets\tea-maid.png,TeaTimer.TeaMaid /resource:assets\gpt-maid.png,TeaTimer.GptMaid /resource:assets\gpt-dragon.png,TeaTimer.GptDragon /resource:assets\idle-maid.png,TeaTimer.IdleMaid /resource:assets\idle-gpt.png,TeaTimer.IdleGpt /resource:assets\idle-dragon.png,TeaTimer.IdleDragon /resource:assets\ready-maid.gif,TeaTimer.ReadyMaid /resource:assets\ready-gpt.gif,TeaTimer.ReadyGpt /resource:assets\ready-dragon.gif,TeaTimer.ReadyDragon /resource:assets\extra-maid.gif,TeaTimer.ExtraMaid /resource:assets\extra-gpt.gif,TeaTimer.ExtraGpt /resource:assets\extra-dragon.gif,TeaTimer.ExtraDragon /resource:assets\brewing-maid.gif,TeaTimer.BrewingMaid /resource:assets\brewing-gpt.gif,TeaTimer.BrewingGpt /resource:assets\brewing-dragon.gif,TeaTimer.BrewingDragon /resource:assets\tea-ready.wav,TeaTimer.ReadyVoice /resource:assets\voice-cheerful.wav,TeaTimer.VoiceCheerful /resource:assets\voice-soft.wav,TeaTimer.VoiceSoft /resource:assets\voice-playful.wav,TeaTimer.VoicePlayful /resource:assets\voice-today.wav,TeaTimer.VoiceToday /resource:assets\voice-brewing.wav,TeaTimer.VoiceBrewing /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Core.dll TeaTimer.cs CompactUi.cs ReminderMedia.cs MediaLibrary.cs MediaSettingsUi.cs SpriteDrawing.cs
    if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
    Write-Output (Join-Path $PSScriptRoot $OutputPath)
} finally { Pop-Location }
