﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using libdebug;
using PS4_Debugger.Properties;
using SharpDisasm;
using Be.Windows.Forms;
using RTF;
using System.Text.RegularExpressions;
using KeystoneEngine;
using System.Threading;
using System.IO;

namespace PS4_Debugger
{
    public partial class Form1 : Form
    {
        static PS4DBG PS4;
        TextBox rBox;
        uint _lwpid, _status;
        string _tdname;
        static reg64 _regs;
        fpreg64 _fpregs;
        dbreg64 _dbregs;
        public static byte[] oldBytes, write, result, Bytedata;
        string dasAddress = "0x00001000", data;
        private ulong address => Convert.ToUInt64(dasAddress.Trim().Replace("0x", ""), 16);
        string[] registers = { "rax", "rbx", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15" };
        ulong[] _regValue = new ulong[10];
        public struct GameInfo
        {
            public string
                titleID,
                version,
                username;
            public GameInfo(PS4DBG PS4)
            {
                string procName = "SceCdlgApp";
                string entryName = "libSceCdlgUtilServer.sprx";
                string uProcName = "SceShellUI";
                string uEntryName = "SceLoginMgrSmallStackJobQueue";
                ulong _userName = 0xEA494;
                ulong _titleId = 0xA0;
                ulong _version = 0xC8;
                int prot = 3;

                string[] result = new string[3];

                try
                {
                    ProcessList pl = PS4.GetProcessList();

                    foreach (Process p in pl.processes)
                    {
                        if (p.name == procName)
                        {
                            ProcessInfo pi = PS4.GetProcessInfo(p.pid);

                            for (int i = 0; i < pi.entries.Length; i++)
                            {
                                MemoryEntry me = pi.entries[i];

                                if (me.prot == prot && me.name == entryName)
                                {
                                    result[0] = PS4.ReadString(p.pid, me.start + _titleId);
                                    result[1] = PS4.ReadString(p.pid, me.start + _version);
                                }
                            }
                        }
                        if (p.name == uProcName)
                        {
                            ProcessInfo p2 = PS4.GetProcessInfo(p.pid);
                            for (int i = 0; i < p2.entries.Length; i++)
                            {
                                MemoryEntry m = p2.entries[i];
                                if (m.prot == prot && m.name == uEntryName)
                                    result[2] = PS4.ReadString(p.pid, m.start + _userName);
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, ex.Source, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                titleID = result[0];
                version = result[1];
                username = result[2];
            }
        }
        struct InstructionData
        {
            public int Length;
            public byte[] Bytes;
            public string Operation, SBytes;
            public ulong Address, nAddress, bAddress;

            public InstructionData(ulong _address, int plusOne = 0)
            {
                ArchitectureMode architecture = ArchitectureMode.x86_64;
                Disassembler.Translator.IncludeAddress = false;
                Disassembler.Translator.IncludeBinary = false;
                Instruction[] array = new Disassembler(PS4.ReadMemory(PID, _address, 100), architecture, _address, copyBinaryToInstruction: true, Vendor.Any, 0ul).Disassemble().ToArray();

                Length = array[plusOne].Bytes.Length;
                Bytes = array[plusOne].Bytes;
                SBytes = BitConverter.ToString(array[0].Bytes);
                Operation = array[plusOne].ToString();
                Address = array[0].Offset;
                nAddress = array[1].Offset;
                bAddress = array[0].Offset - (ulong)array[1].Bytes.Length + 1;
            }
        }
        struct CheatCodes
        {
            public string[] CUSA, GamesData, Cheats;
            private string[] Games;
            public CheatCodes(string input)
            {
                Games = input.Split('\n');
                CUSA = new string[input.Length];
                GamesData = new string[input.Length];
                Cheats = new string[input.Length];
                int g = 0;
                for (int i = 0; i < Games.Length; i++)
                {
                    if (Games[i].StartsWith("#"))
                    {
                        CUSA[i] = $"{Games[i].Remove(0, 1).Replace("#", " ")}\n";
                        g++;
                    }else
                    {
                        if (g != 0)
                            GamesData[g - 1] += $"{Games[i]}\n";
                    }
                }
                string[][] vs = { CUSA, GamesData, Cheats };
                for (int i = 0; i < vs.Length; i++)
                    vs[i] = vs[i].Where(x => !string.IsNullOrEmpty(x)).ToArray();
            }
        }

        static int PID
        {
            get
            {
                ProcessList _PID;
                _PID = PS4.GetProcessList();
                int Result = 0;
                for (int i = 0; i < _PID.processes.Length; i++)
                {
                    if (_PID.processes[i].name == "eboot.bin")
                    {
                        Result = _PID.processes[i].pid;
                        break;
                    }
                }
                return Result;
            }
        }

        GameInfo gameInfo;

        CheatCodes cheats;

        enum inst
        {
            add,
            sub
        }
        ulong _a(string input) { return Convert.ToUInt64(input.Trim().Replace("0x", ""), 16); }
        public static byte[] STB(string hex)
        {
            if ((hex.Length % 2) != 0)
                hex = "0" + hex;
            int length = hex.Length;
            byte[] buffer = new byte[((length / 2) - 1) + 1];
            for (int i = 0; i < length; i += 2)
                buffer[i / 2] = Convert.ToByte(hex.Substring(i, 2), 0x10);
            return buffer;
        }

        void dumpResources()
        {
            byte[][] resources = { Resources.Be_Windows_Forms_HexBox, Resources.keystone, Resources.libdebug, Resources.RTFLib, Resources.SharpDisasm };
            string[] rSources = { "Be.Windows.Forms.HexBox.dll", "keystone.dll", "libdebug.dll", "RTFLib.dll", "SharpDisarm.dll" };
            for (int i = 0; i < rSources.Length; i++)
                if (!File.Exists($@"{rSources[i]}"))
                    File.WriteAllBytes($@"{rSources[i]}", resources[i]);
        }

        public Form1()
        {
            InitializeComponent();
            IPBox.Text = Settings.Default.IP;
            foreach (string item in registers) comboBox1.Items.Add(item);
            comboBox1.SelectedIndex = 0;
            this.Size = new Size(300, 120);
        }

        private void DebuggerInterruptCallback(uint lwpid, uint status, string tdname, reg64 regs, fpreg64 fpregs, dbreg64 dbregs)
        {
            _regs = regs;
            _fpregs = fpregs;
            _lwpid = lwpid;
            _status = status;
            _tdname = tdname;
            _dbregs = dbregs;
            if (AddressTextBox.InvokeRequired)
            {
                AddressTextBox.Invoke((EventHandler)delegate
                {
                    AddressTextBox.Text = "0x" + regs.r_rip.ToString("X");
                });
                _BP.Invoke((EventHandler)delegate
                {
                    if (_wp.Checked)
                    {
                        InstructionData data = new InstructionData(regs.r_rip);
                        _BP.Checked = false;
                        _wp.Checked = true;
                        Peek(data.bAddress);
                    }
                });
            }
            else
            {
                AddressTextBox.Text = "0x" + regs.r_rip.ToString("X");
                if (_wp.Checked)
                {
                    InstructionData data = new InstructionData(regs.r_rip);
                    _BP.Checked = false;
                    _wp.Checked = true;
                    Peek(data.bAddress);
                }
            }
        }

        void send_pay_load(string IP, string payloadPath, int port)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = 1500;
            socket.SendTimeout = 1500;
            socket.Connect(new IPEndPoint(IPAddress.Parse(IP), port));
            socket.SendFile(payloadPath);
            socket.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PS4 = new PS4DBG(IPBox.Text);
            try
            {
                PS4.Connect();
                atch.Enabled = PS4.IsConnected;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string GetDisassembly(ulong address, byte[] data)
        {
            StringBuilder stringBuilder = new StringBuilder();
            ArchitectureMode architecture = ArchitectureMode.x86_64;
            Disassembler.Translator.IncludeAddress = true;
            Disassembler.Translator.IncludeBinary = true;
            foreach (Instruction item in new Disassembler(data, architecture, address, copyBinaryToInstruction: true, Vendor.Any, 0uL).Disassemble())
            {
                stringBuilder.AppendLine(item.ToString());
            }
            return stringBuilder.ToString();
        }

        private string GetDisassembly(string address, byte[] data)
        {
            ulong _address = Convert.ToUInt64(address.Trim().Replace("0x", ""), 16);
            StringBuilder stringBuilder = new StringBuilder();
            ArchitectureMode architecture = ArchitectureMode.x86_64;
            Disassembler.Translator.IncludeAddress = true;
            Disassembler.Translator.IncludeBinary = true;
            foreach (Instruction item in new Disassembler(data, architecture, _address, copyBinaryToInstruction: true, Vendor.Any, 0uL).Disassemble())
            {
                stringBuilder.AppendLine($"0x{item.ToString()}");
            }
            return stringBuilder.ToString();
        }

        public static string SpliceText(string text, int lineLength)
        {
            return Regex.Replace(text, "(.{" + lineLength + "})", "$1" + Environment.NewLine);
        }

        public void Debugging(ulong _address)
        {
            offsetsText.Text = null;
            byte[] bytes;
            RTFBuilderbase builderbase = new RTFBuilder();
            builderbase.Font(RTFFont.CourierNew);
            builderbase.FontSize(22f);
            bytes = PS4.ReadMemory(PID, _address, 500);

            for (int i = 0; i < bytes.Length; i++)
            {
                builderbase.Font(RTFFont.CourierNew);
                builderbase.FontSize(22f);
                if (oldBytes != null)
                {
                    if (bytes.Length == oldBytes.Length)
                    {
                        if (bytes[i] == oldBytes[i])
                            builderbase.Append(bytes[i].ToString("X2") + " ");
                        else
                            builderbase.ForeColor(KnownColor.ForestGreen).Append(bytes[i].ToString("X2") + " ");
                    }
                    else
                        builderbase.Append(bytes[i].ToString("X2") + " ");
                }
                else
                    builderbase.Append(bytes[i].ToString("X2") + " ");
            }
            oldBytes = bytes;
            string str2 = builderbase.ToString();
            hexCode.Rtf = str2;
            string str3 = SpliceText(Encoding.Default.GetString(bytes).Replace("\0", ".").Replace("\a", ".").Replace("\v", ".").Replace("\r", ".").Replace(" ", ".").Replace("\t", ".").Replace("\n", ".").Replace("\b", ".").Replace("\f", "."), 0x10);
            asciiText.Text = str3;
            uint num3 = (uint)_address;
            int num4 = (hexCode.Text.Length / 0x30) + 1;
            for (int j = 0; j < num4; j++)
            {
                offsetsText.Text = offsetsText.Text + "0x" + _address.ToString("X").ToUpper() + Environment.NewLine;
                _address += 0x10;
            }
        }

        void Peek(ulong address, int length = 100)
        {
            Bytedata = PS4.ReadMemory(PID, address, length);
            DisassemblyTextBox.Text = GetDisassembly(address, Bytedata);
            Debugging(address);
        }

        void Peek(string address, int length = 100)
        {
            ulong _address = Convert.ToUInt64(dasAddress.Trim().Replace("0x", ""), 16);
            Bytedata = PS4.ReadMemory(PID, _address, length);
            DisassemblyTextBox.Text = GetDisassembly(address, Bytedata);
            Debugging(_address);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            switch (btn.Text)
            {
                case "Attach":
                    PS4.AttachDebugger(PID, DebuggerInterruptCallback);
                    if (PS4.IsConnected)
                        PS4.ProcessResume();
                    atch.Text = "Detach";
                    this.Size = new Size(1415, 505);
                    PS4.Notify(222, "Cain532 is watching your game");
                    gameInfo = new GameInfo(PS4);
                    this.Text += $"  ||  ◄{gameInfo.titleID} v{gameInfo.version}  '{gameInfo.username}'  ►";
                    break;
                case "Detach":
                    PS4.DetachDebugger();
                    this.Size = new Size(300, 120);
                    PS4.Notify(222, "Your game is hidden from Cain532");
                    atch.Text = "Attach";
                    this.Text = "PS4 Debugger - Cain532";
                    break;
            }
        }
        
        private void PlusMinus_Clicky(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            InstructionData data = new InstructionData(address);
            if (!DisassemblyTextBox.Visible)
                dasAddress = (btn.Text == "+") ? (address + _a(jumpbox.Text)).ToString("X") : (address - _a(jumpbox.Text)).ToString("X");
            else
                dasAddress = (btn.Text == "+") ? data.nAddress.ToString("X") : data.bAddress.ToString("X");
            Peek(dasAddress);
            AddressTextBox.Text = dasAddress;
            offsetTxt.Text = dasAddress;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            dataGridView1.Enabled = true;
            dataGridView1.RowCount = 10;
            for (int i = 0; i < 10; i++) dataGridView1.Rows[i].Cells[0].Value = registers[i];

            if (PS4 != null)
            {
                if (aFresh.Checked)
                    if (AddressTextBox.Text != "")
                        Peek(address);
                if (PS4.IsDebugging)
                {
                    if (_regs != null)
                    {
                        _regValue = new ulong[] { _regs.r_rax, _regs.r_rbx, _regs.r_r8, _regs.r_r9, _regs.r_r10, _regs.r_r11, _regs.r_r12, _regs.r_r13, _regs.r_r14, _regs.r_r15 };
                        for (int i = 0; i < 10; i++) dataGridView1.Rows[i].Cells[1].Value = _regValue[i].ToString("X");
                        if (eView.Checked)
                        {
                            data = $"0x{_regValue[comboBox1.SelectedIndex].ToString("X")}{Environment.NewLine}";
                            if (sBox.Lines.Length <= 0) sBox.Text += data;
                            if (!sBox.Text.Contains(data)) sBox.Text += data;
                        }
                    }
                    if (checkBox2.Checked)
                        PS4.ProcessResume();
                }
            }
        }

        string Line
        {
            get
            {
                try
                {
                    if (!rBox.Text.Contains(" "))
                    {
                        int f = rBox.GetFirstCharIndexOfCurrentLine();
                        int c = rBox.GetLineFromCharIndex(f);
                        return rBox.Lines[c].Replace("\r\n", "");
                    }
                    else
                        return null;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        private void hexCode_Click(object sender, EventArgs e)
        {
            int index = hexCode.SelectionStart / 0x30;
            int num2 = (hexCode.SelectionStart - (index * 0x30)) / 3;
            string str = offsetsText.Lines.ElementAt<string>(index);
            string str2 = num2.ToString("X");
            str = str.Remove(str.Length - 1, 1) + str2;
            offsetTxt.Text = str;
        }

        private void hexCode_DoubleClick(object sender, EventArgs e)
        {
            _BP.Checked = true;
        }
        
        private void WriteMem()
        {
            writebox.Text.Replace(" ", "");
            write = STB(writebox.Text);
            PS4.WriteMemory(PID, address, write);
            Debugging(address);
        }

        private void Process_Clicky(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            switch (btn.Text)
            {
                case "▶": PS4.ProcessResume(); break;
                case "■": checkBox2.Checked = false; PS4.ProcessKill(); break;
                case "❚❚": PS4.ProcessStop(); break;
            }
        }

        private void dasAddress_TextChanged(object sender, EventArgs e)
        {
            TextBox T = sender as TextBox;
            dasAddress = T.Text;
            if (!_BP.Checked)
                AddressTextBox.Text = dasAddress;
            offsetTxt.Text = dasAddress;
        }

        private void _BP_CheckedChanged(object sender, EventArgs e)
        {
            if (address != 0)
            {
                aFresh.Checked = !_BP.Checked;
                AddressTextBox.Enabled = !_BP.Checked;
                dasAddress = AddressTextBox.Text;
                if (__bp.Checked)
                    PS4.ChangeBreakpoint(0, Convert.ToInt16(_BP.Checked), _BP.Checked ? address : 0ul);
                else if (_wp.Checked)
                {
                    var _l = Enum.GetValues(typeof(PS4DBG.WATCHPT_LENGTH)).Cast<PS4DBG.WATCHPT_LENGTH>().ToArray();
                    var _bt = Enum.GetValues(typeof(PS4DBG.WATCHPT_BREAKTYPE)).Cast<PS4DBG.WATCHPT_BREAKTYPE>().ToArray();
                    PS4.ChangeWatchpoint(0, 1, _l[WPLength.SelectedIndex], _bt[WPType.SelectedIndex], _BP.Checked ? address : 0ul);
                }
                if (!_BP.Checked)
                {
                    for (int i = 0; i < _regValue.Length; i++) _regValue[i] = 0ul;
                    eView.Checked = false;
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (PS4 != null)
                if (PS4.IsConnected)
                {
                    if (PS4.IsDebugging)
                        PS4.DetachDebugger();
                    PS4.Notify(222, "Your game is hidden from Cain532");
                }
        }

        private void sBox_DoubleClick(object sender, EventArgs e)
        {
            rBox = sender as TextBox;
            dasAddress = Line;
            AddressTextBox.Text = dasAddress;
            offsetTxt.Text = dasAddress;
            _BP.Checked = false;
            _wp.Checked = true;
            mView.Checked = true;
            Peek(address);
        }

        private void sBox_Click(object sender, EventArgs e)
        {
            rBox = sender as TextBox;
            //if (!checkBox2.Checked)
            //{
            try
            {
                mView.Checked = true;
                ulong _address = Convert.ToUInt64(Line.Trim().Replace("0x", ""), 16);
                Debugging(_address);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
                //}
        }

        void Peeper(object sender, EventArgs e)
        {
            CheckBox btn = sender as CheckBox;
            switch  (btn.Name)
            {
                case "dView":
                    DisassemblyTextBox.Visible = false;
                    DebugLayout.Visible = true;
                    mView.Checked = !dView.Checked;
                    jumpbox.Enabled = false;
                    break;
                case "mView":
                    DisassemblyTextBox.Visible = true;
                    DebugLayout.Visible = false;
                    dView.Checked = !mView.Checked;
                    jumpbox.Enabled = true;
                    break;
            }
        }

        void CodeCave(string[] input)
        {
            InstructionData data = new InstructionData(_a(stBox.Text));
            InstructionData pData = new InstructionData(data.nAddress);
            string[] GetLines = new string[input.Length + 1];
            bool l = true;
            string
                initialJmp = $"jmp 0x{cBox.Text.Replace("0x", "")}",
                over = null;
            if (data.Length >= 5) //adds nop if instruction is greater than 5 byte length
                for (int i = 0; i < (data.Length - 5); i++)
                    initialJmp += $"\nnop";
            else if (data.Length <= 5) //adds nop to next insrtuction, adds next instruction to bottom of cave
            {
                l = false;
                if (_li.Checked)
                    over = pData.Operation;
                for (int i = 0; i < ((data.Length + pData.Length) - 5); i++)
                    initialJmp += $"\nnop";
            }
            for (int i = 0; i < input.Length; i++)
                GetLines[i] = input[i];
            if (over != null)
                GetLines[GetLines.Length - 1] = l ? $"{over}\njmp 0x{ data.nAddress.ToString("X").Replace("0x", "")}" : $"{over}\njmp 0x{pData.nAddress.ToString("X").Replace("0x", "")}";
            else
                GetLines[GetLines.Length - 1] = l ? $"jmp 0x{data.nAddress.ToString("X").Replace("0x", "")}" : $"jmp 0x{pData.nAddress.ToString("X").Replace("0x", "")}";
            Keystone key = new Keystone(Keystone.ks_arch.KS_ARCH_X86, Keystone.ks_mode.KS_MODE_64);
            PS4.WriteMemory(PID, _a(stBox.Text), key.Assemble(initialJmp, _a(stBox.Text)));
            PS4.WriteMemory(PID, _a(cBox.Text), key.Assemble(string.Join(" \n", GetLines), _a(cBox.Text)));
        }

        private void _peek_Click(object sender, EventArgs e)
        {
            Peek(dasAddress);
        }

        private void writebyte_Click(object sender, EventArgs e)
        {
            WriteMem();
        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            CodeCave(_asmCaveBox.Lines);
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void RGLogo_Click(object sender, EventArgs e) =>
            System.Diagnostics.Process.Start("https://www.RivalGamer.com");

        private void Cain532_Click(object sender, EventArgs e) =>
            System.Diagnostics.Process.Start("http://www.rivalgamer.com/rf/?c=L9TEMNSY");

        private void _asm_Click(object sender, EventArgs e)
        {
            Keystone key = new Keystone(Keystone.ks_arch.KS_ARCH_X86, Keystone.ks_mode.KS_MODE_64);
            Console.WriteLine(_asmBox.Text);
            PS4.WriteMemory(PID, address, key.Assemble(_asmBox.Text, address: address));
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            button1.Enabled = !checkBox2.Checked;
            button2.Enabled = !checkBox2.Checked;
        }

        private void IPBox_TextChanged(object sender, EventArgs e)
        {
            Settings.Default.IP = IPBox.Text;
            Settings.Default.Save();
            if (IPBox.Text != "")
                cnct.Enabled = true;
        }
    }
}