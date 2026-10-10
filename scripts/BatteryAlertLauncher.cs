using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("Battery Alert")]
[assembly: AssemblyDescription("Pornește aplicația Battery Alert din pachetul local")]
[assembly: AssemblyCompany("EGOMASTER91")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

internal static class BatteryAlertLauncher
{
    [STAThread]
    private static int Main(string[] args)
    {
        string applicationFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dist", "BatteryAlert");
        string executable = Path.Combine(applicationFolder, "BatteryAlert.exe");
        try
        {
            if (!File.Exists(executable))
            {
                MessageBox.Show("Fișierele aplicației lipsesc. Păstrează folderul dist lângă acest fișier.",
                    "Battery Alert", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }

            var arguments = new StringBuilder();
            foreach (string argument in args)
            {
                if (arguments.Length > 0) arguments.Append(' ');
                arguments.Append(QuoteArgument(argument));
            }
            using (Process process = Process.Start(new ProcessStartInfo(executable)
            {
                WorkingDirectory = applicationFolder,
                Arguments = arguments.ToString(),
                UseShellExecute = true
            })) { }
            return 0;
        }
        catch (Exception)
        {
            MessageBox.Show("Battery Alert nu a putut porni. Verifică dacă pachetul aplicației este extras integral.",
                "Battery Alert", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    // Preserve spaces, quotes and trailing backslashes when forwarding Windows arguments.
    private static string QuoteArgument(string argument)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in argument)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        result.Append('\\', slashes * 2);
        result.Append('"');
        return result.ToString();
    }
}

