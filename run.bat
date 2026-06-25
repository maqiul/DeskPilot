@echo off
REM ========================================
REM DeskPilot 一键启动脚本
REM ========================================
chcp 65001 >nul
cd /d %~dp0

echo.
echo ========================================
echo   DeskPilot 桌面 AI 助手
echo ========================================
echo.

REM 检查 .env 文件
if not exist ".env" (
    echo [警告] 未找到 .env 文件，正在从 .env.example 复制...
    copy .env.example .env >nul
    echo [完成] 已生成 .env，请用记事本打开填入 API Key：
    echo        notepad .env
    echo.
    pause
    exit /b 1
)

REM 检查 Key 是否还是占位符
findstr /C:"sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" .env >nul
if %errorlevel% == 0 (
    echo [警告] .env 中的 API Key 还是占位符！
    echo 请编辑 .env 填入真实 Key：
    echo        notepad .env
    echo.
    pause
    exit /b 1
)

echo [1/3] 还原依赖...
dotnet restore DeskPilot.slnx
if %errorlevel% neq 0 (
    echo [失败] 依赖还原失败
    pause
    exit /b 1
)

echo [2/3] 编译项目...
dotnet build DeskPilot.slnx --nologo
if %errorlevel% neq 0 (
    echo [失败] 编译失败
    pause
    exit /b 1
)

echo [3/3] 启动 DeskPilot...
echo.
dotnet run --project src/DeskPilot.App --no-build

pause