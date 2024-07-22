using System.Collections.Generic;
//using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace annotation
{
    public class ColorPalette
    {
        public class rgb
        {
            public int r;
            public int g;
            public int b;

            public rgb(int r, int g, int b)
            {
                this.r = r;
                this.g = g;
                this.b = b;
            }

        }

        public class Brush
        {
            public rgb color;
            public static Brush Red {get=> new Brush(); }
        }
        public Dictionary<string, Brush> d = new Dictionary<string, Brush>();

        public void Init(LabelConfig lcfg)
        {
#if false
            var solidColorBrushList = new List<Brush>()
            {
                    new SolidBrush(Color.FromArgb(255,27,161,226)),
                    new SolidBrush(Color.FromArgb(255,160,80,0)),
                    new SolidBrush(Color.FromArgb(255, 51, 153, 51)),
                    new SolidBrush(Color.FromArgb(255, 162, 193, 57)),
                    new SolidBrush(Color.FromArgb(255, 216, 0, 115)),
                    new SolidBrush(Color.FromArgb(255, 240, 150, 9)),
                    new SolidBrush(Color.FromArgb(255, 230, 113, 184)),
                    new SolidBrush(Color.FromArgb(255, 162, 0, 255)),
                    new SolidBrush(Color.FromArgb(255, 229, 20, 0)),
                    new SolidBrush(Color.FromArgb(255, 0, 171, 169)),
            };
#endif
            Brush cb(rgb c) { return new Brush { color = c }; }
            var okLst = new List<rgb> {
                new rgb(0, 0, 255),
                    new rgb(0, 226, 0),
                    new rgb(255,127,80),
                    new rgb(255,20,147),
                    new rgb( 162, 0, 255),
                    new rgb( 0, 171, 169),
                    new rgb(255, 233, 71),


                    new rgb(27,161,226),
                    new rgb(160,80,0),
                    new rgb( 51, 153, 51),
                    new rgb( 240, 150, 9),
                    new rgb(148,0,211),
                    new rgb( 230, 113, 184),
                    new rgb(100, 111, 0),
                    new rgb(0, 255, 255),
                    new rgb(139,69,19),
                    new rgb( 162, 193, 57),
                    new rgb(218,165,32),
                    new rgb(32,178,170),

                    new rgb( 229, 20, 0),
                    new rgb(255, 187, 92),
                    new rgb(255, 102, 0),
                    new rgb(255, 0, 255),
                    new rgb(153, 0, 0),
                    new rgb( 216, 0, 115),
                    new rgb(144, 80, 255),
                    new rgb(139,0,139),
                    new rgb(218,165,32),
                    new rgb(226, 94, 62) ,
                    new rgb(148, 78, 99),
                    new rgb(106, 156, 137),
                    new rgb(152,251,152),
                    new rgb(145, 149, 246),
                    new rgb(255,182,193),

                    new rgb(165, 221, 155),
                    new rgb(197, 235, 170),
                    new rgb(205, 250, 219),
                    new rgb(220, 255, 183),
                    new rgb(242, 193, 141),
                    new rgb(244, 83, 138),
                    new rgb(245, 221, 97),
                    new rgb(246, 241, 147),
                    new rgb(246, 253, 195),
                    new rgb(249, 240, 122),
                    new rgb(250, 163, 0),
                    new rgb(251, 136, 180),
                    new rgb(255, 128, 128),
                    new rgb(255, 187, 100),
                    new rgb(255, 207, 150),
                    new rgb(255, 234, 167),
                    new rgb(89, 213, 224),
                new rgb(27,161,226),
                new rgb(160,80,0),
                new rgb( 51, 153, 51),
                new rgb( 162, 193, 57),
                new rgb( 216, 0, 115),
                new rgb( 240, 150, 9),
                new rgb( 230, 113, 184),
                new rgb( 162, 0, 255),
                new rgb( 229, 20, 0),
                new rgb( 0, 171, 169),
                new rgb(145, 149, 246),
                new rgb(165, 221, 155),
                new rgb(197, 235, 170),
                new rgb(205, 250, 219),
                new rgb(220, 255, 183),
                new rgb(242, 193, 141),
                new rgb(244, 83, 138),
                new rgb(245, 221, 97),
                new rgb(246, 241, 147),
                new rgb(246, 253, 195),
                new rgb(249, 240, 122),
                new rgb(250, 163, 0),
                new rgb(251, 136, 180),
                new rgb(255, 104, 104),
                new rgb(255, 128, 128),
                new rgb(255, 187, 100),
                new rgb(255, 207, 150),
                new rgb(255, 234, 167),
                new rgb(89, 213, 224),
            }.ConvertAll(cb);
            var ngLst = new List<rgb> {
                new rgb(0, 0, 255),
                    new rgb(0, 226, 0),
                    new rgb(255,20,147),
                    new rgb( 162, 0, 255),
                    new rgb( 0, 171, 169),
                    new rgb(255, 233, 71),
                    new rgb(255, 102, 0),

                    new rgb(27,161,226),
                    new rgb(160,80,0),
                    new rgb( 51, 153, 51),
                    new rgb( 240, 150, 9),
                    new rgb(148,0,211),
                    new rgb( 230, 113, 184),
                    new rgb(0, 255, 255),
                    new rgb(139,69,19),
                    new rgb( 162, 193, 57),
                    new rgb(255,182,193),
                    new rgb(218,165,32),
                    new rgb(32,178,170),

                    new rgb( 229, 20, 0),
                    new rgb(255, 187, 92),
                    new rgb(255,127,80),
                    new rgb(255, 0, 255),
                    new rgb(153, 0, 0),
                    new rgb( 216, 0, 115),
                    new rgb(144, 80, 255),
                    new rgb(139,0,139),
                    new rgb(218,165,32),
                    new rgb(226, 94, 62) ,
                    new rgb(148, 78, 99),
                    new rgb(106, 156, 137),
                    new rgb(152,251,152),
                    new rgb(145, 149, 246),
                new rgb(255, 187, 92),
                new rgb(255, 155, 80),
                new rgb(226, 94, 62) ,
                new rgb(198, 61, 47) ,
                new rgb(205, 92, 8),
                new rgb(245, 232, 183),
                new rgb(193, 216, 195),
                new rgb(106, 156, 137),
                new rgb(153, 0, 0),
                new rgb(255, 102, 0),
                new rgb(193, 211, 67),
                new rgb(247, 247, 207),
                new rgb(230, 179, 37),
                new rgb(148, 78, 99),
                new rgb(180, 123, 132),
                new rgb(202, 166, 166),
                new rgb(255, 231, 231),
                new rgb(237, 198, 177),
            }.ConvertAll(cb);
            var d = new Dictionary<string, Brush>();
            var okColorIndex = 0;
            var ngColorIndex = 0;
            foreach (var p in lcfg._dict)
            {
                if (Regex.IsMatch(p.Value, "^ng_"))
                {
                    d.Add(p.Value, ngLst[ngColorIndex % ngLst.Count]);
                    ngColorIndex++;
                }
                else
                {
                    d.Add(p.Value, okLst[okColorIndex % okLst.Count]);
                    okColorIndex++;
                }
            }
            lcfg._dict2.ToList().ForEach(p => d[p.Value] = d[p.Key]);
            this.d = d;
        }

        public Brush GetColor(string name)
        {
            if (d.ContainsKey(name))
                return d[name];

            return Brush.Red;
        }
    }
}
