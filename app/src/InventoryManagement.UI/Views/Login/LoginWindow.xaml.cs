using System.Windows;
using InventoryManagement.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.UI.Views.Login;

public partial class LoginWindow : Window
{
    private readonly IAuthenticationService _authService;

    public LoginWindow(IAuthenticationService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        txtError.Visibility = Visibility.Collapsed;
        btnLogin.IsEnabled = false;

        var username = txtUsername.Text;
        var password = txtPassword.Password;

        var user = await _authService.AuthenticateAsync(username, password);
        
        if (user != null)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            txtError.Text = "Invalid username or password.";
            txtError.Visibility = Visibility.Visible;
            btnLogin.IsEnabled = true;
        }
    }
}
