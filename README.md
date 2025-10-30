
# NK732 GUI
For copyright concerns the library/backend files are not added. Please consult the company if needed. 
A lightweight **Windows Forms GUI** for the NK732 Time Interval Analyzer, written in C#.  
Includes a **simulation mode** for development and testing without hardware.

---

## 🚀 Features

- **Simulate Device** mode (`#if SIMULATION` or runtime checkbox)
- Connect / Disconnect / Start / Stop instrument control
- Adjustable **measurement count**
- Real-time console output redirected to a multiline textbox
- Ready for integration with the real **BiDrv** SDK

---

## 🧰 Project Structure

| File | Purpose |
|------|----------|
| `Form1.cs` | Main GUI logic |
| `TIAController.cs` | Bridge between GUI and backend (handles simulation mode) |
| `Program1.cs` | Original NK732 backend logic |
| `README.md` | This file 😊 |

---

## 🖥️ Build & Run

### Requirements
- Visual Studio 2022 or later  
- .NET Framework 4.7.2 (or higher)

### Steps
1. Clone the repo  
   ```bash
   git clone https://github.com/YourUser/NK732_GUI.git
   cd NK732_GUI
