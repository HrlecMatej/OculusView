using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OculusStreetViewHyperlapse.Hyperlapse.StreetView
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class StreetView : Window
    {
        int indexBuff = 0;
        
        int limitX;
        int limitY;

        int cropHeightZoom2 = 320;
        int cropHeigthZoom3 = 128;

        public bool pause = false;
        public bool loopWhole = false;
        public bool moveForwards = false;
        public bool moveBackwards = false;

        public bool restart = false;

        public bool playForward = true;

        BackgroundWorker loader = new BackgroundWorker();

        List<Stream> buffStream;

        List<List<Stream>> jumpBuffStream;
        List<List<Stream>> previousJumpBuffStream;

        SharpDX.WIC.ImagingFactory imagingFactory = new SharpDX.WIC.ImagingFactory();

        int zoom = 2;
        List<string> listPanoIds;
        bool oculusMode;

        public StreetView(List<string> listPanoIds, int specifiedZoom, bool oculusMode)
        {
            this.oculusMode = oculusMode;
            zoom = specifiedZoom;

            if (zoom == 3)
            {
                limitX = 7;
                limitY = 4;
            }
            else if (zoom == 2)
            {
                limitX = 4;
                limitY = 2;
            }

            this.listPanoIds = listPanoIds;

            InitializeComponent();
            Viewer.MouseLeftButtonDown += new MouseButtonEventHandler(Viewer_MouseLeftButtonDown);
            Viewer.MouseMove += new MouseEventHandler(Viewer_MouseMove);

            buffStream = new List<Stream>();

            // removes white lines between tiles!
            SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

            loader.DoWork += new DoWorkEventHandler(loader_DoWork);
            loader.ProgressChanged += new ProgressChangedEventHandler(loader_ProgressChanged);
            loader.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loader_RunWorkerCompleted);
            loader.WorkerReportsProgress = true;
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.H)
            {
                MessageBox.Show("Street View commands:\n\nKey Space to pause/resume,\nKey L to loop ,\nKeys Up and Down to move by one jump,\nKey R to restart playing.", 
                    "Help", MessageBoxButton.OK, MessageBoxImage.Question);
            }
            else if (e.Key == Key.Space)
            {
                pause = !pause;
                moveForwards = false;
                moveBackwards = false;
            }
            else if (e.Key == Key.L)
            {
                if (loopWhole == true)
                {
                    loopWhole = false;
                }
                else if (loopWhole == false)
                {
                    loopWhole = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                pause = true;
                moveForwards = true;
                moveBackwards = false;
            }
            else if (e.Key == Key.Down)
            {
                pause = true;
                moveBackwards = true;
                moveForwards = false;
            }
            else if (e.Key == Key.R)
            {
                restart = true;

                pause = false;
                moveForwards = false;
                moveBackwards = false;

                playForward = true;
            }
        }

        async void loader_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

            if (oculusMode)
                oculusViewMode();
            else
                streetViewMode();
            
        }

        //unsuccesful attempt at paralelization
        /*async Task addToMainPanel(StackPanel mainPanel, int i, int j)
        {
            StackPanel ph = new StackPanel();
            ph.Orientation = Orientation.Horizontal;
            for (int k = 0; k < limitX; k++)
            {
                Image stackImg = new Image();
                stackImg.Source = FromStream(buffStream[i][j][k]);
                ph.Children.Add(stackImg);
            }
            mainPanel.Children.Insert(j, ph);
        }*/

        async private void oculusViewMode()
        {
            ResizeMode = ResizeMode.CanResize;
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;


            OculusView oculusViewForm = new OculusView(zoom, this);

            while (oculusViewForm.oculusReady == false)
            {
                await Task.Delay(100);
            }

            await Task.Delay(1000);
            displayOculusView(oculusViewForm, 0);

            while (true)
            {
                await Task.Delay(100);
                if (restart)
                {
                    restart = false;
                    displayOculusView(oculusViewForm, 0);
                }
            }
        }

        async private void displayOculusView(OculusView oculusViewForm, int i)
        {
            if (restart)
            {
                restart = false;
                displayOculusView(oculusViewForm, 0);
                return;
            }

            oculusViewForm.streamTexture = buffStream[i];
            //oculusViewForm.bitMap = oculusViewForm.LoadBitmap(imagingFactory, buffStream[i]);
            oculusViewForm.newTextureArrived = true;

            Title = "Oculus View, jump number " + (i+1) + ". Press H for help! Options: pause=" + pause + ", loop=" + loopWhole + ", playing forward=" + playForward;

            GC.Collect();
            if (zoom == 2)
                await Task.Delay(70);
            else if (zoom == 3)
                await Task.Delay(140);

            if (moveForwards)
            {
                moveForwards = false;
                if (i < listPanoIds.Count - 1)
                {
                    displayOculusView(oculusViewForm, ++i);
                    return;
                }
            }
            if (moveBackwards)
            {
                moveBackwards = false;
                if (i > 0)
                {
                    displayOculusView(oculusViewForm, --i);
                    return;
                }
            }

            while (pause)
            {
                if (moveForwards)
                {
                    moveForwards = false;
                    if (i < listPanoIds.Count - 1)
                    {
                        displayOculusView(oculusViewForm, ++i);
                        return;
                    }
                }
                if (moveBackwards)
                {
                    moveBackwards = false;
                    if (i > 0)
                    {
                        displayOculusView(oculusViewForm, --i);
                        return;
                    }
                }
                await Task.Delay(45);
            }

            if (loopWhole)
            {
                if (!playForward)
                {
                    if (i > 0)
                    {
                        displayOculusView(oculusViewForm, --i);
                        return;
                    }
                    else if (i <= 0)
                    {
                        playForward = true;
                        displayOculusView(oculusViewForm, ++i);
                        return;
                    }
                }
                else if (playForward)
                {
                    if (i < listPanoIds.Count - 1)
                    {
                        displayOculusView(oculusViewForm, ++i);
                        return;
                    }
                    else if (i >= listPanoIds.Count - 1)
                    {
                        playForward = false;
                        displayOculusView(oculusViewForm, --i);
                        return;
                    }
                }
            }
            if (!loopWhole)
            {
                if (!playForward)
                {
                    if (i > 0)
                    {
                        displayOculusView(oculusViewForm, --i);
                        return;
                    }
                }
                else if (playForward)
                {
                    if (i < listPanoIds.Count - 1)
                    {
                        displayOculusView(oculusViewForm, ++i);
                        return;
                    }
                }
            }
        }

        async private void streetViewMode()
        {
            

            switch (zoom)
            {
                case (2):
                    {
                        ResizeMode = ResizeMode.CanResize;
                        WindowStyle = WindowStyle.SingleBorderWindow;
                        WindowState = WindowState.Normal;
                        break;
                    }
                case (3):
                    {
                        WindowState = WindowState.Maximized;
                        break;
                    }
            }

            displayStreetView(0);
            while (true)
            {
                await Task.Delay(100);
                if (restart)
                {
                    restart = false;
                    displayStreetView(0);
                }
            }
        }

        async private void displayStreetView(int i)
        {
            if (restart)
            {
                restart = false;
                displayStreetView(0);
                return;
            }

            /*StackPanel mainPanel = new StackPanel();

            Image stackImg = new Image();
            stackImg.Source = FromStream(buffStream[i]);
            mainPanel.Children.Add(stackImg);*/

            GC.Collect();
            if (zoom == 2) 
                await Task.Delay(70);
            else if (zoom == 3)
                await Task.Delay(140);

            /*if (canvas.Children.Count > 0)
            {
                canvas.Children.RemoveAt(0);
            }


            mainPanel.UpdateLayout();

            canvas.Children.Add(mainPanel);

            canvas.UpdateLayout();

            //_RenderTargetBitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            _RenderTargetBitmap.Render(mainPanel);*/

            //Viewer.PanoramaImage = _RenderTargetBitmap;
            Viewer.PanoramaImage = FromStream(buffStream[i]);
            Title = "Street View, jump number " + (i+1) + ". Press H for help! Options: pause="+pause+", loop="+loopWhole+", playing forward="+playForward;

            if (moveForwards)
            {
                moveForwards = false;
                if (i < listPanoIds.Count - 1)
                {
                    displayStreetView(++i);
                    return;
                }
            }
            if (moveBackwards)
            {
                moveBackwards = false;
                if (i > 0)
                {
                    displayStreetView(--i);
                    return;
                }
                
            }

            while (pause) 
            {
                if (moveForwards)
                {
                    moveForwards = false;
                    if (i < listPanoIds.Count - 1)
                    {
                        displayStreetView(++i);
                        return;
                    }
                }
                if (moveBackwards)
                {
                    moveBackwards = false;
                    if (i > 0)
                    {
                        displayStreetView(--i);
                        return;
                    }
                }
                await Task.Delay(45);
            }

            if (loopWhole)
            {
                if (!playForward)
                {
                    if (i > 0)
                    {
                        displayStreetView(--i);
                        return;
                    }
                    else if (i <= 0)
                    {
                        playForward = true;
                        displayStreetView(++i);
                        return;
                    }
                }
                else if (playForward)
                {
                    if (i < listPanoIds.Count - 1)
                    {
                        displayStreetView(++i);
                        return;
                    }
                    else if (i >= listPanoIds.Count - 1)
                    {
                        playForward = false;
                        displayStreetView(--i);
                        return;
                    }
                }
            }
            if (!loopWhole)
            {
                if (!playForward)
                {
                    if (i > 0)
                    {
                        displayStreetView(--i);
                        return;
                    }
                }
                else if (playForward)
                {
                    if (i < listPanoIds.Count - 1)
                    {
                        displayStreetView(++i);
                        return;
                    }
                }
            }
        }


        Vector RotationVector = new Vector();
        Point DownPoint = new Point();
        void Viewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
                return;
            Vector Offset = Point.Subtract(e.GetPosition(Viewer), DownPoint) * 0.25;

            Viewer.RotationY = RotationVector.Y + Offset.X;
            Viewer.RotationX = RotationVector.X - Offset.Y;
        }

        void Viewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DownPoint = e.GetPosition(Viewer);
            RotationVector.X = Viewer.RotationX;
            RotationVector.Y = Viewer.RotationY;
            Cursor = Cursors.SizeAll;
        }

        private void Viewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Cursor = Cursors.Arrow;
        }

        class Holder
        {
            public Pass pass;
            public int index;
        }

        void DataWindow_Closing(object sender, CancelEventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        void loader_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 100)
            {
                Holder holder = e.UserState as Holder;
                Pass p = holder.pass;
                int currentIndex = holder.index;

                Title = "OculusView, please wait on first time loading: Number of jumps: "+listPanoIds.Count
                    + ", index of jump: " + (currentIndex+1) + ", picture: " + (p.X+1) + "|" + (p.Y+1) + " of "+limitX+"|"+limitY+". Press H for help!";

                

                if (p.X == limitX - 1 && p.Y == limitY - 1)
                {
                    previousJumpBuffStream[previousJumpBuffStream.Count - 1].Add(p.srcStream);

                    string file = "Tiles\\" + zoom + "\\" + listPanoIds[currentIndex] + "\\img_merged.jpg";
                    MergeImages(file);
                }
                else
                {
                    jumpBuffStream[jumpBuffStream.Count - 1].Add(p.srcStream);
                }
                //i.Source = p.src;
                //ResizeImage(i, 128, 128);

                /*(buff[currentIndex].Children[buff[currentIndex].Children.Count - 1] as StackPanel)
                    .Children.Add(i);*/
            }
            else if (e.ProgressPercentage == 0)
            {
                //Title = "OculusView, please wait on first time loading: zooming...";
                int? index = e.UserState as int?;
                jumpBuffStream.Add(new List<Stream>());
            }
        }

        void loader_DoWork(object sender, DoWorkEventArgs e)
        {
            foreach (string panoId in listPanoIds)
            {
                //string panoId = "uyu6kayK95wR4srpxImGmA";
                

                //0, 1
                //1, 2   
                //2, 4
                //3, 7   
                //4, 13  
                //5, 26  

                jumpBuffStream = new List<List<Stream>>();

                string file = "Tiles\\" + zoom + "\\" + panoId + "\\img_merged.jpg";
                string directory = System.IO.Path.GetDirectoryName(file);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(file))
                {
                    Stream s = File.OpenRead(file);
                    buffStream.Add(s);
                }
                else
                {
                    for (int y = 0; y < limitY; y++)
                    {
                        loader.ReportProgress(0, indexBuff);

                        for (int x = 0; x < limitX; x++)
                        {
                            Pass p = new Pass();
                            Debug.WriteLine(DateTime.Now);
                            //finds the necessary images, also saves stream to p.scrStream
                            ImageSource src = Get("http://maps.google.com/cbk?output=tile&panoid=" + panoId + "&zoom=" + zoom + "&x=" + x + "&y=" + y, out p);

                            if (y == limitY - 1)
                                //crop the black part of the bottom pictures, saves space and optimizes loading
                                src = cropImage(src, zoom == 2 ? cropHeightZoom2 : cropHeigthZoom3);

                           

                            p.Y = y;
                            p.X = x;

                            Holder holder = new Holder();
                            holder.index = indexBuff;
                            holder.pass = p;

                            if (p.X == limitX - 1 && p.Y == limitY - 1)
                                previousJumpBuffStream = new List<List<Stream>>(jumpBuffStream);

                            loader.ReportProgress(100, holder);
                        }
                    }
                }
                

                indexBuff++;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                
            }
        }

        public void MergeImages(string fl)
        {
            Canvas canvas = new Canvas();

            if (zoom == 3)
            {
                canvas.Width = 512 * 6.5;
                canvas.Height = 512 * 3.25;
            }
            else if (zoom == 2)
            {
                canvas.Width = 512 * 3.25;
                canvas.Height = 512 * 1.625;
            }

            canvas.Measure(new Size((int)canvas.Width, (int)canvas.Height));
            canvas.Arrange(new Rect(new Size((int)canvas.Width, (int)canvas.Height)));
            int Height = ((int)(canvas.ActualHeight));
            int Width = ((int)(canvas.ActualWidth));

            StackPanel mainPanel = new StackPanel();
            mainPanel.Orientation = Orientation.Vertical;

            for (int j = 0; j < limitY; j++)
            {
                StackPanel ph = new StackPanel();
                ph.Orientation = Orientation.Horizontal;
                for (int k = 0; k < limitX; k++)
                {
                    Image stackImg = new Image();
                    stackImg.Source = FromStream(previousJumpBuffStream[j][k]);
                    ph.Children.Add(stackImg);
                }
                mainPanel.Children.Add(ph);
            }

            mainPanel.UpdateLayout();
            canvas.Children.Add(mainPanel);

            canvas.UpdateLayout();

            RenderTargetBitmap _RenderTargetBitmap = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
            _RenderTargetBitmap.Render(mainPanel);

            ImageSource mergedImages = _RenderTargetBitmap;

            SaveImg(mergedImages, fl);

            Stream s = File.OpenRead(fl);
            buffStream.Add(s);
        }

        public static ImageSource cropImage(ImageSource originalImage, int height)
        {
            BitmapSource myBitmapSource = originalImage as BitmapSource;

            CroppedBitmap cb = new CroppedBitmap(
               myBitmapSource,
               new Int32Rect(0, 0, (int)originalImage.Width, height));       //select region rect

            return cb as ImageSource;
        }

        public static void SaveImg(ImageSource src, string file)
        {
            using (Stream s = File.OpenWrite(file))
            {
                JpegBitmapEncoder e = new JpegBitmapEncoder();
                e.Frames.Add(BitmapFrame.Create(src as BitmapSource));
                e.Save(s);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            loader.RunWorkerAsync();
        }

        public static Stream CopyStream(Stream inputStream)
        {
            const int readSize = 256;
            byte[] buffer = new byte[readSize];
            MemoryStream ms = new MemoryStream();

            using (inputStream)
            {
                int count = inputStream.Read(buffer, 0, readSize);
                while (count > 0)
                {
                    ms.Write(buffer, 0, count);
                    count = inputStream.Read(buffer, 0, readSize);
                }
            }
            buffer = null;
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static ImageSource FromStream(Stream stream)
        {
            ImageSource ret = null;
            if (stream != null)
            {
                {
                    // try jpeg decoder
                    try
                    {
                        JpegBitmapDecoder bitmapDecoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        ImageSource m = bitmapDecoder.Frames[0];
                        stream.Seek(0, SeekOrigin.Begin);
                        if (m != null)
                        {
                            ret = m;
                        }
                    }
                    catch
                    {
                        ret = null;
                    }

                    // try png decoder
                    if (ret == null)
                    {
                        try
                        {
                            stream.Seek(0, SeekOrigin.Begin);

                            PngBitmapDecoder bitmapDecoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            ImageSource m = bitmapDecoder.Frames[0];

                            if (m != null)
                            {
                                ret = m;
                            }
                        }
                        catch
                        {
                            ret = null;
                        }
                    }
                }
            }
            return ret;
        }

        public static ImageSource Get(string url, out Pass p)
        {
            p = new Pass();

            ImageSource ret = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.ServicePoint.ConnectionLimit = 50;
                request.Proxy = WebRequest.DefaultWebProxy;

                request.UserAgent = "Opera/9.62 (Windows NT 5.1; U; en) Presto/2.1.1";
                request.Timeout = 10 * 1000;
                request.ReadWriteTimeout = request.Timeout * 6;
                request.Referer = string.Format("http://maps.{0}/", GMap.NET.MapProviders.GoogleMapProvider.Instance.Server);
                request.KeepAlive = true;


                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    Stream responseStream = CopyStream(response.GetResponseStream());
                    
                    p.srcStream = responseStream;
                    ret = FromStream(responseStream);
                }
            }
            catch (Exception)
            {
                ret = null;
            }
            return ret;
        }
    }

    public class Pass
    {
        //public ImageSource src;
        public Stream srcStream;
        public int Y;
        public int X;
    }
}
