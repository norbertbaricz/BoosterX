# BoosterX v3.3 - Ultimate Performance Optimizer

Modern GUI version of BoosterX with a clean, user-friendly interface.

![BoosterX](https://img.shields.io/badge/Platform-Windows-blue)
![Python](https://img.shields.io/badge/Python-3.8+-green)
![License](https://img.shields.io/badge/License-MIT-orange)

## 🚀 Features

- **🧹 Deep Clean System** - Remove temporary files, clear cache, empty recycle bin
- **⚡ Optimize Performance** - High performance mode, improved UI responsiveness
- **🌐 Optimize Network** - Lower ping, optimized DNS settings
- **⚙️ Disable Unnecessary Services** - Free up system resources

## 📋 Requirements

### For Development (if you want to modify the code):
- Windows 10/11
- Python 3.8 or higher
- Administrator privileges

### For End Users:
- **NOTHING!** Just download the `.exe` file and run it as administrator
- No Python installation needed!

## 🛠️ How to Build the .exe (For Developers)

### Step 1: Install Python
Download and install Python from [python.org](https://www.python.org/downloads/)
- ✅ Make sure to check "Add Python to PATH" during installation

### Step 2: Build the executable
Simply run the build script:
```bash
build_exe_simple.bat
```

Or if you have a custom icon:
```bash
build_exe.bat
```

### Step 3: Find your executable
After building, your `.exe` will be in the `dist` folder:
```
dist/BoosterX.exe
```

## 💡 How to Use

### For End Users:
1. Download `BoosterX.exe`
2. **Right-click** on it and select **"Run as administrator"**
3. Click any optimization button you want
4. Done! 🎉

### For Developers:
1. Clone this repository
2. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```
3. Run the app:
   ```bash
   python boosterx_gui.py
   ```

## 📦 Project Structure

```
BoosterX/
├── boosterx_gui.py          # Main application with GUI
├── BoosterX.bat             # Original batch script version
├── requirements.txt         # Python dependencies
├── build_exe.bat            # Build script (with icon)
├── build_exe_simple.bat     # Build script (without icon)
├── icon.ico                 # Application icon (optional)
└── README.md                # This file
```

## 🔧 Manual Build Instructions

If the automated script doesn't work, you can build manually:

```bash
# 1. Install dependencies
pip install customtkinter pillow pyinstaller

# 2. Build the executable
pyinstaller --onefile --windowed --name "BoosterX" boosterx_gui.py

# 3. Your .exe is in the dist folder!
```

## ⚠️ Important Notes

- **Administrator Rights Required**: The app needs admin privileges to modify system settings
- **Antivirus Warning**: Some antivirus software may flag the .exe as suspicious (false positive). This is normal for PyInstaller-generated executables
- **Windows Defender**: You may need to add an exception if Windows Defender blocks it

## 🎨 Customization

### Change the Color Theme
Edit `boosterx_gui.py`:
```python
ctk.set_default_color_theme("blue")  # Options: "blue", "green", "dark-blue"
```

### Add Custom Icon
1. Create or download a `.ico` file
2. Save it as `icon.ico` in the project folder
3. Use `build_exe.bat` instead of `build_exe_simple.bat`

## 📝 What Gets Optimized?

### Deep Clean:
- Removes temporary files from `%TEMP%`
- Clears Windows temp folder
- Empties recycle bin

### Performance:
- Sets power plan to High Performance
- Reduces menu show delay to 0ms
- Faster app closing times
- Optimizes visual effects

### Network:
- Sets DNS to Cloudflare (1.1.1.1) and Google (8.8.8.8)
- Optimizes for lower ping

### Services:
- Disables: Windows Update, SysMain (Superfetch), DiagTrack, Update Orchestrator, Windows Search

## 🤝 Contributing

Feel free to fork this project and submit pull requests!

## 📜 License

This project is open source. Created by Skypixel team.

## ⚡ Quick Start for Non-Technical Users

**Don't know anything about programming? No problem!**

1. Ask someone to run `build_exe_simple.bat` for you (or do it yourself - just double-click it!)
2. Wait a few minutes
3. Go to the `dist` folder
4. Copy `BoosterX.exe` to your desktop
5. Right-click it → "Run as administrator"
6. Enjoy your optimized Windows! 🎉

---

**Made with ❤️ for Windows optimization enthusiasts**
