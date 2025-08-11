using System;
using System.Threading.Tasks;
using System.Web.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace YourApp.Web
{
    public partial class Sample : Page
    {
        private readonly ILogger<Sample> _logger;
        private readonly ILoginBusiness _loginBusinessBusiness;
        private readonly IUserBusiness _userBusinessBusiness;

        public Sample()
        {
            // In a real application, use dependency injection container
            // This is just a demonstration
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                StatusLabel.Text = "Page loaded using migrated architecture";
            }
        }

        protected async void LoadDataButton_Click(object sender, EventArgs e)
        {
            try
            {
                StatusLabel.Text = "Loading data using business logic layer...";
                
                // TODO: Use injected business logic services
                // var data = await _userBusiness.GetAllUsersAsync();
                // GridView1.DataSource = data;
                // GridView1.DataBind();
                
                StatusLabel.Text = "Data loaded successfully!";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                _logger?.LogError(ex, "Error loading data");
            }
        }
    }
}
