{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware": "None"
    }
  },
  "AllowedHosts": "*",
  "Database": {
    "Provider": "{{Provider}}",
    "MigrateOnStartup": false
  },
  "Localization": {
    "DefaultCulture": "en",
    "SupportedCultures": [ "en", "fa" ]
  },
  "Cors": {
    "AllowedOrigins": []
  }
}
