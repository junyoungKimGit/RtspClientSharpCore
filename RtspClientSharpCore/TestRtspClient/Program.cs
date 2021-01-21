﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FrameDecoderCore;
using FrameDecoderCore.DecodedFrames;
using RtspClientSharpCore;
using RtspClientSharpCore.RawFrames.Video;
using RtspClientSharpCore.Rtsp;
using FrameDecoderCore.FFmpeg;
using RtspClientSharpCore.RawFrames;
using PixelFormat = FrameDecoderCore.PixelFormat;
using System.Data.SQLite;
using System.IO;

namespace TestRtspClient
{
    class Program
    {

        private const int STREAM_WIDTH = 240;
        private const int STREAM_HEIGHT = 160;
        private static readonly Dictionary<FFmpegVideoCodecId, FFmpegVideoDecoder> _videoDecodersMap =
            new Dictionary<FFmpegVideoCodecId, FFmpegVideoDecoder>();
        //public static event EventHandler<IDecodedVideoFrame> FrameReceived;
        private static bool isWindows;
        private static bool isLinux;

        private static string DBPath = Environment.CurrentDirectory + @"\IPFramesDB.db";
        static void Main(string[] args)
        {
            isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            Console.WriteLine($"Platform {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");

            if( isLinux )
            {
                DBPath = Environment.CurrentDirectory + @"/IPFramesDB.db";
            }

            //createDB
            Console.WriteLine("creating DB... at :" + DBPath);
            CreateDB();

            //부하주는 Task
            //comment for docket 
            int loadWeight = 3;
            /*

            Console.WriteLine("input load task count : ");

            while( true )
            {
                loadWeight = int.Parse(Console.ReadLine());

                if ( loadWeight > 20)
                {
                    Console.WriteLine("Too high number, try again ( samller than 20) :");
                }
                else
                {
                    break;
                }
            }
            */
            for(int i = 0; i < loadWeight; i++ )
            { 
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(1000 * 5); //5 second
                    float temp = 7;
                    while(true)
                    {
                        temp = (temp * (temp + (float)1.414)) / temp;
                    }
                });
            }

            var serverUri = new Uri("rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mov");
            Console.WriteLine("rtsp source : rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mov");
            //var credentials = new NetworkCredential("admin", "admin12345678");


            //rtsp connection
            var connectionParameters = new ConnectionParameters(serverUri/*, credentials*/);
            var cancellationTokenSource = new CancellationTokenSource();

            Task connectTask = ConnectAsync(connectionParameters, cancellationTokenSource.Token);

            Console.WriteLine("Press any key to cancel");
            Console.ReadLine();

            cancellationTokenSource.Cancel();

            Console.WriteLine("Canceling");
            connectTask.Wait(CancellationToken.None);
        }


        private static async Task ConnectAsync(ConnectionParameters connectionParameters, CancellationToken token)
        {
            try
            {
                TimeSpan delay = TimeSpan.FromSeconds(5);

                using (var rtspClient = new RtspClient(connectionParameters))
                {
                    rtspClient.FrameReceived += RtspClient_FrameReceived;

                    while (true)
                    {
                        Console.WriteLine("Connecting...");

                        try
                        {
                            await rtspClient.ConnectAsync(token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (RtspClientException e)
                        {
                            Console.WriteLine(e.ToString());
                            await Task.Delay(delay, token);
                            continue;
                        }

                        Console.WriteLine("Connected.");

                        try
                        {
                            await rtspClient.ReceiveAsync(token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (RtspClientException e)
                        {
                            Console.WriteLine(e.ToString());
                            await Task.Delay(delay, token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static void RtspClient_FrameReceived(object sender, RawFrame rawFrame)
        {
            
            if (!(rawFrame is RawVideoFrame rawVideoFrame))
                return;

            FFmpegVideoDecoder decoder = GetDecoderForFrame(rawVideoFrame);
            IDecodedVideoFrame decodedFrame = decoder.TryDecode(rawVideoFrame);

            if (decodedFrame != null) 
            {
                var _FrameType = rawFrame is RawH264IFrame ? "IFrame" : "PFrame";

                //myCode
                DateTime currentTime = DateTime.Now;

                Console.WriteLine($"{currentTime} ::Frame is {_FrameType }...");

                //string strConn = @"Data Source=E:\work\db\ipframes.db";
                string strConn = @"Data Source = " + DBPath;

                try
                {
                    SQLiteConnection conn = new SQLiteConnection(strConn);

                    conn.Open();

                    if (_FrameType == "IFrame")
                    {
                        string sql = "INSERT Into Frames VALUES( 1, 0, datetime('now', 'localtime'))";
                        SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        string sql = "INSERT Into Frames VALUES( 0, 1, datetime('now', 'localtime'))";
                        SQLiteCommand cmd = new SQLiteCommand(sql, conn);
                        cmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error saving to file: {e.Message}");
                    Debug.WriteLine($"Error saving to file: {e.Message}");
                    Debug.WriteLine($"Stack trace: {e.StackTrace}");
                }

                //original
                /*
                TransformParameters _transformParameters = new TransformParameters(RectangleF.Empty,
                    new Size(STREAM_WIDTH, STREAM_HEIGHT),
                    ScalingPolicy.Stretch, PixelFormat.Bgra32, ScalingQuality.FastBilinear);

                var pictureSize = STREAM_WIDTH* STREAM_HEIGHT;
                IntPtr unmanagedPointer = Marshal.AllocHGlobal(pictureSize*4);

                decodedFrame.TransformTo(unmanagedPointer, STREAM_WIDTH*4, _transformParameters);
                byte[] managedArray = new byte[pictureSize*4];
                Marshal.Copy(unmanagedPointer, managedArray, 0, pictureSize*4);
                Marshal.FreeHGlobal(unmanagedPointer);
                Console.WriteLine($"Frame was successfully decoded! {_FrameType } Trying to save to JPG file...");
                try
                {
                    var im = CopyDataToBitmap(managedArray);
                   if (isWindows)
                    {
                        // Change to your path
                        im.Save(@"E:\TestPhoto\image21.jpg", ImageFormat.Jpeg);
                        return;
                    }
                    if (isLinux)
                    {
                        // Change to your path
                        im.Save(@"/home/alex/image21.jpg", ImageFormat.Jpeg);
                        return;
                    }
                    throw new PlatformNotSupportedException("Not supported OS platform!!");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error saving to file: {e.Message}");
                    Debug.WriteLine($"Error saving to file: {e.Message}");
                    Debug.WriteLine($"Stack trace: {e.StackTrace}");
                }
                */
            }
           
        }


        private static Bitmap CopyDataToBitmap(byte[] data)
        {
            //Here create the Bitmap to the know height, width and format
            Bitmap bmp = new Bitmap( STREAM_WIDTH, STREAM_HEIGHT, System.Drawing.Imaging.PixelFormat.Format32bppArgb);  

            //Create a BitmapData and Lock all pixels to be written 
            BitmapData bmpData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),   
                ImageLockMode.WriteOnly, bmp.PixelFormat);
 
            //Copy the data from the byte array into BitmapData.Scan0
            Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
            //Unlock the pixels
            bmp.UnlockBits(bmpData);
            //Return the bitmap 
            return bmp;
        }

        private static FFmpegVideoDecoder GetDecoderForFrame(RawVideoFrame videoFrame)
        {
            FFmpegVideoCodecId codecId = DetectCodecId(videoFrame);
            if (!_videoDecodersMap.TryGetValue(codecId, out FFmpegVideoDecoder decoder))
            {
                decoder = FFmpegVideoDecoder.CreateDecoder(codecId);
                _videoDecodersMap.Add(codecId, decoder);
            }

            return decoder;
        }

        private static FFmpegVideoCodecId DetectCodecId(RawVideoFrame videoFrame)
        {
            if (videoFrame is RawJpegFrame)
                return FFmpegVideoCodecId.MJPEG;
            if (videoFrame is RawH264Frame)
                return FFmpegVideoCodecId.H264;

            throw new ArgumentOutOfRangeException(nameof(videoFrame));
        }
        private static void CreateDB()
        {
            //파일이 없으면 만들기
            FileInfo fileInfo = new FileInfo(DBPath);

            if ( fileInfo.Exists == false)
            {
                SQLiteConnection.CreateFile(DBPath);

                string connString = @"Data Source = " + DBPath + ";";

                // DB 파일에 테이블을 생성하는 코드
                using (SQLiteConnection conn = new SQLiteConnection(connString))
                {
                    conn.Open();
                    // 테이블 및 필드생성
                    string sql = "create table Frames (i int, p int, TimeStamp TEXT)";

                    SQLiteCommand command = new SQLiteCommand(sql, conn);
                    int result = command.ExecuteNonQuery();

                    command.Dispose();
                    conn.Close();
                }
            }
        }
    }
}
