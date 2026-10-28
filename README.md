# Renegades XML Editor

Created by **Renegade9532**.

Renegades XML Editor is a Windows desktop editor for assigning custom GTA V add-on vehicle model names to Policing Redefined XML regions and special units. It reads supported, unencrypted add-on `dlc.rpf` archives, lets you assign one or more vehicles to one or more patrol zones, and writes cleanly formatted XML with vehicle chances totaling 100% per vehicle list.

## Features

- Automatically detect installed add-on DLC packs by their `dlc.rpf` files, filter clothing packs and default GTA V DLC/patchdays, and select detected packs for the existing vehicle queue.
- Import one or more `dlc.rpf` archives or folders containing them.
- Discover add-on vehicle models in nested RPF archives.
- Assign multiple vehicles to multiple Policing Redefined regions or special units.
- Review the vehicles currently stored in a selected zone directly from its XML file.
- Select a GTA V installation at startup; the folder is remembered for the next launch.
- Preview supported native YFT/YTD vehicle assets in-app, drag to orbit through a full 360°, and scroll to zoom.
- Update `DefaultRegions.xml`, `SpecialUnits.xml`, and `CustomRegions.xml`.
- Preserve a readable XML layout, with one `<Vehicle>` entry per line.
- Use the interface in English, Spanish, French, German, Portuguese, or Russian.

## Build from source

Requirements:

- Windows
- .NET Framework 4.x Developer Pack / C# compiler at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`

Run the included PowerShell script:

```powershell
./Build-OpenSource.ps1
```

It creates `Release-OpenSource\Renegades XML Editor.exe`. The build is intentionally **not obfuscated**.

## Usage

1. Start `Renegades XML Editor.exe` and select the GTA V installation folder. Renegades remembers it and reconnects XML files on later launches.
2. Open **Assignments** and select a zone to review its current XML vehicle list. This view is read-only; app queue assignments remain staged until **Import XML changes**.
3. Import a vehicle pack and select vehicles in **Assignments**.
4. Select one or more agencies/patrol zones and choose **Assign selected vehicles**.
5. Review your selections and choose **Import XML changes**. Before saving, the editor validates all selected agencies, creates a timestamped `.backup-...` copy beside each XML file, stages the updated files, and restores earlier files if a later replacement fails. Keep those backups until the new configuration is confirmed in game.

The dashboard reports how many XML files are connected, how many vehicles are queued, and how many assignments are active. The Activity view shows actions from the current session.

## Open-source status

This repository is released under the [MIT License](LICENSE). The release/source package contains the application’s native RPF, YFT, YTD, and preview implementation. It does not compile against or bundle CodeWalker.

## Support

Discord: `renegade9532`
