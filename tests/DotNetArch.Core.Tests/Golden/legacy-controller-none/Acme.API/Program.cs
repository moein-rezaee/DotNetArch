using Acme.Infrastructure;
using Acme.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using DotNetEnv;
using System.IO;
using System;
var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "development";
var builder = WebApplication.CreateBuilder(args);
DotNetEnv.Env.Load(Path.Combine(Directory.GetCurrentDirectory(), "config", "env", $".env.{env.ToLower()}"));
builder.Configuration.AddJsonFile(Path.Combine("config", "settings", $"appsettings.{env.ToLower()}.json"), optional: true, reloadOnChange: true);
builder.Services.AddControllers();
builder.Services.AddApplication();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();



app.MapControllers();
app.Run();
