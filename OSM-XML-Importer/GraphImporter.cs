#pragma warning disable CS8600, CS8601, CS8602, CS8604 //All Attributes have to be present
using GeoGraph;
using Logging;
using System.Xml;

namespace OSM_XML_Importer
{

    internal class GraphImporter
    {
        private static XmlReaderSettings readerSettings = new()
        {
            IgnoreWhitespace = true,
            IgnoreComments = true
        };
        public static Graph Import(string filePath = "", bool onlyJunctions = true, Logger? logger = null)
        {
            /*
             * Count Node occurances when tag is "highway"
             */
            logger?.Log(LogLevel.DEBUG, "Opening File...");
            Stream mapData = File.Exists(filePath) ? new FileStream(filePath, FileMode.Open, FileAccess.Read) : new MemoryStream(OSM_Data.map);
            logger?.Log(LogLevel.INFO, "Counting Node-Occurances...");
            Dictionary<ulong, ushort> occuranceCount = CountNodeOccurances(mapData, logger);
            logger?.Log(LogLevel.DEBUG, "Way Nodes: {0}", occuranceCount.Count);

            /*
             * Import Nodes and Edges
             */
            mapData.Position = 0;
            logger?.Log(LogLevel.INFO, "Importing Graph...");
            Graph _graph = CreateGraph(mapData, occuranceCount, onlyJunctions, logger);
            logger?.Log(LogLevel.DEBUG, "Loaded Nodes: {0}", _graph.GetNodeCount());

            return _graph;
        }

        private static Dictionary<ulong, ushort> CountNodeOccurances(Stream mapData, Logger? logger = null)
        {
            Dictionary<ulong, ushort> _occurances = new();

            XmlReader _reader = XmlReader.Create(mapData, readerSettings);
            XmlReader _wayReader;
            _reader.MoveToContent();

            bool _isHighway;
            List<ulong> _currentIds = new();

            while (_reader.ReadToFollowing("way"))
            {
                _isHighway = false;
                _currentIds.Clear();
                _wayReader = _reader.ReadSubtree();
                logger?.Log(LogLevel.VERBOSE, "WAY: {0}", _reader.GetAttribute("id"));
                while (_wayReader.Read())
                {
                    if (_reader.Name == "tag" && _reader.GetAttribute("k").Equals("highway"))
                    {
                        _isHighway = true;
                        logger?.Log(LogLevel.VERBOSE, "Highway: {0}", _reader.GetAttribute("v"));
                        /*
                        try
                        {
                            if (!Enum.Parse(typeof(Way.type), _reader.GetAttribute("v"), true).Equals(Way.type.NONE))
                                _isHighway = true;
                            logger?.Log(LogLevel.VERBOSE, "Highway: {0}", _reader.GetAttribute("v"));
                        }
                        catch (ArgumentException) { };*/
                    }
                    else if (_reader.Name == "nd")
                    {
                        try
                        {
                            _currentIds.Add(Convert.ToUInt64(_reader.GetAttribute("ref")));
                            logger?.Log(LogLevel.VERBOSE, "node-ref: {0}", _reader.GetAttribute("ref"));
                        }
                        catch (FormatException) { };
                    }
                }
                if (_isHighway)
                {
                    foreach (ulong _id in _currentIds)
                    {
                        if (!_occurances.TryAdd(_id, 1))
                            _occurances[_id]++;
                    }
                }
                _wayReader.Close();
            }
            _reader.Close();
            GC.Collect();

            return _occurances;
        }

        private static Graph CreateGraph(Stream mapData, Dictionary<ulong, ushort> occuranceCount, bool onlyJunctions, Logger? logger = null)
        {
            Graph _graph = new();
            Way _currentWay;
            Node _n1, _n2, _currentJunction;
            float _time, _distance = 0;

            XmlReader _reader = XmlReader.Create(mapData, readerSettings);
            XmlReader _wayReader;
            _reader.MoveToContent();

            while (_reader.Read())
            {
                if (_reader.Name == "node")
                {
                    ulong id = Convert.ToUInt64(_reader.GetAttribute("id"));
                    if (occuranceCount.ContainsKey(id))
                    {
                        float lat = Convert.ToSingle(_reader.GetAttribute("lat").Replace('.', ','));
                        float lon = Convert.ToSingle(_reader.GetAttribute("lon").Replace('.', ','));
                        _graph.AddNode(id, new Node(lat, lon));
                        logger?.Log(LogLevel.VERBOSE, "NODE {0} {1} {2} {3}", id, lat, lon, occuranceCount[id]);
                    }
                }
                else if (_reader.Name == "way")
                {
                    _wayReader = _reader.ReadSubtree();
                    _currentWay = new();
                    _currentWay.AddTag("id", _reader.GetAttribute("id"));
                    logger?.Log(LogLevel.VERBOSE, "WAY: {0}", _reader.GetAttribute("id"));
                    while (_wayReader.Read())
                    {
                        if (_reader.Name == "tag")
                        {
                            string _value = _reader.GetAttribute("v");
                            string _key = _reader.GetAttribute("k");
                            logger?.Log(LogLevel.VERBOSE, "TAG {0} {1}", _key, _value);
                            _currentWay.AddTag(_key, _value);
                        }
                        else if (_reader.Name == "nd")
                        {
                            ulong _id = Convert.ToUInt64(_reader.GetAttribute("ref"));
                            _currentWay.nodeIds.Add(_id);
                            logger?.Log(LogLevel.VERBOSE, "node-ref: {0}", _id);
                        }
                    }
                    _wayReader.Close();

                    if (!_currentWay.GetHighwayType().Equals(Way.type.NONE))
                    {
                        logger?.Log(LogLevel.VERBOSE, "WAY Nodes-count: {0} Type: {1}", _currentWay.nodeIds.Count, _currentWay.GetHighwayType());
                        if (!onlyJunctions)
                        {
                            for (int _nodeIdIndex = 0; _nodeIdIndex < _currentWay.nodeIds.Count - 1; _nodeIdIndex++)
                            {
                                _n1 = _graph.GetNode(_currentWay.nodeIds[_nodeIdIndex]);
                                _n2 = _graph.GetNode(_currentWay.nodeIds[_nodeIdIndex + 1]);

                                _distance = Convert.ToSingle(Utils.DistanceBetween(_n1, _n2));
                                _time = _distance / _currentWay.GetMaxSpeed();
                                if (!_currentWay.IsOneWay())
                                {
                                    _n1.edges.Add(new Edge(_n2, _time, _distance, _currentWay.GetId()));
                                    _n2.edges.Add(new Edge(_n1, _time, _distance, _currentWay.GetId()));
                                }
                                else if (_currentWay.IsForward())
                                {
                                    _n1.edges.Add(new Edge(_n2, _time, _distance, _currentWay.GetId()));
                                }
                                else
                                {
                                    _n2.edges.Add(new Edge(_n1, _time, _distance, _currentWay.GetId()));
                                }
                                logger?.Log(LogLevel.VERBOSE, "Add Edge: {0} & {1} Weight: {2}", _currentWay.nodeIds[_nodeIdIndex], _currentWay.nodeIds[_nodeIdIndex + 1], _time);
                            }
                        }
                        else
                        {
                            _currentJunction = _graph.GetNode(_currentWay.nodeIds[0]);
                            _n1 = _currentJunction;
                            for (int i = 1; i < _currentWay.nodeIds.Count; i++)
                            {
                                _n2 = _graph.GetNode(_currentWay.nodeIds[i]);
                                _distance += Convert.ToSingle(Utils.DistanceBetween(_n1, _n2));

                                if (occuranceCount[_currentWay.nodeIds[i]] > 1 || i == _currentWay.nodeIds.Count - 1) //Junction or end of way
                                {
                                    _time = _distance / _currentWay.GetMaxSpeed();
                                    if (!_currentWay.IsOneWay())
                                    {
                                        _currentJunction.edges.Add(new Edge(_n2, _time, _distance, _currentWay.GetId()));
                                        _n2.edges.Add(new Edge(_currentJunction, _time, _distance, _currentWay.GetId()));
                                    }
                                    else if (_currentWay.IsForward())
                                    {
                                        _currentJunction.edges.Add(new Edge(_n2, _time, _distance, _currentWay.GetId()));
                                    }
                                    else
                                    {
                                        _n2.edges.Add(new Edge(_currentJunction, _time, _distance, _currentWay.GetId()));
                                    }
                                    logger?.Log(LogLevel.VERBOSE, "Add Edge: {0} & {1} Weight: {2}", _currentJunction, _n2, _time);
                                    _currentJunction = _n2;
                                    _distance = 0;
                                }
                                else
                                {
                                    _graph.RemoveNode(_currentWay.nodeIds[i]);
                                }
                                _n1 = _n2;
                            }
                        }
                    }
                }
            }

            foreach (KeyValuePair<ulong, ushort> kv in occuranceCount)
            {
                if (kv.Value < 1)
                    _graph.RemoveNode(kv.Key);
            }

            _graph.Trim();

            _reader.Close();
            GC.Collect();
            return _graph;
        }
    }
}
