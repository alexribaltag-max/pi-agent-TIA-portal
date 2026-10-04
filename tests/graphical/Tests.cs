using System;
using System.IO;
using System.Xml;
using TiaLocalBridge.Services;

internal static class Tests
{
    private static int count;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
    private static void Main()
    {
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../TiaLocalBridge/exports-kinds/LAD_Main.xml"));
        var result = FlgNetReader.Read(fixture, "en-US");
        Check(result.NetworkCount > 0, "compile units found");
        Console.WriteLine("Parsed LAD fallback:\n" + result.Text);
        Console.WriteLine("Graphical production fixture checks: " + count + " passed");
    }
}