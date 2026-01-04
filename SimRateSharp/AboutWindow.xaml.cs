/* SimRateSharp is a simple overlay application for MSFS to display
 * simulation rate and reset sim-rate via joystick button as well as displaying other vital data.
 *
 * Copyright (C) 2025 Grant DeFayette / CavebatSoftware LLC 
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 of the License.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace SimRateSharp;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // Set localized text
        TitleText.Text = SimRateSharp.Resources.Strings.About_Title;
        SubtitleText.Text = SimRateSharp.Resources.Strings.About_Subtitle;
        DescriptionText.Text = SimRateSharp.Resources.Strings.About_Description;
        GitHubLinkText.Text = SimRateSharp.Resources.Strings.About_ViewOnGitHub;
        CloseButtonControl.Content = SimRateSharp.Resources.Strings.About_Close;

        // Get version from assembly
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
        }
    }

    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Failed to open GitHub link: {ex.Message}");
            MessageBox.Show(
                string.Format(SimRateSharp.Resources.Strings.Error_BrowserOpen, ex.Message),
                SimRateSharp.Resources.Strings.Error_Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
