using Newtonsoft.Json;
using Repositories;

namespace Models
{
    public class Part : IEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; } = RepositoryHelper.NewId();
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("posX")]
        public int? PosX { get; set; }
        [JsonProperty("posY")]
        public int? PosY { get; set; }
        [JsonProperty("posW")]
        public int? PosW { get; set; }
        [JsonProperty("posH")]
        public int? PosH { get; set; }
        [JsonProperty("percent")]
        public int? Percent { get; set; }
        [JsonProperty("judge")]
        public bool? Judge { get; set; }
        [JsonProperty("panelId")]
        public string PanelId { get; set; }
        //virtual public Panel Panel { get; set; }

        public Rect Pos
        {
            set
            {
                if (value == null) return;
                PosX = value.X;
                PosY = value.Y;
                PosW = value.Width;
                PosH = value.Height;
            }
            get
            {
                if (PosX == null || PosY == null || PosW == null || PosH == null) return null;
                return new Rect { X = PosX, Y = PosY, Width = PosW, Height = PosH };
            }
        }
        public class Rect
        {
            public int? X { get; set; }
            public int? Y { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
        }

    }
}
