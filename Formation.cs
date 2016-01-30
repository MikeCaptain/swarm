using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using System.Text;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using ProjNet.Converters;
using MissionPlanner.Utilities;
using MissionPlanner;

namespace MissionPlanner.Swarm
{
    /// <summary>
    /// Follow the leader
    /// </summary>
    class Formation: Swarm
    {

        Dictionary<MAVLinkInterface, HIL.Vector3> offsets = new Dictionary<MAVLinkInterface, HIL.Vector3>();
        
        PointLatLngAlt masterpos = new PointLatLngAlt();

        public void setOffsets(MAVLinkInterface mav, double x, double y, double z)
        {
            offsets[mav] = new HIL.Vector3(x,y,z);
            //log.Info(mav.ToString() + " " + offsets[mav].ToString());
        }

        public HIL.Vector3 getOffsets(MAVLinkInterface mav)
        {
            if (offsets.ContainsKey(mav))
            {
                return offsets[mav];
            }

            return new HIL.Vector3(offsets.Count, 0, 0);
        }

        public override void Update()
        {
            if (MainV2.comPort.MAV.cs.lat == 0 || MainV2.comPort.MAV.cs.lng == 0)
                return;
            if (Leader == null)
                Leader = MainV2.comPort;

            masterpos = new PointLatLngAlt(Leader.MAV.cs.lat, Leader.MAV.cs.lng, Leader.MAV.cs.alt, "");
        }

        public override void SendCommand()
        {
            if (masterpos.Lat == 0 || masterpos.Lng == 0)
                return;
            Console.WriteLine(DateTime.Now);
            Console.WriteLine("Leader {0} {1} {2}",masterpos.Lat,masterpos.Lng,masterpos.Alt);

            int a = 0;
            foreach (var port in MainV2.Comports)
            {
                if (port == Leader) 
                    continue;

                PointLatLngAlt target = new PointLatLngAlt(masterpos);
                //MessageBox.Show(target.Lat.ToString() + "/r/n" + target.Lng.ToString(),"fir");
                try
                {
                    //convert Wgs84ConversionInfo to utm
                    CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();

                    GeographicCoordinateSystem wgs84 = GeographicCoordinateSystem.WGS84;

                    int utmzone = (int)((masterpos.Lng - -186.0) / 6.0);

                    IProjectedCoordinateSystem utm = ProjectedCoordinateSystem.WGS84_UTM(utmzone, masterpos.Lat < 0 ? false : true);

                    ICoordinateTransformation trans = ctfac.CreateFromCoordinateSystems(wgs84, utm);

                    double[] pll1 = { target.Lng, target.Lat };

                    double[] p1 = trans.MathTransform.Transform(pll1);

                    // add offsets to utm
                    p1[0] += ((HIL.Vector3)offsets[port]).x;//offsets为follwer和leader的UTM坐标间距，connect时设置了
                    p1[1] += ((HIL.Vector3)offsets[port]).y;

                    // convert back to wgs84
                    IMathTransform inversedTransform = trans.MathTransform.Inverse();
                    double[] point = inversedTransform.Transform(p1);

                    target.Lat = point[1];
                    target.Lng = point[0];
                    target.Alt += ((HIL.Vector3)offsets[port]).z;
                    //MessageBox.Show(target.Lat.ToString() + "/r/n" + target.Lng.ToString(),"sec");

                    port.setGuidedModeWP(new Locationwp() { alt = (float)target.Alt, lat = target.Lat, lng = target.Lng, id = (byte)MAVLink.MAV_CMD.WAYPOINT });

                    Console.WriteLine("{0} {1} {2} {3}", port.ToString(), target.Lat, target.Lng, target.Alt);

                }
                catch (Exception ex) { Console.WriteLine("Failed to send command " + port.ToString() + "\n" + ex.ToString()); }

                a++;
            }


        }


        //lng、lat、high
        private static List<double[]> startGEOCoords = new List<double[]>();//存储飞行器编队的起始GEO坐标
        public List<double[]> StartGEOCoords
        {
            get
            {
                return startGEOCoords;
            }
            set
            {
                startGEOCoords = value;
            }
        }

        private static List<double[]> endGEOCoords = new List<double[]>();//存储飞行器编队的最终GEO坐标
        public List<double[]> EndGEOCoords
        {
            get
            {
                return endGEOCoords;
            }
            set
            {
                endGEOCoords = value;
            }
        }

        private static List<double[]> startUTMCoords = new List<double[]>();//存储飞行器编队的起始UTM坐标
        public List<double[]> StartUTMCoords
        {
            get
            {
                return startUTMCoords;
            }
            set
            {
                startUTMCoords = value;
            }
        }

        private static List<double[]> endUTMCoords = new List<double[]>();//存储飞行器编队的最终UTM坐标
        public List<double[]> EndUTMCoords
        {
            get
            {
                return endUTMCoords;
            }
            set
            {
                endUTMCoords = value;
            }
        }

        private double safeDis;//安全间距为3m
        public double SafeDis
        {
            get
            {
                return safeDis;
            }
            private set
            {
                safeDis = 3;
            }
        }

        private bool[] S, T;//S为true指X集合在交错路上的点，T为true集合指Y集合在交错路上的点
        private bool[,] map;//表示二分图的相等子图
        private int[] resultX;//存储与X集合相对应的Y序号位置，是算法最终的输出结果,i表示Y集合，resultX[i]表示对应的X集合

        private double[,] distance;//存储UTM坐标系下的各点间距
        public double[,] Distance
        {
            get
            {
                return distance;
            }
            set
            {
                distance = value;
            }
        }

        /// <summary>
        /// 外部调用该函数，将编队的飞行器坐标加入startCoords队列中
        /// </summary>
        public void CollectStartCoords(double[] startCoords)
        {
            startGEOCoords.Add(startCoords);
        }

        /// <summary>
        /// 将GEO指标转换为UTM坐标
        /// </summary>
        /// <param name="tempGEOCoordsList">需要转换的初始GEO坐标</param>
        /// <param name="leader">leader坐标</param>
        /// <returns>被转换的UTM坐标</returns>
        public List<double[]> GEOsToUTMs(List<double[]> tempGEOCoordsList, double[] leader)
        {
            CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();

            GeographicCoordinateSystem wgs84 = GeographicCoordinateSystem.WGS84;

            int utmzone = (int)((leader[0] - -186.0) / 6.0);

            IProjectedCoordinateSystem utm = ProjectedCoordinateSystem.WGS84_UTM(utmzone, leader[1] < 0 ? false : true);//true为北半球

            ICoordinateTransformation trans = ctfac.CreateFromCoordinateSystems(wgs84, utm);//GEO转为UTM

            List<double[]> tempUTMCoordsList = new List<double[]>();
            for (int i = 0; i < startGEOCoords.Count; i++)
            {
                double[] tempGEOCoords = { startGEOCoords[i][0], startGEOCoords[i][1] };
                double[] tempUTMCoords = trans.MathTransform.Transform(tempGEOCoords);
                double[] tempUTMCoords3 = { tempUTMCoords[0], tempUTMCoords[1], tempGEOCoordsList[i][2] };
                tempUTMCoordsList.Add(tempUTMCoords3);
            }
            return tempUTMCoordsList;
        }

        /// <summary>
        /// 将UTM坐标转为GEO坐标
        /// </summary>
        /// <param name="tempUTMCoordsList">被转换的UTM坐标</param>
        /// <param name="leader">leader坐标</param>
        /// <returns>转换后的GEO坐标</returns>
        public List<double[]> UTMsToGEOs(List<double[]> tempUTMCoordsList, double[] leader)
        {
            CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();

            GeographicCoordinateSystem wgs84 = GeographicCoordinateSystem.WGS84;

            int utmzone = (int)((leader[0] - -186.0) / 6.0);

            IProjectedCoordinateSystem utm = ProjectedCoordinateSystem.WGS84_UTM(utmzone, leader[1] < 0 ? false : true);//true为北半球

            ICoordinateTransformation trans = ctfac.CreateFromCoordinateSystems(utm, wgs84);//UTM转为GEO

            List<double[]> tempGEOCoordsList = new List<double[]>();
            for (int i = 0; i < tempUTMCoordsList.Count; i++)
            {
                double[] tempUTMCoors = { tempUTMCoordsList[i][0], tempUTMCoordsList[i][1] };
                double[] tempGEOCoors = trans.MathTransform.Transform(tempUTMCoors);
                double[] tempGEOCoors3 = { tempGEOCoors[0], tempGEOCoors[1], tempUTMCoordsList[i][2] };
                tempGEOCoordsList.Add(tempGEOCoors3);
            }
            return tempGEOCoordsList;
        }

        /// <summary>
        /// 根据形状、半径、数量及leader的UTM坐标确定其余各飞行器的UTM坐标
        /// </summary>
        /// <param name="shape">形状</param>
        /// <param name="dis">半径</param>
        /// <param name="pointNum">数量</param>
        public void BuildEndCoords(int shape, double dis, int pointNum)
        {
            ShapeCoords sc = new ShapeCoords();
            double[] leaderUTM = StartUTMCoords[0];
            switch (shape)
            {
                case 0://正多边形
                    endUTMCoords = sc.RegularPolygonShape(dis, pointNum, leaderUTM);
                    break;
                case 1://一字形
                    endUTMCoords = sc.LineShape(dis, pointNum, leaderUTM);
                    break;
                case 2://人字形
                    endUTMCoords = sc.GooseShape(dis, pointNum, leaderUTM);
                    break;
                default:
                    break;
            }
        }


        /// <summary>
        /// 将UTM的起始和终点坐标间的距离赋值给二维数组distance
        /// </summary>
        /// <param name="tempStartUTMCoords">起始UTM坐标队列</param>
        /// <param name="tempEndUTMCoords">终点UTM坐标队列</param>
        /// <returns>返回距离的二维数组</returns>
        public double[,] GetUTMDistance(List<double[]> tempStartUTMCoords, List<double[]> tempEndUTMCoords)
        {
            double[,] tempDistance = new double[tempStartUTMCoords.Count - 1, tempStartUTMCoords.Count - 1];
            for (int x = 0; x < tempDistance.GetLength(0); x++)//distance的行数
            {
                for (int y = 0; y < tempDistance.GetLength(1); y++)//distance的列数
                {
                    tempDistance[x, y] = Math.Round(Math.Sqrt(Math.Pow(tempStartUTMCoords[x + 1][0] - tempEndUTMCoords[y + 1][0], 2) + Math.Pow(tempStartUTMCoords[x + 1][1] - tempEndUTMCoords[y + 1][1], 2)), 2);//取小数点后两位精度
                }
            }
            return tempDistance;
        }

        /// <summary>
        /// 僚机的路径规划，即startCoords与endCoords的对应连线，应用KM算法
        /// </summary>
        /// <param name="size">size表示僚机个数</param>
        /// <returns>僚机的最优路径对应点</returns>
        public int[] DesignPath(int size)
        {
            S = new bool[size];
            T = new bool[size];//S为true指X集合在交错路上的点，T为true集合指Y集合在交错路上的点
            map = new bool[size, size];//表示二分图的相等子图
            resultX = new int[size];//存储与X集合相对应的Y序号位置，是算法最终的输出结果
            double[] lx = new double[size], ly = new double[size];//lx存储X集合可行顶标，ly存储Y集合可行顶标

            //初始化可行标杆
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    if (distance[i, j] > lx[i])
                    {
                        lx[i] = distance[i, j];
                        resultX[i] = j;
                    }
                }
            }

            //初始可行标杆无重复值则说明其已为完美匹配，执行调动
            if (!CheckRepeat(resultX))
            {
                return resultX;
            }
            for (int i = 0; i < size; i++)
                resultX[i] = -1;//resultX初始无匹配项

            //用匈牙利算法寻找完备匹配
            for (int x = 0; x < size; x++)
            {
                double min = 999999;
                while (true)
                {
                    //初始化邻接矩阵
                    for (int i = 0; i < size; i++)
                    {
                        for (int j = 0; j < size; j++)
                        {
                            if (lx[i] + ly[j] == distance[i, j])
                                map[i, j] = true;
                            else
                                map[i, j] = false;
                        }
                    }
                    for (int i = 0; i < size; i++)
                    {
                        S[i] = false;
                        T[i] = false;
                    }
                    if (DFSHungarian(x, size))
                        break;
                    for (int i = 0; i < size; i++)
                    {
                        if (S[i])
                        {
                            for (int j = 0; j < size; j++)
                            {
                                if (!T[j] && lx[i] + ly[j] - distance[i, j] > 0)
                                {
                                    min = Math.Min(lx[i] + ly[j] - distance[i, j], min);
                                }
                            }
                        }
                    }
                    for (int i = 0; i < size; i++)
                    {
                        if (S[i])
                            lx[i] -= min;
                        if (T[i])
                            ly[i] += min;
                    }
                }
            }
            return resultX;
        }

        /// <summary>
        /// 检查array数组内是否有重复值，若存在则返回true，否则返回false
        /// </summary>
        /// <param name="array">要检查的int数组</param>
        /// <returns></returns>
        public bool CheckRepeat(int[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                for (int j = i + 1; j < array.Length; j++)
                {
                    if (array[i] == array[j])
                        return true;
                }
            }
            return false;
        }


        /// <summary>
        /// 深度优先匈牙利算法
        /// </summary>
        private bool DFSHungarian(int x, int size)
        {
            S[x] = true;
            for (int j = 0; j < size; j++)
            {
                if (!T[j] && map[x, j])
                {
                    T[j] = true;
                    if (resultX[j] == -1 || DFSHungarian(resultX[j], size))
                    {
                        resultX[j] = x;
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
