using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IdleMasterExtended
{
    internal sealed class GameTestForm : Form
    {
        private readonly TextBox search = new TextBox { Dock = DockStyle.Fill };
        private readonly ListBox results = new ListBox { Dock = DockStyle.Fill };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
        private readonly Button start = new Button { AutoSize = true, Enabled = false };
        private readonly Button stop = new Button { AutoSize = true, Enabled = false };
        private readonly Timer monitor = new Timer { Interval = 100 };
        private readonly Stopwatch elapsed = new Stopwatch();
        private List<Badge> games = new List<Badge>();
        private Badge active;

        private static string T(string key) { return localization.strings.ResourceManager.GetString(key); }

        internal GameTestForm(Action pauseQueue, Action<Badge, int?, string> recordFailure)
        {
            Text = T("test_game");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 380);
            MinimumSize = new Size(400, 320);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = T("test_search"), AutoSize = true });
            layout.Controls.Add(search);
            layout.Controls.Add(results);
            layout.Controls.Add(status);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            start.Text = T("test_game");
            stop.Text = T("test_stop");
            actions.Controls.AddRange(new Control[] { start, stop });
            layout.Controls.Add(actions);
            Controls.Add(layout);
            status.Text = T("test_scanning");
            search.TextChanged += (s, e) => Filter();
            results.SelectedIndexChanged += (s, e) => start.Enabled = active == null && results.SelectedItem != null;
            Shown += async (s, e) =>
            {
                try { games = await Task.Run((Func<List<Badge>>)InstalledGames.Scan); }
                catch (Exception ex)
                {
                    if (!IsDisposed) status.Text = T("test_scan_failed");
                    Logger.Exception(ex, "Installed game scan failed");
                    return;
                }
                if (IsDisposed) return;
                Filter();
                search.Focus();
            };
            start.Click += (s, e) =>
            {
                var badge = results.SelectedItem as Badge;
                if (badge == null || active != null) return;
                pauseQueue();
                active = badge;
                search.Enabled = results.Enabled = start.Enabled = false;
                stop.Enabled = true;
                status.Text = badge.Name + " — " + T("helper_starting");
                elapsed.Restart();
                monitor.Start();
                badge.Idle(this, (code, detail) =>
                {
                    if (IsDisposed || active != badge) return;
                    recordFailure(badge, code, detail);
                    Finish(badge.Name + " — " + frmMain.HelperFailureReason(code, detail));
                });
            };
            monitor.Tick += (s, e) =>
            {
                if (active == null || !active.SteamReady) return;
                monitor.Stop();
                status.Text = string.Format(T("test_confirmed"), active.Name, active.AppId, elapsed.Elapsed.TotalSeconds);
            };
            stop.Click += (s, e) => StopTest();
        }

        private void Filter()
        {
            results.BeginUpdate();
            try
            {
                results.Items.Clear();
                string query = search.Text.Trim();
                results.Items.AddRange(games.Where(b => b.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    b.StringId == query).ToArray());
                if (results.Items.Count > 0) results.SelectedIndex = 0;
            }
            finally { results.EndUpdate(); }
            status.Text = string.Format(T("test_matches"), results.Items.Count);
        }

        private void Finish(string message)
        {
            active = null;
            monitor.Stop();
            stop.Enabled = false;
            search.Enabled = results.Enabled = true;
            start.Enabled = results.SelectedItem != null;
            status.Text = message;
        }

        private void StopTest()
        {
            if (active == null) return;
            var badge = active;
            active = null;
            badge.StopIdle();
            Finish(badge.Name + " — " + T("test_stopped"));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopTest();
            monitor.Dispose();
            base.OnFormClosed(e);
        }
    }
}
