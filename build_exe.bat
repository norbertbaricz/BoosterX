@echo off
:: BoosterX - Build Script for creating standalone .exe
:: This script creates a single executable file that can run on any Windows PC

echo ========================================
echo   BoosterX - Build Script
echo ========================================
echo.

:: Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python is not installed or not in PATH!
    echo Please install Python from https://www.python.org/
    pause
    exit /b 1
)

echo [1/4] Installing dependencies...
python -m pip install --upgrade pip
pip install -r requirements.txt

echo.
echo [2/4] Cleaning old build files...
if exist "build" rd /s /q "build"
if exist "dist" rd /s /q "dist"
if exist "*.spec" del /q "*.spec"

echo.
echo [3/4] Building executable with PyInstaller...
echo This may take a few minutes...
pyinstaller --onefile --windowed --name "BoosterX" --icon=icon.ico boosterx_gui.py

echo.
echo [4/4] Build complete!
echo.

if exist "dist\BoosterX.exe" (
    echo ========================================
    echo   SUCCESS!
    echo ========================================
    echo.
    echo Your executable is ready:
    echo   Location: dist\BoosterX.exe
    echo.
    echo You can now:
    echo   1. Test it by running: dist\BoosterX.exe
    echo   2. Copy it to any Windows PC (no Python needed!)
    echo   3. Right-click and "Run as Administrator" for full functionality
    echo.
) else (
    echo ========================================
    echo   BUILD FAILED!
    echo ========================================
    echo.
    echo Check the error messages above.
    echo.
)

pause
