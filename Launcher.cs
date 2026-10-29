// ps3recomp.exe - GUI launcher for the PS3Recomp Windows pipeline.
// Compiles with the .NET Framework C# compiler that ships with Windows (C# 5).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("PS3Recomp Launcher")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace PS3RecompLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    class MainForm : Form
    {
        readonly string root;      // folder that holds ps3recomp.exe (the extracted package)
        readonly string sdk, tools, template, iniPath;
        TextBox txtElf;
        CheckBox chkForce;
        Button btnBrowse, btnCheck, btnBuild, btnRun, btnFolder, btnStop, btnSln;
        RichTextBox log;
        Label status;
        volatile Process current;
        volatile bool cancel;
        bool busy;

        public MainForm()
        {
            root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            sdk = Path.Combine(root, "ps3recomp-master");
            tools = Path.Combine(sdk, "tools");
            template = Path.Combine(sdk, "templates\\project");
            iniPath = Path.Combine(root, "ps3recomp-launcher.ini");

            Text = "PS3Recomp Launcher";
            Width = 860; Height = 600; MinimumSize = new Size(700, 420);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            Label l = new Label(); l.Text = "EBOOT.ELF (decrypted):"; l.Left = 12; l.Top = 16; l.AutoSize = true;
            txtElf = new TextBox(); txtElf.Left = 160; txtElf.Top = 12; txtElf.Width = 570;
            txtElf.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnBrowse = Btn("Browse...", 740, 11, 90); btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowse.Click += OnBrowse;

            chkForce = new CheckBox(); chkForce.Text = "Force redo all steps"; chkForce.Left = 160; chkForce.Top = 42; chkForce.AutoSize = true;

            btnCheck = Btn("Check tools", 12, 70, 110); btnCheck.Click += delegate { Start(CheckTools); };
            btnBuild = Btn("Build game", 130, 70, 110); btnBuild.Click += delegate { Start(Build); };
            btnRun = Btn("Run game", 248, 70, 110); btnRun.Click += OnRun;
            btnFolder = Btn("Open output", 366, 70, 110); btnFolder.Click += OnFolder;
            btnSln = Btn("Open .sln", 484, 70, 110); btnSln.Click += OnSln;
            btnStop = Btn("Stop", 602, 70, 80); btnStop.Enabled = false; btnStop.Click += OnStop;

            log = new RichTextBox(); log.Left = 12; log.Top = 104; log.Width = 820; log.Height = 420;
            log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            log.ReadOnly = true; log.BackColor = Color.FromArgb(24, 24, 28); log.ForeColor = Color.Gainsboro;
            log.Font = new Font("Consolas", 9f); log.WordWrap = false; log.HideSelection = false;

            status = new Label(); status.Left = 12; status.Top = 532; status.Width = 820; status.Height = 20;
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; status.Text = "Ready.";

            Controls.AddRange(new Control[] { l, txtElf, btnBrowse, chkForce, btnCheck, btnBuild, btnRun, btnFolder, btnSln, btnStop, log, status });

            string saved = ReadIni("elf");
            txtElf.Text = !string.IsNullOrEmpty(saved) ? saved : Path.Combine(root, "input\\EBOOT.ELF");
            Load += delegate
            {
                if (!Directory.Exists(sdk))
                    Log("[ERROR] ps3recomp-master folder not found next to ps3recomp.exe.\r\n        Place ps3recomp.exe in the extracted PS3Recomp_Windows_VS2022 folder.", Color.OrangeRed);
                else Log("Ready. Choose an EBOOT.ELF and click 'Build game'.", Color.LightGreen);
                RefreshButtons();
            };
            FormClosing += delegate { WriteIni("elf", txtElf.Text); Kill(); };
        }

        Button Btn(string text, int x, int y, int w)
        {
            Button b = new Button(); b.Text = text; b.Left = x; b.Top = y; b.Width = w; b.Height = 27; return b;
        }

        // ---------- settings ----------
        string ReadIni(string key)
        {
            try
            {
                if (!File.Exists(iniPath)) return null;
                foreach (string line in File.ReadAllLines(iniPath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0 && line.Substring(0, i).Trim() == key) return line.Substring(i + 1).Trim();
                }
            }
            catch { }
            return null;
        }
        void WriteIni(string key, string value)
        {
            try { File.WriteAllText(iniPath, key + "=" + value + "\r\n"); } catch { }
        }

        // ---------- UI helpers ----------
        void Log(string text, Color? color)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { Log(text, color); })); return; }
            log.SelectionStart = log.TextLength; log.SelectionLength = 0;
            log.SelectionColor = color.HasValue ? color.Value : Color.Gainsboro;
            log.AppendText(text + "\r\n");
            log.ScrollToCaret();
        }
        void SetStatus(string s)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { SetStatus(s); })); return; }
            status.Text = s;
        }
        void SetBusy(bool b)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { SetBusy(b); })); return; }
            busy = b; RefreshButtons();
        }
        void RefreshButtons()
        {
            btnCheck.Enabled = btnBuild.Enabled = btnBrowse.Enabled = btnSln.Enabled = !busy;
            btnRun.Enabled = btnFolder.Enabled = !busy;
            btnStop.Enabled = busy;
        }

        // ---------- paths ----------
        string PortName() { string n = Path.GetFileNameWithoutExtension(txtElf.Text.Trim()); return string.IsNullOrEmpty(n) ? "EBOOT" : n; }
        string PortDir() { return Path.Combine(root, "ports\\" + PortName()); }
        string BuildDir() { return Path.Combine(PortDir(), "build"); }

        // ---------- button handlers ----------
        void OnBrowse(object s, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Title = "Select decrypted EBOOT.ELF";
            d.Filter = "PS3 ELF (*.elf;*.self)|*.elf;*.self|All files (*.*)|*.*";
            try { d.InitialDirectory = Path.GetDirectoryName(txtElf.Text); } catch { }
            if (d.ShowDialog(this) == DialogResult.OK) { txtElf.Text = d.FileName; WriteIni("elf", d.FileName); }
        }
        void OnStop(object s, EventArgs e) { cancel = true; Kill(); Log("[STOP] Cancelled by user.", Color.Gold); }
        void OnSln(object s, EventArgs e)
        {
            string p = Path.Combine(root, "PS3RecompWindows.sln");
            if (File.Exists(p)) Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
            else Log("[ERROR] PS3RecompWindows.sln not found.", Color.OrangeRed);
        }
        void OnFolder(object s, EventArgs e)
        {
            string d = Directory.Exists(BuildDir()) ? BuildDir() : (Directory.Exists(PortDir()) ? PortDir() : root);
            Process.Start("explorer.exe", "\"" + d + "\"");
        }
        void OnRun(object s, EventArgs e)
        {
            string exe = FindGameExe();
            if (exe == null) { Log("[ERROR] No game .exe found in " + BuildDir() + " - run 'Build game' first.", Color.OrangeRed); return; }
            try
            {
                Log("[RUN] " + exe, Color.LightGreen);
                Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
            }
            catch (Exception ex) { Log("[ERROR] " + ex.Message, Color.OrangeRed); }
        }
        string FindGameExe()
        {
            string b = BuildDir();
            if (!Directory.Exists(b)) return null;
            string best = null; DateTime bt = DateTime.MinValue;
            foreach (string f in Directory.GetFiles(b, "*.exe", SearchOption.AllDirectories))
            {
                if (f.IndexOf("CMakeFiles", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                DateTime t = File.GetLastWriteTime(f);
                if (t > bt) { bt = t; best = f; }
            }
            return best;
        }

        // ---------- job runner ----------
        void Start(Func<bool> job)
        {
            if (busy) return;
            cancel = false; SetBusy(true);
            Thread t = new Thread(delegate ()
            {
                bool ok = false;
                try { ok = job(); }
                catch (Exception ex) { Log("[ERROR] " + ex.Message, Color.OrangeRed); }
                SetStatus(cancel ? "Cancelled." : (ok ? "Done." : "Failed - see log."));
                SetBusy(false);
            });
            t.IsBackground = true; t.Start();
        }

        void Kill()
        {
            Process p = current;
            if (p == null) return;
            try { if (!p.HasExited) Process.Start(new ProcessStartInfo("taskkill", "/T /F /PID " + p.Id) { CreateNoWindow = true, UseShellExecute = false }); } catch { }
        }

        string vcvars; bool vcvarsSearched;
        string FindVcvars()
        {
            if (vcvarsSearched) return vcvars;
            vcvarsSearched = true;
            try
            {
                string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                string vswhere = Path.Combine(pf86 ?? "", "Microsoft Visual Studio\\Installer\\vswhere.exe");
                if (File.Exists(vswhere))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(vswhere, "-latest -products * -property installationPath");
                    psi.RedirectStandardOutput = true; psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    using (Process p = Process.Start(psi))
                    {
                        string o = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit();
                        if (o.Length > 0)
                        {
                            string v = Path.Combine(o.Split('\n')[0].Trim(), "VC\\Auxiliary\\Build\\vcvars64.bat");
                            if (File.Exists(v)) vcvars = v;
                        }
                    }
                }
            }
            catch { }
            return vcvars;
        }

        // Runs "cmd /c <command>" (inside the VS x64 environment when available), streaming output to the log.
        int Run(string command, string workDir)
        {
            if (cancel) return -1;
            string vc = FindVcvars();
            string full = vc != null ? "call \"" + vc + "\" >nul && " + command : command;
            Log("> " + command, Color.DeepSkyBlue);
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/d /s /c \"" + full + "\"");
            psi.WorkingDirectory = workDir ?? root;
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8; psi.StandardErrorEncoding = Encoding.UTF8;
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) Log(e.Data, null); };
                p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) Log(e.Data, Color.Khaki); };
                p.Start(); current = p;
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                p.WaitForExit();
                current = null;
                return cancel ? -1 : p.ExitCode;
            }
        }

        // Quiet probe: returns true when the command exits 0.
        bool Probe(string command)
        {
            try
            {
                string vc = FindVcvars();
                string full = vc != null ? "call \"" + vc + "\" >nul && " + command : command;
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/d /s /c \"" + full + " >nul 2>&1\"");
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi)) { p.WaitForExit(); return p.ExitCode == 0; }
            }
            catch { return false; }
        }

        string pythonCmd;
        bool FindPython()
        {
            const string chk = " -c \"import sys; raise SystemExit(0 if sys.version_info>=(3,10) else 1)\"";
            if (Probe("py -3" + chk)) { pythonCmd = "py -3"; return true; }
            if (Probe("python" + chk)) { pythonCmd = "python"; return true; }
            pythonCmd = null; return false;
        }

        // ---------- jobs ----------
        bool CheckTools()
        {
            SetStatus("Checking tools...");
            Log("--- Tool check ---", Color.White);
            bool all = true;
            string vc = FindVcvars();
            Log(vc != null ? "[OK] Visual Studio x64 environment: " + vc : "[MISSING] Visual Studio 2022 (C++ desktop workload) - needed for the Windows SDK", vc != null ? Color.LightGreen : Color.OrangeRed);
            all &= vc != null;
            if (FindPython()) Log("[OK] Python 3.10+ (" + pythonCmd + ")", Color.LightGreen);
            else { Log("[MISSING] Python 3.10+ (https://www.python.org/downloads/windows/ - tick 'Add to PATH')", Color.OrangeRed); all = false; }
            string[] need = { "cmake", "ninja", "clang-cl" };
            foreach (string t in need)
            {
                bool ok = Probe("where " + t);
                Log((ok ? "[OK] " : "[MISSING] ") + t, ok ? Color.LightGreen : Color.OrangeRed);
                all &= ok;
            }
            if (pythonCmd != null)
            {
                bool deps = Probe(pythonCmd + " -c \"import elftools\"");
                Log(deps ? "[OK] Python requirements (pyelftools)" : "[INFO] Python requirements not installed - 'Build game' will install them.", deps ? Color.LightGreen : Color.Gold);
            }
            bool elf = File.Exists(txtElf.Text.Trim());
            Log((elf ? "[OK] ELF found: " : "[MISSING] ELF not found: ") + txtElf.Text.Trim(), elf ? Color.LightGreen : Color.OrangeRed);
            Log(all ? "All required tools found." : "Install the missing tools above, then check again.", all ? Color.LightGreen : Color.Gold);
            return all;
        }

        bool Build()
        {
            string elf = txtElf.Text.Trim().Trim('"');
            WriteIni("elf", elf);
            if (!File.Exists(elf)) { Log("[ERROR] ELF not found: " + elf, Color.OrangeRed); return false; }
            if (!File.Exists(Path.Combine(tools, "ppu_loader.py"))) { Log("[ERROR] ps3recomp-master\\tools not found.", Color.OrangeRed); return false; }
            if (!FindPython()) { Log("[ERROR] Python 3.10+ not found. Use 'Check tools'.", Color.OrangeRed); return false; }
            foreach (string t in new string[] { "cmake", "ninja", "clang-cl" })
                if (!Probe("where " + t)) { Log("[ERROR] '" + t + "' not found in PATH. Use 'Check tools'.", Color.OrangeRed); return false; }

            bool force = false;
            Invoke(new Action(delegate { force = chkForce.Checked; }));
            string port = PortDir(), analysis = Path.Combine(port, "analysis"), recomp = Path.Combine(port, "recompiled"), build = BuildDir();
            Directory.CreateDirectory(analysis); Directory.CreateDirectory(recomp);
            if (force && Directory.Exists(build)) { Log("Force: removing old build folder", Color.Gold); try { Directory.Delete(build, true); } catch (Exception ex) { Log("[WARN] " + ex.Message, Color.Gold); } }

            if (!File.Exists(Path.Combine(port, "CMakeLists.txt")))
            {
                Log("Copying project template...", Color.White);
                if (Run("xcopy \"" + template + "\\*\" \"" + port + "\\\" /E /I /Y /Q", root) != 0) return false;
            }

            SetStatus("Installing Python requirements...");
            string req = Path.Combine(tools, "requirements.txt");
            if (File.Exists(req) && !Probe(pythonCmd + " -c \"import elftools\""))
                if (Run(pythonCmd + " -m pip install -r \"" + req + "\"", root) != 0) return false;

            string fn = Path.Combine(analysis, "EBOOT.functions.json"), im = Path.Combine(analysis, "EBOOT.imports.json");
            // Analysis output is named after the ELF stem
            string stem = PortName();
            fn = Path.Combine(analysis, stem + ".functions.json"); im = Path.Combine(analysis, stem + ".imports.json");

            if (force || !File.Exists(fn) || !File.Exists(im))
            {
                SetStatus("[1/4] Analysing ELF...");
                if (Run(pythonCmd + " \"" + Path.Combine(tools, "ppu_loader.py") + "\" \"" + elf + "\" --output \"" + analysis + "\"", root) != 0) return false;
                if (!File.Exists(fn) || !File.Exists(im)) { Log("[ERROR] Loader did not produce functions/imports JSON.", Color.OrangeRed); return false; }
            }
            else Log("[1/4] Analysis already present - skipped.", Color.Gold);

            string hdr = Path.Combine(recomp, "ppu_recomp.h");
            if (force || !File.Exists(hdr))
            {
                SetStatus("[2/4] Lifting PPU code to C/C++ (this can take a long time)...");
                if (Run(pythonCmd + " \"" + Path.Combine(tools, "ppu_lifter.py") + "\" \"" + elf + "\" --functions \"" + fn + "\" --hle-stubs \"" + im + "\" --output \"" + recomp + "\"", root) != 0) return false;
                if (!File.Exists(hdr)) { Log("[ERROR] Lifter did not produce ppu_recomp.h.", Color.OrangeRed); return false; }
            }
            else Log("[2/4] Lifted code already present - skipped.", Color.Gold);

            string nids = Path.Combine(recomp, "ppu_hle_nids.cpp");
            if (force || !File.Exists(nids))
            {
                SetStatus("[3/4] Generating HLE import table...");
                if (Run(pythonCmd + " \"" + Path.Combine(tools, "gen_hle_nids.py") + "\" --all --out \"" + nids + "\"", root) != 0) return false;
            }
            else Log("[3/4] HLE table already present - skipped.", Color.Gold);

            SetStatus("[4/4] Configuring and compiling (CMake + Ninja + clang-cl)...");
            if (Run("cmake -S \"" + port + "\" -B \"" + build + "\" -G Ninja -DCMAKE_BUILD_TYPE=Release \"-DPS3RECOMP_DIR=" + sdk + "\" \"-DRECOMP_DIR=" + recomp + "\" -DCMAKE_C_COMPILER=clang-cl -DCMAKE_CXX_COMPILER=clang-cl", root) != 0) return false;
            if (Run("cmake --build \"" + build + "\"", root) != 0) return false;

            string exe = FindGameExe();
            Log(exe != null ? "[OK] Built: " + exe : "[OK] Build finished, but no .exe was found in " + build, Color.LightGreen);
            Log("Note: a successful build does not guarantee the game boots or is playable.", Color.Gold);
            return true;
        }
    }
}
