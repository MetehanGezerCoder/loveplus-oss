namespace LovePlus.Application.Realtime;

public sealed class GeoDistanceCalculator : IGeoDistanceCalculator
{
    private const double EarthRadiusMeters = 6_371_008.8;

    public double CalculateMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var lat1 = DegreesToRadians(latitude1);
        var lat2 = DegreesToRadians(latitude2);
        var deltaLat = DegreesToRadians(latitude2 - latitude1);
        var deltaLon = DegreesToRadians(longitude2 - longitude1);
        var a = Math.Pow(Math.Sin(deltaLat / 2), 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
