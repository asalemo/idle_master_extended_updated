using System;
using System.Windows.Forms;

namespace steam_idle
{
    public partial class FormSteamIdle : Form
    {
        public FormSteamIdle(long appid)
        {
            InitializeComponent();
            picApp.SizeMode = PictureBoxSizeMode.Zoom;
            picApp.ErrorImage = null;
            picApp.ImageLocation = $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appid}/header.jpg";
        }

        private void FormSteamIdle_Load(object sender, EventArgs e)
        {

        }
    }
}
