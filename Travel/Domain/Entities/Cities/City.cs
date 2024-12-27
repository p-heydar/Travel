namespace Travel.Domain.Entities.Cities;

public record City(string name, long population, string? country=null, string? livingCost=null,
    int? numberOfAirports=null, string? localLanguage=null, string? peakTravelTime=null,
    string? transportation=null);