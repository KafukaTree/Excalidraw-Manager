using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

internal static class FormulaCaptureStub
{
    private static int Main()
    {
        string mode = Environment.GetEnvironmentVariable("FORMULA_CAPTURE_STUB_MODE") ?? "cancel";
        if (string.Equals(mode, "wait", StringComparison.Ordinal))
        {
            Thread.Sleep(1000);
            Console.Out.Write("{\"cancelled\":true}");
            return 0;
        }
        if (string.Equals(mode, "too-large", StringComparison.Ordinal))
        {
            Console.Out.Write("{\"cancelled\":true,\"error\":\"CAPTURE_SELECTION_TOO_LARGE\"}");
            return 2;
        }
        if (!string.Equals(mode, "success", StringComparison.Ordinal))
        {
            Console.Out.Write("{\"cancelled\":true}");
            return 0;
        }

        using (Bitmap bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb))
        using (MemoryStream output = new MemoryStream())
        {
            bitmap.SetPixel(0, 0, Color.White);
            bitmap.SetPixel(1, 0, Color.Black);
            bitmap.SetPixel(0, 1, Color.Black);
            bitmap.SetPixel(1, 1, Color.White);
            bitmap.Save(output, ImageFormat.Png);
            Console.Out.Write(
                "{\"cancelled\":false,\"mimeType\":\"image/png\",\"width\":2,\"height\":2,\"image\":\"" +
                Convert.ToBase64String(output.GetBuffer(), 0, checked((int)output.Length)) + "\"}");
        }
        return 0;
    }
}
