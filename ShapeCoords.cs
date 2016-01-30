using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MissionPlanner.Swarm
{
    class ShapeCoords
    {
        /// <summary>
        /// 求解直线型队形下各飞行器的UTM坐标
        /// leader为正东方位，坐标为(r,0)
        /// </summary>
        /// <param name="dis">两飞行器间距</param>
        /// <param name="pointNum">飞行器个数</param>
        /// <param name="leaderCoords">leader的UTM坐标</param>
        /// <returns>该直线形下的各飞行器UTM坐标</returns>
        public List<double[]> LineShape(double dis, int pointNum, double[] leaderCoords)
        {
            List<double[]> tempEndUTMCoords = new List<double[]>();
            for (int i = 0; i < pointNum; i++)
            {
                double[] tempCoors = new double[3];
                tempCoors[0] = leaderCoords[0] - dis * i;//以东坐标（经度lng）
                tempCoors[1] = leaderCoords[1];//以北坐标（纬度lat）
                tempCoors[2] = leaderCoords[2];//高度
                tempEndUTMCoords.Add(tempCoors);
            }
            return tempEndUTMCoords;
        }

        /// <summary>
        /// 求解正多边形下各飞行器的UTM坐标
        /// leader为正东方位，队形中心为坐标的原点，则leader的坐标为(r,0)
        /// </summary>
        /// <param name="dis">两飞行器间距</param>
        /// <param name="pointNum">飞行器个数</param>
        /// <param name="leaderCoords">leader的UTM坐标</param>
        /// <returns>该正多边形下的各飞行器UTM坐标</returns>
        public List<double[]> RegularPolygonShape(double dis, int pointNum, double[] leaderCoords)
        {
            List<double[]> tempEndUTMCoords = new List<double[]>();
            double r = dis / 2 / Math.Sin(Math.PI / pointNum);
            for (int i = 0; i < pointNum; i++)
            {
                double[] tempCoors = new double[3];
                double innerAngle = 360 / pointNum * i;
                double X = r * Math.Cos(Math.PI * (innerAngle) / 180);
                double Y = r * Math.Sin(Math.PI * (innerAngle) / 180);
                tempCoors[0] = leaderCoords[0] - r + X;//以东坐标（经度lng）
                tempCoors[1] = leaderCoords[1] + Y;//以北坐标（纬度lat）
                tempCoors[2] = leaderCoords[2];//高度
                tempEndUTMCoords.Add(tempCoors);
            }
            return tempEndUTMCoords;
        }
        
        /// <summary>
        /// 求解人形下各飞行器的UTM坐标，默认夹角为120°
        /// 若飞行器数量为奇数，则leader为人字形领头,leader坐标为（0,0）；若飞行器数量为偶数，则leader和另一僚机为领头,相距为安全距离，leader坐标为（0,0）
        /// </summary>
        /// <param name="dis">两飞行器间距</param>
        /// <param name="pointNum">飞行器个数</param>
        /// <param name="leaderCoords">leader的UTM坐标</param>
        /// <returns>该人字形下的各飞行器UTM坐标</returns>
        public List<double[]> GooseShape(double dis, int pointNum, double[] leaderCoords)
        {
            List<double[]> tempEndUTMCoords = new List<double[]>();
            if (pointNum % 2 == 0)//偶数个飞行器
            {
                for (int i = 0; i < pointNum / 2; i++)
                {
                    double[] tempCoors = new double[3];
                    double X = i * dis * Math.Cos(Math.PI * 60 / 180);
                    double Y = i * dis * Math.Sin(Math.PI * 60 / 180);
                    tempCoors[0] = leaderCoords[0] - X;//以东坐标（经度lng）
                    tempCoors[1] = leaderCoords[1] + Y;//以北坐标（纬度lat）
                    tempCoors[2] = leaderCoords[2];//高度
                    tempEndUTMCoords.Add(tempCoors);
                }
                for (int i = pointNum / 2, j = 0; i < pointNum; i++, j++)
                {
                    double[] tempCoors = new double[3];
                    double X = j * dis * Math.Cos(Math.PI * 60 / 180);
                    double Y = j * dis * Math.Sin(Math.PI * 60 / 180);
                    tempCoors[0] = leaderCoords[0] - X;//以东坐标（经度lng）
                    tempCoors[1] = leaderCoords[1] - Y - (new Formation()).SafeDis;//以北坐标（纬度lat）
                    tempCoors[2] = leaderCoords[2];//高度
                    tempEndUTMCoords.Add(tempCoors);
                }
            }
            else//奇数个飞行器
            {
                for (int i = 0; i < pointNum / 2 + 1; i++)
                {
                    double[] tempCoors = new double[3];
                    double X = i * dis * Math.Cos(Math.PI * 60 / 180);
                    double Y = i * dis * Math.Sin(Math.PI * 60 / 180);
                    tempCoors[0] = leaderCoords[0] - X;//以东坐标（经度lng）
                    tempCoors[1] = leaderCoords[1] + Y;//以北坐标（纬度lat）
                    tempCoors[2] = leaderCoords[2];//高度
                    tempEndUTMCoords.Add(tempCoors);
                }
                for (int i = pointNum / 2 + 1, j = 1; i < pointNum; i++, j++)
                {
                    double[] tempCoors = new double[3];
                    double X = j * dis * Math.Cos(Math.PI * 60 / 180);
                    double Y = j * dis * Math.Sin(Math.PI * 60 / 180);
                    tempCoors[0] = leaderCoords[0] - X;//以东坐标（经度lng）
                    tempCoors[1] = leaderCoords[1] - Y;//以北坐标（纬度lat）
                    tempCoors[2] = leaderCoords[2];//高度
                    tempEndUTMCoords.Add(tempCoors);
                }
            }
            return tempEndUTMCoords;
        }


    }
}
