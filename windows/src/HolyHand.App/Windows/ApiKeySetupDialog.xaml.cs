using System.Windows;
using System.Windows.Input;
using HolyHand.Core.Interfaces;

namespace HolyHand.App.Windows;

public partial class ApiKeySetupDialog : Window
{
    private readonly ICredentialStore _credentialStore;

    public ApiKeySetupDialog(ICredentialStore credentialStore)
    {
        InitializeComponent();
        _credentialStore = credentialStore;

        var existingKey = _credentialStore.GetApiKey();
        if (!string.IsNullOrWhiteSpace(existingKey))
        {
            ApiKeyBox.Password = existingKey;
        }

        Loaded += (s, e) => ApiKeyBox.Focus();
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ApiKeyBox.Password.Length > 0)
        {
            TxtStatus.Text = "Key entered. Press Save & Connect to continue.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveAndClose();
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveAndClose()
    {
        var key = ApiKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            TxtStatus.Text = "⚠️ Please enter a valid API key before saving.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
            ApiKeyBox.Focus();
            return;
        }

        try
        {
            _credentialStore.SetApiKey(key);
            Environment.SetEnvironmentVariable("AI_GATEWAY_API_KEY", key);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "⚠️ Failed to save key: " + ex.Message;
            TxtStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SaveAndClose();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }
    }
}
