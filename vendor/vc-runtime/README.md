# Visual C++ Runtime for Windows x64

The four unmodified Microsoft DLLs in `win-x64/` satisfy the imports of ONNX Runtime 1.20.1.
They are copied from the installed Visual Studio 2022 C++ Redistributable component, not System32.
`manifest.json` pins their file version, architecture and SHA-256 checksums.

The project includes them in build output and in the self-extracting single-file publish.
They are extracted beside ONNX Runtime, so all three distributions work without a machine-wide
Visual C++ installation, administrator rights or an extra installer.

Redistribution is governed by the Visual Studio 2022 Distributable Code terms and list.
The original license documents, readable text copies and REDIST list are under
`licenses/Microsoft.VisualC.Runtime/`. The application remains MIT; these DLLs retain Microsoft's terms.
Keep the version, DLLs, checksums and license inventory together when updating this component.
App-local runtime security updates must be delivered through application updates.
