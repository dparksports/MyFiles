# MyFiles

![Dashboard Screenshot](screenshot_v2.png)

**MyFiles** is a high-performance, modern file management and verification tool for Windows. Designed for speed and reliability, it allows users to scan drives, calculate SHA256 checksums, and verify data integrity with ease.

## ✨ Features

- **🚀 Fast Scanning**: Rapidly index entire drives or specific directories with optimized non-blocking background scanning.
- **🛡️ Data Integrity**: Buffered SHA256 checksum calculation ensures your files are intact without hogging system memory.
- **🔍 Smart Comparison**: Compare scan results to detect modified, missing, or corrupted files.
- **📊 Real-Time Monitoring**: Live tracking of scan progress, memory usage (RAM), and file processing status.
- **💾 Resume Capability**: Pause and resume checksum calculations at any time—perfect for large datasets.
- **🔒 Safe & Read-Only**: The application strictly reads data to generate manifests; it never modifies your files.

## 📦 Installation

1.  Go to the [Releases](https://github.com/dparksports/MyFiles/releases) page.
2.  Download the latest [MyFiles_v1.4.0.zip](https://github.com/dparksports/MyFiles/releases/download/v1.4.0/MyFiles_v1.4.0.zip).
3.  Extract the archive to a folder of your choice.
4.  Run `MyFiles.exe`.

*Note: Requires .NET Desktop Runtime (bundled or prompted if missing).*

## 📖 Usage

### Scanning Drives
1.  **Select Drives**: Use the sidebar to check the drives (C:, D:, etc.) you wish to scan.
2.  **Start Scan**: Click the **Scan Selected Drives** button.
3.  **Monitor**: Watch the status bar for progress. The tool will index all files first.
4.  **Checksums**: Once indexing is complete, it automatically begins hashing files. You can **Pause** this process if needed.

### Comparing Scans
1.  Navigate to the **Compare Scans** tab.
2.  Load a previous scan result (CSV) and compare it against a current scan or another file.
3.  View differences instantly to see what has changed.

## 📄 License

This project is licensed under the **Apache-2.0 License** - see the [LICENSE](LICENSE) file for details.

---

Made with ❤️ in California
