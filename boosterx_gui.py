#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
BoosterX v3.3 - Ultimate Performance Optimizer
Modern GUI version with CustomTkinter
"""

import customtkinter as ctk
import subprocess
import os
import sys
import threading
from tkinter import messagebox
import ctypes

# Configure CustomTkinter appearance
ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")


class BoosterXApp(ctk.CTk):
    def __init__(self):
        super().__init__()
        
        # Configure window
        self.title("BoosterX v3.3 - Ultimate Performance")
        self.geometry("700x550")
        self.resizable(False, False)
        
        # Check admin rights
        self.is_admin = self.check_admin()
        
        # Create UI
        self.create_widgets()
        
    def check_admin(self):
        """Check if running with administrator privileges"""
        try:
            return ctypes.windll.shell32.IsUserAnAdmin()
        except:
            return False
    
    def request_admin(self):
        """Request administrator privileges"""
        if not self.is_admin:
            messagebox.showerror(
                "Administrator Required",
                "This application needs administrator privileges to function properly.\n\n"
                "Please right-click the .exe and select 'Run as administrator'."
            )
            return False
        return True
    
    def create_widgets(self):
        """Create the main UI components"""
        
        # Header Frame
        header_frame = ctk.CTkFrame(self, fg_color="transparent")
        header_frame.pack(pady=20, padx=20, fill="x")
        
        title_label = ctk.CTkLabel(
            header_frame,
            text="⚡ BoosterX - Ultimate Performance",
            font=ctk.CTkFont(size=24, weight="bold")
        )
        title_label.pack()
        
        subtitle_label = ctk.CTkLabel(
            header_frame,
            text="Advanced Windows Optimization Tool",
            font=ctk.CTkFont(size=12),
            text_color="gray"
        )
        subtitle_label.pack(pady=(5, 0))
        
        # Admin status indicator
        if self.is_admin:
            admin_label = ctk.CTkLabel(
                header_frame,
                text="✓ Running as Administrator",
                font=ctk.CTkFont(size=10),
                text_color="green"
            )
        else:
            admin_label = ctk.CTkLabel(
                header_frame,
                text="⚠ Not running as Administrator - Some features may not work",
                font=ctk.CTkFont(size=10),
                text_color="orange"
            )
        admin_label.pack(pady=(5, 0))
        
        # Main content frame
        content_frame = ctk.CTkFrame(self)
        content_frame.pack(pady=10, padx=20, fill="both", expand=True)
        
        # Buttons for each optimization
        self.create_option_button(
            content_frame,
            "🧹 Deep Clean System",
            "Remove temporary files, clear cache, and empty recycle bin",
            self.clean_system,
            row=0
        )
        
        self.create_option_button(
            content_frame,
            "🚀 Optimize Performance",
            "Enable high performance mode and improve UI responsiveness",
            self.optimize_performance,
            row=1
        )
        
        self.create_option_button(
            content_frame,
            "🌐 Optimize Network",
            "Reduce ping, optimize DNS settings for gaming",
            self.optimize_network,
            row=2
        )
        
        self.create_option_button(
            content_frame,
            "⚙️ Disable Unnecessary Services",
            "Stop and disable background services to free resources",
            self.disable_services,
            row=3
        )
        
        # Progress/Status label
        self.status_label = ctk.CTkLabel(
            self,
            text="Ready to optimize your system",
            font=ctk.CTkFont(size=11),
            text_color="gray"
        )
        self.status_label.pack(pady=10)
        
        # Exit button
        exit_button = ctk.CTkButton(
            self,
            text="Exit",
            command=self.quit,
            fg_color="gray",
            hover_color="darkred",
            width=120
        )
        exit_button.pack(pady=(0, 20))
        
    def create_option_button(self, parent, title, description, command, row):
        """Create a styled button with description"""
        button_frame = ctk.CTkFrame(parent, fg_color="transparent")
        button_frame.pack(pady=8, padx=15, fill="x")
        
        button = ctk.CTkButton(
            button_frame,
            text=title,
            command=command,
            font=ctk.CTkFont(size=14, weight="bold"),
            height=40,
            corner_radius=10
        )
        button.pack(fill="x")
        
        desc_label = ctk.CTkLabel(
            button_frame,
            text=description,
            font=ctk.CTkFont(size=10),
            text_color="gray"
        )
        desc_label.pack(pady=(2, 0))
    
    def update_status(self, message, color="gray"):
        """Update the status label"""
        self.status_label.configure(text=message, text_color=color)
        self.update_idletasks()
    
    def run_command(self, command, shell=True):
        """Execute a system command"""
        try:
            result = subprocess.run(
                command,
                shell=shell,
                capture_output=True,
                text=True,
                timeout=30
            )
            return result.returncode == 0
        except Exception as e:
            print(f"Error running command: {e}")
            return False
    
    def run_optimization(self, func, name):
        """Run optimization in a separate thread"""
        if not self.request_admin():
            return
        
        def worker():
            self.update_status(f"Running {name}...", "yellow")
            try:
                func()
                self.update_status(f"✓ {name} completed successfully!", "green")
                messagebox.showinfo("Success", f"{name} completed successfully!")
            except Exception as e:
                self.update_status(f"✗ Error during {name}", "red")
                messagebox.showerror("Error", f"An error occurred:\n{str(e)}")
        
        thread = threading.Thread(target=worker)
        thread.daemon = True
        thread.start()
    
    def clean_system(self):
        """Deep clean system - remove temp files and clear cache"""
        self.run_optimization(self._clean_system_worker, "Deep Clean")
    
    def _clean_system_worker(self):
        """Worker function for system cleaning"""
        temp_dir = os.environ.get('TEMP', '')
        wintemp_dir = os.path.join(os.environ.get('WINDIR', 'C:\\Windows'), 'Temp')
        
        # Clean user temp
        if temp_dir:
            self.run_command(f'del /f /q /s "{temp_dir}\\*"')
            self.run_command(f'for /d %x in ("{temp_dir}\\*") do rd /s /q "%x"')
        
        # Clean Windows temp
        self.run_command(f'del /f /q /s "{wintemp_dir}\\*"')
        self.run_command(f'for /d %x in ("{wintemp_dir}\\*") do rd /s /q "%x"')
        
        # Empty recycle bin
        self.run_command('powershell.exe -Command "Clear-RecycleBin -Force -ErrorAction SilentlyContinue"')
    
    def optimize_performance(self):
        """Optimize system performance for gaming and UI responsiveness"""
        self.run_optimization(self._optimize_performance_worker, "Performance Optimization")
    
    def _optimize_performance_worker(self):
        """Worker function for performance optimization"""
        # Set high performance power plan
        self.run_command('powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c')
        
        # UI responsiveness tweaks
        reg_commands = [
            'reg add "HKCU\\Control Panel\\Desktop" /v "AutoEndTasks" /t REG_SZ /d 1 /f',
            'reg add "HKCU\\Control Panel\\Desktop" /v "MenuShowDelay" /t REG_SZ /d 0 /f',
            'reg add "HKCU\\Control Panel\\Desktop" /v "WaitToKillAppTimeout" /t REG_SZ /d 2000 /f',
            'reg add "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects" /v "VisualFxSetting" /t REG_DWORD /d 2 /f',
        ]
        
        for cmd in reg_commands:
            self.run_command(cmd)
    
    def optimize_network(self):
        """Optimize network settings for lower ping"""
        self.run_optimization(self._optimize_network_worker, "Network Optimization")
    
    def _optimize_network_worker(self):
        """Worker function for network optimization"""
        # Get active network interfaces
        result = subprocess.run(
            'netsh interface show interface',
            shell=True,
            capture_output=True,
            text=True
        )
        
        # Set DNS to Cloudflare and Google
        if result.returncode == 0:
            lines = result.stdout.split('\n')
            for line in lines:
                if 'Connected' in line:
                    # Extract interface name (this is simplified, may need adjustment)
                    parts = line.split()
                    if len(parts) >= 4:
                        interface_name = ' '.join(parts[3:])
                        self.run_command(f'netsh interface ip set dns name="{interface_name}" static 1.1.1.1 primary')
                        self.run_command(f'netsh interface ip add dns name="{interface_name}" 8.8.8.8 index=2')
    
    def disable_services(self):
        """Disable unnecessary Windows services"""
        self.run_optimization(self._disable_services_worker, "Service Optimization")
    
    def _disable_services_worker(self):
        """Worker function for disabling services"""
        services = ['wuauserv', 'SysMain', 'DiagTrack', 'UsoSvc', 'WSearch']
        
        for service in services:
            self.run_command(f'sc stop {service}')
            self.run_command(f'sc config {service} start= disabled')


def main():
    """Main entry point"""
    app = BoosterXApp()
    app.mainloop()


if __name__ == "__main__":
    main()
