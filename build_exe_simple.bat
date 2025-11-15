@echo off
:: Quick build without icon (if you don't have icon.ico yet)

echo Building BoosterX without custom icon...
pip install -r requirements.txt
pyinstaller --onefile --windowed --name "BoosterX" boosterx_gui.py

echo.
echo Done! Check dist\BoosterX.exe
pause
