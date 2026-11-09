# How it works (reverse-engineering notes)

All addresses are virtual addresses (image base `0x400000`) of the tested files:
`Game.exe` (Red Alert 2, 4,387,088 bytes) and `gamemd.exe` (Yuri's Revenge 1.000 German, 4,808,976 bytes).
The tool does **not** rely on these addresses – it finds the code by signature (see below).

## 1. Why the original installer fails

`Setup.exe` of the discs closes silently on Windows 10/11 after "Install". The tool replaces it:

* the `.mix` files lie in the disc root and are copied;
* `Install\Game1.cab …` are plain **Microsoft cabinets** (`MSCF`), not InstallShield. `expand.exe` can only read the first part of a multi-part set (Yuri's Revenge has `Game1.cab … Game6.cab`, 116 files). The tool calls the Windows **FDI API** (`cabinet.dll`) instead and processes every part of the set – a file that spans two cabinets is completed through the `fdintNEXT_CABINET` notification, files that start in a later cabinet need that cabinet to be processed on its own;
* cabinet entries of sub-folders start with a backslash (`\Taunts\x.wav`), which must be stripped before `Path.Combine`, otherwise they are interpreted as absolute paths;
* `secdrv.sys` (SafeDisc driver) and the uninstallers are not copied.

## 2. The startup hang ("black screen, 100 % CPU, closes after 60 s")

`gamemd.exe` / `Game.exe` contain a Westwood "copy protection" that is a **launcher handshake**, not a disc check:

| Function (gamemd / Game) | Log string | What it does | Result on Win 10/11 |
|---|---|---|---|
| `0x49F4D0` / `0x4916D0` | `Checking if launcher is running` | `CreateMutexA("48BC11BD-…")`, returns true when the mutex already exists (= launcher running) | started directly → false → the game exits silently |
| `0x49F530` / `0x491710` | `Notify launcher` | `OpenEventA("D6E7FC97-…")` in a loop for 60 s (`0xEA60`, `Sleep(0)` = 100 % CPU), `SetEvent`, then waits for a thread message `0xBEEF` and maps a shared memory section | the launcher `Ra2md.exe` never answers (old protection API) → returns false after 60 s → caller calls `exit(1)` |
| `0x49F6B0` / `0x4917F0` | `Validating` | compares the mapped launcher data with a constant / the registry | returns false |

All callers treat a non-zero `al` as "valid". The fix writes `B0 01 C3` (`mov al,1 ; ret`) at the start of the three functions. No stack arguments, plain `ret`, so no side effects.

### Signature search (version independent)

For each of the three log strings the tool

1. finds the string in the file and converts the file offset to a virtual address (PE section table),
2. finds `68 <va>` (`push imm32`) inside an executable section,
3. walks back over a plain prologue (`push r32` / `sub esp,imm8`) until the previous byte is padding (`90`, `CC`) or a `ret`,
4. patches the first three bytes.

If one of the three functions cannot be found, **nothing** is written. The hash table in the source only adds a friendly name for known versions.

## 3. "Please insert the Red Alert 2 CD"

After the launcher checks the game looks for the discs (`Ra2.dsk`, `YR1.dsk`, `GetVolumeInformation`, dialog `TXT_INSERT_CD_DIALOG`). Patching that disc function to "always true" **breaks** the game ("Initialisierung fehlgeschlagen. Bitte erneut installieren."), because the success path also registers the movie mix files. The game already has the right mechanism:

```
-CD parameter was specified. No CD init - all files must be local
```

The switch sets a global flag (`0x838B50` in `Game.exe`, `0x89D310` in `gamemd.exe`) that makes the disc functions return "present" *and* loads `MOVIES*.MIX` / `MOVMD*.MIX` from the game folder. The tool only creates shortcuts with `-CD`.

## 4. Graphics

Without a DirectDraw wrapper the game shows the error "Grafikeinstellung kann nicht …" (graphics setting cannot be …) on Windows 11 and ends. [DDrawCompat](https://github.com/narzoul/DDrawCompat) v0.7.2 as `ddraw.dll` in the game folder fixes it. Emergency key: **Ctrl+Alt+End**.

## 5. Not needed

* **Registry:** the game runs without `HKLM\SOFTWARE\Westwood\Red Alert 2` / `Yuri's Revenge` (tested by removing both keys). The tool only writes them with `/registry`.
* **Ghidra:** a disassembler (Capstone) was enough; the logic is tiny.
