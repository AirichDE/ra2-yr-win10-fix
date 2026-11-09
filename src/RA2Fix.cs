// Red Alert 2 / Yuri's Revenge - Win10/11 Fix
// By Daniel Airich - MIT License
//
// Console tool (C# 5, .NET Framework 4, x86). Contains NO game code and NO game data:
//   * copies the content of YOUR OWN discs / images / folders into the game folder
//   * extracts the InstallShield-less Microsoft cabinets (Game1.cab ... GameN.cab) with the Windows cabinet.dll (FDI)
//   * finds the launcher / copy-protection checks of Game.exe and gamemd.exe by SIGNATURE (works on
//     different language versions and patch levels) and neutralises them (backup: *.orig)
//   * installs DDrawCompat (official release, SHA-256 checked) as ddraw.dll
//   * creates desktop shortcuts that start the game with the built-in "-CD" switch (= all files are local)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RA2Fix
{
    static class Program
    {
        const string Ver = "1.0";
        const string DdrawUrl = "https://github.com/narzoul/DDrawCompat/releases/download/v0.7.2/DDrawCompat-v0.7.2.zip";
        const string DdrawSha256 = "94895CF6EDE8FD0F74EE1447FDC08B7044A9C65A1A25A49A4DF893A0370975A3"; // ddraw.dll of v0.7.2
        const string DdrawZipSha256 = "A932A4DCB2100552B6918FD4A3CC7DE138BB97448BBCA2A7051954993A397B39";

        static bool de = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        static StreamWriter log;
        static bool yes;
        static string T(string deText, string enText) { return de ? deText : enText; }

        static void Say(string s)
        {
            Console.WriteLine(s);
            if (log != null) { log.WriteLine(s); log.Flush(); }
        }
        static void Say(string deText, string enText) { Say(T(deText, enText)); }

        class Opts
        {
            public string Dir; public List<string> Src = new List<string>();
            public bool Undo, NoDdraw, NoShortcut, Check;
            public bool NoRegistry = true; // the game runs without the Westwood registry keys -> opt-in via /registry
        }

        [STAThread]
        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Opts o = new Opts();
            foreach (string a0 in args)
            {
                string a = a0.Trim();
                string l = a.ToLowerInvariant();
                if (l.StartsWith("/dir=")) o.Dir = a.Substring(5).Trim('"');
                else if (l.StartsWith("/src=")) { foreach (string p in a.Substring(5).Trim('"').Split(';')) if (p.Trim().Length > 0) o.Src.Add(p.Trim()); }
                else if (l == "/undo") o.Undo = true;
                else if (l == "/noddraw") o.NoDdraw = true;
                else if (l == "/noshortcut") o.NoShortcut = true;
                else if (l == "/registry") o.NoRegistry = false;
                else if (l == "/check") o.Check = true;
                else if (l == "/silent" || l == "/yes") yes = true;
                else if (l == "/?" || l == "-h" || l == "/help") { Help(); return 0; }
            }
            Console.WriteLine();
            Console.WriteLine("=== Red Alert 2 / Yuri's Revenge - Win10/11 Fix " + Ver + " (Daniel Airich, MIT) ===");
            Console.WriteLine();
            int rc = 0;
            try { rc = Run(o); }
            catch (Exception ex)
            {
                Say("FEHLER: " + ex.Message, "ERROR: " + ex.Message);
                if (log != null) log.WriteLine(ex.ToString());
                rc = 1;
            }
            if (!yes) { Console.WriteLine(); Console.WriteLine(T("Enter zum Beenden...", "Press Enter to exit...")); Console.ReadLine(); }
            return rc;
        }

        static void Help()
        {
            Console.WriteLine("RA2_Win10_Fix.exe [/dir=<Spielordner>] [/src=<Disc|Ordner|.img>[;...]] [/check] [/undo]");
            Console.WriteLine("                  [/noddraw] [/noshortcut] [/registry] [/silent]");
        }

        // ------------------------------------------------------------------ main flow
        static int Run(Opts o)
        {
            string dir = ResolveGameDir(o);
            if (dir == null) { Say("Kein Zielordner gewählt.", "No target folder chosen."); return 2; }
            Directory.CreateDirectory(dir);
            log = new StreamWriter(Path.Combine(dir, "RA2_Win10_Fix.log"), true, Encoding.UTF8);
            log.WriteLine("---- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ----");
            Say("Spielordner: " + dir, "Game folder: " + dir);

            if (o.Undo) return Undo(dir);

            if (!o.Check)
            {
                List<Disc> discs = FindDiscs(o);
                if (discs.Count > 0)
                {
                    Say("", "");
                    foreach (Disc d in discs)
                    {
                        Say("Erkannt: " + d.Kind + "  <-  " + d.Root, "Found: " + d.Kind + "  <-  " + d.Root);
                    }
                    foreach (Disc d in discs) InstallDisc(d, dir);
                }
                else
                {
                    Say("Keine Disc/Quelle angegeben - es werden die Dateien verwendet, die bereits im Spielordner liegen.",
                        "No disc/source given - using the files that are already in the game folder.");
                }
            }

            // ---- what do we have?
            bool ra2 = File.Exists(Path.Combine(dir, "ra2.mix")) && File.Exists(Path.Combine(dir, "Game.exe"));
            bool yr = File.Exists(Path.Combine(dir, "ra2md.mix")) && File.Exists(Path.Combine(dir, "gamemd.exe"));
            Say("", "");
            Say("Gefundene Installation:  Alarmstufe Rot 2 = " + (ra2 ? "JA" : "nein") + "   Yuris Rache = " + (yr ? "JA" : "nein"),
                "Detected install:  Red Alert 2 = " + (ra2 ? "YES" : "no") + "   Yuri's Revenge = " + (yr ? "YES" : "no"));
            ReportMissing(dir, ra2, yr);
            if (!ra2 && !yr)
            {
                Say("Weder Game.exe noch gamemd.exe samt .mix-Dateien gefunden. Bitte Disc/Ordner angeben (/src=...).",
                    "Neither Game.exe nor gamemd.exe with its .mix files found. Please give a disc/folder (/src=...).");
                return 3;
            }

            // ---- patch
            Say("", "");
            Say("--- Kopierschutz-/Launcher-Prüfung entschärfen ---", "--- Neutralising launcher / copy-protection checks ---");
            bool patchedAny = false;
            foreach (string exe in new string[] { "Game.exe", "gamemd.exe" })
            {
                string p = Path.Combine(dir, exe);
                if (File.Exists(p)) patchedAny |= PatchExe(p, o.Check);
            }

            if (o.Check) return 0;

            // ---- DDrawCompat
            if (!o.NoDdraw) InstallDdraw(dir);

            // ---- registry
            if (!o.NoRegistry) WriteRegistry(dir, ra2, yr);

            // ---- shortcuts
            if (!o.NoShortcut) MakeShortcuts(dir, ra2, yr);

            Say("", "");
            Say("FERTIG. Starte das Spiel über die Desktop-Verknüpfung (sie nutzt den Schalter -CD).",
                "DONE. Start the game with the desktop shortcut (it uses the -CD switch).");
            Say("Ohne Verknüpfung: Game.exe -CD   bzw.   gamemd.exe -CD", "Without shortcut: Game.exe -CD   or   gamemd.exe -CD");
            Say("Notausgang im Spiel: Strg+Alt+Ende", "Emergency exit inside the game: Ctrl+Alt+End");
            return 0;
        }

        // ------------------------------------------------------------------ game folder
        static bool LooksLikeGame(string d)
        {
            if (!Directory.Exists(d)) return false;
            return File.Exists(Path.Combine(d, "ra2.mix")) || File.Exists(Path.Combine(d, "ra2md.mix")) ||
                   File.Exists(Path.Combine(d, "Game.exe")) || File.Exists(Path.Combine(d, "gamemd.exe"));
        }

        static string ResolveGameDir(Opts o)
        {
            if (!string.IsNullOrEmpty(o.Dir)) return Path.GetFullPath(o.Dir);
            string here = Directory.GetCurrentDirectory();
            if (LooksLikeGame(here)) return here;
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (LooksLikeGame(exeDir)) return exeDir;
            foreach (string key in new string[] { "Red Alert 2", "Yuri's Revenge" })
                foreach (RegistryView view in new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    try
                    {
                        using (RegistryKey hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                        using (RegistryKey k = hk.OpenSubKey(@"SOFTWARE\Westwood\" + key))
                        {
                            if (k == null) continue;
                            string ip = k.GetValue("InstallPath") as string;
                            if (!string.IsNullOrEmpty(ip))
                            {
                                string d = Path.GetDirectoryName(ip);
                                if (LooksLikeGame(d)) return d;
                            }
                        }
                    }
                    catch { }
                }
            if (yes) return null;
            Say("Bitte den Spielordner wählen (neuer/leerer Ordner ist ok, z.B. D:\\Spiele\\RA2).",
                "Please choose the game folder (a new/empty folder is fine, e.g. D:\\Games\\RA2).");
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                fb.Description = T("Spielordner für Alarmstufe Rot 2 / Yuris Rache", "Game folder for Red Alert 2 / Yuri's Revenge");
                fb.ShowNewFolderButton = true;
                if (fb.ShowDialog() == DialogResult.OK) return fb.SelectedPath;
            }
            return null;
        }

        // ------------------------------------------------------------------ sources / discs
        class Disc { public string Root; public string Kind; public string Marker; public bool Temp; }

        static readonly Dictionary<string, string> Markers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Ra1.dsk", "Alarmstufe Rot 2 - Alliierte / Red Alert 2 - Allied (Disk 1)" },
            { "Ra2.dsk", "Alarmstufe Rot 2 - Sowjets / Red Alert 2 - Soviet (Disk 2)" },
            { "YR1.dsk", "Yuris Rache / Yuri's Revenge" },
            { "YR2.dsk", "Yuris Rache / Yuri's Revenge (Disk 2)" }
        };

        static List<Disc> FindDiscs(Opts o)
        {
            List<Disc> discs = new List<Disc>();
            List<string> src = new List<string>(o.Src);
            if (src.Count == 0)
            {
                foreach (DriveInfo di in DriveInfo.GetDrives())
                {
                    try { if (di.DriveType == DriveType.CDRom && di.IsReady) src.Add(di.RootDirectory.FullName); } catch { }
                }
            }
            foreach (string s in src) ScanSource(s, discs);

            if (!yes)
            {
                Console.WriteLine();
                Say("Weitere Quellen? Laufwerk (z.B. H:\\), Ordner oder .img/.iso eingeben - Enter = weiter.",
                    "More sources? Enter a drive (e.g. H:\\), a folder or an .img/.iso - Enter = continue.");
                while (true)
                {
                    Console.Write("> ");
                    string line = Console.ReadLine();
                    if (line == null) break;
                    line = line.Trim().Trim('"');
                    if (line.Length == 0) break;
                    ScanSource(line, discs);
                }
            }
            return discs;
        }

        static void ScanSource(string path, List<Disc> discs)
        {
            if (path.Length == 2 && path[1] == ':') path += "\\";
            if (File.Exists(path))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".img" || ext == ".iso" || ext == ".bin")
                {
                    string tmp = ExtractImage(path);
                    if (tmp != null) ScanDir(tmp, discs, 0, true);
                }
                else Say("Nicht unterstützt: " + path, "Not supported: " + path);
                return;
            }
            if (Directory.Exists(path)) { ScanDir(path, discs, 0, false); return; }
            Say("Pfad nicht gefunden: " + path, "Path not found: " + path);
        }

        static string Find7z()
        {
            List<string> c = new List<string>();
            c.Add(Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\7-Zip\7z.exe"));
            c.Add(Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\7-Zip\7z.exe"));
            foreach (string p in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                if (p.Trim().Length > 0) c.Add(Path.Combine(p.Trim(), "7z.exe"));
            foreach (string f in c) { try { if (File.Exists(f)) return f; } catch { } }
            return null;
        }

        static string ExtractImage(string img)
        {
            string z = Find7z();
            if (z == null)
            {
                Say("Für .img/.iso-Dateien wird 7-Zip gebraucht (nicht gefunden). Entweder 7-Zip installieren, das Image mit ImgDrive/WinCDEmu als Laufwerk einbinden oder den Inhalt in einen Ordner entpacken und diesen angeben.",
                    "7-Zip is needed for .img/.iso files (not found). Install 7-Zip, mount the image as a drive (ImgDrive/WinCDEmu) or extract it into a folder and give that folder.");
                return null;
            }
            string tmp = Path.Combine(Path.GetTempPath(), "RA2Fix_" + Path.GetFileNameWithoutExtension(img));
            Directory.CreateDirectory(tmp);
            Say("Entpacke Image: " + img, "Extracting image: " + img);
            ProcessStartInfo psi = new ProcessStartInfo(z, "x -y -o\"" + tmp + "\" \"" + img + "\"");
            psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true;
            using (Process p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.WaitForExit(); if (p.ExitCode > 1) { Say("7-Zip Fehler " + p.ExitCode, "7-Zip error " + p.ExitCode); return null; } }
            return tmp;
        }

        static void ScanDir(string dir, List<Disc> discs, int depth, bool temp)
        {
            try
            {
                foreach (KeyValuePair<string, string> m in Markers)
                {
                    if (File.Exists(Path.Combine(dir, m.Key)))
                    {
                        foreach (Disc d in discs) if (string.Equals(d.Root, dir, StringComparison.OrdinalIgnoreCase)) return;
                        Disc nd = new Disc(); nd.Root = dir; nd.Kind = m.Value; nd.Marker = m.Key; nd.Temp = temp;
                        discs.Add(nd);
                        return;
                    }
                }
                if (depth >= 3) return;
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    string n = Path.GetFileName(sub);
                    if (n.StartsWith("$") || n.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)) continue;
                    ScanDir(sub, discs, depth + 1, temp);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        // ------------------------------------------------------------------ install from disc
        static readonly string[] SkipNames = { "secdrv.sys", "uninst.exe", "uninstll.exe", "unstllap.exe", "uninstap.exe", "autorun.exe", "autorun.inf" };

        static bool Skip(string name)
        {
            foreach (string s in SkipNames) if (name.Equals(s, StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void CopyFile(string from, string to)
        {
            FileInfo fi = new FileInfo(from);
            if (File.Exists(to) && new FileInfo(to).Length == fi.Length) return;
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            if (File.Exists(to)) File.SetAttributes(to, FileAttributes.Normal);
            File.Copy(from, to, true);
            File.SetAttributes(to, FileAttributes.Normal);
        }

        static void CopyTree(string from, string to)
        {
            foreach (string f in Directory.GetFiles(from)) if (!Skip(Path.GetFileName(f))) CopyFile(f, Path.Combine(to, Path.GetFileName(f)));
            foreach (string d in Directory.GetDirectories(from))
            {
                string n = Path.GetFileName(d);
                if (n.Equals("rmcache", StringComparison.OrdinalIgnoreCase)) continue;
                CopyTree(d, Path.Combine(to, n));
            }
        }

        static void InstallDisc(Disc d, string dir)
        {
            Say("", "");
            Say("--- Kopiere: " + d.Kind + " ---", "--- Copying: " + d.Kind + " ---");
            int n = 0;
            foreach (string f in Directory.GetFiles(d.Root, "*.mix"))
            {
                Say("  " + Path.GetFileName(f)); CopyFile(f, Path.Combine(dir, Path.GetFileName(f))); n++;
            }
            string inst = null;
            foreach (string sub in Directory.GetDirectories(d.Root))
                if (Path.GetFileName(sub).Equals("Install", StringComparison.OrdinalIgnoreCase)) inst = sub;
            if (inst != null)
            {
                List<string> cabs = new List<string>();
                foreach (string f in Directory.GetFiles(inst))
                {
                    string name = Path.GetFileName(f);
                    if (name.EndsWith(".cab", StringComparison.OrdinalIgnoreCase)) { cabs.Add(f); continue; }
                    if (Skip(name)) continue;
                    Say("  " + name); CopyFile(f, Path.Combine(dir, name)); n++;
                }
                foreach (string sub in Directory.GetDirectories(inst))
                {
                    if (Path.GetFileName(sub).Equals("rmcache", StringComparison.OrdinalIgnoreCase)) continue;
                    Say("  " + Path.GetFileName(sub) + "\\"); CopyTree(sub, Path.Combine(dir, Path.GetFileName(sub)));
                }
                if (cabs.Count > 0)
                {
                    // multi-part sets (Game1.cab ... GameN.cab): FDI follows a file that spans two cabinets,
                    // but files that START in a later cabinet need that cabinet to be processed on its own.
                    cabs.Sort(new CabOrder());
                    Say("  " + cabs.Count + T(" Cabinet-Datei(en): ", " cabinet file(s): ") + Path.GetFileName(cabs[0]) + (cabs.Count > 1 ? " ... " + Path.GetFileName(cabs[cabs.Count - 1]) : ""));
                    int cnt = 0;
                    foreach (string c in cabs)
                    {
                        int k = CabExtractor.Extract(c, dir, Skip);
                        if (Environment.GetEnvironmentVariable("RA2FIX_DEBUG") == "1") Say("    [debug] " + Path.GetFileName(c) + ": " + k + " | " + CabExtractor.DebugInfo);
                        cnt += k;
                    }
                    Say("    -> " + cnt + T(" Dateien entpackt", " files extracted"));
                }
            }
            string wol = Path.Combine(d.Root, "Wolapi");
            if (Directory.Exists(wol)) CopyTree(wol, Path.Combine(dir, "Wolapi"));
            // Yuri's Revenge: the German disc ships a Taunts folder inside the cabinet; nothing more to do.
        }

        // Game1.cab < Game2.cab < ... < Game10.cab (numeric, not alphabetic)
        class CabOrder : IComparer<string>
        {
            static int Num(string p)
            {
                Match m = Regex.Match(Path.GetFileNameWithoutExtension(p), @"(\d+)$");
                return m.Success ? int.Parse(m.Groups[1].Value) : 0;
            }
            public int Compare(string a, string b)
            {
                string pa = Regex.Replace(Path.GetFileNameWithoutExtension(a), @"\d+$", ""), pb = Regex.Replace(Path.GetFileNameWithoutExtension(b), @"\d+$", "");
                int c = string.Compare(pa, pb, StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : Num(a).CompareTo(Num(b));
            }
        }

        static void ReportMissing(string dir, bool ra2, bool yr)
        {
            List<string> miss = new List<string>();
            if (ra2)
            {
                foreach (string f in new string[] { "ra2.mix", "language.mix", "multi.mix", "theme.mix", "wdt.mix" })
                    if (!File.Exists(Path.Combine(dir, f))) miss.Add(f);
                if (!File.Exists(Path.Combine(dir, "movies01.mix")) || !File.Exists(Path.Combine(dir, "maps01.mix"))) miss.Add("movies01.mix / maps01.mix  (" + T("Allierten-CD", "Allied CD") + ")");
                if (!File.Exists(Path.Combine(dir, "movies02.mix")) || !File.Exists(Path.Combine(dir, "maps02.mix"))) miss.Add("movies02.mix / maps02.mix  (" + T("Sowjet-CD", "Soviet CD") + ")");
            }
            if (yr)
            {
                foreach (string f in new string[] { "ra2md.mix", "langmd.mix", "movmd03.mix", "mapsmd03.mix", "multimd.mix", "thememd.mix" })
                    if (!File.Exists(Path.Combine(dir, f))) miss.Add(f);
            }
            if (miss.Count > 0)
                Say("  Es fehlen noch: " + string.Join(", ", miss.ToArray()), "  Still missing: " + string.Join(", ", miss.ToArray()));
        }

        // ------------------------------------------------------------------ EXE patching (signature based)
        static readonly Dictionary<string, string> KnownExe = new Dictionary<string, string>
        {
            { "135A57A026AEBD62D80ED71A896AD9B5AB56ADA781CDF80BE99727849DB64743", "Alarmstufe Rot 2 - Game.exe (4.387.088 Byte)" },
            { "3C27D0A397939DC3F4BE5ADA526129923FBCF38BC20F43B4BE1107DA959C23A5", "Yuris Rache 1.000 deutsch - gamemd.exe (4.808.976 Byte)" }
        };

        // (marker string, what the function is for)
        static readonly string[][] Checks =
        {
            new string[] { "COPYPROTECTION - Checking if launcher is running", "Launcher-Mutex-Pruefung" },
            new string[] { "COPYPROTECTION - Notify launcher", "Launcher-Handshake (60 s Schleife)" },
            new string[] { "COPYPROTECTION - Validating", "Validierung" }
        };

        static string Sha(string file)
        {
            using (SHA256 s = SHA256.Create())
            using (FileStream fs = File.OpenRead(file))
                return BitConverter.ToString(s.ComputeHash(fs)).Replace("-", "");
        }

        static bool PatchExe(string path, bool checkOnly)
        {
            string name = Path.GetFileName(path);
            string bak = path + ".orig";
            string refFile = File.Exists(bak) ? bak : path;
            string h = Sha(refFile);
            string known;
            Say("  " + name + ": " + (KnownExe.TryGetValue(h, out known) ? T("bekannte Version - ", "known version - ") + known
                                                                      : T("unbekannte Version (Signatur-Suche), SHA-256 ", "unknown version (signature search), SHA-256 ") + h.Substring(0, 16) + "..."));

            byte[] d = File.ReadAllBytes(path);
            Pe pe = Pe.Parse(d);
            if (pe == null) { Say("    keine gültige EXE", "    not a valid EXE"); return false; }
            List<int> starts = new List<int>();
            foreach (string[] c in Checks)
            {
                int s = FindFunction(d, pe, c[0]);
                if (s < 0)
                {
                    Say("    Prüfung '" + c[1] + "' nicht gefunden - diese EXE wird NICHT verändert (bitte als Issue melden, SHA-256: " + h + ")",
                        "    check '" + c[1] + "' not found - this EXE is NOT modified (please report as an issue, SHA-256: " + h + ")");
                    return false;
                }
                starts.Add(s);
            }
            bool already = true;
            foreach (int s in starts) if (!(d[s] == 0xB0 && d[s + 1] == 0x01 && d[s + 2] == 0xC3)) already = false;
            if (already) { Say("    bereits gepatcht", "    already patched"); return true; }
            bool hasCdSwitch = IndexOf(d, Encoding.ASCII.GetBytes("No CD init - all files must be local"), 0) >= 0;
            if (!hasCdSwitch)
                Say("    Hinweis: Schalter -CD in dieser EXE nicht gefunden.", "    Note: -CD switch not found in this EXE.");
            for (int i = 0; i < starts.Count; i++)
            {
                uint va; pe.RawToVa(starts[i], out va);
                Say("    " + Checks[i][1] + ": VA 0x" + va.ToString("X") + "  " + BitConverter.ToString(d, starts[i], 3).Replace("-", " ") + " -> B0 01 C3");
            }
            if (checkOnly) return true;
            if (!File.Exists(bak)) File.Copy(path, bak);
            foreach (int s in starts) { d[s] = 0xB0; d[s + 1] = 0x01; d[s + 2] = 0xC3; } // mov al,1 ; ret
            File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllBytes(path, d);
            Say("    gepatcht (Sicherung: " + Path.GetFileName(bak) + ")", "    patched (backup: " + Path.GetFileName(bak) + ")");
            return true;
        }

        // finds the first byte of the function that pushes the address of `marker`
        static int FindFunction(byte[] d, Pe pe, string marker)
        {
            int so = IndexOf(d, Encoding.ASCII.GetBytes(marker), 0);
            if (so < 0) return -1;
            uint va; if (!pe.RawToVa(so, out va)) return -1;
            byte[] pat = new byte[] { 0x68, (byte)va, (byte)(va >> 8), (byte)(va >> 16), (byte)(va >> 24) };
            int from = 0;
            while (true)
            {
                int p = IndexOf(d, pat, from);
                if (p < 0) break;
                from = p + 1;
                if (!pe.IsCode(p)) continue;
                // already patched by this tool? ("mov al,1 ; ret" replaced the first 3 bytes of the function,
                // the rest of the prologue and the push of the marker string are still there)
                bool wasPatched = false;
                for (int q = p - 3; q >= p - 14 && q > 0; q--)
                {
                    if (d[q] == 0xB0 && d[q + 1] == 0x01 && d[q + 2] == 0xC3 &&
                        (d[q - 1] == 0x90 || d[q - 1] == 0xCC || d[q - 1] == 0xC3 || d[q - 1] == 0xC2) &&
                        PrologueOnly(d, q + 3, p))
                    { wasPatched = true; return q; }
                }
                if (wasPatched) continue;
                // walk back over a plain prologue: push r32 / sub esp,imm8
                int s = p;
                int guard = 0;
                while (guard++ < 12)
                {
                    byte b = d[s - 1];
                    if (b == 0x51 || b == 0x53 || b == 0x55 || b == 0x56 || b == 0x57) { s--; continue; }
                    if (s >= 3 && d[s - 3] == 0x83 && d[s - 2] == 0xEC) { s -= 3; continue; }
                    break;
                }
                // the function must start right after padding / the previous function's ret,
                // and must begin like a normal function (push r32 / sub esp / push imm)
                byte before = d[s - 1];
                bool padOk = before == 0x90 || before == 0xCC || before == 0xC3 || before == 0xC2;
                bool opOk = d[s] == 0x51 || d[s] == 0x53 || d[s] == 0x55 || d[s] == 0x56 || d[s] == 0x57 || d[s] == 0x83 || d[s] == 0x68;
                if (padOk && opOk) return s;
            }

            // Not found: the function may already be patched in a way that also destroyed the "push imm32"
            // (when the prologue before the push is 0-2 bytes long, "B0 01 C3" overwrites the push opcode
            // and/or the first immediate bytes). Look for  B0 01 C3 + the remaining immediate bytes.
            for (int k = 0; k <= 2; k++)
            {
                int tailStart = 3 - k - 1; // first immediate byte that survives (k=2 -> 0, k=1 -> 1, k=0 -> 2)
                if (k == 2) tailStart = 0;
                List<byte> rem = new List<byte>();
                rem.Add(0xB0); rem.Add(0x01); rem.Add(0xC3);
                for (int i = tailStart; i < 4; i++) rem.Add(pat[1 + i]);
                byte[] rp = rem.ToArray();
                int at = 0;
                while ((at = IndexOf(d, rp, at)) >= 0)
                {
                    int q = at; at++;
                    if (q > 0 && pe.IsCode(q) && (d[q - 1] == 0x90 || d[q - 1] == 0xCC || d[q - 1] == 0xC3 || d[q - 1] == 0xC2)) return q;
                }
            }
            return -1;
        }

        // bytes [from, to) consist only of push r32 / sub esp,imm8 (the rest of a function prologue)
        static bool PrologueOnly(byte[] d, int from, int to)
        {
            int i = from;
            while (i < to)
            {
                byte b = d[i];
                if (b == 0x51 || b == 0x53 || b == 0x55 || b == 0x56 || b == 0x57) { i++; continue; }
                if (b == 0x83 && i + 2 < to + 1 && d[i + 1] == 0xEC) { i += 3; continue; }
                return false;
            }
            return i == to;
        }

        static int IndexOf(byte[] h, byte[] n, int start)
        {
            int last = h.Length - n.Length;
            for (int i = start; i <= last; i++)
            {
                if (h[i] != n[0]) continue;
                int j = 1;
                while (j < n.Length && h[i + j] == n[j]) j++;
                if (j == n.Length) return i;
            }
            return -1;
        }

        class Sec { public string Name; public uint Va, VSize, Raw, RSize, Chars; }
        class Pe
        {
            public uint ImageBase; public List<Sec> Secs = new List<Sec>();
            public static Pe Parse(byte[] d)
            {
                if (d.Length < 0x100 || d[0] != 'M' || d[1] != 'Z') return null;
                int pe = BitConverter.ToInt32(d, 0x3C);
                if (pe < 0 || pe + 0x100 > d.Length || d[pe] != 'P' || d[pe + 1] != 'E') return null;
                int n = BitConverter.ToUInt16(d, pe + 6);
                int opt = BitConverter.ToUInt16(d, pe + 20);
                Pe r = new Pe();
                r.ImageBase = BitConverter.ToUInt32(d, pe + 24 + 28);
                int s = pe + 24 + opt;
                for (int i = 0; i < n; i++)
                {
                    int o = s + i * 40;
                    Sec x = new Sec();
                    x.Name = Encoding.ASCII.GetString(d, o, 8).TrimEnd('\0');
                    x.VSize = BitConverter.ToUInt32(d, o + 8); x.Va = BitConverter.ToUInt32(d, o + 12);
                    x.RSize = BitConverter.ToUInt32(d, o + 16); x.Raw = BitConverter.ToUInt32(d, o + 20);
                    x.Chars = BitConverter.ToUInt32(d, o + 36);
                    r.Secs.Add(x);
                }
                return r;
            }
            public bool RawToVa(int raw, out uint va)
            {
                foreach (Sec s in Secs)
                    if (raw >= s.Raw && raw < s.Raw + s.RSize) { va = ImageBase + s.Va + (uint)(raw - s.Raw); return true; }
                va = 0; return false;
            }
            public bool IsCode(int raw)
            {
                foreach (Sec s in Secs)
                    if (raw >= s.Raw && raw < s.Raw + s.RSize) return (s.Chars & 0x20000000) != 0;
                return false;
            }
        }

        // ------------------------------------------------------------------ DDrawCompat
        static void InstallDdraw(string dir)
        {
            Say("", "");
            Say("--- DDrawCompat (Grafik-Fix für Windows 10/11) ---", "--- DDrawCompat (graphics fix for Windows 10/11) ---");
            string target = Path.Combine(dir, "ddraw.dll");
            if (File.Exists(target) && Sha(target) == DdrawSha256) { Say("  ddraw.dll bereits installiert (v0.7.2).", "  ddraw.dll already installed (v0.7.2)."); return; }
            string local = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ddraw.dll");
            if (File.Exists(local) && Sha(local) == DdrawSha256)
            {
                File.Copy(local, target, true); Say("  ddraw.dll neben dem Patcher verwendet.", "  used ddraw.dll next to the patcher."); return;
            }
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                string tmp = Path.Combine(Path.GetTempPath(), "RA2Fix_ddc.zip");
                Say("  Lade " + DdrawUrl, "  Downloading " + DdrawUrl);
                using (WebClient wc = new WebClient()) wc.DownloadFile(DdrawUrl, tmp);
                if (Sha(tmp) != DdrawZipSha256) throw new Exception(T("Prüfsumme des Downloads stimmt nicht - abgebrochen.", "Download checksum mismatch - aborted."));
                string ex = Path.Combine(Path.GetTempPath(), "RA2Fix_ddc");
                if (Directory.Exists(ex)) Directory.Delete(ex, true);
                System.IO.Compression.ZipFile.ExtractToDirectory(tmp, ex);
                string dll = Path.Combine(ex, "ddraw.dll");
                if (Sha(dll) != DdrawSha256) throw new Exception(T("ddraw.dll hat eine unerwartete Prüfsumme - abgebrochen.", "ddraw.dll has an unexpected checksum - aborted."));
                File.Copy(dll, target, true);
                Say("  ddraw.dll installiert (SHA-256 geprüft).", "  ddraw.dll installed (SHA-256 verified).");
            }
            catch (Exception e)
            {
                Say("  DDrawCompat konnte nicht geladen werden: " + e.Message + "\n  Lege ddraw.dll (v0.7.2 von github.com/narzoul/DDrawCompat) neben den Patcher und starte ihn erneut.",
                    "  Could not get DDrawCompat: " + e.Message + "\n  Put ddraw.dll (v0.7.2 from github.com/narzoul/DDrawCompat) next to the patcher and run it again.");
            }
        }

        // ------------------------------------------------------------------ registry / shortcuts
        static void WriteRegistry(string dir, bool ra2, bool yr)
        {
            Say("", "");
            Say("--- Registry ---", "--- Registry ---");
            try
            {
                if (ra2) RegKey("Red Alert 2", Path.Combine(dir, "Ra2.exe"));
                if (yr) RegKey("Yuri's Revenge", Path.Combine(dir, "Ra2md.exe"));
                Say("  Westwood-Einträge gesetzt.", "  Westwood entries written.");
            }
            catch (Exception e)
            {
                Say("  Registry nicht beschreibbar (" + e.Message + "). Meist unkritisch; sonst den Patcher als Administrator starten.",
                    "  Registry not writable (" + e.Message + "). Usually harmless; otherwise run the patcher as administrator.");
            }
        }

        static void RegKey(string product, string exePath)
        {
            using (RegistryKey hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey k = hk.CreateSubKey(@"SOFTWARE\WOW6432Node\Westwood\" + product))
            {
                k.SetValue("InstallPath", exePath);
                k.SetValue("Name", product);
            }
        }

        static void MakeShortcuts(string dir, bool ra2, bool yr)
        {
            Say("", "");
            Say("--- Desktop-Verknüpfungen ---", "--- Desktop shortcuts ---");
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (ra2) Shortcut(Path.Combine(desk, "Alarmstufe Rot 2 (Win10 Fix).lnk"), Path.Combine(dir, "Game.exe"), dir);
            if (yr) Shortcut(Path.Combine(desk, "Yuris Rache (Win10 Fix).lnk"), Path.Combine(dir, "gamemd.exe"), dir);
        }

        static void Shortcut(string lnk, string target, string dir)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object sh = Activator.CreateInstance(t);
                object l = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
                Type lt = l.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, l, new object[] { target });
                lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, l, new object[] { "-CD" });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, l, new object[] { dir });
                string ico = Path.Combine(dir, "RA2.ico");
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, l, new object[] { File.Exists(ico) ? ico : target });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, l, null);
                Say("  " + Path.GetFileName(lnk));
            }
            catch (Exception e) { Say("  Verknüpfung nicht möglich: " + e.Message, "  Shortcut failed: " + e.Message); }
        }

        // ------------------------------------------------------------------ undo
        static int Undo(string dir)
        {
            Say("--- Rückgängig ---", "--- Undo ---");
            foreach (string exe in new string[] { "Game.exe", "gamemd.exe" })
            {
                string p = Path.Combine(dir, exe), b = p + ".orig";
                if (File.Exists(b)) { File.SetAttributes(p, FileAttributes.Normal); File.Copy(b, p, true); File.Delete(b); Say("  " + exe + " wiederhergestellt", "  " + exe + " restored"); }
            }
            string dd = Path.Combine(dir, "ddraw.dll");
            if (File.Exists(dd) && Sha(dd) == DdrawSha256) { File.Delete(dd); Say("  ddraw.dll entfernt", "  ddraw.dll removed"); }
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            foreach (string n in new string[] { "Alarmstufe Rot 2 (Win10 Fix).lnk", "Yuris Rache (Win10 Fix).lnk" })
            {
                string f = Path.Combine(desk, n);
                if (File.Exists(f)) { File.Delete(f); Say("  " + n + " gelöscht", "  " + n + " deleted"); }
            }
            Say("Kopierte Spieldateien bleiben erhalten.", "Copied game files are kept.");
            return 0;
        }
    }

    // ---------------------------------------------------------------------- Windows cabinet.dll (FDI) - supports spanned cabinets
    static class CabExtractor
    {
        [StructLayout(LayoutKind.Sequential)]
        struct ERF { public int erfOper; public int erfType; public int fError; }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct FDINOTIFICATION
        {
            public int cb; public IntPtr psz1, psz2, psz3, pv, hf;
            public ushort date, time, attribs, setID, iCabinet, iFolder;
            public int fdie;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr FnAlloc(uint cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void FnFree(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr FnOpen([MarshalAs(UnmanagedType.LPStr)] string f, int oflag, int pmode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint FnRead(IntPtr hf, IntPtr pv, uint cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint FnWrite(IntPtr hf, IntPtr pv, uint cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int FnClose(IntPtr hf);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int FnSeek(IntPtr hf, int dist, int type);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr FnNotify(int fdint, ref FDINOTIFICATION n);

        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr FDICreate(FnAlloc a, FnFree f, FnOpen o, FnRead r, FnWrite w, FnClose c, FnSeek s, int cpu, ref ERF erf);
        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        static extern int FDICopy(IntPtr hfdi, string cabName, string cabPath, int flags, FnNotify n, IntPtr dec, IntPtr user);
        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int FDIDestroy(IntPtr hfdi);

        static Dictionary<int, FileStream> files = new Dictionary<int, FileStream>();
        static int nextId = 1;
        static string target;
        static Predicate<string> skip;
        static int count;
        static FnAlloc fa; static FnFree ff; static FnOpen fo; static FnRead fr; static FnWrite fw; static FnClose fc; static FnSeek fs; static FnNotify fn;

        static IntPtr DoAlloc(uint cb) { return Marshal.AllocHGlobal((int)cb); }
        static void DoFree(IntPtr p) { Marshal.FreeHGlobal(p); }
        static IntPtr DoOpen(string f, int oflag, int pmode)
        {
            try { FileStream s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read); int id = nextId++; files[id] = s; return (IntPtr)id; }
            catch { return (IntPtr)(-1); }
        }
        static uint DoRead(IntPtr hf, IntPtr pv, uint cb)
        {
            FileStream s = files[(int)hf]; byte[] b = new byte[cb];
            int n = s.Read(b, 0, (int)cb); Marshal.Copy(b, 0, pv, n); return (uint)n;
        }
        static uint DoWrite(IntPtr hf, IntPtr pv, uint cb)
        {
            FileStream s = files[(int)hf]; byte[] b = new byte[cb];
            Marshal.Copy(pv, b, 0, (int)cb); s.Write(b, 0, (int)cb); return cb;
        }
        static int DoClose(IntPtr hf)
        {
            FileStream s; int id = (int)hf;
            if (files.TryGetValue(id, out s)) { s.Dispose(); files.Remove(id); }
            return 0;
        }
        static int DoSeek(IntPtr hf, int dist, int type) { return (int)files[(int)hf].Seek(dist, (SeekOrigin)type); }

        public static string DebugInfo = "";
        static int[] notifyCounts = new int[8];
        static IntPtr DoNotify(int t, ref FDINOTIFICATION n)
        {
            if (t >= 0 && t < 8) notifyCounts[t]++;
            DebugInfo = "info=" + notifyCounts[0] + " partial=" + notifyCounts[1] + " copy=" + notifyCounts[2] + " close=" + notifyCounts[3] + " next=" + notifyCounts[4] + " enum=" + notifyCounts[5];
            switch (t)
            {
                case 2: // fdintCOPY_FILE
                    {
                        string name = Marshal.PtrToStringAnsi(n.psz1).TrimStart('\\', '/'); // cabinet entries may start with "\"
                        if (skip != null && skip(Path.GetFileName(name))) return IntPtr.Zero;
                        string root = Path.GetFullPath(target).TrimEnd('\\') + "\\";
                        string full = Path.GetFullPath(Path.Combine(target, name));
                        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero; // path traversal guard
                        Directory.CreateDirectory(Path.GetDirectoryName(full));
                        if (File.Exists(full)) File.SetAttributes(full, FileAttributes.Normal);
                        FileStream s = new FileStream(full, FileMode.Create, FileAccess.Write);
                        int id = nextId++; files[id] = s; return (IntPtr)id;
                    }
                case 3: // fdintCLOSE_FILE_INFO
                    DoClose(n.hf); count++; return (IntPtr)1;
                default: // CABINET_INFO, PARTIAL_FILE, NEXT_CABINET (same folder), ENUMERATE
                    return IntPtr.Zero;
            }
        }

        public static int Extract(string firstCab, string targetDir, Predicate<string> skipFile)
        {
            target = targetDir; skip = skipFile; count = 0;
            fa = DoAlloc; ff = DoFree; fo = DoOpen; fr = DoRead; fw = DoWrite; fc = DoClose; fs = DoSeek; fn = DoNotify;
            ERF erf = new ERF();
            IntPtr h = FDICreate(fa, ff, fo, fr, fw, fc, fs, 1, ref erf);
            if (h == IntPtr.Zero) throw new Exception("FDICreate failed (" + erf.erfOper + ")");
            try
            {
                string dir = Path.GetDirectoryName(firstCab);
                if (!dir.EndsWith("\\")) dir += "\\";
                if (FDICopy(h, Path.GetFileName(firstCab), dir, 0, fn, IntPtr.Zero, IntPtr.Zero) == 0)
                    throw new Exception("FDICopy failed (" + erf.erfOper + "/" + erf.erfType + ")");
            }
            finally { FDIDestroy(h); foreach (FileStream s in files.Values) s.Dispose(); files.Clear(); }
            return count;
        }
    }
}
