@echo off
REM ============================================
REM DeskPilot 清理 + 编译 + 测试 一键脚本
REM ============================================
REM 解决 dotnet test 缓存旧 App.dll 的问题
REM ============================================

REM 强制 UTF-8 代码页（65001 = UTF-8）
chcp 65001 >nul

echo [1/3] 清理 bin/obj ...
for /r %%G in (bin obj) do @if exist "%%G" rmdir /s /q "%%G"

echo [2/3] 编译 ...
dotnet build DeskPilot.slnx --nologo
if errorlevel 1 exit /b 1

echo [3/3] 测试 ...
dotnet test DeskPilot.slnx --nologo
if errorlevel 1 exit /b 1

echo.
echo ✅ 全部通过！