using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using ChattModels;

namespace Client
{
    public partial class Form1 : Form
    {
        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private readonly HashSet<Guid> _sentMessageIds = new HashSet<Guid>();
        private readonly object _sentLock = new object();

        // UI controls - initialize with the null-forgiving operator because they are set in BuildUi()
        private TextBox txtServer = null!;
        private TextBox txtPort = null!;
        private TextBox txtName = null!;
        private TextBox txtToken = null!;
        private TextBox txtMessage = null!;
        private Button btnConnect = null!;
        private Button btnSend = null!;
        private TextBox lstChat = null!;

        public Form1()
        {
            InitializeComponent();
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Chattklient";
            Width = 800;
            Height = 600;
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterScreen;
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(6), AutoSize = false, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            var lblServer = new Label { Text = "Server:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(6, 8, 0, 0) };
            txtServer = new TextBox { Width = 140, Text = "127.0.0.1", Margin = new Padding(6, 6, 0, 0) };
            var lblPort = new Label { Text = "Port:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(6, 8, 0, 0) };
            txtPort = new TextBox { Width = 60, Text = "5000", Margin = new Padding(6, 6, 0, 0) };
            var lblName = new Label { Text = "Name:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(6, 8, 0, 0) };
            txtName = new TextBox { Width = 140, Text = Environment.UserName, Margin = new Padding(6, 6, 0, 0) };
            var lblToken = new Label { Text = "Token:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(6, 8, 0, 0) };
            txtToken = new TextBox { Width = 180, Text = string.Empty, Margin = new Padding(6, 6, 0, 0) };
            btnConnect = new Button { Width = 140, Height = 36, Text = "Connect", Margin = new Padding(12, 8, 0, 0) };
            btnConnect.Click += BtnConnect_Click;

            topFlow.Controls.Add(lblServer);
            topFlow.Controls.Add(txtServer);
            topFlow.Controls.Add(lblPort);
            topFlow.Controls.Add(txtPort);
            topFlow.Controls.Add(lblName);
            topFlow.Controls.Add(txtName);
            topFlow.Controls.Add(lblToken);
            topFlow.Controls.Add(txtToken);
            topFlow.Controls.Add(btnConnect);

            lstChat = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true };

            var bottomPanel = new Panel { Dock = DockStyle.Fill, Height = 100, Padding = new Padding(6) };
            txtMessage = new TextBox { Dock = DockStyle.Fill, Multiline = true, Margin = new Padding(0, 0, 6, 0) };
            btnSend = new Button { Width = 110, Text = "Send", Dock = DockStyle.Right, Margin = new Padding(6, 0, 0, 0) };
            btnSend.Click += BtnSend_Click;
            btnSend.Enabled = false;

            // Enter sends message; Shift+Enter inserts newline
            txtMessage.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    // If Shift is held, allow newline insertion
                    if ((e.Modifiers & Keys.Shift) == Keys.Shift)
                    {
                        return;
                    }
                    // Otherwise send the message
                    e.SuppressKeyPress = true;
                    BtnSend_Click(btnSend, EventArgs.Empty);
                }
            };

            bottomPanel.Controls.Add(txtMessage);
            bottomPanel.Controls.Add(btnSend);

            var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, topFlow.Height));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, bottomPanel.Height));
            tlp.Controls.Add(topFlow, 0, 0);
            tlp.Controls.Add(lstChat, 0, 1);
            tlp.Controls.Add(bottomPanel, 0, 2);

            Controls.Add(tlp);
        }

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            if (_tcp != null) return;

            try
            {
                var host = txtServer.Text.Trim();
                var port = int.Parse(txtPort.Text.Trim());
                _tcp = new TcpClient();
                await _tcp.ConnectAsync(host, port);
                _stream = _tcp.GetStream();
                _reader = new StreamReader(_stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
                _writer = new StreamWriter(_stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true) { AutoFlush = true };

                btnConnect.Enabled = false;
                btnSend.Enabled = true;

                _ = Task.Run(ReceiveLoopAsync);
                lstChat.AppendText("Connected to server" + Environment.NewLine);
                lstChat.SelectionStart = lstChat.TextLength;
                lstChat.ScrollToCaret();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect: {ex.Message}");
                CleanupConnection();
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (_reader != null)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;
                    var msg = MessageBase.FromJson(line);
                    if (msg != null)
                    {
                        // if this client sent the message, server will echo it back — skip duplicates
                        var id = msg.MessageId;
                        var skip = false;
                        if (id != Guid.Empty)
                        {
                            lock (_sentLock)
                            {
                                if (_sentMessageIds.Contains(id))
                                {
                                    _sentMessageIds.Remove(id);
                                    skip = true;
                                }
                            }
                        }
                        if (skip) continue;

                        BeginInvoke(() =>
                        {
                        switch (msg)
                        {
                            case TextMessage tm:
                                lstChat.AppendText($"{tm.Timestamp.ToLocalTime():HH:mm} {tm.Sender}: {tm.Content}" + Environment.NewLine);
                                break;
                            case PrivateMessage pm:
                                lstChat.AppendText($"{pm.Timestamp.ToLocalTime():HH:mm} {pm.Sender} -> {pm.Recipient}: {pm.Content}" + Environment.NewLine);
                                break;
                            case SystemMessage sm:
                                lstChat.AppendText($"{sm.Timestamp.ToLocalTime():HH:mm} [SYSTEM] {sm.Action}" + Environment.NewLine);
                                break;
                            default:
                                lstChat.AppendText(msg.ToString() + Environment.NewLine);
                                break;
                        }
                        lstChat.SelectionStart = lstChat.TextLength;
                        lstChat.ScrollToCaret();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(() =>
                {
                    lstChat.AppendText($"Receive error: {ex.Message}" + Environment.NewLine);
                    lstChat.SelectionStart = lstChat.TextLength;
                    lstChat.ScrollToCaret();
                });
            }
            finally
            {
                BeginInvoke(() =>
                {
                    lstChat.AppendText("Disconnected from server" + Environment.NewLine);
                    lstChat.SelectionStart = lstChat.TextLength;
                    lstChat.ScrollToCaret();
                });
                CleanupConnection();
            }
        }

        private async void BtnSend_Click(object? sender, EventArgs e)
        {
            if (_writer == null) return;
            var name = txtName.Text.Trim();
            var text = txtMessage.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(text)) return;

            var msg = new TextMessage(name, text);
            var payload = msg.ToJson();
            // remember id so incoming echo can be ignored
            lock (_sentLock)
            {
                _sentMessageIds.Add(msg.MessageId);
            }
            try
            {
                await _writer.WriteLineAsync(payload);
                // local echo
                lstChat.AppendText($"{DateTime.Now:HH:mm} {name}: {text}" + Environment.NewLine);
                lstChat.SelectionStart = lstChat.TextLength;
                lstChat.ScrollToCaret();
                txtMessage.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Send failed: {ex.Message}");
            }
        }

        private void CleanupConnection()
        {
            try { _writer?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }
            _writer = null;
            _reader = null;
            _stream = null;
            _tcp = null;
            BeginInvoke(() =>
            {
                btnConnect.Enabled = true;
                btnSend.Enabled = false;
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            CleanupConnection();
        }
    }
} // End of Form1.cs