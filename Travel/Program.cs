using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using Scalar.AspNetCore;
using Travel.Domain.Entities.Cities;
using Path = System.IO.Path;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

#region Config Neo4j

builder.Services.AddSingleton<IDriver>(services =>
{
    return GraphDatabase.Driver(uri: "bolt://localhost:7687",
        authToken: AuthTokens.Basic("neo4j", "admin123!"));
});
#endregion

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPost("/City",async ([FromBody] List<City> cities, IDriver driver, CancellationToken cancellationToken) =>
{
    await using var session = driver.AsyncSession();

    foreach (City city in cities)
    {
        var insertCommand = @"

                            CREATE (city:City {
                                name: $name,
                                population: $population,
                                country: $country,
                                livingCost: $livingCost,
                                numberOfAirports: $numberOfAirports,
                                localLanguage: $localLanguage,
                                peakTravelTime: $peakTravelTime,
                                transportation: $transportation
                            })
                          ";
        await session.RunAsync(insertCommand, new
        {
            name = city.name,
            population = city.population,
            country = city.country,
            livingCost = city.livingCost,
            numberOfAirports = city.numberOfAirports,
            localLanguage = city.localLanguage,
            peakTravelTime = city.peakTravelTime,
            transportation = city.transportation
        });

    }
    

    await session.DisposeAsync();
});


app.MapGet("/City", async (IDriver driver, string filter) =>
{
    await using var session = driver.AsyncSession();
    var getAllQuery = @"
                        MATCH (city:City)
                        WHERE
                            city.name CONTAINS $filter
                        RETURN 
                            city.name AS name, 
                            city.population AS population,
                            city.country AS country,
                            city.livingCost AS livingCost,
                            city.numberOfAirports AS numberOfAirports,
                            city.localLanguage AS localLanguage,
                            city.peakTravelTime AS peakTravelTime,
                            city.transportation AS transportation
                      ";
    var result = await session.RunAsync(getAllQuery, new
    {
        filter=filter
    });

    List<City> cities = new List<City>();
    
    await result.ForEachAsync(record =>
    {
        cities.Add(new(record["name"] as string,
            long.Parse(record["population"].ToString()),
            record["country"] as string,
            record["livingCost"] as string,
            int.Parse(record["numberOfAirports"].ToString()),
            record["localLanguage"] as string,
            record["peakTravelTime"] as string,
            record["transportation"] as string));
    });
    
    return Results.Ok(cities);

});

app.MapPost("/Path",
    async (IDriver driver, string source, string destination, int distance) =>
    {
        var addPathCommand = @"
                               MATCH (source:City{name:$source}), (destination:City{name:$destination})
                               MERGE (source)-[road:Road{distance:$distance}]->(destination)
                           ";

        await using var session = driver.AsyncSession();
        session.RunAsync(addPathCommand, new
        {
            source = source, destination = destination, distance = distance
        });
        return Results.Ok();
    });


app.MapGet("/Path", async (IDriver driver) =>
{
    await using var session = driver.AsyncSession();
    var getAllQuery = @"
                        MATCH (source:City)-[road:Road]->(destination:City)
                        RETURN source.name AS SourceName, road.distance As Distance, destination.name AS DestinationName
                      ";
    var result = await session.RunAsync(getAllQuery);

    List<TravelPath> travelPaths = new List<TravelPath>();
    
    await result.ForEachAsync(record =>
    {
        travelPaths.Add(new TravelPath(record["SourceName"].As<string>(),
                record["Distance"].As<int>(),
                record["DestinationName"].As<string>()));
    });
    
    return Results.Ok(travelPaths);
});

app.Run();
