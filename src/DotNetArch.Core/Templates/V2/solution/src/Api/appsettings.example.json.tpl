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
  "Cors": {
    "AllowedOrigins": [ "https://app.example.com" ]
  }
}
