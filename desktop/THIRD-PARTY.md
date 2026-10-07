# Third-party components

- LibreHardwareMonitorLib 0.9.6 — MPL-2.0. Source at https://github.com/LibreHardwareMonitor/LibreHardwareMonitor (package commit 3d331e3370efb858411f19511373eff65a218701). Used unmodified. Includes its transitive NuGet dependencies; see the project's THIRD-PARTY-NOTICES.txt and the NuGet package metadata.
- .NET 10 / ASP.NET Core / Windows Forms — Microsoft, MIT. Includes Microsoft.Extensions.Hosting.WindowsServices for the optional hardware service. Runtime license and third-party notices are included in the self-contained output.
- PawnIO 2.2.0 — namazso. Optional unmodified signed installer from https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0 . Driver source and license: https://github.com/namazso/PawnIO . It is a separately installed dependency, not started as another monitoring application.

This repository does not include or redistribute AIDA64 binaries or its monitoring engine.

- PresentMon 2.6.0 — Intel / NVIDIA, MIT. Pinned commit e13fce6acdb55a808fd8318175a56863e532d95f from https://github.com/GameTechDev/PresentMon . Collection/analysis sources and host-adapter changes are in third_party/PresentMon; built as the in-process PanelDeck.PresentMon.dll. License: licenses/PresentMon-LICENSE.txt.
