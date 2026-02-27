using MhpdCommon.Extensions;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Models.OpenApi;
using MhpdCommon.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using PensionsRetrievalFunction.Orchestration;
using PensionsRetrievalFunction.Repository;
using System.Reflection;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(builder.Configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddApplicationInsightsTelemetryWorkerService();
}

builder.Services.AddMhpdCosmosDb(builder.Configuration);
builder.Services.AddMhpdUtilities(builder.Configuration);
builder.Services.AddMhpdHttpClients(builder.Configuration);
builder.Services.AddIntegrationServices();
builder.Services.AddMhpdServiceBusTools(builder.Configuration);
builder.Services.AddCommonConfigurations(builder.Configuration);

builder.Services.AddScoped<IPensionRetrievalRepository, PensionRetrievalRepository>();
builder.Services.AddTransient<IPeiIntegrationOrchestrator, PeiIntegrationOrchestrator>();
builder.Services.AddTransient<ICosmosDbRepository<UserSessionData>, UserSessionDataRepository>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations();
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MaPS Pensions Retrieval Records",
        Description = "This service allows a client to retrieve pensions retrieval records for a pension owner session",
        Contact = new OpenApiContact
        {
            Name = "General Enquires",
            Email = "contact@maps.org.uk",
            Url = new Uri("https://maps.org.uk/en/about-us/contact-us")
        },
        License = new OpenApiLicense
        {
            Name = "Government API License",
            Url = new Uri("https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/")
        },
    });
    c.DocumentFilter<PensionDataOpenApiFilter>(Assembly.GetExecutingAssembly());
    c.DocumentFilter<PensionDataOpenApiFilter>(Assembly.GetExecutingAssembly());
    c.AddServer(new OpenApiServer
    {
        Url = builder.Configuration.GetValue<string>("OpenApiServerUrl") ?? "http://localhost:7123"
    });
});

builder
    .ConfigureAspNetCoreMvcIntegration(mvcBuilder =>
    {
        mvcBuilder.AddMvcOptions(mvcOptions => { });
    })
    .UseAspNetCoreMiddleware(app =>
    {
        app.UseFunctionSwaggerUI();

        app.UseSwagger(c => c.OpenApiVersion = OpenApiSpecVersion.OpenApi2_0);
        app.UseSwaggerUI();
    });

var app = builder.Build();
await app.RunAsync();