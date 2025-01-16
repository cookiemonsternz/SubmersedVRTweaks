using Nautilus.Json;
using Nautilus.Options;
using Nautilus.Options.Attributes;

namespace SubmersedVRTweaks
{
    [Menu("Submersed Tweaks")]
    public class ModOptions: ConfigFile
    {
        [Toggle("Steer with hand for Seaglide")]
        public bool SteerWithHandForSeaglide = true;
        [Toggle("Steer with hand underwater")]
        public bool SteerWithHandUnderWater = true;
    }
}