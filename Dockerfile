FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .
# Expose port 80 for HTTP traffic
EXPOSE 80
# Set the entrypoint to run the app
ENTRYPOINT ["dotnet", "appointly.dll"]
