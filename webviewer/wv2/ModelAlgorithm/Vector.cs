namespace WP_GUI.ModelAlgorithm
{
    public class Vector2D
    {
        public double x;
        public double y;

        public Vector2D(double x, double y)
        {
            this.x = x; this.y = y;
        }

        public static double norm(Vector2D v1)
        {
            return Math.Sqrt(v1.x * v1.x + v1.y * v1.y);
        }

        public static double Angle(Vector2D v1, Vector2D v2)
        {
            double cross_ab = v1.x * v2.y - v1.y * v2.x;
            double dot_ab = v1.x * v2.x + v1.y * v2.y;
            return Math.Atan2(Math.Abs(cross_ab), dot_ab) / Math.PI * 180;
        }

        public static Vector2D rotz(Vector2D u, double theta)
        {
            theta = theta * 3.1415926 / 180;
            double cos_theta = Math.Cos(theta);
            double sin_theta = Math.Sin(theta);

            double x = cos_theta * u.x - sin_theta * u.y;
            double y = sin_theta * u.x + cos_theta * u.y;


            return new Vector2D(x, y);
        }

        public static Vector2D operator +(Vector2D v1, Vector2D v2)
        {
            return new Vector2D(v1.x + v2.x, v1.y + v2.y);
        }

        public static Vector2D operator -(Vector2D v1, Vector2D v2)
        {
            return new Vector2D(v1.x - v2.x, v1.y - v2.y);
        }

        public static double operator *(Vector2D v1, Vector2D v2)
        {
            return (v1.x * v2.x + v1.y * v2.y);
        }

        // not operator cross

    }

    public class Vector3D
    {
        public double x;
        public double y;
        public double z;

        public Vector3D(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static double norm(Vector3D v)
        {
            return Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }

        public static Vector3D operator +(Vector3D v1, Vector3D v2)
        {
            return new Vector3D(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
        }

        public static Vector3D operator -(Vector3D v1, Vector3D v2)
        {
            return new Vector3D(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);
        }

        public static double operator *(Vector3D v1, Vector3D v2)
        {
            return (v1.x * v2.x + v1.y * v2.y + v1.z * v2.z);
        }

        public static Vector3D cross(Vector3D v1, Vector3D v2)
        {
            double x = v1.y * v2.z - v1.z * v2.y;
            double y = v1.z * v2.x - v1.x * v2.z;
            double z = v1.x * v2.y - v1.y * v2.x;
            return new Vector3D(x, y, z);
        }
    }
}
