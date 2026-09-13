<div align="center">

<img src="src/Assets/AppLogo.png" alt="KomiKuy Logo" width="120" style="border-radius: 20px;" />

# KomiKuy

A native Universal Windows Platform (UWP) comic and manga reader for Windows 10 Mobile and Windows 10.

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20Mobile%20%7C%20UWP-0078D7.svg?style=flat-square&logo=windows)](https://github.com/WhaTheFoxSay/KomiKuy)
[![Architecture](https://img.shields.io/badge/Arch-ARM32%20%7C%20x86-blue.svg?style=flat-square)](https://github.com/WhaTheFoxSay/KomiKuy)
[![Target SDK](https://img.shields.io/badge/Min%20SDK-10.0.10586.0%20(W10M)-green.svg?style=flat-square)](https://github.com/WhaTheFoxSay/KomiKuy)
[![Language](https://img.shields.io/badge/Language-C%23%20%7C%20XAML-purple.svg?style=flat-square&logo=c-sharp)](https://github.com/WhaTheFoxSay/KomiKuy)
[![License](https://img.shields.io/badge/License-MIT-orange.svg?style=flat-square)](LICENSE)

</div>

---

## Overview

KomiKuy is a lightweight comic and manga reader developed for Windows 10 Mobile and Windows 10 desktop devices. Built with native C# and XAML on the Universal Windows Platform (UWP), the application is optimized to run efficiently on resource-constrained hardware such as Lumia devices with 1 GB of RAM (Snapdragon 200/400).

---

## Key Features

- **Local Catalog Cache**: Includes an offline base catalog and two-tier caching (memory and local disk storage) to ensure fast startup without blocking the user interface.
- **Custom HTML Parser**: Employs a zero-dependency, lightweight string-scanning parser (`FastHtmlParser.cs`) designed for low memory overhead and rapid metadata extraction.
- **Content Classification**: Categorizes titles into Manga, Manhwa, and Manhua based on metadata and slug indicators.
- **Continuous Vertical Reader**: Provides a vertical scroll reading interface tailored for webtoons and digital comics.
- **Controlled Memory Footprint**: Implements display-level image downsampling (`DecodePixelWidth`) to prevent out-of-memory errors on devices with limited RAM.
- **Reading Progress Tracking**: Records chapter and page positions locally for bookmarking and resuming reading sessions.
- **Dark Theme Interface**: Uses an AMOLED dark palette (`#000000` / `#0F1117`) to reduce display power consumption on OLED panels.
- **Protobuf and XOR Decryption Support**: Contains an internal Protocol Buffers reader and XOR stream decryptor for supported web API endpoints.

---

## System Requirements and Supported Devices

### Minimum Requirements
- **OS**: Windows 10 Mobile (Version 1511 / Build 10586 or later) or Windows 10 Desktop
- **Architecture**: ARM32 or x86
- **RAM**: 512 MB minimum (1 GB recommended)

### Compatible Devices
- **Lumia Series**: 520, 525, 530, 535, 620, 625, 630, 635, 640, 640 XL, 720, 730, 735, 820, 830, 920, 925, 930, 950, 950 XL, 1020, 1320, 1520
- **OEM Windows 10 Mobile Devices**: HP Elite x3, Alcatel Idol 4S, Acer Liquid Jade Primo
- **Windows 10 / 11 PC**: Any x86/x64/ARM64 machine running Windows 10 version 1511 or later

---

## Installation Guide (Windows 10 Mobile)

### 1. Install Developer Certificate
1. Download `Aya.cer` from the [Releases](https://github.com/WhaTheFoxSay/KomiKuy/releases) section or the `builds/` directory.
2. Transfer the certificate to your phone (via USB transfer or Microsoft Edge).
3. Open the file on your device and install it into the **Trusted Root Certification Authorities** store.

### 2. Enable Developer Mode
1. Open **Settings** on your phone.
2. Navigate to **Update & Security** > **For developers**.
3. Select **Developer mode**.

### 3. Install Application Package
1. Download `KomiKuy_v1.0.0_ARM.appx` from the [Releases](https://github.com/WhaTheFoxSay/KomiKuy/releases) page.
2. Open the file using the built-in **File Explorer** app on your phone and confirm the installation.
3. The application will appear in the App List once installation completes.

---

## Building from Source

### Prerequisites
- Visual Studio 2019 or Visual Studio 2022
- Workload: Universal Windows Platform development
- Windows 10 SDK (Build 10.0.19041.0 or newer)

### Build Steps
1. Clone the repository:
   ```powershell
   git clone https://github.com/WhaTheFoxSay/KomiKuy.git
   cd KomiKuy
   ```
2. Execute the automated build script:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build_and_sign.ps1
   ```
3. The build output will be placed in the `builds/` directory:
   - `builds/KomiKuy_v1.0.0_ARM.appx`
   - `builds/Aya.cer`

---

## Repository Structure

```text
KomiKuy/
├── KomiKuy.sln                  # Visual Studio Solution
├── build_and_sign.ps1           # Build and signing script
├── builds/
│   └── Aya.cer                  # Public developer certificate
└── src/
    ├── App.xaml / App.xaml.cs   # Application entry point and lifecycle
    ├── Mangaplus.csproj         # UWP C# project file
    ├── Package.appxmanifest     # App identity and capabilities
    ├── Assets/                  # Visual assets, icons, and catalog data
    ├── Helpers/                 # Display and resolution utilities
    ├── Models/                  # Data structures (MangaTitle, ChapterItem, History)
    ├── Services/                # Parsing engine, API clients, and storage services
    └── Views/                   # UI views (MainPage, TitleDetailPage, ReaderPage)
```

---

## License and Disclaimer

This project is distributed under the terms of the [MIT License](LICENSE).

All comic titles, images, and trademarks displayed within the application are the property of their respective owners and publishers. This application is an independent open-source client developed for educational and interoperability purposes on legacy mobile platforms.
