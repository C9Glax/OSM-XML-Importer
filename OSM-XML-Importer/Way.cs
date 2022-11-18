using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSM_XML_Importer
{
    partial class Importer
    {
        internal struct Way
        {
            public List<ulong> nodeIds;
            private Dictionary<string, object> tags;


            public Dictionary<type, int> speed = new() {
                { type.NONE, 1 },
                { type.motorway, 130 },
                { type.trunk, 125 },
                { type.primary, 110 },
                { type.secondary, 100 },
                { type.tertiary, 90 },
                { type.unclassified, 40 },
                { type.residential, 20 },
                { type.motorway_link, 50 },
                { type.trunk_link, 50 },
                { type.primary_link, 30 },
                { type.secondary_link, 25 },
                { type.tertiary_link, 25 },
                { type.living_street, 20 },
                { type.service, 10 },
                { type.pedestrian, 10 },
                { type.track, 1 },
                { type.bus_guideway, 5 },
                { type.escape, 1 },
                { type.raceway, 1 },
                { type.road, 25 },
                { type.busway, 5 },
                { type.footway, 1 },
                { type.bridleway, 1 },
                { type.steps, 1 },
                { type.corridor, 1 },
                { type.path, 10 },
                { type.cycleway, 5 },
                { type.construction, 1 }
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
}
