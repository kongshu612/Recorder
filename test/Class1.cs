using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SharpAvi;
using SharpAvi.Output;
using SharpAvi.Codecs;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace myRecorder
{

    public class TakeScreenShot
    {
        [STAThread]
        public static Image CaptureScreen(Rectangle area)
        {
            Bitmap screenShotBmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppRgb);
            Graphics g_screenShot = Graphics.FromImage(screenShotBmp);
            g_screenShot.CopyFromScreen(area.X, area.Y, 0, 0, area.Size, CopyPixelOperation.SourceCopy);
            return screenShotBmp;
        }

        [STAThread]
        public static Image CaptureScreen()
        {
            Rectangle area = Screen.PrimaryScreen.Bounds;
            return CaptureScreen(area);
        }
    }

    public class ConvertToAvi
    {
        private AviWriter _instance;
        private IAviVideoStream AviVStream;

        #region Construct

        public ConvertToAvi(
            string targetFile,   // the target video file path  name.avi
            int Width,         // specify the Width of the video.
            int Height,        // specify the Hegight of the video. 
            int framesPerSec = 10,     // the frames number Per Second, the default value is 10
            string encoderName = "",      // the encoder name , now support value is Jpeg, if not supplied, no-compress format will be used.
            int Quality = 10         // specify the quality of the video, the validate range is 1-100.this parameter company with the encoderName.
            )
        {
            _instance = new AviWriter(targetFile)
            {
                FramesPerSecond = framesPerSec,
                // Emitting AVI v1 index in addition to OpenDML index (AVI v2)
                // improves compatibility with some software, including 
                // standard Windows programs like Media Player and File Explorer
                EmitIndex1 = true
            };
            AviVStream = _instance.AddVideoStream();
            AviVStream.Width = Width;
            AviVStream.Height = Height;
            if (string.IsNullOrEmpty(encoderName))
            {
                // class SharpAvi.KnownFourCCs.Codecs contains FOURCCs for several well-known codecs
                // Uncompressed is the default value, just set it for clarity
                // if Uncompressed is used, the bitmap data is stardant bottom to up.
                AviVStream.Codec = KnownFourCCs.Codecs.Uncompressed;
                // Uncompressed format requires to also specify bits per pixel
                AviVStream.BitsPerPixel = BitsPerPixel.Bpp32;
            }
            else if (encoderName == "Jpeg")
            {
                if (Quality < 1 || Quality > 100)
                {
                    Console.WriteLine("the Quality is out of validate range. use the default 10");
                    Quality = 10;
                }
                MotionJpegVideoEncoderWpf encoding = new MotionJpegVideoEncoderWpf(Width, Height, 20);
                AviVStream = _instance.AddEncodingVideoStream(encoding, width: Width, height: Height);

                //MotionJpegVideoEncoderWpf encoding = new MotionJpegVideoEncoderWpf(Width, Height, Quality);
                //AviVStream = _instance.AddEncodingVideoStream(encoding, width: Width, height: Height);
            }
            else
            {
                Console.WriteLine("not support encoder");
            }
        }

        #endregion

        private byte[] BitmapToByteArray(Bitmap bitmap)
        {
            BitmapData bmpdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            int numbytes = bmpdata.Stride * bitmap.Height;
            byte[] bytedata = new byte[numbytes];
            IntPtr ptr = bmpdata.Scan0;
            Marshal.Copy(ptr, bytedata, 0, numbytes);
            bitmap.UnlockBits(bmpdata);
            return bytedata;
        }

        public void Convert(Bitmap tmpImage)
        {
            byte[] framdata = new byte[0];
            framdata = BitmapToByteArray(tmpImage);
            AviVStream.WriteFrame(true, framdata, 0, framdata.Length);
        }

        public void CloseAvi()
        {
            _instance.Close();
        }

        ~ConvertToAvi()
        {
            CloseAvi();
        }
    }

    public class ModifyRegistry
    {

        private string subKey = "SOFTWARE\\UIRecorder";
        /// <summary>
        /// A property to set the SubKey value
        /// </summary>
        public string SubKey
        {
            get { return subKey; }
            set { subKey = value; }
        }


        private RegistryKey baseRegistryKey = Registry.LocalMachine;
        /// <summary>
        /// A property to set the BaseRegistryKey value.
        /// (default = Registry.LocalMachine)
        /// </summary>
        public RegistryKey BaseRegistryKey
        {
            get { return baseRegistryKey; }
            set { baseRegistryKey = value; }
        }

        /* **************************************************************************
		 * **************************************************************************/

        /// <summary>
        /// To read a registry key.
        /// input: KeyName (string)
        /// output: value (string) 
        /// </summary>
        public object Read(string KeyName)
        {
            // Opening the registry key
            RegistryKey rk = baseRegistryKey;
            // Open a subKey as read-only
            RegistryKey sk1 = rk.OpenSubKey(subKey);
            // If the RegistrySubKey doesn't exist -> (null)
            if (sk1 == null)
            {
                return null;
            }
            return sk1.GetValue(KeyName);
        }

        /* **************************************************************************
         * **************************************************************************/

        /// <summary>
        /// To write into a registry key.
        /// input: KeyName (string) , Value (object)
        /// output: true or false 
        /// </summary>
        public bool Write(string KeyName, object Value)
        {
            try
            {
                // Setting
                RegistryKey rk = baseRegistryKey;
                // I have to use CreateSubKey 
                // (create or open it if already exits), 
                // 'cause OpenSubKey open a subKey as read-only
                RegistryKey sk1 = rk.CreateSubKey(subKey);
                // Save the value
                sk1.SetValue(KeyName.ToUpper(), Value);

                return true;
            }
            catch (Exception e)
            {
                // AAAAAAAAAAARGH, an error!
                return false;
            }
        }
    }

    public class Recorder
    {
        public static void StartRecord()
        {
            ModifyRegistry RInstance = new ModifyRegistry();
            string aviFile = (string)RInstance.Read("aviFilePath");
            string encodingName = (string)RInstance.Read("encodingName");
            int framesPerSecond = (int)RInstance.Read("framesPerSecond");
            int X = (int)RInstance.Read("X");
            int Y = (int)RInstance.Read("Y");
            int Width = (int)RInstance.Read("Width");
            int Height = (int)RInstance.Read("Height");
            int Quality = (int)RInstance.Read("Quality");
            int TimeLimit = (int)RInstance.Read("TimeLimit");
            Boolean flag = RInstance.Write("status", 1);
            Rectangle area = new Rectangle()
            {
                X = X,
                Y = Y,
                Width = Width,
                Height = Height
            };
            ConvertToAvi AviC = new ConvertToAvi(aviFile, framesPerSec: framesPerSecond, encoderName: encodingName, Width: Width, Height: Height, Quality: Quality);
            int pauseTime = 2000 / framesPerSecond;
            int Status = (int)RInstance.Read("status");
            DateTime TimeEnd = DateTime.Now.AddSeconds(TimeLimit);
            while (Status == 1 && DateTime.Now < TimeEnd)
            {
                Image tmp = TakeScreenShot.CaptureScreen(area);
                AviC.Convert((Bitmap)tmp);
                Thread.Sleep(pauseTime);
                Status = (int)RInstance.Read("status");
            }
            AviC.CloseAvi();
            RInstance.Write("status", 0);
        }
    }

}
