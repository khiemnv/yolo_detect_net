using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Extensions
{
    internal static class ImageExtensions
    {
        public static Bitmap CloneFromFile(this string path)
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            var img = System.Drawing.Image.FromStream(ms);
            return (Bitmap)img;
        }
        public static bool SaveAsJpeg(this Bitmap img, string path)
        {
            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            EncoderParameters myEncoderParameters = new EncoderParameters(1);
            System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
            EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 90L);
            myEncoderParameters.Param[0] = myEncoderParameter;
            img.Save(path, jpgEncoder, myEncoderParameters);
            return true;
        }
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        public static Bitmap Base64StringToBitmap(this string base64String)
        {
            byte[] byteBuffer = Convert.FromBase64String(base64String);
            MemoryStream memoryStream = new MemoryStream(byteBuffer)
            {
                Position = 0
            };

            //Bitmap bmpReturn = (Bitmap)Image.FromStream(memoryStream);
            Bitmap bmpReturn = new Bitmap(memoryStream);
            //memoryStream.Close();
            return bmpReturn;
        }

        #region Public Methods

        /// <summary>
        /// Extension method that converts a Image to an byte array
        /// </summary>
        /// <param name="imageIn">The Image to convert</param>
        /// <returns>An byte array containing the JPG format Image</returns>
        public static byte[] ToArray(this SixLabors.ImageSharp.Image imageIn)
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                imageIn.Save(ms, JpegFormat.Instance);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Extension method that converts a Image to an byte array
        /// </summary>
        /// <param name="imageIn">The Image to convert</param>
        /// <param name="fmt"></param>
        /// <returns>An byte array containing the JPG format Image</returns>
        public static byte[] ToArray(this SixLabors.ImageSharp.Image imageIn, IImageFormat fmt)
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                imageIn.Save(ms, fmt);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Extension method that converts a Image to an byte array
        /// </summary>
        /// <param name="imageIn">The Image to convert</param>
        /// <returns>An byte array containing the JPG format Image</returns>
        public static byte[] ToArray(this global::System.Drawing.Image imageIn)
        {
            return ToArray(imageIn, System.Drawing.Imaging.ImageFormat.Png);
        }

        /// <summary>
        /// Converts the image data into a byte array.
        /// </summary>
        /// <param name="imageIn">The image to convert to an array</param>
        /// <param name="fmt">The format to save the image in</param>
        /// <returns>An array of bytes</returns>
        public static byte[] ToArray(this global::System.Drawing.Image imageIn, System.Drawing.Imaging.ImageFormat fmt)
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                imageIn.Save(ms, fmt);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Extension method that converts a byte array with JPG data to an Image
        /// </summary>
        /// <param name="byteArrayIn">The byte array with JPG data</param>
        /// <returns>The reconstructed Image</returns>
        public static Image<Rgba32> ToImage(this byte[] byteArrayIn)
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(byteArrayIn))
            {
                var returnImage = SixLabors.ImageSharp.Image.Load<Rgba32>(ms);
                return returnImage;
            }
        }

        public static global::System.Drawing.Image ToNetImage(this byte[] byteArrayIn)
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(byteArrayIn))
            {
                global::System.Drawing.Image returnImage = global::System.Drawing.Image.FromStream(ms);
                return returnImage;
            }
        }

        #endregion Public Methods
    }
}
