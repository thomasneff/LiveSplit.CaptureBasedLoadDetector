using LiveSplit.Model;
using LiveSplit.PokemonRedBlue;
using LiveSplit.UI.Components;
using System;

[assembly: ComponentFactory(typeof(CaptureBasedLoadDetectorFactory))]

namespace LiveSplit.PokemonRedBlue
{
    public class CaptureBasedLoadDetectorFactory : IComponentFactory
    {
        public string ComponentName
        {
            get { return "Capture-Based Load Detector"; }
        }

        public ComponentCategory Category
        {
            get { return ComponentCategory.Control; }
        }

        public string Description
        {
            get { return "Automatically detects and removes loads (GameTime) for games using image capture."; }
        }

        public IComponent Create(LiveSplitState state)
        {
            return new CaptureBasedLoadDetectorComponent(state);
        }

        public string UpdateName
        {
            get { return ComponentName; }
        }
		public string UpdateURL => "https://raw.githubusercontent.com/thomasneff/LiveSplit.CaptureBasedLoadDetector/master/";
		public string XMLURL => UpdateURL + "update.LiveSplit.CaptureBasedLoadDetector.xml";
		

        public Version Version
        {
            get { return Version.Parse("1.0"); }
        }
    }
}
