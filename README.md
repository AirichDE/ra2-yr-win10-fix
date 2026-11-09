# Red Alert 2 & Yuri's Revenge – Win10/11 Fix

**By Daniel Airich** · open source (MIT)

Makes **Command & Conquer: Red Alert 2** and the expansion **Yuri's Revenge** run on Windows 10 / 11 **without any CD in the drive** – no "Please insert the Red Alert 2 CD", no black screen, no 60-second hang at start. The disc contents can come from a real drive, from **ImgDrive** (or any virtual drive), from `.img` / `.iso` files or simply from a **folder** you copied them into.

> Unofficial fan tool. It contains **no game code and no game data**. It only copies and patches *your own* files, keeps a backup (`*.orig`) of every EXE it changes and downloads DDrawCompat from its official release (SHA-256 checked).

[Deutsche Version weiter unten](#deutsch)

## What it does

| Problem on Windows 10/11 | What the fix does |
|---|---|
| The original `Setup.exe` just closes after "Install" | The tool does the installation itself: it copies the `.mix` files and unpacks the `Game1.cab … GameN.cab` cabinets with the Windows `cabinet.dll` (also multi-part sets such as Yuri's Revenge) |
| "Initialisation failed / DirectDraw / graphics setting" error or crash at start | Installs [DDrawCompat](https://github.com/narzoul/DDrawCompat) v0.7.2 as `ddraw.dll` (SHA-256 checked download) |
| Black screen, program "not responding" for 60 s, then closes | `Game.exe` / `gamemd.exe` wait for the old copy-protection launcher that no longer works on Windows 10/11. The tool finds the three launcher checks **by signature** and neutralises them (`mov al,1 ; ret`) |
| "Please insert a Red Alert 2 CD into the CD-ROM drive" | Both games have a built-in switch **`-CD`** ("No CD init – all files must be local"). The tool creates desktop shortcuts that use it |

Everything can be undone with `/undo` (original EXEs are restored from `*.orig`, `ddraw.dll` and the shortcuts are removed; copied game files are kept).

## Supported versions – how it recognises them

* **Discs** are recognised by their marker file: `Ra1.dsk` = Allied CD (Disk 1), `Ra2.dsk` = Soviet CD (Disk 2), `YR1.dsk` = Yuri's Revenge. Only the German Yuri's Revenge disc was tested; other language versions are expected to use the same layout (please report if not).
* **EXE files** are recognised by SHA-256 (list of known versions) **and** – if the hash is unknown – by a signature search for the launcher checks, so other language versions and patch levels are found as well. If the checks cannot be found, the EXE is **not touched** and the tool tells you the hash so that it can be added.
* A source can be a **drive** (real or virtual), a **folder** that contains the disc content (also several discs in sub-folders, e.g. `Soviet\`, `Allied\`, `Yuri\`), or an **`.img` / `.iso`** (needs [7-Zip](https://www.7-zip.org/) installed).

| Game | Needs |
|---|---|
| Red Alert 2 (`Game.exe`) | Allied CD **and** Soviet CD (both contain different `movies*.mix` / `maps*.mix`) |
| Yuri's Revenge (`gamemd.exe`) | Yuri's Revenge CD **and** Red Alert 2 (Yuri's Revenge needs the base game files) |

## Use

1. **Get the disc contents ready** – any of these:
   * mount the images with **ImgDrive** (or WinCDEmu, Daemon Tools …) → each CD gets a drive letter, **or**
   * put the real CDs in the drive one after another, **or**
   * extract/copy each CD into a folder (7-Zip can open `.img`/`.iso`), **or**
   * just keep the `.img` files and let the tool unpack them (7-Zip required).
2. Run `RA2_Win10_Fix.exe`. A dialog asks for the **game folder** (new folder is fine, e.g. `D:\Games\RA2`).
3. The tool scans all CD drives, then asks *"More sources?"*. Enter a drive (`H:\`), a folder or an image file, one after another – **Enter** continues. Do this for the Soviet, the Allied and the Yuri disc (any order).
4. Start the game with the new **desktop shortcut** (it starts the game with `-CD`).

Already copied everything into the game folder by hand? Run the tool in that folder (or `/dir=`) and skip the sources – it only patches, installs DDrawCompat and creates the shortcuts.

Command line:

```
RA2_Win10_Fix.exe [/dir=<game folder>] [/src=<drive|folder|.img>[;...]] [/check] [/undo]
                  [/noddraw] [/noshortcut] [/registry] [/silent]
```

`/check` only reports what would be patched. Without the shortcut start the games with `Game.exe -CD` / `gamemd.exe -CD`.

## Known limitations

* Without the `-CD` switch (e.g. starting the EXE directly) the game will still ask for the CD. Always use the shortcut.
* Resolution: the game itself only offers up to 800×600 (more is unstable); DDrawCompat scales the picture to your screen. You can tune it with a `DDrawCompat.ini` (see DDrawCompat documentation).
* If something hangs in a black screen, **Ctrl+Alt+End** ends the game (DDrawCompat emergency key).
* Online play is not part of this tool (the original Westwood Online service is no longer available; community projects such as CnCNet exist).
* Only unmodified original EXEs are patched. Cracked EXEs are recognised as "already patched" or refused.
* Tested on Windows 11, German Yuri's Revenge 1.000 disc and a Red Alert 2 `Game.exe` (4,387,088 bytes). Other versions rely on the signature search – please open an issue with the SHA-256 printed by the tool if one is refused.

## How it works

See [docs/HOW_IT_WORKS.md](docs/HOW_IT_WORKS.md) (reverse engineering notes).

## Build

```powershell
.\build.ps1        # uses the csc.exe that ships with Windows -> dist\RA2_Win10_Fix.exe
```

## License and credits

* The code in this repository: **MIT**, © 2026 Daniel Airich – see [LICENSE](LICENSE).
* DDrawCompat by narzoul is downloaded from its official release – see [THIRD_PARTY.md](THIRD_PARTY.md).
* *Command & Conquer, Red Alert 2* and *Yuri's Revenge* are © Electronic Arts / Westwood Studios. This project is not affiliated with them. Use it only with a copy of the game that you legally own.

---

## Deutsch

**Von Daniel Airich** · Open Source (MIT)

Lässt **Command & Conquer: Alarmstufe Rot 2** und das Add-on **Yuris Rache** unter Windows 10/11 laufen – **ohne CD im Laufwerk**: kein „Bitte legen Sie die Alarmstufe Rot 2-CD ein", kein Blackscreen, kein 60-Sekunden-Hänger. Die Inhalte der Discs können aus einem echten Laufwerk, aus **ImgDrive** (oder einem anderen virtuellen Laufwerk), aus `.img` / `.iso`-Dateien oder einfach aus einem **Ordner** kommen, in den du sie kopiert hast.

> Inoffizielles Fan-Werkzeug. Es enthält **keinen Spielcode und keine Spieldaten**. Es kopiert und patcht nur *deine eigenen* Dateien, legt von jeder geänderten EXE eine Sicherung (`*.orig`) an und lädt DDrawCompat vom offiziellen Release (SHA-256 geprüft).

**Was es macht**

* **Installation statt Setup:** Das Original-`Setup.exe` schließt sich unter Windows 11 einfach. Das Tool kopiert die `.mix`-Dateien und entpackt die Cabinets `Game1.cab … GameN.cab` mit der Windows-`cabinet.dll` (auch mehrteilige wie bei Yuris Rache).
* **Grafik-Fix:** DDrawCompat v0.7.2 als `ddraw.dll` (Download mit SHA-256-Prüfung).
* **Blackscreen / Hänger:** `Game.exe` und `gamemd.exe` warten auf den alten Kopierschutz-Launcher. Das Tool findet die drei Launcher-Prüfungen **per Signatur** und schaltet sie ab.
* **„CD einlegen":** Beide Spiele haben den eingebauten Schalter **`-CD`** („alle Dateien sind lokal"). Das Tool legt Desktop-Verknüpfungen an, die ihn nutzen.
* **Rückgängig:** `/undo`.

**Versionserkennung:** Discs werden an `Ra1.dsk` (Allierten-CD), `Ra2.dsk` (Sowjet-CD) und `YR1.dsk` (Yuris Rache) erkannt, EXE-Dateien per SHA-256 und – bei unbekanntem Hash – per Signatursuche. Findet das Tool die Prüfungen nicht, bleibt die EXE **unverändert**.

**Benutzung**

1. Disc-Inhalte bereitstellen: mit **ImgDrive** mounten (jede CD bekommt einen Laufwerksbuchstaben), **oder** echte CDs nacheinander einlegen, **oder** jede CD in einen Ordner entpacken/kopieren, **oder** die `.img`-Dateien direkt angeben (7-Zip nötig).
2. `RA2_Win10_Fix.exe` starten, den **Spielordner** wählen (neuer Ordner ist ok, z. B. `D:\Spiele\RA2`).
3. Das Tool scannt alle CD-Laufwerke und fragt dann *„Weitere Quellen?"*. Laufwerk (`H:\`), Ordner oder Image-Datei eingeben, jeweils **Enter** – für Sowjet-, Allierten- und Yuri-Disc.
4. Das Spiel mit der neuen **Desktop-Verknüpfung** starten (sie nutzt `-CD`).

Schon alles von Hand in den Spielordner kopiert? Das Tool dort starten (oder `/dir=`) und keine Quellen angeben – es patcht dann nur, installiert DDrawCompat und legt die Verknüpfungen an.

**Bekannte Einschränkungen:** Ohne `-CD` (EXE direkt starten) fragt das Spiel wieder nach der CD. Das Spiel selbst bietet höchstens 800×600, DDrawCompat skaliert auf deinen Bildschirm. Bei einem Blackscreen beendet **Strg+Alt+Ende** das Spiel. Online-Spiel ist nicht Teil des Tools (den originalen Westwood-Online-Dienst gibt es nicht mehr, Community-Projekte wie CnCNet schon). Getestet unter Windows 11 mit der deutschen Yuris-Rache-Disc 1.000; andere Versionen laufen über die Signatursuche – bei einer abgelehnten EXE bitte ein Issue mit dem angezeigten SHA-256 öffnen.

**Rechtliches:** Nur mit einer legal erworbenen Kopie des Spiels verwenden. *Command & Conquer, Alarmstufe Rot 2* und *Yuris Rache* sind © Electronic Arts / Westwood Studios; dieses Projekt steht in keiner Verbindung zu ihnen.
