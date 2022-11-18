using Logging;

namespace OSM_XML_Importer
{
    struct Way
    {
        public List<ulong> nodeIds;
        private Dictionary<string, object> tags;


        public Dictionary<type, int> speedcar = new() {
                { type.NONE, 0 },
                { type.motorway, 110 },
                { type.trunk, 100 },
                { type.primary, 80 },
                { type.secondary, 80 },
                { type.tertiary, 70 },
                { type.unclassified, 20 },
                { type.residential, 10 },
                { type.motorway_link, 50 },
                { type.trunk_link, 50 },
                { type.primary_link, 30 },
                { type.secondary_link, 25 },
                { type.tertiary_link, 25 },
                { type.living_street, 10 },
                { type.service, 0 },
                { type.pedestrian, 0 },
                { type.track, 0 },
                { type.bus_guideway, 0 },
                { type.escape, 0 },
                { type.raceway, 0 },
                { type.road, 25 },
                { type.busway, 0 },
                { type.footway, 0 },
                { type.bridleway, 0 },
                { type.steps, 0 },
                { type.corridor, 0 },
                { type.path, 0 },
                { type.cycleway, 0 },
                { type.construction, 0 }
            };

        public Dictionary<type, int> speedped = new() {
                { type.NONE, 0 },
                { type.motorway, 0 },
                { type.trunk, 0 },
                { type.primary, 0 },
                { type.secondary, 0 },
                { type.tertiary, 0 },
                { type.unclassified, 1 },
                { type.residential, 3 },
                { type.motorway_link, 0 },
                { type.trunk_link, 0 },
                { type.primary_link, 0 },
                { type.secondary_link, 0 },
                { type.tertiary_link, 0 },
                { type.living_street, 5 },
                { type.service, 2 },
                { type.pedestrian, 5 },
                { type.track, 0 },
                { type.bus_guideway, 0 },
                { type.escape, 0 },
                { type.raceway, 0 },
                { type.road, 3 },
                { type.busway, 0 },
                { type.footway, 4 },
                { type.bridleway, 1 },
                { type.steps, 2 },
                { type.corridor, 3 },
                { type.path, 4 },
                { type.cycleway, 2 },
                { type.construction, 0 }
            };
        public enum type { NONE, motorway, trunk, primary, secondary, tertiary, unclassified, residential, motorway_link, trunk_link, primary_link, secondary_link, tertiary_link, living_street, service, pedestrian, track, bus_guideway, escape, raceway, road, busway, footway, bridleway, steps, corridor, path, cycleway, construction }


        public Way()
        {
            this.nodeIds = new List<ulong>();
            this.tags = new();
        }
        public void AddTag(string key, string value, Logger? logger = null)
        {
            switch (key)
            {
                case "highway":
                    try
                    {
                        this.tags.Add(key, (type)Enum.Parse(typeof(type), value, true));
                        if (this.GetMaxSpeed().Equals((int)type.NONE))
                        {
                            this.tags["maxspeed"] = (int)this.GetHighwayType();
                        }
                    }
                    catch (ArgumentException)
                    {
                        this.tags.Add(key, type.NONE);
                    }
                    break;
                case "maxspeed":
                    try
                    {
                        if (this.tags.ContainsKey("maxspeed"))
                            this.tags["maxspeed"] = Convert.ToInt32(value);
                        else
                            this.tags.Add(key, Convert.ToInt32(value));
                    }
                    catch (FormatException)
                    {
                        this.tags.Add(key, (int)this.GetHighwayType());
                    }
                    break;
                case "oneway":
                    switch (value)
                    {
                        case "yes":
                            this.tags.Add(key, true);
                            break;
                        case "-1":
                            this.tags.Add("forward", false);
                            break;
                        case "no":
                            this.tags.Add(key, false);
                            break;
                    }
                    break;
                case "id":
                    this.tags.Add(key, Convert.ToUInt64(value));
                    break;
                default:
                    logger?.Log(LogLevel.VERBOSE, "Tag {0} - {1} was not added.", key, value);
                    break;
            }
        }

        public ulong GetId()
        {
            return this.tags.ContainsKey("id") ? (ulong)this.tags["id"] : 0;
        }

        public type GetHighwayType()
        {
            return this.tags.ContainsKey("highway") ? (type)this.tags["highway"] : type.NONE;
        }

        public bool IsOneWay()
        {
            return this.tags.ContainsKey("oneway") ? (bool)this.tags["oneway"] : false;
        }

        public int GetMaxSpeed()
        {
            return this.tags.ContainsKey("maxspeed") ? (int)this.tags["maxspeed"] : (int)this.GetHighwayType();

        }

        public bool IsForward()
        {
            return this.tags.ContainsKey("forward") ? (bool)this.tags["forward"] : true;
        }
    }
}
