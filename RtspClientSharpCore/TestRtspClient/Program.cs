using System;
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
using System.Net.Sockets;
using System.Net;
using System.Text;

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

        private static string DBPath;
        static void Main(string[] args)
        {
            isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            Console.WriteLine($"Platform {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");

            if( isLinux )
            {
                DBPath = Environment.CurrentDirectory + @"/IPFramesDB.db";                
            }
            else
            {
                DBPath = Environment.CurrentDirectory + @"\IPFramesDB.db";
            }

            //createDB
            Console.WriteLine("creating DB... at :" + DBPath );
            CreateDB();

            //부하주는 Task들
            Console.WriteLine("Load CPU...");
            ImplicitLoadCPU();

            //monitor용 network server 동작
            Console.WriteLine("Start server for monitoring...");
            AysncQueryServer();

            Console.WriteLine("Start server for rtsp streaming...");
            string uri = "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mov";
            //string uri = "rtsp://127.0.0.1:8554/test";
            //string uri = "rtsp://123.30.182.75:554/vod/mp4:bird.mp4";
            //var serverUri = new Uri("rtsp://localhost:8554/test/BillGates_2020-480p-ko.mp4");
            //var serverUri = new Uri(uri);
            var serverUri = new Uri(uri);
            Console.WriteLine(uri);
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

        static void ImplicitLoadCPU()
        {
            int loadWeight = 0;
            /*
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

            for (int i = 0; i < loadWeight; i++)
            {
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(1000 * 5); //5 second
                    float temp = 7;
                    ulong count = 0;
                    while (true)
                    {
                        temp = (temp * (temp + (float)1.414)) / temp;
                        count++;

                        if (count == 100000000 * 10)
                        {
                            Thread.Sleep(1000 * 10); //10 second
                            count = 0;
                        }
                    }
                });
            }
        }
        async static Task AysncQueryServer()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 7000);
            listener.Start();
            while (true)
            {
                // 비동기 Accept                
                TcpClient tc = await listener.AcceptTcpClientAsync().ConfigureAwait(false);

                // 새 쓰레드에서 처리
                Task.Factory.StartNew(AsyncTcpProcess, tc);
            }
        }
        async static void AsyncTcpProcess(object o)
        {
            TcpClient tc = (TcpClient)o;

            int MAX_SIZE = 1024;  // 가정
            NetworkStream stream = tc.GetStream();
            /*

            string msg = "Hello World";
            byte[] buff = Encoding.ASCII.GetBytes(msg);

            // (3) 스트림에 바이트 데이타 전송
            stream.Write(buff, 0, buff.Length);
            Console.WriteLine("Send Hellow world");
            */

            string queryResult = "";

            // 비동기 수신            
            var buff = new byte[MAX_SIZE];
            var nbytes = await stream.ReadAsync(buff, 0, buff.Length).ConfigureAwait(false);
            if (nbytes > 0)
            {
                string msg = Encoding.ASCII.GetString(buff, 0, nbytes);
                Console.WriteLine($"{msg} at {DateTime.Now}");

                queryResult = queryDB(msg);
                buff = Encoding.ASCII.GetBytes(queryResult);
                Console.WriteLine(queryResult);

                // 비동기 송신
                await stream.WriteAsync(buff, 0, buff.Length).ConfigureAwait(false);
            }


            stream.Close();
            tc.Close();
        }

        static string queryDB(string queryString)
        {
            //string strConn = @"Data Source=E:\work\db\ipframes.db";
            //string DBPath = @"E:\work\RtspClientSharpCore\RtspClientSharpCore\TestRtspClient\bin\x64\Debug\netcoreapp3.0\IPFramesDB.db";
            string strConn = @"Data Source = " + DBPath;

            string queryResult = "Failed to query";

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(strConn))
                {

                    conn.Open();

                    using (SQLiteCommand cmd = new SQLiteCommand(queryString, conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        { 

                            DateTime currentTime = DateTime.Now;

                            if (reader.Read())
                            {
                                queryResult = currentTime + " :: i frames=" + reader["sum(i)"] + ", p frames=" + reader["sum(p)"];
                            }
                            else
                            {
                                queryResult = "Failed to read DB. Qeury String is :" + queryString;
                            }
                        }
                    }

                    conn.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error saving to file: {e.Message}");
                Debug.WriteLine($"Error saving to file: {e.Message}");
                Debug.WriteLine($"Stack trace: {e.StackTrace}");
            }
            

            return queryResult;
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
                    using (SQLiteConnection conn = new SQLiteConnection(strConn))
                    { 
                        conn.Open();

                        if (_FrameType == "IFrame")
                        {
                            string sql = "INSERT Into Frames VALUES( 1, 0, datetime('now', 'localtime'))";
                            using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            string sql = "INSERT Into Frames VALUES( 0, 1, datetime('now', 'localtime'))";
                            using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }

                        conn.Close();
                    }
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
