using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using ProjNet.Converters;
using MissionPlanner;
using System.Collections;

namespace MissionPlanner.Swarm
{
    public partial class FormationControl : Form
    {
        public enum TaskStyle { Point = 0, Height, Model, ROI }
        public enum WPStyle { OnlyHigher = 0, KeepLevel, WayPoint }
        Formation SwarmInterface = null;
        bool threadrun = false;
        private MAVLinkInterface curChoosePort = new MAVLinkInterface();
        public List<MAVLinkInterface> formationComports = new List<MAVLinkInterface>();
        public List<MAVLinkInterface> outComports = new List<MAVLinkInterface>();
        private List<FormationTask> formationTaskList = new List<FormationTask>();//任务列表(无序)，编队变换时存储的是各飞行器的任务
        private List<FormationTask> leaderTaskList = new List<FormationTask>(); //编队运动时的长机任务(有序)
        private List<List<FormationTask>> planList = new List<List<FormationTask>>();//编队整体任务计划（有序）

        public int PortNum
        {
            get
            {
                return MainV2.Comports.Count;
            }
            set
            {

            }
        }

        public FormationControl()
        {
            InitializeComponent();

            SwarmInterface = new Formation();

            bindingSource1.DataSource = MainV2.Comports;

            CMB_mavs.DataSource = bindingSource1;
            /*2015年12月2日16:22:23
            updateicons();

            this.MouseWheel += new MouseEventHandler(FollowLeaderControl_MouseWheel);
            */
            //MessageBox.Show("this is beta, use at own risk");

            MissionPlanner.Utilities.Tracking.AddPage(this.GetType().ToString(), this.Text);
        }
        /*2015年12月2日16:22:26
        void FollowLeaderControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta < 0)
            {
                grid1.setScale(grid1.getScale() + 4);
            }
            else
            {
                grid1.setScale(grid1.getScale() - 4);
            }
        }

        void updateicons()
        {
            bindingSource1.ResetBindings(false);

            foreach (var port in MainV2.Comports)
            {
                if (port == SwarmInterface.getLeader())
                {
                    ((Formation)SwarmInterface).setOffsets(port, 0, 0, 0);
                    var vector = SwarmInterface.getOffsets(port);
                    grid1.UpdateIcon(port, (float)vector.x, (float)vector.y, (float)vector.z, false);
                }
                else
                {
                    var vector = SwarmInterface.getOffsets(port);
                    grid1.UpdateIcon(port, (float)vector.x, (float)vector.y, (float)vector.z, true);
                }
            }
            grid1.Invalidate();
        }*/

        private void CMB_mavs_SelectedIndexChanged(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                if (port.ToString() == CMB_mavs.Text)
                {
                    MainV2.comPort = port;
                }
            }
        }

        private void BUT_Start_Click(object sender, EventArgs e)
        {
            if (threadrun == true)
            {
                threadrun = false;
                BUT_Start.Text = Strings.Start;
                timer_status.Stop();
                return;
            }

            if (SwarmInterface != null)
            {
                new System.Threading.Thread(mainloop) { IsBackground = true }.Start();
                BUT_Start.Text = Strings.Stop;
                timer_status.Start();
            }
        }

        void mainloop()
        {

            threadrun = true;

            while (threadrun)
            {
                // update leader pos
                SwarmInterface.Update();

                // update other mavs
                SwarmInterface.SendCommand();

                // 5 hz
                System.Threading.Thread.Sleep(200);
            }
        }

        private void BUT_Arm_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.Arm();
            }
        }

        private void BUT_Disarm_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.Disarm();
            }
        }

        private void BUT_Takeoff_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.Takeoff();
            }
        }

        private void BUT_Land_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.Land();
            }

        }

        private void BUT_leader_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                var vectorlead = SwarmInterface.getOffsets(MainV2.comPort);
                /*2015年11月25日16:46:00
                foreach (var port in MainV2.Comports)
                {
                    var vector = SwarmInterface.getOffsets(port);

                    SwarmInterface.setOffsets(port,(float)( vector.x - vectorlead.x),(float)(vector.y - vectorlead.y),(float)(vector.z - vectorlead.z));
                }
                */
                SwarmInterface.setLeader(MainV2.comPort);
                formationComports.Add(MainV2.comPort);//2015年12月9日14:05:44，将leader加入到编队list中
                webBrowser1.Document.InvokeScript("SetLeaderNum", new object[] { GetSerialString(MainV2.comPort) });//2015年12月4日14:20:23,设置leader的SerialString
                //updateicons();
                BUT_Start.Enabled = true;
                BUT_Updatepos.Enabled = true;

            }
        }

        private void BUT_connect_Click(object sender, EventArgs e)
        {
            Comms.CommsSerialScan.Scan(false);

            DateTime deadline = DateTime.Now.AddSeconds(50);//50秒后未检测到连接设备则提示过期

            while (Comms.CommsSerialScan.foundport == false)
            {
                System.Threading.Thread.Sleep(100);

                if (DateTime.Now > deadline)
                {
                    CustomMessageBox.Show("Timeout waiting for autoscan/no mavlink device connected");
                    return;
                }
            }

            MAVLinkInterface com2 = new MAVLinkInterface();

            com2.BaseStream.PortName = Comms.CommsSerialScan.portinterface.PortName;
            com2.BaseStream.BaudRate = Comms.CommsSerialScan.portinterface.BaudRate;

            com2.Open(true);

            MainV2.Comports.Add(com2);
            formationComports.Add(com2);//2015年12月9日14:07:09，将新follower加入到编队list中

            // CMB_mavs.DataSource = MainV2.Comports;

            //CMB_mavs.DataSource

            //updateicons();2015年12月2日16:22:15

            bindingSource1.ResetBindings(false);

            //2015年11月25日16:47:33,连接新飞行器时设置存储其与leader的间距
            var vectorlead = SwarmInterface.getOffsets(SwarmInterface.getLeader());
            foreach (var port in MainV2.Comports)
            {
                var vector = getOffsetFromLeader(SwarmInterface.getLeader(), (MAVLinkInterface)port);

                SwarmInterface.setOffsets(port, (float)vector.x, (float)vector.y, (float)vector.z);
            }
            //2015年11月25日16:47:38
        }

        public HIL.Vector3 getOffsetFromLeader(MAVLinkInterface leader, MAVLinkInterface mav)
        {
            //convert Wgs84ConversionInfo to utm
            CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();

            GeographicCoordinateSystem wgs84 = GeographicCoordinateSystem.WGS84;

            int utmzone = (int)((leader.MAV.cs.lng - -186.0) / 6.0);

            IProjectedCoordinateSystem utm = ProjectedCoordinateSystem.WGS84_UTM(utmzone, leader.MAV.cs.lat < 0 ? false : true);

            ICoordinateTransformation trans = ctfac.CreateFromCoordinateSystems(wgs84, utm);

            double[] masterpll = { leader.MAV.cs.lng, leader.MAV.cs.lat };

            // get leader utm coords(坐标)
            double[] masterutm = trans.MathTransform.Transform(masterpll);

            double[] mavpll = { mav.MAV.cs.lng, mav.MAV.cs.lat };

            //getLeader follower utm coords
            double[] mavutm = trans.MathTransform.Transform(mavpll);

            return new HIL.Vector3(masterutm[1] - mavutm[1], masterutm[0] - mavutm[0], 0);
        }

        private void grid1_UpdateOffsets(MAVLinkInterface mav, float x, float y, float z, Grid.icon ico)
        {
            if (mav == SwarmInterface.Leader)
            {
                CustomMessageBox.Show("Can not move Leader");
                ico.z = 0;
            }
            else
            {
                ((Formation)SwarmInterface).setOffsets(mav, x, y, z);
            }
        }

        private void Control_FormClosing(object sender, FormClosingEventArgs e)
        {
            threadrun = false;
        }

        private void BUT_Updatepos_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                port.MAV.cs.UpdateCurrentSettings(null, true, port);

                if (port == SwarmInterface.Leader)
                    continue;
                /*2015年12月8日16:34:44 暂时不用这个按钮，connect时便已经计算出与leader的offset
                HIL.Vector3 offset = getOffsetFromLeader(((Formation)SwarmInterface).getLeader(), port);

                if (Math.Abs(offset.x) < 200 && Math.Abs(offset.y) < 200)
                {
                    //grid1.UpdateIcon(port, (float)offset.x, (float)offset.y, (float)offset.z, true);2015年12月2日16:22:09
                    //((Formation)SwarmInterface).setOffsets(port, offset.x, offset.y, offset.z);
                }*/
            }
        }

        private void timer_status_Tick(object sender, EventArgs e)
        {
            // clean up old
            foreach (Control ctl in PNL_status.Controls)
            {
                if (!MainV2.Comports.Contains((MAVLinkInterface)ctl.Tag))
                {
                    ctl.Dispose();
                    webBrowser1.Document.InvokeScript("ClearMap");//2015年12月3日09:48:56，清除地图上的标记点
                }
            }

            // setup new
            foreach (var port in MainV2.Comports)
            {
                bool exists = false;
                foreach (Control ctl in PNL_status.Controls)
                {
                    if (ctl is Status && ctl.Tag == port)
                    {
                        exists = true;
                        // MessageBox.Show(MainV2.Comports.ToString());
                        ((Status)ctl).GPS.Text = port.MAV.cs.gpsstatus >= 3 ? "OK" : "Bad";
                        ((Status)ctl).Armed.Text = port.MAV.cs.armed.ToString();
                        ((Status)ctl).Mode.Text = port.MAV.cs.mode;
                        ((Status)ctl).MAV.Text = port.ToString();
                        ((Status)ctl).Guided.Text = port.MAV.GuidedMode.x + "," + port.MAV.GuidedMode.y + "," + port.MAV.GuidedMode.z;
                    }
                }

                if (!exists)
                {
                    Status newstatus = new Status();
                    newstatus.Tag = port;
                    PNL_status.Controls.Add(newstatus);
                }
            }

            //显示各类标记点，如编队内飞行器及脱队飞行器
            foreach (var port in formationComports)
                webBrowser1.Document.InvokeScript("MarkFormation", new object[] { port.MAV.GuidedMode.x, port.MAV.GuidedMode.y, GetSerialString((MAVLinkInterface)port) });//2015年12月3日10:08:38，显示地图标记点
            foreach (var port in outComports)
                webBrowser1.Document.InvokeScript("MarkOut", new object[] { port.MAV.GuidedMode.x, port.MAV.GuidedMode.y, GetSerialString((MAVLinkInterface)port) });//2015年12月3日10:08:38，显示地图标记点


        }


        /*2015年11月12日14:51:17 
    private void trackBar1_Scroll(object sender, EventArgs e)
    {
        foreach (var port in MainV2.Comports)
        {
            Thread thr = new Thread(new ThreadStart(changethr));
            thr.IsBackground = true;
            thr.Start();
        }*/
        /*
        foreach (var port in MainV2.Comports)
        {
            /*
            Thread thr = new Thread(new ThreadStart(changethr));
            thr.IsBackground = true;
            thr.Start();
            */
        /*
        for (int imotor = 1; imotor <= 4; imotor++)
        {
            if (!port.doCommand(MAVLink.MAV_CMD.DO_MOTOR_TEST, (float)imotor, (float)(byte)MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, (float)trackBar1.Value, 5, 0, 0, 0))//doMotorTest(imotor, MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, trackBar1.Value, 5))
            {
                CustomMessageBox.Show("Command was denied by the autopilot");
            }

        }
    }
    }
    */


        private void changethr(object comport)
        {
            var port = (MAVLinkInterface)comport;
            for (int imotor = 1; imotor <= 4; imotor++)
            {
                this.Invoke((EventHandler)(delegate
                {
                    if (!port.doCommand(MAVLink.MAV_CMD.DO_MOTOR_TEST, (float)imotor, (float)(byte)MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, (float)trackBar1.Value, 5, 0, 0, 0))//doMotorTest(imotor, MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, trackBar1.Value, 5))
                    {
                        CustomMessageBox.Show("Command was denied by the autopilot");
                    }
                }));
            }
            /*
            for (int imotor = 1; imotor <= 4; imotor++)
            {
                if (!MainV2.comPort.doMotorTest(imotor, MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, trackBar1.Value, 5))
                {
                    CustomMessageBox.Show("Command was denied by the autopilot");
                }
            }
            */
        }
        /*
        foreach (var port in MainV2.Comports)
        {
            
            Thread thr = new Thread(new ThreadStart(changethr));
            thr.IsBackground = true;
            thr.Start();
            
            for (int imotor = 1; imotor <= 4; imotor++)
            {
                if (!port.doMotorTest(imotor, MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, trackBar1.Value, 5))
                {
                    CustomMessageBox.Show("Command was denied by the autopilot");
                }
            }
        }
        */

        /*2015年11月12日14:51:05*/
        private void trackBar1_KeyUp(object sender, KeyEventArgs e)
        {
            /*
            foreach (var port in MainV2.Comports)
            {              
                for (int imotor = 1; imotor <= 4; imotor++)
                {
                    if (!port.doCommand(MAVLink.MAV_CMD.DO_MOTOR_TEST, (float)imotor, (float)(byte)MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, (float)trackBar1.Value, 5, 0, 0, 0))//doMotorTest(imotor, MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, trackBar1.Value, 5))
                    {
                        CustomMessageBox.Show("Command was denied by the autopilot");
                    }

                }
            }
            */
        }


        private void trackBar1_MouseUp(object sender, MouseEventArgs e)
        {
            /*
            foreach (var port in MainV2.Comports)
            {
                for (int imotor = 1; imotor <= 4; imotor++)
                {
                    if (!port.doCommand(MAVLink.MAV_CMD.DO_MOTOR_TEST, (float)imotor, (float)(byte)MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, (float)trackBar1.Value, 5, 0, 0, 0))//doMotorTest(imotor, MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PWM, trackBar1.Value, 5))
                    {
                        CustomMessageBox.Show("Command was denied by the autopilot");
                    }

                }
            }
            */
        }


        private void takeoff(object p)
        {
            MAVLinkInterface port = (MAVLinkInterface)p;
            float alt = float.Parse(this.textBox1.Text);

            port.setMode("Guided");
            //System.Threading.Thread.Sleep(300);
            if (port.doARM(true))
            {
                System.Threading.Thread.Sleep(200);
                //if(port.MAV.cs.mode == "Guided")
                port.doCommand(MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, alt);
            }
        }


        private void button3_Click(object sender, EventArgs e)
        {
            string mode = comboBox1.SelectedItem.ToString();
            foreach (var port in MainV2.Comports)
            {
                port.setMode(mode);
            }
        }

        private void FormationControl_Load(object sender, EventArgs e)
        {
            string str_url = Application.StartupPath + "\\map.html";
            Uri url = new Uri(str_url);
            webBrowser1.Url = url;
            webBrowser1.ObjectForScripting = this;
        }

        public void PrintLngLat(double lng, double lat)
        {
            //MessageBox.Show(lat + "," + lng);
            toolStripStatusLabel1.Text = "当前坐标：" + lng.ToString("F6") + "," + lat.ToString("F6");

        }

        /// <summary>
        /// 显示右键菜单并获取标记飞行器的SerialString，并设置curChoosePort
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="port"></param>
        public void ShowMenu(int x, int y, string port)
        {
            Point p = new Point(x, y);
            SetCurPort(port);//设置当前任务选择点
            contextMenuStrip1.Show(webBrowser1, p);
        }

        /// <summary>
        /// 根据port获取该飞行器的SerialString
        /// </summary>
        /// <param name="port"></param>
        public string GetSerialString(MAVLinkInterface port)
        {
            return port.MAV.SerialString;
        }


        /// <summary>
        /// 根据SerialString设置当前port
        /// </summary>
        /// <param name="serialString"></param>
        public void SetCurPort(string serialString)
        {
            foreach (var port in formationComports)
            {
                if (port.MAV.SerialString == serialString)
                    curChoosePort = port;
            }
        }

        /// <summary>
        /// 设置当前选择port的任务
        /// </summary>
        /// <param name="model"></param>
        public void SetTask(string model)
        {
            curChoosePort.setMode(model);
            System.Threading.Thread.Sleep(2000);
            if (String.Compare(curChoosePort.MAV.cs.mode, model, true) == 0)
            {
                formationComports.Remove(curChoosePort);
                outComports.Add(curChoosePort);
            }
            else
                MessageBox.Show(model + "设置失败！");
        }


        /// <summary>
        /// 降落
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            //MessageBox.Show(toolStripMenuItem3.Text);

            //SetTask("Land");
        }

        /// <summary>
        /// 返航
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            //SetTask("RTL");
        }

        /// <summary>
        /// 悬停
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToolStripMenuItem5_Click(object sender, EventArgs e)
        {
            //SetTask("Loiter");
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                MAVLink.mavlink_rc_channels_override_t rc = new MAVLink.mavlink_rc_channels_override_t();
                rc.target_component = port.MAV.compid;
                rc.target_system = port.MAV.sysid;
                ushort pwm = (ushort)trackBar1.Value;
                rc.chan3_raw = pwm;
                label1.Text = pwm.ToString();
                port.sendPacket(rc);
            }
        }

        private void myButton3_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                /*
                Thread t = new Thread(new ParameterizedThreadStart(takeoff));
                t.Start(port);
                */
                takeoff(port);
            }
        }

        private void myButton4_Click(object sender, EventArgs e)
        {
            string mode = comboBox1.SelectedItem.ToString();
            foreach (var port in MainV2.Comports)
            {
                port.setMode(mode);
            }
        }

        private void myButton1_Click(object sender, EventArgs e)
        {
            Formation formation = new Formation();

            double[] b = { 126.633247, 45.629651, 3 };

            formation.CollectStartCoords(b);
            formation.StartUTMCoords = formation.GEOsToUTMs(formation.StartGEOCoords, formation.StartGEOCoords[0]);
            formation.BuildEndCoords(0, 20, 3);
            formation.EndGEOCoords = formation.UTMsToGEOs(formation.EndUTMCoords, formation.StartGEOCoords[0]);
            /*
            label3.Text = a.endGEOCoords[0][0] + " " + a.endGEOCoords[0][1] + " " + a.endGEOCoords[0][2] + "";
            MessageBox.Show(a.endGEOCoords[0][0] + " " + a.endGEOCoords[0][1] + " " + a.endGEOCoords[0][2] + "");
            */

            //设计完线路在执行该对策之前，对对策队列“查重”，若有重复，则弃用该队列，执行最简单的贪心算法，保证无失事
            {

            }

            int[] wingRoute = formation.DesignPath(MainV2.Comports.Count);
            int[] formationRoute = new int[wingRoute.Length + 1];//i存储初始编队编号,formationRoute[i]对应目的编号，相对于wingRoute，多了个长机（0位置）
            for (int i = 0; i < wingRoute.Length; i++)
            {
                for (int j = 0; j < wingRoute.Length; j++)
                {
                    if (wingRoute[j] == i)
                        formationRoute[i + 1] = j + 1;
                }
            }

            //任务list添加任务、坐标list添加坐标，编制任务list
            planList.Add(BuildTaskList("guide"));
            planList.Add(BuildTaskList(formationRoute, MAVLink.MAV_CMD.WAYPOINT, formation.EndGEOCoords, WPStyle.OnlyHigher));
            planList.Add(BuildTaskList(formationRoute, MAVLink.MAV_CMD.WAYPOINT, formation.EndGEOCoords, WPStyle.WayPoint));
            planList.Add(BuildTaskList(formationRoute, MAVLink.MAV_CMD.WAYPOINT, formation.EndGEOCoords, WPStyle.KeepLevel));

            //开启执行线程,每当任务规划列表内完成某一段任务时才继续执行下一任务            
            while (planList.Count > 0)
            {
                //开启监测线程
                Thread checkThread = new Thread(new ThreadStart(CheckState));
                Thread doTaskThread = new Thread(new ParameterizedThreadStart(DoPlan));
                checkThread.Start();
                doTaskThread.Start(planList[0]);
                checkThread.Join();//在监测线程完成任务即编队任务完成时才继续执行下一任务点
            }
            
        }

        public class FormationTask
        {
            public TaskStyle taskStyle { get; set; }
            public MAVLinkInterface taskPort { get; set; }//or serialString
            public MAVLink.MAV_CMD cmd { get; set; }//长时间不执行时再次发送此任务命令
            public float[] param { get; set; }
            public string model { get; set; }
        }

        private const double EARTH_RADIUS = 6378.137;//地球半径
        private static double rad(double d)
        {
            return d * Math.PI / 180.0;
        }

        /// <summary>
        /// 根据GEO坐标测算两点间距
        /// </summary>
        /// <param name="lat1"></param>
        /// <param name="lng1"></param>
        /// <param name="lat2"></param>
        /// <param name="lng2"></param>
        /// <returns></returns>
        public static double GetDistance(double lat1, double lng1, double lat2, double lng2)
        {
            double radLat1 = rad(lat1);
            double radLat2 = rad(lat2);
            double a = radLat1 - radLat2;
            double b = rad(lng1) - rad(lng2);

            double s = 2 * Math.Asin(Math.Sqrt(Math.Pow(Math.Sin(a / 2), 2) +
             Math.Cos(radLat1) * Math.Cos(radLat2) * Math.Pow(Math.Sin(b / 2), 2)));
            s = s * EARTH_RADIUS;
            s = Math.Round(s * 1000, 2);
            return s;
        }

        /// <summary>
        /// 监测同一阶段的编队飞行任务，完成该阶段后删除该任务点并返回，
        /// 目前支持飞行点、高度变换、模式变换；
        /// 后期可加入角度偏航变换
        /// </summary>
        private void CheckState()
        {
            if (planList.Count > 0)
            {
                int runNum = 0;
                List<FormationTask> taskPlanPoint = planList[0];
                while (taskPlanPoint.Count > 0)
                {
                    System.Threading.Thread.Sleep(200);

                    //目标达到任务期望位置后应该改为悬停模式（飞行时默认只有悬停模式是“最安全”的）
                    //实验cmd命令执行后飞行器处于什么状态，是否自动转至悬停

                    for (int i = 0; i < taskPlanPoint.Count; i++)
                    {
                        FormationTask ft = taskPlanPoint[i];

                        if (ft.taskStyle == TaskStyle.Point &&
                            GetDistance(ft.param[4], ft.param[5], ft.taskPort.MAV.GuidedMode.x, ft.taskPort.MAV.GuidedMode.y) < 4e-1)//任务点到达，根据距离误差？
                        {
                            taskPlanPoint.Remove(ft);
                            i--;
                        }
                        else if (ft.taskStyle == TaskStyle.Height && ft.param[7] - ft.taskPort.MAV.GuidedMode.z < 2e-1)
                        {
                            taskPlanPoint.Remove(ft);
                            i--;
                        }
                        else if (ft.taskStyle == TaskStyle.Model && String.Compare(ft.model, ft.taskPort.MAV.cs.mode, true) == 0)
                        {
                            taskPlanPoint.Remove(ft);
                            i--;
                        }
                    }
                    runNum++;
                }
                planList.Remove(taskPlanPoint);
            }
        }

        /// <summary>
        /// 编制模式任务
        /// </summary>
        /// <param name="comport">执行任务端口</param>
        /// <param name="model">模式</param>
        /// <returns>返回任务</returns>
        private FormationTask BuildTask(MAVLinkInterface comport, string model)
        {
            FormationTask singleTask = new FormationTask();
            singleTask.taskPort = comport;
            singleTask.taskStyle = TaskStyle.Model;
            singleTask.model = model;
            return singleTask;
        }

        /// <summary>
        /// 编制任务点任务
        /// </summary>
        /// <param name="comport">执行任务端口</param>
        /// <param name="cmd">指令</param>
        /// <param name="param">参数</param>
        /// <returns>返回任务</returns>
        private FormationTask BuildTask(MAVLinkInterface comport, MAVLink.MAV_CMD cmd, float[] param)
        {
            FormationTask singleTask = new FormationTask();
            if (param[6] == 0)
                singleTask.taskStyle = TaskStyle.Point;
            else
                singleTask.taskStyle = TaskStyle.Height;
            singleTask.taskPort = comport;
            singleTask.cmd = cmd;
            singleTask.param = param;
            return singleTask;
        }

        /// <summary>
        /// 编制单任务（特指模式变更任务）的编队列表
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public List<FormationTask> BuildTaskList(string model)
        {
            List<FormationTask> taskList = new List<FormationTask>();
            foreach (MAVLinkInterface port in MainV2.Comports)
            {
                taskList.Add(BuildTask(port, model));
            }
            return taskList;
        }

        /// <summary>
        /// 编制单任务（特指飞行点任务）的编队列表
        /// 不能保证port和formationRoute中的port对应，所以参数需要单独设置port及FormationTask
        /// </summary>
        /// <param name="formationRoute">编队内各飞行器存储的目标位置</param>
        /// <param name="cmd"></param>
        /// <param name="endGEOCoords"></param>
        /// <param name="paramOption">0：只改变高度 1：只改变经纬度</param>
        /// <returns></returns>
        public List<FormationTask> BuildTaskList(int[] formationRoute, MAVLink.MAV_CMD cmd, List<double[]> endGEOCoords, WPStyle paramOption)
        {
            int i = 0;
            List<FormationTask> taskList = new List<FormationTask>();
            float[] tempParam = new float[7];
            foreach (MAVLinkInterface port in MainV2.Comports)
            {
                switch (paramOption)
                {
                    case WPStyle.OnlyHigher://只改变高度
                        tempParam[6] = (float)endGEOCoords[i][2] + i;//高度alt,各僚机高度不同
                        break;
                    case WPStyle.KeepLevel:
                        tempParam[6] = (float)endGEOCoords[i][2];//高度alt,编队恢复原高度
                        break;
                    case WPStyle.WayPoint://只改变经纬度
                        tempParam[4] = (float)endGEOCoords[i][1];//纬度lat
                        tempParam[5] = (float)endGEOCoords[i][0];//经度lng
                        break;
                }

                taskList.Add(BuildTask(port, cmd, tempParam));
                i++;
            }
            return taskList;
        }

        /// <summary>
        /// 执行任务计划（针对某一阶段的编队）
        /// </summary>
        /// <param name="taskPlanPoint"></param>
        private void DoPlan(object taskPlanPoint)
        {
            foreach (FormationTask taskPoint in (FormationTask[])taskPlanPoint)
            {
                if (taskPoint.taskStyle == TaskStyle.Model)
                    taskPoint.taskPort.setMode(taskPoint.model);
                else
                    taskPoint.taskPort.doCommand(taskPoint.cmd, taskPoint.param[0], taskPoint.param[1], taskPoint.param[2], taskPoint.param[3], taskPoint.param[4], taskPoint.param[5], taskPoint.param[6]);
            }
        }
        
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown1.Value < 3 || numericUpDown1.Value > 20)
            {
                MessageBox.Show("请在编队间距内输入3到20以内的数值");
                numericUpDown1.Value = 3;
            }
        }

        private void label1_DoubleClick(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                MAVLink.mavlink_rc_channels_override_t rc = new MAVLink.mavlink_rc_channels_override_t();
                rc.target_component = port.MAV.compid;
                rc.target_system = port.MAV.sysid;
                ushort pwm = 0;
                rc.chan3_raw = pwm;
                label1.Text = pwm.ToString();
                port.sendPacket(rc);
            }
        }
    }
}
