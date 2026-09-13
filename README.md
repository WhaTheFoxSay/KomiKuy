# KomiKuy! — Windows 10 Mobile Comic & Manga Reader

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20Mobile%20%7C%20UWP-0078D7.svg?style=flat-square&logo=windows)](https://github.com/WhaTheFoxSay/KomiKuy)
[![Architecture](https://img.shields.io/badge/Arch-ARM32%20%7C%20x86-blue.svg?style=flat-square)](https://github.com/WhaTheFoxSay/KomiKuy)
[![Target SDK](https://img.shields.io/badge/Min%20SDK-10.0.10586.0%20(W10M)-green.svg?style=flat-square)](https://github.com/WhaTheFoxSay/KomiKuy)
[![Language](https://img.shields.io/badge/Language-C%23%20%7C%20XAML-purple.svg?style=flat-square&logo=c-sharp)](https://github.com/WhaTheFoxSay/KomiKuy)
[![License](https://img.shields.io/badge/License-MIT-orange.svg?style=flat-square)](LICENSE)

**KomiKuy!** is a native, ultra-lightweight Universal Windows Platform (UWP) comic and manga reader designed specifically for **Windows 10 Mobile** devices and Windows 10 PCs. Tuned for peak efficiency on low-end hardware (such as the **Microsoft Lumia 535** with Qualcomm Snapdragon 200 and 1 GB RAM), KomiKuy! brings smooth, modern webtoon and manga streaming to vintage and contemporary Windows hardware alike.

---

## ✨ Features

- **⚡ Instant 0 ms Launch**: Pre-bundled offline catalog and two-tier caching (in-memory + local disk) eliminate startup network lag.
- **🚀 Zero-Dependency Fast HTML Engine**: Uses `FastHtmlParser.cs`, a custom zero-allocation string scanner that extracts metadata, cover artwork, and chapter lists in single-digit milliseconds without heavy DOM libraries.
- **🎯 Smart Comic Classification**: Automatically classifies catalog titles into **Manga** (Japan), **Manhwa** (Korea), and **Manhua** (China) via heuristic and slug analysis.
- **📖 Continuous Vertical Reader**: Smooth, vertical infinite-scroll reading view optimized for mobile screens.
- **🛡️ RAM Footprint Under 60 MB**: Hardware-tailored display scaling (`DecodePixelWidth = 720`) ensures fluid navigation without out-of-memory crashes on 1 GB RAM Lumia devices.
- **🔖 Precision Bookmark & History**: Remembers your exact reading progress down to the specific page number with one-tap instant resume.
- **🎨 Metro AMOLED Dark Theme**: Pure `#000000` / `#0F1117` background with signature emerald accents (`#144E13`), designed to maximize battery life on OLED and LCD screens.
- **🌐 Protocol Buffers & XOR Decryptor**: Built-in Protobuf wire reader and on-the-fly XOR image cipher decryption engine.

---

## 📱 Supported Devices

| Category | Models |
| :--- | :--- |
| **Lumia x20 / x30 Series** | Lumia 520, 525, 530, 535, 620, 625, 630, 635, 720, 730, 735, 820, 830, 920, 925, 1020, 1320, 1520 |
| **Lumia x40 / x50 Series** | Lumia 540, 640, 640 XL, 550, 650, 950, 950 XL |
| **OEM Windows Phones** | HP Elite x3, Alcatel Idol 4S, Acer Liquid Jade Primo |
| **Windows Desktop & Tablet** | Any PC running Windows 10 (1511 / Build 10586 or newer) or Windows 11 |

---

## 📥 Installation Guide (Sideloading on Windows 10 Mobile)

To install KomiKuy! on your Windows 10 Mobile device:

### Step 1: Install the Developer Certificate
1. Download `Aya.cer` from the [Releases](https://github.com/WhaTheFoxSay/KomiKuy/releases) tab or the `builds/` directory.
2. Transfer `Aya.cer` to your phone via USB or download it directly via Microsoft Edge on your device.
3. Tap on `Aya.cer` to install it, choosing **Root Store** or **Trusted Root Certification Authorities**.
   *(Alternatively, install it via the Windows Device Portal certificate manager).*

### Step 2: Install the App Package
1. On your phone, ensure Developer Mode is active:
   - Go to **Settings > Update & Security > For developers**.
   - Select **Developer mode**.
2. Download `KomiKuy_v1.0.0_ARM.appx` from the [Releases](https://github.com/WhaTheFoxSay/KomiKuy/releases) page.
3. Open the **File Explorer** app on your phone, tap the `.appx` file, and tap **Install**.
4. The application will appear in your App List within 10–30 seconds.

---

## 🛠️ Building from Source

### Prerequisites
- **Visual Studio 2019** or **Visual Studio 2022** (Community, Professional, or Enterprise)
- Workload: **Universal Windows Platform development**
- **Windows 10 SDK** (`10.0.19041.0` or newer)

### Build Script
Clone the repository and run the automated build and signing script:

```powershell
git clone https://github.com/WhaTheFoxSay/KomiKuy.git
cd KomiKuy
powershell -ExecutionPolicy Bypass -File .\build_and_sign.ps1
```

The script will automatically:
1. Restore NuGet dependencies (`Microsoft.NETCore.UniversalWindowsPlatform`).
2. Clean existing build artifacts.
3. Compile `Mangaplus.csproj` in `Release | ARM` mode.
4. Sign the output package using `signtool.exe`.
5. Output the finished package to `builds/KomiKuy_v1.0.0_ARM.appx`.

---

## 📁 Repository Structure

```text
KomiKuy/
├── KomiKuy.sln                  # Visual Studio Solution File
├── build_and_sign.ps1           # Automated MSBuild & Signing Pipeline
├── builds/
│   └── Aya.cer                  # Public Developer Certificate
└── src/
    ├── App.xaml / App.xaml.cs   # App Lifecycle, Navigation & Status Bar Configuration
    ├── Mangaplus.csproj         # Core UWP C# Project
    ├── Package.appxmanifest     # Application Capabilities & Asset Declarations
    ├── Assets/                  # Visual Tile Logos, Icons & Splash Assets
    ├── Helpers/                 # Screen Resolution & Density Calculations
    ├── Models/                  # MangaTitle, ChapterItem, ReadingHistory Models
    ├── Services/                # FastHtmlParser, API Clients & Image Decryptors
    └── Views/                   # MainPage, TitleDetailPage, and ReaderPage
```

---

## 📄 License & Disclaimer

This project is licensed under the [MIT License](LICENSE).

*Disclaimer: This is an independent open-source fan project created for preservation and enjoyment on legacy mobile hardware. All comic content, artwork, and trademarks belong to their respective creators and publishers.*
