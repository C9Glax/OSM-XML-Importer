using System.Globalization;

namespace OSM_XML_Importer;

public static class Util
{
    
    public static long GetRegionId(float lat, float lon, float _regionSize)
    {
        double latMult = Math.Floor(lat / _regionSize);
        double lonMult = Math.Floor(lon / _regionSize);

        string r = $"{latMult:00000}{lonMult:00000}".Replace(".", "").Replace(",","");
        return long.Parse(r);
    } 
    
    public static long GetRegionId(string lat, string lon, float _regionSize)
    {
        float flat = float.Parse(lat, NumberStyles.Float, NumberFormatInfo.InvariantInfo);
        float flon = float.Parse(lon, NumberStyles.Float, NumberFormatInfo.InvariantInfo);

        return GetRegionId(flat, flon, _regionSize);
    }
}