using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using IdleMasterExtended.Properties;

namespace IdleMasterExtended
{
    public partial class frmSettingsAdvanced : Form
    {
        public frmSettingsAdvanced()
        {
            InitializeComponent();
        }

        private void btnView_Click(object sender, EventArgs e)
        {
            txtSessionID.UseSystemPasswordChar = txtSteamLoginSecure.UseSystemPasswordChar = txtSteamParental.UseSystemPasswordChar = false;
            txtSessionID.PasswordChar = '\0';
            txtSteamLoginSecure.PasswordChar = '\0';
            txtSteamParental.PasswordChar = '\0';

            txtSessionID.Enabled = true;
            txtSteamLoginSecure.Enabled = true;
            txtSteamParental.Enabled = true;

            btnView.Visible = false;
        }

        private void frmSettingsAdvanced_Load(object sender, EventArgs e)
        {
            // Localize Form
            btnUpdate.Text = localization.strings.update;
            this.Text = localization.strings.auth_data;
            linkLabelWhatIsThis.Text = localization.strings.ResourceManager.GetString("manual_fields_help");
            linkLabelWhatIsThis.Left = ClientSize.Width - linkLabelWhatIsThis.PreferredWidth - 12;
            ttHelp.SetToolTip(btnView, localization.strings.cookie_warning);

            // Read settings
            var customTheme = Settings.Default.customTheme;
            var whiteIcons = Settings.Default.whiteIcons;

            // Define colors
            this.BackColor = customTheme ? Settings.Default.colorBgd : Settings.Default.colorBgdOriginal;
            this.ForeColor = customTheme ? Settings.Default.colorTxt : Settings.Default.colorTxtOriginal;

            // Buttons
            FlatStyle buttonStyle = customTheme ? FlatStyle.Flat : FlatStyle.Standard;
            btnView.FlatStyle = btnUpdate.FlatStyle = buttonStyle;
            btnView.BackColor = btnUpdate.BackColor = this.BackColor;
            btnView.ForeColor = btnUpdate.ForeColor = this.ForeColor;
            btnView.Image = customTheme ? Resources.imgView_w : Resources.imgView;

            // Links
            linkLabelWhatIsThis.LinkColor = customTheme ? Color.GhostWhite : Color.Blue;

            txtSessionID.UseSystemPasswordChar = txtSteamLoginSecure.UseSystemPasswordChar = txtSteamParental.UseSystemPasswordChar = true;
            if (!string.IsNullOrWhiteSpace(ManualSession.Current?.SessionId))
            {
                txtSessionID.Text = ManualSession.Current?.SessionId;
                txtSessionID.Enabled = false;
            }
            else
            {
                txtSessionID.UseSystemPasswordChar = true;
            }

            if (!string.IsNullOrWhiteSpace(ManualSession.Current?.Login))
            {
                txtSteamLoginSecure.Text = ManualSession.Current?.Login;
                txtSteamLoginSecure.Enabled = false;
            }
            else
            {
                txtSteamLoginSecure.UseSystemPasswordChar = true;
            }

            if (!string.IsNullOrWhiteSpace(ManualSession.Current?.Parental))
            {
                txtSteamParental.Text = ManualSession.Current?.Parental;
                txtSteamParental.Enabled = false;
            }
            else
            {
                txtSteamParental.UseSystemPasswordChar = true;
                txtSteamParental.Clear();
            }

            if (txtSessionID.Enabled && txtSteamLoginSecure.Enabled && txtSteamParental.Enabled)
            {
                btnView.Visible = false;
            }

            btnUpdate.Enabled = false;
        }

        private void txtSessionID_TextChanged(object sender, EventArgs e)
        {
            btnUpdate.Enabled = true;
        }

        private void txtSteamLogin_TextChanged(object sender, EventArgs e)
        {
            btnUpdate.Enabled = true;
        }

        private void txtSteamParental_TextChanged(object sender, EventArgs e)
        {
            btnUpdate.Enabled = true;
        }

        private async Task ValidateSessionAsync()
        {
            var status = SessionStatus.Unavailable;
            try
            {
                var candidate = new ManualSession(txtSessionID.Text.Trim(), txtSteamLoginSecure.Text.Trim(), txtSteamParental.Text.Trim());
                status = await CookieClient.CheckManualSessionAsync(candidate);
                if (IsDisposed || Disposing || !Visible) return;
                if (status == SessionStatus.Connected)
                {
                    ManualSession.Current = candidate;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Could not validate Steam session.");
            }
            if (IsDisposed || Disposing || !Visible) return;
            txtSessionID.Enabled = txtSteamLoginSecure.Enabled = txtSteamParental.Enabled = true;
            btnUpdate.Text = localization.strings.update;
            btnUpdate.Enabled = true;
            ttHelp.SetToolTip(btnUpdate, localization.strings.ResourceManager.GetString("diagnostics_title"));
            Logger.Report(status == SessionStatus.Expired ? localization.strings.validate_failed :
                localization.strings.ResourceManager.GetString("session_try_again"));
        }

        private async void btnUpdate_Click(object sender, EventArgs e)
        {
            btnUpdate.Enabled = false;
            txtSessionID.Enabled = false;
            txtSteamLoginSecure.Enabled = false;
            txtSteamParental.Enabled = false;

            btnUpdate.Text = localization.strings.validating;

            await ValidateSessionAsync();
        }

        private void linkLabelWhatIsThis_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/asalemo/idle_master_extended_updated/wiki/Getting-started");
        }
    }
}
