using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using Half = SystemHalf.Half;

namespace OSM_XML_Importer;

public class OSMFileSplitter
{
    
    /*
     * Region naming format:
     * Latitude/Longitude.dat
     * Latitude/Longitude are modulo regionSize, e.g. regionSize = 0.01 Lat = 34.345 => LatMod = 34.34
     */
    
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        IgnoreWhitespace = true,
        IgnoreComments = true
    };
    
    private string NodesMapFile => Path.Join(_nodesDirectory, _regionSize.ToString(), "NodesMapFile");
    private string WaysMapFile => Path.Join(_waysDirectory, _regionSize.ToString(), "WaysMapFile");
    private string NodesDirectory => Path.Join(_nodesDirectory, _regionSize.ToString(), "nodes");
    private string WaysDirectory => Path.Join(_waysDirectory, _regionSize.ToString(), "ways");
    private string _nodesDirectory, _waysDirectory;
    private ILogger? _logger;
    private Half _regionSize;

    public void SplitFileIntoRegions(Half regionSize, string? filePath = null, string? nodesDirectory = null, string? waysDirectory = null, bool filterHighways = false, ILogger? logger = null)
    {
        _logger = logger;
        this._nodesDirectory = nodesDirectory ?? Environment.CurrentDirectory;
        this._waysDirectory = waysDirectory ?? Environment.CurrentDirectory;

        this._regionSize = regionSize;
        
        _logger?.LogInformation($"Input: {filePath} Output-Nodes: {NodesDirectory} Output-Ways: {WaysDirectory}");
        
        _logger?.LogDebug("Opening File...");
        Stream mapData = filePath is not null && File.Exists(filePath) ? new FileStream(filePath, FileMode.Open, FileAccess.Read) : new MemoryStream(OSM_Data.map);

        Nodes(mapData);
        Ways(mapData, filterHighways);
        CleanUnusedNodes();
    }
    
    /*
     * Node-File format
     * ID-Latitude-Longitude\n
     */
    private void Nodes(Stream mapData)
    {
        _logger?.LogDebug("Splitting Nodes...");
        Dictionary<long, FileStream> nodesRegionFileStreams = new();

        if (!Directory.Exists(NodesDirectory))
            Directory.CreateDirectory(NodesDirectory);
        
        FileStream nodesMapFileStream = new(NodesMapFile, FileMode.Create, FileAccess.Write);
        mapData.Position = 0;
        XmlReader reader = XmlReader.Create(mapData, ReaderSettings);
        reader.MoveToContent();
        
        while (reader.ReadToFollowing("node"))
        {
            string? id = reader.GetAttribute("id");
            string? lat = reader.GetAttribute("lat");
            string? lon = reader.GetAttribute("lon");
            if(id is null || lat is null || lon is null)
                continue;
            long regionFileStream = GetRegionFileStream(lat, lon, ref nodesRegionFileStreams);
            FileStream f = nodesRegionFileStreams[regionFileStream];
            //ID-Latitude-Longitude\n
            string line = $"{id}-{lat}-{lon}\n";
            f.Write(Encoding.ASCII.GetBytes(line));

            //nodeId-{regionId}\n
            string map = $"{id}-{regionFileStream}\n";
            nodesMapFileStream.Write(Encoding.ASCII.GetBytes(map));
            _logger?.LogTrace($"{line} -> {regionFileStream} = {Path.Join(NodesDirectory, regionFileStream.ToString())}");
        }
        foreach(FileStream fs in nodesRegionFileStreams.Values)
            fs.Close();
        nodesMapFileStream.Close();
    }

    private long GetRegionFileStream(string lat, string lon, ref Dictionary<long, FileStream> nodesRegionFileStreams)
    {
        float flat = float.Parse(lat, NumberStyles.Float, NumberFormatInfo.InvariantInfo);
        float flon = float.Parse(lon, NumberStyles.Float, NumberFormatInfo.InvariantInfo);

        double latMult = Math.Floor(flat / _regionSize);
        double lonMult = Math.Floor(flon / _regionSize);

        string r = $"{latMult:00000}{lonMult:00000}".Replace(".", "").Replace(",","");
        long ret = long.Parse(r);
        if(!nodesRegionFileStreams.ContainsKey(ret))
            nodesRegionFileStreams.Add(ret, new FileStream(Path.Join(NodesDirectory, r), FileMode.Create, FileAccess.Write));

        return ret;
    }
    
    /*
     * Way-File format
     * ID-{nodeId,}+-{tagkey|tagvalue,}+\n
     */
    private void Ways(Stream mapData, bool filterToHighways = false)
    {
        _logger?.LogDebug("Splitting Ways...");
        Dictionary<string, FileStream> waysRegionFileStreams = new();
        
        if (!Directory.Exists(WaysDirectory))
            Directory.CreateDirectory(WaysDirectory);
        
        FileStream waysMapFileStream = new(WaysMapFile, FileMode.Create, FileAccess.Write);
        Dictionary<string, string> nodeIdMap = GetNodeIdMap();
        mapData.Position = 0;
        XmlReader reader = XmlReader.Create(mapData, ReaderSettings);
        reader.MoveToContent();
        
        while (reader.ReadToFollowing("way"))
        {
            string? id = reader.GetAttribute("id");
            List<string> nodeIds = new();
            Dictionary<string, string> tags = new();
            using (XmlReader wayReader = reader.ReadSubtree())
            {
                while (wayReader.Read())
                {
                    if (reader.Name == "tag")
                    {
                        string? key = reader.GetAttribute("k");
                        string? value = reader.GetAttribute("v");
                        if(value is null || key is null)
                            continue;
                        tags.Add(key, value);
                    }
                    else if (reader.Name == "nd")
                    {
                        string? nodeId = reader.GetAttribute("ref");
                        if(nodeId is null)
                            continue;
                        nodeIds.Add(nodeId);
                    }
                }
            }
            if(id is null)
                continue;
            if(filterToHighways && !tags.ContainsKey("highway"))
                continue;//We are filtering all ways that aren't highways
            
            //ID-{nodeId,}+-{tagkey|tagvalue,}+\n
            string line = $"{id}-{string.Join(',',nodeIds)}-{string.Join(',', tags.Select(t => $"{t.Key}|{t.Value}"))}\n";
            List<string> regionIds = nodeIds.Select(nId => nodeIdMap[nId]).Distinct().ToList();
            foreach (string regionId in regionIds)
            {
                if(!waysRegionFileStreams.ContainsKey(regionId))
                    waysRegionFileStreams.Add(regionId, new FileStream(Path.Join(WaysDirectory, regionId), FileMode.Create, FileAccess.Write));
                FileStream f = waysRegionFileStreams[regionId];
                f.Write(Encoding.UTF8.GetBytes(line));
                _logger?.LogTrace($"{line} -> {regionId} = {Path.Join(WaysDirectory, regionId)}");
            }
            
            //wayId-{regionId,}+\n
            string map = $"{id}-{string.Join(',', regionIds)}\n";
            waysMapFileStream.Write(Encoding.ASCII.GetBytes(map));
        }
        foreach(FileStream fs in waysRegionFileStreams.Values)
            fs.Close();
        waysMapFileStream.Close();
    }

    private Dictionary<string, string> GetNodeIdMap()
    {
        Dictionary<string, string> ret = new();
        using (FileStream f = new(NodesMapFile, FileMode.Open, FileAccess.Read))
        {
            using (StreamReader s = new(f))
            {
                while (!s.EndOfStream)
                {
                    string? line = s.ReadLine();
                    if(line is null)
                        continue;
                    string[] split = line.Split('-');
                    if(split.Length != 2)
                        continue;
                    ret.Add(split[0], split[1]);
                }
            }
        }
        return ret;
    }

    /*
     * If we filtered to highways, not all nodes will be used
     * Also 
     */
    private void CleanUnusedNodes()
    {
        _logger?.LogInformation("Removing unnecessary nodes from regions...");
        string[] regionFiles = Directory.GetFiles(WaysDirectory);
        
        File.Copy(NodesMapFile, $"{NodesMapFile}.bak", true);
        Dictionary<string, string> nodesMap = GetNodeIdMap();
        string newNodesMapFile = $"{NodesMapFile}.new";
        FileStream nodesMapFileStream = new(newNodesMapFile, FileMode.Create, FileAccess.Write);
        
        foreach (string region in regionFiles)
        {
            FileInfo fi = new (region);
            string regionId = fi.Name;
            string nodeRegionFile = Path.Join(NodesDirectory, regionId);
            
            if(!File.Exists(nodeRegionFile))
                continue;
            
            HashSet<string> nodeIds = new(); //All the nodeIds in the region of the way
            using (FileStream fs = new (region, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader sr = new(fs))
                {
                    while (!sr.EndOfStream)
                    {
                        string? line = sr.ReadLine();
                        if(line is null)
                            continue;
                        //ID-{nodeId,}+-{tagkey|tagvalue,}+\n
                        string[] split = line.Split('-');
                        if(split.Length != 3)
                            continue;
                        string[] ids = split[1].Split(',');
                        foreach (string id in ids)
                            nodeIds.Add(id);
                    }
                }
            }
            _logger?.LogDebug($"Region {regionId}\n" +
                              $"\tIds:\t{string.Join("\n\t\t", nodeIds)}");
            
            File.Copy(nodeRegionFile, $"{nodeRegionFile}.bak", true);
            string newNodeFile = $"{nodeRegionFile}.new";
            using (FileStream fs = new(nodeRegionFile, FileMode.Open, FileAccess.Read))
            {
                using (FileStream fsn = new(newNodeFile, FileMode.Create, FileAccess.Write))
                {
                    using (StreamReader sr = new(fs))
                    {
                        while (!sr.EndOfStream)
                        {
                            string? line = sr.ReadLine();
                            if(line is null)
                                continue;
                            //ID-Latitude-Longitude\n
                            string[] split = line.Split('-');
                            if(split.Length != 3)
                                continue;
                            string id = split[0];
                            if(nodeIds.Contains(id))
                                fsn.Write(Encoding.ASCII.GetBytes(line));
                            else
                                _logger?.LogTrace($"Region {regionId} removed Node {id}");
                        }
                    }
                }
            }
            if(new FileInfo(newNodeFile).Length > 0)
                File.Move(newNodeFile, nodeRegionFile, true);
            else
                File.Delete(newNodeFile);

            foreach (string nodeId in nodeIds)
            {
                if (nodesMap.ContainsKey(nodeId))
                {
                    //nodeId-{regionId}\n
                    string line = $"{nodeId}-{regionId}\n";
                    nodesMapFileStream.Write(Encoding.ASCII.GetBytes(line));
                    nodesMap.Remove(nodeId);
                }
            }
        }
        nodesMapFileStream.Close();
        File.Move(newNodesMapFile, NodesMapFile, true);
    }

    public void CleanBakFiles()
    {
        _logger?.LogInformation("Deleting Backup files...");
        string[] nodeFiles = Directory.GetFiles(NodesDirectory, "*.bak");
        string[] wayFiles = Directory.GetFiles(WaysDirectory, "*.bak");
        foreach (string file in nodeFiles.Union(wayFiles))
        {
            _logger?.LogDebug($"Deleting {file}");
            File.Delete(file);
        }
    }
}