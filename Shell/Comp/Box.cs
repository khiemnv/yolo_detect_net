using annotation;
using System;
using System.Diagnostics;
using Yolov;

namespace comp
{
    public class Box : PredictionBox, IComparable, IBox
    {
        public int LabelId { get => base.label.id; set => base.label.id = value; }
        public float Score { get => base.score; set => base.score = value; }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            var b = obj as Box;
            var a = this;
            var ret = a.LabelName.CompareTo(b.LabelName);
            if (ret != 0)
            {
                return ret;
            }

            var o = a.Overlap(b);
            if (o > 0.6)
            {
                return 0;
            }

            ret = a.X.CompareTo(b.X);
            if (ret != 0)
            {
                return ret;
            }

            ret = a.Y.CompareTo(b.Y);
            if (ret != 0)
            {
                return ret;
            }

            ret = a.W.CompareTo(b.W);
            if (ret != 0)
            {
                return ret;
            }

            ret = a.H.CompareTo(b.H);
            if (ret != 0)
            {
                return ret;
            }

            Debug.Assert(false); // un expected
            return 1;
        }

        public override string ToString()
        {
            return label.name + $" {X} {Y}";
        }

        public static implicit operator string(Box p) => p.ToString();
    }

}
