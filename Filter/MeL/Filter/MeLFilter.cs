using System.Numerics;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using VoiDPlugins.Filter.MeL.Core;

namespace VoiDPlugins.Filter.MeL
{
    [PluginName("MeL")]
    public class MeLFilter : IFilter
    {
        public Vector2 Filter(Vector2 point)
        {
            Core.Add(point);
            try
            {
                var a = Core.IsReady ? Core.Predict(Offset) : point;
                rateLimit = false;
                return a;
            }
            catch
            {
                if (!rateLimit)
                {
                    Log.Write("MeLFilter", "Unknown error in MeLCore", LogLevel.Error);
                    rateLimit = true;
                }
                return point;
            }
        }

        public FilterStage FilterStage => FilterStage.PostTranspose;

        [Property("Offset"), Unit("ms"), DefaultPropertyValue(0)]
        public float Offset { set; get; }

        [Property("Samples"), DefaultPropertyValue(20)]
        public int Samples { set => Core.Samples = value; }

        [Property("Complexity"), DefaultPropertyValue(2)]
        public int Complexity { set => Core.Complexity = value; }

        [Property("Weight"), DefaultPropertyValue(1.4f)]
        public float Weight { set => Core.Weight = value; }

        private readonly MLCore Core = new MLCore();
        private bool rateLimit;
    }
}