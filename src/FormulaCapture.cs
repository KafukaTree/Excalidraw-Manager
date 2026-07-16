using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Excalidraw Manager Formula Capture")]
[assembly: System.Reflection.AssemblyProduct("Excalidraw Manager")]
[assembly: System.Reflection.AssemblyVersion("0.4.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.4.0.0")]

namespace ExcalidrawManager
{
    internal static class FormulaCaptureProgram
    {
        private const int MinimumSelectionSize = 2;
        private const int MaximumBitmapDimension = 32767;
        private const long MaximumDesktopPixels = 64000000L;
        private const long MaximumSelectionPixels = 40000000L;
        private const long MaximumPngBytes = 12L * 1024L * 1024L;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && string.Equals(args[0], "--describe", StringComparison.Ordinal))
            {
                Console.Out.Write("{\"name\":\"Excalidraw Manager Formula Capture\",\"version\":1,\"mimeType\":\"image/png\",\"inMemory\":true,\"maximumDesktopPixels\":64000000,\"maximumSelectionPixels\":40000000,\"maximumPngBytes\":12582912}");
                return 0;
            }

            try
            {
                bool useChinese;
                int parentPid = ParseOptions(args, out useChinese);
                EnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Rectangle virtualScreen = SystemInformation.VirtualScreen;
                if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
                    throw new InvalidOperationException("Windows did not report a usable virtual screen.");
                ValidateDesktopSize(virtualScreen);

                // Capture first. The selection UI is created only after the original pixels are in memory,
                // so neither the overlay nor a temporary screenshot file can appear in the result.
                using (Bitmap desktop = new Bitmap(virtualScreen.Width, virtualScreen.Height, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(desktop))
                    {
                        graphics.CopyFromScreen(
                            virtualScreen.Left,
                            virtualScreen.Top,
                            0,
                            0,
                            virtualScreen.Size,
                            CopyPixelOperation.SourceCopy);
                    }

                    Rectangle selection;
                    using (SelectionForm form = new SelectionForm(desktop, virtualScreen, parentPid, useChinese))
                    {
                        Application.Run(form);
                        if (form.Cancelled || form.Selection.Width < MinimumSelectionSize || form.Selection.Height < MinimumSelectionSize)
                        {
                            Console.Out.Write("{\"cancelled\":true}");
                            return 0;
                        }
                        selection = form.Selection;
                    }
                    ValidateSelectionSize(selection, desktop.Size);

                    using (Bitmap cropped = desktop.Clone(selection, PixelFormat.Format32bppPArgb))
                    using (MemoryStream output = new MemoryStream())
                    {
                        cropped.Save(output, ImageFormat.Png);
                        if (output.Length > MaximumPngBytes)
                            throw new CaptureLimitException("CAPTURE_OUTPUT_TOO_LARGE", "The selected PNG exceeds the OCR byte limit.");
                        string image = Convert.ToBase64String(output.GetBuffer(), 0, checked((int)output.Length));
                        Console.Out.Write(
                            "{\"cancelled\":false,\"mimeType\":\"image/png\",\"width\":" +
                            selection.Width + ",\"height\":" + selection.Height +
                            ",\"image\":\"" + image + "\"}");
                    }
                }
                return 0;
            }
            catch (CaptureLimitException error)
            {
                Console.Error.WriteLine("Formula screen capture rejected: " + error.Message);
                Console.Out.Write("{\"cancelled\":true,\"error\":\"" + error.Code + "\"}");
                return 2;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Formula screen capture failed: " + error.Message);
                Console.Out.Write("{\"cancelled\":true,\"error\":\"CAPTURE_FAILED\"}");
                return 1;
            }
        }

        private static void ValidateDesktopSize(Rectangle bounds)
        {
            long pixels = checked((long)bounds.Width * bounds.Height);
            if (bounds.Width > MaximumBitmapDimension || bounds.Height > MaximumBitmapDimension || pixels > MaximumDesktopPixels)
                throw new CaptureLimitException("CAPTURE_DESKTOP_TOO_LARGE", "The virtual desktop is too large to capture safely.");
        }

        private static void ValidateSelectionSize(Rectangle selection, Size desktopSize)
        {
            if (selection.X < 0 || selection.Y < 0 || selection.Right > desktopSize.Width || selection.Bottom > desktopSize.Height)
                throw new CaptureLimitException("CAPTURE_SELECTION_INVALID", "The selected rectangle is outside the captured desktop.");
            long pixels = checked((long)selection.Width * selection.Height);
            if (selection.Width > MaximumBitmapDimension || selection.Height > MaximumBitmapDimension || pixels > MaximumSelectionPixels)
                throw new CaptureLimitException("CAPTURE_SELECTION_TOO_LARGE", "The selected rectangle is too large for OCR.");
        }

        private sealed class CaptureLimitException : Exception
        {
            internal CaptureLimitException(string code, string message) : base(message)
            {
                Code = code;
            }

            internal string Code { get; private set; }
        }

        private static int ParseOptions(string[] args, out bool useChinese)
        {
            int parentPid = 0;
            useChinese = false;
            for (int index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length) throw new ArgumentException("Invalid screen capture arguments.");
                if (string.Equals(args[index], "--parent-pid", StringComparison.Ordinal))
                {
                    int parsedPid;
                    if (!int.TryParse(args[index + 1], out parsedPid) || parsedPid <= 0)
                        throw new ArgumentException("Invalid parent process identifier.");
                    parentPid = parsedPid;
                }
                else if (string.Equals(args[index], "--lang", StringComparison.Ordinal))
                {
                    useChinese = args[index + 1].StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    throw new ArgumentException("Invalid screen capture argument.");
                }
            }
            return parentPid;
        }

        private static void EnableDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 keeps virtual-screen and mouse
                // coordinates aligned even when monitors use different scaling factors.
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }

            try { SetProcessDPIAware(); }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
        }

        private sealed class SelectionForm : Form
        {
            private readonly Bitmap _desktop;
            private Point _anchor;
            private Point _current;
            private bool _dragging;
            private readonly Timer _parentTimer;
            private Process _parentProcess;
            private bool _parentMissing;
            private readonly bool _useChinese;

            internal SelectionForm(Bitmap desktop, Rectangle virtualScreen, int parentPid, bool useChinese)
            {
                _desktop = desktop;
                _useChinese = useChinese;
                Cancelled = true;
                Selection = Rectangle.Empty;

                AutoScaleMode = AutoScaleMode.None;
                Bounds = virtualScreen;
                Cursor = Cursors.Cross;
                FormBorderStyle = FormBorderStyle.None;
                KeyPreview = true;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowIcon = false;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                TopMost = true;

                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.UserPaint,
                    true);
                UpdateStyles();

                if (parentPid > 0)
                {
                    try { _parentProcess = Process.GetProcessById(parentPid); }
                    catch (ArgumentException) { _parentMissing = true; }

                    _parentTimer = new Timer();
                    _parentTimer.Interval = 1000;
                    _parentTimer.Tick += CheckParent;
                    _parentTimer.Start();
                }
            }

            internal bool Cancelled { get; private set; }
            internal Rectangle Selection { get; private set; }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams parameters = base.CreateParams;
                    parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: keep the picker out of Alt+Tab.
                    return parameters;
                }
            }

            protected override bool ProcessCmdKey(ref Message message, Keys keyData)
            {
                if (keyData == Keys.Escape)
                {
                    CancelAndClose();
                    return true;
                }
                return base.ProcessCmdKey(ref message, keyData);
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                Activate();
                BringToFront();
                Focus();
                if (_parentMissing) BeginInvoke(new Action(CancelAndClose));
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_parentTimer != null) _parentTimer.Dispose();
                    if (_parentProcess != null) _parentProcess.Dispose();
                }
                base.Dispose(disposing);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Right)
                {
                    CancelAndClose();
                    return;
                }
                if (e.Button != MouseButtons.Left) return;

                _anchor = ClampToClient(e.Location);
                _current = _anchor;
                _dragging = true;
                Capture = true;
                Selection = Rectangle.Empty;
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (!_dragging) return;
                _current = ClampToClient(e.Location);
                Selection = Normalize(_anchor, _current);
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (!_dragging || e.Button != MouseButtons.Left) return;

                _dragging = false;
                Capture = false;
                _current = ClampToClient(e.Location);
                Selection = Normalize(_anchor, _current);
                if (Selection.Width < MinimumSelectionSize || Selection.Height < MinimumSelectionSize)
                {
                    Selection = Rectangle.Empty;
                    Invalidate();
                    return;
                }

                Cancelled = false;
                Close();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.CompositingMode = CompositingMode.SourceCopy;
                e.Graphics.DrawImageUnscaled(_desktop, 0, 0);
                e.Graphics.CompositingMode = CompositingMode.SourceOver;
                using (SolidBrush shade = new SolidBrush(Color.FromArgb(105, 8, 14, 28)))
                    e.Graphics.FillRectangle(shade, ClientRectangle);

                if (!Selection.IsEmpty)
                {
                    e.Graphics.DrawImage(
                        _desktop,
                        Selection,
                        Selection.X,
                        Selection.Y,
                        Selection.Width,
                        Selection.Height,
                        GraphicsUnit.Pixel);
                    using (Pen outline = new Pen(Color.FromArgb(255, 40, 132, 255), 2f))
                    {
                        outline.Alignment = PenAlignment.Inset;
                        e.Graphics.DrawRectangle(outline, Selection);
                    }
                    DrawDimensions(e.Graphics, Selection);
                }
                else
                {
                    DrawInstructions(e.Graphics);
                }
            }

            private void DrawInstructions(Graphics graphics)
            {
                string text = _useChinese
                    ? "拖动选择公式区域  |  按 Esc 或右键取消"
                    : "Drag to select a formula  |  Esc or right-click to cancel";
                using (Font font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point))
                {
                    SizeF size = graphics.MeasureString(text, font);
                    float x = Math.Max(12f, (ClientSize.Width - size.Width) / 2f);
                    RectangleF box = new RectangleF(x - 12f, 18f, size.Width + 24f, size.Height + 14f);
                    using (SolidBrush background = new SolidBrush(Color.FromArgb(210, 20, 25, 36)))
                    using (SolidBrush foreground = new SolidBrush(Color.White))
                    {
                        graphics.FillRectangle(background, box);
                        graphics.DrawString(text, font, foreground, x, 25f);
                    }
                }
            }

            private static void DrawDimensions(Graphics graphics, Rectangle selection)
            {
                string text = selection.Width + " x " + selection.Height;
                using (Font font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point))
                {
                    SizeF size = graphics.MeasureString(text, font);
                    float x = selection.Left;
                    float y = selection.Top - size.Height - 8f;
                    if (y < 2f) y = Math.Min(selection.Bottom + 4f, graphics.VisibleClipBounds.Bottom - size.Height - 6f);
                    using (SolidBrush background = new SolidBrush(Color.FromArgb(225, 20, 25, 36)))
                    using (SolidBrush foreground = new SolidBrush(Color.White))
                    {
                        graphics.FillRectangle(background, x, y, size.Width + 10f, size.Height + 4f);
                        graphics.DrawString(text, font, foreground, x + 5f, y + 2f);
                    }
                }
            }

            private Point ClampToClient(Point point)
            {
                int x = Math.Max(0, Math.Min(ClientSize.Width, point.X));
                int y = Math.Max(0, Math.Min(ClientSize.Height, point.Y));
                return new Point(x, y);
            }

            private static Rectangle Normalize(Point first, Point second)
            {
                int left = Math.Min(first.X, second.X);
                int top = Math.Min(first.Y, second.Y);
                return new Rectangle(left, top, Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));
            }

            private void CancelAndClose()
            {
                Cancelled = true;
                Selection = Rectangle.Empty;
                Capture = false;
                Close();
            }

            private void CheckParent(object sender, EventArgs e)
            {
                try
                {
                    if (_parentMissing || _parentProcess == null || _parentProcess.HasExited)
                        CancelAndClose();
                }
                catch (InvalidOperationException)
                {
                    CancelAndClose();
                }
            }
        }
    }
}
