using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurfaceInterpolation.Tools.SBRep.Utils
{
    /// <summary>
    /// Представляет функцию вида Ax + By + Cz + D = 0
    /// </summary>
    public struct PlaneFace
    {
        public const double EPS_PointOnPlane = 1e-3;
        public double A, B, C, D;
        public Vector3d Normal;

        /// <summary>
        /// расстояние от точки до плоскости
        /// https://ru.onlinemschool.com/math/library/analytic_geometry/p_plane/#:~:text=Расстояние%20от%20точки%20до%20плоскости%20—%20равно%20длине%20перпендикуляра,опущенного%20из%20точки%20на%20плоскость.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public double Distance(Vector3d point)
        {
            var dividend = Math.Abs(A * point.x + B * point.y + C * point.z + D);
            var devider = Math.Sqrt(A * A + B * B + C * C);
            return dividend / devider;
        }

        /// <summary>
        /// Точка на плоскости
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool PointOnPlane(Vector3d point)
        {
            return Distance(point) < EPS_PointOnPlane;
        }

        public double GetZ(double x, double y)
        {
            return (-A * x - B * y - D) / C;
        }

        /// <summary>
        /// Создаёт уравнение плоскости по трём точкам
        /// http://algolist.ru/maths/geom/equation/plane.php
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static PlaneFace FromPoints(
            Vector3d p1,
            Vector3d p2,
            Vector3d p3,
            Vector3d normal)
        {
            double a, b, c, d;
            a = p1.y * (p2.z - p3.z) + p2.y * (p3.z - p1.z) + p3.y * (p1.z - p2.z);
            b = p1.z * (p2.x - p3.x) + p2.z * (p3.x - p1.x) + p3.z * (p1.x - p2.x);
            c = p1.x * (p2.y - p3.y) + p2.x * (p3.y - p1.y) + p3.x * (p1.y - p2.y);
            d = (p1.x * (p2.y * p3.z - p3.y * p2.z) + p2.x * (p3.y * p1.z - p1.y * p3.z) + p3.x * (p1.y * p2.z - p2.y * p1.z)) * -1.0;

            return new PlaneFace()
            {
                A = a,
                B = b,
                C = c,
                D = d,
                Normal = normal
            };
        }

        public override string ToString()
        {
            var signA = Math.Sign(A) >= 0 ? "" : "-";
            var signB = Math.Sign(B) >= 0 ? "+" : "-";
            var signC = Math.Sign(C) >= 0 ? "+" : "-";
            var signD = Math.Sign(D) >= 0 ? "+" : "-";
            var strA = Math.Round(Math.Abs(A), 2).ToString();
            var strB = Math.Round(Math.Abs(B), 2).ToString();
            var strC = Math.Round(Math.Abs(C), 2).ToString();
            var strD = Math.Round(Math.Abs(D), 2).ToString();
            return $"{signA} {strA}x {signB} {strB}y {signC} {strC}z {signD} {strD} = 0";
        }
    }
}
