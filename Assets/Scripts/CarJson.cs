using System;

namespace TopDownRace {
    [Serializable]
    public class CarJson {
        public string id;
        public string nickname;
        public string shortName;
        public string image;  // filename in Resources/Cars
        public string icon;   // filename in Resources/Icons
        public string countryFlag; // filename in Resources/Flags
    }

    [Serializable]
    public class CarJsonList {
        public CarJson[] cars;
    }
}
