using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace CodeSmith.Core.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// Captures the Desktop in a screenshot.
        /// </summary>
        /// <returns>Screenshot of the Desktop.</returns>
        public static Image CaptureDesktop()
        {
            return CaptureWindow(NativeMethods.GetDesktopWindow());
        }

        /// <summary>
        /// Captures the Foreground window in a screenshot.
        /// </summary>
        /// <returns>Screenshot of the current Foreground window.</returns>
        public static Image CaptureForegroundWindow()
        {
            return CaptureWindow(NativeMethods.GetForegroundWindow());
        }

        /// <summary>
        /// Captures a screenshot of the window associated with the handle argument.
        /// </summary>
        /// <param name="handle">Used to determine which window to provide a screenshot for.</param>
        /// <returns>Screenshot of the window corresponding to the handle argument.</returns>
        public static Image CaptureWindow(IntPtr handle)
        {
            IntPtr sourceContext = NativeMethods.GetWindowDC(handle);
            IntPtr destinationContext = NativeMethods.CreateCompatibleDC(sourceContext);

            NativeMethods.RECT windowRect = new NativeMethods.RECT();
            NativeMethods.GetWindowRect(handle, ref windowRect);
            int width = windowRect.right - windowRect.left;
            int height = windowRect.bottom - windowRect.top;

            IntPtr bitmap = NativeMethods.CreateCompatibleBitmap(sourceContext, width, height);
            IntPtr replaceContext = NativeMethods.SelectObject(destinationContext, bitmap);
            NativeMethods.BitBlt(destinationContext, 0, 0, width, height, sourceContext, 0, 0, NativeMethods.SRCCOPY);

            NativeMethods.SelectObject(destinationContext, replaceContext);

            NativeMethods.DeleteDC(destinationContext);
            NativeMethods.ReleaseDC(handle, sourceContext);

            Image img = Image.FromHbitmap(bitmap);

            NativeMethods.DeleteObject(bitmap);

            return img;
        }

        /// <summary>
        /// Saves the encoded image to a file
        /// </summary>
        /// <param name="image">The image to save</param>
        /// <param name="quality">The quality desired (a value between 1 and 100).</param>
        /// <param name="format">The <see cref="ImageFormat"/> to save the image as.</param>
        /// <param name="fileName">The file path to save the image to.</param>
        public static void ToFile(Image image, long quality, ImageFormat format, string fileName)
        {
            using (Stream original = ToStream(image, quality, format))
            {
                using (var writer = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    original.Position = 0;
                    byte[] buffer = new byte[original.Length];
                    int bytesRead = original.Read(buffer, 0, (int)original.Length);
                    writer.Write(buffer, 0, bytesRead);
                    writer.Flush();
                }
            }
        }

        /// <summary>
        /// Saves the encoded image to a stream.
        /// </summary>
        /// <param name="image">The image to save</param>
        /// <param name="quality">The quality desired (a value between 1 and 100).</param>
        /// <param name="format">The <see cref="ImageFormat"/> to save the image as.</param>
        /// <returns></returns>
        public static Stream ToStream(Image image, long quality, ImageFormat format)
        {
            var memoryStream = new MemoryStream();
            ImageCodecInfo encoder = null;
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

            foreach (ImageCodecInfo codec in encoders)
            {
                if (codec.MimeType == "image/jpeg")
                    encoder = codec;
            }

            var encoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = encoderParameter;

            image.Save(memoryStream, encoder, encoderParameters);

            return memoryStream;
        }

        public static byte[] GetBytes(Image image)
        {
            byte[] result = null;

            var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Jpeg);
            result = stream.ToArray();

            return result;

        }

        //TODO: Determine content type
    }
}
