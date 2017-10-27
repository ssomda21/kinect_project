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

using Microsoft.Kinect;

using ggeut;

namespace ggeut
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Member Variables
        private KinectSensor _KinectDevice;
        private WriteableBitmap _DepthImage;
        private Int32Rect _DepthImageRect;
        private int _DepthImageStride;
        private short[] _DepthPixelData;
        private int _TotalFrames;
        private DateTime _StartFrameTime;
        private DepthImageFrame _LastDepthFrame;
        private readonly Brush[] _SkeletonBrushes;
        private Skeleton[] _FrameSkeletons;

        private int saveFlag = 0;
        private snap saveSnap;
        private List<double> fronts;
        private List<double> rears;

        private IndexDepth[] datas = new IndexDepth[307200];
        private int gMinDepth = 9999999;
        private static readonly double HorizontalTanA = Math.Tan(28.5 * Math.PI / 180);
        private static readonly double VerticalTanA = Math.Abs(Math.Tan(21.5 * Math.PI / 180));
        private double bodyHeight;
        private double bodyWidth;
        #endregion Member Variables

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();

            _SkeletonBrushes = new Brush[] { Brushes.Aqua, Brushes.Crimson, Brushes.Indigo, Brushes.DodgerBlue, Brushes.Purple, Brushes.Pink };

            KinectSensor.KinectSensors.StatusChanged += KinectSensor_StatusChanged;
            this.KinectDevice = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
            //saveSnap = snap.getInstance(480, 640);
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
                    getHeight(frame, this._DepthPixelData);
                }
            }
        }

        private void CreateBetterShadesOfGray(DepthImageFrame depthFrame, short[] pixelData)
        {
            int depth;
            int bytesPerPixel = 4;
            byte[] enhPixelData = new byte[depthFrame.Width * depthFrame.Height * bytesPerPixel];
            int playerIndex; // additional
            int gray;
            const int LoDepthThreshold = 800;
            const int HiDepthThreshold = 4000;

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

                // 맵 재구성
                datas[i] = new IndexDepth();
                datas[i].depth = depth;
                datas[i].index = playerIndex;
                
                enhPixelData[j] = (byte)gray;
                enhPixelData[j + 1] = (byte)gray;
                enhPixelData[j + 2] = (byte)gray;
            };

            this._DepthImage.WritePixels(this._DepthImageRect, enhPixelData, this._DepthImageStride, 0);
            //pickPixels(gray);
        }

        // 스켈레톤 프레임
        private void KinectDevice_SkeletonFrameReady(object sendor, SkeletonFrameReadyEventArgs e)
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

                    Skeleton skeleton2 = GetPrimarySkeleton(this._FrameSkeletons);

                    // 스켈레톤 그리는 부분
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

                    // 이 아래부터 스켈레톤 프레임이나 조인트 사용하는 함수 호출

                    Joint[] joints = new Joint[20];

                    for (int i = 0; i < 20; i++)
                    {
                        joints[i] = GetPrimaryJoint(skeleton2, i);
                    }

                    if(checkJoints(joints)) // 사람의 모든 관절이 보일 때, 계산 진행
                    {
                        Btn1.IsEnabled = true;

                        Text1.Text = "계산 중입니다.";

                        // 여기서 모든 연산을 한다.
                        CalData(640, 480, GetJointPoint(joints[0]));

                    }
                    else
                    {
                        Btn1.IsEnabled = false;
                        Text1.Text = "사람의 모든 관절을 찾을 수 없습니다.\n올바른 자세를 취해주세요.";
                    }
                    //saveJoints(joints);
                }
            }
        }

        private bool checkJoints(Joint[] joints)
        {
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].TrackingState == JointTrackingState.NotTracked) {
                    return false;
                }
            }

            return true;
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

        private Joint GetPrimaryJoint(Skeleton skeleton, int jnum)
        {
            Joint primaryJoint = new Joint();

            if (skeleton != null)
            {
                primaryJoint = skeleton.Joints[(JointType)jnum];
            }

            return primaryJoint;
        }

        private void saveJoints(Joint[] joints)
        {
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].TrackingState == JointTrackingState.NotTracked) Text1.Text = "Not Tracked";
                else
                {
                    DepthImagePoint point = KinectDevice.MapSkeletonPointToDepth(joints[i].Position, DepthImageFormat.Resolution640x480Fps30);

                    point.X = (int)(point.X * SkeletonImage.ActualWidth / this.KinectDevice.DepthStream.FrameWidth);
                    point.Y = (int)(point.Y * SkeletonImage.ActualHeight / this.KinectDevice.DepthStream.FrameHeight);

                    Text1.Text = string.Format("{0}: {1}, {2}", (JointType)i, point.X, point.Y);
                }
            }
        }

        private void Btn1_Click(object sender, RoutedEventArgs e)
        {
            if(saveFlag == 0)
            {
                saveFlag = 1;

                fronts = new List<double>();

                Btn1.Content = "정면 데이터 추출 중";
                Btn1.IsEnabled = false;
            } else if(saveFlag == 1)
            {
                saveFlag = 2;

                rears = new List<double>();

                Btn1.Content = "후면 데이터 추출 중";
                Btn1.IsEnabled = false;
            } else
            {
                // 결과 값 계산 하기
                // 결과 저장
                //CalcResult();

                saveFlag = 0;

                Btn1.Content = "데이터 추출 (정면)";
                Btn1.IsEnabled = false;
            }
        }

        private void CalcResult()
        {
            fronts.Sort();
            rears.Sort();

            //Text4.Text = string.Format("{0}, {1}, {2}", fronts.ElementAt(0), fronts.ElementAt(49), fronts.ElementAt(99));
            //Text5.Text = string.Format("{0}, {1}, {2}", rears.ElementAt(0), rears.ElementAt(49), rears.ElementAt(99));

            String front_str = fronts.ElementAt(0) + "";
            String rear_str = rears.ElementAt(0) + "";

            for(int i = 1; i < 100; i++)
            {
                front_str += "," + fronts.ElementAt(i);
                rear_str += "," + rears.ElementAt(i);
            }

            System.IO.File.AppendAllText("results.txt", front_str + "\n", Encoding.UTF8);
            System.IO.File.AppendAllText("results.txt", rear_str + "\n\n\n", Encoding.UTF8);
        }

        private Point[] GetRect(int width, int height, int index)
        {
            Point[] results = new Point[2];
            results[0].X = 1000.0; results[0].Y = 1000.0;   // 왼쪽 상단
            results[1].X = 0.0; results[1].Y = 0.0;         // 오른쪽 하단

            for(int row = 0; row < height; row++)
            {
                for(int col = 0; col < width; col++)
                {
                    if(datas[(row * width) + col].index == index)
                    {
                        if (results[0].X > col) results[0].X = col;
                        if (results[0].Y > row) results[0].Y = row;

                        if (results[1].X < col) results[1].X = col;
                        if (results[1].Y < row) results[1].Y = row;
                    }
                }
            }

            return results;
        }

        private void CalData(int width, int height, Point center)
        {
            int now_index = datas[Convert.ToInt32((center.Y * width) + center.X)].index;
            Point[] data = GetRect(width, height, now_index);

            double temp = data[1].X - data[0].X;

            if(temp > 300 && temp < 1000)
            {
                if(saveFlag == 1)
                {
                    if (fronts.Count >= 100)
                    {
                        Btn1.IsEnabled = true;
                        Btn1.Content = "데이터 추출 (후면)";
                    } else
                    {
                        double front = GetWaistMeasurement(center, now_index, width);

                        Text2.Text = string.Format("{0}", front);

                        fronts.Add(front);
                    }
                } else if(saveFlag == 2)
                {
                    if (rears.Count >= 100)
                    {
                        Btn1.IsEnabled = true;
                        Btn1.Content = "결과 값 계산";
                    } else
                    {
                        double rear = GetWaistMeasurement(center, now_index, width);

                        Text3.Text = string.Format("{0}", rear);

                        rears.Add(rear);
                    }
                    // 후면 계산 및 각종 계산
                }
            }
        }
        
        // 필요하면 호출.. 
        private Point FindNIndex(Point center, int index, int width) // 현재 값이 같은 인덱스가 아닌 경우, 인접한 곳 중에 같은 인덱스를 가진 점을 찾아준다.
        {
            Point result = new Point(); result.Y = 0.0;
            int std = Convert.ToInt32(center.Y * width + center.X);

            for (int i = 1; i < 10; i++)
            {
                if(datas[std + i].index == index)
                {
                    result.X = center.X + i;
                    result.Y = center.Y;

                    break;
                } else if (datas[std - i].index == index)
                {
                    result.X = center.X - i;
                    result.Y = center.Y;

                    break;
                }
            }

            return result;
        }

        private double CalcDist(int depth1, int depth2)
        {
            double b = 0;
            double MilliPerPixel = 0;
            double distance = 0;

            b = (double)gMinDepth * HorizontalTanA;
            MilliPerPixel = 2 * b / 640;

            distance = Math.Sqrt(Math.Pow(MilliPerPixel,2) + Math.Pow((depth2 - depth1), 2));

            return distance / 10;
        }

        private double CalcWaistMeasurement(Point center, int index, int width)
        {
            int std = Convert.ToInt32(center.Y * width + center.X);
            double result = 0.0;

            for (int i = 1; i < 1000; i++)
            {
                if ((std + i) % 640 == 0)
                {
                    break;
                } else if (index == datas[std + i].index)
                {
                    result += CalcDist(datas[std + i].depth, datas[std + i - 1].depth);
                } else 
                {
                    break;
                }
            }

            for (int i = 0; i < 1000; i++)
            {
                if ((std - i) % 640 == 0)
                {
                    break;
                } else if (index == datas[std - i].index)
                {
                    result += CalcDist(datas[std - i].depth, datas[std - i + 1].depth);
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private void SetGMinDepth(int index)
        {
            gMinDepth = 9999999;

            for(int i = 0; i < 307200; i++)
            {
                if(index == datas[i].index && gMinDepth > datas[i].depth)
                {
                    gMinDepth = datas[i].depth;
                }
            }
        }

        private double GetWaistMeasurement(Point center, int index, int width)
        {
            SetGMinDepth(index);

            double gMax = CalcWaistMeasurement(center, index, width);
            double temp = 0.0;
            Point temp_point;

            for (int i = 1; i < 5; i++)
            {
                temp_point = new Point(center.X, center.Y + i);

                temp = CalcWaistMeasurement(temp_point, index, width);

                if (temp > gMax) gMax = temp;
            }

            for (int i = 1; i < 5; i++)
            {
                temp_point = new Point(center.X, center.Y - i);

                temp = CalcWaistMeasurement(temp_point, index, width);

                if (temp > gMax) gMax = temp;
            }

            return gMax;
        }


        private void getHeight(DepthImageFrame depthFrame, short[] pixelData)
        {
            int depth;
            int playerIndex;
            int pixelIndex;
            int bytesPerPixel = depthFrame.BytesPerPixel;           
            PlayerDepthData[] players = new PlayerDepthData[6];

            for (int row = 0; row < depthFrame.Height; row++)
            {
                for (int col = 0; col < depthFrame.Width; col++)
                {
                    pixelIndex = col + (row * depthFrame.Width);
                    depth = pixelData[pixelIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                    if (depth != 0)
                    {
                        playerIndex = (pixelData[pixelIndex] & DepthImageFrame.PlayerIndexBitmask);
                        playerIndex -= 1;

                        if (playerIndex > -1)
                        {
                            if (players[playerIndex] == null)
                            {
                                players[playerIndex] = new PlayerDepthData(playerIndex + 1, depthFrame.Width, depthFrame.Height);
                            }
                            players[playerIndex].UpdateData(col, row, depth);
                            bodyHeight = players[playerIndex].RealHeightCenties;
                        }
                    }
                }
            }
            Text6.Text = string.Format("{0}", bodyHeight);
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
                        //this._KinectDevice.ColorFrameReady -= KinectDevice_ColorFrameReady;
                        this._KinectDevice.DepthFrameReady -= KinectDevice_DepthFrameReady;
                        this._KinectDevice.SkeletonFrameReady -= KinectDevice_SkeletonFrameReady;
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
                            //this._KinectDevice.ColorStream.Enable();

                            /* Initialize Color */
                            /*
                            ColorImageStream colorStream = this._KinectDevice.ColorStream;
                            colorStream.Enable();
                            this._ColorImageBitmap = new WriteableBitmap(colorStream.FrameWidth, colorStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null);
                            this._ColorImageBitmapRect = new Int32Rect(0, 0, colorStream.FrameWidth, colorStream.FrameHeight);
                            this._ColorImageStride = colorStream.FrameWidth * colorStream.FrameBytesPerPixel;
                            this.ColorImage.Source = this._ColorImageBitmap;
                            this._ColorImagePixelData = new byte[colorStream.FramePixelDataLength];
                            */

                            /* Initialize Depth */
                            DepthImageStream depthStream = this._KinectDevice.DepthStream;
                            this._DepthImage = new WriteableBitmap(depthStream.FrameWidth, depthStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null);
                            this._DepthImageRect = new Int32Rect(0, 0, (int)Math.Ceiling(this._DepthImage.Width), (int)Math.Ceiling(this._DepthImage.Height));
                            this._DepthImageStride = depthStream.FrameWidth * 4;
                            this._DepthPixelData = new short[depthStream.FramePixelDataLength];
                            this.DepthImage.Source = this._DepthImage;

                            /* Initialize Skeleton */
                            this._FrameSkeletons = new Skeleton[this._KinectDevice.SkeletonStream.FrameSkeletonArrayLength];

                            this.KinectDevice.SkeletonFrameReady += KinectDevice_SkeletonFrameReady;
                            //this._KinectDevice.ColorFrameReady += KinectDevice_ColorFrameReady;
                            this._KinectDevice.DepthFrameReady += KinectDevice_DepthFrameReady;
                            this._KinectDevice.Start();

                            this._StartFrameTime = DateTime.Now;
                        }
                    }
                }
            }
        }

        private class IndexDepth
        {
            public int index;
            public int depth;
        }

        #endregion Properties
             
    }
}
