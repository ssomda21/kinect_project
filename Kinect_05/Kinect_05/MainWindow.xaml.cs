using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;

using Microsoft.Kinect;

namespace Kinect_05
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {

        #region Member Variables
        public Point p1;
        public Point p2;
        public Point p3;
        public Point p4;
        public int flag1 = 0;
        public int flag2 = 0;
        public double waistline;
        private KinectSensor _KinectDevice;
        private WriteableBitmap _ColorImageBitmap;
        private Int32Rect _ColorImageBitmapRect;
        private int _ColorImageStride;
        private byte[] _ColorImagePixelData;
        private WriteableBitmap _DepthImage;
        private Int32Rect _DepthImageRect;
        //private int DepthImageStride;
        private short[] _DepthPixelData;
        private int _DepthImageStride;
        private int _TotalFrames;
        private DateTime _StartFrameTime;
        private DepthImageFrame _LastDepthFrame;
        private static readonly double HorizontalTanA = Math.Tan(28.5 * Math.PI / 180);
        private const int LoDepthThreshold = 800;
        private const int HiDepthThreshold = 2000;
        private readonly Brush[] _SkeletonBrushes;
        private Skeleton[] _FrameSkeletons;
        public int gray;
        public byte[] enhPixelData;
        #endregion Member Variables

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();

            _SkeletonBrushes = new Brush[] { Brushes.Aqua, Brushes.Crimson, Brushes.Indigo, Brushes.DodgerBlue, Brushes.Purple, Brushes.Pink };

            KinectSensor.KinectSensors.StatusChanged += KinectSensor_StatusChanged;
            this.KinectDevice = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);

            DepthImage.MouseLeftButtonUp += DepthImage_MouseLeftButtonUp;
            DepthImage.MouseRightButtonUp += DepthImage_MouseRightButtonUp;

        }
        #endregion Constructor

        #region Methods
        private void KinectSensor_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Initializing:
                case KinectStatus.Connected:
                case KinectStatus.NotPowered:
                case KinectStatus.NotReady:
                case KinectStatus.DeviceNotGenuine:
                    this.KinectDevice = e.Sensor;
                    break;
                case KinectStatus.Disconnected:
                    this.KinectDevice = null;
                    break;
                default:
                    break;
            }
        }

        private void KinectDevice_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            if (this._LastDepthFrame != null)
            {
                this._LastDepthFrame.Dispose();
                this._LastDepthFrame = null;
            }

            this._LastDepthFrame = e.OpenDepthImageFrame();

            if (this._LastDepthFrame != null)
            {
                this._LastDepthFrame.CopyPixelDataTo(this._DepthPixelData);
            }

            using (DepthImageFrame frame = e.OpenDepthImageFrame())
            {
                if (frame != null)
                {
                    frame.CopyPixelDataTo(this._DepthPixelData);
                    CreateBetterShadesOfGray(frame, this._DepthPixelData);
                    // CalculatePlayerSize(frame, this._DepthPixelData);
                    //CalculateWaistline(frame, this._DepthPixelData);
                    //pixels(frame, this._DepthPixelData);
                    //CreateDepthHistogram(frame, this._DepthPixelData);
                }
            }

            FramesPerSecondElement.Text = string.Format("{0:0} fps", (this._TotalFrames++ / DateTime.Now.Subtract(this._StartFrameTime).TotalSeconds));
        }

        private void KinectDevice_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame frame = e.OpenColorImageFrame())
            {
                if (frame != null)
                {
                    frame.CopyPixelDataTo(this._ColorImagePixelData);
                    this.ColorImage.Source = this._ColorImageBitmap;
                    this._ColorImageBitmap.WritePixels(this._ColorImageBitmapRect, this._ColorImagePixelData, this._ColorImageStride, 0);

                }
            }
        }

        private void KinetDevice_SkeletonFrameReady(object sendor, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame != null)
                {
                    Polyline figure;
                    Brush userBrush;
                    Skeleton skeleton;

                    SkeletonImage.Children.Clear();
                    frame.CopySkeletonDataTo(this._FrameSkeletons);

                    //Skeleton[] dataSet2 = new Skeleton[this._FrameSkeletons.Length];
                    //.CopySkeletonDataTo(dataSet2);
                    Skeleton skeleton2 = GetPrimarySkeleton(this._FrameSkeletons);

                    for (int i = 0; i < this._FrameSkeletons.Length; i++)
                    {
                        skeleton = this._FrameSkeletons[i];

                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            userBrush = this._SkeletonBrushes[i % this._SkeletonBrushes.Length];

                            //Draw head and torso
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.Head, JointType.ShoulderCenter, JointType.ShoulderLeft, JointType.Spine,
                                                                                JointType.ShoulderRight, JointType.ShoulderCenter, JointType.HipCenter
                                                                                });
                            SkeletonImage.Children.Add(figure);

                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipLeft, JointType.HipRight });
                            SkeletonImage.Children.Add(figure);

                            //Draw left leg
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipCenter, JointType.HipLeft, JointType.KneeLeft, JointType.AnkleLeft, JointType.FootLeft });
                            SkeletonImage.Children.Add(figure);

                            //Draw right leg
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipCenter, JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight });
                            SkeletonImage.Children.Add(figure);

                            //Draw left arm
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.ShoulderLeft, JointType.ElbowLeft, JointType.WristLeft, JointType.HandLeft });
                            SkeletonImage.Children.Add(figure);

                            //Draw right arm
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight });
                            SkeletonImage.Children.Add(figure);
                        }
                    }

                    Joint primaryWaist = GetPrimaryJoint(skeleton2, "waist");
                    TrackJoints(primaryWaist, 0);

                    Joint primaryKneeLeft = GetPrimaryJoint(skeleton2, "leftknee");
                    TrackJoints(primaryKneeLeft, 1);
                }
            }
        }

        private Polyline CreateFigure(Skeleton skeleton, Brush brush, JointType[] joints)
        {
            Polyline figure = new Polyline();

            figure.StrokeThickness = 8;
            figure.Stroke = brush;

            for (int i = 0; i < joints.Length; i++)
            {
                figure.Points.Add(GetJointPoint(skeleton.Joints[joints[i]]));
            }

            return figure;
        }


        private Point GetJointPoint(Joint joint)
        {
            DepthImagePoint point = this.KinectDevice.MapSkeletonPointToDepth(joint.Position, this.KinectDevice.DepthStream.Format);
            point.X *= (int)this.SkeletonImage.ActualWidth / KinectDevice.DepthStream.FrameWidth;
            point.Y *= (int)this.SkeletonImage.ActualHeight / KinectDevice.DepthStream.FrameHeight;

            return new Point(point.X, point.Y);
        }


        private void CreateBetterShadesOfGray(DepthImageFrame depthFrame, short[] pixelData)
        {
            int depth;
            int bytesPerPixel = 4;
            enhPixelData = new byte[depthFrame.Width * depthFrame.Height * bytesPerPixel];
            int playerIndex; // additional
            byte[] rgb = new byte[3];
            double hue;

            for (int i = 0, j = 0; i < pixelData.Length; i++, j += bytesPerPixel)
            {
                depth = pixelData[i] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                playerIndex = pixelData[i] & DepthImageFrame.PlayerIndexBitmask; // add

                if (playerIndex != 0)
                {
                    gray = 0x00;
                }
                else
                {
                    if (depth < LoDepthThreshold || depth > HiDepthThreshold)
                    {
                        gray = 0xFF;
                    }
                    else
                    {
                        gray = 255 - (255 * depth / 0xFFF);
                    }
                }

                enhPixelData[j] = (byte)gray;
                enhPixelData[j + 1] = (byte)gray;
                enhPixelData[j + 2] = (byte)gray;
                /*
                if ( p1.X != 0  && p1.Y != 0)
                {
                    hue = 0;
                    ConvertHslToRgb(hue, 100, 100, rgb);
                    int index1 = (int)p1.X + (int)p1.Y * 640;
                    enhPixelData[index1 * 4] = rgb[0];
                    enhPixelData[index1 * 4 + 1] = rgb[0];
                    enhPixelData[index1 * 4 + 1] = rgb[0];
                }

                if (p2.X != 0 && p2.Y != 0)
                {
                    hue = 0;
                    ConvertHslToRgb(hue, 100, 100, rgb);
                    int index2 = (int)p2.X + (int)p2.Y * 640;
                    enhPixelData[index2 * 4] = rgb[2];
                    enhPixelData[index2 * 4 + 1] = rgb[2];
                    enhPixelData[index2 * 4 + 1] = rgb[2];
                }
                */

            }

          

            this._DepthImage.WritePixels(this._DepthImageRect, enhPixelData, this._DepthImageStride, 0);
            //pickPixels(gray);
        }

        public void ConvertHslToRgb(double hue, double saturation, double lightness, byte[] rgb)
        {
            double red = 0.0;
            double green = 0.0;
            double blue = 0.0;
            hue = hue % 360.0;
            saturation = saturation / 100.0;
            lightness = lightness / 100.0;

            if (saturation == 0.0)
            {
                red = lightness;
                green = lightness;
                blue = lightness;
            }
            else
            {
                double huePrime = hue / 60.0;
                int x = (int)huePrime;
                double xPrime = huePrime - (double)x;
                double L0 = lightness * (1.0 - saturation);
                double L1 = lightness * (1.0 - (saturation * xPrime));
                double L2 = lightness * (1.0 - (saturation * (1.0 - xPrime)));

                switch (x)
                {
                    case 0:
                        red = lightness;
                        green = L2;
                        blue = L0;
                        break;
                    case 1:
                        red = L1;
                        green = lightness;
                        blue = L0;
                        break;
                    case 2:
                        red = L0;
                        green = lightness;
                        blue = L2;
                        break;
                    case 3:
                        red = L0;
                        green = L1;
                        blue = lightness;
                        break;
                    case 4:
                        red = L2;
                        green = L0;
                        blue = lightness;
                        break;
                    case 5:
                        red = lightness;
                        green = L0;
                        blue = L1;
                        break;
                }
            }

            rgb[0] = (byte)(255.0 * red);
            rgb[1] = (byte)(255.0 * green);
            rgb[2] = (byte)(255.0 * blue);
        }


        private static Skeleton GetPrimarySkeleton(Skeleton[] skeletons)
        {
            Skeleton skeleton = null;
            if (skeletons != null)
            {
                for (int i = 0; i < skeletons.Length; i++)
                {
                    if (skeletons[i].TrackingState == SkeletonTrackingState.Tracked)
                    {
                        if (skeleton == null) skeleton = skeletons[i];
                        else { if (skeleton.Position.Z > skeletons[i].Position.Z) skeleton = skeletons[i]; }
                    }
                }
            }
            return skeleton;
        }

        private static Joint GetPrimaryJoint(Skeleton skeleton, string jointname)
        {
            Joint primaryJoint = new Joint();

            if (skeleton != null)
            {
                switch (jointname)
                {
                    case "waist" :
                        primaryJoint = skeleton.Joints[JointType.HipCenter];
                        break;
                    case "leftknee":
                        primaryJoint = skeleton.Joints[JointType.KneeLeft];
                        break;
                    case "rightknee":
                        primaryJoint = skeleton.Joints[JointType.KneeRight];
                        break;
                }
            }

            return primaryJoint;
        }

        private void TrackJoints(Joint joint, int sign)
        {
            if (joint.TrackingState == JointTrackingState.NotTracked) test.Text = string.Format("수동으로 클릭해 주세요.");
            else
            {
                test.Text = string.Format("자동 측정이 진행 중입니다.");

                //SkeletonPoint point = waist.Position;
                DepthImagePoint point = KinectDevice.MapSkeletonPointToDepth(joint.Position, DepthImageFormat.Resolution640x480Fps30);

                point.X = (int)(point.X * SkeletonImage.ActualWidth / this.KinectDevice.DepthStream.FrameWidth );
                point.Y = (int)(point.Y * SkeletonImage.ActualHeight / this.KinectDevice.DepthStream.FrameHeight );

                Point pt1 = new Point();
                Point pt2 = new Point();

                if (_LastDepthFrame != null)
                {
                    pt1.Y = point.Y; pt2.Y = point.Y;
                    int startPoint = (int)(point.X * 0.5) + (point.Y - 1) * _LastDepthFrame.Width;
                    int midPoint = (int)point.X + (int)(point.Y * _LastDepthFrame.Width);
                    int lastPoint = (int)(point.X * 1.5) + (int)((point.Y + 1) * this._LastDepthFrame.Width);

                    for (int i = startPoint, k = startPoint * 4; i < lastPoint; i++, k += 4)
                    {
                        if (enhPixelData[k] == 0x00)
                        {
                            pt1.X = i - 640 * pt1.Y + 640;
                            break;
                        }
                    }

                    for (int i = midPoint, k = midPoint * 4; i < lastPoint; i++, k += 4)
                    {                       
                        if (enhPixelData[k] > 0x00)
                        {
                            pt2.X = i - 640 * pt2.Y;
                            break;
                        }

                    }

                    if ( sign == 0 )
                    {
                        CaculateWaistCircumference(pt1, pt2);
                        if (flag1 == 0) ToFile(pt1, pt2);
                    }
                    else if (sign == 1)
                    {
                        CalculateHipCircumference(pt1, pt2);
                    }
                    
                }                               
            }            
        }

        
        private void ToFile(Point p1, Point p2)
        {
            int index1 = (int)p1.X + (int)p1.Y * 640;
            int index2 = (int)p2.X + (int)p2.Y * 640;
            int length = (int)(p2.X - p1.X);
            byte[] arr = new byte[length];
            byte[] arr2 = new byte[length];
            byte[] arr3 = new byte[length];

            FileStream fs = new FileStream("line1.bin", FileMode.Create);
            for (int id = index1, i = 0; i < length; id++, i++)
            {
                float depth = _DepthPixelData[id] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                arr[i] = (byte) depth;                      
            }
            fs.Write(arr, 0, arr.Length);
            fs.Close();

            FileStream fs2 = new FileStream("line2.bin", FileMode.Create);
            for (int id = index1+640, i = 0; i < length; id++, i++)
            {
                float depth = _DepthPixelData[id] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                arr2[i] = (byte)depth;
            }
            fs2.Write(arr2, 0, arr.Length);
            fs2.Close();

            FileStream fs3 = new FileStream("line3.bin", FileMode.Create);
            for (int id = index1 + 1280, i = 0; i < length; id++, i++)
            {
                float depth = _DepthPixelData[id] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                arr3[i] = (byte)depth;
            }
            fs3.Write(arr3, 0, arr.Length);
            fs3.Close();



        }    

        private void DepthImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (flag1 == 0)
            {
                p1 = e.GetPosition(DepthImage);
                Pixel_Left.Text = string.Format("{0}", p1);
                flag1 = 1;
            }
            else
            {
                p3 = e.GetPosition(DepthImage);
                Pixel_Left2.Text = string.Format("{0}", p3);
                flag1 = 0;
            }

        }

        private void DepthImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (flag2 == 0)
            {
                p2 = e.GetPosition(DepthImage);
                Pixel_Right.Text = string.Format("{0}", p2);
                CaculateWaistCircumference(p1, p2);
                flag2 = 1;
            }
            else
            {
                p4 = e.GetPosition(DepthImage);
                Pixel_Right2.Text = string.Format("{0}", p4);
                CalculateHipCircumference(p3, p4);
                flag2 = 0;
            }

        }

        private void CaculateWaistCircumference(Point p1, Point p2)
        {
            // Calulate Waist Size
            int waist_width =  (int)p2.X - (int)p1.X ;

            if (waist_width < 0) waist_width = -waist_width;
            //int waist_width = (int)Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            int[] array = new int[1000];
            waistline = 0;
            // waist_width = -waist_width;
            int Min_depth = 0;
            double MilliPerPixel = 0;
            //int count = 0;
            w_width.Text = string.Format("{0}pixels", waist_width);

            //double opposite = 
            int j = 0;
            for (int i = 1; i < waist_width; i++)
            {
                int pixelIndex = (int)(p1.X + ((int)p1.Y * this._LastDepthFrame.Width));
                int depth = this._DepthPixelData[pixelIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                array[i] = depth;
                p1.X++;
                if (i == 1) Min_depth = array[1];
                else Min_depth = Math.Min(array[i], array[i - 1]);

                MilliPerPixel = 2 * (double)Min_depth * HorizontalTanA / 680;

                //if (i != 1) waistline = array[i-1] - array[i];
                j = i;
                //waistline += Math.Sqrt(Math.Pow(MilliPerPixel,2) + 1.69;
                if (i != 1) waistline += Math.Sqrt(Math.Pow(MilliPerPixel, 2) + Math.Pow(array[i - 1] - array[i], 2));
                //count++;
                //test.Text = string.Format("{0} {1} {2} {3} {4} / {5:##.##}mm", array[0], array[1], array[2], array[3], array[4], MilliPerPixel);
                //ttt.Text = string.Format("{0} {1} {2} {3} {4} {5} {6} {7}", array[5], array[6], array[7], array[8], array[9], array[10], array[11], array[12]);
            }
            Waistline.Text = string.Format("{0: #.###}cm = {1} - {2}", waistline / 10, j, j + 1);
            waist_histograms(array, waist_width);
        }

        private void CalculateHipCircumference(Point p3, Point p4)
        {
            int hip_width = (int)p4.X - (int)p3.X;
            int[] array2 = new int[641];
            double hip_circum = 0;
            int Min_depth = 0;
            double MilliPerPixel = 0;
            h_width.Text = string.Format("{0}pixels", hip_width);

            // j = 0;
            for (int i = 1; i < hip_width; i++)
            {
                int pixelIndex = (int)(p3.X + ((int)p3.Y * this._LastDepthFrame.Width));
                int depth = this._DepthPixelData[pixelIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                array2[i] = depth;
                p3.X++;
                if (i == 1) Min_depth = array2[1];
                else Min_depth = Math.Min(array2[i], array2[i - 1]);

                MilliPerPixel = 2 * (double)Min_depth * HorizontalTanA / 680;

                //j = i;
                if (i != 1) hip_circum += Math.Sqrt(Math.Pow(MilliPerPixel, 2) + Math.Pow(array2[i - 1] - array2[i], 2));
                //test.Text = string.Format("{0} {1} {2} {3} {4} / {5:##.##}mm", array2[0], array2[1], array2[2], array2[3], array2[4], MilliPerPixel);
            }
            Hip_circum.Text = string.Format("{0: #.###}cm", hip_circum / 10);
            WaistHipRatio(waistline, hip_circum);
        }

        private void WaistHipRatio(double waistline, double hip_circum)
        {
            double WHR = waistline / hip_circum;
            WHR_t.Text = string.Format("{0: #.##}", WHR);

        }

        private void waist_histograms(int[] array, int waist_width)
        {
            DepthHistogram.Children.Clear();
            double chartBarWidth = DepthHistogram.ActualWidth / waist_width;
            int maxValue = 0;
            int minValue = 8000;

            for (int i = 0; i < waist_width; i++) maxValue = Math.Max(maxValue, array[i]);
            for (int i = 0; i < waist_width; i++) minValue = Math.Min(minValue, array[i]);
            int[] hist_array = new int[waist_width];

            for (int i = 0; i < waist_width; i++)
            {
                hist_array[i] = array[i] - minValue;
                if (array[i] > 0)
                {
                    Rectangle r = new Rectangle();
                    r.Fill = Brushes.Black;
                    r.Width = 2;
                    r.Height = 300 - DepthHistogram.ActualHeight * (hist_array[i] * 0.5) / maxValue;
                    r.Margin = new Thickness(1, 0, 1, 0);
                    r.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                    DepthHistogram.Children.Add(r);
                }
            }

        }

        #endregion Methods

        #region Properties
        public KinectSensor KinectDevice
        {

            get { return this._KinectDevice; }
            set
            {
                if (this._KinectDevice != value)
                {
                    //Uninitialize
                    if (this._KinectDevice != null)
                    {
                        this._KinectDevice.Stop();
                        this._KinectDevice.ColorFrameReady -= KinectDevice_ColorFrameReady;
                        this._KinectDevice.DepthFrameReady -= KinectDevice_DepthFrameReady;
                        this._KinectDevice.SkeletonFrameReady -= KinetDevice_SkeletonFrameReady;
                        this._KinectDevice.ColorStream.Disable();
                        this._KinectDevice.DepthStream.Disable();
                        this._KinectDevice.SkeletonStream.Disable();
                    }

                    this._KinectDevice = value;

                    //Initialize
                    if (this._KinectDevice != null)
                    {
                        if (this._KinectDevice.Status == KinectStatus.Connected)
                        {
                            this._KinectDevice.SkeletonStream.Enable();
                            this._KinectDevice.DepthStream.Enable();
                            this._KinectDevice.ColorStream.Enable();

                            /* Initialize Color */
                            ColorImageStream colorStream = this._KinectDevice.ColorStream;
                            colorStream.Enable();
                            this._ColorImageBitmap = new WriteableBitmap(colorStream.FrameWidth, colorStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null);
                            this._ColorImageBitmapRect = new Int32Rect(0, 0, colorStream.FrameWidth, colorStream.FrameHeight);
                            this._ColorImageStride = colorStream.FrameWidth * colorStream.FrameBytesPerPixel;
                            this.ColorImage.Source = this._ColorImageBitmap;
                            this._ColorImagePixelData = new byte[colorStream.FramePixelDataLength];

                            /* Initialize Depth */
                            DepthImageStream depthStream = this._KinectDevice.DepthStream;
                            this._DepthImage = new WriteableBitmap(depthStream.FrameWidth, depthStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null);
                            this._DepthImageRect = new Int32Rect(0, 0, (int)Math.Ceiling(this._DepthImage.Width), (int)Math.Ceiling(this._DepthImage.Height));
                            this._DepthImageStride = depthStream.FrameWidth * 4;
                            this._DepthPixelData = new short[depthStream.FramePixelDataLength];
                            this.DepthImage.Source = this._DepthImage;

                            /* Initialize Skeleton */
                            this._FrameSkeletons = new Skeleton[this._KinectDevice.SkeletonStream.FrameSkeletonArrayLength];

                            this.KinectDevice.SkeletonFrameReady += KinetDevice_SkeletonFrameReady;
                            this._KinectDevice.ColorFrameReady += KinectDevice_ColorFrameReady;
                            this._KinectDevice.DepthFrameReady += KinectDevice_DepthFrameReady;
                            this._KinectDevice.Start();

                            this._StartFrameTime = DateTime.Now;
                        }
                    }
                }
            }
        }
        #endregion Properties
    }
}
