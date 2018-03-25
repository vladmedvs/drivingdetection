using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.Structure;

namespace face_tracking.cs
{
    public partial class MainForm : Form
    {
        public enum Label
        {
            StatusLabel,
            AlertsLabel
        };

        public PXCMSession Session;
        public volatile bool Register = false;
        public volatile bool Unregister = false;        
        public volatile bool Stopped = false;

        private readonly object m_bitmapLock = new object();
        private readonly FaceTextOrganizer m_faceTextOrganizer;
        private IEnumerable<CheckBox> m_modulesCheckBoxes;
        private IEnumerable<TextBox> m_modulesTextBoxes; 
        private Bitmap m_bitmap;
        private string m_filename;
        private Tuple<PXCMImage.ImageInfo, PXCMRangeF32> m_selectedColorResolution;
        private volatile bool m_closing;
        private static ToolStripMenuItem m_deviceMenuItem;
        private static ToolStripMenuItem m_moduleMenuItem;
        private static readonly int LANDMARK_ALIGNMENT = -3;

        private Bitmap bitmap = null;
        public void DisplayBitmap(Bitmap bmp)
        {
            lock (this)
            {
                if (bitmap != null)
                    bitmap.Dispose();
                bitmap = new Bitmap(bmp);
            }
        }


        public MainForm(PXCMSession session)
        {
            InitializeComponent();
            InitializeCheckboxes();
            InitializeTextBoxes();

            m_faceTextOrganizer = new FaceTextOrganizer();
            m_deviceMenuItem = new ToolStripMenuItem("Device");
            m_moduleMenuItem = new ToolStripMenuItem("Module");
            Session = session;
            CreateResolutionMap();
            PopulateDeviceMenu();
            PopulateModuleMenu();
            PopulateProfileMenu();

            FormClosing += MainForm_FormClosing;
            Panel2.Paint += Panel_Paint;
            
        }

        Point LeftCar;
        Point RightCar;
        double hpx = 0;
        int CarWidth = 150;
        //int HeadPos = 90;
        Image<Bgr, byte> Ekran;
        private void ReDraw()
        {
            DrawPano();
            LeftCar = new Point(Ekran.Width / 4, Ekran.Height / 5);
            RightCar = new Point(3 * Ekran.Width / 4, Ekran.Height / 5);
            double PixelCount = CarWidth / (Ekran.Width / 2.0);
            //double HeadPosY = HeadPos / PixelCount;
            //double HeadPosX = hpx / PixelCount;
            //DrawCar();
            //Point HeadC = new Point((int)(3 * Ekran.Width/8+HeadPosX), (int)(Ekran.Height / 5 + HeadPosY));
            //DrawHead(HeadC);
            //DrawZL(HeadPosY, HeadPosX, HeadC);
            //DrawZR(HeadPosY, HeadPosX, HeadC);
            //Ekran.Draw(new LineSegment2D(new Point((Pointofinterest + 1) * Ekran.Width / 4, Ekran.Height / 10), new Point((Pointofinterest + 2) * Ekran.Width / 4, Ekran.Height / 10)), new Bgr(0, 0, 255), 3);
        }

        //private void DrawZL(double HeadPosY, double HeadPosX, Point HeadC)
        //{
        //    double alphal = 0;
        //    double xl = HeadPosX + Ekran.Width / 8.0 + Ekran.Width / 8.2;
        //    alphal = HeadPosY / xl;
        //    alphal = Math.Atan(alphal);
        //    alphal = alphal / 2;
        //    Point ZerkaloL = new Point((int)(LeftCar.X - (Math.Cos(alphal)) * Ekran.Width / 4.1), (int)(LeftCar.Y + (Math.Sin(alphal)) * Ekran.Width / 4.1));
        //    Ekran.Draw(new LineSegment2D(LeftCar, ZerkaloL), new Bgr(255, 255, 255), 3);
        //    Point ZerkaloLC = new Point((ZerkaloL.X + LeftCar.X) / 2, (ZerkaloL.Y + LeftCar.Y) / 2);
        //    Ekran.Draw(new LineSegment2D(ZerkaloLC, new Point(ZerkaloLC.X, Ekran.Width)), new Bgr(0, 255, 255), 3);
        //    Ekran.Draw(new LineSegment2D(ZerkaloLC, HeadC), new Bgr(0, 255, 255), 3);
        //}
        //private void DrawZR(double HeadPosY, double HeadPosX, Point HeadC)
        //{
        //    double alphal = 0;
        //    double xl = - HeadPosX + 3 * Ekran.Width / 8.0 + Ekran.Width / 8.2;
        //    alphal = HeadPosY / xl;
        //    alphal = Math.Atan(alphal);
        //    alphal = alphal / 2;
        //    Point ZerkaloL = new Point((int)(RightCar.X + (Math.Cos(alphal)) * Ekran.Width / 4.1), (int)(RightCar.Y + (Math.Sin(alphal)) * Ekran.Width / 4.1));
        //    Ekran.Draw(new LineSegment2D(RightCar, ZerkaloL), new Bgr(255, 255, 255), 3);
        //    Point ZerkaloLC = new Point((ZerkaloL.X + RightCar.X) / 2, (ZerkaloL.Y + RightCar.Y) / 2);
        //    Ekran.Draw(new LineSegment2D(ZerkaloLC, new Point(ZerkaloLC.X, Ekran.Width)), new Bgr(0, 255, 255), 3);
        //    Ekran.Draw(new LineSegment2D(ZerkaloLC, HeadC), new Bgr(0, 255, 255), 3);
        //}

        //private void DrawHead(Point HeadC)
        //{
        //    CircleF Head = new CircleF(HeadC, Ekran.Width / 16);
        //    Ekran.Draw(Head, new Bgr(255, 255, 255), 2);
        //}



        private void DrawCar()
        {
            Ekran.Draw(new LineSegment2D(LeftCar, RightCar), new Bgr(255, 255, 255), 3);
            Ekran.Draw(new LineSegment2D(LeftCar, new Point(LeftCar.X, Ekran.Height)), new Bgr(255, 255, 255), 3);
            Ekran.Draw(new LineSegment2D(RightCar, new Point(RightCar.X, Ekran.Height)), new Bgr(255, 255, 255), 3);
        }
        Bgr panoColor = new Bgr(255, 0, 0);
        private void DrawPano()
        {
            Ekran = new Image<Bgr, byte>(VideoWindow.Size);
            Ekran.SetValue(panoColor);
            VideoWindow.Image = Ekran;
        }


        private void InitializeTextBoxes()
        {
            m_modulesTextBoxes = new List<TextBox>
            {
                NumDetectionText,
                NumLandmarksText,
                NumPoseText,
                NumExpressionsText,
            };

            foreach (var textBox in m_modulesTextBoxes)
            {
                textBox.Text = @"4";
            }
        }
        private void InitializeCheckboxes()
        {
            m_modulesCheckBoxes = new List<CheckBox>
            {
                Detection,
                Landmarks,
                Pose,
                Expressions,
                Recognition
            };

            foreach (var checkBox in m_modulesCheckBoxes)
            {
                checkBox.Enabled = true;
                checkBox.Checked = true;
            }
        }

        public Dictionary<string, PXCMCapture.DeviceInfo> Devices { get; set; }
        public Dictionary<string, IEnumerable<Tuple<PXCMImage.ImageInfo, PXCMRangeF32>>> ColorResolutions { get; set; }
        private readonly List<Tuple<int, int>> SupportedColorResolutions = new List<Tuple<int, int>>
        {
            Tuple.Create(1920, 1080),
            Tuple.Create(1280, 720),
            Tuple.Create(960, 540),
            Tuple.Create(640, 480),
            Tuple.Create(640, 360),
        };

        public int NumDetection
        {
            get 
            {
                int val;
                try
                {
                    val = Convert.ToInt32(NumDetectionText.Text); 
                }
                catch
                {
                    val = 0;
                }
                return val; 
            }
        }

        public int NumLandmarks
        {
            get 
            {
                int val;
                try
                {
                    val = Convert.ToInt32(NumLandmarksText.Text); 
                }
                catch
                {
                    val = 0;
                }
                return val; 
            }            
        }

        public int NumPose
        {
            get 
            {
                int val;
                try
                {
                    val = Convert.ToInt32(NumPoseText.Text); 
                }
                catch
                {
                    val = 0;
                }
                return val; 
            }             
        }

        public int NumExpressions
        {
            get 
            {
                int val;
                try
                {
                    val = Convert.ToInt32(NumExpressionsText.Text); 
                }
                catch
                {
                    val = 0;
                }
                return val; 
            }
        }

        public string GetFileName()
        {
            return m_filename;
        }

        public bool IsRecognitionChecked()
        {
            return Recognition.Checked;
        }

        private void CreateResolutionMap()
        {
            ColorResolutions = new Dictionary<string, IEnumerable<Tuple<PXCMImage.ImageInfo, PXCMRangeF32>>>();
            var desc = new PXCMSession.ImplDesc
            {
                group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR,
                subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE
            };

            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (Session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                PXCMCapture capture;
                if (Session.CreateImpl(desc1, out capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;

                for (int j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo info;
                    if (capture.QueryDeviceInfo(j, out info) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                    PXCMCapture.Device device = capture.CreateDevice(j);
                    if (device == null)
                    {
                        throw new Exception("PXCMCapture.Device null");
                    }
                    var deviceResolutions = new List<Tuple<PXCMImage.ImageInfo, PXCMRangeF32>>();

                    for (int k = 0; k < device.QueryStreamProfileSetNum(PXCMCapture.StreamType.STREAM_TYPE_COLOR); k++)
                    {
                        PXCMCapture.Device.StreamProfileSet profileSet;
                        device.QueryStreamProfileSet(PXCMCapture.StreamType.STREAM_TYPE_COLOR, k, out profileSet);
                        var currentRes = new Tuple<PXCMImage.ImageInfo, PXCMRangeF32>(profileSet.color.imageInfo,
                            profileSet.color.frameRate);

                        if (SupportedColorResolutions.Contains(new Tuple<int, int>(currentRes.Item1.width, currentRes.Item1.height)))
                        {
                            deviceResolutions.Add(currentRes);
                        }
                    }
                    ColorResolutions.Add(info.name, deviceResolutions);
                    device.Dispose();
                }                              
                
                capture.Dispose();
            }
        }

        public void PopulateDeviceMenu()
        {
            Devices = new Dictionary<string, PXCMCapture.DeviceInfo>();
            var desc = new PXCMSession.ImplDesc
            {
                group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR,
                subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE
            };
                        
            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (Session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                PXCMCapture capture;
                if (Session.CreateImpl(desc1, out capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;

                for (int j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo dinfo;
                    if (capture.QueryDeviceInfo(j, out dinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                    if (!Devices.ContainsKey(dinfo.name))
                        Devices.Add(dinfo.name, dinfo);
                    var sm1 = new ToolStripMenuItem(dinfo.name, null, Device_Item_Click);
                    m_deviceMenuItem.DropDownItems.Add(sm1);
                }

                capture.Dispose();
            }

            if (m_deviceMenuItem.DropDownItems.Count > 0)
            {
                ((ToolStripMenuItem)m_deviceMenuItem.DropDownItems[0]).Checked = true;
                PopulateColorResolutionMenu(m_deviceMenuItem.DropDownItems[0].ToString());
            }

            try
            {
                MainMenu.Items.RemoveAt(0);
            }
            catch (NotSupportedException)
            {
                m_deviceMenuItem.Dispose();
                throw;
            }
            MainMenu.Items.Insert(0, m_deviceMenuItem);
        }

        public void PopulateColorResolutionMenu(string deviceName)
        {
            bool foundDefaultResolution = false;
            var sm = new ToolStripMenuItem("Color Resolution");
            foreach (var resolution in ColorResolutions[deviceName])
            {
                string resText = PixelFormat2String(resolution.Item1.format) + " " + resolution.Item1.width + "x"
                                 + resolution.Item1.height + " " + resolution.Item2.max + " fps";
                var sm1 = new ToolStripMenuItem(resText, null);
                Tuple<PXCMImage.ImageInfo, PXCMRangeF32> selectedResolution = resolution;
                sm1.Click += (sender, eventArgs) =>
                {
                    m_selectedColorResolution = selectedResolution;
                    ColorResolution_Item_Click(sender);
                };
            
                sm.DropDownItems.Add(sm1);

                if (selectedResolution.Item1.format == PXCMImage.PixelFormat.PIXEL_FORMAT_YUY2 && 
                    selectedResolution.Item1.width == 640 && selectedResolution.Item1.height == 360 && selectedResolution.Item2.min == 30)
                {
                    foundDefaultResolution = true;
                    sm1.Checked = true;
                    sm1.PerformClick();
                }
            }

	        if (!foundDefaultResolution && sm.DropDownItems.Count > 0)
	        {
	            ((ToolStripMenuItem)sm.DropDownItems[0]).Checked = true;
	            ((ToolStripMenuItem)sm.DropDownItems[0]).PerformClick();
	        }

            try
            {
                MainMenu.Items.RemoveAt(1);
            }
            catch (NotSupportedException)
            {
                sm.Dispose();
                throw;
            }
            MainMenu.Items.Insert(1, sm);
        }

        private void PopulateModuleMenu()
        {
            var desc = new PXCMSession.ImplDesc();
            desc.cuids[0] = PXCMFaceModule.CUID;
            
            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (Session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                var mm1 = new ToolStripMenuItem(desc1.friendlyName, null, Module_Item_Click);
                m_moduleMenuItem.DropDownItems.Add(mm1);
            }
            if (m_moduleMenuItem.DropDownItems.Count > 0)
                ((ToolStripMenuItem)m_moduleMenuItem.DropDownItems[0]).Checked = true;
            try
            {
                MainMenu.Items.RemoveAt(2);
            }
            catch (NotSupportedException)
            {
                m_moduleMenuItem.Dispose();
                throw;
            }
            MainMenu.Items.Insert(2, m_moduleMenuItem);
            
        }

        private void PopulateProfileMenu()
        {
            var pm = new ToolStripMenuItem("Profile");

            foreach (var trackingMode in (PXCMFaceConfiguration.TrackingModeType[])Enum.GetValues(typeof(PXCMFaceConfiguration.TrackingModeType)))
            {
                var pm1 = new ToolStripMenuItem(FaceMode2String(trackingMode), null, Profile_Item_Click);
                pm.DropDownItems.Add(pm1);

                if (trackingMode == PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH) //3d = default
                {
                    pm1.Checked = true;
                }
            }
            try
            {
                MainMenu.Items.RemoveAt(3);
            }
            catch (NotSupportedException)
            {
                pm.Dispose();
                throw;
            }
            MainMenu.Items.Insert(3, pm);
        }

        private static string FaceMode2String(PXCMFaceConfiguration.TrackingModeType mode)
        {
            switch (mode)
            {
                case PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR:
                    return "2D Tracking";
                case PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH:
                    return "3D Tracking";
            }
            return "";
        }

        private static string PixelFormat2String(PXCMImage.PixelFormat format)
        {
            switch (format)
            {
                case PXCMImage.PixelFormat.PIXEL_FORMAT_YUY2:
                    return "YUY2";
                case PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32:
                    return "RGB32";
                case PXCMImage.PixelFormat.PIXEL_FORMAT_RGB24:
                    return "RGB24";                
            }
            return "NA";
        }

        private void RadioCheck(object sender, string name)
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals(name)) continue;
                foreach (ToolStripMenuItem e1 in m.DropDownItems)
                {
                    e1.Checked = (sender == e1);
                }
            }
        }

        private void ColorResolution_Item_Click(object sender)
        {
            RadioCheck(sender, "Color Resolution");
        }

        private void Device_Item_Click(object sender, EventArgs e)
        {
            PopulateColorResolutionMenu(sender.ToString());
            RadioCheck(sender, "Device");
        }

        private void Module_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Module");
            PopulateProfileMenu();
        }

        private void Profile_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Profile");
        }
        bool faceDetect = true;
        bool handMode = false;
        private void Start_Click(object sender, EventArgs e)
        {
            Start.Enabled = false;
            MainMenu.Enabled = false;
            Mirror.Enabled = false;
            NumDetectionText.Enabled = false;
            NumLandmarksText.Enabled = false;
            NumPoseText.Enabled = false;
            NumExpressionsText.Enabled = false;
            Stop.Enabled = true;

            foreach (CheckBox moduleCheckBox in m_modulesCheckBoxes)
            {
                moduleCheckBox.Enabled = false;
            }

            if (Recognition.Checked)
            {
                RegisterUser.Enabled = true;
                UnregisterUser.Enabled = true;
            }

            Stopped = false;
            if (faceDetect)
            {
                handMode = false;
                var thread = new Thread(DoTracking);
                thread.Start();
            }
            //else
            //{
            //    ///var thread = new Thread(DoHandTracking);
            //    //thread.Start();
            //}
        }

        //private void DoHandTracking()
        //{
        //    HandsRecognition ft = new HandsRecognition(this);
        //    ft.SimplePipeline();
        //    Invoke(new DoTrackingCompleted(() =>
        //    {
        //        foreach (CheckBox moduleCheckBox in m_modulesCheckBoxes)
        //        {
        //            moduleCheckBox.Enabled = true;
        //        }
        //        Start.Enabled = true;
        //        Stop.Enabled = false;
        //        MainMenu.Enabled = true;

        //        Mirror.Enabled = true;
        //        NumDetectionText.Enabled = true;
        //        NumLandmarksText.Enabled = true;
        //        NumPoseText.Enabled = true;
        //        NumExpressionsText.Enabled = true;

        //        RegisterUser.Enabled = false;
        //        UnregisterUser.Enabled = false;

        //        if (m_closing) Close();
        //    }));
        //}
        private void DoTracking()
        {
            var ft = new FaceTracking(this);
            ft.SimplePipeline();
            Invoke(new DoTrackingCompleted(() =>
            {
                foreach (CheckBox moduleCheckBox in m_modulesCheckBoxes)
                {
                    moduleCheckBox.Enabled = true;
                }
                Start.Enabled = true;
                Stop.Enabled = false;
                MainMenu.Enabled = true;

                Mirror.Enabled = true;
                NumDetectionText.Enabled = true;
                NumLandmarksText.Enabled = true;
                NumPoseText.Enabled = true;
                NumExpressionsText.Enabled = true;

                RegisterUser.Enabled = false;
                UnregisterUser.Enabled = false;

                //if (m_closing) Close();
            }));
        }

        public string GetCheckedDevice()
        {
            return (from ToolStripMenuItem m in MainMenu.Items
                where m.Text.Equals("Device")
                from ToolStripMenuItem e in m.DropDownItems
                where e.Checked
                select e.Text).FirstOrDefault();
        }

        public Tuple<PXCMImage.ImageInfo, PXCMRangeF32> GetCheckedColorResolution()
        {
            return m_selectedColorResolution;
        }

        public string GetCheckedModule()
        {
            return (from ToolStripMenuItem m in MainMenu.Items
                where m.Text.Equals("Module")
                from ToolStripMenuItem e in m.DropDownItems
                where e.Checked
                select e.Text).FirstOrDefault();
        }

        public string GetCheckedProfile()
        {
            foreach (ToolStripMenuItem m in from ToolStripMenuItem m in MainMenu.Items where m.Text.Equals("Profile") select m)
            {
                for (int i = 0; i < m.DropDownItems.Count; i++)
                {
                    if (((ToolStripMenuItem) m.DropDownItems[i]).Checked)
                        return m.DropDownItems[i].Text;
                }
            }
            return "";
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Stopped = true;
            e.Cancel = Stop.Enabled;
            m_closing = true;
        }

        public void UpdateStatus(string status, Label label)
        {
            if (label == Label.StatusLabel)
                Status2.Invoke(new UpdateStatusDelegate(delegate(string s) { StatusLabel.Text = s; }),
                    new object[] {status});

            if (label == Label.AlertsLabel)
                Status2.Invoke(new UpdateStatusDelegate(delegate(string s) { AlertsLabel.Text = s; }),
                    new object[] {status});
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            Stopped = true;
        }

        private void Panel_Paint(object sender, PaintEventArgs e)
        {
            lock (m_bitmapLock)
            {
                if (m_bitmap == null) return;
                if (Scale.Checked)
                {
                    e.Graphics.DrawImage(m_bitmap, Panel2.ClientRectangle);
                }
                else
                {
                    e.Graphics.DrawImageUnscaled(m_bitmap, 0, 0);
                }
            }
        }

        public void UpdatePanel()
        {
            Panel2.Invoke(new UpdatePanelDelegate(() => Panel2.Invalidate()));
        }

        public void DrawBitmap(Bitmap picture)
        {
            lock (m_bitmapLock)
            {
                if (m_bitmap != null)
                {
                    m_bitmap.Dispose();
                }
                m_bitmap = new Bitmap(picture);
            }
        }

        public void DrawGraphics(PXCMFaceData moduleOutput)
        {
            Debug.Assert(moduleOutput != null);

            for (int i = 0; i < moduleOutput.QueryNumberOfDetectedFaces(); i++)
            {
                PXCMFaceData.Face face = moduleOutput.QueryFaceByIndex(i);
                if (face == null)
                {
                    throw new Exception("DrawGraphics::PXCMFaceData.Face null");
                }
                
                lock (m_bitmapLock)
                {
                    m_faceTextOrganizer.ChangeFace(i, face, m_bitmap.Height, m_bitmap.Width);
                }

             //   DrawLocation(face);
                DrawLandmark(face);
                DrawPose(face);
                DrawExpressions(face);
            //    DrawRecognition(face);
            }
        }

        private void RegisterUser_Click(object sender, EventArgs e)
        {
            Register = true;
        }

        private void UnregisterUser_Click(object sender, EventArgs e)
        {
            Unregister = true;
        }

        #region Playback / Record

        private void Live_Click(object sender, EventArgs e)
        {
            Playback.Checked = Record.Checked = false;
            Live.Checked = true;
        }

        private void Playback_Click(object sender, EventArgs e)
        {
            Live.Checked = Record.Checked = false;
            Playback.Checked = true;
            var ofd = new OpenFileDialog
            {
                Filter = @"RSSDK clip|*.rssdk|Old format clip|*.pcsdk|All files|*.*",
                CheckFileExists = true,
                CheckPathExists = true
            };
            try
            {
                m_filename = (ofd.ShowDialog() == DialogResult.OK) ? ofd.FileName : null;                
            }
            catch (Exception)
            {
                ofd.Dispose();
                throw;
            }
            ofd.Dispose();
        }

        public bool GetPlaybackState()
        {
            return Playback.Checked;
        }

        private void Record_Click(object sender, EventArgs e)
        {
            Live.Checked = Playback.Checked = false;
            Record.Checked = true;
            var sfd = new SaveFileDialog
            {
                Filter = @"RSSDK clip|*.rssdk|All files|*.*",
                CheckPathExists = true,
                OverwritePrompt = true,
                AddExtension    = true
            };
            try
            {
                m_filename = (sfd.ShowDialog() == DialogResult.OK) ? sfd.FileName : null;
            }
            catch (Exception)
            {
                sfd.Dispose();
                throw;
            }
            sfd.Dispose();
        }

        public bool GetRecordState()
        {
            return Record.Checked;
        }

        public string GetPlaybackFile()
        {
            return Invoke(new GetFileDelegate(() =>
            {
                var ofd = new OpenFileDialog
                {
                    Filter = @"All files (*.*)|*.*",
                    CheckFileExists = true,
                    CheckPathExists = true
                };
                return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
            })) as string;
        }

        public string GetRecordFile()
        {
            return Invoke(new GetFileDelegate(() =>
            {
                var sfd = new SaveFileDialog
                {
                    Filter = @"All files (*.*)|*.*",
                    CheckFileExists = true,
                    OverwritePrompt = true
                };
                if (sfd.ShowDialog() == DialogResult.OK) return sfd.FileName;
                return null;
            })) as string;
        }

        private delegate string GetFileDelegate();

        #endregion

        #region Modules Drawing

        private static readonly Assembly m_assembly = Assembly.GetExecutingAssembly();

        private readonly ResourceSet m_resources = 
            new ResourceSet(m_assembly.GetManifestResourceStream(@"face_tracking.cs.Properties.Resources.resources"));

        private readonly Dictionary<PXCMFaceData.ExpressionsData.FaceExpression, Bitmap> m_cachedExpressions =
            new Dictionary<PXCMFaceData.ExpressionsData.FaceExpression, Bitmap>();

        private readonly Dictionary<PXCMFaceData.ExpressionsData.FaceExpression, string> m_expressionDictionary =
            new Dictionary<PXCMFaceData.ExpressionsData.FaceExpression, string>
            {
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_MOUTH_OPEN, @"MouthOpen"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_SMILE, @"Smile"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_KISS, @"Kiss"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_UP, @"Eyes_Turn_Up"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_DOWN, @"Eyes_Turn_Down"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_TURN_LEFT, @"Eyes_Turn_Left"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_TURN_RIGHT, @"Eyes_Turn_Right"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_CLOSED_LEFT, @"Eyes_Closed_Left"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_CLOSED_RIGHT, @"Eyes_Closed_Right"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_BROW_LOWERER_RIGHT, @"Brow_Lowerer_Right"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_BROW_LOWERER_LEFT, @"Brow_Lowerer_Left"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_BROW_RAISER_RIGHT, @"Brow_Raiser_Right"},
                {PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_BROW_RAISER_LEFT, @"Brow_Raiser_Left"}
            };

        public void DrawLocation(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            if (m_bitmap == null || !Detection.Checked) return;

            PXCMFaceData.DetectionData detection = face.QueryDetection();
            if (detection == null)
                return;

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var pen = new Pen(m_faceTextOrganizer.Colour, 3.0f))
                using (var brush = new SolidBrush(m_faceTextOrganizer.Colour))
                using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                {
                    graphics.DrawRectangle(pen, m_faceTextOrganizer.RectangleLocation);
                    String faceId = String.Format("Face ID: {0}",
                        face.QueryUserID().ToString(CultureInfo.InvariantCulture));
                    graphics.DrawString(faceId, font, brush, m_faceTextOrganizer.FaceIdLocation);
                }
            }
        }


        PXCMFaceData.PoseEulerAngles PoseEur;
        PXCMFaceData.ExpressionsData expressionData;
        //PXCMFaceData.HeadPosition HeadPosit;
        double NormalDelta;
        Image<Bgr, byte> Graph;
        List<double> YawInTime = new List<double>();
        List<double> EyeInTime = new List<double>();
        public void DrawLandmark(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            PXCMFaceData.PoseData Pos =  face.QueryPose();
            PXCMFaceData.LandmarksData landmarks = face.QueryLandmarks();
            
            try
            {
                Pos.QueryPoseAngles(out PoseEur);
                //Pos.QueryHeadPosition(out HeadPosit);
                
                
            }
            catch
            {
            }
            
            if (m_bitmap == null || !Landmarks.Checked || landmarks == null) return;

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var brush = new SolidBrush(Color.White))
                using (var lowConfidenceBrush = new SolidBrush(Color.Red))
                using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                {
                    PXCMFaceData.LandmarkPoint[] points;
                    bool res = landmarks.QueryPoints(out points);
                    Debug.Assert(res);

                    var point = new PointF();

                    foreach (PXCMFaceData.LandmarkPoint landmark in points)
                    
                    {
                        
                        point.X = landmark.image.x + LANDMARK_ALIGNMENT;
                        point.Y = landmark.image.y + LANDMARK_ALIGNMENT;

                        if (landmark.confidenceImage == 0)
                            graphics.DrawString("x", font, lowConfidenceBrush, point);
                        else
                            graphics.DrawString("•", font, brush, point);
                    }
                    PointF MeanL = new PointF();
                    for (int i=10;i<18;i++)
                    {
                        MeanL.X += points[i].image.x;
                        MeanL.Y += points[i].image.y;
                    }
                    MeanL.X = (float)(MeanL.X / 8.0);
                    MeanL.Y = (float)(MeanL.Y / 8.0);
                    graphics.DrawString("P", font, lowConfidenceBrush, MeanL);
                    PointF MeanR = new PointF();
                    for (int i = 18; i < 26; i++)
                    {
                        MeanR.X += points[i].image.x;
                        MeanR.Y += points[i].image.y;
                    }
                    MeanR.X = (float)(MeanR.X / 8.0);
                    MeanR.Y = (float)(MeanR.Y / 8.0);
                    graphics.DrawString("P", font, lowConfidenceBrush, MeanR);

                    NormalDelta=(points[76].image.x-MeanL.X +points[77].image.x-MeanR.X)/(MeanR.X-MeanL.X);
                }
            }
        }

        public void DrawPose(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            PXCMFaceData.PoseEulerAngles poseAngles;
            PXCMFaceData.PoseData pdata = face.QueryPose();
            if (pdata == null)
            {
                return;
            }
            if (!Pose.Checked || !pdata.QueryPoseAngles(out poseAngles)) return;

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var brush = new SolidBrush(m_faceTextOrganizer.Colour))
                using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                {
                    string yawText = String.Format("Yaw = {0}",
                        Convert.ToInt32(poseAngles.yaw).ToString(CultureInfo.InvariantCulture));
                    graphics.DrawString(yawText, font, brush, m_faceTextOrganizer.PoseLocation.X,
                        m_faceTextOrganizer.PoseLocation.Y);

                    string pitchText = String.Format("Pitch = {0}",
                        Convert.ToInt32(poseAngles.pitch).ToString(CultureInfo.InvariantCulture));
                    graphics.DrawString(pitchText, font, brush, m_faceTextOrganizer.PoseLocation.X,
                        m_faceTextOrganizer.PoseLocation.Y + m_faceTextOrganizer.FontSize);

                    string rollText = String.Format("Roll = {0}",
                        Convert.ToInt32(poseAngles.roll).ToString(CultureInfo.InvariantCulture));
                    graphics.DrawString(rollText, font, brush, m_faceTextOrganizer.PoseLocation.X,
                        m_faceTextOrganizer.PoseLocation.Y + 2 * m_faceTextOrganizer.FontSize);
                }
            }
        }

        public void DrawExpressions(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            if (m_bitmap == null || !Expressions.Checked) return;

            PXCMFaceData.ExpressionsData expressionsOutput = face.QueryExpressions();

            if (expressionsOutput == null) return;

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var brush = new SolidBrush(m_faceTextOrganizer.Colour))
                {
                    const int imageSizeWidth = 18;
                    const int imageSizeHeight = 18;

                    int positionX = m_faceTextOrganizer.ExpressionsLocation.X;
                    int positionXText = positionX + imageSizeWidth;
                    int positionY = m_faceTextOrganizer.ExpressionsLocation.Y;
                    int positionYText = positionY + imageSizeHeight / 4;

                    foreach (var expressionEntry in m_expressionDictionary)
                    {
                        PXCMFaceData.ExpressionsData.FaceExpression expression = expressionEntry.Key;
                        PXCMFaceData.ExpressionsData.FaceExpressionResult result;
                        bool status = expressionsOutput.QueryExpression(expression, out result);
                        if (!status) continue;

                        Bitmap cachedExpressionBitmap;
                        bool hasCachedExpressionBitmap = m_cachedExpressions.TryGetValue(expression, out cachedExpressionBitmap);
                        if (!hasCachedExpressionBitmap)
                        {
                            cachedExpressionBitmap = (Bitmap) m_resources.GetObject(expressionEntry.Value);
                            m_cachedExpressions.Add(expression, cachedExpressionBitmap);
                        }

                        //using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                        //{
                        //    graphics.DrawImage(cachedExpressionBitmap, new Rectangle(positionX, positionY, imageSizeWidth, imageSizeHeight));
                        //    string expressionText = String.Format("= {0}", result.intensity);
                        //    graphics.DrawString(expressionText, font, brush, positionXText, positionYText);

                        //    positionY += imageSizeHeight;
                        //    positionYText += imageSizeHeight;
                        //}
                    }
                }
            }
            //int eyeClose;
            //PXCMFaceData.ExpressionsData.FaceExpressionResult expressionResultR;
            //PXCMFaceData.ExpressionsData.FaceExpressionResult expressionResultL;
            //if ((expressionsOutput.QueryExpression(PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_CLOSED_LEFT, out expressionResultR))
            // && (expressionsOutput.QueryExpression(PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_CLOSED_RIGHT, out expressionResultL)))
            //{
            //    if ((expressionResultL.intensity == 100) && (expressionResultR.intensity == 100))
            //    {
            //        Console.Beep(1000, 100);
            //        //NewMethod();
            //        //eyeClose = 100;
            //    }

            //}
            //textBox7.Text = eyeClose.ToString();
        }
        
        
        public void DrawRecognition(PXCMFaceData.Face face)
        {
            Debug.Assert(face != null);
            if (m_bitmap == null || !Recognition.Checked) return;

            PXCMFaceData.RecognitionData qrecognition = face.QueryRecognition();
            if (qrecognition == null)
            {
                throw new Exception(" PXCMFaceData.RecognitionData null");
            }
            int userId = qrecognition.QueryUserID();
            string recognitionText = userId == -1 ? "Not Registered" : String.Format("Registered ID: {0}", userId);

            lock (m_bitmapLock)
            {
                using (Graphics graphics = Graphics.FromImage(m_bitmap))
                using (var brush = new SolidBrush(m_faceTextOrganizer.Colour))
                using (var font = new Font(FontFamily.GenericMonospace, m_faceTextOrganizer.FontSize, FontStyle.Bold))
                {
                    graphics.DrawString(recognitionText, font, brush, m_faceTextOrganizer.RecognitionLocation);
                }
            }
        }

        #endregion

        private delegate void DoTrackingCompleted();

        private delegate void UpdatePanelDelegate();

        private delegate void UpdateStatusDelegate(string status);

        private void Detection_CheckedChanged(object sender, EventArgs e)
        {
            NumDetectionText.Enabled = Detection.Checked;
        }

        private void Landmarks_CheckedChanged(object sender, EventArgs e)
        {
            NumLandmarksText.Enabled = Landmarks.Checked;
        }

        private void Pose_CheckedChanged(object sender, EventArgs e)
        {
            NumPoseText.Enabled = Pose.Checked;
        }

        private void Expressions_CheckedChanged(object sender, EventArgs e)
        {
            NumExpressionsText.Enabled = Expressions.Checked;
        }

        public bool IsDetectionEnabled()
        {
            return Detection.Checked;
        }

        public bool IsLandmarksEnabled()
        {
            return Landmarks.Checked;
        }

        public bool IsPoseEnabled()
        {
            return Pose.Checked;
        }

        public bool IsExpressionsEnabled()
        {
            return Expressions.Checked;
        }

        public bool IsMirrored()
        {
            return Mirror.Checked;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ReDraw();
        }
        List<double> SummitAll = new List<double>();
        int Pointofinterest = 0;
        double integrate = 0;
        int panel = 0;
        ///bool runHand = false;
        // bool handrunning = false;
        
        
        
        

        private void timer1_Tick(object sender, EventArgs e)
        {

            if (PoseEur != null)
            {
                textBox1.Text = PoseEur.pitch.ToString();
                textBox2.Text = PoseEur.roll.ToString();
                textBox3.Text = PoseEur.yaw.ToString();
                textBox4.Text = NormalDelta.ToString();
                //textBox7.Text = PXCMFaceData.ExpressionsData.FaceExpression.EXPRESSION_EYES_CLOSED_LEFT.ToString(expre);
                Graph = new Image<Bgr, byte>(imageBox1.Size);
                Graph.SetValue(new Bgr(255, 255, 255));
                YawInTime.Add(PoseEur.yaw);
                EyeInTime.Add(0-NormalDelta*200+10);
                if (Math.Abs(PoseEur.yaw)<10)
                {
                    SummitAll.Add(PoseEur.yaw - NormalDelta * 200 + 10);
                }
                else
                {
                    SummitAll.Add(PoseEur.yaw);
                }
                if (YawInTime.Count >= Graph.Width)
                {
                    EyeInTime.RemoveAt(0);
                    YawInTime.RemoveAt(0);
                    SummitAll.RemoveAt(0);
                }
                textBox5.Text = SummitAll[SummitAll.Count-1].ToString();
                if (SummitAll[SummitAll.Count - 1] > 12)
                {
                    panel = 0;
                    Pointofinterest = -1;
                }
                else if (SummitAll[SummitAll.Count - 1] > -7)
                {
                    panel = 0;
                    Pointofinterest = 0;
                }
                //else if (SummitAll[SummitAll.Count - 1] > -18)
                //{
                //    Pointofinterest = 1;
                //    if (!handrunning)
                //    {
                //        panel++;
                        

                //        if (panel > 30)
                //        {
                //            Stopped = true;
                //            checkBox1.Checked = false;
                //            //Thread.Sleep(3000);
                //            //runHand = true;
                //            handrunning = true;
                //            //Start_Click(sender,e);
                            
                //        }
                //    }
                //}
                else
                {
                    Pointofinterest = 2;
                    panel = 0;
                }
                Point[] Ps = new Point[Graph.Width];
                Point[] Ps2 = new Point[Graph.Width];
                Point[] Ps3 = new Point[Graph.Width];
                for (int i = 0; i < Ps.Length; i++)
                {
                    Ps[i] = new Point(i, Graph.Height / 2);
                    Ps2[i] = new Point(i, Graph.Height / 2);
                    Ps3[i] = new Point(i, Graph.Height / 2);
                }
                    for (int i = 0; i < Math.Min(YawInTime.Count, Graph.Width); i++)
                    {
                        Ps[i] = new Point(i, Graph.Height / 2 + (int)YawInTime[i]);
                        Ps2[i] = new Point(i, Graph.Height / 2 + (int)EyeInTime[i]);
                        Ps3[i] = new Point(i, Graph.Height / 2 + (int)SummitAll[i]);
                    }
                   integrate = 0;
                   int from = SummitAll.Count - 1;
                   int toto = Math.Max(SummitAll.Count - Graph.Width / 2, 0);
                   if (from != 0)
                   {
                       for (int i = from; i > toto; i--)
                       {
                           integrate += SummitAll[i];
                       }
                       integrate = integrate / (from - toto);
                       double dd = 0;
                       for (int i = from; i > toto; i--)
                       {
                          dd += Math.Sqrt((integrate - SummitAll[i]) * (integrate - SummitAll[i]));
                       }
                       integrate = dd / (from - toto);
                   }
                
                    
                    textBox6.Text = integrate.ToString();
                label1.Text = "TIMER = " + integrate.ToString();
                Graph.DrawPolyline(Ps,false, new Bgr(255, 0, 0), 1);
                Graph.DrawPolyline(Ps2, false, new Bgr(0, 255, 0), 1);
                Graph.DrawPolyline(Ps3, false, new Bgr(0, 0, 255), 1);
                imageBox1.Image = Graph;
                //hpx = (int)(HeadPosit.headCenter.x / 10);
                //HeadPos =  (int)(HeadPosit.headCenter.z/10);
                ReDraw();
                



            }
        }

        private void NumExpressionsText_TextChanged(object sender, EventArgs e)
        {

        }

        private void NumPoseText_TextChanged(object sender, EventArgs e)
        {

        }

        private void NumLandmarksText_TextChanged(object sender, EventArgs e)
        {

        }

        private void NumDetectionText_TextChanged(object sender, EventArgs e)
        {

        }

        private void Recognition_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void Scale_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void Mirror_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
          
            faceDetect = checkBox1.Checked;
  
        }
        bool move = false;
        bool handFound=false;
        int noHand=0;
        int await = 0;
        private void timer2_Tick(object sender, EventArgs e)
        {
            if (bitmap != null)
            {
                lock (this)
                {
                    try
                    {
                        //pictureBox1.Image = (Bitmap)bitmap.Clone();
                    }
                    catch
                    { }
                }
            }

            //pictureBox2.Refresh();

            checkBox3.Checked = move;
            checkBox2.Checked = scaling;

            
            
        }

        List<bool> moves = new List<bool>();
        double xstart = 0;
        double ystart = 0;
        double X0 = 0;
        double Y0 = 0;
        bool prevscale = false;
        bool scaling = false;
        PXCMPoint3DF32 prev = new PXCMPoint3DF32();

        //public void AnalyzeHand(PXCMHandData.JointData[] nodes)
        //{
        //    PXCMPoint3DF32 center = nodes[1].positionWorld;

        //    PXCMPoint3DF32[] fingers = new PXCMPoint3DF32[5];
        //    fingers[0] = nodes[5].positionWorld;
        //    fingers[1] = nodes[9].positionWorld;
        //    fingers[2] = nodes[13].positionWorld;
        //    fingers[3] = nodes[17].positionWorld;
        //    fingers[4] = nodes[21].positionWorld;



        //    PXCMPoint3DF32 averageDirection = new PXCMPoint3DF32();
        //    for (int i = 1; i < fingers.Length; i++)
        //    {
        //        averageDirection.x += (fingers[i].x - center.x);
        //        averageDirection.y += (fingers[i].y - center.y);
        //        averageDirection.z += (fingers[i].z - center.z);
        //    }
        //    PXCMPoint3DF32 average = new PXCMPoint3DF32();
        //    for (int i = 1; i < nodes.Length; i++)
        //    {
        //        average.x += nodes[i].positionWorld.x;
        //        average.y += nodes[i].positionWorld.y;
        //        average.z += nodes[i].positionWorld.z;
        //    }
        //    average.x /= (float)nodes.Length;
        //    average.y /= (float)nodes.Length;
        //    average.z /= (float)nodes.Length;

        //    if (Math.Sqrt((prev.x - prev.x) * (prev.x - prev.x) + (prev.y - prev.y) * (prev.y - prev.y) + (prev.z - prev.z) * (prev.z - prev.z)) > 0.02)
        //    {
        //        move = false;
        //    }
        //    prev = average;

        //    averageDirection.x /= 5.0f;
        //    averageDirection.y /= 5.0f;
        //    averageDirection.z /= 5.0f;
        //    moves.Add(move);
        //    if ((averageDirection.z < -0.04))// && (Math.Abs(averageDirection.x) < 30) && (Math.Abs(averageDirection.z) < 50))
        //    {
        //        move = true;
        //        if (moves.Count > 2)
        //        {
        //            if (moves[moves.Count - 1] && (!moves[moves.Count - 2]))
        //            {
        //                xstart = average.x;
        //                ystart = average.y;
        //                X0 = X;
        //                Y0 = Y;
        //            }
        //            else
        //            {
        //                if (moves[moves.Count - 1])
        //                {
        //                    X = X0 + average.x - xstart;
        //                    Y = Y0 + -average.y + ystart;

        //                }
        //            }
        //        }
        //    }
        //    else
        //    {
        //        if (moves.Count > 1)
        //        {
        //            if (moves[moves.Count - 1])
        //            {
        //                X0 = X;
        //                Y0 = Y;
        //            }
        //        }
        //        move = false;
        //    }

        //    double length3 = 0;

        //    for (int i = 2; i < fingers.Length; i++)
        //    {
        //        length3+=Math.Sqrt((center.x - fingers[i].x) * (center.x - fingers[i].x) + (center.y - fingers[i].y) * (center.y - fingers[i].y) + (center.z - fingers[i].z) * (center.z - fingers[i].z));

        //    }
        //    length3 /= 3.0;
        //    double length2 = 0;
            
        //    length2 = Math.Sqrt((center.x - fingers[1].x) * (center.x - fingers[1].x) + (center.y - fingers[1].y) * (center.y - fingers[1].y) + (center.z - fingers[1].z) * (center.z - fingers[1].z));
        //    if ((length2 > 0.08) && (length3 < 0.04))
        //    {
        //        scaling = true;
        //        if (prevscale)
        //        {
        //            double s = Math.Sqrt((fingers[0].x - fingers[1].x) * (fingers[0].x - fingers[1].x) + (fingers[0].y - fingers[1].y) * (fingers[0].y - fingers[1].y) + (fingers[0].z - fingers[1].z) * (fingers[0].z - fingers[1].z));
        //            scale = s / 0.05;
        //        }
        //        prevscale = scaling;
        //        //if (scale > 1.0) scale = 1.0;

        //    }
        //    else
        //        scaling = false;

        //}
        double X = 0;
        double Y = 0;
        double scale = 1.0;

        Point start = new Point();
        Point finish = new Point();
        //Bitmap map = new Bitmap("c:\\temp\\map.png");

       

     

        private void pictureBox2_MouseDown(object sender, MouseEventArgs e)
        {
            
        }

        private void pictureBox2_MouseUp(object sender, MouseEventArgs e)
        {
          
        }

        

        private void timer3_Tick(object sender, EventArgs e)
        {
            if ((integrate < 1.5)&&(integrate!=0))
            {
                if (panoColor.Blue == 255)
                {
                    panoColor = new Bgr(0, 0, 255);
                    Console.Beep(1000, 100);
                }
                else
                {
                    panoColor = new Bgr(255, 0, 0);
                }
            }
            else 
            {
                panoColor = new Bgr(255, 0, 0);
            }
        }

        private void Start_picure_Click(object sender, EventArgs e)
        {
            Start.Enabled = false;
            MainMenu.Enabled = false;
            Mirror.Enabled = false;
            NumDetectionText.Enabled = false;
            NumLandmarksText.Enabled = false;
            NumPoseText.Enabled = false;
            NumExpressionsText.Enabled = false;
            Stop.Enabled = true;

            foreach (CheckBox moduleCheckBox in m_modulesCheckBoxes)
            {
                moduleCheckBox.Enabled = false;
            }

            if (Recognition.Checked)
            {
                RegisterUser.Enabled = true;
                UnregisterUser.Enabled = true;
            }

            Stopped = false;
            if (faceDetect)
            {
                handMode = false;
                var thread = new Thread(DoTracking);
                thread.Start();
            }
            //else
            //{
            //    ///var thread = new Thread(DoHandTracking);
            //    //thread.Start();
            //}
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Stopped = true;
        }
    }
}
