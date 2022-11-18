#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8629 //All Attributes have to be present
using Logging;
using OSM_Landmarks;
using System.Xml;

namespace OSM_XML_Importer
{
    public class LandmarksImporter
    {
        private static XmlReaderSettings readerSettings = new()
        {
            IgnoreWhitespace = true,
            IgnoreComments = true
        };


        public static Landmarks Import(string filePath = "", Logger? logger = null)
        {
            Stream mapData = File.Exists(filePath) ? new FileStream(filePath, FileMode.Open, FileAccess.Read) : new MemoryStream(OSM_Data.map);

            Dictionary<ulong, Address> addresses = new Dictionary<ulong, Address>();
            List<Address> ret = new List<Address>();

            XmlReader _reader = XmlReader.Create(mapData, readerSettings);

            while (_reader.ReadToFollowing("way"))
            {
                Address currentAddress = new Address();
                XmlReader wayReader = _reader.ReadSubtree();
                while (wayReader.Read())
                {
                    if (wayReader.Name == "nd" && currentAddress.locationId == null)
                    {
                        currentAddress.locationId = Convert.ToUInt64(wayReader.GetAttribute("ref"));
                    }
                    else if (wayReader.Name == "tag")
                    {
                        string key = (string)wayReader.GetAttribute("k");
                        string value = (string)wayReader.GetAttribute("v");
                        switch (key)
                        {
                            case "addr:street":
                            case "addr:conscriptionnumber":
                            case "addr:place":
                                currentAddress.street = value;
                                break;
                            case "addr:housenumber":
                            case "addr:housename":
                            case "addr:flats":
                                currentAddress.house = value;
                                break;
                            case "addr:postcode":
                                currentAddress.zipCode = value;
                                break;
                            case "addr:city":
                                currentAddress.city = value;
                                break;
                            case "addr:country":
                                currentAddress.country = value;
                                break;
                        }
                    }
                }
                if (currentAddress.street != null)
                {
                    addresses.Add((ulong)currentAddress.locationId, currentAddress);
                }
            }

            mapData.Position = 0;
            _reader = XmlReader.Create(mapData, readerSettings);

            while (_reader.ReadToFollowing("node"))
            {
                Address currentAddress = new Address();
                currentAddress.locationId = Convert.ToUInt64(_reader.GetAttribute("id"));
                currentAddress.lat = Convert.ToSingle(_reader.GetAttribute("lat").Replace('.', ','));
                currentAddress.lon = Convert.ToSingle(_reader.GetAttribute("lon").Replace('.', ','));
                XmlReader nodeReader = _reader.ReadSubtree();
                while (nodeReader.ReadToFollowing("tag"))
                {
                    string key = (string)nodeReader.GetAttribute("k");
                    string value = (string)nodeReader.GetAttribute("v");
                    switch (key)
                    {
                        case "addr:street":
                        case "addr:conscriptionnumber":
                        case "addr:place":
                            currentAddress.street = value;
                            break;
                        case "addr:housenumber":
                        case "addr:housename":
                        case "addr:flats":
                            currentAddress.house = value;
                            break;
                        case "addr:postcode":
                            currentAddress.zipCode = value;
                            break;
                        case "addr:city":
                            currentAddress.city = value;
                            break;
                        case "addr:country":
                            currentAddress.country = value;
                            break;
                    }
                }
                if (addresses.ContainsKey((ulong)currentAddress.locationId))
                {
                    Address mod = addresses[(ulong)currentAddress.locationId];
                    mod.lat = currentAddress.lat;
                    mod.lon = currentAddress.lon;
                    ret.Add(mod);
                    addresses.Remove((ulong)currentAddress.locationId);
                }
                else if (currentAddress.street != null)
                {
                    ret.Add(currentAddress);
                }
            }

            return new Landmarks(ret);
        }
    }
}
